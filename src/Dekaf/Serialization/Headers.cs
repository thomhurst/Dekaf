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
    private int _deferredTraceparentIndex = -1;
    private int _deferredTracestateIndex = -1;

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
        AddHeader(new Header(key, Encoding.UTF8.GetBytes(value)));
        return this;
    }

    /// <summary>
    /// Adds a header with a byte array value.
    /// </summary>
    public Headers Add(string key, byte[]? value)
    {
        AddHeader(new Header(key, value));
        return this;
    }

    /// <summary>
    /// Adds a header.
    /// </summary>
    public Headers Add(Header header)
    {
        AddHeader(header);
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
                RemoveAt(i);
        }
        return this;
    }

    /// <summary>
    /// Clears all headers.
    /// </summary>
    public void Clear()
    {
        _headers.Clear();
        _deferredTraceparentIndex = -1;
        _deferredTracestateIndex = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddDeferredTraceContext(Activity activity, string? traceState)
    {
        if (_deferredTraceparentIndex >= 0 || _deferredTracestateIndex >= 0)
            RemoveDeferredTraceContext();

        _deferredTraceparentIndex = _headers.Count;
        _headers.Add(Header.CreateDeferredTraceparent("traceparent", activity));
        if (string.IsNullOrEmpty(traceState))
            return;

        _deferredTracestateIndex = _headers.Count;
        _headers.Add(Header.CreateDeferredTracestate("tracestate", traceState!));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void RemoveDeferredTraceContext()
    {
        var traceparentIndex = _deferredTraceparentIndex;
        var tracestateIndex = _deferredTracestateIndex;
        if (traceparentIndex < 0 && tracestateIndex < 0)
            return;

        _deferredTraceparentIndex = -1;
        _deferredTracestateIndex = -1;

        if ((uint)tracestateIndex < (uint)_headers.Count)
            _headers.RemoveAt(tracestateIndex);

        if ((uint)traceparentIndex < (uint)_headers.Count
            && _headers[traceparentIndex].HasDeferredTraceparent)
            _headers.RemoveAt(traceparentIndex);
    }

    private void RemoveAt(int index)
    {
        _headers.RemoveAt(index);
        _deferredTraceparentIndex = AdjustTrackedIndex(_deferredTraceparentIndex, index);
        _deferredTracestateIndex = AdjustTrackedIndex(_deferredTracestateIndex, index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddHeader(Header header)
    {
        var traceparentIndex = _deferredTraceparentIndex;
        if (traceparentIndex < 0)
        {
            _headers.Add(header);
            return;
        }

        _headers.Insert(traceparentIndex, header);
        _deferredTraceparentIndex = traceparentIndex + 1;
        if (_deferredTracestateIndex >= 0)
            _deferredTracestateIndex++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int AdjustTrackedIndex(int trackedIndex, int removedIndex) =>
        trackedIndex == removedIndex ? -1 : trackedIndex > removedIndex ? trackedIndex - 1 : trackedIndex;

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
    private readonly object? _deferredValue;

    /// <summary>
    /// Creates a new header with a byte array value.
    /// </summary>
    public Header(string key, byte[]? value)
    {
        Key = key;
        _value = value.AsMemory();
        _deferredValue = null;
        IsValueNull = value is null;
    }

    /// <summary>
    /// Creates a new header with a memory value (zero-copy).
    /// </summary>
    public Header(string key, ReadOnlyMemory<byte> value, bool isNull = false)
    {
        Key = key;
        _value = value;
        _deferredValue = null;
        IsValueNull = isNull;
    }

    private Header(string key, object deferredValue)
    {
        Key = key;
        _value = default;
        _deferredValue = deferredValue;
        IsValueNull = false;
    }

    internal static Header CreateDeferredTraceparent(string key, Activity activity) =>
        new(key, activity);

    internal static Header CreateDeferredTracestate(string key, string traceState) =>
        new(key, traceState);

    internal bool HasDeferredTraceparent => _deferredValue is Activity;

    /// <summary>
    /// The header key.
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// The header value as bytes. Check IsValueNull before accessing.
    /// </summary>
    public ReadOnlyMemory<byte> Value
    {
        // Preserve the metadata emitted by the previously shipped auto-property accessors.
        [CompilerGenerated]
        get
        {
            return _deferredValue switch
            {
                null => _value,
                Activity activity => EncodeTraceparent(activity),
                string traceState => Encoding.UTF8.GetBytes(traceState),
                _ => throw new UnreachableException()
            };
        }
        [CompilerGenerated]
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

        return _deferredValue switch
        {
            null => _value.ToArray(),
            Activity activity => EncodeTraceparent(activity),
            string traceState => Encoding.UTF8.GetBytes(traceState),
            _ => throw new UnreachableException()
        };
    }

    /// <summary>
    /// Gets the value as a UTF-8 string.
    /// </summary>
    public string? GetValueAsString()
    {
        if (IsValueNull)
            return null;

        if (_deferredValue is string traceState)
            return traceState;
        if (_deferredValue is null)
            return Encoding.UTF8.GetString(_value.Span);

        Span<byte> value = stackalloc byte[Diagnostics.TraceContextPropagator.TraceparentLength];
        Diagnostics.TraceContextPropagator.WriteTraceparentUnchecked((Activity)_deferredValue, value);
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
            switch (_deferredValue)
            {
                case null:
                    writer.WriteVarInt(_value.Length);
                    writer.WriteRawBytes(_value.Span);
                    break;
                case Activity activity:
                    writer.WriteVarInt(Diagnostics.TraceContextPropagator.TraceparentLength);
                    var traceparentDestination = writer.BufferWriter.GetSpan(
                        Diagnostics.TraceContextPropagator.TraceparentLength);
                    Diagnostics.TraceContextPropagator.WriteTraceparentUnchecked(
                        activity,
                        traceparentDestination);
                    writer.BufferWriter.Advance(Diagnostics.TraceContextPropagator.TraceparentLength);
                    writer.AddBytesWritten(Diagnostics.TraceContextPropagator.TraceparentLength);
                    break;
                case string traceState:
                    var traceStateLength = Encoding.UTF8.GetByteCount(traceState);
                    writer.WriteVarInt(traceStateLength);
                    var traceStateDestination = writer.BufferWriter.GetSpan(traceStateLength);
                    Encoding.UTF8.GetBytes(traceState, traceStateDestination);
                    writer.BufferWriter.Advance(traceStateLength);
                    writer.AddBytesWritten(traceStateLength);
                    break;
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
            var valueLength = _deferredValue switch
            {
                null => _value.Length,
                Activity => Diagnostics.TraceContextPropagator.TraceparentLength,
                string traceState => Encoding.UTF8.GetByteCount(traceState),
                _ => throw new UnreachableException()
            };
            Record.WriteVarInt(destination, ref offset, valueLength);
            switch (_deferredValue)
            {
                case null:
                    _value.Span.CopyTo(destination[offset..]);
                    break;
                case Activity activity:
                    Diagnostics.TraceContextPropagator.WriteTraceparentUnchecked(
                        activity,
                        destination.Slice(offset, valueLength));
                    break;
                case string traceState:
                    Encoding.UTF8.GetBytes(traceState, destination[offset..]);
                    break;
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
            var valueLength = _deferredValue switch
            {
                null => _value.Length,
                Activity => Diagnostics.TraceContextPropagator.TraceparentLength,
                string traceState => Encoding.UTF8.GetByteCount(traceState),
                _ => throw new UnreachableException()
            };
            size += Record.VarIntSize(valueLength) + valueLength;
        }

        return size;
    }

    private static byte[] EncodeTraceparent(Activity activity)
    {
        var value = GC.AllocateUninitializedArray<byte>(
            Diagnostics.TraceContextPropagator.TraceparentLength);
        Diagnostics.TraceContextPropagator.WriteTraceparentUnchecked(activity, value);
        return value;
    }
}
