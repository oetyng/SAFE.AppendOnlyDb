using SAFE.AppendOnlyDb.Network;
using SAFE.Data;
using System;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Factories
{
    internal class DataTreeFactory
    {
        IMdNodeFactory _nodeFactory;

        public DataTreeFactory(IMdNodeFactory nodeFactory)
            => _nodeFactory = nodeFactory;

        public async Task<Result<DataTree>> LocateAsync(MdLocator locator, Func<MdLocator, Task> onHeadAddressChange)
        {
            var head = await _nodeFactory.LocateAsync(locator).ConfigureAwait(false);
            if (!head.HasValue)
                return head.CastError<IMdNode, DataTree>();
            var dataTree = new DataTree(head.Value, onHeadAddressChange);
            return Result.OK(dataTree);
        }

        public async Task<DataTree> CreateAsync(Func<MdLocator, Task> onHeadAddressChange)
        {
            var head = await _nodeFactory.CreateNewMdNodeAsync(metadata: null).ConfigureAwait(false);
            var dataTree = new DataTree(head, onHeadAddressChange);
            return dataTree;
        }
    }
}