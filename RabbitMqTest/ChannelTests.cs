using Moq;
using RabbitMq;
using RabbitMQ.Client;

namespace RabbitMqTests
{
    public class ChannelTests
    {
        [Fact]
        public async Task GetChannelAsync_ShouldReturnChannel_WhenChannelIsOpen()
        {
            // Arrange
            var ct = CancellationToken.None;

            var mockFactory = new Mock<IConnectionFactory>();
            var mockConnection = new Mock<IConnection>();
            var mockChannel = new Mock<IChannel>();

            mockConnection.Setup(c => c.IsOpen).Returns(true);
            mockChannel.Setup(c => c.IsOpen).Returns(true);

            mockFactory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(mockConnection.Object);

            mockConnection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(mockChannel.Object);

            var connection = new Connection(mockFactory.Object);

            var channelWrapper = new Channel(
                connection: connection,
                queueName: "test-queue",
                exchangeName: "test-exchange",
                routingKey: "test-routing",
                exchangeType: ExchangeType.Direct,
                retryChannelCount: 3,
                retryChannelDelayInSeconds: 1);

            // Act
            var result = await channelWrapper.GetChannelAsync(ct);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(mockChannel.Object, result);

            mockConnection.Verify(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
            mockChannel.VerifyGet(c => c.IsOpen, Times.AtLeastOnce);
        }

        [Fact]
        public void GetChannel_ShouldThrow_WhenChannelNotInitialized()
        {
            // Arrange
            var mockFactory = new Mock<IConnectionFactory>();
            var connection = new Connection(mockFactory.Object);

            var channelWrapper = new Channel(
                connection: connection,
                queueName: "test-queue",
                exchangeName: "test-exchange",
                routingKey: "test-routing",
                exchangeType: ExchangeType.Direct,
                retryChannelCount: 3,
                retryChannelDelayInSeconds: 1);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => channelWrapper.GetChannel());
        }

        [Fact]
        public async Task BasicPublishAsync_ShouldPublishMessage_WhenChannelIsOpen()
        {
            // Arrange
            var ct = CancellationToken.None;

            var mockFactory = new Mock<IConnectionFactory>();
            var mockConnection = new Mock<IConnection>();
            var mockChannel = new Mock<IChannel>();

            mockConnection.Setup(c => c.IsOpen).Returns(true);
            mockChannel.Setup(c => c.IsOpen).Returns(true);

            mockFactory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(mockConnection.Object);

            mockConnection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(mockChannel.Object);

            var connection = new Connection(mockFactory.Object);

            var channelWrapper = new Channel(
                connection: connection,
                queueName: "test-queue",
                exchangeName: "test-exchange",
                routingKey: "test-routing",
                exchangeType: ExchangeType.Direct,
                retryChannelCount: 3,
                retryChannelDelayInSeconds: 1);

            var body = new byte[] { 0x01, 0x02 };
            var props = new BasicProperties { Persistent = true };

            // Act
            await channelWrapper.GetChannelAsync(ct); // ensure open
            await channelWrapper.BasicPublishAsync(body, props, ct);

            // Assert
            mockChannel.Verify(c => c.BasicPublishAsync(
                    "test-exchange",
                    "test-routing",
                    false,
                    It.IsAny<BasicProperties>(),
                    It.Is<ReadOnlyMemory<byte>>(m => m.ToArray().Length == body.Length),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DisposeAsync_ShouldCloseAndDisposeChannel_WhenCalled()
        {
            // Arrange
            var ct = CancellationToken.None;

            var mockFactory = new Mock<IConnectionFactory>();
            var mockConnection = new Mock<IConnection>();
            var mockChannel = new Mock<IChannel>();

            mockConnection.Setup(c => c.IsOpen).Returns(true);
            mockChannel.Setup(c => c.IsOpen).Returns(true);

            mockFactory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(mockConnection.Object);

            mockConnection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(mockChannel.Object);

            // Se o seu wrapper chama CloseAsync/DisposeAsync:
            mockChannel.Setup(c => c.CloseAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            mockChannel.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

            var connection = new Connection(mockFactory.Object);

            var channelWrapper = new Channel(
                connection: connection,
                queueName: "test-queue",
                exchangeName: "test-exchange",
                routingKey: "test-routing",
                exchangeType: ExchangeType.Direct,
                retryChannelCount: 3,
                retryChannelDelayInSeconds: 1);

            // Act
            await channelWrapper.GetChannelAsync(ct);
            await channelWrapper.DisposeAsync();

            // Assert
            mockChannel.Verify(c => c.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
            mockChannel.Verify(c => c.DisposeAsync(), Times.Once);
        }

        [Fact]
        public async Task DisposeAsync_ShouldNotThrow_WhenCalledMultipleTimes()
        {
            // Arrange
            var ct = CancellationToken.None;

            var mockFactory = new Mock<IConnectionFactory>();
            var mockConnection = new Mock<IConnection>();
            var mockChannel = new Mock<IChannel>();

            mockConnection.Setup(c => c.IsOpen).Returns(true);
            mockChannel.Setup(c => c.IsOpen).Returns(true);

            mockFactory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(mockConnection.Object);

            mockConnection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(mockChannel.Object);

            mockChannel.Setup(c => c.CloseAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            mockChannel.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

            var connection = new Connection(mockFactory.Object);

            var channelWrapper = new Channel(
                connection: connection,
                queueName: "test-queue",
                exchangeName: "test-exchange",
                routingKey: "test-routing",
                exchangeType: ExchangeType.Direct,
                retryChannelCount: 3,
                retryChannelDelayInSeconds: 1);

            // Act
            await channelWrapper.GetChannelAsync(ct);
            await channelWrapper.DisposeAsync();
            await channelWrapper.DisposeAsync();

            // Assert
            mockChannel.Verify(c => c.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
            mockChannel.Verify(c => c.DisposeAsync(), Times.Once);
        }
    }
}
