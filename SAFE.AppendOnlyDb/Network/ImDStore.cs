using SAFE.AppendOnlyDb.Utils;
using SAFE.Data.Client;
using SafeApp.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Network
{
    public class ImDStore : IImDStore
    {
        readonly INetworkDataOps _networkOps;

        public ImDStore(INetworkDataOps networkOps)
            => _networkOps = networkOps;

        public async Task<byte[]> StoreImDAsync(byte[] payload)
        {
            if (_networkOps.DbEncryption == null)
                return await _StoreImDAsync(payload).ConfigureAwait(false);

            var session = _networkOps.Session;
            using (var publicKey = await session.Crypto.EncPubKeyNewAsync(_networkOps.DbEncryption.PublicKey))
            {
                var plainBytes = payload.ToList();
                var encryptedData = await session.Crypto.EncryptSealedBoxAsync(plainBytes, publicKey);
                var dataMap = await StoreImDAsync(encryptedData.ToArray());
                var encryptedDataMap = await session.Crypto.EncryptSealedBoxAsync(dataMap.ToList(), publicKey);
                return encryptedDataMap.ToArray();
            }
        }

        public async Task<byte[]> GetImDAsync(byte[] map)
        {
            if (_networkOps.DbEncryption == null)
                return await _GetImDAsync(map).ConfigureAwait(false);

            var session = _networkOps.Session;
            using (var publicKey = await session.Crypto.EncPubKeyNewAsync(_networkOps.DbEncryption.PublicKey))
            using (var secretKey = await session.Crypto.EncSecretKeyNewAsync(_networkOps.DbEncryption.SecretKey))
            {
                var dataMap = await session.Crypto.DecryptSealedBoxAsync(map.ToList(), publicKey, secretKey);
                var encryptedData = await GetImDAsync(dataMap.ToArray());
                var plainBytes = await session.Crypto.DecryptSealedBoxAsync(encryptedData.ToList(), publicKey, secretKey);
                return plainBytes.ToArray();
            }
        }


        // While map size is too large for an MD entry
        // store the map as ImD.
        async Task<byte[]> _StoreImDAsync(byte[] payload)
        {
            if (payload.Length == 0)
                throw new ArgumentException("Payload cannot be empty.");

            var map = await _networkOps.StoreImmutableData(payload.Compress());
            if (map.Length < 1000)
                return map;
            return await StoreImDAsync(map);
        }

        // While not throwing,
        // the payload is a datamap.
        async Task<byte[]> _GetImDAsync(byte[] map)
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