using Moq;
using RabbitMq;
using RabbitMQ.Client;

namespace RabbitMqTest
{
    public class ConnectionTests
    {
        private readonly Mock<IConnectionFactory> _mockFactory;
        private readonly Mock<IConnection> _mockConnection;
        private readonly Mock<IModel> _mockChannel;
        private readonly Connection _connection;

        public ConnectionTests()
        {
            _mockFactory = new Mock<IConnectionFactory>();
            _mockConnection = new Mock<IConnection>();
            _mockChannel = new Mock<IModel>();

            _mockFactory.Setup(f => f.CreateConnection()).Returns(_mockConnection.Object);
            _mockConnection.Setup(c => c.CreateModel()).Returns(_mockChannel.Object);

            _connection = new Connection(_mockFactory.Object);
        }

        private void SetupConnectionOpen(bool isOpen)
        {
            _mockConnection.Setup(c => c.IsOpen).Returns(isOpen);
        }

        [Fact]
        public void PrepareConnection_Should_Open_Connection_If_Not_Already_Open()
        {
            // Arrange
            SetupConnectionOpen(false); 

            _mockFactory.Setup(f => f.CreateConnection())
                        .Callback(() => SetupConnectionOpen(true))  
                        .Returns(_mockConnection.Object);

            // Act
            _connection.PrepareConnection();

            // Assert
            _mockFactory.Verify(f => f.CreateConnection(), Times.Once);
            _mockConnection.Verify(c => c.IsOpen, Times.AtLeastOnce);
        }

        [Fact]
        public void PrepareConnection_Should_Not_Reopen_Connection_If_Already_Open()
        {
            // Arrange
            SetupConnectionOpen(true);
            _connection.PrepareConnection();  // Open the connection first

            // Act
            _connection.PrepareConnection();

            // Assert
            _mockFactory.Verify(f => f.CreateConnection(), Times.Once);
        }

        [Fact]
        public void PrepareConnection_Should_Throw_InvalidOperationException_If_Connection_Fails()
        {
            // Arrange
            _mockFactory.Setup(f => f.CreateConnection()).Throws(new Exception());

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _connection.PrepareConnection());
        }

        [Fact]
        public void CreateChannel_Should_Return_Channel_If_Connection_Is_Open()
        {
            // Arrange
            SetupConnectionOpen(true);

            // Act
            _connection.PrepareConnection();
            var channel = _connection.CreateChannel();

            // Assert
            Assert.NotNull(channel);
            _mockConnection.Verify(c => c.CreateModel(), Times.Once);
        }

        [Fact]
        public void CreateChannel_Should_Throw_InvalidOperationException_If_Connection_Is_Not_Open()
        {
            // Arrange
            SetupConnectionOpen(false);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _connection.CreateChannel());
        }

        [Fact]
        public void Dispose_Should_Close_And_Dispose_Connection()
        {
            // Arrange
            SetupConnectionOpen(true);
            _connection.PrepareConnection();

            // Act
            _connection.Dispose();

            // Assert
            _mockConnection.Verify(c => c.Close(It.IsAny<TimeSpan>()), Times.Once);
            _mockConnection.Verify(c => c.Dispose(), Times.Once);
        }

        [Fact]
        public void Dispose_Should_Not_Dispose_If_Already_Disposed()
        {
            // Arrange
            SetupConnectionOpen(true);
            _connection.PrepareConnection();
            _connection.Dispose();

            // Act
            _connection.Dispose();

            // Assert
            _mockConnection.Verify(c => c.Close(It.IsAny<TimeSpan>()), Times.Once);
            _mockConnection.Verify(c => c.Dispose(), Times.Once);
        }
    }

}
