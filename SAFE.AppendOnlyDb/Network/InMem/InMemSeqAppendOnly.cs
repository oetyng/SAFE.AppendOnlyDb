using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SAFE.Data;
using static SAFE.Data.Utils.EnumerableExt;

namespace SAFE.AppendOnlyDb.Network
{
    class InMemSeqAppendOnly : ISeqAppendOnly
    {
        readonly List<Entry> _entries = new List<Entry>();
        readonly List<Owner> _owners = new List<Owner>();
        readonly List<Permissions> _permissions = new List<Permissions>();
        readonly Address _address;

        public InMemSeqAppendOnly(Address address)
            => _address = address;

        public Task<Result<Index>> AppendRangeAsync(List<Entry> entries, Index expectedIndex)
        {
            if (expectedIndex.Value != (ulong)_entries.Count)
                return Task.FromResult((Result<Index>)new VersionMismatch<Index>());
            _entries.AddRange(entries);
            return Task.FromResult(Result.OK(((ulong)_entries.Count).AsIndex()));
        }

        public Result<Index> AppendOwner(Owner owner, Index expectedIndex)
        {
            if (expectedIndex.Value != (ulong)_owners.Count)
                return new VersionMismatch<Index>();
            _owners.Add(owner);
            return Result.OK(expectedIndex);
        }

        public Result<Index> AppendPermissions(Permissions permissions, Index expectedIndex)
        {
            if (expectedIndex.Value != (ulong)_permissions.Count)
                return new VersionMismatch<Index>();
            _permissions.Add(permissions);
            return Result.OK(expectedIndex);
        }

        public Address GetAddress() => _address;
        public XorName GetName() => _address.Name;
        public ulong GetTypeTag() => throw new NotImplementedException();

        public Index GetExpectedEntriesIndex() => ((ulong)_entries.Count).AsIndex();
        public Index GetExpectedOwnersIndex() => ((ulong)_owners.Count).AsIndex();
        public Index GetExpectedPermissionsIndex() => ((ulong)_permissions.Count).AsIndex();

        public Result<Index> GetCurrentEntriesIndex()
        {
            if (_entries.Count == 0) new DataNotFound<Index>();
            return Result.OK(((ulong)_entries.Count - 1).AsIndex());
        }

        public Result<Index> GetCurrentOwnerIndex()
        {
            if (_owners.Count == 0) new DataNotFound<Index>();
            return Result.OK(((ulong)_owners.Count - 1).AsIndex());
        }

        public Result<Index> GetCurrentPermissionsIndex()
        {
            if (_permissions.Count == 0) new DataNotFound<Index>();
            return Result.OK(((ulong)_permissions.Count - 1).AsIndex());
        }

        public List<Entry> GetEntries() => _entries.ToList();
        public Result<byte[]> GetValue(byte[] key) => throw new NotImplementedException();
        public Result<bool> IsLastOwner(PublicKey requester)
        {
            var lastOwner = GetLastOwner();
            if (!lastOwner.HasValue)
                return lastOwner.CastError<Owner, bool>();

            return Result.OK(lastOwner.Value.PublicKey == requester);
        }


        public Result<Entry> GetEntry(Index index)
        {
            var i = (int)index.Value;
            return _entries.Count > i ?
                Result.OK(_entries[i]) :
                new DataNotFound<Entry>();
        }

        public Result<Owner> GetOwner(Index index)
        {
            var i = (int)index.Value;
            return _owners.Count > i ?
                Result.OK(_owners[i]) :
                new DataNotFound<Owner>();
        }

        public Result<Permissions> GetPermissions(Index index)
        {
            var i = (int)index.Value;
            return _permissions.Count > i ?
                Result.OK(_permissions[i]) :
                new DataNotFound<Permissions>();
        }

        public Result<Entry> GetLastEntry()
            => _entries.Any() ?
                Result.OK(_entries.Last()) :
                new DataNotFound<Entry>();

        public Result<Owner> GetLastOwner()
            => _owners.Any() ?
                Result.OK(_owners.Last()) :
                new DataNotFound<Owner>();

        public Result<Permissions> GetLastPermissions()
            => _permissions.Any() ?
                Result.OK(_permissions.Last()) :
                new DataNotFound<Permissions>();

        public Result<List<(Index, Entry)>> GetEntriesRange(Index start, Index end)
        {
            var backwards = start.Value > end.Value;
            var from = start.Value < end.Value ? start.Value : end.Value;
            var to = start.Value > end.Value ? start.Value : end.Value;
            var count = to - from + (to == from ? 1UL : 0);
            var range = _entries.Count > 0 ? 
                _entries.GetRange((int)from, (int)count) :
                new List<Entry>();
            var indices = LongRange(from, count).Select(c => c.AsIndex());

            if (backwards)
                return Result.OK(range
                    .Zip(indices, (c, i) => (i, c))
                    .OrderByDescending(c => c.i)
                    .ToList());
            else
                return Result.OK(range
                    .Zip(indices, (c, i) => (i, c))
                    .OrderBy(c => c.i)
                    .ToList());
        }

        public Result<List<Owner>> GetOwnersRange(Index start, Index end)
        {
            var from = start.Value < end.Value ? start.Value : end.Value;
            var to = start.Value > end.Value ? start.Value : end.Value;
            return Result.OK(_owners.GetRange((int)from, (int)(to - from)));
        }

        public Result<List<Permissions>> GetPermissionsRange(Index start, Index end)
        {
            var from = start.Value < end.Value ? start.Value : end.Value;
            var to = start.Value > end.Value ? start.Value : end.Value;
            return Result.OK(_permissions.GetRange((int)from, (int)(to - from)));
        }
    }
}
