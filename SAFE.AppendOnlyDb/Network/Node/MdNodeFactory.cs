using System.Threading.Tasks;
using SAFE.Data;

namespace SAFE.AppendOnlyDb.Network
{
    internal sealed class MdNodeFactory : IMdNodeFactory
    {
        readonly INetworkDataOps _networkOps;
        readonly Snapshots.Snapshotter _snapshotter;

        public MdNodeFactory(INetworkDataOps networkOps, Snapshots.Snapshotter snapshotter)
        {
            _networkOps = networkOps;
            _snapshotter = snapshotter;
        }

        public async Task<Result<IMdNode>> LocateAsync(MdLocator location)
        {
            var mdResult = await _networkOps.LocatePublicMd(location.XORName, location.TypeTag)
                .ConfigureAwait(false);
            if (!mdResult.HasValue)
                return new KeyNotFound<IMdNode>($"Could not locate md: {location.TypeTag}, {location.XORName}");

            var mdInfo = mdResult.Value;
            var dataOps = new MdDataOps(this, _networkOps, mdInfo);
            var md = new MdNode(dataOps, _snapshotter);
            await md.Initialize(metadata: null).ConfigureAwait(false);
            return Result.OK((IMdNode)md);
        }

        public async Task<IMdNode> CreateNewMdNodeAsync(MdMetadata metadata)
        {
            var mdInfo = await _networkOps.CreateEmptyMd(DataProtocol.DEFAULT_AD_PROTOCOL).ConfigureAwait(false);
            var dataOps = new MdDataOps(this, _networkOps, mdInfo);
            var newMd = new MdNode(dataOps, _snapshotter);
            await newMd.Initialize(metadata).ConfigureAwait(false);
            return newMd;
        }
    }
}