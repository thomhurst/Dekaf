using System.Buffers.Binary;

namespace Dekaf.SchemaRegistry.Avro.Poco;

/// <summary>Allocation-free Avro decimal logical-type codec for CLR <see cref="decimal"/>.</summary>
public static class AvroDecimalCodec
{
    /// <summary>Writes a decimal using Avro's signed, big-endian unscaled representation.</summary>
    public static void Write(ref AvroValueWriter writer, decimal value, int precision, int scale)
    {
        Validate(precision, scale);

        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        var magnitude = new UInt96((uint)bits[0], (uint)bits[1], (uint)bits[2]);
        var valueScale = (bits[3] >> 16) & 0x7F;
        while (valueScale < scale)
        {
            if (!magnitude.TryMultiplyBy10())
                throw new OverflowException("Decimal cannot be represented at the configured Avro scale.");
            valueScale++;
        }
        while (valueScale > scale)
        {
            if (magnitude.DivideBy10() != 0)
                throw new InvalidOperationException("Decimal has more fractional digits than the configured Avro scale.");
            valueScale--;
        }

        if (magnitude.CountDigits() > precision)
            throw new OverflowException("Decimal exceeds the configured Avro precision.");

        Span<byte> encoded = stackalloc byte[13];
        encoded[0] = 0;
        BinaryPrimitives.WriteUInt32BigEndian(encoded.Slice(1, 4), magnitude.High);
        BinaryPrimitives.WriteUInt32BigEndian(encoded.Slice(5, 4), magnitude.Middle);
        BinaryPrimitives.WriteUInt32BigEndian(encoded.Slice(9, 4), magnitude.Low);
        var negative = (bits[3] & int.MinValue) != 0 && !magnitude.IsZero;
        if (negative)
        {
            encoded[0] = 0;
            var carry = 1;
            for (var index = encoded.Length - 1; index >= 0; index--)
            {
                var current = (~encoded[index] & 0xFF) + carry;
                encoded[index] = (byte)current;
                carry = current >> 8;
            }
        }

        var start = 0;
        var signExtension = negative ? (byte)0xFF : (byte)0;
        var signMask = negative ? 0x80 : 0;
        while (start < encoded.Length - 1 && encoded[start] == signExtension &&
               (encoded[start + 1] & 0x80) == signMask)
        {
            start++;
        }

        writer.WriteBytes(encoded.Slice(start));
    }

    /// <summary>Reads a decimal without allocating an intermediate byte array.</summary>
    public static decimal Read(ref AvroValueReader reader, int precision, int scale)
    {
        Validate(precision, scale);
        var encoded = reader.ReadBytesSpan();
        return Decode(encoded, precision, scale);
    }

    /// <summary>Reads a decimal using the cached writer node to distinguish bytes from fixed encoding.</summary>
    public static decimal Read(
        ref AvroValueReader reader,
        int precision,
        int scale,
        AvroPocoReadNode writerType)
    {
        Validate(precision, scale);
        var encoded = writerType.FixedSize > 0
            ? reader.ReadFixed(writerType.FixedSize)
            : reader.ReadBytesSpan();
        return Decode(encoded, precision, scale);
    }

    private static decimal Decode(ReadOnlySpan<byte> encoded, int precision, int scale)
    {
        if (encoded.IsEmpty)
            throw new InvalidDataException("Avro decimal byte sequence cannot be empty.");

        var negative = (encoded[0] & 0x80) != 0;
        while (encoded.Length > 13 && encoded[0] == (negative ? (byte)0xFF : (byte)0) &&
               (encoded[1] & 0x80) == (negative ? 0x80 : 0))
        {
            encoded = encoded.Slice(1);
        }
        if (encoded.Length > 13)
            throw new OverflowException("Avro decimal exceeds CLR decimal range.");

        Span<byte> normalized = stackalloc byte[13];
        normalized.Fill(negative ? (byte)0xFF : (byte)0);
        encoded.CopyTo(normalized.Slice(normalized.Length - encoded.Length));
        if (negative)
        {
            var carry = 1;
            for (var index = normalized.Length - 1; index >= 0; index--)
            {
                var current = (~normalized[index] & 0xFF) + carry;
                normalized[index] = (byte)current;
                carry = current >> 8;
            }
        }

        if (normalized[0] != 0)
            throw new OverflowException("Avro decimal exceeds CLR decimal range.");

        var magnitude = new UInt96(
            BinaryPrimitives.ReadUInt32BigEndian(normalized.Slice(9, 4)),
            BinaryPrimitives.ReadUInt32BigEndian(normalized.Slice(5, 4)),
            BinaryPrimitives.ReadUInt32BigEndian(normalized.Slice(1, 4)));
        if (magnitude.CountDigits() > precision)
            throw new OverflowException("Avro decimal exceeds the configured precision.");

        return new decimal((int)magnitude.Low, (int)magnitude.Middle, (int)magnitude.High, negative, (byte)scale);
    }

    private static void Validate(int precision, int scale)
    {
        if (precision is < 1 or > 29)
            throw new ArgumentOutOfRangeException(nameof(precision), "CLR decimal precision must be between 1 and 29.");
        if (scale < 0 || scale > 28 || scale > precision)
            throw new ArgumentOutOfRangeException(nameof(scale), "CLR decimal scale must be between 0 and 28 and no greater than precision.");
    }

    private struct UInt96(uint low, uint middle, uint high)
    {
        internal uint Low = low;
        internal uint Middle = middle;
        internal uint High = high;

        internal readonly bool IsZero => (Low | Middle | High) == 0;

        internal bool TryMultiplyBy10()
        {
            var product = (ulong)Low * 10;
            Low = (uint)product;
            product = (ulong)Middle * 10 + (product >> 32);
            Middle = (uint)product;
            product = (ulong)High * 10 + (product >> 32);
            High = (uint)product;
            return (product >> 32) == 0;
        }

        internal uint DivideBy10()
        {
            ulong dividend = High;
            High = (uint)(dividend / 10);
            var remainder = dividend % 10;
            dividend = (remainder << 32) | Middle;
            Middle = (uint)(dividend / 10);
            remainder = dividend % 10;
            dividend = (remainder << 32) | Low;
            Low = (uint)(dividend / 10);
            return (uint)(dividend % 10);
        }

        internal readonly int CountDigits()
        {
            if (IsZero)
                return 1;
            var copy = this;
            var digits = 0;
            while (!copy.IsZero)
            {
                _ = copy.DivideBy10();
                digits++;
            }
            return digits;
        }
    }
}
