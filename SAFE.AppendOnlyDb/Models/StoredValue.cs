using Newtonsoft.Json;
using SAFE.AppendOnlyDb.Utils;

namespace SAFE.AppendOnlyDb
{
    internal class StoredValue
    {
        [JsonConstructor]
        StoredValue()
        { }

        public StoredValue(object data)
        {
            Payload = data.Json();
            ValueType = data.GetType().FullName;
        }

        public string Payload { get; set; }

        public string ValueType { get; set; }

        public T Parse<T>()
        {
            //return Payload.Parse<T>();
            return (T)Payload.Parse(ValueType);
        }
    }
}