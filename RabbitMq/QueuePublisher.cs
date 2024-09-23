using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Text;

namespace RabbitMq
{
    public abstract class QueuePublisher<TModel> : IDisposable where TModel : class
    {
        private readonly IServiceProvider _services;
        private readonly Connection _connection;

        protected abstract string QueueName { get; }
        protected virtual int RetryChannelCount => 3;
        protected virtual int RetryChannelDelay => 10;

        private IModel? channel;

        private string exchange => $"{QueueName}-ex";
        private string routing => $"key-{QueueName}";

        public QueuePublisher(IServiceProvider services)
        {
            _connection = services.GetRequiredService<Connection>();
            _services = services;
        }

        public virtual async Task Publish(TModel model, Dictionary<string, object> headers, CancellationToken ctx)
        {
            var retryPolicy = Policy
               .Handle<OperationInterruptedException>()
               .WaitAndRetryAsync(
                   retryCount: RetryChannelCount,
                   sleepDurationProvider: attempt => TimeSpan.FromSeconds(RetryChannelDelay), 
                   onRetry: (exception, timespan, attempt, context) =>
                   {
                      
                   });

            await retryPolicy.ExecuteAsync(async () =>
            {
                if (channel is null)
                    CreateChannel();

                var body = Encoding.UTF8.GetBytes(Serialize(model));

                if (!ctx.IsCancellationRequested)
                {
                    var properties = channel?.CreateBasicProperties();
                    properties.Headers = new Dictionary<string, object>();

                    if (headers is not null)
                    {
                        foreach (var header in headers)
                        {
                            properties.Headers[header.Key] = FormatHeaderValue(header);
                        }
                    }

                    channel?.BasicPublish(exchange: exchange, routingKey: routing, mandatory: false, basicProperties: properties, body: body);
                }

                return Task.CompletedTask;
            });

            static object FormatHeaderValue(KeyValuePair<string, object> header)
            {
                if (header.Value is null)
                    throw new ArgumentNullException(nameof(header.Key));

                if (header.Value.GetType() == typeof(DateTimeOffset) || header.Value.GetType() == typeof(Guid))
                    return header.Value.ToString();
                else
                    return header.Value;
            }
        }

        private void CreateChannel()
        {
            _connection.PrepareConnection();

            channel = _connection.CreateChannel();

            var exchange = $"{QueueName}-ex";
            var routing = $"key-{QueueName}";

            channel.ExchangeDeclare(exchange, ExchangeType.Fanout, true, false);
            channel.QueueDeclare(queue: QueueName, durable: true, exclusive: false, autoDelete: false);
            channel.QueueBind(queue: QueueName, exchange: exchange, routingKey: routing);
        }

        protected virtual string Serialize(TModel model) => JsonConvert.SerializeObject(model);

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}
