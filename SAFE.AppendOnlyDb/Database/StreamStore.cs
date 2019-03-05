using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    class StreamType
    {
        public string StreamName { get; set; }
        public MdLocator MdLocator { get; set; }
    }

    internal class StreamStore : IStreamStore
    {
        readonly MutableCollection<StreamType> _collection;

        public StreamStore(IValueAD dataTree)
        {
            _collection = new MutableCollection<StreamType>(dataTree);
        }

        public async Task AddAsync(string type, MdLocator location)
        {
            var stream = new StreamType
            {
                StreamName = type,
                MdLocator = location
            };
            await _collection.AddAsync(stream).ConfigureAwait(false);
        }

        public async Task<IEnumerable<(string, MdLocator)>> GetAllAsync()
        {
            var streams = (await _collection.GetAsync().ConfigureAwait(false))
                .Select(c => (c.StreamName, c.MdLocator));
            return streams;
        }

        public async Task UpdateAsync(string type, MdLocator location)
        {
            var updated = new List<StreamType>();
            foreach (var item in await _collection.GetAsync())
            {
                if (item.StreamName == type)
                    updated.Add(new StreamType { StreamName = type, MdLocator = location });
                else
                    updated.Add(item);
            }
            await _collection.SetAsync(updated);
        }
    }
}