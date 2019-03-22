using SAFE.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    internal interface IStreamStore
    {
        Task<Result<Pointer>> AddAsync(string type, MdLocator location);
        IAsyncEnumerable<(string, MdLocator)> GetAllAsync();
        Task<Result<Pointer>> UpdateAsync(string type, MdLocator location);
    }
}