using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Utils
{
    public static class Extensions
    {
        public static async Task<IEnumerable<KeyValuePair<T1, T2>>> ToValues<T1, T2>(
            this IEnumerable<KeyValuePair<T1, Task<T2>>> tasks)
        {
            return await Task.WhenAll(
               tasks.Select(
                   async pair =>
                       new KeyValuePair<T1, T2>(pair.Key, await pair.Value)));
        }
    }

    public static class EnumerableExt
    {
        public static IEnumerable<ulong> LongRange(ulong start, ulong count)
        {
            var end = start + count;
            for (var current = start; current < end; ++current)
                yield return current;
        }
    }
}