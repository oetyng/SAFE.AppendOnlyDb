using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SAFE.AppendOnlyDb.Snapshots;
using SAFE.Data;

namespace SAFE.AppendOnlyDb.Network
{
    internal class ValueStream : IStreamAD, IValueAD
    {
        readonly ISeqAppendOnly _stream;
        readonly Snapshotter _snapshotter;

        public Address Address => _stream.GetAddress();

        public ValueStream(ISeqAppendOnly stream, Snapshotter snapshotter)
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
            => TryAppendAsync(value, ExpectedIndex.Any);

        public Task<Result<Index>> TrySetAsync(StoredValue value, ExpectedIndex expectedIndex)
            => TryAppendAsync(value, expectedIndex);

        #endregion ValueAD


        #region StreamAD

        public Task<Result<Index>> AppendAsync(StoredValue value)
            => TryAppendAsync(value, ExpectedIndex.Any);

        public Task<Result<Index>> AppendRangeAsync(List<StoredValue> values)
           => TryAppendRangeAsync(values, ExpectedIndex.Any);

        public Task<Result<Index>> TryAppendAsync(StoredValue value, ExpectedIndex expectedIndex)
            => TryAppendRangeAsync(new[] { value }.ToList(), expectedIndex);
        
        public Task<Result<Index>> TryAppendRangeAsync(List<StoredValue> values, ExpectedIndex expectedIndex)
        {
            return expectedIndex switch
            {
                SpecificIndex some => TrySnapshotAndAppendRangeAsync(values, some.Value.Value.AsIndex()),
                AnyIndex _ => TrySnapshotAndAppendRangeAsync(values, _stream.GetExpectedEntriesIndex()),
                _ => Task.FromResult((Result<Index>)new ArgumentOutOfRange<Index>()),
            };
        }

        async Task<Result<Index>> TrySnapshotAndAppendRangeAsync(List<StoredValue> values, Index index)
        {
            if (CanSnapshot(index))
            {
                var snapshotPointer = await _snapshotter.StoreSnapshotAsync(_stream);
                if (!snapshotPointer.HasValue)
                    return snapshotPointer.CastError<SnapshotPointer, Index>();
                var nextIndex = await _stream.AppendRangeAsync(new StoredValue(snapshotPointer.Value).ToEntries(), index);
                if (!nextIndex.HasValue)
                    return nextIndex;
                index = nextIndex.Value;
            }
            return await _stream.AppendRangeAsync(values.ToEntries(), index);
        }

        bool CanSnapshot(Index index)
            => _snapshotter != null && _snapshotter.IsSnapshotIndex(index);

        public IAsyncEnumerable<(Index, StoredValue)> GetAllIndexValuesAsync()
            => ReadForwardFromAsync(Index.Zero);

        public IAsyncEnumerable<StoredValue> GetAllValuesAsync()
            => _stream.GetEntries().Select((c) => c.ToStoredValue()).ToAsyncEnumerable();

        public Task<Result<StoredValue>> GetAtIndexAsync(Index index)
        {
            var result = _stream.GetEntriesRange(index, index);
            if (result.HasValue)
                return Task.FromResult(Result.OK(result.Value.First().Item2.ToStoredValue()));
            else return Task.FromResult(result.CastError<List<(Index, Entry)>, StoredValue>());
        }

        public IAsyncEnumerable<(Index, StoredValue)> GetRangeAsync(Index from, Index to)
            => Map(_stream.GetEntriesRange(from, to));

        public IAsyncEnumerable<(Index, StoredValue)> ReadBackwardsFromAsync(Index from)
            => Map(_stream.GetEntriesRange(from, 0UL.AsIndex()));

        public IAsyncEnumerable<(Index, StoredValue)> ReadForwardFromAsync(Index from)
            => Map(_stream.GetEntriesRange(from, _stream.GetExpectedEntriesIndex()));

        IAsyncEnumerable<(Index, StoredValue)> Map(Result<List<(Index, Entry)>> result)
        {
            if (result.HasValue)
                return Map(result.Value);
            else return new List<(Index, StoredValue)>().ToAsyncEnumerable();
        }

        IAsyncEnumerable<(Index, StoredValue)> Map(IEnumerable<(Index, Entry)> entries)
            => entries.Select(c => (c.Item1, c.Item2.ToStoredValue())).ToAsyncEnumerable();

        public Task<Result<SnapshotReading>> GetSnapshotReading()
            => _snapshotter.GetSnapshotReading(_stream);

        #endregion StreamAD
    }
}
