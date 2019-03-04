using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    internal interface IStreamStore
    {
        Task AddAsync(string type, MdLocator location);
        Task<IEnumerable<(string, MdLocator)>> GetAllAsync();
        Task UpdateAsync(string type, MdLocator location);
    }
}