using SAFE.Data;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    internal interface IValueAD : IData
    {
        Task<Result<StoredValue>> GetValueAsync();
        Task<Result<Pointer>> SetAsync(StoredValue value);
        Task<Result<Pointer>> TrySetAsync(StoredValue value, ulong expectedVersion);
    }
}