using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Dekaf.Serialization;

/// <summary>
/// Built-in serializers for common types.
/// </summary>
public static class Serializers
{
    /// <summary>
    /// Serializer for byte arrays (pass-through).
    /// </summary>
    public static ISerde<byte[]> ByteArray { get; } = new ByteArraySerde();

    /// <summary>
    /// Serializer for strings using UTF-8.
    /// </summary>
    public static ISerde<string> String { get; } = new StringSerde();

    /// <summary>
    /// Serializer for nullable strings using UTF-8.
    /// </summary>
    public static ISerde<string?> NullableString { get; } = new NullableStringSerde();

    /// <summary>
    /// Serializer for 32-bit integers.
    /// </summary>
    public static ISerde<int> Int32 { get; } = new Int32Serde();

    /// <summary>
    /// Serializer for 64-bit integers.
    /// </summary>
    public static ISerde<long> Int64 { get; } = new Int64Serde();

    /// <summary>
    /// Serializer for GUIDs.
    /// </summary>
    public static ISerde<Guid> Guid { get; } = new GuidSerde();

    /// <summary>
    /// Serializer for doubles.
    /// </summary>
    public static ISerde<double> Double { get; } = new DoubleSerde();

    /// <summary>
    /// Serializer for single-precision floats.
    /// </summary>
    public static ISerde<float> Float { get; } = new FloatSerde();

    /// <summary>
    /// Serializer for DateTime values. Serialized as UTC ticks (8 bytes).
    /// </summary>
    public static ISerde<DateTime> DateTime { get; } = new DateTimeSerde();

    /// <summary>
    /// Serializer for DateTimeOffset values. Serialized as ticks (8 bytes) + offset minutes (2 bytes).
    /// </summary>
    public static ISerde<DateTimeOffset> DateTimeOffset { get; } = new DateTimeOffsetSerde();

    /// <summary>
    /// Serializer for TimeSpan values. Serialized as ticks (8 bytes).
    /// </summary>
    public static ISerde<TimeSpan> TimeSpan { get; } = new TimeSpanSerde();

    /// <summary>
    /// Zero-copy deserializer that returns raw bytes as ReadOnlyMemory.
    /// </summary>
    /// <remarks>
    /// <para>This deserializer avoids all allocations by returning a slice of the underlying
    /// network buffer directly. This is ideal for high-throughput scenarios where you need
    /// to process raw bytes without string conversion overhead.</para>
    ///
    /// <para><b>Important lifetime considerations:</b></para>
    /// <list type="bullet">
    /// <item>The returned memory is only valid while consuming the current message batch</item>
    /// <item>If you need to keep the data longer, copy it: <c>data.ToArray()</c></item>
    /// <item>Do not store references to the returned memory across consume iterations</item>
    /// </list>
    ///
    /// <para><b>Usage example:</b></para>
    /// <code>
    /// var consumer = Dekaf.CreateConsumer&lt;ReadOnlyMemory&lt;byte&gt;, ReadOnlyMemory&lt;byte&gt;&gt;()
    ///     .WithBootstrapServers("localhost:9092")
    ///     .WithGroupId("my-group")
    ///     .Build();
    /// </code>
    /// </remarks>
    public static ISerde<ReadOnlyMemory<byte>> RawBytes { get; } = new RawBytesSerde();

    /// <summary>
    /// Creates a null serializer that returns default values.
    /// </summary>
    public static ISerde<T?> Null<T>() where T : class => new NullSerde<T>();

    /// <summary>
    /// Creates a void serializer that ignores values.
    /// </summary>
    public static ISerde<Ignore> Ignore { get; } = new IgnoreSerde();
}

/// <summary>
/// Represents a value to be ignored during serialization.
/// </summary>
public readonly struct Ignore;

internal sealed class ByteArraySerde : ISerde<byte[]>
{
    public void Serialize<TWriter>(byte[] value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var span = destination.GetSpan(value.Length);
        value.CopyTo(span);
        destination.Advance(value.Length);
    }

    public byte[] Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        return data.ToArray();
    }
}

internal sealed class StringSerde : ISerde<string>
{
    public void Serialize<TWriter>(string value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        var span = destination.GetSpan(byteCount);
        Encoding.UTF8.GetBytes(value, span);
        destination.Advance(byteCount);
    }

    public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        return Encoding.UTF8.GetString(data.Span);
    }
}

internal sealed class NullableStringSerde : ISerde<string?>
{
    public void Serialize<TWriter>(string? value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        if (value is null)
            return;

        var byteCount = Encoding.UTF8.GetByteCount(value);
        var span = destination.GetSpan(byteCount);
        Encoding.UTF8.GetBytes(value, span);
        destination.Advance(byteCount);
    }

    public string? Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        if (context.IsNull)
            return null;

        if (data.Length == 0)
            return string.Empty;

        return Encoding.UTF8.GetString(data.Span);
    }
}

internal sealed class Int32Serde : ISerde<int>
{
    public void Serialize<TWriter>(int value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var span = destination.GetSpan(4);
        BinaryPrimitives.WriteInt32BigEndian(span, value);
        destination.Advance(4);
    }

    public int Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        return BinaryPrimitives.ReadInt32BigEndian(data.Span);
    }
}

internal sealed class Int64Serde : ISerde<long>
{
    public void Serialize<TWriter>(long value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var span = destination.GetSpan(8);
        BinaryPrimitives.WriteInt64BigEndian(span, value);
        destination.Advance(8);
    }

    public long Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        return BinaryPrimitives.ReadInt64BigEndian(data.Span);
    }
}

internal sealed class GuidSerde : ISerde<Guid>
{
    public void Serialize<TWriter>(Guid value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var span = destination.GetSpan(16);
        value.TryWriteBytes(span, bigEndian: true, out _);
        destination.Advance(16);
    }

    public Guid Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        return new Guid(data.Span, bigEndian: true);
    }
}

internal sealed class DoubleSerde : ISerde<double>
{
    public void Serialize<TWriter>(double value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var span = destination.GetSpan(8);
        BinaryPrimitives.WriteDoubleBigEndian(span, value);
        destination.Advance(8);
    }

    public double Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        return BinaryPrimitives.ReadDoubleBigEndian(data.Span);
    }
}

internal sealed class FloatSerde : ISerde<float>
{
    public void Serialize<TWriter>(float value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var span = destination.GetSpan(4);
        BinaryPrimitives.WriteSingleBigEndian(span, value);
        destination.Advance(4);
    }

    public float Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        return BinaryPrimitives.ReadSingleBigEndian(data.Span);
    }
}

internal sealed class DateTimeSerde : ISerde<DateTime>
{
    public void Serialize<TWriter>(DateTime value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var span = destination.GetSpan(8);
        BinaryPrimitives.WriteInt64BigEndian(span, value.ToUniversalTime().Ticks);
        destination.Advance(8);
    }

    public DateTime Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        var ticks = BinaryPrimitives.ReadInt64BigEndian(data.Span);
        return new DateTime(ticks, DateTimeKind.Utc);
    }
}

internal sealed class DateTimeOffsetSerde : ISerde<DateTimeOffset>
{
    public void Serialize<TWriter>(DateTimeOffset value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var span = destination.GetSpan(10);
        BinaryPrimitives.WriteInt64BigEndian(span, value.UtcTicks);
        BinaryPrimitives.WriteInt16BigEndian(span.Slice(8), (short)(value.Offset.TotalMinutes));
        destination.Advance(10);
    }

    public DateTimeOffset Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        var span = data.Span;
        var utcTicks = BinaryPrimitives.ReadInt64BigEndian(span);
        var offsetMinutes = BinaryPrimitives.ReadInt16BigEndian(span.Slice(8));
        var offset = TimeSpan.FromMinutes(offsetMinutes);
        return new DateTimeOffset(utcTicks + offset.Ticks, offset);
    }
}

internal sealed class TimeSpanSerde : ISerde<TimeSpan>
{
    public void Serialize<TWriter>(TimeSpan value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var span = destination.GetSpan(8);
        BinaryPrimitives.WriteInt64BigEndian(span, value.Ticks);
        destination.Advance(8);
    }

    public TimeSpan Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        var ticks = BinaryPrimitives.ReadInt64BigEndian(data.Span);
        return new TimeSpan(ticks);
    }
}

internal sealed class NullSerde<T> : ISerde<T?> where T : class
{
    public void Serialize<TWriter>(T? value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        // No-op
    }

    public T? Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        return null;
    }
}

internal sealed class IgnoreSerde : ISerde<Ignore>
{
    public void Serialize<TWriter>(Ignore value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        // No-op
    }

    public Ignore Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        return default;
    }
}

/// <summary>
/// Serde that works with ReadOnlyMemory for raw byte access.
/// </summary>
/// <remarks>
/// Since <see cref="IDeserializer{T}.Deserialize"/> takes <see cref="ReadOnlyMemory{T}"/>,
/// deserialization returns the input directly — true zero-copy with no allocation.
/// The returned memory is only valid while consuming the current message batch.
/// </remarks>
internal sealed class RawBytesSerde : ISerde<ReadOnlyMemory<byte>>
{
    public void Serialize<TWriter>(ReadOnlyMemory<byte> value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var span = destination.GetSpan(value.Length);
        value.Span.CopyTo(span);
        destination.Advance(value.Length);
    }

    public ReadOnlyMemory<byte> Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        return data;
    }
}
