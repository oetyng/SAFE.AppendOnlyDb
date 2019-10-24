using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Network
{
    public interface INetworkDataOps
    {
        // Task<(byte[], byte[])> GenerateRandomKeyPair();
        Task<byte[]> StoreImmutableData(byte[] payload);
        Task<byte[]> GetImmutableData(byte[] datamap);
    }
}