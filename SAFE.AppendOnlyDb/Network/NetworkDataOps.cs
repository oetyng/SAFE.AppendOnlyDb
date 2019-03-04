using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SAFE.Data;
using SafeApp;
using SafeApp.Misc;
using SafeApp.Utilities;

[assembly: InternalsVisibleTo("SAFE.AppendOnlyDb.Tests")]

namespace SAFE.AppendOnlyDb.Network
{
    internal class NetworkDataOps : INetworkDataOps
    {
        public Session Session { get; }

        public NetworkDataOps(Session session)
        {
            Session = session;
        }

        public static async Task<byte[]> GetMdXorName(string plainTextId)
        {
            return (await Crypto.Sha3HashAsync(plainTextId.ToUtfBytes())).ToArray();
        }

        // Creates with data.

        /// <summary>
        /// Empty, without data.
        /// </summary>
        /// <param name="permissionsHandle"></param>
        /// <returns>SerialisedMdInfo</returns>
        public async Task<MDataInfo> CreateEmptyRandomPrivateMd(NativeHandle permissionsHandle, ulong protocol)
        {
            return await CreateRandomPrivateMd(permissionsHandle, NativeHandle.EmptyMDataEntries, protocol);
        }

        /// <summary>
        /// </summary>
        /// <param name="permissionsHandle"></param>
        /// <param name="dataEntries"></param>
        /// <returns>A serialised MdInfo</returns>
        public async Task<MDataInfo> CreateRandomPrivateMd(NativeHandle permissionsHandle, NativeHandle dataEntries, ulong protocol)
        {
            var mdInfo = await Session.MDataInfoActions.RandomPrivateAsync(protocol);
            await Session.MData.PutAsync(mdInfo, permissionsHandle, dataEntries); // <----------------------------------------------    Commit ------------------------
            return mdInfo;
        }

        public async Task<Result<MDataInfo>> LocatePublicMd(byte[] xor, ulong protocol)
        {
            var md = new MDataInfo { Name = xor, TypeTag = protocol };

            try
            {
                await Session.MData.ListKeysAsync(md);
            }
            catch
            {
                // (System.Exception ex)
                return new KeyNotFound<MDataInfo>($"Could not find Md with tag type {protocol} and address {xor}");
            }

            return Result.OK(md);
        }

        public async Task<Result<MDataInfo>> LocatePrivateMd(byte[] xor, ulong protocol, byte[] secEncKey, byte[] nonce)
        {
            var md = new MDataInfo { Name = xor, TypeTag = protocol };

            try
            {
                await Session.MData.ListKeysAsync(md);
            }
            catch
            {
                // (System.Exception ex)
                return new KeyNotFound<MDataInfo>($"Could not find Md with tag type {protocol} and address {xor}");
            }

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

                var info = await Session.MDataInfoActions.RandomPrivateAsync(typeTag);
                await Session.MData.PutAsync(info, permissionH, NativeHandle.EmptyMDataEntries); // <----------------------------------------------    Commit ------------------------
                return info;
            }
        }

        public async Task<List<byte>> CreateEmptyMdSerialized(ulong typeTag)
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
            {
                using (var seWriterHandle = await Session.IData.NewSelfEncryptorAsync())
                {
                    await Session.IData.WriteToSelfEncryptorAsync(seWriterHandle, payload.ToList());
                    var datamap = await Session.IData.CloseSelfEncryptorAsync(seWriterHandle, cipherOptHandle);
                    return datamap;
                }
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
            {
                using (var seWriterHandle = await Session.IData.NewSelfEncryptorAsync())
                {
                    await Session.IData.WriteToSelfEncryptorAsync(seWriterHandle, payload);
                    var datamap = await Session.IData.CloseSelfEncryptorAsync(seWriterHandle, cipherOptHandle);
                    return datamap.ToList();
                }
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
            {
                using (var inboxEncSkH = randomKeyPairTuple.Item2)
                {
                    encPublicKey = await Session.Crypto.EncPubKeyGetAsync(inboxEncPkH);
                    encSecretKey = await Session.Crypto.EncSecretKeyGetAsync(inboxEncSkH);
                }
            }
            return (encPublicKey, encSecretKey);
        }
    }
}