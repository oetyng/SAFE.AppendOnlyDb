using System.Threading.Tasks;

namespace SAFE.Data.Client
{
    public interface IImDStore_v2
    {
        Task<byte[]> StoreImDAsync(byte[] payload);
        Task<byte[]> GetImDAsync(byte[] datamap);
    }
}