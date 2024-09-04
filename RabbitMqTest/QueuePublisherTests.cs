using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using RabbitMq;
using RabbitMQ.Client;
using System.Text;

namespace RabbitMqTests
{
    public class QueuePublisherTests
    {
        private class TestModel
        {
            public string Content { get; set; }
        }

        private class TestQueuePublisher : QueuePublisher<TestModel>
        {
            public TestQueuePublisher(IServiceProvider services) : base(services) { }

            protected override string QueueName => "test-queue";
        }

        [Fact]
        public void Constructor_ShouldInitializeConnection()
        {
            // Arrange
            var serviceProvider = new Mock<IServiceProvider>();

            var mockFactory = new Mock<IConnectionFactory>();
            var mockConnection = new Mock<IConnection>();
            var mockChannel = new Mock<IModel>();

            mockConnection.Setup(c => c.IsOpen).Returns(true);
            mockChannel.Setup(c => c.IsOpen).Returns(true);

            mockFactory.Setup(f => f.CreateConnection()).Returns(mockConnection.Object);
            mockConnection.Setup(c => c.CreateModel()).Returns(mockChannel.Object);

            var _connection = new Connection(mockFactory.Object);

            serviceProvider.Setup(sp => sp.GetService(typeof(Connection))).Returns(_connection);

            // Act
            var publisher = new TestQueuePublisher(serviceProvider.Object);

            // Assert
            Assert.NotNull(publisher);
        }

        [Fact]
        public async Task Publish_ShouldPublishMessageWithHeaders()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "header1", "value1" },
                { "header2", DateTimeOffset.Now }
            };

            var serviceProvider = new Mock<IServiceProvider>();

            var mockFactory = new Mock<IConnectionFactory>();
            var mockConnection = new Mock<IConnection>();
            var mockChannel = new Mock<IModel>();
            var mockProperties = new Mock<IBasicProperties>();

            mockProperties.Setup(c => c.Headers).Returns(headers);
            mockConnection.Setup(c => c.IsOpen).Returns(true);
            mockChannel.Setup(c => c.IsOpen).Returns(true);
            mockChannel.Setup(c => c.CreateBasicProperties()).Returns(mockProperties.Object);

            mockFactory.Setup(f => f.CreateConnection()).Returns(mockConnection.Object);
            mockConnection.Setup(c => c.CreateModel()).Returns(mockChannel.Object);

            var _connection = new Connection(mockFactory.Object);
            _connection.PrepareConnection();

            serviceProvider.Setup(sp => sp.GetService(typeof(Connection))).Returns(_connection);

            var publisher = new TestQueuePublisher(serviceProvider.Object);

            // Act
            await publisher.Publish(new TestModel { Content = "Test" }, headers, CancellationToken.None);

            // Convert to ReadOnlyMemory<byte>
            var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new TestModel { Content = "Test" }));
            var readOnlyMemoryBody = new ReadOnlyMemory<byte>(body);

            // Assert
            mockChannel.Verify(c => c.BasicPublish(
                It.Is<string>(s => s == "test-queue-ex"),
                It.Is<string>(s => s == "key-test-queue"),
                It.Is<bool>(b => b == false),
                It.Is<IBasicProperties>(p => p == mockProperties.Object),
                It.Is<ReadOnlyMemory<byte>>(p => p.ToArray().SequenceEqual(readOnlyMemoryBody.ToArray())) 
            ), Times.Once);

            Assert.Equal(mockProperties.Object.Headers, headers);
        }
    }
}
