using System;
using System.Threading.Tasks;
using SAFE.AppendOnlyDb.Factories;
using SAFE.AppendOnlyDb.Snapshots;
using SAFE.Data.Client;
using SAFE.Data.Client.Auth;

namespace SAFE.AppendOnlyDb.Tests
{
    public class TestBase
    {
        internal NetworkFixture _fixture;

        protected Task Init(bool inMem = true, bool mock = true)
        {
            _fixture = new NetworkFixture();
            return _fixture.InitClient(inMem, mock);
        }

        protected void SetSnapshotter(Snapshotter snapshotter)
            => _fixture.SetSnapshotter(snapshotter);
    }

    internal class NetworkFixture
    {
        readonly string _appId = "testapp";
        Snapshotter _snapshotter;
        Network.IMdNodeFactory _nodeFactory;
        DataTreeFactory _dataTreeFactory;
        IStorageClient _client;

        internal async Task InitClient(bool inMem = true, bool mock = true)
        {
            SAFEClient.SetFactory(async (sess, app, db) => await CreateForApp(sess, app, db));

            var clientFactory = new ClientFactory(GetAppInfo(), (session, appId) => new SAFEClient(session, appId));

            if (mock)
                _client = await clientFactory.GetMockNetworkClient(Credentials.Random, inMem);
            else // live network
                throw new NotImplementedException("Live network not yet implemented.");
        }

        internal void SetSnapshotter(Snapshotter snapshotter)
            => _snapshotter = snapshotter;

        async Task<Data.Result<IStreamDb>> CreateForApp(SafeApp.Session session, string appId, string dbId)
        {
            var factory = new StreamDbFactory(new Network.NetworkDataOps(session), _snapshotter);
            _nodeFactory = factory.NodeFactory;
            _dataTreeFactory = new DataTreeFactory(factory.NodeFactory);
            return await factory.CreateForApp(appId, dbId);
        }

        internal Task<IMdNode> CreateNodeAsync()
            => _nodeFactory.CreateNewMdNodeAsync(null);

        internal async Task<MutableCollection<T>> CreateCollection<T>()
            => new MutableCollection<T>(await GetValueADAsync(), _dataTreeFactory);

        internal async Task<IValueAD> GetValueADAsync()
        {
            var db = await GetDatabase("theDb");
            var mdHead = await CreateNodeAsync();
            return new DataTree(mdHead, (s) => throw new ArgumentOutOfRangeException("Can only add 1k items to this collection."));
        }

        internal async Task<IStreamAD> GetStreamADAsync(string streamKey = "theStream")
        {
            var db = await GetDatabase("theDb");
            await db.AddStreamAsync(streamKey);
            return (await db.GetStreamAsync(streamKey)).Value;
        }

        AppInfo GetAppInfo()
            => new AppInfo
            {
                Id = _appId,
                Name = "testapp",
                Scope = string.Empty,
                Vendor = "test"
            };

        internal Authentication GetAuth()
            => new Authentication(GetAppInfo());

        internal Task<IStreamDb> GetDatabase(string dbName)
            => _client.GetOrAddDbAsync<IStreamDb>(dbName);

        internal IImDStore GetImdStore()
            => _client.GetImDStore();
    }
}