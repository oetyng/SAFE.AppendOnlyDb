using SAFE.AppendOnlyDb;
using SAFE.AppendOnlyDb.Factories;
using SAFE.AppendOnlyDb.Network;
using System.Threading.Tasks;

namespace SAFE.Data.Client
{
    public class SAFEClient
    {
        readonly string _appId;
        readonly INetworkDataOps _networkDataOps;
        readonly StreamDbFactory _dbFactory;

        public SAFEClient(string appId, INetworkDataOps networkDataOps, StreamDbFactory dbFactory)
        {
            _appId = appId;
            _networkDataOps = networkDataOps;
            _dbFactory = dbFactory;
        }

        public Task<Result<IStreamDb>> GetOrAddDbAsync(string dbId, MdHeadPermissionSettings permissionSettings = null)
            => _dbFactory.CreateForApp(_appId, dbId, permissionSettings);

        public IImDStore GetImDStore()
            => new ImDStore(_networkDataOps);
    }
}