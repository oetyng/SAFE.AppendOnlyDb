using SAFE.AppendOnlyDb.Factories;
using SAFE.Data;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    internal class StreamDb : IStreamDb
    {
        readonly IStreamCollection _streams;
        readonly Network.IMdNodeFactory _nodeFactory;
        readonly DataTreeFactory _dataTreeFactory;

        protected readonly ConcurrentDictionary<string, IStreamAD> _dataTreeCache = new ConcurrentDictionary<string, IStreamAD>();
        protected ConcurrentDictionary<string, MdLocator> _dataTreeAddresses = new ConcurrentDictionary<string, MdLocator>();

        public StreamDb(IStreamCollection streams, Network.IMdNodeFactory nodeFactory)
        { 
            _streams = streams;
            _nodeFactory = nodeFactory;
            _dataTreeFactory = new DataTreeFactory(nodeFactory);
        }

        public async Task<Result<IStreamAD>> GetOrAddStreamAsync(string streamKey)
        {
            try
            {
                await InitDb();
                if (!_dataTreeAddresses.ContainsKey(streamKey))
                {
                    await InitStreamAsync(streamKey).ConfigureAwait(false);
                    await LoadStreamAsync(streamKey).ConfigureAwait(false);
                }
                if (!_dataTreeCache.ContainsKey(streamKey))
                    await LoadStreamAsync(streamKey).ConfigureAwait(false);
                return Result.OK(_dataTreeCache[streamKey]);
            }
            catch (Exception ex)
            {
                return Result.Fail<IStreamAD>(-999, ex.Message);
            }
        }

        public async Task<Result<IStreamAD>> GetStreamAsync(string streamKey)
        {
            await InitDb();
            if (!_dataTreeAddresses.ContainsKey(streamKey))
                return new KeyNotFound<IStreamAD>(streamKey);
            if (!_dataTreeCache.ContainsKey(streamKey))
                await LoadStreamAsync(streamKey).ConfigureAwait(false);
            return Result.OK(_dataTreeCache[streamKey]);
        }

        public async Task<Result<bool>> AddStreamAsync(string streamKey)
        {
            try
            {
                await InitDb();
                if (_dataTreeAddresses.ContainsKey(streamKey))
                    return Result.OK(false);

                await InitStreamAsync(streamKey).ConfigureAwait(false);
                await LoadStreamAsync(streamKey).ConfigureAwait(false);

                return Result.OK(true);
            }
            catch(Exception ex)
            {
                return Result.Fail<bool>(-999, ex.Message);
            }
        }

        async Task InitStreamAsync(string type)
        {
            await InitDb();
            if (_dataTreeAddresses.ContainsKey(type))
                return;

            Task OnHeadChange(MdLocator newLocation) => UpdateTypeStores(type, newLocation);

            var dataTree = await _dataTreeFactory.CreateAsync(OnHeadChange).ConfigureAwait(false);
            _dataTreeCache[type] = dataTree;
            _dataTreeAddresses[type] = dataTree.MdLocator;
            await _streams.AddAsync(type, dataTree.MdLocator).ConfigureAwait(false);
        }

        async Task LoadStreamAsync(string type)
        {
            await InitDb();
            if (!_dataTreeAddresses.ContainsKey(type))
                throw new InvalidOperationException($"Store does not exist! {type}");

            Task OnHeadChange(MdLocator newXOR) => UpdateTypeStores(type, newXOR);
            var headResult = await _nodeFactory.LocateAsync(_dataTreeAddresses[type]).ConfigureAwait(false);
            if (!headResult.HasValue)
                throw new Exception($"Error code: {headResult.ErrorCode.Value}. {headResult.ErrorMsg}");
            var dataTree = new DataTree(headResult.Value, OnHeadChange);
            _dataTreeCache[type] = dataTree;
        }

        async Task UpdateTypeStores(string type, MdLocator location)
        {
            await InitDb();
            await _streams.UpdateAsync(type, location).ConfigureAwait(false);
            _dataTreeAddresses[type] = location;
        }

        async Task InitDb()
        {
            if (_dataTreeAddresses == null)
            {
                var streams = await _streams.GetAllAsync()
                    .ToDictionaryAsync(c => c.Item1, c => c.Item2);

                _dataTreeAddresses = new ConcurrentDictionary<string, MdLocator>(streams);
            }
        }
    }
}