using System;
using System.Threading.Tasks;
using SAFE.AppendOnlyDb.Network;
using SAFE.AppendOnlyDb.Snapshots;
using SAFE.Data.Client;
using SAFE.AppendOnlyDb.AD.Database;
using SAFE.AppendOnlyDb.Network.AD;

namespace SAFE.AppendOnlyDb.Tests
{
    public class TestBase_v2
    {
        internal NetworkFixture_v2 _fixture;

        protected Task Init(Func<IImDStore, Snapshotter> snapShotterFactory = null, bool inMem = true, bool mock = true)
        {
            _fixture = new NetworkFixture_v2();
            return _fixture.InitSession(snapShotterFactory, inMem, mock);
        }
    }

    internal class NetworkFixture_v2
    {
        readonly string _appId = "testapp";
        // INetworkDataOps _networkDataOps;
        StreamDbFactory_v2 _dbFactory;

        internal Task InitSession(Func<IImDStore, Snapshotter> snapShotterFactory, bool inMem = true, bool mock = true)
        {
            if (!mock) throw new InvalidOperationException("Not testing against live networks.");

            // var mockClient = new CredentialAuth(_appId, inMem);
            // var session = (await mockClient.AuthenticateAsync()).Value;
            // _networkDataOps = new NetworkDataOps(session);
            // var snapshotter = snapShotterFactory == null ? null : snapShotterFactory(GetImdStore());
            _dbFactory = new StreamDbFactory_v2(); // StreamDbFactory_v2(_networkDataOps, snapshotter)
            return Task.FromResult(0);
        }

        //internal async Task<MutableCollection<T>> CreateCollection<T>()
        //    => new MutableCollection<T>(await GetValueADAsync(), _dataTreeFactory);

        internal async Task<IValueAD_v2> GetValueADAsync()
        {
            var streamAd = await GetStreamADAsync();
            return (IValueAD_v2)streamAd;
        }

        internal async Task<IStreamAD_v2> GetStreamADAsync(string streamKey = "theStream")
        {
            var db = await GetDatabase("theDb");
            await db.AddStreamAsync(streamKey);
            return (await db.GetStreamAsync(streamKey)).Value;
        }

        internal async Task<IStreamDb_v2> GetDatabase(string dbName)
        {
            var res = await _dbFactory.CreateForApp(_appId, dbName);
            return res.Value;
        }

        //internal IImDStore GetImdStore()
        //    => new ImDStore(_networkDataOps);
    }
}