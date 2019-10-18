using SAFE.AppendOnlyDb.Network.AD;
using SAFE.Data;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    public interface IStreamDb_v2
    {
        Task<Result<IStreamAD_v2>> GetOrAddStreamAsync(string streamKey);
        Task<Result<bool>> AddStreamAsync(string streamKey);
        Task<Result<IStreamAD_v2>> GetStreamAsync(string streamKey);
    }
}