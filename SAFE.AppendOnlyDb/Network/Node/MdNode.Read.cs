using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SAFE.AppendOnlyDb.Utils;
using SAFE.Data;
using SafeApp;
using SafeApp.Utilities;

namespace SAFE.AppendOnlyDb.Network
{
    internal sealed partial class MdNode
    {
        public async Task<Result<StoredValue>> GetValueAsync(ulong version)
        {
            try
            {
                switch (Type)
                {
                    case MdType.Pointers:
                        return new InvalidOperation<StoredValue>($"There are no values in pointers. Method must be called on a ValuePointer (i.e. Md with Level = 0). Version {version}.");
                    case MdType.Values:
                        var valueRes = await _dataOps.GetStringValueAsync($"{version}").ConfigureAwait(false);
                        var json = valueRes.Item1;
                        if (!json.TryParse(out StoredValue item)) // beware of this, the type parsed must have proper property validations for this to work (Like [JsonRequired])
                            return new DeserializationError<StoredValue>();
                        return Result.OK(item);
                    default:
                        return new ArgumentOutOfRange<StoredValue>(nameof(Type));
                }
            }
            catch (FfiException ex)
            {
                if (ex.ErrorCode != -106)
                    throw;
                return new KeyNotFound<StoredValue>($"Key: {version}.");
            }
        }

        public async Task<Result<(Pointer, StoredValue)>> GetPointerAndValueAsync(ulong version)
        {
            switch (Type)
            {
                case MdType.Pointers:
                    return new InvalidOperation<(Pointer, StoredValue)>($"There are no values in pointers. Method must be called on a ValuePointer (i.e. Md with Level = 0). Key {version}.");
                case MdType.Values:
                    if (Contains(version))
                    {
                        var valueResult = await GetValueAsync(version).ConfigureAwait(false);
                        if (!valueResult.HasValue)
                            return Result.Fail<(Pointer, StoredValue)>(valueResult.ErrorCode.Value, valueResult.ErrorMsg);
                        var value = valueResult.Value;
                        return Result.OK((new Pointer
                        {
                            MdLocator = MdLocator,
                            MdKey = $"{version}",
                            ValueType = value.ValueType
                        }, value));
                    }
                    else
                        return new KeyNotFound<(Pointer, StoredValue)>($"Key: {version}");
                default:
                    return new ArgumentOutOfRange<(Pointer, StoredValue)>(nameof(Type));
            }
        }

        public async Task<IEnumerable<StoredValue>> GetAllValuesAsync()
        {
            try
            {
                switch (Type)
                {
                    case MdType.Values:
                        var values = await _dataOps.GetEntriesAsync<ulong, StoredValue>(c => ulong.Parse(c));
                        return values.Select(c => c.Item2);
                    case MdType.Pointers:
                        var pointers = await _dataOps.GetEntriesAsync<ulong, Pointer>(c => ulong.Parse(c));
                        var pointerTasks = pointers // from pointers get regs to mds
                            .Select(c => c.Item2)
                            .Select(c => LocateAsync(c.MdLocator, _dataOps.Session));
                        var pointerValues = await Task.WhenAll(pointerTasks).ConfigureAwait(false);
                        var valueTasks = pointerValues
                           .Select(c => c.Value.GetAllValuesAsync());
                        var fetchedValues = (await Task.WhenAll(valueTasks).ConfigureAwait(false))
                            .SelectMany(c => c);
                        var valueBag = new ConcurrentBag<StoredValue>();
                        Parallel.ForEach(fetchedValues, val => valueBag.Add(val));
                        return valueBag;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(Type));
                }
            }
            catch
            {
                // (FfiException ex)
                // if (ex.ErrorCode != -106) // does not make sense to check for key not found error here
                //    throw;
                throw;
            }
        }

        public async Task<IEnumerable<(Pointer, StoredValue)>> GetAllPointerValuesAsync()
        {
            switch (Type)
            {
                case MdType.Pointers:
                    var pointerTasks = (await GetAllPointersAsync().ConfigureAwait(false))
                        .Select(c => LocateAsync(c.MdLocator, _dataOps.Session));
                    var pointerValuesTasks = (await Task.WhenAll(pointerTasks).ConfigureAwait(false))
                        .Select(c => c.Value.GetAllPointerValuesAsync());
                    return (await Task.WhenAll(pointerValuesTasks).ConfigureAwait(false))
                        .SelectMany(c => c);
                case MdType.Values:

                    // return (await GetAllValuesAsync())
                    //    .Where(c => c.ValueType != typeof(MdMetadata).Name)
                    //    .Select(c => (new Pointer
                    //    {
                    //        XORAddress = this.XORAddress,
                    //        MdKey = c.Key, // We do not have the key here, unfortunately..
                    //        ValueType = c.ValueType
                    //    }, c));

                    var keys = EnumerableExt.LongRange(StartIndex, (ulong)Count);
                    var pairs = new ConcurrentDictionary<ulong, StoredValue>();
                    var valueTasks = keys.Select(async c =>
                    {
                        var val = await GetValueAsync(c).ConfigureAwait(false);
                        if (val.HasValue)
                            pairs[c] = val.Value;
                    });
                    await Task.WhenAll(valueTasks).ConfigureAwait(false);

                    return pairs
                        .Where(c => c.Value.ValueType != typeof(MdMetadata).Name)
                        .Select(c => (new Pointer
                        {
                            MdLocator = MdLocator,
                            MdKey = $"{c.Key}",
                            ValueType = c.Value.ValueType
                        }, c.Value));
                default:
                    throw new ArgumentOutOfRangeException(nameof(Type));
            }
        }

        public async Task<Result<StoredValue>> GetLastVersionAsync()
        {
            var lastNode = await GetLastNode();
            if (!lastNode.HasValue)
                return Result.Fail<StoredValue>((int)lastNode.ErrorCode, lastNode.ErrorMsg);
            if (lastNode.Value.Version is NoVersion && lastNode.Value.StartIndex == 0)
                return new DataNotFound<StoredValue>("There is no data in the tree.");
            if (lastNode.Value.Count == 0)
                return new InvalidOperation<StoredValue>("hmmmm, means the last version is in previous. But this should not happen, since if previous had a Next link, it was because it needed to add something to Next..");
            return await lastNode.Value.GetValueAsync((ulong)lastNode.Value.Version.Value);
        }

        public async Task<ulong> GetCount()
        {
            var lastNode = await GetLastNode();
            if (!lastNode.HasValue)
                return 0;
            return (ulong)lastNode.Value.Version.Value;
        }

        // ------------------------------------------------------------------------------------------------------------
        // ------------------------------ PRIVATE ------------------------------
        // ------------------------------------------------------------------------------------------------------------

        async Task<Result<IMdNode>> GetLastNode()
        {
            switch (Type)
            {
                case MdType.Values:
                    if (!IsFull || Next == null)
                        return Result.OK((IMdNode)this);
                    else
                    {
                        var nextResult = await LocateAsync(Next, _dataOps.Session)
                            .ConfigureAwait(false);
                        return await (nextResult.Value as MdNode).GetLastNode();
                    }
                case MdType.Pointers:
                    var pointer = await GetLastPointer().ConfigureAwait(false);
                    if (!pointer.HasValue)
                        return Result.Fail<IMdNode>(pointer.ErrorCode.Value, pointer.ErrorMsg);

                    var targetResult = await LocateAsync(pointer.Value.MdLocator, _dataOps.Session)
                        .ConfigureAwait(false);
                    return await (targetResult.Value as MdNode).GetLastNode();
                default:
                    return new ArgumentOutOfRange<IMdNode>(nameof(Type));
            }
        }

        Task<Result<Pointer>> GetLastPointer()
        {
            var pointer = GetPointerAsync((Count - 1).ToString());
            return pointer;
        }

        async Task<Result<Pointer>> GetPointerAsync(string key)
        {
            try
            {
                switch (Type)
                {
                    case MdType.Pointers:
                        var valueRes = await _dataOps.GetStringValueAsync(key).ConfigureAwait(false);
                        var json = valueRes.Item1;
                        if (!json.TryParse(out Pointer item)) // beware of this, the type parsed must have proper property validations for this to work (Like [JsonRequired])
                            return new DeserializationError<Pointer>();
                        return Result.OK(item);
                    case MdType.Values:
                        return new InvalidOperation<Pointer>($"There are no pointers in value mds. Method must be called on a Pointer (i.e. Md with Level > 0). Key {key}.");
                    default:
                        return new ArgumentOutOfRange<Pointer>(nameof(Type));
                }
            }
            catch (FfiException ex)
            {
                if (ex.ErrorCode != -106)
                    throw;
                return new KeyNotFound<Pointer>($"Key: {key}.");
            }
        }

        // Added for conversion
        async Task<IEnumerable<Pointer>> GetAllPointersAsync()
        {
            try
            {
                switch (Type)
                {
                    case MdType.Pointers:
                        return await _dataOps.GetValuesAsync<Pointer>().ConfigureAwait(false);
                    case MdType.Values:
                        throw new InvalidOperationException("Pointers can only be fetched in Pointer type Mds (i.e. Level > 0).");
                    default:
                        throw new ArgumentOutOfRangeException(nameof(Type));
                }
            }
            catch
            {
                // (FfiException ex)
                // if (ex.ErrorCode != -106) // does not make sense to check for key not found error here
                //    throw;
                throw;
            }
        }
    }
}