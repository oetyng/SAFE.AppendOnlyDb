using SAFE.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    public interface IStreamDb
    {
        Task<Result<Pointer>> AppendAsync(string streamKey, object data);
        Task<IEnumerable<T>> GetStream<T>(string streamKey);
        Task<Result<T>> GetVersionAsync<T>(string streamKey, ulong version);
    }
}