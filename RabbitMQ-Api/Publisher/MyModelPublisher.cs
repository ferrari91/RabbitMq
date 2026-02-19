using RabbitMq;
using RabbitMQ_Api.Model;

namespace RabbitMQ_Api.Publisher
{
    public class MyModelPublisher : QueuePublisher<MyModel>, IMyModelPublisher<MyModel>
    {
        public MyModelPublisher(Connection connection) : base(connection)
        {
        }

        protected override string Queue => "my-consumer";

        public async Task Publish(MyModel model, Dictionary<string, object> headers, CancellationToken ctx)
            => await PublishAsync(model, headers, ctx).ConfigureAwait(true);
    }
}