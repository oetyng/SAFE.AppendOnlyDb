using System;
using System.Threading.Tasks;
using SAFE.AppendOnlyDb.Network;
using SAFE.Data;

namespace SAFE.AppendOnlyDb.Factories
{
    public class StreamDbFactory
    {
        readonly INetworkDataOps _dataOps;
        readonly StreamCollectionFactory _streamCollectionFactory;
        internal IMdNodeFactory NodeFactory { get; }

        public StreamDbFactory(INetworkDataOps dataOps, Snapshots.Snapshotter snapshotter)
        {
            _dataOps = dataOps;
            NodeFactory = new MdNodeFactory(dataOps, snapshotter);
            _streamCollectionFactory = new StreamCollectionFactory(NodeFactory, new DataTreeFactory(NodeFactory));
        }

        public async Task<Result<IStreamDb>> CreateForApp(string appId, string dbId)
        {
            var manager = new MdHeadManager(_dataOps, NodeFactory, appId, DataProtocol.DEFAULT_AD_PROTOCOL);
            await manager.InitializeManager();
            var streamDbHead = await manager.GetOrAddHeadAsync(dbId);
            var dbResult = await GetOrAddAsync(streamDbHead);
            return dbResult;
        }

        async Task<Result<IStreamDb>> GetOrAddAsync(MdHead streamDbHead)
        {
            var streamDbRoot = new DataTree(streamDbHead.Md, (s) => throw new ArgumentOutOfRangeException("Can only add 999 items to this collection."));
            var streamCollection = await _streamCollectionFactory.GetOrAddStreamCollectionAsync(streamDbRoot);

            var db = new StreamDb(streamCollection, NodeFactory);

            return Result.OK((IStreamDb)db);
        }
    }

    class StreamCollectionFactory
    {
        readonly IMdNodeFactory _nodeFactory;
        readonly DataTreeFactory _dataTreeFactory;

        public StreamCollectionFactory(IMdNodeFactory nodeFactory, DataTreeFactory dataTreeFactory)
        { 
            _nodeFactory = nodeFactory;
            _dataTreeFactory = dataTreeFactory;
        }

        public async Task<IStreamCollection> GetOrAddStreamCollectionAsync(IValueAD streamDbRoot)
        {
            IMdNode typeStoreHead;
            var typeStoreResult = await streamDbRoot.GetValueAsync().ConfigureAwait(false);
            if (!typeStoreResult.HasValue)
            {
                typeStoreHead = await _nodeFactory.CreateNewMdNodeAsync(null)
                    .ConfigureAwait(false);
                await streamDbRoot.SetAsync(new StoredValue(typeStoreHead.MdLocator))
                    .ConfigureAwait(false);
            }
            else
            {
                var typeStoreHeadLocation = typeStoreResult.Value.Parse<MdLocator>();
                typeStoreHead = (await _nodeFactory.LocateAsync(typeStoreHeadLocation)
                    .ConfigureAwait(false)).Value;
            }

            Task OnHeadChange(MdLocator newLocation) => streamDbRoot.SetAsync(new StoredValue(newLocation));

            var streamsRoot = new DataTree(typeStoreHead, OnHeadChange);

            return new StreamCollection(streamsRoot, _dataTreeFactory);
        }
    }
}