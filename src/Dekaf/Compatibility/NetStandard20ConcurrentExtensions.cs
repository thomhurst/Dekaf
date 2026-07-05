#if NETSTANDARD2_0
namespace System.Collections.Concurrent;

internal static class ConcurrentCollectionCompatibilityExtensions
{
    public static TValue? GetValueOrDefault<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key)
        where TKey : notnull
        => dictionary.TryGetValue(key, out var value) ? value : default;

    public static TValue GetValueOrDefault<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key,
        TValue defaultValue)
        where TKey : notnull
        => dictionary.TryGetValue(key, out var value) ? value : defaultValue;

    public static void Clear<T>(this ConcurrentQueue<T> queue)
    {
        while (queue.TryDequeue(out _))
        {
        }
    }

    public static bool TryRemove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key)
        where TKey : notnull
        => dictionary.TryRemove(key, out _);

    public static bool TryRemove<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        KeyValuePair<TKey, TValue> item)
        where TKey : notnull
        => ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Remove(item);

    public static TValue GetOrAdd<TKey, TValue, TArg>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key,
        Func<TKey, TArg, TValue> valueFactory,
        TArg factoryArgument)
        where TKey : notnull
        => dictionary.GetOrAdd(key, k => valueFactory(k, factoryArgument));

    public static TValue AddOrUpdate<TKey, TValue, TArg>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key,
        Func<TKey, TArg, TValue> addValueFactory,
        Func<TKey, TValue, TArg, TValue> updateValueFactory,
        TArg factoryArgument)
        where TKey : notnull
        => dictionary.AddOrUpdate(
            key,
            k => addValueFactory(k, factoryArgument),
            (k, existing) => updateValueFactory(k, existing, factoryArgument));
}
#endif
