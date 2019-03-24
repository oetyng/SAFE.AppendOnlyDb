using SAFE.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    internal interface IMdNode
    {
        byte[] Snapshot { get; }

        ulong StartIndex { get; }
        ulong EndIndex { get; }
        MdLocator Previous { get; }
        MdLocator Next { get; }

        int Count { get; }
        bool IsFull { get; }
        int Level { get; }
        MdType Type { get; }
        MdLocator MdLocator { get; }
        ExpectedVersion Version { get; }

        Network.IMdNodeFactory NodeFactory { get; }

        // StreamAD
        Task<Result<Pointer>> AppendAsync(StoredValue value);
        Task<Result<StoredValue>> FindAsync(ulong version);
        IAsyncEnumerable<(ulong, StoredValue)> ReadToEndAsync(ulong from);
        IAsyncEnumerable<(ulong, StoredValue)> FindRangeAsync(ulong from, ulong to); // from and to can be any values
        IAsyncEnumerable<StoredValue> GetAllValuesAsync();
        // (Snapshot, IAsyncEnumerable<StoredValue>) GetStateSource(); // returns the snapshot - if any - and all events since (or just the events if no snapshot exists)

        // ValueAD
        Task<Result<StoredValue>> GetLastVersionAsync(); // GetValueAsync

        // StreamAD / ValueAD
        Task<Result<Pointer>> TryAppendAsync(StoredValue value, ExpectedVersion version);
        

        // Internal
        Task<Result<Pointer>> AddAsync(Pointer pointer);
        Task<Result<StoredValue>> GetValueAsync(ulong version);
        IAsyncEnumerable<(Pointer, StoredValue)> GetAllPointerValuesAsync(); // dubious: consider getting for range instead
        Task<Result<(Pointer, StoredValue)>> GetPointerAndValueAsync(ulong version); // for indexing
        Task<Result<bool>> SetNext(IMdNode node); // dubious: mixed level of responsibility for setting previous and next
        Task<ulong> GetCount(); // dubious
    }
}