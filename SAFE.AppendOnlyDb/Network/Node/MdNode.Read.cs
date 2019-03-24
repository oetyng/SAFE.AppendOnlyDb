using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SAFE.AppendOnlyDb.Utils;
using SAFE.Data;
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
            catch (FfiException ex) when (ex.ErrorCode == -106) { return new KeyNotFound<StoredValue>($"Key: {version}."); }
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
                            return valueResult.CastError<StoredValue, (Pointer, StoredValue)>();
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

        public async IAsyncEnumerable<StoredValue> GetAllValuesAsync()
        {
            switch (Type)
            {
                case MdType.Values:
                    var values = _dataOps.GetEntriesAsync<ulong, StoredValue>(c => ulong.Parse(c));
                    await foreach (var item in values.Select(c => c.Item2))
                        yield return item;
                    break;
                case MdType.Pointers:
                    var pointerValues = LocateMany(_dataOps.GetEntriesAsync<ulong, Pointer>(c => ulong.Parse(c))
                        .Select(c => c.Item2.MdLocator))
                        .SelectMany(c => c.Value.GetAllValuesAsync());
                    await foreach (var item in pointerValues)
                        yield return item;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Type));
            }
        }

        public async IAsyncEnumerable<(Pointer, StoredValue)> GetAllPointerValuesAsync()
        {
            switch (Type)
            {
                case MdType.Pointers:
                    var pointers = LocateMany(GetAllPointersAsync().Select(c => c.MdLocator));
                    var allPointers = pointers.SelectMany(c => c.Value.GetAllPointerValuesAsync()).ConfigureAwait(false);
                    await foreach (var item in allPointers)
                        yield return item;
                    break;
                case MdType.Values:
                    var keys = EnumerableExt.LongRange(StartIndex, (ulong)Count);
                    foreach (var key in keys)
                    {
                        var val = await GetValueAsync(key).ConfigureAwait(false);
                        if (!val.HasValue)
                            continue;
                        yield return (new Pointer
                        {
                            MdLocator = MdLocator,
                            MdKey = $"{key}",
                            ValueType = val.Value.ValueType
                        }, val.Value);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Type));
            }
        }

        public async Task<Result<StoredValue>> GetLastVersionAsync()
        {
            var lastNode = await GetLastNode();
            if (!lastNode.HasValue)
                return lastNode.CastError<IMdNode, StoredValue>();
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
            if (lastNode.Value.Version is NoVersion)
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
                        var nextResult = await _dataOps.NodeFactory.LocateAsync(Next)
                            .ConfigureAwait(false);
                        return await (nextResult.Value as MdNode).GetLastNode();
                    }
                case MdType.Pointers:
                    var pointer = await GetLastPointer().ConfigureAwait(false);
                    if (!pointer.HasValue)
                        return pointer.CastError<Pointer, IMdNode>();

                    var targetResult = await _dataOps.NodeFactory.LocateAsync(pointer.Value.MdLocator)
                        .ConfigureAwait(false);
                    return await (targetResult.Value as MdNode).GetLastNode();
                default:
                    return new ArgumentOutOfRange<IMdNode>(nameof(Type));
            }
        }

        Task<Result<Pointer>> GetLastPointer() 
            => GetPointerAsync((Count - 1).ToString());

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
            catch (FfiException ex) when (ex.ErrorCode == -106) { return new KeyNotFound<Pointer>($"Key: {key}."); }
        }

        async IAsyncEnumerable<Pointer> GetAllPointersAsync()
        {
            switch (Type)
            {
                case MdType.Pointers:
                    var pointers = _dataOps.GetValuesAsync<Pointer>().ConfigureAwait(false);
                    await foreach (var pointer in pointers)
                        yield return pointer;
                    break;
                case MdType.Values:
                    throw new InvalidOperationException("Pointers can only be fetched in Pointer type Mds (i.e. Level > 0).");
                default:
                    throw new ArgumentOutOfRangeException(nameof(Type));
            }
        }
    }
}