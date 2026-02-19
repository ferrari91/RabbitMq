using Moq;
using RabbitMq;
using RabbitMQ.Client;

namespace RabbitMqTests
{
    public class ConnectionTests
    {
        private readonly Mock<IConnectionFactory> _mockFactory;
        private readonly Mock<IConnection> _mockConnection;
        private readonly Mock<IChannel> _mockChannel;
        private readonly Connection _connection;

        public ConnectionTests()
        {
            _mockFactory = new Mock<IConnectionFactory>();
            _mockConnection = new Mock<IConnection>();
            _mockChannel = new Mock<IChannel>();

            _mockConnection.Setup(c => c.IsOpen).Returns(true);

            _mockFactory
                .Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockConnection.Object);

            // Mesmo que Connection.cs não crie channel, é comum deixar pronto para outros testes
            _mockConnection
                .Setup(c => c.CreateChannelAsync(
                    It.IsAny<CreateChannelOptions?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockChannel.Object);

            _mockConnection.Setup(c => c.CloseAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockConnection.Setup(c => c.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            _connection = new Connection(_mockFactory.Object);
        }

        private void SetupConnectionOpen(bool isOpen)
        {
            _mockConnection.Setup(c => c.IsOpen).Returns(isOpen);
        }

        [Fact]
        public async Task PrepareConnectionAsync_Should_Open_Connection_If_Not_Already_Open()
        {
            // Arrange
            SetupConnectionOpen(false);

            _mockFactory
                .Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
                .Callback(() => SetupConnectionOpen(true))
                .ReturnsAsync(_mockConnection.Object);

            // Act
            await _connection.PrepareConnectionAsync(CancellationToken.None);

            // Assert
            _mockFactory.Verify(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockConnection.VerifyGet(c => c.IsOpen, Times.AtLeastOnce);
        }

        [Fact]
        public async Task PrepareConnectionAsync_Should_Not_Reopen_Connection_If_Already_Open()
        {
            // Arrange
            SetupConnectionOpen(true);

            await _connection.PrepareConnectionAsync(CancellationToken.None);

            // Act
            await _connection.PrepareConnectionAsync(CancellationToken.None);

            // Assert
            _mockFactory.Verify(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PrepareConnectionAsync_Should_Throw_InvalidOperationException_If_Connection_Fails()
        {
            // Arrange
            _mockFactory
                .Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("fail"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _connection.PrepareConnectionAsync(CancellationToken.None));
        }

        [Fact]
        public void GetConnection_Should_Return_IConnection_When_Prepared()
        {
            // Arrange
            SetupConnectionOpen(true);
            _connection.PrepareConnection();

            // Act
            var conn = _connection.GetConnection();

            // Assert
            Assert.NotNull(conn);
            Assert.Equal(_mockConnection.Object, conn);
        }

        [Fact]
        public async Task DisposeAsync_Should_Close_And_Dispose_Connection()
        {
            // Arrange
            SetupConnectionOpen(true);
            await _connection.PrepareConnectionAsync(CancellationToken.None);

            // Act
            await _connection.DisposeAsync();

            // Assert
            _mockConnection.Verify(c => c.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockConnection.Verify(c => c.DisposeAsync(), Times.Once);
        }

        [Fact]
        public async Task DisposeAsync_Should_Not_Dispose_Twice()
        {
            // Arrange
            SetupConnectionOpen(true);
            await _connection.PrepareConnectionAsync(CancellationToken.None);

            // Act
            await _connection.DisposeAsync();
            await _connection.DisposeAsync();

            // Assert
            _mockConnection.Verify(c => c.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockConnection.Verify(c => c.DisposeAsync(), Times.Once);
        }
    }
}
