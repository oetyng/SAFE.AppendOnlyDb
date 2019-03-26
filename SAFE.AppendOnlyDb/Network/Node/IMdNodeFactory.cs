using System.Threading.Tasks;
using SAFE.Data;

namespace SAFE.AppendOnlyDb.Network
{
    internal interface IMdNodeFactory
    {
        Task<IMdNode> CreateNewMdNodeAsync(MdMetadata metadata);
        Task<Result<IMdNode>> LocateAsync(MdLocator location);
    }
}