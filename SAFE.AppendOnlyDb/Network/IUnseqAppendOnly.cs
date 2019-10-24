using SAFE.Data;
using System.Collections.Generic;

namespace SAFE.AppendOnlyDb.Network
{
    // Common methods for published and unpublished unsequenced `AppendOnlyData`.
    interface IUnseqAppendOnly : IAppendOnlyData
    {
        /// Appends new entries.
        /// Returns an error if duplicate entries are present.
        Result Append(List<Entry> entries);
    }
}
