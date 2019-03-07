using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SAFE.AppendOnlyDb.Utils;
using SAFE.Data;
using SafeApp.Utilities;

namespace SAFE.AppendOnlyDb.Network
{
    internal sealed partial class MdNode
    {
        readonly AsyncDuplicateLock _writeLock = new AsyncDuplicateLock();

        // Adds if not exists
        // It will return the direct pointer to the stored value
        // which makes it readily available for indexing at higher levels.
        public Task<Result<Pointer>> AppendAsync(StoredValue value)
            => TryAppendAsync(value, ExpectedVersion.Any);

        public async Task<Result<Pointer>> TryAppendAsync(StoredValue value, ExpectedVersion expectedVersion)
        {
            using (var synch = await _writeLock.LockAsync(_uniqueId))
            {
                if (IsFull) return new MdOutOfEntriesError<Pointer>($"Filled: {Count}/{Constants.MdCapacity}");

                switch (Type)
                {
                    case MdType.Values:
                        var versionRes = ValidateVersion(expectedVersion);
                        if (!versionRes.HasValue) // optimistic concurrency
                            return new VersionMismatch<Pointer>(versionRes.ErrorMsg);
                        return await AddObjectAsync($"{NextVersion}", value).ConfigureAwait(false);

                    case MdType.Pointers:
                        if (Count > 0)
                        {
                            var pointer = await GetLastPointer().ConfigureAwait(false);
                            if (!pointer.HasValue) return pointer;

                            var targetResult = await LocateAsync(pointer.Value.MdLocator, _dataOps.Session).ConfigureAwait(false);
                            if (!targetResult.HasValue)
                                return Result.Fail<Pointer>(targetResult.ErrorCode.Value, targetResult.ErrorMsg);
                            var target = targetResult.Value;
                            if (target.IsFull)
                                return await ExpandLevelAsync(value, expectedVersion, previous: target).ConfigureAwait(false);

                            return await target.TryAppendAsync(value, expectedVersion).ConfigureAwait(false);
                        }

                        // Count == 0, i.e. we must get last MdNode held by Previous for this node. 
                        // (i.e. last node, one level down, of previous node on this level)
                        if (Previous == null)  // (if null Previous, this would be the very first, still empty, MdNode in the tree)
                            return await ExpandLevelAsync(value, expectedVersion, previous: default).ConfigureAwait(false);

                        var prevNode = await LocateAsync(Previous, _dataOps.Session).ConfigureAwait(false);
                        if (!prevNode.HasValue)
                            return Result.Fail<Pointer>(prevNode.ErrorCode.Value, prevNode.ErrorMsg);

                        var lastPointerOfPrevNode = await (prevNode.Value as MdNode).GetLastPointer();
                        if (!lastPointerOfPrevNode.HasValue)
                            return lastPointerOfPrevNode;

                        var prevNodeForNewNode = await LocateAsync(lastPointerOfPrevNode.Value.MdLocator, _dataOps.Session).ConfigureAwait(false);
                        if (!prevNodeForNewNode.HasValue)
                            return Result.Fail<Pointer>(prevNodeForNewNode.ErrorCode.Value, prevNodeForNewNode.ErrorMsg);

                        return await ExpandLevelAsync(value, expectedVersion, previous: prevNodeForNewNode.Value).ConfigureAwait(false);
                    default:
                        return new ArgumentOutOfRange<Pointer>(nameof(Type));
                }
            }
        }

        public async Task<Result<Pointer>> AddAsync(Pointer pointer)
        {
            // Since this is nested under lock of TryAppend, 
            // we must distinguish it by appending "Pointer" to lock key.
            // (That is OK, since Pointer and Value cannot conflict, never added in same instance.)
            // If this had not been publicly exposed, we would not have needed the lock.
            using (var synch = await _writeLock.LockAsync(_uniqueId + "Pointer"))
            {
                if (IsFull)
                    return new MdOutOfEntriesError<Pointer>($"Filled: {Count}/{Constants.MdCapacity}");
                if (Type == MdType.Values)
                    return new InvalidOperation<Pointer>("Pointers can only be added in Pointer type Mds (i.e. Level > 0).");
                var index = Count.ToString();
                pointer.MdKey = index;
                return await AddObjectAsync(index, pointer).ConfigureAwait(false);
            }
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
            else if (!IsFull)
                return new InvalidOperation<bool>($"Cannot set Next until node is full (Current count: {Count} of capacity {Constants.MdCapacity}.");

            var metadata = new MdMetadata
            {
                Level = this.Level,
                Previous = this.Previous,
                StartIndex = this.StartIndex,
                Next = node.MdLocator
            };
            // this is not 100% AD
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
                case NoVersion noversion when noversion == this.Version:
                case SpecificVersion specific when specific == this.Version:
                    return Result.OK(expectedVersion);

                case NoVersion noversion when noversion != this.Version:
                case SpecificVersion specific when specific != this.Version:
                    return new VersionMismatch<ExpectedVersion>();

                case null:
                default:
                    throw new ArgumentOutOfRangeException(nameof(expectedVersion));
            }
        }

        async Task<Result<Pointer>> AddObjectAsync(string key, Pointer value)
        {
            try
            {
                await _dataOps.AddObjectAsync(key, value).ConfigureAwait(false);
                Interlocked.Increment(ref _count);

                return Result.OK(value);
            }
            catch (FfiException ex) when (ex.ErrorCode == -107) { return new ValueAlreadyExists<Pointer>(ex.Message); }
            catch (FfiException ex) when (ex.ErrorCode == -108) { return new MdOutOfEntriesError<Pointer>($"Filled: {Count}/{Constants.MdCapacity}"); } // entry limit exceeded
            // todo: handle transient error
        }

        async Task<Result<Pointer>> AddObjectAsync(string key, StoredValue value)
        {
            try
            {
                await _dataOps.AddObjectAsync(key, value).ConfigureAwait(false);
                Interlocked.Increment(ref _count);

                return Result.OK(new Pointer // return pointer, to be used for indexing
                {
                    MdLocator = MdLocator,
                    MdKey = key,
                    ValueType = value.ValueType
                });
            }
            catch (FfiException ex) when (ex.ErrorCode == -107) { return new ValueAlreadyExists<Pointer>(ex.Message); }
            catch (FfiException ex) when (ex.ErrorCode == -108) { return new MdOutOfEntriesError<Pointer>($"Filled: {Count}/{Constants.MdCapacity}"); } // entry limit exceeded
            // todo: handle transient error
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

            var meta = new MdMetadata
            {
                Level = this.Level - 1,
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