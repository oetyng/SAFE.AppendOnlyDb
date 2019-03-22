using Newtonsoft.Json;
using SAFE.AppendOnlyDb.Utils;

namespace SAFE.AppendOnlyDb
{
    public class StoredValue
    {
        [JsonConstructor]
        StoredValue()
        { }

        public StoredValue(object data)
        {
            Payload = data.Json();
            ValueType = data.GetType().AssemblyQualifiedName;
        }

        public string Payload { get; set; }
        public string ValueType { get; set; }

        public T Parse<T>() => (T)Payload.Parse(ValueType);
    }
}