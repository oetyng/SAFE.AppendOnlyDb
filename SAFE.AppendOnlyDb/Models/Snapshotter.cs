using Newtonsoft.Json;
using SAFE.Data;
using SAFE.Data.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    public abstract class Snapshotter
    {
        internal abstract Task<Result<SnapshotPointer>> StoreSnapshot(IMdNode node);
    }

    class EmptySnapshotter : Snapshotter
    {
        internal override Task<Result<SnapshotPointer>> StoreSnapshot(IMdNode node)
            => Task.FromResult(Result.OK(new SnapshotPointer(new byte[0])));
    }

    public class Snapshotter<T> : Snapshotter
    {
        readonly Data.Client.IImDStore _store;
        readonly Func<Snapshot, IAsyncEnumerable<T>, Task<Snapshot>> _leftFold;

        /// <summary>
        /// For snapshotting the IMdNode entries.
        /// </summary>
        /// <typeparam name="T">Type of the data to aggregate.</typeparam>
        /// <param name="store">Immutable data store.</param>
        /// <param name="leftFold">The aggregating function.</param>
        public Snapshotter(Data.Client.IImDStore store, Func<Snapshot, IAsyncEnumerable<T>, Task<Snapshot>> leftFold)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _leftFold = leftFold ?? throw new ArgumentNullException(nameof(leftFold)); ;
        }

        /// <summary>
        /// Stores a snapshot of all entries.
        /// </summary>
        /// <returns>Data map (pointer) to immutable data with serialized snapshot.</returns>
        internal override async Task<Result<SnapshotPointer>> StoreSnapshot(IMdNode node)
        {
            if (!node.IsFull) // we've settled for this invariant: only snapshotting full Mds
                return new InvalidOperation<SnapshotPointer>("Cannot snapshot unless Md is full!");

            var entries = node.FindRangeAsync(node.StartIndex, node.EndIndex);
            var ordered = entries
                .OrderBy(c => c.Item1)
                .Select(c => c.Item2.Parse<T>());

            Snapshot previous = default;
            if (node.SnapshotPointer != null)
                previous = await GetSnapshotAsync(node.SnapshotPointer);

            var snapshot = await _leftFold(previous, ordered);
            var pointer = await _store.StoreImDAsync(snapshot.Serialize());
            return Result.OK(new SnapshotPointer(pointer));
        }

        public async Task<Snapshot> GetSnapshotAsync(SnapshotPointer pointer)
        {
            var bytes = await _store.GetImDAsync(pointer.Pointer);
            var snapshot = bytes.Parse<Snapshot>();
            return snapshot;
        }
    }
}