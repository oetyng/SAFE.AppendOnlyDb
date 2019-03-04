using System;
using System.Threading.Tasks;
using SAFE.AppendOnlyDb.Factories;
using SAFE.Data.Client;
using SAFE.Data.Client.Auth;

namespace SAFE.AppendOnlyDb.Tests
{
    public class TestBase
    {
        readonly string _appId = "testapp";
        IStorageClient _client;

        protected async Task InitClient(bool inMem = true, bool mock = true)
        {
            SAFEClient.SetFactory(async (sess, app, db) => (object)await StreamDbFactory.CreateForApp(sess, app, db));

            var clientFactory = new ClientFactory(GetAppInfo(), (session, appId) => new SAFEClient(session, appId));

            if (mock)
                _client = await clientFactory.GetMockNetworkClient(Credentials.Random, inMem);
            else // live network
                throw new NotImplementedException("Live network not yet implemented.");

            //Func<SafeApp.Session, string, string, Task<T>> factory
        }

        AppInfo GetAppInfo()
            => new AppInfo
            {
                Id = _appId,
                Name = "testapp",
                Scope = string.Empty,
                Vendor = "test"
            };

        protected Authentication GetAuth() 
            => new Authentication(GetAppInfo());

        protected Task<IStreamDb> GetDatabase(string dbName) 
            => _client.GetOrAddDbAsync<IStreamDb>(dbName);
    }
}