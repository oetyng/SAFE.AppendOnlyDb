using SAFE.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    public interface IStreamDb
    {
        Task<Result<Pointer>> AppendAsync(string streamKey, object data);
        IAsyncEnumerable<T> GetStreamAsync<T>(string streamKey);
        Task<Result<T>> GetAtVersionAsync<T>(string streamKey, ulong version);
    }
}