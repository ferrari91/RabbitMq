using Newtonsoft.Json;
using RabbitMq.Extensions;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Net.Sockets;
using System.Text;

namespace RabbitMq
{
    /// <summary>
    /// Publisher base compatible with RabbitMQ.Client 7.x.
    /// </summary>
    public abstract class QueuePublisher<TModel> : IAsyncDisposable, IDisposable where TModel : class
    {
        private readonly Connection _connection;
        private readonly Channel _channel;

        protected abstract string Queue { get; }
        protected virtual string Exchange => $"{Queue}-ex";
        protected virtual string RoutingKey => $"{Queue}-key";
        protected virtual string ExchangeType => RabbitMQ.Client.ExchangeType.Direct;

        protected virtual int RetryChannelCount => 3;
        protected virtual int RetryChannelDelayInSeconds => 3;

        protected QueuePublisher(Connection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _channel = new Channel(
                connection: _connection,
                queueName: Queue,
                exchangeName: Exchange,
                routingKey: RoutingKey,
                exchangeType: ExchangeType,
                retryChannelCount: RetryChannelCount,
                retryChannelDelayInSeconds: RetryChannelDelayInSeconds);
        }

        public void Publish(TModel message, IDictionary<string, object>? headers = null)
            => PublishAsync(message, headers, CancellationToken.None).GetAwaiter().GetResult();

        public async Task PublishAsync(TModel message, IDictionary<string, object>? headers, CancellationToken ct)
        {
            if (message is null) throw new ArgumentNullException(nameof(message));

            byte[] body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
            var props = new BasicProperties
            {
                Persistent = true,
                Headers = headers is null ? null : headers.ConvertToObject()
            };

            try
            {
                await _channel.BasicPublishAsync(body, props, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is SocketException || ex is BrokerUnreachableException)
            {
                await _connection.PrepareConnectionAsync(ct).ConfigureAwait(false);
                await _channel.BasicPublishAsync(body, props, ct).ConfigureAwait(false);
            }
        }

        public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

        public async ValueTask DisposeAsync()
        {
            await _channel.DisposeAsync().ConfigureAwait(false);
        }
    }
}
