using Newtonsoft.Json;
using SAFE.Data.Utils;
using System.Text;

namespace SAFE.AppendOnlyDb.Snapshots
{
    public class Snapshot
    {
        [JsonConstructor]
        Snapshot() { }

        public Snapshot(object data)
        {
            Payload = data.GetBytes();
            AssemblyQualifiedName = data.GetType().AssemblyQualifiedName;
        }

        public byte[] Payload { get; set; }
        public string AssemblyQualifiedName { get; set; }

        public static Snapshot Get(byte[] data) => data.Parse<Snapshot>();

        public byte[] Serialize() => this.GetBytes();


        public TState GetState<TState>()
            => (TState)Encoding.UTF8.GetString(Payload).Parse(AssemblyQualifiedName);
    }
}
