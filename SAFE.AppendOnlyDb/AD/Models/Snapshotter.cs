using SAFE.AppendOnlyDb.Network.AD;
using SAFE.Data;
using SAFE.Data.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IImDStore_v2 = SAFE.Data.Client.IImDStore_v2;

namespace SAFE.AppendOnlyDb.Snapshots
{
    public abstract class Snapshotter_v2
    {
        public Snapshotter_v2(ulong interval)
        {
            if (interval < 10) 
                throw new ArgumentOutOfRangeException(nameof(interval));
            Interval = interval;
        }

        public ulong Interval { get; }
        internal abstract Task<Result<byte[]>> StoreSnapshot(ISeqAppendOnly ad);
    }

    class EmptySnapshotter_v2 : Snapshotter_v2
    {
        public EmptySnapshotter_v2() : base(0) { }

        internal override Task<Result<byte[]>> StoreSnapshot(ISeqAppendOnly ad)
            => Task.FromResult(Result.OK(new byte[0]));
    }

    public class Snapshotter_v2<T> : Snapshotter_v2
    {
        readonly SnapshotReader _reader;
        readonly IImDStore_v2 _store;
        readonly Func<Snapshot, IAsyncEnumerable<T>, Task<Snapshot>> _leftFold;

        /// <summary>
        /// For snapshotting the IMdNode entries.
        /// </summary>
        /// <typeparam name="T">Type of the data to aggregate.</typeparam>
        /// <param name="store">Immutable data store.</param>
        /// <param name="leftFold">The aggregating function.</param>
        public Snapshotter_v2(ulong interval, IImDStore_v2 store, Func<Snapshot, IAsyncEnumerable<T>, Task<Snapshot>> leftFold)
            : base(interval)
        {
            _reader = new SnapshotReader(interval);
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _leftFold = leftFold ?? throw new ArgumentNullException(nameof(leftFold));
        }

        /// <summary>
        /// Stores a snapshot of all entries.
        /// </summary>
        /// <returns>Data map (pointer) to immutable data with serialized snapshot.</returns>
        internal override async Task<Result<byte[]>> StoreSnapshot(ISeqAppendOnly ad)
        {
            var nextIndex = ad.GetNextEntriesIndex();
            if (nextIndex.Value == 0 || nextIndex.Value % Interval != 0) // we've settled for this invariant: only snapshotting full Mds
                return new InvalidOperation<byte[]>("Can only snapshot at set interval!");

            var startIndex = nextIndex.Value == Interval ? 
                Index.Zero : 
                new Index(nextIndex.Next.Value - Interval);
            var entries = ad.GetInRange(startIndex, nextIndex);
            if (!entries.HasValue)
                return entries.CastError<List<(Index, Entry)>, byte[]>();
            var ordered = entries.Value
                .OrderBy(c => c.Item1)
                .Select(c => c.Item2.ToStoredValue().Parse<T>())
                .ToAsyncEnumerable();

            var (_, rawBytes) = await _reader.GetPreviousAsync(nextIndex, ad);
            Snapshot previous = default;
            if (rawBytes != null)
                previous = await GetSnapshotAsync(rawBytes);

            var snapshot = await _leftFold(previous, ordered);
            var pointer = await _store.StoreImDAsync(snapshot.Serialize());
            return Result.OK(pointer);
        }

        public async Task<Snapshot> GetSnapshotAsync(byte[] pointer)
        {
            var bytes = await _store.GetImDAsync(pointer);
            var snapshot = bytes.Parse<Snapshot>();
            return snapshot;
        }
    }

    public class SnapshotReader
    {
        public ulong Interval { get; }
        
        public SnapshotReader(ulong interval)
            => Interval = interval;

        internal Task<(Index, byte[])> GetPreviousAsync(Index nextUnusedIndex, ISeqAppendOnly stream)
        {
            if (nextUnusedIndex.Value == Interval)
                return Task.FromResult((default(Index), default(byte[])));

            var previous = nextUnusedIndex.Value;
            var rest = previous % Interval;
            var previousSnapshotIndex = new Index(previous - rest);

            byte[] previousSnapshot = null;
            var entryResult = stream.GetEntry(previousSnapshotIndex);
            if (entryResult.HasValue)
                previousSnapshot = entryResult.Value
                    .ToStoredValue()
                    .Parse<byte[]>();

            return Task.FromResult((previousSnapshotIndex, previousSnapshot));
        }
    }
}