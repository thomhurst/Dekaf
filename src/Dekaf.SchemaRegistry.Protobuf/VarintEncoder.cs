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

        var size = 1;
        var v = (uint)value;
        while (v >= 0x80)
        {
            size++;
            v >>= 7;
        }
        return size;
    }

    /// <summary>
    /// Writes an integer as a base-128 varint into the given span.
    /// Protobuf message indexes are always non-negative.
    /// </summary>
    /// <returns>The number of bytes written.</returns>
    internal static int WriteVarint(Span<byte> span, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        var size = CalculateVarintSize(value);
        var v = (uint)value;
        for (var i = 0; i < size - 1; i++)
        {
            span[i] = (byte)(v | 0x80);
            v >>= 7;
        }
        span[size - 1] = (byte)v;
        return size;
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
}
