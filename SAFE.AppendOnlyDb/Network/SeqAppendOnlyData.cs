using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SAFE.Data;
using static SAFE.Data.Utils.EnumerableExt;

namespace SAFE.AppendOnlyDb.Network
{
    class SeqAppendOnlyDataMock : ISeqAppendOnly
    {
        readonly List<Entry> _entries = new List<Entry>();
        readonly List<Owner> _owners = new List<Owner>();
        readonly List<Permissions> _permissions = new List<Permissions>();
        readonly Address _address;

        public SeqAppendOnlyDataMock(Address address)
            => _address = address;

        public Task<Result<Index>> AppendAsync(List<Entry> entries, Index nextUnusedIndex)
        {
            if (nextUnusedIndex.Value != (ulong)_entries.Count)
                return Task.FromResult((Result<Index>)new VersionMismatch<Index>());
            _entries.AddRange(entries);
            return Task.FromResult(Result.OK(nextUnusedIndex.Next));
        }

        public Result<Index> AppendOwner(Owner owner, Index nextUnusedIndex)
        {
            if (nextUnusedIndex.Value != (ulong)_owners.Count)
                return Result.Fail<Index>(-1, "");
            _owners.Add(owner);
            return Result.OK(nextUnusedIndex);
        }

        public Result<Index> AppendPermissions(Permissions permissions, Index nextUnusedIndex)
        {
            if (nextUnusedIndex.Value != (ulong)_permissions.Count)
                return Result.Fail<Index>(-1, "");
            _permissions.Add(permissions);
            return Result.OK(nextUnusedIndex);
        }

        public Address GetAddress() => _address;


        public Result<Entry> GetEntry(Index index)
        {
            var i = (int)index.Value;
            return _entries.Count > i ?
                Result.OK(_entries[i]) :
                new DataNotFound<Entry>();
        }


        public List<Entry> GetEntries() => _entries.ToList();

        public Index GetNextEntriesIndex() => ((ulong)_entries.Count).AsIndex();

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

        public Result<Entry> GetLastEntry()
            => _entries.Any() ?
                Result.OK(_entries.Last()) :
                new DataNotFound<Entry>();

        public XorName GetName() => _address.Name;

        public Result<Owner> GetOwner(Index index)
        {
            var i = (int)index.Value;
            return _owners.Count > i ?
                Result.OK(_owners[i]) :
                new DataNotFound<Owner>();
        }

        public Index GetOwnersIndex()
            => ((ulong)_owners.Count).AsIndex();

        public Result<List<Owner>> GetOwnersRange(Index start, Index end)
        {
            var from = start.Value < end.Value ? start.Value : end.Value;
            var to = start.Value > end.Value ? start.Value : end.Value;
            return Result.OK(_owners.GetRange((int)from, (int)(to - from)));
        }

        public Result<Permissions> GetPermissions(Index index)
        {
            var i = (int)index.Value;
            return _permissions.Count > i ?
                Result.OK(_permissions[i]) :
                new DataNotFound<Permissions>();
        }

        public Index GetPermissionsIndex()
            => ((ulong)_permissions.Count).AsIndex();

        public Result<List<Permissions>> GetPermissionsRange(Index start, Index end)
        {
            var from = start.Value < end.Value ? start.Value : end.Value;
            var to = start.Value > end.Value ? start.Value : end.Value;
            return Result.OK(_permissions.GetRange((int)from, (int)(to - from)));
        }

        public ulong GetTag()
        {
            throw new NotImplementedException();
        }

        public Result<byte[]> GetValue(byte[] key)
        {
            throw new NotImplementedException();
        }

        public Result<string> IsLastOwner(PublicKey requester)
        {
            throw new NotImplementedException();
        }
    }
}
