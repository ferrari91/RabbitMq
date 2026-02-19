using System.Text;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMq.Extensions;

namespace RabbitMq
{
    public abstract class QueueSubscriber<TModel> : BackgroundService where TModel : class
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Connection _connection;

    private Channel? _delayedChannel;
    private Channel? _deadChannel;
    private Channel? _mainChannelWrapper;

    protected abstract string Queue { get; }
    protected abstract string DelayedQueue { get; }
    protected abstract string DeadQueue { get; }

    protected virtual int RetryAttempts => 3;
    protected virtual int RetryDelaySeconds => 10;

    /// <summary>
    /// If true, wraps ProcessMessage in an ambient TransactionScope.
    /// Prefer local DB transaction for maximum performance.
    /// </summary>
    protected virtual bool UseTransactionScope => false;

    protected virtual int PrefetchCount => 32;

    // Header key padronizada
    protected virtual string AttemptHeaderKey => "attempt";

    protected QueueSubscriber(IServiceProvider serviceProvider, Connection connection)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    protected abstract Task ProcessMessage(IServiceProvider scopedProvider, MessageContext<TModel> context, CancellationToken ct);
    
    protected virtual Task OnMessageFailedAsync(
        IServiceProvider scopedProvider,
        MessageContext<TModel>? context,
        Exception exception,
        int attempt,
        CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // init delayed/dead channels now (derived already built)
        _delayedChannel = new Channel(
            connection: _connection,
            queueName: DelayedQueue,
            exchangeName: $"{DelayedQueue}-ex",
            routingKey: $"{DelayedQueue}-key",
            exchangeType: ExchangeType.Direct,
            retryChannelCount: 3,
            retryChannelDelayInSeconds: 3,
            queueArguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = $"{Queue}-ex",
                ["x-dead-letter-routing-key"] = $"{Queue}-key"
            });

        _deadChannel = new Channel(
            connection: _connection,
            queueName: DeadQueue,
            exchangeName: $"{DeadQueue}-ex",
            routingKey: $"{DeadQueue}-key",
            exchangeType: ExchangeType.Direct,
            retryChannelCount: 3,
            retryChannelDelayInSeconds: 3);

        _mainChannelWrapper = new Channel(
            connection: _connection,
            queueName: Queue,
            exchangeName: $"{Queue}-ex",
            routingKey: $"{Queue}-key",
            exchangeType: ExchangeType.Direct,
            retryChannelCount: 3,
            retryChannelDelayInSeconds: 3);

        // IMPORTANTE: esse é o channel do consumo. ACK deve ser feito nele.
        var consumerChannel = await _mainChannelWrapper.GetChannelAsync(stoppingToken).ConfigureAwait(false);

        await consumerChannel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: (ushort)PrefetchCount,
            global: false,
            cancellationToken: stoppingToken
        ).ConfigureAwait(false);

        var consumer = new AsyncEventingBasicConsumer(consumerChannel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            // use sempre o consumerChannel para Ack/Nack
            await OnMessageReceived(consumerChannel, ea, stoppingToken).ConfigureAwait(false);
        };

        await consumerChannel.BasicConsumeAsync(
            queue: Queue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken
        ).ConfigureAwait(false);

        // mantém o BackgroundService vivo
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
    }

    private async Task OnMessageReceived(IChannel consumerChannel, BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        TModel? model = null;

        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.Span);
            model = JsonConvert.DeserializeObject<TModel>(json);

            if (model is null)
                throw new InvalidOperationException("Mensagem inválida (JSON nulo).");

            using var scope = _scopeFactory.CreateScope();

            var (attempt, headers) = ea.BasicProperties.GetHeader();

            var context = new MessageContext<TModel>
            {
                Model = model,
                Headers = headers,
                Attempt = attempt,
                DeliveryTag = ea.DeliveryTag
            };
            
            if (UseTransactionScope)
            {
                using var tx = new TransactionScope(
                    TransactionScopeOption.Required,
                    new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                    TransactionScopeAsyncFlowOption.Enabled);

                await ProcessMessage(scope.ServiceProvider, context, stoppingToken).ConfigureAwait(false);

                tx.Complete();
            }
            else
            {
                await ProcessMessage(scope.ServiceProvider, context, stoppingToken).ConfigureAwait(false);
            }

            await consumerChannel.BasicAckAsync(
                deliveryTag: ea.DeliveryTag,
                multiple: false,
                cancellationToken: stoppingToken
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // attempt atual antes de incrementar (o HandleFailure incrementa)

            var (attempt, headers) = ea.BasicProperties.GetHeader();
            var nextAttempt = attempt + 1;

            var context = new MessageContext<TModel>
            {
                Model = model,
                Headers = headers,
                Attempt = nextAttempt,
                DeliveryTag = ea.DeliveryTag
            };
            
            // scope novo só para logging/telemetria/efeitos colaterais controlados
            using var scope = _scopeFactory.CreateScope();

            await OnMessageFailedAsync(
                scopedProvider: scope.ServiceProvider,
                context: context,
                exception: ex,
                attempt: nextAttempt,
                ct: stoppingToken
            ).ConfigureAwait(false);

            await HandleFailureAsync(consumerChannel, ea, ex, stoppingToken).ConfigureAwait(false);
        }
    }
    private async Task HandleFailureAsync(IChannel consumerChannel, BasicDeliverEventArgs ea, Exception ex, CancellationToken ct)
    {
        if (_delayedChannel is null) throw new InvalidOperationException("Delayed channel not initialized.");
        if (_deadChannel is null) throw new InvalidOperationException("Dead channel not initialized.");

        var (attempt, headers) = ea.BasicProperties.GetHeader();
        var nextAttempt = attempt + 1;

        headers[AttemptHeaderKey] = nextAttempt;

        if (nextAttempt <= RetryAttempts)
        {
            // TTL por mensagem: Expiration (ms como string)
            var delayMs = checked(RetryDelaySeconds * 1000);

            var props = new BasicProperties
            {
                Persistent = true,
                Headers = headers.ConvertToObject(),
                Expiration = delayMs.ToString()
            };

            await _delayedChannel.BasicPublishAsync(ea.Body, props, ct).ConfigureAwait(false);

            // ACK sempre no consumerChannel
            await consumerChannel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        // dead-letter
        {
            var props = new BasicProperties
            {
                Persistent = true,
                Headers = headers.ConvertToObject()
            };

            await _deadChannel.BasicPublishAsync(ea.Body, props, ct).ConfigureAwait(false);
            await consumerChannel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct).ConfigureAwait(false);
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        _ = _delayedChannel?.DisposeAsync();
        _ = _deadChannel?.DisposeAsync();
        _ = _mainChannelWrapper?.DisposeAsync();
    }
}
}
