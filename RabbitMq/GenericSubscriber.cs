namespace RabbitMq
{
    public abstract class GenericSubscriber<TModel> : QueueSubscriber<TModel> where TModel : class
    {
        protected GenericSubscriber(IServiceProvider serviceProvider, Connection connection)
            : base(serviceProvider, connection)
        {
        }

        protected override string DelayedQueue => $"{Queue}-delayed";
        protected override string DeadQueue => $"{Queue}-dead";

        // legado
        protected override bool UseTransactionScope => true;

        protected override int RetryAttempts => 3;
        protected override int RetryDelaySeconds => 10;

        // Seu processamento “principal” continua igual
        protected override abstract Task ProcessMessage(IServiceProvider scopedProvider, MessageContext<TModel> context, CancellationToken ct);

        // ✅ “Override de exception” no formato que você quer (LEGACY)
        protected abstract Task ExceptionExecute(
            IServiceProvider provider,
            Exception exception,
            MessageContext<TModel>? context,
            int attempt,
            CancellationToken ct);

        // 🔥 Conecta o hook novo da base ao método legacy
        protected override Task OnMessageFailedAsync(
            IServiceProvider scopedProvider,
           MessageContext<TModel>? context,
            Exception exception,
            int attempt,
            CancellationToken ct)
        {
            return ExceptionExecute(scopedProvider, exception, context, attempt, ct);
        }
    }
}
