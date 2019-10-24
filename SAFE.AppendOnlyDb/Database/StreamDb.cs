using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SAFE.AppendOnlyDb.Network;
using SAFE.Data;

namespace SAFE.AppendOnlyDb.AD.Database
{
    internal class StreamDb : IStreamDb
    {
        Index _nextUnusedIndex;
        readonly ISeqAppendOnly _dbHistory;
        readonly Snapshots.Snapshotter _snapshotter;
        readonly Dictionary<string, ISeqAppendOnly> _streams = new Dictionary<string, ISeqAppendOnly>();

        public StreamDb(ISeqAppendOnly dbHistory, Snapshots.Snapshotter snapshotter)
        {
            _dbHistory = dbHistory;
            _snapshotter = snapshotter;
        }

        public void Init()
        {
            var history = _dbHistory
                .GetEntries()
                .Select(c => c.ToStoredValue())
                .Select(c => c.Parse<StreamDbEvent>());
            foreach (var e in history)
                Apply(e);
        }

        public async Task<Result<bool>> AddStreamAsync(string streamKey)
        {
            if (_streams.ContainsKey(streamKey))
                return Result.OK(false);

            var address = new Address
            {
                Name = new XorName { Value = AddressUtil.GetHash(streamKey) },
                Tag = 0,
            };

            StreamDbEvent e = new StreamAdded(streamKey, address);
            var result = await _dbHistory.AppendAsync(new StoredValue(e).ToEntries(), _nextUnusedIndex);

            if (result.HasValue)
            {
                Apply(e);
                return Result.OK(true);
            }
            else
                return result.CastError<Index, bool>();
        }

        public async Task<Result<IStreamAD>> GetOrAddStreamAsync(string streamKey)
        {
            if (!_streams.ContainsKey(streamKey))
            {
                var res = await AddStreamAsync(streamKey);
                if (!res.HasValue || !res.Value)
                    return Result.Fail<IStreamAD>(-1, "");
            }

            return await GetStreamAsync(streamKey);
        }

        public Task<Result<IStreamAD>> GetStreamAsync(string streamKey)
        {
            if (_streams.ContainsKey(streamKey))
                return Task.FromResult(Result.OK((IStreamAD)new ValueStream(_streams[streamKey], _snapshotter)));
            else
                return Task.FromResult((Result<IStreamAD>)new KeyNotFound<IStreamAD>());
        }

        void Apply(StreamDbEvent e)
        {
            Apply((StreamAdded)e);
            _nextUnusedIndex = new Index(_nextUnusedIndex.Value + 1);
        }

        void Apply(StreamAdded e)
        {
            var stream = new SeqAppendOnlyDataMock(e.Address);
            _streams.Add(e.StreamName, stream);
        }
    }
    
    class StreamDbEvent
    {

    }

    class StreamDbInitiated : StreamDbEvent
    {
        public StreamDbInitiated(string dbName, Address address)
        {
            DbName = dbName;
            Address = address;
        }

        public string DbName { get; }
        public Address Address { get; }
    }

    class StreamAdded : StreamDbEvent
    {
        public StreamAdded(string streamName, Address address)
        {
            StreamName = streamName;
            Address = address;
        }

        public string StreamName { get; }
        public Address Address { get; }
    }
}
