using System;
using System.Threading.Tasks;

namespace SAFE.Data.Client
{
    internal class SAFEClient : IStorageClient
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
        {
            return ((Result<T>)await _factory(_session, _appId, dbId)).Value;
        }
    }
}