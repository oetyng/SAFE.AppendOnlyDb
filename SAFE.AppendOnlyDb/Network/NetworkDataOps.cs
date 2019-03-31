using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SAFE.Data;
using SafeApp;
using SafeApp.Misc;
using SafeApp.Utilities;

namespace SAFE.AppendOnlyDb.Network
{
    internal class NetworkDataOps : INetworkDataOps
    {
        public Session Session { get; }
        public DbEncryption DbEncryption { get; }

        public NetworkDataOps(Session session, DbEncryption options = null)
        {
            Session = session;
            DbEncryption = options;
        }

        public static async Task<byte[]> GetMdXorName(string plainTextId)
            => (await Crypto.Sha3HashAsync(plainTextId.ToUtfBytes())).ToArray();

        public async Task<List<byte>> TryEncryptAsync(List<byte> data)
        {
            if (DbEncryption == null)
                return data;
            using (var publicKey = await Session.Crypto.EncPubKeyNewAsync(DbEncryption.PublicKey).ConfigureAwait(false))
            {
                var encrypted = await Session.Crypto.EncryptSealedBoxAsync(data, publicKey).ConfigureAwait(false);
                return encrypted;
            }
        }

        public async Task<List<byte>> TryDecryptAsync(List<byte> data)
        {
            if (DbEncryption == null)
                return data;
            using (var publicKey = await Session.Crypto.EncPubKeyNewAsync(DbEncryption.PublicKey).ConfigureAwait(false))
            using (var secretKey = await Session.Crypto.EncSecretKeyNewAsync(DbEncryption.SecretKey).ConfigureAwait(false))
            {
                var decrypted = await Session.Crypto.DecryptSealedBoxAsync(data, publicKey, secretKey).ConfigureAwait(false);
                return decrypted;
            }
        }


        // Creates with data.

        /// <summary>
        /// Empty, without data.
        /// </summary>
        /// <param name="permissionsHandle"></param>
        /// <returns>An MDataInfo</returns>
        public Task<MDataInfo> CreateRandomEmptyMd(NativeHandle permissionsHandle, ulong protocol)
            => CreateRandomMd(permissionsHandle, NativeHandle.EmptyMDataEntries, protocol);

        /// <summary>
        /// </summary>
        /// <param name="permissionsHandle"></param>
        /// <param name="dataEntries"></param>
        /// <returns>An MDataInfo</returns>
        public async Task<MDataInfo> CreateRandomMd(NativeHandle permissionsHandle, NativeHandle dataEntries, ulong protocol)
        {
            var mdInfo = await Session.MDataInfoActions.RandomPublicAsync(protocol);
            await Session.MData.PutAsync(mdInfo, permissionsHandle, dataEntries); // <----------------------------------------------    Commit ------------------------
            return mdInfo;
        }

        /// <summary>
        /// Empty, without data.
        /// </summary>
        /// <param name="permissionsHandle"></param>
        /// <returns>An MDataInfo</returns>
        public Task<MDataInfo> CreatePrivateRandomEmptyMd(NativeHandle permissionsHandle, ulong protocol)
            => CreatePrivateRandomMd(permissionsHandle, NativeHandle.EmptyMDataEntries, protocol);

        /// <summary>
        /// </summary>
        /// <param name="permissionsHandle"></param>
        /// <param name="dataEntries"></param>
        /// <returns>An MDataInfo</returns>
        public async Task<MDataInfo> CreatePrivateRandomMd(NativeHandle permissionsHandle, NativeHandle dataEntries, ulong protocol)
        {
            var mdInfo = await Session.MDataInfoActions.RandomPrivateAsync(protocol);
            await Session.MData.PutAsync(mdInfo, permissionsHandle, dataEntries); // <----------------------------------------------    Commit ------------------------
            return mdInfo;
        }

        public async Task<Result<MDataInfo>> LocatePublicMd(byte[] xor, ulong protocol)
        {
            var md = new MDataInfo { Name = xor, TypeTag = protocol };

            try { await Session.MData.ListKeysAsync(md); }
            catch { return new KeyNotFound<MDataInfo>($"Could not find Md with tag type {protocol} and address {xor}"); }

            return Result.OK(md);
        }

        public async Task<Result<MDataInfo>> LocatePrivateMd(byte[] xor, ulong protocol, byte[] secEncKey, byte[] nonce)
        {
            var md = new MDataInfo { Name = xor, TypeTag = protocol };

            try { await Session.MData.ListKeysAsync(md); }
            catch { return new KeyNotFound<MDataInfo>($"Could not find Md with tag type {protocol} and address {xor}"); }

            md = await Session.MDataInfoActions.NewPrivateAsync(xor, protocol, secEncKey, nonce);
            return Result.OK(md);
        }

        public PermissionSet GetFullPermissions()
        {
            return new PermissionSet
            {
                Delete = true,
                Insert = true,
                ManagePermissions = true,
                Read = true,
                Update = true
            };
        }

        public async Task<MDataInfo> CreateEmptyMd(ulong typeTag)
        {
            using (var permissionH = await Session.MDataPermissions.NewAsync())
            {
                using (var appSignPkH = await Session.Crypto.AppPubSignKeyAsync())
                    await Session.MDataPermissions.InsertAsync(permissionH, appSignPkH, GetFullPermissions());

                var info = await Session.MDataInfoActions.RandomPublicAsync(typeTag);
                await Session.MData.PutAsync(info, permissionH, NativeHandle.EmptyMDataEntries); // <----------------------------------------------    Commit ------------------------
                return info;
            }
        }

        public async Task<MDataInfo> CreatePrivateEmptyMd(ulong typeTag)
        {
            using (var permissionH = await Session.MDataPermissions.NewAsync())
            {
                using (var appSignPkH = await Session.Crypto.AppPubSignKeyAsync())
                    await Session.MDataPermissions.InsertAsync(permissionH, appSignPkH, GetFullPermissions());

                var info = await Session.MDataInfoActions.RandomPrivateAsync(typeTag);
                await Session.MData.PutAsync(info, permissionH, NativeHandle.EmptyMDataEntries); // <----------------------------------------------    Commit ------------------------
                return info;
            }
        }

        public async Task<List<byte>> CreatePrivateEmptyMdSerialized(ulong typeTag)
        {
            using (var permissionH = await Session.MDataPermissions.NewAsync())
            {
                using (var appSignPkH = await Session.Crypto.AppPubSignKeyAsync())
                    await Session.MDataPermissions.InsertAsync(permissionH, appSignPkH, GetFullPermissions());

                var info = await Session.MDataInfoActions.RandomPrivateAsync(typeTag);
                await Session.MData.PutAsync(info, permissionH, NativeHandle.EmptyMDataEntries); // <----------------------------------------------    Commit ------------------------
                return await Session.MDataInfoActions.SerialiseAsync(info);
            }
        }

        // Returns data map (which acts as an address).
        public async Task<byte[]> StoreImmutableData(byte[] payload)
        {
            using (var cipherOptHandle = await Session.CipherOpt.NewPlaintextAsync())
            using (var seWriterHandle = await Session.IData.NewSelfEncryptorAsync())
            {
                await Session.IData.WriteToSelfEncryptorAsync(seWriterHandle, payload.ToList());
                var datamap = await Session.IData.CloseSelfEncryptorAsync(seWriterHandle, cipherOptHandle);
                return datamap;
            }
        }

        public async Task<byte[]> GetImmutableData(byte[] datamap)
        {
            using (var seReaderHandle = await Session.IData.FetchSelfEncryptorAsync(datamap))
            {
                var len = await Session.IData.SizeAsync(seReaderHandle);
                var readData = await Session.IData.ReadFromSelfEncryptorAsync(seReaderHandle, 0, len);
                return readData.ToArray();
            }
        }

        public async Task<List<byte>> StoreImmutableData(List<byte> payload)
        {
            using (var cipherOptHandle = await Session.CipherOpt.NewPlaintextAsync())
            using (var seWriterHandle = await Session.IData.NewSelfEncryptorAsync())
            {
                await Session.IData.WriteToSelfEncryptorAsync(seWriterHandle, payload);
                var datamap = await Session.IData.CloseSelfEncryptorAsync(seWriterHandle, cipherOptHandle);
                return datamap.ToList();
            }
        }

        public async Task<List<byte>> GetImmutableData(List<byte> datamap)
        {
            using (var seReaderHandle = await Session.IData.FetchSelfEncryptorAsync(datamap.ToArray()))
            {
                var len = await Session.IData.SizeAsync(seReaderHandle);
                var readData = await Session.IData.ReadFromSelfEncryptorAsync(seReaderHandle, 0, len);
                return readData;
            }
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