using RabbitMq.Constants;
using System.Text;

namespace RabbitMq.Extensions
{
    public static class DictionaryExtension
    {
        public static IDictionary<string, object> ConvertToBytes(this IDictionary<string, object> dictionary)
        {
            foreach (var keyValye in dictionary)
                if (keyValye.Value is not IEnumerable<object>)
                    dictionary[keyValye.Key] = Encoding.UTF8.GetBytes(keyValye.Value.ToString());

            return dictionary;
        }

        public static IDictionary<string, object> ConvertToObject(this IDictionary<string, object> dictionary)
        {
            foreach (var keyValye in dictionary)
                if (keyValye.Value is not IEnumerable<object>)
                    dictionary[keyValye.Key] = Encoding.UTF8.GetString((byte[])keyValye.Value);

            return dictionary;
        }

        public static IDictionary<string, object> CreateHeaders(this IDictionary<string, object> dictionary)
        {
            dictionary.Add(ConstantsHeader.CreatedAt, DateTimeOffset.Now.ToString());
            return dictionary;
        }
    }
}
