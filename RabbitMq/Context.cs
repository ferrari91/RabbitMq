namespace RabbitMq
{
    public readonly struct Context<TModel>
    {
        public Context(IServiceProvider serviceProvider, TModel model, IDictionary<string, object> properties)
        {
            Services = serviceProvider;
            Message = model;
            Headers = properties;
        }

        public IServiceProvider Services { get; }
        public TModel Message { get; }
        public IDictionary<string, object> Headers { get; }
    }
}
