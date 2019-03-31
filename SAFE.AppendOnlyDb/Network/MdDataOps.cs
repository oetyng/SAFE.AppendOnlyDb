using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SAFE.AppendOnlyDb.Utils;
using SafeApp;
using SafeApp.Utilities;

namespace SAFE.AppendOnlyDb.Network
{
    internal class MdDataOps : IMdDataOps
    {
        readonly MDataInfo _mdInfo;
        readonly INetworkDataOps _networkOps;
        readonly ImDStore _imDStore;

        public Session Session { get; }
        public IMdNodeFactory NodeFactory { get; }
        public MdLocator MdLocator => new MdLocator(_mdInfo.Name, _mdInfo.TypeTag);

        public MdDataOps(IMdNodeFactory nodeFactory, INetworkDataOps networkOps, MDataInfo mdInfo)
        {
            _mdInfo = mdInfo;
            _networkOps = networkOps;
            _imDStore = new ImDStore(networkOps);
            Session = networkOps.Session;
            NodeFactory = nodeFactory;
        }

        public async Task<int> GetKeyCountAsync()
        {
            var keys = await Session.MData.ListKeysAsync(_mdInfo).ConfigureAwait(false);
            return keys.Count;
        }

        public async Task<bool> ContainsKeyAsync(string key)
        {
            var keyEntries = await Session.MData.ListKeysAsync(_mdInfo).ConfigureAwait(false);
            var keys = keyEntries.Select(c => c.Key);
            var encryptedKey = await _networkOps.TryEncryptAsync(key.ToUtfBytes()).ConfigureAwait(false);
            return keys.Any(c => c.SequenceEqual(encryptedKey));
        }

        public async IAsyncEnumerable<string> GetKeysAsync()
        {
            var keyEntries = await Session.MData.ListKeysAsync(_mdInfo).ConfigureAwait(false);
            var keyTasks = keyEntries.Select(c => _networkOps.TryDecryptAsync(c.Key));

            var data = new ConcurrentBag<string>();
            Parallel.ForEach(keyEntries, c =>
            {
                var key = _networkOps.TryDecryptAsync(c.Key).GetAwaiter().GetResult();
                data.Add(key.ToUtfString());
            });

            foreach (var entry in data)
                yield return entry;
        }

        public async Task<T> GetValueAsync<T>(string key)
        {
            var ret = await GetStringValueAsync(key).ConfigureAwait(false);
            return ret.Item1.Parse<T>();
        }

        public async Task<ulong> GetEntryVersionAsync(string key)
        {
            var encryptedKey = await _networkOps.TryEncryptAsync(key.ToUtfBytes()).ConfigureAwait(false);
            var mdRef = await Session.MData.GetValueAsync(_mdInfo, encryptedKey).ConfigureAwait(false);
            return mdRef.Item2;
        }

        public async Task<(string, ulong)> GetStringValueAsync(string key)
        {
            var keyBytes = key.ToUtfBytes();
            var encryptedKey = await _networkOps.TryEncryptAsync(keyBytes).ConfigureAwait(false);
            var mdRef = await Session.MData.GetValueAsync(_mdInfo, encryptedKey).ConfigureAwait(false);
            var map = await _networkOps.TryDecryptAsync(mdRef.Item1).ConfigureAwait(false);

            // get ImD
            var val = await GetImDAsync(map);
            return (val.ToUtfString(), mdRef.Item2);
        }

        // Todo: evaluate if this should fetch entries instead, internally, and filter out the 2 reserved fields
        public async IAsyncEnumerable<T> GetValuesAsync<T>()
        {
            var values = new ConcurrentBag<T>();
            using (var entriesHandle = await Session.MDataEntries.GetHandleAsync(_mdInfo).ConfigureAwait(false))
            {
                // Fetch and decrypt entries
                var encryptedEntries = await Session.MData.ListEntriesAsync(entriesHandle).ConfigureAwait(false);
                Parallel.ForEach(encryptedEntries, entry =>
                {
                    // protects against deleted entries
                    if (entry.Value.Content.Count != 0)
                    {
                        var decryptedValue = _networkOps.TryDecryptAsync(entry.Value.Content).GetAwaiter().GetResult();
                        var data = GetImDAsync(decryptedValue).GetAwaiter().GetResult();
                        if (data.ToUtfString().TryParse(out T result))
                            values.Add(result);
                    }
                });
            }
            foreach (var val in values)
                yield return val;
        }

        public IAsyncEnumerable<(TKey, TVal)> GetEntriesAsync<TKey, TVal>(Func<string, TKey> keyParser)
            => GetEntriesAsync<TKey, TVal>(keyParser, k => true);

        public async IAsyncEnumerable<(TKey, TVal)> GetEntriesAsync<TKey, TVal>(Func<string, TKey> keyParser, Func<TKey, bool> selector)
        {
            var values = new ConcurrentBag<(TKey, TVal)>();
            using (var entriesHandle = await Session.MDataEntries.GetHandleAsync(_mdInfo).ConfigureAwait(false))
            {
                // Fetch and decrypt entries
                var encryptedEntries = await Session.MData.ListEntriesAsync(entriesHandle).ConfigureAwait(false);

                Parallel.ForEach(encryptedEntries, entry =>
                {
                    // protects against deleted entries // should not be valid operation on append only though
                    if (entry.Value.Content.Count != 0)
                    {
                        var decryptedKey = _networkOps.TryDecryptAsync(entry.Key.Key).GetAwaiter().GetResult();
                        var keystring = decryptedKey.ToUtfString();
                        if (keystring == Constants.METADATA_KEY)
                            return;

                        var key = keyParser(keystring);
                        if (!selector(key)) // within range forexample
                            return;

                        var decryptedValue = _networkOps.TryDecryptAsync(entry.Value.Content).GetAwaiter().GetResult();

                        var data = GetImDAsync(decryptedValue).GetAwaiter().GetResult();
                        if (data.ToUtfString().TryParse(out TVal result))
                            values.Add((key, result));
                    }
                });
            }
            foreach (var val in values)
                yield return val;
        }

        public async Task AddObjectAsync(string key, object value)
        {
            using (var entryActionsH = await Session.MDataEntryActions.NewAsync().ConfigureAwait(false))
            {
                var insertObj = new Dictionary<string, object>
                {
                    { key, value }
                };
                await InsertEntriesAsync(entryActionsH, insertObj).ConfigureAwait(false);
                await CommitEntryMutationAsync(entryActionsH).ConfigureAwait(false);
            }
        }

        public async Task UpdateObjectAsync(string key, object value, ulong version)
        {
            using (var entryActionsH = await Session.MDataEntryActions.NewAsync().ConfigureAwait(false))
            {
                var updateObj = new Dictionary<string, (object, ulong)>
                    {
                        { key, (value, version + 1) },
                    };
                await UpdateEntriesAsync(entryActionsH, updateObj).ConfigureAwait(false);
                await CommitEntryMutationAsync(entryActionsH).ConfigureAwait(false);
            }
        }

        public async Task DeleteObjectAsync(string key, ulong version)
        {
            using (var entryActionsH = await Session.MDataEntryActions.NewAsync().ConfigureAwait(false))
            {
                var deleteObj = new Dictionary<string, ulong>
                {
                    { key, version + 1 }
                };
                await DeleteEntriesAsync(entryActionsH, deleteObj).ConfigureAwait(false);
                await CommitEntryMutationAsync(entryActionsH).ConfigureAwait(false);
            }
        }

        // Populate the md entry actions handle.
        public async Task InsertEntriesAsync(NativeHandle entryActionsH, Dictionary<string, object> data)
        {
            foreach (var pair in data)
            {
                // store value to ImD
                var map = await StoreImDAsync(pair.Value.Json().ToUtfBytes());

                var encryptedKey = await _networkOps.TryEncryptAsync(pair.Key.ToUtfBytes()).ConfigureAwait(false);
                var encryptedValue = await _networkOps.TryEncryptAsync(map).ConfigureAwait(false);
                await Session.MDataEntryActions.InsertAsync(entryActionsH, encryptedKey, encryptedValue).ConfigureAwait(false);
            }
        }

        // Populate the md entry actions handle.
        public async Task UpdateEntriesAsync(NativeHandle entryActionsH, Dictionary<string, (object, ulong)> data)
        {
            foreach (var pair in data)
            {
                // store value to ImD

                var val = pair.Value.Item1;
                var map = await StoreImDAsync(val.Json().ToUtfBytes());

                var version = pair.Value.Item2;

                var encryptedKey = await _networkOps.TryEncryptAsync(pair.Key.ToUtfBytes()).ConfigureAwait(false);
                var encryptedValue = await _networkOps.TryEncryptAsync(map).ConfigureAwait(false);

                await Session.MDataEntryActions.UpdateAsync(entryActionsH, encryptedKey, encryptedValue, version).ConfigureAwait(false);
            }
        }

        // Populate the md entry actions handle.
        public async Task DeleteEntriesAsync(NativeHandle entryActionsH, Dictionary<string, ulong> data)
        {
            foreach (var pair in data)
            {
                var version = pair.Value;
                var encryptedKey = await _networkOps.TryEncryptAsync(pair.Key.ToUtfBytes()).ConfigureAwait(false);
                await Session.MDataEntryActions.DeleteAsync(entryActionsH, encryptedKey, version).ConfigureAwait(false);
            }
        }

        // Commit the operations in the md entry actions handle.
        public Task CommitEntryMutationAsync(NativeHandle entryActionsH)
            => Session.MData.MutateEntriesAsync(_mdInfo, entryActionsH); // <----------------------------------------------    Commit ------------------------

        // While map size is too large for an MD entry
        // store the map as ImD.
        async Task<List<byte>> StoreImDAsync(List<byte> payload)
        {
            if (payload.Count < 1000)
                return payload;
            var map = await _imDStore.StoreImDAsync(payload.ToArray());
            return map.ToList();
        }

        // While not throwing,
        // the payload is a datamap.
        // NB: Obviously this is not resistant to other errors,
        // so we must catch the specific exception here. (todo)
        async Task<List<byte>> GetImDAsync(List<byte> map)
        {
            var data = await _imDStore.GetImDAsync(map.ToArray());
            return data.ToList();
        }
    }
}