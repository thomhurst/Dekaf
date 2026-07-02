namespace Dekaf.SchemaRegistry.Protobuf;

/// <summary>
/// Provides methods for encoding integers as Protocol Buffer base-128 varints.
/// Used internally by the Protobuf Schema Registry serializer to encode message index arrays
/// in the Confluent wire format.
/// </summary>
internal static class VarintEncoder
{
    /// <summary>
    /// Calculates the number of bytes required to encode an integer as a varint.
    /// This is the single source of truth for varint size computation, used by both
    /// <see cref="CalculateVarintArraySize"/> and <see cref="WriteVarint"/>.
    /// </summary>
    internal static int CalculateVarintSize(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        return CalculateUnsignedVarintSize((uint)value);
    }

    /// <summary>
    /// Writes an integer as a base-128 varint into the given span.
    /// Protobuf message indexes are always non-negative.
    /// </summary>
    /// <returns>The number of bytes written.</returns>
    internal static int WriteVarint(Span<byte> span, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        return WriteUnsignedVarint(span, (uint)value);
    }

    /// <summary>
    /// Calculates the total byte size of a varint-encoded array (count prefix + elements).
    /// </summary>
    internal static int CalculateVarintArraySize(int[] values)
    {
        // First, write the count of elements as varint
        var size = CalculateVarintSize(values.Length);

        // Then write each value as varint
        foreach (var value in values)
        {
            size += CalculateVarintSize(value);
        }

        return size;
    }

    /// <summary>
    /// Writes a varint-encoded array into the given span (count prefix + elements).
    /// </summary>
    /// <returns>The number of bytes written.</returns>
    internal static int WriteVarintArray(Span<byte> span, int[] values)
    {
        var written = 0;

        // Write the count first
        written += WriteVarint(span.Slice(written), values.Length);

        // Write each value
        foreach (var value in values)
        {
            written += WriteVarint(span.Slice(written), value);
        }

        return written;
    }

    /// <summary>
    /// Pre-encodes Confluent Protobuf message indexes.
    /// </summary>
    internal static byte[] EncodeMessageIndexes(int[] values, bool useDeprecatedFormat = false)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Length == 0)
            throw new ArgumentException("Message index array cannot be empty.", nameof(values));

        if (values.Length == 1 && values[0] == 0)
            return [0x00];

        var bytes = new byte[CalculateMessageIndexArraySize(values, useDeprecatedFormat)];
        WriteMessageIndexArray(bytes, values, useDeprecatedFormat);
        return bytes;
    }

    private static int CalculateMessageIndexArraySize(int[] values, bool useDeprecatedFormat)
    {
        var size = CalculateMessageIndexVarintSize(values.Length, useDeprecatedFormat);

        foreach (var value in values)
        {
            size += CalculateMessageIndexVarintSize(value, useDeprecatedFormat);
        }

        return size;
    }

    private static int WriteMessageIndexArray(Span<byte> span, int[] values, bool useDeprecatedFormat)
    {
        var written = WriteMessageIndexVarint(span, values.Length, useDeprecatedFormat);

        foreach (var value in values)
        {
            written += WriteMessageIndexVarint(span.Slice(written), value, useDeprecatedFormat);
        }

        return written;
    }

    private static int CalculateMessageIndexVarintSize(int value, bool useDeprecatedFormat)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        return useDeprecatedFormat
            ? CalculateUnsignedVarintSize((uint)value)
            : CalculateUnsignedVarintSize(EncodeZigZag(value));
    }

    private static int WriteMessageIndexVarint(Span<byte> span, int value, bool useDeprecatedFormat)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        return WriteUnsignedVarint(span, useDeprecatedFormat ? (uint)value : EncodeZigZag(value));
    }

    private static int CalculateUnsignedVarintSize(uint value)
    {
        var size = 1;
        while (value >= 0x80)
        {
            size++;
            value >>= 7;
        }

        return size;
    }

    private static int WriteUnsignedVarint(Span<byte> span, uint value)
    {
        var written = 0;
        while (value >= 0x80)
        {
            span[written++] = (byte)((value & 0x7F) | 0x80);
            value >>= 7;
        }

        span[written++] = (byte)value;
        return written;
    }

    private static uint EncodeZigZag(int value)
    {
        return (uint)((value << 1) ^ (value >> 31));
    }
}
