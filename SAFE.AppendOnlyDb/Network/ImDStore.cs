using SAFE.AppendOnlyDb.Utils;
using SAFE.Data.Client;
using SafeApp.Utilities;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Network
{
    public class ImDStore : IImDStore
    {
        INetworkDataOps _networkOps;

        public ImDStore(INetworkDataOps networkOps)
        {
            _networkOps = networkOps;
        }

        // While map size is too large for an MD entry
        // store the map as ImD.
        public async Task<byte[]> StoreImDAsync(byte[] payload)
        {
            if (payload.Length < 1000)
                throw new System.ArgumentException("Payload must be at least 1k size.");

            var map = await _networkOps.StoreImmutableData(payload.Compress());
            if (map.Length < 1000)
                return map;
            return await StoreImDAsync(map);
        }

        // While not throwing,
        // the payload is a datamap.
        // NB: Obviously this is not resistant to other errors,
        // so we must catch the specific exception here. (todo)
        public async Task<byte[]> GetImDAsync(byte[] map)
        {
            try
            {
                var payload = await _networkOps.GetImmutableData(map);
                return await GetImDAsync(payload.Decompress());
            }
            catch (FfiException ex) when (ex.ErrorCode == -103) { return map; }
        }
    }
}