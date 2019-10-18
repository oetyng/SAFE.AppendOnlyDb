using SAFE.Data;
using System.Collections.Generic;

namespace SAFE.AppendOnlyDb.Network.AD
{
    /// Common methods for all `AppendOnlyData` flavours.
    interface IAppendOnlyData
    {
        /// Adds a new owner entry.
        ///
        /// If the specified `next_unused_index` does not match 
        /// the last entry index + 1 (or 0 in case of empty)
        /// of the owners collection, an error will be returned.
        Result<Index> AppendOwner(Owner owner, Index nextUnusedIndex);

        /// Adds a new permissions entry.
        /// The `Perm` struct should contain valid indices.
        ///
        /// If the specified `next_unused_index` does not match 
        /// the last entry index + 1 (or 0 in case of empty)
        /// of the permissions collection, an error will be returned.
        Result<Index> AppendPermissions(Permissions permissions, Index nextUnusedIndex);

        /// Returns a value for the given key, if present.
        Result<byte[]> GetValue(byte[] key);

        /// Returns the last entry, if present.
        Result<Entry> GetLastEntry();

        /// Gets a list of keys and values with the given indices.
        Result<List<(Index, Entry)>> GetInRange(Index start, Index end);

        /// Returns all entries.
        List<Entry> GetEntries();

        /// Returns the address.
        Address GetAddress();

        /// Returns the name.
        XorName GetName();

        /// Returns the type tag.
        ulong GetTag();

        /// Returns the last entry index.
        Index GetNextEntriesIndex();

        /// Returns the last owners index.
        Index GetOwnersIndex();

        /// Returns the last permissions index.
        Index GetPermissionsIndex();

        /// Gets a complete list of permissions from the entry in the permissions list at the specified
        /// indices.
        Result<List<Permissions>> GetPermissionsRange(Index start, Index end);

        /// Fetches permissions at index.
        Result<Permissions> GetPermissions(Index index);

        /// Fetches owner at index.
        Result<Owner> GetOwner(Index index);

        /// Gets a complete list of owners from the entry in the permissions list at the specified
        /// index.
        Result<List<Owner>> GetOwnersRange(Index start, Index end);

        /// Checks if the requester is the last owner.
        ///
        /// Returns:
        /// `Ok(())` if the requester is the owner,
        /// `Err::InvalidOwners` if the last owner is invalid,
        /// `Err::AccessDenied` if the requester is not the owner.
        Result<string> IsLastOwner(PublicKey requester);
    }

    struct Entry
    {
        public byte[] Key { get; set; }
        public byte[] Value { get; set; }
    }

    public struct Index : System.IComparable
    {
        public static Index Zero => new Index { Value = 0 };
        public ulong Value { get; set; }

        public int CompareTo(object obj)
        {
            if (!(obj is Index index) || index.Value > Value)
                return -1;
            else if (Value == index.Value)
                return 0;
            else if (Value > index.Value)
                return 1;
            else
                throw new System.Exception();
        }

        public override bool Equals(object obj)
            => obj is Index index &&
                   Value == index.Value;

        public override int GetHashCode()
            => -1937169414 + Value.GetHashCode();
    }

    struct Address
    {
        public XorName Name { get; set; }
        public ulong Tag { get; set; }
    }

    struct XorName
    {
        public byte[] Value { get; set; }
    }

    struct Permissions
    { }

    struct Owner
    {
        public PublicKey PublicKey { get; set; }
        public ulong EntriesIndex { get; set; }
        public ulong PermissionsIndex { get; set; }
    }

    struct PublicKey
    { }
}
