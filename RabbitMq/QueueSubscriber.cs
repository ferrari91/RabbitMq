using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Polly;
using RabbitMq.Constants;
using RabbitMq.Extensions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;
using System.Transactions;

namespace RabbitMq
{
    public abstract class QueueSubscriber<TModel> : BackgroundService where TModel : class
    {
        private readonly IServiceScope _scope;
        private readonly IServiceProvider _serviceProvider;

        private readonly Channel _delayedChannel;
        private readonly Channel _deadChannel;

        protected abstract string Queue { get; }
        protected abstract string DelayedQueue { get; }
        protected abstract string DeadQueue { get; }

        protected virtual int RetryAttempts => 3;
        protected virtual int RetryDelay => 10;
        protected virtual bool UseTransactionScope { get; } = false;

        public QueueSubscriber(IServiceProvider serviceProvider)
        {
            _scope = serviceProvider.CreateScope();
            _serviceProvider = _scope.ServiceProvider;

            var connection = serviceProvider.GetRequiredService<Connection>();

            _delayedChannel = new Channel(
                connection: connection,
                queueName: DelayedQueue,
                exchangeName: $"{DelayedQueue}-ex",
                routingKey: $"{DelayedQueue}-key",
                exchangeType: ExchangeType.Direct,
                retryChannelCount: RetryAttempts,
                retryChannelDelayInSeconds: RetryDelay,
                exchangeArguments: new Dictionary<string, object>
                {
                    [ConstantsHeader.TypeDelayed] = ExchangeType.Direct
                },
                queueArguments: new Dictionary<string, object>
                {
                    { ConstantsHeader.ExchangeDeadLetter, string.Empty },
                    { ConstantsHeader.ExchangeDeadRoutingKey, Queue }
                },
                bindArguments: null
                );

            _deadChannel = new Channel(
                connection: connection,
                queueName: DeadQueue,
                exchangeName: $"{DeadQueue}-ex",
                routingKey: $"{DeadQueue}-key",
                exchangeType: ExchangeType.Direct,
                retryChannelCount: RetryAttempts,
                retryChannelDelayInSeconds: RetryDelay,
                exchangeArguments: null,
                queueArguments: new Dictionary<string, object>
                {
                    [ConstantsHeader.ExchangeDeadLetter] = $"{DeadQueue}-ex"
                },
                bindArguments: null
                );
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Policy
            .Handle<OperationInterruptedException>()
            .RetryForeverAsync((exception, context, attempt) =>
            {
                Task.Delay(TimeSpan.FromSeconds(3)).Wait();
            })
            .ExecuteAsync(async () =>
            {
                using (var connection = _serviceProvider.GetRequiredService<Connection>())
                {
                    connection.PrepareConnection();
                    using (var channel = connection.CreateChannel())
                    {
                        var exchange = $"{Queue}-ex";
                        var routing = $"{Queue}-key";

                        channel.ExchangeDeclare(exchange, ExchangeType.Fanout, true, false);
                        channel.QueueDeclare(queue: Queue, durable: true, exclusive: false, autoDelete: false);
                        channel.QueueBind(queue: Queue, exchange: exchange, routingKey: routing);

                        var consumer = new AsyncEventingBasicConsumer(channel);
                        consumer.Received += OnMessageReceived;

                        channel.BasicQos(0, 1, false);
                        channel.BasicConsume(queue: Queue, autoAck: false, consumer: consumer);

                        while (!stoppingToken.IsCancellationRequested)
                            await Task.Delay(TimeSpan.FromSeconds(120), stoppingToken);

                        channel.Close();
                        channel.Dispose();
                        connection.Dispose();

                        async Task OnMessageReceived(object sender, BasicDeliverEventArgs @event)
                        {
                            using var scope = _serviceProvider.CreateScope();
                            using var transaction = UseTransactionScope ? new TransactionScope(TransactionScopeAsyncFlowOption.Enabled) : default;
                            int attempt = 0;
                            IDictionary<string, object> headers = null;
                            TModel model;
                            Context<TModel> context = default;

                            try
                            {
                                var body = @event.Body.ToArray();
                                var contentType = @event.BasicProperties.ContentType;
                                try
                                {
                                    (attempt, headers) = @event.BasicProperties.GetHeader();

                                    model = DeserializeMessage(Encoding.UTF8.GetString(body), headers);
                                    context = new Context<TModel>(scope.ServiceProvider, model, @event.BasicProperties.Headers);
                                }
                                catch
                                {
                                    ToDead(contentType, headers, body);
                                    channel.BasicAck(@event.DeliveryTag, false);
                                    throw;
                                }

                                try
                                {
                                    await ProcessMessage(scope.ServiceProvider, context, stoppingToken);
                                    transaction?.Complete();
                                }
                                catch (Exception exception)
                                {
                                    if (++attempt <= RetryAttempts)
                                        ToDelayed(contentType, headers, body, attempt);
                                    else
                                        ToDead(contentType, headers, body);

                                    ExceptionExecute(scope.ServiceProvider, exception, context, attempt, stoppingToken);
                                }
                                finally
                                {
                                    channel.BasicAck(@event.DeliveryTag, false);
                                }
                            }
                            catch (Exception exception)
                            {
                                transaction?.Dispose();
                                ExceptionExecute(scope.ServiceProvider, exception, context, attempt, stoppingToken);
                            }
                        }
                    }
                }
            });
        }

        protected abstract Task ProcessMessage(IServiceProvider serviceProvider, Context<TModel> context, CancellationToken stoppingToken);

        private void ToDelayed(string contentType, IDictionary<string, object> headers, byte[] body, int attempt)
        {
            using (var channel = _delayedChannel.GetChannel())
            {
                var properties = channel.CreateBasicProperties();

                properties.Persistent = true;
                properties.ContentType = contentType;
                properties.DeliveryMode = 2;
                properties.CorrelationId = Guid.NewGuid().ToString();

                properties.Headers = headers;   
                var sleepInterval = TimeSpan.FromSeconds(10).TotalMilliseconds;

                properties.Headers[ConstantsHeader.Attempt] = attempt;
                properties.Headers[ConstantsHeader.HeaderMessageTTL] = (int)sleepInterval;

                properties.Headers = headers.ConvertToBytes();
                properties.Expiration = sleepInterval.ToString();

                _delayedChannel.Publish(body, properties);
            }
        }

        private void ToDead(string contentType, IDictionary<string, object> headers, byte[] body)
        {
            using (var channel = _deadChannel.GetChannel())
            {
                var properties = channel.CreateBasicProperties();

                properties.Persistent = true;
                properties.ContentType = contentType;
                properties.DeliveryMode = 2;
                properties.CorrelationId = Guid.NewGuid().ToString();
                properties.Headers = headers;

                properties.Headers = headers.ConvertToBytes();

                _deadChannel.Publish(body, properties);
            }
        }

        protected virtual TModel DeserializeMessage(string rawMessage, IDictionary<string, object> headers = null)
        {
            return JsonConvert.DeserializeObject<TModel>(rawMessage) ?? throw new Exception();
        }

        protected abstract void ExceptionExecute(IServiceProvider provider, Exception exception, Context<TModel> context, int attempt, CancellationToken ctx);

        public override void Dispose()
        {
            _scope.Dispose();
            base.Dispose();
        }
    }
}
