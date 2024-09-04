using RabbitMq.Constants;
using RabbitMQ.Client;

namespace RabbitMq.Extensions
{
    public static class BasicPropertiesExtension
    {
        public static (int Attempt, IDictionary<string, object> Headers) GetHeader(this IBasicProperties properties)
        {
            var headers = properties.Headers is not null ? properties.Headers.ConvertToObject() : new Dictionary<string, object>().CreateHeaders();

            var attempt = headers.TryGetValue(ConstantsHeader.Attempt, out object attemptHeader) ? int.Parse(attemptHeader.ToString()) : 1;
            var createdAt = headers.TryGetValue(ConstantsHeader.CreatedAt, out object createdAtHeader) ? true : false;

            if (!createdAt)
                headers.Add(ConstantsHeader.CreatedAt, DateTimeOffset.Now.ToString());

            headers[ConstantsHeader.Attempt] = attempt;

            return (attempt, headers);
        }
    }
}
