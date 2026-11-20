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
        : this(key, value, timestamp, headers, keyIsNull: false, valueIsNull: false)
    {
    }

    internal Record(TKey key, TValue value, long timestamp, Headers? headers, bool keyIsNull, bool valueIsNull)
    {
        Key = key;
        Value = value;
        Timestamp = timestamp;
        Headers = headers;
        _keyIsNull = keyIsNull;
        _valueIsNull = valueIsNull;
    }

    // Set when the record was read with a null key or value. A value type cannot hold null, so without
    // these a null read as long would be indistinguishable from 0.
    private readonly bool _keyIsNull;
    private readonly bool _valueIsNull;

    /// <summary>Gets the record key. For a record read with a null key this is <see langword="default"/>.</summary>
    public TKey Key
    {
        get;
    }

    /// <summary>Gets the record value. For a record read with a null value this is <see langword="default"/>.</summary>
    public TValue Value
    {
        get;
    }

    /// <summary>Gets the event timestamp in Unix milliseconds.</summary>
    public long Timestamp
    {
        get;
    }

    /// <summary>Gets the record headers, or <see langword="null"/> when there are none.</summary>
    public Headers? Headers
    {
        get;
    }

    /// <summary>
    /// Gets whether the key is null. This is also true for a value-type key that was read from a record
    /// without a key.
    /// </summary>
    public bool IsKeyNull
    {
        get
        {
            // Branches, not "x || y is null": unoptimized code boxes a value type for the latter.
            if (_keyIsNull)
            {
                return true;
            }

            if (Key is null)
            {
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Gets whether the value is null: a tombstone. This is also true for a value-type value that was
    /// read from a record without a value.
    /// </summary>
    public bool IsValueNull
    {
        get
        {
            if (_valueIsNull)
            {
                return true;
            }

            if (Value is null)
            {
                return true;
            }

            return false;
        }
    }

    /// <summary>Returns a record with the same key, timestamp and headers and a new value.</summary>
    public Record<TKey, TNewValue> WithValue<TNewValue>(TNewValue value) =>
        new(Key, value, Timestamp, Headers, _keyIsNull, valueIsNull: false);

    /// <summary>Returns a record with the same value, timestamp and headers and a new key.</summary>
    public Record<TNewKey, TValue> WithKey<TNewKey>(TNewKey key) =>
        new(key, Value, Timestamp, Headers, keyIsNull: false, _valueIsNull);
}

/// <summary>A key qualified by the time window it was aggregated in.</summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <param name="Key">The record key.</param>
/// <param name="Start">The inclusive window start in Unix milliseconds.</param>
/// <param name="End">The exclusive window end in Unix milliseconds.</param>
public readonly record struct Windowed<TKey>(TKey Key, long Start, long End);
