using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Serialization;

/// <summary>
/// Collection of headers for a Kafka record.
/// </summary>
public sealed class Headers : IEnumerable<Header>
{
    private readonly List<Header> _headers;

    /// <summary>
    /// Creates an empty headers collection.
    /// </summary>
    public Headers()
    {
        _headers = [];
    }

    /// <summary>
    /// Creates a headers collection with the specified capacity.
    /// </summary>
    public Headers(int capacity)
    {
        _headers = new List<Header>(capacity);
    }

    /// <summary>
    /// Creates a headers collection from existing headers.
    /// </summary>
    public Headers(IEnumerable<Header> headers)
    {
        _headers = [.. headers];
    }

    /// <summary>
    /// Creates a new empty headers collection.
    /// </summary>
    /// <returns>A new empty Headers instance.</returns>
    public static Headers Create() => new();

    /// <summary>
    /// Creates a new headers collection with a single header.
    /// </summary>
    /// <param name="key">The header key.</param>
    /// <param name="value">The header value.</param>
    /// <returns>A new Headers instance with one header.</returns>
    public static Headers Create(string key, string value) => new Headers().Add(key, value);

    /// <summary>
    /// Creates a new headers collection with a single header.
    /// </summary>
    /// <param name="key">The header key.</param>
    /// <param name="value">The header value as bytes.</param>
    /// <returns>A new Headers instance with one header.</returns>
    public static Headers Create(string key, byte[]? value) => new Headers().Add(key, value);

    /// <summary>
    /// Gets the number of headers.
    /// </summary>
    public int Count => _headers.Count;

    /// <summary>
    /// Gets the header at the specified index.
    /// </summary>
    public Header this[int index] => _headers[index];

    /// <summary>
    /// Adds a header with a string value.
    /// </summary>
    public Headers Add(string key, string value)
    {
        _headers.Add(new Header(key, Encoding.UTF8.GetBytes(value)));
        return this;
    }

    /// <summary>
    /// Adds a header with a byte array value.
    /// </summary>
    public Headers Add(string key, byte[]? value)
    {
        _headers.Add(new Header(key, value));
        return this;
    }

    /// <summary>
    /// Adds a header.
    /// </summary>
    public Headers Add(Header header)
    {
        _headers.Add(header);
        return this;
    }

    /// <summary>
    /// Gets the first header with the specified key.
    /// </summary>
    public Header? GetFirst(string key)
    {
        // Manual loop to avoid closure allocation from lambda predicate
        foreach (var header in _headers)
        {
            if (header.Key == key)
                return header;
        }
        return null;
    }

    /// <summary>
    /// Gets all headers with the specified key.
    /// Uses yield return for deferred execution without list allocation.
    /// </summary>
    public IEnumerable<Header> GetAll(string key)
    {
        // Use iterator method for zero-allocation deferred execution.
        // The state machine is only allocated when the caller enumerates.
        foreach (var header in _headers)
        {
            if (header.Key == key)
                yield return header;
        }
    }

    /// <summary>
    /// Gets the first header value with the specified key as a string.
    /// </summary>
    public string? GetFirstAsString(string key)
    {
        var header = GetFirst(key);
        return header?.GetValueAsString();
    }

    /// <summary>
    /// Removes all headers with the specified key.
    /// </summary>
    public Headers Remove(string key)
    {
        // Manual loop to avoid closure allocation from RemoveAll predicate
        for (var i = _headers.Count - 1; i >= 0; i--)
        {
            if (_headers[i].Key == key)
                _headers.RemoveAt(i);
        }
        return this;
    }

    /// <summary>
    /// Clears all headers.
    /// </summary>
    public void Clear()
    {
        _headers.Clear();
    }

    /// <summary>
    /// Adds multiple headers from a collection of key-value pairs.
    /// </summary>
    /// <param name="headers">The headers to add.</param>
    /// <returns>This Headers instance for chaining.</returns>
    public Headers AddRange(IEnumerable<KeyValuePair<string, string>> headers)
    {
        foreach (var kvp in headers)
        {
            Add(kvp.Key, kvp.Value);
        }
        return this;
    }

    /// <summary>
    /// Adds a header conditionally.
    /// </summary>
    /// <param name="condition">If true, the header is added; otherwise, nothing happens.</param>
    /// <param name="key">The header key.</param>
    /// <param name="value">The header value.</param>
    /// <returns>This Headers instance for chaining.</returns>
    public Headers AddIf(bool condition, string key, string value)
    {
        if (condition)
        {
            Add(key, value);
        }
        return this;
    }

    /// <summary>
    /// Adds a header if the value is not null.
    /// </summary>
    /// <param name="key">The header key.</param>
    /// <param name="value">The header value (if null, header is not added).</param>
    /// <returns>This Headers instance for chaining.</returns>
    public Headers AddIfNotNull(string key, string? value)
    {
        if (value is not null)
        {
            Add(key, value);
        }
        return this;
    }

    /// <summary>
    /// Adds a header if the value is not null or empty.
    /// </summary>
    /// <param name="key">The header key.</param>
    /// <param name="value">The header value (if null or empty, header is not added).</param>
    /// <returns>This Headers instance for chaining.</returns>
    public Headers AddIfNotNullOrEmpty(string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            Add(key, value);
        }
        return this;
    }

    /// <summary>
    /// Gets all headers as a list.
    /// </summary>
    public IReadOnlyList<Header> ToList() => _headers.AsReadOnly();

    public IEnumerator<Header> GetEnumerator() => _headers.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Represents a single header in a Kafka record.
/// Uses ReadOnlyMemory to avoid copying header data.
/// This is a struct to avoid heap allocations in the hot path.
/// </summary>
public readonly record struct Header
{
    /// <summary>
    /// Cache of interned header key strings to avoid per-message allocations.
    /// Kafka headers typically reuse the same small set of keys across all messages,
    /// so caching them avoids repeated string allocations. Capped to prevent unbounded growth.
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> s_keyCache = new();
    private static int s_keyCacheCount;
    private const int MaxCachedKeys = 128;

    /// <summary>
    /// Creates a new header with a byte array value.
    /// </summary>
    public Header(string key, byte[]? value)
    {
        Key = key;
        Value = value.AsMemory();
        IsValueNull = value is null;
    }

    /// <summary>
    /// Creates a new header with a memory value (zero-copy).
    /// </summary>
    public Header(string key, ReadOnlyMemory<byte> value, bool isNull = false)
    {
        Key = key;
        Value = value;
        IsValueNull = isNull;
    }

    /// <summary>
    /// The header key.
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// The header value as bytes. Check IsValueNull before accessing.
    /// </summary>
    public ReadOnlyMemory<byte> Value { get; init; }

    /// <summary>
    /// Returns true if the header value is null.
    /// </summary>
    public bool IsValueNull { get; init; }

    /// <summary>
    /// Gets the value as a byte array. Prefer using Value property to avoid allocation.
    /// </summary>
    public byte[]? GetValueAsArray() => IsValueNull ? null : Value.ToArray();

    /// <summary>
    /// Gets the value as a UTF-8 string.
    /// </summary>
    public string? GetValueAsString()
    {
        return IsValueNull ? null : Encoding.UTF8.GetString(Value.Span);
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Key}={GetValueAsString() ?? "(null)"}";

    /// <summary>
    /// Writes the header to the protocol writer.
    /// </summary>
    internal void Write(ref KafkaProtocolWriter writer)
    {
        // Write key with VarInt length prefix - use the writer's string encoding support
        var keyByteCount = Encoding.UTF8.GetByteCount(Key);
        writer.WriteVarInt(keyByteCount);
        writer.WriteStringContent(Key);

        if (IsValueNull)
        {
            writer.WriteVarInt(-1);
        }
        else
        {
            writer.WriteVarInt(Value.Length);
            writer.WriteRawBytes(Value.Span);
        }
    }

    /// <summary>
    /// Reads a header from the protocol reader.
    /// </summary>
    internal static Header Read(ref KafkaProtocolReader reader)
    {
        var keyLength = reader.ReadVarInt();
        var key = reader.ReadStringContent(keyLength);

        // Intern the key string: Kafka headers typically reuse the same keys
        // across all messages, so caching avoids per-message string allocation.
        // Use TryGetValue first (lock-free read) for the common case where the key is already cached.
        if (s_keyCache.TryGetValue(key, out var cached))
        {
            key = cached;
        }
        else if (Volatile.Read(ref s_keyCacheCount) < MaxCachedKeys)
        {
            // Only attempt to add if under the cap. The volatile read avoids
            // ConcurrentDictionary.Count which acquires all bucket locks.
            if (s_keyCache.TryAdd(key, key))
            {
                Interlocked.Increment(ref s_keyCacheCount);
            }
        }

        var valueLength = reader.ReadVarInt();
        var isValueNull = valueLength < 0;
        var value = isValueNull ? ReadOnlyMemory<byte>.Empty : reader.ReadMemorySlice(valueLength);

        return new Header(key, value, isNull: isValueNull);
    }

    internal int CalculateSize()
    {
        var keyBytes = Encoding.UTF8.GetByteCount(Key);
        var size = Record.VarIntSize(keyBytes) + keyBytes;

        if (IsValueNull)
        {
            size += Record.VarIntSize(-1);
        }
        else
        {
            size += Record.VarIntSize(Value.Length) + Value.Length;
        }

        return size;
    }
}

/// <summary>
/// A zero-copy read-only view over a slice of a <see cref="Header"/> array.
/// Implements <see cref="IReadOnlyList{Header}"/> without allocating a new array,
/// avoiding per-message header copying in the consumer hot path.
/// </summary>
/// <remarks>
/// The backing array may be rented from <see cref="System.Buffers.ArrayPool{T}"/> and
/// will be returned when the owning <c>LazyRecordList</c> is disposed.
/// The <see cref="HeaderSlice"/> is valid only for the lifetime of the current
/// consume iteration (same as <c>RawBytes</c> deserializer semantics).
/// This is a class (not struct) because it must implement <see cref="IReadOnlyList{Header}"/>
/// avoiding the per-message <c>Header[]</c> copy. Instances are pooled via
/// <see cref="Rent"/>/<see cref="Return"/> to eliminate per-message class allocation
/// for messages with headers.
/// The struct enumerator avoids boxing when iterated over the concrete type;
/// iteration through <c>IReadOnlyList{Header}</c> will still box.
/// </remarks>
internal sealed class HeaderSlice : IReadOnlyList<Header>
{
    // Pool for reusing HeaderSlice instances to avoid per-message class allocation.
    // Most messages that have headers use a small number of them, so a modest pool suffices.
    // Soft limit via Volatile.Read avoids ConcurrentStack.Count overhead.
    private static readonly ConcurrentStack<HeaderSlice> s_pool = new();
    private static int s_poolCount;
    private const int MaxPoolSize = 256;

    private Header[] _array = null!;
    private int _count;

    private HeaderSlice() { }

    /// <summary>
    /// Rents a HeaderSlice from the pool or creates a new one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static HeaderSlice Rent(Header[] array, int count)
    {
        if (s_pool.TryPop(out var instance))
        {
            Interlocked.Decrement(ref s_poolCount);
            instance._array = array;
            instance._count = count;
            return instance;
        }

        return new HeaderSlice { _array = array, _count = count };
    }

    /// <summary>
    /// Returns a HeaderSlice to the pool for reuse.
    /// After return, the instance's data is cleared — callers must not access
    /// a returned HeaderSlice. Use <see cref="MaterializeHeaders"/> to snapshot
    /// headers before returning (e.g., in <c>ConsumeOneAsync</c>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Return(HeaderSlice instance)
    {
        // Zero the fields to prevent data races: if a returned HeaderSlice is re-rented
        // by another consumer, the previous holder would see the new consumer's header data.
        instance._array = null!;
        instance._count = 0;

        if (Interlocked.Increment(ref s_poolCount) <= MaxPoolSize)
        {
            s_pool.Push(instance);
        }
        else
        {
            Interlocked.Decrement(ref s_poolCount);
        }
    }

    public int Count => _count;

    public Header this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _count);
            return _array[index];
        }
    }

    /// <summary>
    /// Creates a standalone snapshot of the headers as a plain array.
    /// The returned array is not pooled and is safe to hold indefinitely.
    /// Used by <c>ConsumeOneAsync</c> to materialize headers before the pooled
    /// HeaderSlice is returned during enumerator disposal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Header[] MaterializeHeaders() => _array[.._count];

    public Enumerator GetEnumerator() => new Enumerator(_array, _count);
    IEnumerator<Header> IEnumerable<Header>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<Header>
    {
        private readonly Header[] _array;
        private readonly int _count;
        private int _index;

        internal Enumerator(Header[] array, int count)
        {
            _array = array;
            _count = count;
            _index = -1;
        }

        public Header Current => _array[_index];
        object IEnumerator.Current => Current;
        public bool MoveNext() => ++_index < _count;
        public void Reset() => _index = -1;
        public void Dispose() { }
    }
}
