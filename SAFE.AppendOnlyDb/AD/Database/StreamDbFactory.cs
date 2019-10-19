using System;
using System.Text;
using System.Threading.Tasks;
using SAFE.AppendOnlyDb.Network;
using SAFE.AppendOnlyDb.Snapshots;
using SAFE.Data;

namespace SAFE.AppendOnlyDb.AD.Database
{
    public class StreamDbFactory_v2
    {
        readonly INetworkDataOps_v2 _networkDataOps;
        readonly Snapshotter_v2 _snapshotter;

        public StreamDbFactory_v2(INetworkDataOps_v2 networkDataOps, Snapshotter_v2 snapshotter)
        {
            _networkDataOps = networkDataOps;
            _snapshotter = snapshotter;
        }
        
        public Task<Result<IStreamDb_v2>> CreateForApp(string appId, string dbId)
        {
            var db = new StreamDb_v2(new Network.AD.SeqAppendOnlyDataMock(AddressUtil.GetAddress(dbId)), _snapshotter);
            return Task.FromResult(Result.OK((IStreamDb_v2)db));
        }
    }

    // Mock
    internal class AddressUtil
    {
        public static Network.AD.Address GetAddress(string name)
            => new Network.AD.Address { Name = new Network.AD.XorName { Value = GetHash(name) }, Tag = 0 };

        public static byte[] GetHash(string name)
            => Encoding.UTF8.GetBytes(name);
    }
}