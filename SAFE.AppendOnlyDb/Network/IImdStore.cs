using System.Threading.Tasks;

namespace SAFE.Data.Client
{
    public interface IImDStore
    {
        Task<byte[]> StoreImDAsync(byte[] payload);
        Task<byte[]> GetImDAsync(byte[] datamap);
    }
}