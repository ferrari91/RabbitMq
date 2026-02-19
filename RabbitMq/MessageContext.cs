namespace RabbitMq;

public sealed class MessageContext<TModel> where TModel : class
{
    public required TModel Model { get; init; }
    public required IDictionary<string, object?> Headers { get; init; }
    public required int Attempt { get; init; }
    public required ulong DeliveryTag { get; init; }
}