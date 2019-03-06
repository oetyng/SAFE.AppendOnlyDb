using Newtonsoft.Json;
using System;
using System.Linq;

namespace SAFE.AppendOnlyDb.Utils
{
    public static class Serializer
    {
        public static JsonSerializerSettings SerializerSettings;

        static Serializer()
        {
            SerializerSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                Culture = new System.Globalization.CultureInfo(string.Empty)
                {
                    NumberFormat = new System.Globalization.NumberFormatInfo
                    {
                        CurrencyDecimalDigits = 31
                    }
                },
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                ContractResolver = new PrivateMembersContractResolver()
            };
            SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            JsonConvert.DefaultSettings = () => SerializerSettings;
        }

        public static string Json(this object data)
            => JsonConvert.SerializeObject(data, SerializerSettings);

        public static T Parse<T>(this string json)
            => JsonConvert.DeserializeObject<T>(json, SerializerSettings);

        public static bool TryParse<T>(this string json, out T result)
        {
            try
            {
                result = JsonConvert.DeserializeObject<T>(json, SerializerSettings);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        public static object Parse(this string json, string typeName)
        {
            var type = Type.GetType(typeName);
            if (type == null)
            {
                var calling = System.Reflection.Assembly.GetCallingAssembly();
                var entry = System.Reflection.Assembly.GetEntryAssembly();
                var executing = System.Reflection.Assembly.GetExecutingAssembly();
                var assemblies = new[] { calling, entry, executing };
                type = assemblies.SelectMany(c => c.GetTypes().Where(t => t.Name == typeName)).FirstOrDefault();
            }
            
            return JsonConvert.DeserializeObject(json, type, SerializerSettings); // //JsonConvert.DefaultSettings = () => SerializerSettings;
        }
    }
}