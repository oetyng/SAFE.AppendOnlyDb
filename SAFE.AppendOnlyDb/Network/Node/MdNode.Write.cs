using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SAFE.Data;
using SafeApp.Utilities;

namespace SAFE.AppendOnlyDb.Network
{
    internal sealed partial class MdNode
    {
        // NOT THREAD SAFE
        // Adds if not exists
        // It will return the direct pointer to the stored value
        // which makes it readily available for indexing at higher levels.
        public Task<Result<Pointer>> AppendAsync(StoredValue value)
            => TryAppendAsync(value, ExpectedVersion.Any);

        // NOT THREAD SAFE
        public async Task<Result<Pointer>> TryAppendAsync(StoredValue value, ExpectedVersion expectedVersion)
        {
            if (IsFull)
                return new MdOutOfEntriesError<Pointer>($"Filled: {Count}/{Constants.MdCapacity}");
            
            try
            {
                switch (Type)
                {
                    case MdType.Pointers:
                        if (Count == 0) // i.e. we must get last MdNode held by Previous for this node. (i.e. last node, one level down, of previous node on this level)
                        {
                            IMdNode previous = default;
                            if (Previous != null) // (if null Previous, this would be the very first, still empty, MdNode in the tree)
                            {
                                var prevNodeForThis = await LocateAsync(Previous, _dataOps.Session)
                                    .ConfigureAwait(false);
                                if (!prevNodeForThis.HasValue)
                                    return Result.Fail<Pointer>(prevNodeForThis.ErrorCode.Value, prevNodeForThis.ErrorMsg);
                                switch(prevNodeForThis.Value.Type)
                                {
                                    case MdType.Pointers:
                                        var prevNodePointerForValue = await (prevNodeForThis.Value as MdNode).GetLastPointer();
                                        if (!prevNodePointerForValue.HasValue)
                                            return Result.Fail<Pointer>(prevNodePointerForValue.ErrorCode.Value, prevNodePointerForValue.ErrorMsg);
                                        var prevNodeForValue = await LocateAsync(prevNodePointerForValue.Value.MdLocator, _dataOps.Session)
                                            .ConfigureAwait(false);
                                        if (!prevNodeForValue.HasValue)
                                            return Result.Fail<Pointer>(prevNodeForValue.ErrorCode.Value, prevNodeForValue.ErrorMsg);
                                        previous = prevNodeForValue.Value;
                                        break;
                                    case MdType.Values: // Previous of a Pointer type cannot be of Value type.
                                    default:
                                        return new ArgumentOutOfRange<Pointer>("prevNodeForThis.Value.Type");
                                }
                            }
                            return await ExpandLevelAsync(value, expectedVersion, previous).ConfigureAwait(false);
                        }

                        var pointer = await GetLastPointer().ConfigureAwait(false);
                        if (!pointer.HasValue)
                            return pointer;

                        var targetResult = await LocateAsync(pointer.Value.MdLocator, _dataOps.Session)
                            .ConfigureAwait(false);
                        if (!targetResult.HasValue)
                            return Result.Fail<Pointer>(targetResult.ErrorCode.Value, targetResult.ErrorMsg);
                        var target = targetResult.Value;
                        if (target.IsFull)
                            return await ExpandLevelAsync(value, expectedVersion, previous: target).ConfigureAwait(false);

                        return await target.TryAppendAsync(value, expectedVersion).ConfigureAwait(false);
                    case MdType.Values:
                        // optimistic concurrency
                        var versionRes = ValidateVersion(expectedVersion);
                        if (!versionRes.HasValue) return new VersionMismatch<Pointer>(versionRes.ErrorMsg);

                        var key = $"{NextVersion}";
                        await AddObjectAsync(key, value).ConfigureAwait(false);

                        return Result.OK(new Pointer // return pointer, to be used for indexing
                        {
                            MdLocator = MdLocator,
                            MdKey = key,
                            ValueType = value.ValueType
                        });
                    default:
                        return new ArgumentOutOfRange<Pointer>(nameof(Type));
                }
            }
            catch (FfiException ex)
            {
                // will throw -108 if entry limit exceeded
                if (ex.ErrorCode == -107)
                    return new ValueAlreadyExists<Pointer>(ex.Message);
                else
                    throw;
            }
        }

        public async Task<Result<Pointer>> AddAsync(Pointer pointer)
        {
            if (IsFull)
                return new MdOutOfEntriesError<Pointer>($"Filled: {Count}/{Constants.MdCapacity}");
            if (Type == MdType.Values)
                return new InvalidOperation<Pointer>("Pointers can only be added in Pointer type Mds (i.e. Level > 0).");
            var index = (Count).ToString();
            pointer.MdKey = index;
            await AddObjectAsync(index, pointer).ConfigureAwait(false);
            return Result.OK(pointer);
        }

        /// <summary>
        /// Stores a snapshot of all entries.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="leftFold">The aggregating function</param>
        /// <returns>true if snapshot was stored, false if already existed</returns>
        public async Task<Result<bool>> Snapshot<T>(Func<IAsyncEnumerable<T>, byte[]> leftFold)
        {
            if (!IsFull)
                return new InvalidOperation<bool>("Cannot snapshot unless Md is full!");
            if (await _dataOps.ContainsKeyAsync(Constants.SNAPSHOT_KEY))
                return Result.OK(false); // already snapshotted, i.e. OK with changed=false

            var entries = _dataOps.GetEntriesAsync<ulong, StoredValue>((k) => ulong.Parse(k));

            var ordered = entries
                .OrderBy(c => c.Item1)
                .Select(c => c.Item2.Parse<T>());

            var snapshot = leftFold(ordered);

            await _dataOps.AddObjectAsync(Constants.SNAPSHOT_KEY, snapshot);

            return Result.OK(true); // OK with changed=true
        }

        public async Task<Result<bool>> SetNext(IMdNode node)
        {
            if (Next != null)
            {
                if (Next.XORName.SequenceEqual(node.MdLocator.XORName))
                    return Result.OK(false); // no change
                return new InvalidOperation<bool>($"Cannot change Next. Current: {Next.XORName}");
            }
            if (!IsFull)
                return new InvalidOperation<bool>($"Cannot set Next until node is full (Current count: {Count} of capacity {Constants.MdCapacity}.");

            var metadata = new MdMetadata
            {
                Level = this.Level,
                Previous = this.Previous,
                StartIndex = this.StartIndex,
                Next = node.MdLocator
            };

            var version = await _dataOps.GetEntryVersionAsync(Constants.METADATA_KEY).ConfigureAwait(false);
            await _dataOps.UpdateObjectAsync(Constants.METADATA_KEY, metadata, version).ConfigureAwait(false);
            _metadata = metadata;
            return Result.OK(true);
        }

        // ------------------------------------------------------------------------------------------------------------
        // ------------------------------ PRIVATE ------------------------------
        // ------------------------------------------------------------------------------------------------------------

        Result<ExpectedVersion> ValidateVersion(ExpectedVersion expectedVersion)
        {
            switch (expectedVersion)
            {
                case AnyVersion _:
                    break;
                case SpecificVersion specific 
                    when specific != this.Version:
                    return new VersionMismatch<ExpectedVersion>();
                case NoVersion noversion 
                    when noversion != this.Version:
                    return new VersionMismatch<ExpectedVersion>();
                case SpecificVersion specific
                    when specific == this.Version:
                    break;
                case NoVersion noversion
                    when noversion == this.Version:
                    break;
                case null:
                default:
                    throw new ArgumentOutOfRangeException(nameof(expectedVersion));
            }
            return Result.OK(expectedVersion);
        }

        async Task AddObjectAsync(string key, object value)
        {
            await _dataOps.AddObjectAsync(key, value).ConfigureAwait(false);
            Interlocked.Increment(ref _count);
        }

        async Task GetOrAddMetadata(MdMetadata metadata = null)
        {
            var keyCount = await _dataOps.GetKeyCountAsync().ConfigureAwait(false);
            if (keyCount > 0)
            {
                _count = keyCount == 1000 ? keyCount - 2 : keyCount - 1;
                _metadata = await _dataOps.GetValueAsync<MdMetadata>(Constants.METADATA_KEY).ConfigureAwait(false);
                return;
            }

            metadata ??= new MdMetadata();
            await _dataOps.AddObjectAsync(Constants.METADATA_KEY, metadata).ConfigureAwait(false);

            _metadata = metadata;
        }

        Task<IMdNode> CreateNewMdNode(MdMetadata meta)
            => CreateNewMdNodeAsync(meta, _dataOps.Session, DataProtocol.DEFAULT_PROTOCOL);

        async Task<Result<Pointer>> ExpandLevelAsync(StoredValue value, ExpectedVersion expectedVersion, IMdNode previous)
        {
            if (Level == 0)
                return new ArgumentOutOfRange<Pointer>(nameof(Level));

            var level = this.Level - 1;

            var meta = new MdMetadata
            {
                Level = level,
                Previous = previous?.MdLocator,
                StartIndex = previous?.EndIndex + 1 ?? 0
            };

            var md = await CreateNewMdNode(meta).ConfigureAwait(false);
            var leafPointer = await md.TryAppendAsync(value, expectedVersion).ConfigureAwait(false);
            if (!leafPointer.HasValue)
                return leafPointer;

            switch (md.Type)
            {
                case MdType.Pointers: // i.e. we have still not reached the end of the tree
                    await AddAsync(new Pointer
                    {
                        MdLocator = md.MdLocator,
                        ValueType = typeof(Pointer).Name
                    }).ConfigureAwait(false);
                    break;
                case MdType.Values: // i.e. we are now right above leaf level
                    await AddAsync(new Pointer
                    {
                        MdLocator = leafPointer.Value.MdLocator,
                        ValueType = typeof(Pointer).Name
                    }).ConfigureAwait(false);
                    break;
                default:
                    return new ArgumentOutOfRange<Pointer>(nameof(md.Type));
            }

            return leafPointer;
        }
    }
}