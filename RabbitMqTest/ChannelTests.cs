using Moq;
using Polly;
using RabbitMq;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Net.Sockets;
using Xunit;

namespace RabbitMqTests
{
    public class ChannelTests
    {
        [Fact]
        public void GetChannel_ShouldReturnChannel_WhenChannelIsOpen()
        {
            // Arrange
            var mockFactory = new Mock<IConnectionFactory>();
            var mockConnection = new Mock<IConnection>();
            var mockChannel = new Mock<IModel>();

            mockConnection.Setup(c => c.IsOpen).Returns(true);
            mockChannel.Setup(c => c.IsOpen).Returns(true);

            mockFactory.Setup(f => f.CreateConnection()).Returns(mockConnection.Object);
            mockConnection.Setup(c => c.CreateModel()).Returns(mockChannel.Object);

            var _connection = new Connection(mockFactory.Object);

            var channel = new Channel(
                connection: _connection,
                queueName: "test-queue",
                exchangeName: "test-exchange",
                routingKey: "test-routing",
                exchangeType: "direct",
                retryChannelCount: 3,
                retryChannelDelayInSeconds: 1);

            // Act
            var result = channel.GetChannel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(mockChannel.Object, result);

            // Verify
            mockChannel.Verify(c => c.IsOpen, Times.Exactly(1)); 
            mockConnection.Verify(c => c.CreateModel(), Times.Once); 
        }

        [Fact]
        public void GetChannel_ShouldThrowException_WhenChannelCannotBeOpened()
        {
            // Arrange
            var mockFactory = new Mock<IConnectionFactory>();
            var mockConnection = new Mock<IConnection>();
            var mockChannel = new Mock<IModel>();

            mockConnection.Setup(c => c.IsOpen).Returns(false);

            var _connection = new Connection(mockFactory.Object);

            var channel = new Channel(
                connection: _connection,
                queueName: "test-queue",
                exchangeName: "test-exchange",
                routingKey: "test-routing",
                exchangeType: "direct",
                retryChannelCount: 3,
                retryChannelDelayInSeconds: 1);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => channel.GetChannel());
        }

        [Fact]
        public void Publish_ShouldPublishMessage_WhenChannelIsOpen()
        {
            // Arrange
            var mockFactory = new Mock<IConnectionFactory>();
            var mockConnection = new Mock<IConnection>();
            var mockChannel = new Mock<IModel>();

            mockConnection.Setup(c => c.IsOpen).Returns(true);
            mockChannel.Setup(c => c.IsOpen).Returns(true);

            mockFactory.Setup(f => f.CreateConnection()).Returns(mockConnection.Object);
            mockConnection.Setup(c => c.CreateModel()).Returns(mockChannel.Object);

            var _connection = new Connection(mockFactory.Object);

            var channel = new Channel(
                connection: _connection,
                queueName: "test-queue",
                exchangeName: "test-exchange",
                routingKey: "test-routing",
                exchangeType: "direct",
                retryChannelCount: 3,
                retryChannelDelayInSeconds: 1);

            var body = new byte[] { 0x01, 0x02 };
            var basicProperties = Mock.Of<IBasicProperties>();

            // Act
            channel.GetChannel(); // Ensures the channel is open
            channel.Publish(body, basicProperties);

            // Assert
            mockChannel.Verify(c => c.BasicPublish("test-exchange", "test-routing", false, basicProperties, body), Times.Once);
        }

        [Fact]
        public void Dispose_ShouldCloseAndDisposeChannel_WhenCalled()
        {
            // Arrange
            var mockFactory = new Mock<IConnectionFactory>();
            var mockConnection = new Mock<IConnection>();
            var mockChannel = new Mock<IModel>();

            mockConnection.Setup(c => c.IsOpen).Returns(true);
            mockChannel.Setup(c => c.IsOpen).Returns(true);

            mockFactory.Setup(f => f.CreateConnection()).Returns(mockConnection.Object);
            mockConnection.Setup(c => c.CreateModel()).Returns(mockChannel.Object);

            var _connection = new Connection(mockFactory.Object);

            var channel = new Channel(
                connection: _connection,
                queueName: "test-queue",
                exchangeName: "test-exchange",
                routingKey: "test-routing",
                exchangeType: "direct",
                retryChannelCount: 3,
                retryChannelDelayInSeconds: 1);

            // Act
            channel.GetChannel(); 
            channel.Dispose();

            // Assert
            mockChannel.Verify(c => c.Close(), Times.Once);
            mockChannel.Verify(c => c.Dispose(), Times.Once);
        }

        [Fact]
        public void Dispose_ShouldNotThrowException_WhenCalledMultipleTimes()
        {
            // Arrange
            var mockFactory = new Mock<IConnectionFactory>();
            var mockConnection = new Mock<IConnection>();
            var mockChannel = new Mock<IModel>();

            mockConnection.Setup(c => c.IsOpen).Returns(true);
            mockChannel.Setup(c => c.IsOpen).Returns(true);

            mockFactory.Setup(f => f.CreateConnection()).Returns(mockConnection.Object);
            mockConnection.Setup(c => c.CreateModel()).Returns(mockChannel.Object);

            var _connection = new Connection(mockFactory.Object);

            var channel = new Channel(
                connection: _connection,
                queueName: "test-queue",
                exchangeName: "test-exchange",
                routingKey: "test-routing",
                exchangeType: "direct",
                retryChannelCount: 3,
                retryChannelDelayInSeconds: 1);

            // Act
            channel.GetChannel(); 
            channel.Dispose();
            channel.Dispose();

            // Assert
            mockChannel.Verify(c => c.Close(), Times.Once);
            mockChannel.Verify(c => c.Dispose(), Times.Once);
        }
    }
}
