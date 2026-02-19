using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading;

namespace RabbitMq
{
    /// <summary>
    /// RabbitMQ connection wrapper compatible with RabbitMQ.Client 7.x (async API).
    /// </summary>
    public sealed class Connection : IAsyncDisposable, IDisposable
    {
        private readonly object _sync = new();
        private readonly IConnectionFactory _factory;
        private readonly double _closeConnectionTimeoutInSeconds;
        private readonly int _retryConnectionCount;
        private readonly int _retryConnectionDelayInSeconds;

        private IConnection? _connection;
        private bool _disposed;
        private int _reconnecting; // 0/1

        public bool IsConnectionOpen => _connection is not null && _connection.IsOpen && !_disposed;

        public Connection(
            IConnectionFactory factory,
            double closeConnectionTimeoutInSeconds = 15.0,
            int retryConnectionCount = 3,
            int retryConnectionDelayInSeconds = 10)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _closeConnectionTimeoutInSeconds = closeConnectionTimeoutInSeconds;
            _retryConnectionCount = retryConnectionCount;
            _retryConnectionDelayInSeconds = retryConnectionDelayInSeconds;
        }

        public void PrepareConnection() => PrepareConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();

        public async Task PrepareConnectionAsync(CancellationToken ct)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                if (IsConnectionOpen) return;
            }

            // Ensure only one reconnect happens at a time
            if (Interlocked.Exchange(ref _reconnecting, 1) == 1)
            {
                // wait for other reconnect
                for (int i = 0; i < 50; i++)
                {
                    if (IsConnectionOpen) { Interlocked.Exchange(ref _reconnecting, 0); return; }
                    await Task.Delay(100, ct).ConfigureAwait(false);
                }
            }

            try
            {
                lock (_sync)
                {
                    if (IsConnectionOpen) return;
                }

                await SafeCloseAndDisposeAsync(_connection).ConfigureAwait(false);
                _connection = null;

                Exception? last = null;

                for (int i = 0; i < _retryConnectionCount; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        _connection = await _factory.CreateConnectionAsync(ct).ConfigureAwait(false);
                        if (IsConnectionOpen)
                        {
                            AttachHealthHandlers(_connection);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        if (i < _retryConnectionCount - 1)
                            await Task.Delay(TimeSpan.FromSeconds(_retryConnectionDelayInSeconds), ct).ConfigureAwait(false);
                    }
                }

                throw new InvalidOperationException("Não foi possível abrir a conexão com RabbitMQ.", last);
            }
            finally
            {
                Interlocked.Exchange(ref _reconnecting, 0);
            }
        }

        public IConnection GetConnection()
        {
            ThrowIfDisposed();
            PrepareConnection();
            return _connection ?? throw new InvalidOperationException("Conexão RabbitMQ não está disponível.");
        }

        private void AttachHealthHandlers(IConnection conn)
        {
            // RabbitMQ.Client 7.x uses async event handlers
            conn.ConnectionShutdownAsync -= OnConnectionShutdownAsync;
            conn.CallbackExceptionAsync -= OnCallbackExceptionAsync;
            conn.ConnectionBlockedAsync -= OnConnectionBlockedAsync;

            conn.ConnectionShutdownAsync += OnConnectionShutdownAsync;
            conn.CallbackExceptionAsync += OnCallbackExceptionAsync;
            conn.ConnectionBlockedAsync += OnConnectionBlockedAsync;
        }

        private Task OnConnectionShutdownAsync(object sender, ShutdownEventArgs e)
        {
            _ = PrepareConnectionAsync(CancellationToken.None);
            return Task.CompletedTask;
        }

        private Task OnCallbackExceptionAsync(object sender, CallbackExceptionEventArgs e)
        {
            _ = PrepareConnectionAsync(CancellationToken.None);
            return Task.CompletedTask;
        }

        private Task OnConnectionBlockedAsync(object sender, ConnectionBlockedEventArgs e)
        {
            _ = PrepareConnectionAsync(CancellationToken.None);
            return Task.CompletedTask;
        }

        private async Task SafeCloseAndDisposeAsync(IConnection? connection)
        {
            if (connection is null) return;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_closeConnectionTimeoutInSeconds));
                await connection.CloseAsync(cts.Token).ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }

            try
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Connection));
        }

        public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            var conn = _connection;
            _connection = null;

            await SafeCloseAndDisposeAsync(conn).ConfigureAwait(false);
        }
    }
}
