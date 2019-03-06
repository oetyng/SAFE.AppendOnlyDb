using SAFE.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    internal class StreamStore : IStreamStore
    {
        readonly MutableCollection<StreamType> _collection;

        public StreamStore(IValueAD dataTree)
            => _collection = new MutableCollection<StreamType>(dataTree);

        public async Task<Result<Pointer>> AddAsync(string type, MdLocator location)
        {
            var stream = new StreamType
            {
                StreamName = type,
                MdLocator = location
            };
            return await _collection.AddAsync(stream).ConfigureAwait(false);
        }

        public async IAsyncEnumerable<(string, MdLocator)> GetAllAsync()
        {
            var streams = _collection.GetAsync()
                .Select(c => (c.StreamName, c.MdLocator)).ConfigureAwait(false);
            await foreach (var item in streams)
                yield return item;
        }

        public async Task<Result<Pointer>> UpdateAsync(string type, MdLocator location)
        {
            var items = _collection.GetAsync()
                .SkipWhile(c => c.StreamName == type)
                .Append(new StreamType { StreamName = type, MdLocator = location });
            return await _collection.SetAsync(items).ConfigureAwait(false);
        }
    }

    class StreamType
    {
        public string StreamName { get; set; }
        public MdLocator MdLocator { get; set; }
    }
}