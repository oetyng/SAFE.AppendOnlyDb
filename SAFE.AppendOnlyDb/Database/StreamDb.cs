using SAFE.AppendOnlyDb.Factories;
using SAFE.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("SAFE.AppendOnlyDb.Tests")]

namespace SAFE.AppendOnlyDb
{
    internal class StreamDb : IStreamDb
    {
        readonly IStreamStore _streamStore;

        protected readonly ConcurrentDictionary<string, IStreamAD> _dataTreeCache = new ConcurrentDictionary<string, IStreamAD>();
        protected ConcurrentDictionary<string, MdLocator> _dataTreeAddresses = new ConcurrentDictionary<string, MdLocator>();

        protected StreamDb(IStreamStore streamStore)
            => _streamStore = streamStore;

        public static async Task<Result<IStreamDb>> GetOrAddAsync(MdHead mdHead)
        {
            IValueAD root = new DataTree(mdHead.Md, (s) => throw new ArgumentOutOfRangeException("Can only add 1k items to this collection."));
            var streamStore = await StreamStoreFactory.GetOrAddStreamStoreAsync(root).ConfigureAwait(false);

            var db = new StreamDb(streamStore);

            var streams = await streamStore.GetAllAsync()
                .ToDictionaryAsync(c => c.Item1, c => c.Item2);

            db._dataTreeAddresses = new ConcurrentDictionary<string, MdLocator>(streams);

            return Result.OK((IStreamDb)db);
        }

        public async IAsyncEnumerable<T> GetStreamAsync<T>(string streamKey)
        {
            if (!_dataTreeCache.ContainsKey(streamKey))
                await LoadStoreAsync(streamKey).ConfigureAwait(false);

            var items = _dataTreeCache[streamKey]
                .ReadForwardFromAsync(0)
                .Select(c => c.Item2.Parse<T>());

            await foreach (var item in items)
                yield return item;
        }

        public async Task<Result<T>> GetAtVersionAsync<T>(string streamKey, ulong version)
        {
            if (!_dataTreeCache.ContainsKey(streamKey))
                await LoadStoreAsync(streamKey).ConfigureAwait(false);

            var value = await _dataTreeCache[streamKey]
                .GetAtVersionAsync(version)
                .ConfigureAwait(false);

            if (!value.HasValue)
                return Result.Fail<T>((int)value.ErrorCode, value.ErrorMsg);

            return Result.OK(value.Value.Parse<T>());
        }

        public async Task<Result<Pointer>> AppendAsync(string streamKey, object data)
        {
            if (!_dataTreeAddresses.ContainsKey(streamKey))
                await AddStoreAsync(streamKey).ConfigureAwait(false);
            if (!_dataTreeCache.ContainsKey(streamKey))
                await LoadStoreAsync(streamKey).ConfigureAwait(false);

            var value = new StoredValue(data);
            var pointer = await _dataTreeCache[streamKey]
                .AppendAsync(value)
                .ConfigureAwait(false);

            if (!pointer.HasValue)
                return pointer;

            return pointer;
        }

        protected async Task AddStoreAsync(string type)
        {
            if (_dataTreeAddresses.ContainsKey(type))
                return;

            Task OnHeadChange(MdLocator newLocation) => UpdateTypeStores(type, newLocation);

            var dataTree = await DataTreeFactory.CreateAsync(OnHeadChange).ConfigureAwait(false);
            _dataTreeCache[type] = dataTree;
            _dataTreeAddresses[type] = dataTree.MdLocator;
            await _streamStore.AddAsync(type, dataTree.MdLocator).ConfigureAwait(false);
        }

        protected async Task LoadStoreAsync(string type)
        {
            if (!_dataTreeAddresses.ContainsKey(type))
                throw new InvalidOperationException($"Store does not exist! {type}");

            Task OnHeadChange(MdLocator newXOR) => UpdateTypeStores(type, newXOR);
            var headResult = await MdAccess.LocateAsync(_dataTreeAddresses[type]).ConfigureAwait(false);
            if (!headResult.HasValue)
                throw new Exception($"Error code: {headResult.ErrorCode.Value}. {headResult.ErrorMsg}");
            var dataTree = new DataTree(headResult.Value, OnHeadChange);
            _dataTreeCache[type] = dataTree;
        }

        async Task UpdateTypeStores(string type, MdLocator location)
        {
            await _streamStore.UpdateAsync(type, location).ConfigureAwait(false);
            _dataTreeAddresses[type] = location;
        }
    }
}