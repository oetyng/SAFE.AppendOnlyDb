using SAFE.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    class MutableCollection<T>
    {
        readonly IValueAD _root;

        public MutableCollection(IValueAD root)
        {
            _root = root;
        }

        public async Task<Result<Pointer>> AddAsync(T data)
        {
            var stream = await GetStreamAsync();
            return await stream.AppendAsync(new StoredValue(data));
        }

        public async Task<IEnumerable<T>> GetAsync()
        {
            var stream = await GetStreamAsync();
            return (await stream.GetAllValuesAsync())
                .Select(c => c.Parse<T>());
        }

        public async Task<Result<Pointer>> SetAsync(IEnumerable<T> reordered)
        {
            var md = await MdAccess.CreateAsync();
            IStreamAD newStream = new DataTree(md, (t) => throw new NotSupportedException());
            foreach (var elem in reordered)
                await newStream.AppendAsync(new StoredValue(elem));
            return await _root.SetAsync(new StoredValue(md.MdLocator));
        }

        async Task<IStreamAD> GetStreamAsync()
        {
            var collection = await _root.GetValueAsync();
            if (collection is DataNotFound<StoredValue>)
            {
                await SetAsync(new List<T>());
                collection = await _root.GetValueAsync();
            }
            
            var locator = collection.Value.Parse<MdLocator>();
            var md = await MdAccess.LocateAsync(locator);
            return new DataTree(md.Value, (t) => throw new NotSupportedException());
        }
    }
}
