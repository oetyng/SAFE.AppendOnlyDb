using SAFE.AppendOnlyDb;
using SAFE.AppendOnlyDb.Network;
using System;
using System.Threading.Tasks;

namespace SAFE.Data.Client
{
    public class SAFEClient : IStorageClient
    {
        readonly SafeApp.Session _session;
        readonly string _appId;

        static Func<SafeApp.Session, string, string, Task<object>> _factory;

        public SAFEClient(SafeApp.Session session, string appId)
        {
            _session = session;
            _appId = appId;
        }

        public static void SetFactory(Func<SafeApp.Session, string, string, Task<object>> factory)
            => _factory = factory;

        public async Task<T> GetOrAddDbAsync<T>(string dbId)
            => ((Result<T>)await _factory(_session, _appId, dbId)).Value;

        public IImDStore GetImDStore()
            => new ImDStore(new NetworkDataOps(_session, null));

        public IImDStore GetImDStore(DbEncryption dbEncryption)
            => new ImDStore(new NetworkDataOps(_session, dbEncryption));
    }
}