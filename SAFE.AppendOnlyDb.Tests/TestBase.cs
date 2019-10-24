using System;
using System.Threading.Tasks;
using SAFE.AppendOnlyDb.Network;
using SAFE.AppendOnlyDb.Snapshots;
using SAFE.Data.Client;
using SAFE.AppendOnlyDb.AD.Database;
using SAFE.AppendOnlyDb.Network.InMem;

namespace SAFE.AppendOnlyDb.Tests
{
    public class TestBase
    {
        internal NetworkFixture _fixture;

        protected Task Init(Func<IImDStore, Snapshotter> snapShotterFactory = null, bool inMem = true, bool mock = true)
        {
            _fixture = new NetworkFixture();
            return _fixture.InitSession(snapShotterFactory, inMem, mock);
        }
    }

    internal class NetworkFixture
    {
        readonly string _appId = "testapp";
        INetworkDataOps _networkDataOps;
        StreamDbFactory _dbFactory;

        internal Task InitSession(Func<IImDStore, Snapshotter> snapShotterFactory, bool inMem = true, bool mock = true)
        {
            if (!mock) throw new InvalidOperationException("Not testing against live networks.");

            // var mockClient = new CredentialAuth(_appId, inMem);
            // var session = (await mockClient.AuthenticateAsync()).Value;
            _networkDataOps = new InMemNetworkDataOps();
            var snapshotter = snapShotterFactory == null ? null : snapShotterFactory(GetImdStore());
            _dbFactory = new StreamDbFactory(_networkDataOps, snapshotter);
            return Task.FromResult(0);
        }

        internal async Task<IValueAD> GetValueADAsync()
        {
            var streamAd = await GetStreamADAsync();
            return (IValueAD)streamAd;
        }

        internal async Task<IStreamAD> GetStreamADAsync(string streamKey = "theStream")
        {
            var db = await GetDatabase("theDb");
            await db.AddStreamAsync(streamKey);
            return (await db.GetStreamAsync(streamKey)).Value;
        }

        internal async Task<IStreamDb> GetDatabase(string dbName)
        {
            var res = await _dbFactory.CreateForApp(_appId, dbName);
            return res.Value;
        }

        internal IImDStore GetImdStore()
            => new InMemImDStore(_networkDataOps);
    }
}