using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Net.Sockets;

namespace RabbitMq
{
    /// <summary>
    /// Thread-safe wrapper for a single RabbitMQ IChannel instance (RabbitMQ.Client 7.x).
    /// Owns the channel lifecycle; callers MUST NOT dispose returned channel.
    /// </summary>
    public sealed class Channel : IAsyncDisposable, IDisposable
    {
        private readonly object _channelLockObject = new();
        private readonly SemaphoreSlim _publishLock = new(1, 1);

        private IChannel? _channel;
        private bool _hasDeclaredTopology;
        private bool _disposed;

        private readonly Connection _connection;

        private readonly string _queueName;
        private readonly string _exchangeName;
        private readonly string _routingKey;
        private readonly string _exchangeType;

        private readonly IDictionary<string, object>? _exchangeArguments;
        private readonly IDictionary<string, object>? _queueArguments;
        private readonly IDictionary<string, object>? _bindArguments;

        private readonly int _retryChannelCount;
        private readonly int _retryChannelDelayInSeconds;

        private bool IsChannelOpen => _channel is not null && _channel.IsOpen && !_disposed;

        public Channel(
            Connection connection,
            string queueName,
            string exchangeName,
            string routingKey,
            string exchangeType,
            int retryChannelCount,
            int retryChannelDelayInSeconds,
            IDictionary<string, object>? exchangeArguments = null,
            IDictionary<string, object>? queueArguments = null,
            IDictionary<string, object>? bindArguments = null)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));

            _queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
            _exchangeName = exchangeName ?? throw new ArgumentNullException(nameof(exchangeName));
            _routingKey = routingKey ?? throw new ArgumentNullException(nameof(routingKey));
            _exchangeType = exchangeType ?? throw new ArgumentNullException(nameof(exchangeType));

            _exchangeArguments = exchangeArguments;
            _queueArguments = queueArguments;
            _bindArguments = bindArguments;

            _retryChannelCount = retryChannelCount;
            _retryChannelDelayInSeconds = retryChannelDelayInSeconds;
        }

        public IChannel GetChannel() => GetChannelAsync(CancellationToken.None).GetAwaiter().GetResult();

        public async Task<IChannel> GetChannelAsync(CancellationToken ct)
        {
            ThrowIfDisposed();

            await _connection.PrepareConnectionAsync(ct).ConfigureAwait(false);

            lock (_channelLockObject)
            {
                if (IsChannelOpen)
                    return _channel!;
            }

            var channel = await PrepareChannelWithRetryAsync(ct).ConfigureAwait(false);

            lock (_channelLockObject)
            {
                if (IsChannelOpen)
                    return _channel!;

                _channel = channel;
                _hasDeclaredTopology = false;
                return _channel!;
            }
        }

        private async Task<IChannel> PrepareChannelWithRetryAsync(CancellationToken ct)
        {
            Exception? last = null;

            for (int i = 0; i < _retryChannelCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    return await CreateOrRecoverChannelAsync(ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is SocketException
                                           || ex is BrokerUnreachableException
                                           || ex is InvalidOperationException)
                {
                    last = ex;
                    if (i < _retryChannelCount - 1)
                        await Task.Delay(TimeSpan.FromSeconds(_retryChannelDelayInSeconds), ct).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException(
                "Não foi possível abrir o Channel RabbitMQ.",
                last);
        }

        private async Task<IChannel> CreateOrRecoverChannelAsync(CancellationToken ct)
        {
            var conn = _connection.GetConnection();
            var ch = await conn.CreateChannelAsync(cancellationToken: ct).ConfigureAwait(false);

            // declare topology once per channel
            if (!_hasDeclaredTopology)
            {
                await DeclareTopologyAsync(ch, ct).ConfigureAwait(false);
                _hasDeclaredTopology = true;
            }

            return ch;
        }

        private async Task DeclareTopologyAsync(IChannel channel, CancellationToken ct)
        {
            await channel.ExchangeDeclareAsync(
                exchange: _exchangeName,
                type: _exchangeType,
                durable: true,
                autoDelete: false,
                arguments: _exchangeArguments,
                cancellationToken: ct).ConfigureAwait(false);

            await channel.QueueDeclareAsync(
                queue: _queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: _queueArguments,
                cancellationToken: ct).ConfigureAwait(false);

            await channel.QueueBindAsync(
                queue: _queueName,
                exchange: _exchangeName,
                routingKey: _routingKey,
                arguments: _bindArguments,
                cancellationToken: ct).ConfigureAwait(false);
        }

        public async Task BasicPublishAsync(ReadOnlyMemory<byte> body, IBasicProperties? properties, CancellationToken ct)
        {
            var channel = await GetChannelAsync(ct).ConfigureAwait(false);

            await _publishLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!IsChannelOpen)
                {
                    channel = await GetChannelAsync(ct).ConfigureAwait(false);
                }

                BasicProperties props = properties is null
                    ? new BasicProperties()
                    : ToAmqp091(properties);

                await channel.BasicPublishAsync(
                    exchange: _exchangeName,
                    routingKey: _routingKey,
                    mandatory: false,
                    basicProperties: props,
                    body: body,
                    cancellationToken: ct
                ).ConfigureAwait(false);
            }
            finally
            {
                _publishLock.Release();
            }
        }

        private static BasicProperties ToAmqp091(IBasicProperties p)
        {
            var bp = new BasicProperties
            {
                ContentType = p.ContentType,
                ContentEncoding = p.ContentEncoding,
                CorrelationId = p.CorrelationId,
                ReplyTo = p.ReplyTo,
                Expiration = p.Expiration,
                MessageId = p.MessageId,
                Type = p.Type,
                UserId = p.UserId,
                AppId = p.AppId,
                ClusterId = p.ClusterId,
                DeliveryMode = p.DeliveryMode,
                Priority = p.Priority,
                Timestamp = p.Timestamp
            };

            if (p.Headers is not null && p.Headers.Count > 0)
            {
                bp.Headers = new Dictionary<string, object?>(p.Headers.Count);
                foreach (var kv in p.Headers)
                    bp.Headers[kv.Key] = kv.Value;
            }

            return bp;
        }
        
        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Channel));
        }

        public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            IChannel? ch;
            lock (_channelLockObject)
            {
                ch = _channel;
                _channel = null;
            }

            if (ch is not null)
            {
                try { await ch.CloseAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
                try { await ch.DisposeAsync().ConfigureAwait(false); } catch { }
            }

            _publishLock.Dispose();
        }
    }
}
