using Newtonsoft.Json;
using RabbitMq;
using RabbitMQ_Api.Model;
using RabbitMQ_Api.Statics;

namespace RabbitMQ_Api.Consumer
{
    public class MyModelConsumer : GenericSubscriber<MyModel>
    {
        public MyModelConsumer(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override string Queue => "my-consumer";
        protected override string DeadQueue => "my-consumer-exception";
        protected override string DelayedQueue => "my-consumer-retry";
        protected override int RetryDelay => 5;
        protected override int RetryAttempts => 3;

        protected override void ExceptionExecute(IServiceProvider provider, Exception exception, Context<MyModel> context, int attempt, CancellationToken ctx)
        {
            Console.WriteLine($"Error Message{Environment.NewLine}==========={Environment.NewLine}{JsonConvert.SerializeObject(exception.Message)}StackTrace:{JsonConvert.SerializeObject(exception.StackTrace)}{Environment.NewLine}===========");
        }

        protected override async Task ProcessMessage(IServiceProvider serviceProvider, Context<MyModel> context, CancellationToken stoppingToken)
        {
            if (Define.ShouldThrow)
                throw new ArgumentException($"Sent throw by flag: {nameof(Define.ShouldThrow)}");

            Console.WriteLine($"Sucess Message{Environment.NewLine}==========={Environment.NewLine}{JsonConvert.SerializeObject(context.Message)}{Environment.NewLine}===========");
            await Task.CompletedTask;
        }
    }
}
