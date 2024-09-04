using RabbitMq;

namespace RabbitMQ_Api.Consumer
{
    public class Consumer : GenericSubscriber<ConsumerModel>
    {
        static int retry = 0;


        public Consumer(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override string Queue => "consumer";

        protected override void ExceptionExecute(IServiceProvider provider, Exception exception, Context<ConsumerModel> context, int attempt, CancellationToken ctx)
        {
            retry++;
            Console.WriteLine(exception.ToString());
        }

        protected override async Task ProcessMessage(IServiceProvider serviceProvider, Context<ConsumerModel> context, CancellationToken stoppingToken)
        {
            Console.WriteLine($"ConsumerName: {context.Message.Name}");


            throw new ArgumentNullException(nameof(Consumer));

            await Task.CompletedTask;
        }
    }

    public class ConsumerModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public class PublisherConsumer : QueuePublisher<ConsumerModel>
    {
        public PublisherConsumer(IServiceProvider services) : base(services)
        {
        }

        protected override string QueueName => "consumer";
    }
}
