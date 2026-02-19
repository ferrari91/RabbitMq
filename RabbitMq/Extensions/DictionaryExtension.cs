using RabbitMq.Constants;
using System.Globalization;
using System.Text;

namespace RabbitMq.Extensions
{
    public static class DictionaryExtension
    {
        /// <summary>
        /// Normalizes headers to RabbitMQ-compatible types.
        /// - string/Guid/DateTime/DateTimeOffset => UTF8 byte[]
        /// - byte[] kept as-is
        /// - numeric/bool kept as-is
        /// Null values are removed.
        /// </summary>
        public static IDictionary<string, object> ConvertToBytes(this IDictionary<string, object> dictionary)
        {
            if (dictionary is null) return new Dictionary<string, object>();

            var keys = dictionary.Keys.ToList();
            foreach (var key in keys)
            {
                var value = dictionary[key];
                if (value is null)
                {
                    dictionary.Remove(key);
                    continue;
                }

                if (value is byte[] || value is ReadOnlyMemory<byte> || value is Memory<byte>)
                    continue;

                if (value is string s)
                {
                    dictionary[key] = Encoding.UTF8.GetBytes(s);
                    continue;
                }

                if (value is Guid g)
                {
                    dictionary[key] = Encoding.UTF8.GetBytes(g.ToString());
                    continue;
                }

                if (value is DateTimeOffset dto)
                {
                    dictionary[key] = Encoding.UTF8.GetBytes(dto.ToString("O", CultureInfo.InvariantCulture));
                    continue;
                }

                if (value is DateTime dt)
                {
                    dictionary[key] = Encoding.UTF8.GetBytes(dt.ToString("O", CultureInfo.InvariantCulture));
                    continue;
                }

                // keep primitives (int/long/bool/etc.) as-is; RabbitMQ client can serialize these types
                var t = value.GetType();
                if (t.IsPrimitive || value is decimal)
                    continue;

                // fallback: stringify
                dictionary[key] = Encoding.UTF8.GetBytes(value.ToString()!);
            }

            return dictionary;
        }

        /// <summary>
        /// Converts byte[] header values to UTF8 strings, keeping non-byte values untouched.
        /// </summary>
        public static IDictionary<string, object> ConvertToObject(this IDictionary<string, object> dictionary)
        {
            if (dictionary is null) return new Dictionary<string, object>();

            var keys = dictionary.Keys.ToList();
            foreach (var key in keys)
            {
                var value = dictionary[key];

                if (value is byte[] bytes)
                {
                    dictionary[key] = Encoding.UTF8.GetString(bytes);
                }
                else if (value is ReadOnlyMemory<byte> rom)
                {
                    dictionary[key] = Encoding.UTF8.GetString(rom.Span);
                }
            }

            return dictionary;
        }

        public static IDictionary<string, object> CreateHeaders(this IDictionary<string, object> dictionary)
        {
            dictionary ??= new Dictionary<string, object>();
            if (!dictionary.ContainsKey(ConstantsHeader.CreatedAt))
                dictionary.Add(ConstantsHeader.CreatedAt, DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
            return dictionary;
        }
    }
}
