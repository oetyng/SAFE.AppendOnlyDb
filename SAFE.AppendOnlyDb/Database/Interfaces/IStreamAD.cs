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

        Task<IOrderedEnumerable<(ulong, StoredValue)>> ReadForwardFromAsync(ulong from);
        Task<IOrderedEnumerable<(ulong, StoredValue)>> ReadBackwardsFromAsync(ulong from);
        Task<IEnumerable<(ulong, StoredValue)>> GetRangeAsync(ulong from, ulong to);

        Task<IEnumerable<StoredValue>> GetAllValuesAsync();
        Task<IEnumerable<(Pointer, StoredValue)>> GetAllPointerValuesAsync();
    }
}