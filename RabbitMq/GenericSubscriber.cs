namespace RabbitMq
{
    public abstract class GenericSubscriber<TModel> : QueueSubscriber<TModel> where TModel : class
    {
        protected GenericSubscriber(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override string DelayedQueue => $"{Queue}-delead";
        protected override string DeadQueue => $"{Queue}-dead";
        protected override bool UseTransactionScope => true;
        protected override int RetryAttempts => 3;
        protected override int RetryDelay => 10;

        protected override abstract Task ProcessMessage(IServiceProvider serviceProvider, Context<TModel> context, CancellationToken stoppingToken);
        protected override abstract void ExceptionExecute(IServiceProvider provider, Exception exception, Context<TModel> context, int attempt, CancellationToken ctx);
    }
}
