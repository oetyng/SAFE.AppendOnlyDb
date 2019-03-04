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
    internal class MdDataOps
    {
        readonly MDataInfo _mdInfo;
        readonly INetworkDataOps _networkDataOps;

        public Session Session { get; set; }

        public MdDataOps(Session session, MDataInfo mdInfo)
        {
            Session = session;
            _mdInfo = mdInfo;
            _networkDataOps = new NetworkDataOps(session);
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
            var encryptedKey = await Session.MDataInfoActions.EncryptEntryKeyAsync(_mdInfo, key.ToUtfBytes()).ConfigureAwait(false);
            return keys.Any(c => c.SequenceEqual(encryptedKey));
        }

        public async Task<IEnumerable<string>> GetKeysAsync()
        {
            var keyEntries = await Session.MData.ListKeysAsync(_mdInfo).ConfigureAwait(false);
            var keyTasks = keyEntries.Select(c => Session.MDataInfoActions.DecryptAsync(_mdInfo, c.Key));
            return (await Task.WhenAll(keyTasks).ConfigureAwait(false)).Select(c => c.ToUtfString());
        }

        public async Task<T> GetValueAsync<T>(string key)
        {
            var ret = await GetStringValueAsync(key).ConfigureAwait(false);
            return ret.Item1.Parse<T>();
        }

        public async Task<ulong> GetEntryVersionAsync(string key)
        {
            var keyBytes = key.ToUtfBytes();
            var encryptedKey = await Session.MDataInfoActions.EncryptEntryKeyAsync(_mdInfo, keyBytes).ConfigureAwait(false);
            var mdRef = await Session.MData.GetValueAsync(_mdInfo, encryptedKey).ConfigureAwait(false);
            return mdRef.Item2;
        }

        public async Task<(string, ulong)> GetStringValueAsync(string key)
        {
            var keyBytes = key.ToUtfBytes();
            var encryptedKey = await Session.MDataInfoActions.EncryptEntryKeyAsync(_mdInfo, keyBytes).ConfigureAwait(false);
            var mdRef = await Session.MData.GetValueAsync(_mdInfo, encryptedKey).ConfigureAwait(false);
            var map = await Session.MDataInfoActions.DecryptAsync(_mdInfo, mdRef.Item1).ConfigureAwait(false);

            // get ImD
            var val = await GetImD(map);

            return (val.ToUtfString(), mdRef.Item2);
        }

        public async Task<IEnumerable<T>> GetValuesAsync<T>()
        {
            var entries = new ConcurrentBag<T>();

            var maps = new ConcurrentBag<List<byte>>();

            using (var entriesHandle = await Session.MDataEntries.GetHandleAsync(_mdInfo).ConfigureAwait(false))
            {
                // Fetch and decrypt entries
                var encryptedEntries = await Session.MData.ListEntriesAsync(entriesHandle).ConfigureAwait(false);
                Parallel.ForEach(encryptedEntries, entry =>
                {
                    // protects against deleted entries
                    if (entry.Value.Content.Count != 0)
                    {
                        if (entry.Value.Content.Count < 50) // protects against the zero index 0 or 1 value.
                            return;
                        var decryptedValue = Session.MDataInfoActions.DecryptAsync(_mdInfo, entry.Value.Content).GetAwaiter().GetResult();
                        maps.Add(decryptedValue);
                    }
                });
            }

            // get ImD
            var tasks = maps.Select(c => GetImD(c));
            var data = await Task.WhenAll(tasks);
            Parallel.ForEach(data, c =>
            {
                if (c.ToUtfString().TryParse(out T result))
                    entries.Add(result);
            });

            return entries;
        }

        public Task<IEnumerable<(TKey, TVal)>> GetEntriesAsync<TKey, TVal>(Func<string, TKey> keyParser)
            => GetEntriesAsync<TKey, TVal>(keyParser, k => true);

        public async Task<IEnumerable<(TKey, TVal)>> GetEntriesAsync<TKey, TVal>(Func<string, TKey> keyParser, Func<TKey, bool> selector)
        {
            var tasks = new ConcurrentDictionary<TKey, Task<List<byte>>>();

            using (var entriesHandle = await Session.MDataEntries.GetHandleAsync(_mdInfo).ConfigureAwait(false))
            {
                // Fetch and decrypt entries
                var encryptedEntries = await Session.MData.ListEntriesAsync(entriesHandle).ConfigureAwait(false);
                Parallel.ForEach(encryptedEntries, entry =>
                {
                    // protects against deleted entries // should not be valid operation on append only though
                    if (entry.Value.Content.Count != 0)
                    {
                        var decryptedKey = Session.MDataInfoActions.DecryptAsync(_mdInfo, entry.Key.Key).GetAwaiter().GetResult();
                        var keystring = decryptedKey.ToUtfString();
                        if (keystring == Constants.METADATA_KEY || keystring == Constants.SNAPSHOT_KEY)
                            return;

                        var key = keyParser(keystring);
                        if (!selector(key)) // within range forexample
                            return;

                        var decryptedValue = Session.MDataInfoActions.DecryptAsync(_mdInfo, entry.Value.Content).GetAwaiter().GetResult();
                        tasks.TryAdd(key, GetImD(decryptedValue));
                    }
                });
            }

            var data = await tasks.ToValues();
            var entries = new ConcurrentBag<(TKey, TVal)>();
            Parallel.ForEach(data, c =>
            {
                if (c.Value.ToUtfString().TryParse(out TVal result))
                    entries.Add((c.Key, result));
            });

            return entries;
        }

        public async Task<List<MDataEntry>> GetEntriesAsync()
        {
            var entries = new ConcurrentBag<MDataEntry>();

            using (var entriesHandle = await Session.MDataEntries.GetHandleAsync(_mdInfo).ConfigureAwait(false))
            {
                // Fetch and decrypt entries
                var encryptedEntries = await Session.MData.ListEntriesAsync(entriesHandle).ConfigureAwait(false);
                Parallel.ForEach(encryptedEntries, entry =>
                {
                    if (entry.Value.Content.Count != 0)
                    {
                        var decryptedKey = Session.MDataInfoActions.DecryptAsync(_mdInfo, entry.Key.Key).GetAwaiter().GetResult();
                        var decryptedValue = Session.MDataInfoActions.DecryptAsync(_mdInfo, entry.Value.Content).GetAwaiter().GetResult();
                        entries.Add(new MDataEntry()
                        {
                            Key = new MDataKey() { Key = decryptedKey },
                            Value = new MDataValue { Content = decryptedValue, EntryVersion = entry.Value.EntryVersion }
                        });
                    }
                });
            }

            // get ImD? or hide this from API

            return entries.ToList();
        }

        public async Task AddObjectAsync(string key, object value)
        {
            using (var entryActionsH = await Session.MDataEntryActions.NewAsync().ConfigureAwait(false))
            {
                // insert value
                var insertObj = new Dictionary<string, object>
                {
                    { key, value }
                };
                await InsertEntriesAsync(entryActionsH, insertObj).ConfigureAwait(false);

                // commit
                await CommitEntryMutationAsync(entryActionsH).ConfigureAwait(false);
            }
        }

        public async Task UpdateObjectAsync(string key, object value, ulong version)
        {
            using (var entryActionsH = await Session.MDataEntryActions.NewAsync().ConfigureAwait(false))
            {
                // update value
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
                // delete
                var deleteObj = new Dictionary<string, ulong>
                {
                    { key, version + 1 }
                };
                await DeleteEntriesAsync(entryActionsH, deleteObj).ConfigureAwait(false);

                // commit
                await CommitEntryMutationAsync(entryActionsH).ConfigureAwait(false);
            }
        }

        // Populate the md entry actions handle.
        public async Task InsertEntriesAsync(NativeHandle entryActionsH, Dictionary<string, object> data)
        {
            foreach (var pair in data)
            {
                // store value to ImD
                var map = await StoreImD(pair.Value.Json().ToUtfBytes());

                var encryptedKey = await Session.MDataInfoActions.EncryptEntryKeyAsync(_mdInfo, pair.Key.ToUtfBytes()).ConfigureAwait(false);
                var encryptedValue = await Session.MDataInfoActions.EncryptEntryValueAsync(_mdInfo, map).ConfigureAwait(false);
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
                var map = await StoreImD(val.Json().ToUtfBytes());

                var version = pair.Value.Item2;

                var encryptedKey = await Session.MDataInfoActions.EncryptEntryKeyAsync(_mdInfo, pair.Key.ToUtfBytes()).ConfigureAwait(false);
                var encryptedValue = await Session.MDataInfoActions.EncryptEntryValueAsync(_mdInfo, map).ConfigureAwait(false);

                await Session.MDataEntryActions.UpdateAsync(entryActionsH, encryptedKey, encryptedValue, version).ConfigureAwait(false);
            }
        }

        // Populate the md entry actions handle.
        public async Task DeleteEntriesAsync(NativeHandle entryActionsH, Dictionary<string, ulong> data)
        {
            foreach (var pair in data)
            {
                var version = pair.Value;
                var encryptedKey = await Session.MDataInfoActions.EncryptEntryKeyAsync(_mdInfo, pair.Key.ToUtfBytes()).ConfigureAwait(false);
                await Session.MDataEntryActions.DeleteAsync(entryActionsH, encryptedKey, version).ConfigureAwait(false);
            }
        }

        // Commit the operations in the md entry actions handle.
        public async Task CommitEntryMutationAsync(NativeHandle entryActionsH)
        {
            await Session.MData.MutateEntriesAsync(_mdInfo, entryActionsH).ConfigureAwait(false); // <----------------------------------------------    Commit ------------------------
        }

        // While map size is too large for an MD entry
        // store the map as ImD.
        async Task<List<byte>> StoreImD(List<byte> payload)
        {
            if (payload.Count < 1000)
                return payload;
            var map = await _networkDataOps.StoreImmutableData(payload);
            return await StoreImD(map);
        }

        // While not throwing,
        // the payload is a datamap.
        // NB: Obviously this is not resistant to other errors,
        // so we must catch the specific exception here. (todo)
        async Task<List<byte>> GetImD(List<byte> map)
        {
            try
            {
                var payload = await _networkDataOps.GetImmutableData(map);
                return await GetImD(payload);
            }
            catch
            {
                return map;
            }
        }
    }
}