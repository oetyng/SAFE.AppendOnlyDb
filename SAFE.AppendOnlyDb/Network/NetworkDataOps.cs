using System.Linq;
using System.Threading.Tasks;
using SafeApp;
using SafeApp.Misc;
using SafeApp.Utilities;

namespace SAFE.AppendOnlyDb.Network
{
    public class NetworkDataOps : INetworkDataOps
    {
        Session Session { get; }

        public NetworkDataOps(Session session)
            => Session = session;

        public static async Task<byte[]> GetMdXorName(string plainTextId)
            => (await Crypto.Sha3HashAsync(plainTextId.ToUtfBytes())).ToArray();

        // Returns data map (which acts as an address).
        public async Task<byte[]> StoreImmutableData(byte[] payload)
        {
            using var cipherOptHandle = await Session.CipherOpt.NewPlaintextAsync();
            using var seWriterHandle = await Session.IData.NewSelfEncryptorAsync();
            await Session.IData.WriteToSelfEncryptorAsync(seWriterHandle, payload.ToList());
            var datamap = await Session.IData.CloseSelfEncryptorAsync(seWriterHandle, cipherOptHandle);
            return datamap;
        }

        public async Task<byte[]> GetImmutableData(byte[] datamap)
        {
            using var seReaderHandle = await Session.IData.FetchSelfEncryptorAsync(datamap);
            var len = await Session.IData.SizeAsync(seReaderHandle);
            var readData = await Session.IData.ReadFromSelfEncryptorAsync(seReaderHandle, 0, len);
            return readData.ToArray();
        }

        public async Task<(byte[], byte[])> GenerateRandomKeyPair()
        {
            var randomKeyPairTuple = await Session.Crypto.EncGenerateKeyPairAsync();
            byte[] encPublicKey, encSecretKey;
            using (var inboxEncPkH = randomKeyPairTuple.Item1)
            using (var inboxEncSkH = randomKeyPairTuple.Item2)
            {
                encPublicKey = await Session.Crypto.EncPubKeyGetAsync(inboxEncPkH);
                encSecretKey = await Session.Crypto.EncSecretKeyGetAsync(inboxEncSkH);
            }
            return (encPublicKey, encSecretKey);
        }
    }
}