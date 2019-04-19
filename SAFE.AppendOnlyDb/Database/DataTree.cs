using SAFE.Data;
using SAFE.Data.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    internal class DataTree : IStreamAD, IValueAD
    {
        readonly AsyncDuplicateLock _writeLock = new AsyncDuplicateLock();
        readonly Func<MdLocator, Task> _onHeadAddressChange;
        readonly string _uniqueId;
        IMdNode _head;
        IMdNode _currentLeaf;

        public MdLocator MdLocator => _head.MdLocator;

        public DataTree(IMdNode head, Func<MdLocator, Task> onHeadAddressChange)
        {
            _head = head;
            _onHeadAddressChange = onHeadAddressChange;
            _uniqueId = Encoding.UTF8.GetString(head.MdLocator.XORName);
        }

        #region StreamAD

        /// <summary>
        /// Reads the latest snapshot - if any - and all events since.
        /// </summary>
        /// <returns><see cref="SnapshotReading"/></returns>
        Task<Result<Snapshots.SnapshotReading>> IStreamAD.ReadFromSnapshot()
            => _currentLeaf?.ReadFromSnapshot() ?? _head.ReadFromSnapshot();

        IAsyncEnumerable<StoredValue> IStreamAD.GetAllValuesAsync()
            => _head.GetAllValuesAsync();

        IAsyncEnumerable<(Pointer, StoredValue)> IStreamAD.GetAllPointerValuesAsync()
            => _head.GetAllPointerValuesAsync();

        Task<Result<StoredValue>> IStreamAD.GetAtVersionAsync(ulong version)
            => _head.FindAsync(version);

        IAsyncEnumerable<(ulong, StoredValue)> IStreamAD.GetRangeAsync(ulong from, ulong to)
            => _head.FindRangeAsync(from, to);

        public IAsyncEnumerable<(ulong, StoredValue)> ReadForwardFromAsync(ulong from)
            => _head.ReadToEndAsync(from).OrderBy(c => c.Item1);

        public IAsyncEnumerable<(ulong, StoredValue)> ReadBackwardsFromAsync(ulong from)
            => _head.FindRangeAsync(0, from).OrderByDescending(c => c.Item1);

        /// <summary>
        /// Adds data to a tree structure
        /// that grows in an unbalanced way.
        /// </summary>
        /// <param name="key">Key under which the value will be stored.</param>
        /// <param name="value">The value to store.</param>
        /// <returns>A pointer to the value that was added, to be used for indexing.</returns>
        Task<Result<Pointer>> IStreamAD.AppendAsync(StoredValue value)
            => TryAppendAsync(value, ExpectedVersion.Any);

        Task<Result<Pointer>> IStreamAD.TryAppendAsync(StoredValue value, ExpectedVersion expectedVersion)
            => TryAppendAsync(value, expectedVersion);

        #endregion StreamAD

        // ----------------------------------------------------------------

        #region ValueAD

        Task<Result<StoredValue>> IValueAD.GetValueAsync()
            => _head.GetLastVersionAsync();

        Task<Result<Pointer>> IValueAD.SetAsync(StoredValue value) 
            => TryAppendAsync(value, ExpectedVersion.Any);

        Task<Result<Pointer>> IValueAD.TrySetAsync(StoredValue value, ExpectedVersion expectedVersion)
            => TryAppendAsync(value, expectedVersion);

        #endregion ValueAD

        // ----------------------------------------------------------------

        #region Common

        async Task<Result<Pointer>> TryAppendAsync(StoredValue value, ExpectedVersion expectedVersion)
        {
            using (var synch = await _writeLock.LockAsync(_uniqueId))
            {
                if (_head.IsFull)
                {
                    // create new head, add pointer to current head in to it.
                    // the level > 0 indicates its role as pointer holder
                    var meta = new MdMetadata
                    {
                        Level = _head.Level + 1,
                        StartIndex = _head.StartIndex // this is the head, so it always starts from previous head start, i.e. zero
                    };
                    var newHead = await _head.NodeFactory.CreateNewMdNodeAsync(meta).ConfigureAwait(false);
                    var pointer = new Pointer
                    {
                        MdLocator = _head.MdLocator,
                        ValueType = typeof(Pointer).Name
                    };
                    await newHead.AddAsync(pointer).ConfigureAwait(false);
                    _head = newHead;
                    await _onHeadAddressChange(newHead.MdLocator).ConfigureAwait(false);
                }

                return await DirectlyAppendToLeaf(value, expectedVersion).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Instead of traversing through the tree on every add,
        /// we keep a reference to current leaf, and add to it directly.
        /// </summary>
        /// <param name="key">Key under which the value will be stored.</param>
        /// <param name="value">The value to store.</param>
        /// <returns>A pointer to the value that was added.</returns>
        async Task<Result<Pointer>> DirectlyAppendToLeaf(StoredValue value, ExpectedVersion expectedVersion)
        {
            if (_currentLeaf == null)
                _currentLeaf = _head;
            else if (_currentLeaf.IsFull)
            {
                var result = await _head.TryAppendAsync(value, expectedVersion).ConfigureAwait(false);
                if (!result.HasValue)
                    return result;
                
                var leafResult = await _head.NodeFactory.LocateAsync(result.Value.MdLocator);
                if (leafResult.HasValue)
                {
                    await _currentLeaf.SetNext(leafResult.Value); // for range search
                    _currentLeaf = leafResult.Value;
                }
                else
                    return leafResult.CastError<IMdNode, Pointer>();
                // else problem, since previous current won't 
                // have done SetNext, which will break searching..

                return result;
            }

            return await _currentLeaf.TryAppendAsync(value, expectedVersion);
        }

        #endregion Common
    }
}