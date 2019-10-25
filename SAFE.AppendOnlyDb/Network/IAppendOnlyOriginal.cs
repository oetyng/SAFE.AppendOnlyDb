using SAFE.Data;
using System.Collections.Generic;
using System.Linq;

namespace SAFE.AppendOnlyDb.Network
{
    /// Common methods for all `AppendOnlyData` flavours.
    interface IAppendOnlyOriginal
    {
        /// Returns the address.
        Address GetAddress();

        /// Returns the name.
        XorName GetName();

        /// Returns the type tag.
        ulong GetTag();

        /// Returns all entries.
        List<Entry> GetEntries();

        /// Returns the last entry, if present.
        Result<Entry> GetLastEntry();

        /// Returns a value for the given key, if present.
        Result<byte[]> GetValue(byte[] key);

        /// Checks if the requester is the last owner.
        ///
        /// Returns:
        /// `Ok(())` if the requester is the owner,
        /// `Err::InvalidOwners` if the last owner is invalid,
        /// `Err::AccessDenied` if the requester is not the owner.
        Result<string> IsLastOwner(PublicKey requester);

        /// Returns the last entry index.
        Index GetExpectedEntriesIndex();

        /// Returns the last owners index.
        Index GetCurrentOwnerIndex();

        /// Returns the last permissions index.
        Index GetCurrentPermissionsIndex();

        /// Gets entry at index.
        Result<Entry> GetEntry(Index index);

        /// Gets a list of keys and values with the given indices.
        Result<List<(Index, Entry)>> GetEntriesRange(Index start, Index end);

        /// Gets owner at index.
        Result<Owner> GetOwner(Index index);

        /// Gets a complete list of owners from the entry in the permissions list at the specified
        /// index.
        Result<List<Owner>> GetOwnersRange(Index start, Index end);

        /// Gets permissions at index.
        Result<Permissions> GetPermissions(Index index);

        /// Gets a complete list of permissions from the entry in the permissions list at the specified
        /// indices.
        Result<List<Permissions>> GetPermissionsRange(Index start, Index end);

        /// Adds a new owner entry.
        ///
        /// If the specified `next_unused_index` does not match 
        /// the last entry index + 1 (or 0 in case of empty)
        /// of the owners collection, an error will be returned.
        Result<Index> AppendOwner(Owner owner, Index expectedIndex);

        /// Adds a new permissions entry.
        /// The `Perm` struct should contain valid indices.
        ///
        /// If the specified `next_unused_index` does not match 
        /// the last entry index + 1 (or 0 in case of empty)
        /// of the permissions collection, an error will be returned.
        Result<Index> AppendPermissions(Permissions permissions, Index expectedIndex);
    }
}
