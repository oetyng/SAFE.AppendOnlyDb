using SafeApp.Misc;
using System;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    public class DbEncryptionFactory
    {
        readonly Crypto _crypto;

        public DbEncryptionFactory(Crypto crypto)
            => _crypto = crypto;

        public async Task<DbEncryption> GenerateNew()
        {
            var pair = await _crypto.EncGenerateKeyPairAsync().ConfigureAwait(false);
            var pk = await _crypto.EncPubKeyGetAsync(pair.Item1).ConfigureAwait(false);
            var sk = await _crypto.EncSecretKeyGetAsync(pair.Item2).ConfigureAwait(false);
            return new DbEncryption(pk, sk);
        }
    }

    public class DbEncryption
    {
        public DbEncryption(byte[] publicKey, byte[] secretKey)
        {
            PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
            SecretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
            if (publicKey.Length == 0) throw new ArgumentNullException(nameof(publicKey));
            if (secretKey.Length == 0) throw new ArgumentNullException(nameof(secretKey));
        }

        public byte[] PublicKey { get; }
        public byte[] SecretKey { get; }
    }
}