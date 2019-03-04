using SAFE.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Network
{
    internal sealed partial class MdNode
    {
        public bool Contains(ulong version) => NextVersion > version && version >= StartIndex;

        bool ExistsInNext(ulong version) => version > EndIndex && IsFull;
        bool ExistsInPrevious(ulong version) => StartIndex > version;

        public Task<Result<StoredValue>> FindAsync(ulong version)
        {
            if (Contains(version))
                return FindHereAsync(version);
            else if (ExistsInNext(version))
                return FindInNextAsync(version);
            else if (ExistsInPrevious(version))
                return FindInPreviousAsync(version);
            else
                return TaskFrom(new KeyNotFound<StoredValue>($"{version}"));
        }

        public async Task<IEnumerable<(ulong, StoredValue)>> FindRangeAsync(ulong from, ulong to)
        {
            var min = Math.Min(from, to);
            var max = Math.Max(from, to);
            if (0 > min) min = 0;
            if (0 > max) max = 0;

            if (Contains(min) && Contains(max))
                return await FindRangeHereAsync(min, max);
            else if (Contains(min))
            {
                if (IsFull)
                {
                    var here = await FindRangeHereAsync(min, EndIndex);
                    var next = await FindRangeInNextAsync(EndIndex + 1, max);
                    return here.Concat(next);
                }
                else
                    return await FindRangeHereAsync(min, NextVersion - 1);
            }
            else if (Contains(max))
            {
                var here = await FindRangeHereAsync(StartIndex, max);
                var previous = await FindRangeInPreviousAsync(min, StartIndex - 1);
                return here.Concat(previous);
            }
            else
                return new List<(ulong, StoredValue)>();
        }

        async Task<IEnumerable<(ulong, StoredValue)>> FindRangeHereAsync(ulong min, ulong max)
        {
            switch (Type)
            {
                case MdType.Values:
                    return await _dataOps.GetEntriesAsync<ulong, StoredValue>(
                        k => ulong.Parse(k),
                        k => k >= min && max >= k);
                case MdType.Pointers:
                    var indexMin = (ulong)Math.Truncate(min / Math.Pow(Constants.MdCapacity, Level));
                    var indexMax = (ulong)Math.Truncate(max / Math.Pow(Constants.MdCapacity, Level));
                    var pointers = await _dataOps.GetEntriesAsync<ulong, Pointer>(
                        k => ulong.Parse(k),
                        k => k >= indexMin && indexMax >= k);

                    var targetTasks = pointers.Select(c => LocateAsync(c.Item2.MdLocator, _dataOps.Session));
                    var targets = await Task.WhenAll(targetTasks);
                    var concurrent = targets
                        .Where(c => c.HasValue)
                        .Select(c => c.Value)
                        .Where(c => !(c.Version is NoVersion)) // might be unnecesary safety measure
                        .Select(c => c.FindRangeAsync(
                            Math.Max(indexMin, c.StartIndex), 
                            Math.Min(indexMax, (ulong)c.Version.Value)));
                    var all = await Task.WhenAll(concurrent);
                    return all.SelectMany(c => c);
                default:
                    return new List<(ulong, StoredValue)>();
            }
        }

        async Task<IEnumerable<(ulong, StoredValue)>> FindRangeInNextAsync(ulong min, ulong max)
        {
            var targetResult = await LocateAsync(Next, _dataOps.Session)
                .ConfigureAwait(false);
            return await targetResult.Value.FindRangeAsync(min, max).ConfigureAwait(false);
        }

        async Task<IEnumerable<(ulong, StoredValue)>> FindRangeInPreviousAsync(ulong min, ulong max)
        {
            var targetResult = await LocateAsync(Previous, _dataOps.Session)
                .ConfigureAwait(false);
            return await targetResult.Value.FindRangeAsync(min, max).ConfigureAwait(false);
        }

        async Task<Result<StoredValue>> FindHereAsync(ulong version)
        {
            switch (Type)
            {
                case MdType.Values:
                    return await GetValueAsync(version).ConfigureAwait(false);
                case MdType.Pointers:
                    var index = (ulong)Math.Truncate(version / Math.Pow(Constants.MdCapacity, Level));

                    var pointer = await GetPointerAsync(index.ToString()).ConfigureAwait(false);
                    if (!pointer.HasValue)
                        return Result.Fail<StoredValue>(pointer.ErrorCode.Value, pointer.ErrorMsg);

                    var targetResult = await LocateAsync(pointer.Value.MdLocator, _dataOps.Session)
                        .ConfigureAwait(false);
                    return await targetResult.Value.FindAsync(version).ConfigureAwait(false);
                default:
                    return new ArgumentOutOfRange<StoredValue>(nameof(Type));
            }
        }

        async Task<Result<StoredValue>> FindInNextAsync(ulong version)
        {
            var targetResult = await LocateAsync(Next, _dataOps.Session)
                .ConfigureAwait(false);
            return await targetResult.Value.FindAsync(version).ConfigureAwait(false);
        }

        async Task<Result<StoredValue>> FindInPreviousAsync(ulong version)
        {
            var targetResult = await LocateAsync(Previous, _dataOps.Session)
                .ConfigureAwait(false);
            return await targetResult.Value.FindAsync(version).ConfigureAwait(false);
        }

        Task<Result<T>> TaskFrom<T>(Result<T> res) => Task.FromResult(res);
    }
}