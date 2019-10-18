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

        internal static List<Entry> ToEntries(this StoredValue value)
            => new[] { value.ToEntry() }.ToList();

        internal static StoredValue ToStoredValue(this Entry entry)
            => entry.Value.Parse<StoredValue>();

        internal static Index AsIndex(this ulong index)
            => new Index { Value = index };
    }

    internal class ValueStream : IStreamAD_v2, IValueAD_v2
    {
        readonly ISeqAppendOnly _stream;

        public Address Address => _stream.GetAddress();

        public ValueStream(ISeqAppendOnly stream)
            => _stream = stream;

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

        public Task<Result<Index>> TryAppendAsync(StoredValue value, ExpectedVersion expectedVersion)
        {
            return expectedVersion switch
            {
                NoVersion _ => _stream.AppendAsync<Index>(value.ToEntries(), Index.Zero),
                SpecificVersion some => _stream.AppendAsync<Index>(value.ToEntries(), some.Value.Value.AsIndex()),
                AnyVersion _ => _stream.AppendAsync<Index>(value.ToEntries(), _stream.GetNextEntriesIndex()),
                _ => Task.FromResult((Result<Index>)new ArgumentOutOfRange<Index>()),
            };
        }

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

        public Task<Result<SnapshotReading>> ReadFromSnapshot()
            => throw new NotImplementedException();

        #endregion StreamAD
    }
}
