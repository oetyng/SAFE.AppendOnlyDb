using SAFE.Data.Client;
using SAFE.Data.Utils;
using SafeApp.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Network
{
    public class ImDStore_v2 : IImDStore_v2
    {
        readonly INetworkDataOps_v2 _networkOps;

        public ImDStore_v2(INetworkDataOps_v2 networkOps)
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

    public interface INetworkDataOps_v2
    {
        Task<byte []> GetImmutableData(byte[] map);
        Task<byte[]> StoreImmutableData(byte[] payload);
    }

    public class NetworkDataOps_v2 : INetworkDataOps_v2
    {
        readonly List<(byte[], byte[])> _store = new List<(byte[], byte[])>();

        public Task<byte[]> GetImmutableData(byte[] map)
        {
            var res = _store
                .SingleOrDefault(c => Enumerable.SequenceEqual(c.Item1, map)).Item2;
            if (res == null)
                throw new ImmutableDataNotFound();
            return Task.FromResult(res);
        }

        public Task<byte[]> StoreImmutableData(byte[] payload)
        {
            var map = payload.Reverse().ToArray();
            _store.Add((map, payload));
            return Task.FromResult(map);
        }
    }

    public class ImmutableDataNotFound : Exception { }
}