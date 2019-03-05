using SAFE.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    internal class DataTree : IStreamAD, IValueAD
    {
        readonly Func<MdLocator, Task> _onHeadAddressChange;
        IMdNode _head;
        IMdNode _currentLeaf;

        public MdLocator MdLocator => _head.MdLocator;

        public DataTree(IMdNode head, Func<MdLocator, Task> onHeadAddressChange)
        {
            _head = head;
            _onHeadAddressChange = onHeadAddressChange;
        }

        #region StreamAD

        IAsyncEnumerable<StoredValue> IStreamAD.GetAllValuesAsync()
            => _head.GetAllValuesAsync();

        IAsyncEnumerable<(Pointer, StoredValue)> IStreamAD.GetAllPointerValuesAsync()
            => _head.GetAllPointerValuesAsync();

        Task<Result<StoredValue>> IStreamAD.GetVersionAsync(ulong version)
            => _head.FindAsync(version);

        IAsyncEnumerable<(ulong, StoredValue)> IStreamAD.GetRangeAsync(ulong from, ulong to)
            => _head.FindRangeAsync(from, to);

        public IOrderedAsyncEnumerable<(ulong, StoredValue)> ReadForwardFromAsync(ulong from)
            => _head.ReadToEndAsync(from).OrderBy(c => c.Item1);

        public IOrderedAsyncEnumerable<(ulong, StoredValue)> ReadBackwardsFromAsync(ulong from)
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

        Task<Result<Pointer>> IStreamAD.TryAppendAsync(StoredValue value, ulong expectedVersion)
            => TryAppendAsync(value, ExpectedVersion.Specific(expectedVersion));

        #endregion StreamAD

        // ----------------------------------------------------------------

        #region ValueAD

        Task<Result<StoredValue>> IValueAD.GetValueAsync()
            => _head.GetLastVersionAsync();

        Task<Result<Pointer>> IValueAD.SetAsync(StoredValue value) 
            => TryAppendAsync(value, ExpectedVersion.Any);

        Task<Result<Pointer>> IValueAD.TrySetAsync(StoredValue value, ulong expectedVersion)
            => TryAppendAsync(value, ExpectedVersion.Specific(expectedVersion));

        #endregion ValueAD

        // ----------------------------------------------------------------

        #region Common

        async Task<Result<Pointer>> TryAppendAsync(StoredValue value, ExpectedVersion expectedVersion)
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
                var newHead = await MdAccess.CreateAsync(meta).ConfigureAwait(false);
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
                
                var leafResult = await MdAccess.LocateAsync(result.Value.MdLocator);
                if (leafResult.HasValue)
                {
                    await _currentLeaf.SetNext(leafResult.Value); // for range search
                    _currentLeaf = leafResult.Value;
                }
                // else problem, since previous current won't 
                // have done SetNext, which will break searching..

                return result;
            }

            return await _currentLeaf.TryAppendAsync(value, expectedVersion);
        }

        #endregion Common
    }
}