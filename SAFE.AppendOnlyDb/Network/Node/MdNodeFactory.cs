using System.Threading.Tasks;
using SAFE.Data;
using SafeApp;

namespace SAFE.AppendOnlyDb.Network
{
    internal sealed class MdNodeFactory
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
            var dataOps = new MdDataOps(networkDataOps.Session, mdInfo);
            var md = new MdNode(dataOps);
            await md.Initialize(metadata: null).ConfigureAwait(false);
            return Result.OK((IMdNode)md);
        }

        public static async Task<IMdNode> CreateNewMdNodeAsync(MdMetadata metadata, Session session, ulong protocol)
        {
            var networkDataOps = new NetworkDataOps(session);
            var mdInfo = await networkDataOps.CreateEmptyMd(protocol).ConfigureAwait(false);
            var dataOps = new MdDataOps(session, mdInfo);
            var newMd = new MdNode(dataOps);
            await newMd.Initialize(metadata).ConfigureAwait(false);
            return newMd;
        }
    }
}