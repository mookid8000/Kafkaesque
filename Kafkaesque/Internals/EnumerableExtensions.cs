using System.Collections.Generic;
using System.Linq;

namespace Kafkaesque.Internals
{
    static class EnumerableExtensions
    {
        public static IEnumerable<List<TItem>> Batch<TItem>(this IEnumerable<TItem> items, int maxBatchSize)
        {
            var list = new List<TItem>(maxBatchSize);

            foreach (var item in items)
            {
                list.Add(item);

                if (list.Count < maxBatchSize) continue;

                yield return list;
                list = new List<TItem>(maxBatchSize);
            }

            if (list.Any()) yield return list;
        }
    }
}