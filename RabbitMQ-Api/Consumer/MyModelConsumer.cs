using Newtonsoft.Json;
using RabbitMq;
using RabbitMQ_Api.Model;
using RabbitMQ_Api.Statics;

namespace RabbitMQ_Api.Consumer
{
    public class MyModelConsumer : GenericSubscriber<MyModel>
    {
        public MyModelConsumer(IServiceProvider serviceProvider, Connection connection) : base(serviceProvider, connection)
        {
        }

        protected override string Queue => "my-consumer";
        protected override string DeadQueue => "my-consumer-exception";
        protected override string DelayedQueue => "my-consumer-retry";
        protected override int RetryDelaySeconds => 5;
        protected override async Task ProcessMessage(IServiceProvider scopedProvider, MessageContext<MyModel> context, CancellationToken ct)
        {
            if (Define.ShouldThrow)
                throw new ArgumentException($"Sent throw by flag: {nameof(Define.ShouldThrow)}");

            Console.WriteLine($"Headers:{JsonConvert.SerializeObject(context.Headers)}");
            Console.WriteLine($"Sucess Message{Environment.NewLine}==========={Environment.NewLine}{JsonConvert.SerializeObject(context.Model)}{Environment.NewLine}===========");
            await Task.CompletedTask;
        }

        protected override async Task ExceptionExecute(IServiceProvider provider, Exception exception, MessageContext<MyModel>? context, int attempt,
            CancellationToken ct)
        {
            Console.WriteLine($"Error Message{Environment.NewLine}==========={Environment.NewLine}{JsonConvert.SerializeObject(exception.Message)}StackTrace:{JsonConvert.SerializeObject(exception.StackTrace)}{Environment.NewLine}===========");
        }

        protected override int RetryAttempts => 3;
    }
}
