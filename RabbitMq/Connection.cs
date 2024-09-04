using RabbitMQ.Client;

namespace RabbitMq
{
    public class Connection : IDisposable
    {
        private readonly object _connectionLockObject = new object();
        private readonly IConnectionFactory _factory;
        private readonly double _closeConnectionTimeoutInSeconds;
        private readonly Action _retryPolicy;
        private IConnection _connection;
        private bool Disposed;

        public bool IsConnectionOpen => _connection is not null && _connection.IsOpen && !Disposed;

        public Connection(
            IConnectionFactory factory,
            double closeConnectionTimeoutInSeconds = 15.0,
            int retryConnectionCount = 3,
            int retryConnectionDelayInSeconds = 10)
        {
            _factory = factory;
            _closeConnectionTimeoutInSeconds = closeConnectionTimeoutInSeconds;

            _retryPolicy = () =>
            {
                for (int i = 0; i < retryConnectionCount; i++)
                {
                    try
                    {
                        _connection = _factory.CreateConnection();
                        if (IsConnectionOpen) break;
                    }
                    catch
                    {
                        if (i < retryConnectionCount - 1)
                            Thread.Sleep(TimeSpan.FromSeconds(retryConnectionDelayInSeconds));
                    }
                }
            };
        }

        public void PrepareConnection()
        {
            lock (_connectionLockObject)
            {
                if (IsConnectionOpen) return;

                _retryPolicy.Invoke();

                if (!IsConnectionOpen)
                    throw new InvalidOperationException("Falha ao conectar-se ao RabbitMQ");

                WatchConnectionHealth();
            }
        }

        private void FinishConnection()
        {
            _connection?.Close(TimeSpan.FromSeconds(_closeConnectionTimeoutInSeconds));
            _connection?.Dispose();
        }

        private void WatchConnectionHealth()
        {
            _connection.ConnectionShutdown += (sender, e) => TryReconnect();
            _connection.CallbackException += (sender, e) => TryReconnect();
            _connection.ConnectionBlocked += (sender, e) => TryReconnect();
        }

        private void TryReconnect()
        {
            if (Disposed) return;
            PrepareConnection();
        }

        public IModel CreateChannel()
        {
            if (!IsConnectionOpen)
                throw new InvalidOperationException("Nenhuma conexão disponível com o RabbitMQ.");

            return _connection.CreateModel();
        }

        public void Dispose()
        {
            if (Disposed) return;

            FinishConnection();
            Disposed = true;
        }
    }
}
