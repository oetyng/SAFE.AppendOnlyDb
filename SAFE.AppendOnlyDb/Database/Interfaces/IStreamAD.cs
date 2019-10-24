using SAFE.AppendOnlyDb.Snapshots;
using SAFE.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Network
{
    public interface IStreamAD
    {
        Task<Result<Index>> AppendAsync(StoredValue value);
        Task<Result<Index>> TryAppendAsync(StoredValue value, ExpectedIndex expectedIndex);

        Task<Result<Index>> AppendRangeAsync(List<StoredValue> value);
        Task<Result<Index>> TryAppendRangeAsync(List<StoredValue> value, ExpectedIndex expectedIndex);

        Task<Result<StoredValue>> GetAtIndexAsync(Index index);

        /// <summary>
        /// Reads the latest snapshot - if any - and all events since.
        /// </summary>
        /// <returns><see cref="SnapshotReading"/></returns>
        Task<Result<SnapshotReading>> GetSnapshotReading();

        IAsyncEnumerable<(Index, StoredValue)> ReadForwardFromAsync(Index from);
        IAsyncEnumerable<(Index, StoredValue)> ReadBackwardsFromAsync(Index from);
        IAsyncEnumerable<(Index, StoredValue)> GetRangeAsync(Index from, Index to);

        IAsyncEnumerable<StoredValue> GetAllValuesAsync();
        IAsyncEnumerable<(Index, StoredValue)> GetAllIndexValuesAsync();
    }
}