using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SAFE.AppendOnlyDb.Snapshots;
using SAFE.Data;
using SAFE.Data.Utils;

namespace SAFE.AppendOnlyDb.Network.AD
{
    public static class DataHelper
    {
        internal static Entry ToEntry(this StoredValue value)
            => new Entry
            {
                Key = new byte[0],
                Value = value.GetBytes()
            };

        internal static List<Entry> ToEntries(this List<StoredValue> values)
            => values.Select(c => c.ToEntry()).ToList();

        internal static List<Entry> ToEntries(this StoredValue value)
            => new[] { value.ToEntry() }.ToList();

        internal static StoredValue ToStoredValue(this Entry entry)
            => entry.Value.Parse<StoredValue>();

        internal static Index AsIndex(this ulong index)
            => new Index(index);
    }

    internal class ValueStream : IStreamAD_v2, IValueAD_v2
    {
        readonly ISeqAppendOnly _stream;
        readonly Snapshotter_v2 _snapshotter;

        public Address Address => _stream.GetAddress();

        public ValueStream(ISeqAppendOnly stream, Snapshotter_v2 snapshotter)
        {
            _stream = stream;
            _snapshotter = snapshotter;
        }

        #region ValueAD

        public Task<Result<StoredValue>> GetValueAsync()
        {
            var res = _stream.GetLastEntry();
            if (res.HasValue)
                return Task.FromResult(Result.OK(res.Value.ToStoredValue()));
            else
                return Task.FromResult(res.CastError<Entry, StoredValue>());
        }

        public Task<Result<Index>> SetAsync(StoredValue value)
            => TryAppendAsync(value, ExpectedVersion.Any);

        public Task<Result<Index>> TrySetAsync(StoredValue value, ExpectedVersion expectedVersion)
            => TryAppendAsync(value, expectedVersion);

        #endregion ValueAD


        #region StreamAD

        public Task<Result<Index>> AppendAsync(StoredValue value)
            => TryAppendAsync(value, ExpectedVersion.Any);

        public Task<Result<Index>> AppendRangeAsync(List<StoredValue> values)
           => TryAppendRangeAsync(values, ExpectedVersion.Any);

        public Task<Result<Index>> TryAppendAsync(StoredValue value, ExpectedVersion expectedVersion)
            => TryAppendRangeAsync(new[] { value }.ToList(), expectedVersion);
        
        public Task<Result<Index>> TryAppendRangeAsync(List<StoredValue> values, ExpectedVersion expectedVersion)
        {
            return expectedVersion switch
            {
                NoVersion _ => AppendAndTrySnapshotRangeAsync(values, Index.Zero),
                SpecificVersion some => AppendAndTrySnapshotRangeAsync(values, some.Value.Value.AsIndex()),
                AnyVersion _ => AppendAndTrySnapshotRangeAsync(values, _stream.GetNextEntriesIndex()),
                _ => Task.FromResult((Result<Index>)new ArgumentOutOfRange<Index>()),
            };
        }

        async Task<Result<Index>> AppendAndTrySnapshotRangeAsync(List<StoredValue> values, Index index)
        {
            if (CanSnapshot(index))
            {
                var pointer = await _snapshotter.StoreSnapshot(_stream);
                if (!pointer.HasValue)
                    return pointer.CastError<byte[], Index>();
                var snapShotResult = await _stream.AppendAsync(new StoredValue(pointer.Value).ToEntries(), index);
                if (!snapShotResult.HasValue)
                    return snapShotResult;
                index = snapShotResult.Value;
            }
            return await _stream.AppendAsync(values.ToEntries(), index);
        }

        bool CanSnapshot(Index index)
            => _snapshotter != null && index.Value > 0 && index.Value % _snapshotter.Interval == 0;

        public IAsyncEnumerable<(Index, StoredValue)> GetAllIndexValuesAsync()
            => ReadForwardFromAsync(Index.Zero);

        public IAsyncEnumerable<StoredValue> GetAllValuesAsync()
            => _stream.GetEntries().Select((c) => c.ToStoredValue()).ToAsyncEnumerable();

        public Task<Result<StoredValue>> GetAtIndexAsync(Index index)
        {
            var result = _stream.GetInRange(index, index);
            if (result.HasValue)
                return Task.FromResult(Result.OK(result.Value.First().Item2.ToStoredValue()));
            else return Task.FromResult(result.CastError<List<(Index, Entry)>, StoredValue>());
        }

        public IAsyncEnumerable<(Index, StoredValue)> GetRangeAsync(Index from, Index to)
            => Map(_stream.GetInRange(from, to));

        public IAsyncEnumerable<(Index, StoredValue)> ReadBackwardsFromAsync(Index from)
            => Map(_stream.GetInRange(from, 0UL.AsIndex()));

        public IAsyncEnumerable<(Index, StoredValue)> ReadForwardFromAsync(Index from)
            => Map(_stream.GetInRange(from, _stream.GetNextEntriesIndex()));

        IAsyncEnumerable<(Index, StoredValue)> Map(Result<List<(Index, Entry)>> result)
        {
            if (result.HasValue)
                return Map(result.Value);
            else return new List<(Index, StoredValue)>().ToAsyncEnumerable();
        }

        IAsyncEnumerable<(Index, StoredValue)> Map(IEnumerable<(Index, Entry)> entries)
            => entries.Select(c => (c.Item1, c.Item2.ToStoredValue())).ToAsyncEnumerable();

        public async Task<Result<SnapshotReading>> ReadFromSnapshot()
        {
            var nextUnusedIndex = _stream.GetNextEntriesIndex();
            if (_snapshotter.Interval + 1 > nextUnusedIndex.Value)
                return new ArgumentOutOfRange<SnapshotReading>("No snapshot in the stream.");

            var lastEntry = _stream.GetLastEntry();
            var lastEntryIndex = new Index(nextUnusedIndex.Value - 1);
            if (!lastEntry.HasValue)
                return new DataNotFound<SnapshotReading>("No data in the stream.");

            var reader = new SnapshotReader(_snapshotter.Interval);
            var (previousSnapshotIndex, previousSnapshot) = await reader.GetPreviousAsync(nextUnusedIndex, _stream);

            var reading = new SnapshotReading
            {
                SnapshotMap = previousSnapshot,
                NewEvents = _stream.GetInRange(previousSnapshotIndex.Next, lastEntryIndex)
                            .Value
                            .Select(c => (c.Item1.Value, c.Item2.ToStoredValue()))
                            .ToAsyncEnumerable()
            };
            return Result.OK(reading);
        }

        #endregion StreamAD
    }
}
