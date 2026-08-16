using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Dekaf.Internal;
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
            Add(key, value!);
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
    private const int MaxCachedKeys = 128;
    private const int MaxCachedKeyBytes = 256;
    private static readonly Utf8StringInternCache s_keyCache = new(MaxCachedKeys, MaxCachedKeyBytes);
    private readonly ReadOnlyMemory<byte> _value;
    private readonly Activity? _deferredTraceparentActivity;

    /// <summary>
    /// Creates a new header with a byte array value.
    /// </summary>
    public Header(string key, byte[]? value)
    {
        Key = key;
        _value = value.AsMemory();
        _deferredTraceparentActivity = null;
        IsValueNull = value is null;
    }

    /// <summary>
    /// Creates a new header with a memory value (zero-copy).
    /// </summary>
    public Header(string key, ReadOnlyMemory<byte> value, bool isNull = false)
    {
        Key = key;
        _value = value;
        _deferredTraceparentActivity = null;
        IsValueNull = isNull;
    }

    private Header(string key, Activity traceparentActivity)
    {
        Key = key;
        _value = default;
        _deferredTraceparentActivity = traceparentActivity;
        IsValueNull = false;
    }

    internal static Header CreateDeferredTraceparent(string key, Activity activity) =>
        new(key, activity);

    /// <summary>
    /// The header key.
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// The header value as bytes. Check IsValueNull before accessing.
    /// </summary>
    public ReadOnlyMemory<byte> Value
    {
        get
        {
            if (_deferredTraceparentActivity is null)
                return _value;

            var value = GC.AllocateUninitializedArray<byte>(
                Diagnostics.TraceContextPropagator.TraceparentLength);
            Diagnostics.TraceContextPropagator.WriteTraceparentUnchecked(
                _deferredTraceparentActivity,
                value);
            return value;
        }
        init => _value = value;
    }

    /// <summary>
    /// Returns true if the header value is null.
    /// </summary>
    public bool IsValueNull { get; init; }

    /// <summary>
    /// Gets the value as a byte array. Prefer using Value property to avoid allocation.
    /// </summary>
    public byte[]? GetValueAsArray()
    {
        if (IsValueNull)
            return null;

        if (_deferredTraceparentActivity is null)
            return _value.ToArray();

        var value = GC.AllocateUninitializedArray<byte>(
            Diagnostics.TraceContextPropagator.TraceparentLength);
        Diagnostics.TraceContextPropagator.WriteTraceparentUnchecked(
            _deferredTraceparentActivity,
            value);
        return value;
    }

    /// <summary>
    /// Gets the value as a UTF-8 string.
    /// </summary>
    public string? GetValueAsString()
    {
        if (IsValueNull)
            return null;

        if (_deferredTraceparentActivity is null)
            return Encoding.UTF8.GetString(_value.Span);

        Span<byte> value = stackalloc byte[Diagnostics.TraceContextPropagator.TraceparentLength];
        Diagnostics.TraceContextPropagator.WriteTraceparentUnchecked(_deferredTraceparentActivity, value);
        return Encoding.UTF8.GetString(value);
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Key}={GetValueAsString() ?? "(null)"}";

    /// <summary>
    /// Writes the header to the protocol writer.
    /// </summary>
    [SkipLocalsInit]
    internal void Write(ref KafkaProtocolWriter writer)
    {
        WriteKey(ref writer, Key);

        if (IsValueNull)
        {
            writer.WriteVarInt(-1);
        }
        else
        {
            if (_deferredTraceparentActivity is null)
            {
                writer.WriteVarInt(_value.Length);
                writer.WriteRawBytes(_value.Span);
            }
            else
            {
                writer.WriteVarInt(Diagnostics.TraceContextPropagator.TraceparentLength);
                var destination = writer.BufferWriter.GetSpan(
                    Diagnostics.TraceContextPropagator.TraceparentLength);
                Diagnostics.TraceContextPropagator.WriteTraceparentUnchecked(
                    _deferredTraceparentActivity,
                    destination);
                writer.BufferWriter.Advance(Diagnostics.TraceContextPropagator.TraceparentLength);
                writer.AddBytesWritten(Diagnostics.TraceContextPropagator.TraceparentLength);
            }
        }
    }

    private static void WriteKey(ref KafkaProtocolWriter writer, string key)
    {
        if (key.Length <= 128)
        {
            Span<byte> buffer = stackalloc byte[512];
            var actualBytes = Encoding.UTF8.GetBytes(key, buffer);
            writer.WriteVarInt(actualBytes);
            if (actualBytes > 0)
            {
                var outputSpan = writer.BufferWriter.GetSpan(actualBytes);
                buffer[..actualBytes].CopyTo(outputSpan);
                writer.BufferWriter.Advance(actualBytes);
                writer.AddBytesWritten(actualBytes);
            }
            return;
        }

        var keyByteCount = Encoding.UTF8.GetByteCount(key);
        writer.WriteVarInt(keyByteCount);
        if (keyByteCount == 0)
            return;

        var span = writer.BufferWriter.GetSpan(keyByteCount);
        Encoding.UTF8.GetBytes(key, span);
        writer.BufferWriter.Advance(keyByteCount);
        writer.AddBytesWritten(keyByteCount);
    }

    /// <summary>
    /// Reads a header from the protocol reader.
    /// </summary>
    internal static Header Read(ref KafkaProtocolReader reader)
    {
        var keyLength = reader.ReadVarInt();
        var key = s_keyCache.Intern(reader.ReadMemorySlice(keyLength));

        var valueLength = reader.ReadVarInt();
        var isValueNull = valueLength < 0;
        var value = isValueNull ? ReadOnlyMemory<byte>.Empty : reader.ReadMemorySlice(valueLength);

        return new Header(key, value, isNull: isValueNull);
    }

    /// <summary>
    /// Encodes the header into a fixed-size destination span at <paramref name="offset"/>,
    /// advancing it past the bytes written. Must write exactly <see cref="CalculateSize"/>
    /// bytes — keep the two methods in sync.
    /// </summary>
    [SkipLocalsInit]
    internal void Encode(Span<byte> destination, ref int offset)
    {
        var key = Key;
        if (key.Length <= 128)
        {
            // Single-pass encode for short keys (the common case): UTF-8 worst case is
            // 3 bytes per char, so 128 chars always fit in the 512-byte scratch buffer.
            Span<byte> buffer = stackalloc byte[512];
            var keyByteCount = Encoding.UTF8.GetBytes(key, buffer);
            Record.WriteVarInt(destination, ref offset, keyByteCount);
            buffer[..keyByteCount].CopyTo(destination[offset..]);
            offset += keyByteCount;
        }
        else
        {
            var keyByteCount = Encoding.UTF8.GetByteCount(key);
            Record.WriteVarInt(destination, ref offset, keyByteCount);
            Encoding.UTF8.GetBytes(key, destination[offset..]);
            offset += keyByteCount;
        }

        if (IsValueNull)
        {
            Record.WriteVarInt(destination, ref offset, -1);
        }
        else
        {
            var valueLength = _deferredTraceparentActivity is null
                ? _value.Length
                : Diagnostics.TraceContextPropagator.TraceparentLength;
            Record.WriteVarInt(destination, ref offset, valueLength);
            if (_deferredTraceparentActivity is null)
            {
                _value.Span.CopyTo(destination[offset..]);
            }
            else
            {
                Diagnostics.TraceContextPropagator.WriteTraceparentUnchecked(
                    _deferredTraceparentActivity,
                    destination.Slice(offset, valueLength));
            }
            offset += valueLength;
        }
    }

    internal int CalculateSize()
    {
        // ASCII keys (99%+ of cases): byte count == char count. Ascii.IsValid is
        // SIMD-optimized and much cheaper than UTF8.GetByteCount for this case.
        var keyBytes = Ascii.IsValid(Key) ? Key.Length : Encoding.UTF8.GetByteCount(Key);
        var size = Record.VarIntSize(keyBytes) + keyBytes;

        if (IsValueNull)
        {
            size += Record.VarIntSize(-1);
        }
        else
        {
            var valueLength = _deferredTraceparentActivity is null
                ? _value.Length
                : Diagnostics.TraceContextPropagator.TraceparentLength;
            size += Record.VarIntSize(valueLength) + valueLength;
        }

        return size;
    }
}
