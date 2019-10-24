using SAFE.Data.Client;
using SAFE.Data.Utils;
using SafeApp.Utilities;
using System;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Network.InMem
{
    public class InMemImDStore : IImDStore
    {
        readonly INetworkDataOps _networkOps;

        public InMemImDStore(INetworkDataOps networkOps)
            => _networkOps = networkOps;

        // While map size is too large for an MD entry
        // store the map as ImD.
        public async Task<byte[]> StoreImDAsync(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (payload.Length == 0) throw new ArgumentException("Payload cannot be empty.");

            var map = await _networkOps.StoreImmutableData(payload.Compress());
            if (map.Length < 1000)
                return map;
            return await StoreImDAsync(map);
        }

        // While not throwing,
        // the payload is a datamap.
        public async Task<byte[]> GetImDAsync(byte[] map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            try
            {
                var payload = await _networkOps.GetImmutableData(map);
                return await GetImDAsync(payload.Decompress());
            }
            catch (FfiException ex) when (ex.ErrorCode == -103) { return map; }
            catch (ImmutableDataNotFound) { return map; }
        }
    }
}