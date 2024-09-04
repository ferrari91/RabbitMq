using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Net.Sockets;

namespace RabbitMq
{
    delegate Policy PersistenteChannelPolicyFactory();

    public class Channel : IDisposable
    {
        private readonly object _channelLockObject = new object();
        private readonly object _publishLockObject = new object();

        private IModel _channel;
        private bool m_HasCreatedQueue;
        private bool Disposed;

        private bool IsConnectionOpen => _connection is not null && _connection.IsConnectionOpen;
        private bool IsChannelOpen => _channel is not null && _channel.IsOpen && !Disposed;

        private Connection _connection;

        private readonly string _queueName;
        private readonly string _exchangeName;
        private readonly string _routingKey;
        private readonly string _exchangeType;

        private readonly Dictionary<string, object> _exchangeArguments;
        private readonly Dictionary<string, object> _queueArguments;
        private readonly Dictionary<string, object> _bindArguments;

        private readonly PersistenteChannelPolicyFactory _policyFactory;

        public Channel(Connection connection, string queueName, string exchangeName, string routingKey, string exchangeType,
            int retryChannelCount, int retryChannelDelayInSeconds,
            Dictionary<string, object> exchangeArguments = null,
            Dictionary<string, object> queueArguments = null,
            Dictionary<string, object> bindArguments = null)
        {
            _connection = connection;

            _queueName = queueName;
            _exchangeName = exchangeName;
            _routingKey = routingKey;
            _exchangeType = exchangeType;

            _exchangeArguments = exchangeArguments;
            _queueArguments = queueArguments;
            _bindArguments = bindArguments;

            _policyFactory = () => Policy
            .Handle<SocketException>()
            .Or<BrokerUnreachableException>()
            .Or<InvalidOperationException>()
            .WaitAndRetry(
                retryCount: retryChannelCount,
                retryAttempt => TimeSpan.FromSeconds(retryChannelDelayInSeconds),
                (ex, time) => { });
        }

        public IModel GetChannel()
        {
            _connection.PrepareConnection();

            lock (_channelLockObject)
            {
                if (IsChannelOpen)
                {
                    return _channel;
                }

                FinishChannel();

                _channel = PrepareChannel();

                if (!IsChannelOpen)
                {
                    throw new Exception("Não foi possível abrir o Channel RabbitMQ (encontra-se fechado após tentar abrir).");
                }

                return _channel;
            }
        }

        // Revisar o RecorvedChannel do RabbitMQ está dando falha e perdendo registro
        // 'RabbitMQ.Client.Impl.AutorecoveringModel'.
        private void FinishChannel()
        {
            try
            {
                _channel?.Close();
                _channel?.Dispose();
            }
            catch (Exception)
            {
            }
        }

        private IModel PrepareChannel()
        {
            IModel channel = null;

            var policy = _policyFactory();
            policy.Execute(() =>
            {
                channel = CreateOrWaitRecoverChannel();
            });

            if (channel is null)
                throw new Exception("Não foi possível abri o Channel RabbitMQ (encontra-se nulo após tentar abrir)");

            return channel;
        }

        private IModel CreateOrWaitRecoverChannel()
        {
            if (!IsConnectionOpen)
                throw new InvalidOperationException("A Connection se encontrava fechada ao tentar estabelecer o Channel.");

            if (IsChannelOpen)
                return _channel;

            return ConfigureChannel(_connection.CreateChannel());
        }

        private IModel ConfigureChannel(IModel channel)
        {
            if (!m_HasCreatedQueue)
            {
                channel.ExchangeDeclare(_exchangeName, _exchangeType, autoDelete: false, durable: true, arguments: _exchangeArguments);
                channel.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false, arguments: _queueArguments);
                channel.QueueBind(_queueName, _exchangeName, _routingKey, arguments: _bindArguments);
            }

            channel.ConfirmSelect();

            return channel;
        }

        public void Publish(byte[] body, IBasicProperties basicProperties)
        {
            lock (_publishLockObject)
            {
                _channel.BasicPublish(exchange: _exchangeName, routingKey: _routingKey, mandatory: false, basicProperties: basicProperties, body: body);
            }
        }

        public void Dispose()
        {
            if (Disposed) return;

            try
            {
                FinishChannel();
                Disposed = true;
            }
            catch (IOException ex)
            {
            }
        }
    }
}
