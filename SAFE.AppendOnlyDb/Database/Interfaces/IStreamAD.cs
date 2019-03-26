using SAFE.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    public interface IStreamAD : IData
    {
        Task<Result<Pointer>> AppendAsync(StoredValue value);
        Task<Result<Pointer>> TryAppendAsync(StoredValue value, ExpectedVersion expectedVersion);

        // Todo: AppendRange / TryAppendRange

        Task<Result<StoredValue>> GetAtVersionAsync(ulong version);

        /// <summary>
        /// Reads the latest snapshot - if any - and all events since.
        /// </summary>
        /// <returns><see cref="SnapshotReading"/></returns>
        Task<Result<Snapshots.SnapshotReading>> ReadFromSnapshot();

        IOrderedAsyncEnumerable<(ulong, StoredValue)> ReadForwardFromAsync(ulong from);
        IOrderedAsyncEnumerable<(ulong, StoredValue)> ReadBackwardsFromAsync(ulong from);
        IAsyncEnumerable<(ulong, StoredValue)> GetRangeAsync(ulong from, ulong to);

        IAsyncEnumerable<StoredValue> GetAllValuesAsync();
        IAsyncEnumerable<(Pointer, StoredValue)> GetAllPointerValuesAsync();
    }
}