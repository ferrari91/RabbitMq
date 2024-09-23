namespace RabbitMQ_Api.Publisher
{
    public interface IMyModelPublisher<TModel> where TModel : class
    {
        Task Publish(TModel model, Dictionary<string, object> headers, CancellationToken ctx);
    }
}
