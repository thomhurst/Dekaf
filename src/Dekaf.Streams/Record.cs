using Dekaf.Serialization;

namespace Dekaf.Streams;

/// <summary>A record flowing through a topology: key, value, event timestamp and headers.</summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
public readonly struct Record<TKey, TValue>
{
    /// <summary>Creates a record.</summary>
    /// <param name="key">The record key.</param>
    /// <param name="value">The record value.</param>
    /// <param name="timestamp">The event timestamp in Unix milliseconds.</param>
    /// <param name="headers">The record headers, or <see langword="null"/> when there are none.</param>
    public Record(TKey key, TValue value, long timestamp, Headers? headers = null)
    {
        Key = key;
        Value = value;
        Timestamp = timestamp;
        Headers = headers;
    }

    /// <summary>Gets the record key.</summary>
    public TKey Key { get; }

    /// <summary>Gets the record value.</summary>
    public TValue Value { get; }

    /// <summary>Gets the event timestamp in Unix milliseconds.</summary>
    public long Timestamp { get; }

    /// <summary>Gets the record headers, or <see langword="null"/> when there are none.</summary>
    public Headers? Headers { get; }

    /// <summary>Returns a record with the same key, timestamp and headers and a new value.</summary>
    public Record<TKey, TNewValue> WithValue<TNewValue>(TNewValue value) => new(Key, value, Timestamp, Headers);

    /// <summary>Returns a record with the same value, timestamp and headers and a new key.</summary>
    public Record<TNewKey, TValue> WithKey<TNewKey>(TNewKey key) => new(key, Value, Timestamp, Headers);
}

/// <summary>A key qualified by the time window it was aggregated in.</summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <param name="Key">The record key.</param>
/// <param name="Start">The inclusive window start in Unix milliseconds.</param>
/// <param name="End">The exclusive window end in Unix milliseconds.</param>
public readonly record struct Windowed<TKey>(TKey Key, long Start, long End);
