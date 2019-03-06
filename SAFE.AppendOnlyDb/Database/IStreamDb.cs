using SAFE.Data;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    public interface IStreamDb
    {
        Task<Result<bool>> AddStreamAsync(string streamKey);
        Task<Result<IStreamAD>> GetStreamAsync(string streamKey);
    }
}