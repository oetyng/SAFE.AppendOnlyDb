using SAFE.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    /// <summary>
    /// A collection which has 
    /// at most 999 items. 
    /// </summary>
    /// <typeparam name="T">Type of data in the collection.</typeparam>
    class MutableCollection<T>
    {
        readonly IValueAD _root;
        readonly Factories.DataTreeFactory _dataTreeFactory;

        public MutableCollection(IValueAD root, Factories.DataTreeFactory dataTreeFactory)
        {
            _root = root;
            _dataTreeFactory = dataTreeFactory;
        }

        public async Task<Result<Pointer>> AddAsync(T data)
        {
            var stream = await GetStreamAsync();
            return await stream.AppendAsync(new StoredValue(data));
        }

        public async IAsyncEnumerable<T> GetAsync()
        {
            var stream = await GetStreamAsync();
            var items = stream.GetAllValuesAsync()
                .Select(c => c.Parse<T>());
            await foreach (var item in items)
                yield return item;
        }

        public async Task<Result<Pointer>> SetAsync(IAsyncEnumerable<T> reordered)
        {
            IStreamAD newStream = await _dataTreeFactory.CreateAsync((t) => throw new NotSupportedException());
            await foreach (var elem in reordered)
                await newStream.AppendAsync(new StoredValue(elem));
            return await _root.SetAsync(new StoredValue(newStream.MdLocator));
        }

        async Task<IStreamAD> GetStreamAsync()
        {
            var currentValue = await _root.GetValueAsync();
            if (currentValue is DataNotFound<StoredValue>)
            {
                await SetAsync(AsyncEnumerable.Empty<T>());
                currentValue = await _root.GetValueAsync();
            }
            
            var locator = currentValue.Value.Parse<MdLocator>();
            var dataTree = await _dataTreeFactory.LocateAsync(locator, (t) => throw new NotSupportedException());
            return dataTree.Value;
        }
    }
}