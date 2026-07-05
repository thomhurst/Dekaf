#if NETSTANDARD2_0
namespace System.Linq;

internal static class LinqCompatibilityExtensions
{
    public static IOrderedEnumerable<T> Order<T>(this IEnumerable<T> source) => source.OrderBy(static item => item);

    public static bool TryGetNonEnumeratedCount<T>(this IEnumerable<T> source, out int count)
    {
        if (source is ICollection<T> collection)
        {
            count = collection.Count;
            return true;
        }

        if (source is IReadOnlyCollection<T> readOnlyCollection)
        {
            count = readOnlyCollection.Count;
            return true;
        }

        count = 0;
        return false;
    }

    public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source) => new(source);
}
#endif
