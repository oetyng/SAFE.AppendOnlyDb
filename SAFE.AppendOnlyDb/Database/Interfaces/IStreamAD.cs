using SAFE.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    internal interface IStreamAD : IData
    {
        Task<Result<Pointer>> AppendAsync(StoredValue value);
        Task<Result<Pointer>> TryAppendAsync(StoredValue value, ulong expectedVersion);

        Task<Result<StoredValue>> GetVersionAsync(ulong version);

        IOrderedAsyncEnumerable<(ulong, StoredValue)> ReadForwardFromAsync(ulong from);
        IOrderedAsyncEnumerable<(ulong, StoredValue)> ReadBackwardsFromAsync(ulong from);
        IAsyncEnumerable<(ulong, StoredValue)> GetRangeAsync(ulong from, ulong to);

        IAsyncEnumerable<StoredValue> GetAllValuesAsync();
        IAsyncEnumerable<(Pointer, StoredValue)> GetAllPointerValuesAsync();
    }
}