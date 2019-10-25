using SAFE.AppendOnlyDb.Network;
using SAFE.Data;
using SAFE.Data.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IImDStore = SAFE.Data.Client.IImDStore;


namespace SAFE.AppendOnlyDb.Snapshots
{
    internal class Snapshotter<T> : Snapshotter
    {
        readonly IImDStore _store;
        readonly Func<Snapshot, IAsyncEnumerable<T>, Task<Snapshot>> _leftFold;

        /// <summary>
        /// For snapshotting the IMdNode entries.
        /// </summary>
        /// <typeparam name="T">Type of the data to aggregate.</typeparam>
        /// <param name="store">Immutable data store.</param>
        /// <param name="leftFold">The aggregating function.</param>
        public Snapshotter(ulong interval, IImDStore store, Func<Snapshot, IAsyncEnumerable<T>, Task<Snapshot>> leftFold)
            : base(interval)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _leftFold = leftFold ?? throw new ArgumentNullException(nameof(leftFold));
        }

        internal override bool IsSnapshotIndex(Index index)
        {
            if (index.Value == Interval) return true;
            var count = index.Value / Interval;
            var rest = index.Value % Interval;
            if (count - rest == 1) return true;
            return false;
        }

        /// <summary>
        /// Stores a snapshot of all entries.
        /// </summary>
        /// <returns>Data map (pointer) to immutable data with serialized snapshot.</returns>
        internal override async Task<Result<SnapshotPointer>> StoreSnapshotAsync(ISeqAppendOnly stream)
        {
            var sourceDataResult = await GetSourceDataAsync(stream);
            if (!sourceDataResult.HasValue)
                return sourceDataResult.CastError<SnapshotSourceData, SnapshotPointer>();

            var sourceData = sourceDataResult.Value;
            var newSnapshot = await _leftFold(sourceData.PreviousSnapshot, sourceData.NewEvents);
            var pointer = await _store.StoreImDAsync(newSnapshot.Serialize());
            return Result.OK(new SnapshotPointer(pointer));
        }

        internal override async Task<Result<SnapshotReading>> GetSnapshotReading(ISeqAppendOnly stream)
        {
            var validation = await ValidateRequestAsync(stream);
            if (!validation.HasValue)
                return validation.CastError<Index, SnapshotReading>();
            
            var expectedIndex = validation.Value;
            var lastEntryIndex = new Index(expectedIndex.Value - 1);
            var (indexOfPreviousSnapshot, previousSnapshot) = await TryGetPointerToPreviousAsync(stream, expectedIndex);

            var reading = new SnapshotReading
            {
                SnapshotPointer = previousSnapshot,
                NewEvents = stream.GetEntriesRange(indexOfPreviousSnapshot.Next, expectedIndex)
                            .Value
                            .Select(c => (c.Item1.Value, c.Item2.ToStoredValue()))
                            .ToAsyncEnumerable()
            };
            return Result.OK(reading);
        }

        // can be called for any index, even when expectedIndex is NOT SnapshotIndex
        Task<(Index, SnapshotPointer)> TryGetPointerToPreviousAsync(ISeqAppendOnly stream, Index expectedIndex)
        {
            var previousSnapshotIndex = TryGetPreviousIndex(expectedIndex);
            if (!previousSnapshotIndex.HasValue)
                return Task.FromResult((default(Index), default(SnapshotPointer)));

            SnapshotPointer previousSnapshot = null;
            var entryResult = stream.GetEntry(previousSnapshotIndex.Value);
            if (entryResult.HasValue)
                previousSnapshot = entryResult.Value
                    .ToStoredValue()
                    .Parse<SnapshotPointer>();

            return Task.FromResult((previousSnapshotIndex.Value, previousSnapshot));
        }

        Result<Index> TryGetPreviousIndex(Index expectedIndex)
        {
            if (expectedIndex.Value < (Interval + 1))
                return new ArgumentOutOfRange<Index>();

            var unit = new Index(1);
            while (!IsSnapshotIndex(expectedIndex - unit)) // todo: optimize
                expectedIndex -= unit;
            return Result.OK(expectedIndex - unit);
        }

        // todo: improve this, currently has bad naming and iffy logic
        async Task<Result<Index>> ValidateRequestAsync(ISeqAppendOnly stream)
        {
            var expectedIndex = stream.GetExpectedEntriesIndex();
            if (Interval + 1 > expectedIndex.Value)
                return new ArgumentOutOfRange<Index>("No snapshot in the stream.");

            var lastEntry = stream.GetLastEntry();
            if (!lastEntry.HasValue)
                return new DataNotFound<Index>("No data in the stream.");

            return Result.OK(expectedIndex);
        }

        // is only called when IsSnapshotIndex(expectedIndex) == true
        async Task<Result<SnapshotSourceData>> GetSourceDataAsync(ISeqAppendOnly stream)
        {
            var expectedIndex = stream.GetExpectedEntriesIndex();
            if (!IsSnapshotIndex(expectedIndex))
                return new ArgumentOutOfRange<SnapshotSourceData>();

            var (_, pointerToPrevious) = await TryGetPointerToPreviousAsync(stream, expectedIndex);
            var newEvents = await GetNewEventsAsync(stream, expectedIndex);
            if (!newEvents.HasValue)
                return newEvents.CastError<IAsyncEnumerable<T>, SnapshotSourceData>();

            Snapshot previousSnapshot = default;
            if (pointerToPrevious != null)
                previousSnapshot = await GetSnapshotAsync(pointerToPrevious);

            return Result.OK(new SnapshotSourceData(previousSnapshot, newEvents.Value));
        }

        async Task<Result<IAsyncEnumerable<T>>> GetNewEventsAsync(ISeqAppendOnly stream, Index expectedIndex)
        {
            // todo: fix this, with improper value of index passed in, it would crash
            var startIndex = expectedIndex.Value == Interval ?
                Index.Zero :
                TryGetPreviousIndex(expectedIndex).Value.Next;
            var entries = stream.GetEntriesRange(startIndex, expectedIndex);
            if (!entries.HasValue)
                return entries.CastError<List<(Index, Entry)>, IAsyncEnumerable<T>>();

            var ordered = entries.Value
                .OrderBy(c => c.Item1)
                .Select(c => c.Item2.ToStoredValue().Parse<T>())
                .ToAsyncEnumerable();

            return Result.OK(ordered);
        }

        async Task<Snapshot> GetSnapshotAsync(SnapshotPointer pointer)
        {
            var bytes = await _store.GetImDAsync(pointer.Pointer);
            var snapshot = bytes.Parse<Snapshot>();
            return snapshot;
        }

        class SnapshotSourceData
        {
            public SnapshotSourceData(Snapshot previousSnapshot, IAsyncEnumerable<T> newEvents)
            {
                PreviousSnapshot = previousSnapshot;
                NewEvents = newEvents;
            }

            public Snapshot PreviousSnapshot { get; }
            public IAsyncEnumerable<T> NewEvents { get; }
        }
    }

    public abstract class Snapshotter
    {
        public Snapshotter(ulong interval)
        {
            if (interval < 10)
                throw new ArgumentOutOfRangeException(nameof(interval));
            Interval = interval;
        }

        public ulong Interval { get; }
        internal abstract bool IsSnapshotIndex(Index index);
        internal abstract Task<Result<SnapshotPointer>> StoreSnapshotAsync(ISeqAppendOnly stream);
        internal abstract Task<Result<SnapshotReading>> GetSnapshotReading(ISeqAppendOnly stream);
    }

    class EmptySnapshotter : Snapshotter
    {
        public EmptySnapshotter() : base(0) { }

        internal override Task<Result<SnapshotPointer>> StoreSnapshotAsync(ISeqAppendOnly stream)
            => Task.FromResult(Result.OK(new SnapshotPointer(new byte[0])));

        internal override Task<Result<SnapshotReading>> GetSnapshotReading(ISeqAppendOnly stream)
                => default;

        internal override bool IsSnapshotIndex(Index index)
            => false;
    }
}