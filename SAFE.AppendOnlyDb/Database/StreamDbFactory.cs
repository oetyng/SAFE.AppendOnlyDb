using System.Text;
using System.Threading.Tasks;
using SAFE.AppendOnlyDb.Network;
using SAFE.AppendOnlyDb.Snapshots;
using SAFE.Data;

namespace SAFE.AppendOnlyDb.AD.Database
{
    public class StreamDbFactory
    {
        readonly INetworkDataOps _networkDataOps;
        readonly Snapshotter _snapshotter;

        public StreamDbFactory(INetworkDataOps networkDataOps, Snapshotter snapshotter)
        {
            _networkDataOps = networkDataOps;
            _snapshotter = snapshotter;
        }
        
        public Task<Result<IStreamDb>> CreateForApp(string appId, string dbId)
        {
            var db = new StreamDb(new SeqAppendOnlyDataMock(AddressUtil.GetAddress(dbId)), _snapshotter);
            return Task.FromResult(Result.OK((IStreamDb)db));
        }
    }

    // Mock
    internal class AddressUtil
    {
        public static Address GetAddress(string name)
            => new Address { Name = new XorName { Value = GetHash(name) }, Tag = 0 };

        public static byte[] GetHash(string name)
            => Encoding.UTF8.GetBytes(name);
    }
}