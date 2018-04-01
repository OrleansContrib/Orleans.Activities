using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Activities
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<TSource> Append<TSource>(this IEnumerable<TSource> source, TSource item)
        {
            foreach (var element in source ?? throw new ArgumentNullException(nameof(source)))
                yield return element;
            yield return item;
        }

        public static IEnumerable<TSource> Yield<TSource>(this TSource item)
        {
            yield return item;
        }
    }

    public static class EnumerableFactory
    {
        private static class EmptyEnumerableFactory<TElement>
        {
            public static readonly Func<IEnumerable<TElement>> Instance = () => Enumerable.Empty<TElement>();
        }

        public static Func<IEnumerable<TSource>> Empty<TSource>() => EmptyEnumerableFactory<TSource>.Instance;
    }
}
