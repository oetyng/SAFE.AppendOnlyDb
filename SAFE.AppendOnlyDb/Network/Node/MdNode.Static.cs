using System.Threading.Tasks;
using SAFE.Data;
using SafeApp;

namespace SAFE.AppendOnlyDb.Network
{
    internal sealed partial class MdNode
    {
        public static async Task<Result<IMdNode>> LocateAsync(MdLocator location, Session session)
        {
            var networkDataOps = new NetworkDataOps(session);

            // var mdResult = await networkDataOps.LocatePublicMd(location.XORName, location.TypeTag);
            var mdResult = await networkDataOps.LocatePrivateMd(location.XORName, location.TypeTag, location.SecEncKey, location.Nonce)
                .ConfigureAwait(false);
            if (!mdResult.HasValue)
                return new KeyNotFound<IMdNode>($"Could not locate md: {location.TypeTag}, {location.XORName}");

            var mdInfo = mdResult.Value;
            var md = new MdNode(mdInfo, networkDataOps.Session);
            await md.GetOrAddMetadata().ConfigureAwait(false);
            return Result.OK((IMdNode)md);
        }

        public static async Task<IMdNode> CreateNewMdNodeAsync(MdMetadata metadata, Session session, ulong protocol)
        {
            var networkDataOps = new NetworkDataOps(session);
            var mdInfo = await networkDataOps.CreateEmptyMd(protocol).ConfigureAwait(false);
            var newMd = new MdNode(mdInfo, session);
            await newMd.Initialize(metadata).ConfigureAwait(false);
            return newMd;
        }
    }
}