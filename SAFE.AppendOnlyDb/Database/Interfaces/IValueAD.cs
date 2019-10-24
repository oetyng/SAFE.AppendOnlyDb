using SAFE.Data;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Network
{
    internal interface IValueAD
    {
        Task<Result<StoredValue>> GetValueAsync();
        Task<Result<Index>> SetAsync(StoredValue value);
        Task<Result<Index>> TrySetAsync(StoredValue value, ExpectedIndex expectedIndex);
    }
}