using SAFE.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Network.AD
{
    public interface IStreamAD_v2
    {
        Task<Result<Index>> AppendAsync(StoredValue value);
        Task<Result<Index>> TryAppendAsync(StoredValue value, ExpectedVersion expectedVersion);

        // Todo: AppendRange / TryAppendRange

        Task<Result<StoredValue>> GetAtIndexAsync(Index version);

        /// <summary>
        /// Reads the latest snapshot - if any - and all events since.
        /// </summary>
        /// <returns><see cref="SnapshotReading"/></returns>
        Task<Result<Snapshots.SnapshotReading>> ReadFromSnapshot();

        IAsyncEnumerable<(Index, StoredValue)> ReadForwardFromAsync(Index from);
        IAsyncEnumerable<(Index, StoredValue)> ReadBackwardsFromAsync(Index from);
        IAsyncEnumerable<(Index, StoredValue)> GetRangeAsync(Index from, Index to);

        IAsyncEnumerable<StoredValue> GetAllValuesAsync();
        IAsyncEnumerable<(Index, StoredValue)> GetAllIndexValuesAsync();
    }
}