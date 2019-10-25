using SAFE.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Network
{
    // Common methods for published and unpublished unsequenced `AppendOnlyData`.
    interface ISeqAppendOnly : IAppendOnly
    {
        /// Appends new entries.
        /// Returns an error if duplicate entries are present.
        /// If the specified `expected_index` does not match 
        /// the length of the AD, an error will be returned.
        Task<Result<Index>> AppendRangeAsync(List<Entry> entries, Index expectedIndex);
    }
}
