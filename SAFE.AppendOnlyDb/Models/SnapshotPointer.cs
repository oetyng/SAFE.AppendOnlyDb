
using Newtonsoft.Json;

namespace SAFE.AppendOnlyDb.Snapshots
{
    /// <summary>
    /// Stores a reference to an immutable data,
    /// which will hold the actual snapshot.
    /// </summary>
    public class SnapshotPointer
    {
        [JsonConstructor]
        SnapshotPointer() { }

        public SnapshotPointer(byte[] pointer)
            => Pointer = pointer;

        /// <summary>
        /// The pointer is a so-called
        /// data map, which resolves to an
        /// immutable data instance, when passed to
        /// the GetImDAsync method of an IImdStore.
        /// </summary>
        public byte[] Pointer { get; set; }
    }
}
