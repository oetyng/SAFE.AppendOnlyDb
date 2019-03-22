using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Factories
{
    internal class StreamStoreFactory
    {
        public static async Task<IStreamStore> GetOrAddStreamStoreAsync(IValueAD dbInfoMd)
        {
            IMdNode typeStoreHead;
            var typeStoreResult = await dbInfoMd.GetValueAsync().ConfigureAwait(false);
            if (!typeStoreResult.HasValue)
            {
                typeStoreHead = await MdAccess.CreateAsync(null)
                    .ConfigureAwait(false);
                await dbInfoMd.SetAsync(new StoredValue(typeStoreHead.MdLocator))
                    .ConfigureAwait(false);
            }
            else
            {
                var typeStoreHeadLocation = typeStoreResult.Value.Parse<MdLocator>();
                typeStoreHead = (await MdAccess.LocateAsync(typeStoreHeadLocation)
                    .ConfigureAwait(false)).Value;
            }

            Task OnHeadChange(MdLocator newLocation) => dbInfoMd.SetAsync(new StoredValue(newLocation));

            var dataTree = new DataTree(typeStoreHead, OnHeadChange);

            return new StreamStore(dataTree);
        }
    }
}