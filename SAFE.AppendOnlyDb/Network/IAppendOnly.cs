using SAFE.Data;
using System.Collections.Generic;

namespace SAFE.AppendOnlyDb.Network
{
    /// Common methods for all `AppendOnlyData` flavours.
    interface IAppendOnly
    {
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


        /// Returns the address.
        Address GetAddress();

        /// Returns the name.
        XorName GetName();

        /// Returns the type tag.
        ulong GetTypeTag();


        /// Returns the expected entry index.
        Index GetExpectedEntriesIndex();

        /// Returns the expected owner index.
        Index GetExpectedOwnersIndex();

        /// Returns the expected permissions index.
        Index GetExpectedPermissionsIndex();


        /// Returns the current entry index.
        Result<Index> GetCurrentEntriesIndex();

        /// Returns the owner index.
        Result<Index> GetCurrentOwnerIndex();

        /// Returns the permissions index.
        Result<Index> GetCurrentPermissionsIndex();


        /// Returns all entries.
        List<Entry> GetEntries();

        /// Returns a value for the given key, if present.
        Result<byte[]> GetValue(byte[] key);

        /// Checks if the requester is the last owner.
        ///
        /// Returns:
        /// `Ok(())` if the requester is the owner,
        /// `Err::InvalidOwners` if the last owner is invalid,
        /// `Err::AccessDenied` if the requester is not the owner.
        Result<bool> IsLastOwner(PublicKey requester);


        /// Gets entry at index.
        Result<Entry> GetEntry(Index index);

        /// Gets owner at index.
        Result<Owner> GetOwner(Index index);

        /// Gets permissions at index.
        Result<Permissions> GetPermissions(Index index);


        /// Returns the last entry, if present.
        Result<Entry> GetLastEntry();

        /// Returns the owner entry, if present.
        Result<Owner> GetLastOwner();

        /// Returns the permissions entry, if present.
        Result<Permissions> GetLastPermissions();


        /// Gets a list of keys and values with the given indices.
        Result<List<(Index, Entry)>> GetEntriesRange(Index start, Index end);

        /// Gets a complete list of owners from the entry in the permissions list at the specified
        /// index.
        Result<List<Owner>> GetOwnersRange(Index start, Index end);

        /// Gets a complete list of permissions from the entry in the permissions list at the specified
        /// indices.
        Result<List<Permissions>> GetPermissionsRange(Index start, Index end);
    }
}
