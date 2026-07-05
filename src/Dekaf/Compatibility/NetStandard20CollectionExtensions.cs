#if NETSTANDARD2_0
namespace System.Collections.Generic;

internal static class KeyValuePairCompatibilityExtensions
{
    public static void Deconstruct<TKey, TValue>(
        this KeyValuePair<TKey, TValue> pair,
        out TKey key,
        out TValue value)
    {
        key = pair.Key;
        value = pair.Value;
    }

    public static bool TryDequeue<T>(this Queue<T> queue, out T result)
    {
        if (queue.Count == 0)
        {
            result = default!;
            return false;
        }

        result = queue.Dequeue();
        return true;
    }

    public static TValue? GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
        where TKey : notnull
        => dictionary.TryGetValue(key, out var value) ? value : default;

    public static TValue GetValueOrDefault<TKey, TValue>(
        this Dictionary<TKey, TValue> dictionary,
        TKey key,
        TValue defaultValue)
        where TKey : notnull
        => dictionary.TryGetValue(key, out var value) ? value : defaultValue;

    public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        where TKey : notnull
    {
        if (dictionary.ContainsKey(key))
            return false;

        dictionary.Add(key, value);
        return true;
    }

    public static bool Remove<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, out TValue value)
        where TKey : notnull
    {
        if (!dictionary.TryGetValue(key, out value!))
            return false;

        dictionary.Remove(key);
        return true;
    }

    public static int EnsureCapacity<T>(this List<T> list, int capacity)
    {
        if (list.Capacity < capacity)
            list.Capacity = capacity;
        return list.Capacity;
    }
}
#endif
