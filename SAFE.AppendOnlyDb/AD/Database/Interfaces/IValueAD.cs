using SAFE.Data;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Network.AD
{
    internal interface IValueAD_v2
    {
        Task<Result<StoredValue>> GetValueAsync();
        Task<Result<Index>> SetAsync(StoredValue value);
        Task<Result<Index>> TrySetAsync(StoredValue value, ExpectedVersion expectedVersion);
    }
}