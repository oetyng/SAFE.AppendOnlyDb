using System.Threading.Tasks;

namespace SAFE.Data.Client
{
    public interface IStorageClient
    {
        Task<T> GetOrAddDbAsync<T>(string dbId);
        IImDStore GetImDStore();
    }
}