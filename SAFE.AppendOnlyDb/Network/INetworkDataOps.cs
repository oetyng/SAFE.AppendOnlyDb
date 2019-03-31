using System.Collections.Generic;
using System.Threading.Tasks;
using SAFE.Data;
using SafeApp;
using SafeApp.Utilities;

namespace SAFE.AppendOnlyDb.Network
{
    public interface INetworkDataOps
    {
        Session Session { get; }
        DbEncryption DbEncryption { get; }
        Task<List<byte>> TryEncryptAsync(List<byte> data);
        Task<List<byte>> TryDecryptAsync(List<byte> data);
        Task<MDataInfo> CreateEmptyMd(ulong typeTag);
        Task<MDataInfo> CreateRandomEmptyMd(NativeHandle permissionsHandle, ulong protocol);
        Task<MDataInfo> CreatePrivateEmptyMd(ulong typeTag);
        Task<List<byte>> CreatePrivateEmptyMdSerialized(ulong typeTag);
        Task<MDataInfo> CreatePrivateRandomEmptyMd(NativeHandle permissionsHandle, ulong protocol);
        Task<MDataInfo> CreatePrivateRandomMd(NativeHandle permissionsHandle, NativeHandle dataEntries, ulong protocol);
        Task<(byte[], byte[])> GenerateRandomKeyPair();
        PermissionSet GetFullPermissions();
        Task<Result<MDataInfo>> LocatePublicMd(byte[] xor, ulong protocol);
        Task<Result<MDataInfo>> LocatePrivateMd(byte[] xor, ulong protocol, byte[] secEncKey, byte[] nonce);
        Task<byte[]> StoreImmutableData(byte[] payload);
        Task<byte[]> GetImmutableData(byte[] datamap);
        Task<List<byte>> StoreImmutableData(List<byte> payload);
        Task<List<byte>> GetImmutableData(List<byte> datamap);
    }
}