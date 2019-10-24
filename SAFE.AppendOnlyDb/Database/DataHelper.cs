using SAFE.Data.Utils;
using System.Collections.Generic;
using System.Linq;

namespace SAFE.AppendOnlyDb.Network
{
    public static class DataHelper
    {
        internal static Entry ToEntry(this StoredValue value)
            => new Entry
            {
                Key = new byte[0],
                Value = value.GetBytes()
            };

        internal static List<Entry> ToEntries(this List<StoredValue> values)
            => values.Select(c => c.ToEntry()).ToList();

        internal static List<Entry> ToEntries(this StoredValue value)
            => new[] { value.ToEntry() }.ToList();

        internal static StoredValue ToStoredValue(this Entry entry)
            => entry.Value.Parse<StoredValue>();

        internal static Index AsIndex(this ulong index)
            => new Index(index);
    }
}
