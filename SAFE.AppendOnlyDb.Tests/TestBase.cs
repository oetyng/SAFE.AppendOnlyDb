using System;
using System.Threading.Tasks;
using SAFE.AppendOnlyDb.Factories;
using SAFE.AppendOnlyDb.Network;
using SAFE.AppendOnlyDb.Snapshots;
using SAFE.MockAuthClient;
using SAFE.Data.Client;

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
        IMdNodeFactory _nodeFactory;
        DataTreeFactory _dataTreeFactory;
        StreamDbFactory _dbFactory;

        internal async Task InitSession(Func<IImDStore, Snapshotter> snapShotterFactory, bool inMem = true, bool mock = true)
        {
            if (!mock) throw new InvalidOperationException("Not testing against live networks.");

            var mockClient = new CredentialAuth(_appId, inMem);
            var session = (await mockClient.AuthenticateAsync()).Value;
            _networkDataOps = new NetworkDataOps(session);

            var snapshotter = snapShotterFactory == null ? null : snapShotterFactory(GetImdStore());
            _dbFactory = new StreamDbFactory(_networkDataOps, snapshotter);
            _nodeFactory = _dbFactory.NodeFactory;
            _dataTreeFactory = new DataTreeFactory(_nodeFactory);
        }

        internal Task<IMdNode> CreateNodeAsync()
            => _nodeFactory.CreateNewMdNodeAsync(null);

        internal async Task<MutableCollection<T>> CreateCollection<T>()
            => new MutableCollection<T>(await GetValueADAsync(), _dataTreeFactory);

        internal async Task<IValueAD> GetValueADAsync(MdHeadPermissionSettings permissionSettings = null)
        {
            var db = await GetDatabase("theDb", permissionSettings);
            var mdHead = await CreateNodeAsync();
            return new DataTree(mdHead, (s) => throw new ArgumentOutOfRangeException("Can only add 999k items to this collection."));
        }

        internal async Task<IStreamAD> GetStreamADAsync(string streamKey = "theStream", MdHeadPermissionSettings permissionSettings = null)
        {
            var db = await GetDatabase("theDb", permissionSettings);
            await db.AddStreamAsync(streamKey);
            return (await db.GetStreamAsync(streamKey)).Value;
        }

        internal async Task<IStreamDb> GetDatabase(string dbName, MdHeadPermissionSettings permissionSettings = null)
        {
            var res = await _dbFactory.CreateForApp(_appId, dbName, permissionSettings);
            return res.Value;
        }

        internal IImDStore GetImdStore()
            => new ImDStore(_networkDataOps);
    }
}