using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dekaf.Serialization;

namespace Dekaf.Protocol.Records;

/// <summary>
/// A single record within a RecordBatch.
/// Uses variable-length encoding for efficiency.
/// Key and Value use ReadOnlyMemory to avoid copying data from the network buffer.
/// This is a struct to avoid heap allocations in the hot path.
/// </summary>
public readonly record struct Record
{
    // Length varint plus six mandatory one-byte body fields.
    internal const int MinimumEncodedSize = 7;

    // Even empty headers need two wire bytes. This limit already represents at least
    // 512 KiB of header metadata while bounding pooled-array amplification and parse work.
    internal const int MaxReasonableHeaderCount = 256 * 1024;

    public int Length { get; init; }
    public byte Attributes { get; init; }
    public long TimestampDelta { get; init; }
    public int OffsetDelta { get; init; }
    public ReadOnlyMemory<byte> Key { get; init; }
    public ReadOnlyMemory<byte> Value { get; init; }
    public Header[]? Headers { get; init; }

    /// <summary>
    /// The number of valid headers in the Headers array.
    /// Required because the array may be rented from ArrayPool and oversized.
    /// </summary>
    public int HeaderCount { get; init; }

    private int RoutedHeaderIndex0 { get; init; }
    private int RoutedHeaderIndex1 { get; init; }
    private int RoutedHeaderTailOffset { get; init; }

    /// <summary>
    /// Returns true if the key is null (empty memory with special flag).
    /// </summary>
    public bool IsKeyNull { get; init; }

    /// <summary>
    /// Returns true if the value is null (empty memory with special flag).
    /// </summary>
    public bool IsValueNull { get; init; }

    /// <summary>
    /// Pre-computed body size to avoid redundant calculation during Write().
    /// Set at record creation time; 0 means not pre-computed (use CalculateBodySize()).
    /// </summary>
    internal int CachedBodySize { get; init; }

    /// <summary>
    /// Gets the effective header count, handling both exact-sized and pooled arrays.
    /// </summary>
    /// <remarks>
    /// Invariant: HeaderCount always equals the actual number of valid headers.
    /// When Headers is a pooled array (from Record.Read), HeaderCount &lt; Headers.Length is expected.
    /// When Headers is an exact-sized array (from producer path), HeaderCount == Headers.Length.
    /// </remarks>
    internal int EffectiveHeaderCount => Headers is null ? 0 : HeaderCount;

    /// <summary>
    /// Writes the record to the protocol writer.
    /// </summary>
    public void Write(ref KafkaProtocolWriter writer)
    {
        // First calculate the record body size
        var bodySize = CachedBodySize > 0
            ? CachedBodySize
            : ComputeBodySize(TimestampDelta, OffsetDelta, IsKeyNull, Key.Length, IsValueNull, Value.Length, Headers, EffectiveHeaderCount);

        // Write length as varint
        writer.WriteVarInt(bodySize);

        // Write attributes (always 0 for now)
        writer.WriteInt8((sbyte)Attributes);

        // Write timestamp delta as varlong (per Kafka spec)
        writer.WriteVarLong(TimestampDelta);

        // Write offset delta as varint
        writer.WriteVarInt(OffsetDelta);

        // Write key
        if (IsKeyNull)
        {
            writer.WriteVarInt(-1);
        }
        else
        {
            writer.WriteVarInt(Key.Length);
            writer.WriteRawBytes(Key.Span);
        }

        // Write value
        if (IsValueNull)
        {
            writer.WriteVarInt(-1);
        }
        else
        {
            writer.WriteVarInt(Value.Length);
            writer.WriteRawBytes(Value.Span);
        }

        // Write headers
        var effectiveHeaderCount = EffectiveHeaderCount;
        writer.WriteVarInt(effectiveHeaderCount);

        if (Headers is not null)
        {
            for (var i = 0; i < effectiveHeaderCount; i++)
            {
                HeaderProtocol.Write(in Headers[i], ref writer);
            }
        }
    }

    /// <summary>
    /// Reads a record from the protocol reader.
    /// The returned Record's Key and Value reference memory from the reader's buffer.
    /// </summary>
    public static Record Read(ref KafkaProtocolReader reader)
        => Read(ref reader, headerRoutingPlan: null);

    internal static Record Read(
        ref KafkaProtocolReader reader,
        RecordHeaderRoutingPlan? headerRoutingPlan)
    {
        var length = reader.ReadVarInt();
        if (length < 0)
            throw new MalformedProtocolDataException($"Invalid record length {length}");

        // The body is parsed in place with the caller's reader rather than through a
        // per-record sub-reader sliced to the declared length. Every variable-length field
        // is bounded by the declared body end before it is sliced, so a corrupt interior
        // field still cannot consume later records or masquerade as truncation (#2065).
        var availableBodyBytes = reader.Remaining;
        var bodyStart = reader.Consumed;

        try
        {
            return ReadBody(ref reader, length, bodyStart, headerRoutingPlan);
        }
        catch (RecordBodyLengthMismatchException ex)
        {
            throw new MalformedProtocolDataException(ex.Message, ex);
        }
        catch (InsufficientDataException ex) when (length <= availableBodyBytes)
        {
            throw new MalformedProtocolDataException("Record body cannot be parsed within its declared length", ex);
        }
        catch (MalformedProtocolDataException) when (length > availableBodyBytes)
        {
            throw new InsufficientDataException();
        }
    }

    private static Record ReadBody(
        ref KafkaProtocolReader reader,
        int length,
        long bodyStart,
        RecordHeaderRoutingPlan? headerRoutingPlan)
    {
        var bodyEnd = bodyStart + length;

        var attributes = (byte)reader.ReadInt8();
        var timestampDelta = reader.ReadVarLong();
        var offsetDelta = reader.ReadVarInt();

        var keyLength = reader.ReadVarInt();
        var isKeyNull = keyLength < 0;
        var key = isKeyNull ? ReadOnlyMemory<byte>.Empty : reader.ReadMemorySlice(keyLength, bodyEnd);

        var valueLength = reader.ReadVarInt();
        var isValueNull = valueLength < 0;
        var value = isValueNull ? ReadOnlyMemory<byte>.Empty : reader.ReadMemorySlice(valueLength, bodyEnd);

        var headerCount = reader.ReadVarInt();
        ValidateHeaderCount(headerCount, length, reader.Consumed - bodyStart, reader.Remaining);

        Header[]? headers = null;
        var routedHeaderIndex0 = 0;
        var routedHeaderIndex1 = 0;
        var routedHeaderTailOffset = headerRoutingPlan is null
            ? 0
            : RecordHeaderRoutingPlan.FullyIndexedWithoutTail;
        if (headerCount > 0)
        {
            // Rent from ArrayPool to avoid per-record allocation.
            // The rented array may be oversized; HeaderCount tracks the valid count.
            // The array is returned to the pool when the owning LazyRecordList is disposed.
            var routedHeaderTailCount = headerRoutingPlan is { Count: > 2 }
                ? headerRoutingPlan.GetRoutingTailCapacity(headerCount)
                : 0;
            headers = ArrayPool<Header>.Shared.Rent(checked(headerCount + routedHeaderTailCount));
            if (routedHeaderTailCount > 0)
            {
                routedHeaderTailOffset = headerCount;
                headers.AsSpan(routedHeaderTailOffset, routedHeaderTailCount).Clear();
            }
            try
            {
                for (var i = 0; i < headerCount; i++)
                {
                    var header = HeaderProtocol.Read(ref reader, bodyEnd);
                    headers[i] = header;
                    if (headerRoutingPlan is not null
                        && headerRoutingPlan.TryGetSlot(header.Key, out var slot))
                    {
                        var index = i + 1;
                        switch (slot)
                        {
                            case 0:
                                routedHeaderIndex0 = index;
                                break;
                            case 1:
                                routedHeaderIndex1 = index;
                                break;
                            default:
                                var mask = routedHeaderTailCount - 1;
                                var bucket = RecordHeaderRoutingPlan.GetRoutingTailBucket(slot, mask);
                                while (headers[routedHeaderTailOffset + bucket].Key is { } existingKey
                                       && !string.Equals(existingKey, header.Key, StringComparison.Ordinal))
                                {
                                    bucket = (bucket + 1) & mask;
                                }
                                headers[routedHeaderTailOffset + bucket] = header;
                                break;
                        }
                    }
                }

                ValidateBodyLength(length, reader.Consumed - bodyStart);
            }
            catch
            {
                ArrayPool<Header>.Shared.Return(headers, clearArray: true);
                throw;
            }
        }
        else
        {
            ValidateBodyLength(length, reader.Consumed - bodyStart);
        }

        return new Record
        {
            Length = length,
            Attributes = attributes,
            TimestampDelta = timestampDelta,
            OffsetDelta = offsetDelta,
            Key = key,
            IsKeyNull = isKeyNull,
            Value = value,
            IsValueNull = isValueNull,
            Headers = headers,
            HeaderCount = headerCount,
            RoutedHeaderIndex0 = routedHeaderIndex0,
            RoutedHeaderIndex1 = routedHeaderIndex1,
            RoutedHeaderTailOffset = routedHeaderTailOffset
        };
    }

    internal RecordHeaderRoutingLookup CreateHeaderRoutingLookup(
        RecordHeaderRoutingPlan? headerRoutingPlan) =>
        new(
            headerRoutingPlan,
            Headers,
            HeaderCount,
            RoutedHeaderIndex0,
            RoutedHeaderIndex1,
            RoutedHeaderTailOffset);

    internal Record IndexHeaders(RecordHeaderRoutingPlan headerRoutingPlan)
    {
        if (Headers is null || HeaderCount == 0)
            return this;

        var firstIndex = 0;
        var secondIndex = 0;
        for (var index = 0; index < HeaderCount; index++)
        {
            if (!headerRoutingPlan.TryGetSlot(Headers[index].Key, out var slot))
                continue;

            var encodedIndex = index + 1;
            switch (slot)
            {
                case 0:
                    firstIndex = encodedIndex;
                    break;
                case 1:
                    secondIndex = encodedIndex;
                    break;
            }
        }

        return this with
        {
            RoutedHeaderIndex0 = firstIndex,
            RoutedHeaderIndex1 = secondIndex,
            RoutedHeaderTailOffset = headerRoutingPlan.Count > 2
                ? RecordHeaderRoutingPlan.InlineSlotsOnly
                : RecordHeaderRoutingPlan.FullyIndexedWithoutTail
        };
    }

    internal Record IndexPooledHeaders(RecordHeaderRoutingPlan headerRoutingPlan)
    {
        if (Headers is null || HeaderCount == 0)
        {
            return this with
            {
                RoutedHeaderIndex0 = 0,
                RoutedHeaderIndex1 = 0,
                RoutedHeaderTailOffset = RecordHeaderRoutingPlan.FullyIndexedWithoutTail
            };
        }

        var headers = Headers;
        var routedHeaderTailCount = headerRoutingPlan.Count > 2
            ? headerRoutingPlan.GetRoutingTailCapacity(HeaderCount)
            : 0;
        if (routedHeaderTailCount > 0)
        {
            var requiredLength = checked(HeaderCount + routedHeaderTailCount);
            if (headers.Length < requiredLength)
            {
                var resizedHeaders = ArrayPool<Header>.Shared.Rent(requiredLength);
                headers.AsSpan(0, HeaderCount).CopyTo(resizedHeaders);
                ArrayPool<Header>.Shared.Return(headers, clearArray: true);
                headers = resizedHeaders;
            }

            headers.AsSpan(HeaderCount, routedHeaderTailCount).Clear();
        }

        var firstIndex = 0;
        var secondIndex = 0;
        for (var index = 0; index < HeaderCount; index++)
        {
            var header = headers[index];
            if (!headerRoutingPlan.TryGetSlot(header.Key, out var slot))
                continue;

            var encodedIndex = index + 1;
            switch (slot)
            {
                case 0:
                    firstIndex = encodedIndex;
                    break;
                case 1:
                    secondIndex = encodedIndex;
                    break;
                default:
                    var mask = routedHeaderTailCount - 1;
                    var bucket = RecordHeaderRoutingPlan.GetRoutingTailBucket(slot, mask);
                    while (headers[HeaderCount + bucket].Key is { } existingKey
                           && !string.Equals(existingKey, header.Key, StringComparison.Ordinal))
                    {
                        bucket = (bucket + 1) & mask;
                    }

                    headers[HeaderCount + bucket] = header;
                    break;
            }
        }

        return this with
        {
            Headers = headers,
            RoutedHeaderIndex0 = firstIndex,
            RoutedHeaderIndex1 = secondIndex,
            RoutedHeaderTailOffset = routedHeaderTailCount > 0
                ? HeaderCount
                : RecordHeaderRoutingPlan.FullyIndexedWithoutTail
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateBodyLength(int declaredLength, long consumedLength)
    {
        if (consumedLength != declaredLength)
            ThrowBodyLengthMismatch(declaredLength, consumedLength);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowBodyLengthMismatch(int declaredLength, long consumedLength)
    {
        // Fixed-width fields and varints are not pre-bounded, so a corrupt body can run past
        // its declared end before the mismatch is observed. That is the same condition a
        // reader bounded to the declared length would have reported as exhausted.
        if (consumedLength > declaredLength)
            KafkaProtocolReader.ThrowInsufficientData();

        throw new RecordBodyLengthMismatchException(
            $"Record body length mismatch: declared {declaredLength}, consumed {consumedLength}");
    }

    private sealed class RecordBodyLengthMismatchException(string message) : Exception(message);

    private static void ValidateHeaderCount(int headerCount, int recordBodyLength, long bodyBytesRead, long readerRemaining)
    {
        var remainingInRecord = recordBodyLength - bodyBytesRead;
        if (remainingInRecord < 0)
            KafkaProtocolReader.ThrowInsufficientData();
        if (headerCount < 0 || headerCount > MaxReasonableHeaderCount)
            throw new MalformedProtocolDataException($"Invalid record header count {headerCount}");

        // A header needs at least one byte for key length and one byte for value length.
        // Bound the declared record body by the actual readable bytes so corrupt frames
        // cannot force a giant ArrayPool rent before the first header read fails.
        var readableRecordBytes = Math.Min(remainingInRecord, readerRemaining);
        if (headerCount > readableRecordBytes / 2)
            throw new MalformedProtocolDataException($"Invalid record header count {headerCount}");
    }

    /// <summary>
    /// Copies headers from a (potentially pooled/oversized) array into an owned array
    /// that safely outlives the record batch. Returns null if no headers.
    /// </summary>
    internal static IReadOnlyList<Header>? CopyHeaders(Header[]? headers, int headerCount)
    {
        if (headers is null || headerCount == 0)
            return null;

        var result = new Header[headerCount];
        headers.AsSpan(0, headerCount).CopyTo(result);
        return result;
    }

    internal static int ComputeBodySize(long timestampDelta, int offsetDelta, bool isKeyNull, int keyLength, bool isValueNull, int valueLength, Header[]? headers, int headerCount)
    {
        var size = 1; // attributes

        size += VarLongSize(timestampDelta);
        size += VarIntSize(offsetDelta);

        if (isKeyNull)
        {
            size += VarIntSize(-1);
        }
        else
        {
            size += VarIntSize(keyLength);
            size += keyLength;
        }

        if (isValueNull)
        {
            size += VarIntSize(-1);
        }
        else
        {
            size += VarIntSize(valueLength);
            size += valueLength;
        }

        size += VarIntSize(headerCount);

        if (headers is not null)
        {
            for (var i = 0; i < headerCount; i++)
            {
                size += HeaderProtocol.CalculateSize(in headers[i]);
            }
        }

        return size;
    }

    /// <summary>
    /// Encodes a record directly into a fixed-size destination span using the Kafka record
    /// wire format (length varint + body). The destination length must equal
    /// <c>VarIntSize(bodySize) + bodySize</c> where <paramref name="bodySize"/> was computed by
    /// <see cref="ComputeBodySize"/> with the same arguments. Every byte written here must be
    /// counted there — keep the two methods in sync.
    /// </summary>
    internal static void Encode(
        Span<byte> destination,
        int bodySize,
        long timestampDelta,
        int offsetDelta,
        ReadOnlySpan<byte> keyData,
        bool isKeyNull,
        ReadOnlySpan<byte> valueData,
        bool isValueNull,
        Header[]? headers,
        int headerCount)
    {
        var offset = 0;

        WriteVarInt(destination, ref offset, bodySize);
        destination[offset++] = 0; // record attributes
        WriteVarLong(destination, ref offset, timestampDelta);
        WriteVarInt(destination, ref offset, offsetDelta);

        if (isKeyNull)
        {
            WriteVarInt(destination, ref offset, -1);
        }
        else
        {
            WriteVarInt(destination, ref offset, keyData.Length);
            keyData.CopyTo(destination[offset..]);
            offset += keyData.Length;
        }

        if (isValueNull)
        {
            WriteVarInt(destination, ref offset, -1);
        }
        else
        {
            WriteVarInt(destination, ref offset, valueData.Length);
            valueData.CopyTo(destination[offset..]);
            offset += valueData.Length;
        }

        WriteVarInt(destination, ref offset, headerCount);
        if (headers is not null)
        {
            for (var i = 0; i < headerCount; i++)
            {
                HeaderProtocol.Encode(in headers[i], destination, ref offset);
            }
        }

        if (offset != destination.Length)
            ThrowEncodedSizeMismatch(offset, destination.Length);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowEncodedSizeMismatch(int bytesWritten, int expectedBytes)
    {
        throw new InvalidOperationException(
            $"Record.Encode wrote {bytesWritten} bytes but destination length is {expectedBytes}. " +
            "Record.ComputeBodySize and Record.Encode are out of sync.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteVarInt(Span<byte> destination, ref int offset, int value)
    {
        var zigzag = (uint)((value << 1) ^ (value >> 31));
        while (zigzag >= 0x80)
        {
            destination[offset++] = (byte)(zigzag | 0x80);
            zigzag >>= 7;
        }

        destination[offset++] = (byte)zigzag;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteVarLong(Span<byte> destination, ref int offset, long value)
    {
        var zigzag = (ulong)((value << 1) ^ (value >> 63));
        while (zigzag >= 0x80)
        {
            destination[offset++] = (byte)(zigzag | 0x80);
            zigzag >>= 7;
        }

        destination[offset++] = (byte)zigzag;
    }

    internal static int VarIntSize(int value)
    {
        var zigzag = (uint)((value << 1) ^ (value >> 31));
        return VarUIntSize(zigzag);
    }

    internal static int VarLongSize(long value)
    {
        var zigzag = (ulong)((value << 1) ^ (value >> 63));
        return VarULongSize(zigzag);
    }

    internal static int VarUIntSize(uint value)
    {
        return (BitOperations.Log2(value | 1u) / 7) + 1;
    }

    internal static int VarULongSize(ulong value)
    {
        return (BitOperations.Log2(value | 1ul) / 7) + 1;
    }
}
