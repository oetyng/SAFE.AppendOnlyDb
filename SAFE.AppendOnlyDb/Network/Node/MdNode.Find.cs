using SAFE.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Network
{
    internal sealed partial class MdNode
    {
        public bool Contains(ulong version)
        {
            if (Count == 0) return false;
            switch (Type)
            {
                case MdType.Values:
                    return NextVersion > version && version >= StartIndex;
                case MdType.Pointers:
                    return version >= StartIndex && MaxVersionHeld() > version;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Type));
            }
        }

        bool ExistsInNext(ulong version) => version > EndIndex && IsFull;
        bool ExistsInPrevious(ulong version) => StartIndex > version;

        public Task<Result<StoredValue>> FindAsync(ulong version)
        {
            if (Contains(version)) return FindHereAsync(version);
            else if (ExistsInNext(version)) return FindInNextAsync(version);
            else if (ExistsInPrevious(version)) return FindInPreviousAsync(version);
            else return TaskFrom(new KeyNotFound<StoredValue>($"{version}"));
        }

        public async IAsyncEnumerable<(ulong, StoredValue)> ReadToEndAsync(ulong from)
        {
            var end = await GetCount();
            await foreach (var item in FindRangeAsync(from, to: end))
                yield return item;
        }

        public async IAsyncEnumerable<(ulong, StoredValue)> FindRangeAsync(ulong from, ulong to)
        {
            var min = Math.Min(from, to);
            var max = Math.Max(from, to);
            if (0 > min) min = 0;
            if (0 > max) max = 0;

            if (Contains(min) && Contains(max))
                await foreach (var item in FindRangeHereAsync(min, max))
                    yield return item;
            else if (Contains(min))
            {
                if (IsFull)
                {
                    var here = FindRangeHereAsync(min, EndIndex);
                    var next = FindRangeInNextAsync(EndIndex + 1, max);
                    await foreach (var item in here.Concat(next))
                        yield return item;
                }
                else
                    await foreach (var item in FindRangeHereAsync(min, GetNextVersion()))
                        yield return item;
            }
            else if (Contains(max))
            {
                var here = FindRangeHereAsync(StartIndex, max);
                var previous = FindRangeInPreviousAsync(min, StartIndex - 1);
                await foreach (var item in previous.Concat(here))
                    yield return item;
            }
        }

        async IAsyncEnumerable<(ulong, StoredValue)> FindRangeHereAsync(ulong min, ulong max)
        {
            switch (Type)
            {
                case MdType.Values:
                    var entries = _dataOps.GetEntriesAsync<ulong, StoredValue>(
                        k => ulong.Parse(k),
                        k => k >= min && max >= k);

                    await foreach (var item in entries)
                        yield return item;

                    break;

                case MdType.Pointers:
                    var indexMin = GetIndex(min);
                    var indexMax = GetIndex(max);
                    var pointers = _dataOps.GetEntriesAsync<ulong, Pointer>(
                        k => ulong.Parse(k),
                        k => k >= indexMin && indexMax >= k)
                        .Select(c => c.Item2.MdLocator);

                    var items = LocateMany(pointers)
                        .Where(c => c.HasValue)
                        .Select(c => c.Value)
                        .Where(c => !(c.Version is NoVersion)) // might be unnecessary safety measure;
                        .SelectMany(c => c.FindRangeAsync(
                            Math.Max(min, c.StartIndex),
                            Math.Min(max, (ulong)c.Version.Value)));

                    await foreach (var item in items)
                        yield return item;

                    break;

                default:
                    break;
            }
        }

        async IAsyncEnumerable<Result<IMdNode>> LocateMany(IAsyncEnumerable<MdLocator> locators)
        {
            await foreach (var locator in locators)
                yield return await _dataOps.NodeFactory.LocateAsync(locator);
        }

        async IAsyncEnumerable<(ulong, StoredValue)> FindRangeInNextAsync(ulong min, ulong max)
        {
            if (Next != null)
            {
                var targetResult = await _dataOps.NodeFactory.LocateAsync(Next)
                    .ConfigureAwait(false);
                var range = targetResult.Value.FindRangeAsync(min, max).ConfigureAwait(false);
                await foreach (var item in range)
                    yield return item;
            }
        }

        async IAsyncEnumerable<(ulong, StoredValue)> FindRangeInPreviousAsync(ulong min, ulong max)
        {
            var targetResult = await _dataOps.NodeFactory.LocateAsync(Previous)
                .ConfigureAwait(false);
            var range = targetResult.Value.FindRangeAsync(min, max).ConfigureAwait(false);
            await foreach (var item in range)
                yield return item;
        }

        async Task<Result<StoredValue>> FindHereAsync(ulong version)
        {
            switch (Type)
            {
                case MdType.Values:
                    return await GetValueAsync(version).ConfigureAwait(false);
                case MdType.Pointers:
                    var index = GetIndex(version);
                    var pointer = await GetPointerAsync(index.ToString()).ConfigureAwait(false);
                    if (!pointer.HasValue)
                        return pointer.CastError<Pointer, StoredValue>();

                    var targetResult = await _dataOps.NodeFactory.LocateAsync(pointer.Value.MdLocator)
                        .ConfigureAwait(false);
                    return await targetResult.Value.FindAsync(version).ConfigureAwait(false);
                default:
                    return new ArgumentOutOfRange<StoredValue>(nameof(Type));
            }
        }

        async Task<Result<StoredValue>> FindInNextAsync(ulong version)
        {
            var targetResult = await _dataOps.NodeFactory.LocateAsync(Next)
                .ConfigureAwait(false);
            return await targetResult.Value.FindAsync(version).ConfigureAwait(false);
        }

        async Task<Result<StoredValue>> FindInPreviousAsync(ulong version)
        {
            var targetResult = await _dataOps.NodeFactory.LocateAsync(Previous)
                .ConfigureAwait(false);
            return await targetResult.Value.FindAsync(version).ConfigureAwait(false);
        }

        Task<Result<T>> TaskFrom<T>(Result<T> res) => Task.FromResult(res);
        ulong GetNextVersion() => Type == MdType.Pointers ? MaxVersionHeld() : NextVersion - 1;
        ulong GetIndex(ulong version) => (ulong)Math.Truncate(version / Math.Pow(Capacity, Level));

        // the node with this EndIndex exists, but we don't know at what version it is
        ulong MaxVersionHeld() => Type == MdType.Pointers ? (ulong)(Count * Math.Pow(Capacity, Level)) - 1 : NextVersion - 1;
    }
}