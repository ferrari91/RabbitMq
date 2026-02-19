using RabbitMq.Constants;
using RabbitMQ.Client;
using System.Globalization;

namespace RabbitMq.Extensions
{
    public static class BasicPropertiesExtension
    {
        public static (int Attempt, IDictionary<string, object> Headers) GetHeader(this IReadOnlyBasicProperties properties)
        {
            var headers = properties.Headers is not null
                ? properties.Headers.ConvertToObject()
                : new Dictionary<string, object>().CreateHeaders();

            var attempt = 1;

            if (headers.TryGetValue(ConstantsHeader.Attempt, out var attemptHeader) && attemptHeader is not null)
            {
                if (attemptHeader is int i) attempt = i;
                else if (attemptHeader is long l) attempt = (int)l;
                else if (int.TryParse(attemptHeader.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    attempt = parsed;
            }

            if (!headers.ContainsKey(ConstantsHeader.CreatedAt))
                headers.Add(ConstantsHeader.CreatedAt, DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));

            headers[ConstantsHeader.Attempt] = attempt;

            return (attempt, headers);
        }
    }
}
