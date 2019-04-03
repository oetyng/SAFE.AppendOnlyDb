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

        public async Task<Result<IStreamDb>> CreateForApp(string appId, string dbId, MdHeadPermissionSettings permissionSettings = null)
        {
            var manager = new MdHeadManager(_dataOps, NodeFactory, appId, DataProtocol.DEFAULT_AD_PROTOCOL, permissionSettings);
            await manager.InitializeManager();
            var streamDbHead = await manager.GetOrAddHeadAsync(dbId);
            var dbResult = await GetOrAddAsync(streamDbHead);
            return dbResult;
        }

        async Task<Result<IStreamDb>> GetOrAddAsync(MdHead streamDbHead)
        {
            var streamDbRoot = new DataTree(streamDbHead.Md, (s) => throw new ArgumentOutOfRangeException("Can only add 999 items to this collection."));
            var streamCollection = await _streamCollectionFactory.GetOrAddDataCollectionAsync(streamDbRoot);

            var db = new StreamDb(streamCollection, NodeFactory);

            return Result.OK((IStreamDb)db);
        }
    }
}