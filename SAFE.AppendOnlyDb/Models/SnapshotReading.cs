using System.Collections.Generic;

namespace SAFE.AppendOnlyDb.Snapshots
{
    public class SnapshotReading
    {
        /// <summary>
        /// The data map to an immutable data
        /// with the serialized Snapshot instance.
        /// </summary>
        public SnapshotPointer SnapshotPointer { get; set; }

        /// <summary>
        /// All new events since the snapshot was taken.
        /// </summary>
        public IAsyncEnumerable<(ulong, StoredValue)> NewEvents { get; set; }
    }
}
