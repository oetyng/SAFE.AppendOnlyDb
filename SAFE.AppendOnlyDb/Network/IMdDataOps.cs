using System.Collections.Generic;
using System.Threading.Tasks;
using SafeApp;

namespace SAFE.AppendOnlyDb.Network
{
    internal interface IMdDataOps
    {
        Session Session { get; }
        IMdNodeFactory NodeFactory { get; }
        MdLocator MdLocator { get; }

        Task AddObjectAsync(string key, object value);
        Task CommitEntryMutationAsync(NativeHandle entryActionsH);
        Task<bool> ContainsKeyAsync(string key);
        Task DeleteEntriesAsync(NativeHandle entryActionsH, Dictionary<string, ulong> data);
        Task DeleteObjectAsync(string key, ulong version);
        IAsyncEnumerable<(TKey, TVal)> GetEntriesAsync<TKey, TVal>(System.Func<string, TKey> keyParser);
        IAsyncEnumerable<(TKey, TVal)> GetEntriesAsync<TKey, TVal>(System.Func<string, TKey> keyParser, System.Func<TKey, bool> selector);
        Task<ulong> GetEntryVersionAsync(string key);
        Task<int> GetKeyCountAsync();
        IAsyncEnumerable<string> GetKeysAsync();
        Task<(string, ulong)> GetStringValueAsync(string key);
        Task<T> GetValueAsync<T>(string key);
        IAsyncEnumerable<T> GetValuesAsync<T>();
        Task InsertEntriesAsync(NativeHandle entryActionsH, Dictionary<string, object> data);
        Task UpdateEntriesAsync(NativeHandle entryActionsH, Dictionary<string, (object, ulong)> data);
        Task UpdateObjectAsync(string key, object value, ulong version);
    }
}