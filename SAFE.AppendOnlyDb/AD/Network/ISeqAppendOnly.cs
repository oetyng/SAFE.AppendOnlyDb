using SAFE.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Network.AD
{
    // Common methods for published and unpublished unsequenced `AppendOnlyData`.
    interface ISeqAppendOnly : IAppendOnlyData
    {
        /// Appends new entries.
        /// Returns an error if duplicate entries are present.
        /// If the specified `next_unused_index` does not match 
        /// the length of the AD, an error will be returned.
        Task<Result<Index>> AppendAsync(List<Entry> entries, Index nextUnusedIndex);
    }
}
