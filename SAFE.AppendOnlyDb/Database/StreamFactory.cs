using System.Threading.Tasks;
using SAFE.AppendOnlyDb.Network;
using SAFE.Data;

namespace SAFE.AppendOnlyDb.Factories
{
    public class StreamDbFactory
    {
        public static async Task<Result<IStreamDb>> CreateForApp(SafeApp.Session session, string appId, string dbId)
        {
            var manager = new MdHeadManager(session, appId, DataProtocol.DEFAULT_AD_PROTOCOL);
            await manager.InitializeManager();

            MdAccess.SetCreator((meta) => manager.CreateNewMdNode(meta, DataProtocol.DEFAULT_AD_PROTOCOL));
            MdAccess.SetLocator(manager.LocateMdNode);

            var streamMdHead = await manager.GetOrAddHeadAsync(dbId);
            var dbResult = await StreamDb.GetOrAddAsync(streamMdHead);
            return dbResult;
        }
    }
}