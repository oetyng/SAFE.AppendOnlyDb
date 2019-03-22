using System;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Factories
{
    internal class DataTreeFactory
    {
        public static async Task<DataTree> CreateAsync(Func<MdLocator, Task> onHeadAddressChange)
        {
            var head = await MdAccess.CreateAsync(metadata: null);
            var dataTree = new DataTree(head, onHeadAddressChange);
            return dataTree;
        }
    }
}