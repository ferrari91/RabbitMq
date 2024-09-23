using RabbitMq;
using RabbitMQ_Api.Model;

namespace RabbitMQ_Api.Publisher
{
    public class MyModelPublisher : QueuePublisher<MyModel>, IMyModelPublisher<MyModel>
    {
        public MyModelPublisher(IServiceProvider services) : base(services)
        {
        }

        protected override string QueueName => "my-consumer";
    }
}
