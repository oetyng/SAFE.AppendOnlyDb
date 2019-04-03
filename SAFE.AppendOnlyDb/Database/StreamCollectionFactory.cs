using SAFE.AppendOnlyDb.Network;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Factories
{
    internal class StreamCollectionFactory
    {
        readonly IMdNodeFactory _nodeFactory;
        readonly DataTreeFactory _dataTreeFactory;

        public StreamCollectionFactory(IMdNodeFactory nodeFactory, DataTreeFactory dataTreeFactory)
        {
            _nodeFactory = nodeFactory;
            _dataTreeFactory = dataTreeFactory;
        }

        public async Task<IStreamCollection> GetOrAddDataCollectionAsync(IValueAD dataDbRoot)
        {
            IMdNode typeStoreHead;
            var typeStoreResult = await dataDbRoot.GetValueAsync().ConfigureAwait(false);
            if (!typeStoreResult.HasValue)
            {
                typeStoreHead = await _nodeFactory.CreateNewMdNodeAsync(null)
                    .ConfigureAwait(false);
                await dataDbRoot.SetAsync(new StoredValue(typeStoreHead.MdLocator))
                    .ConfigureAwait(false);
            }
            else
            {
                var typeStoreHeadLocation = typeStoreResult.Value.Parse<MdLocator>();
                typeStoreHead = (await _nodeFactory.LocateAsync(typeStoreHeadLocation)
                    .ConfigureAwait(false)).Value;
            }

            Task OnHeadChange(MdLocator newLocation) => dataDbRoot.SetAsync(new StoredValue(newLocation));

            var dataRoot = new DataTree(typeStoreHead, OnHeadChange);

            return new StreamCollection(dataRoot, _dataTreeFactory);
        }
    }
}