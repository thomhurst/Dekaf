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

    public byte[] Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
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

    public string Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        if (data.IsSingleSegment)
        {
            return Encoding.UTF8.GetString(data.FirstSpan);
        }

        // Use ArrayPool for multi-segment case to avoid allocation
        var length = (int)data.Length;
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            data.CopyTo(buffer);
            return Encoding.UTF8.GetString(buffer.AsSpan(0, length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
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

    public string? Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        if (context.IsNull)
            return null;

        if (data.Length == 0)
            return string.Empty;

        if (data.IsSingleSegment)
        {
            return Encoding.UTF8.GetString(data.FirstSpan);
        }

        // Use ArrayPool for multi-segment case to avoid allocation
        var length = (int)data.Length;
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            data.CopyTo(buffer);
            return Encoding.UTF8.GetString(buffer.AsSpan(0, length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
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

    public int Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        Span<byte> buffer = stackalloc byte[4];
        data.Slice(0, 4).CopyTo(buffer);
        return BinaryPrimitives.ReadInt32BigEndian(buffer);
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

    public long Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        Span<byte> buffer = stackalloc byte[8];
        data.Slice(0, 8).CopyTo(buffer);
        return BinaryPrimitives.ReadInt64BigEndian(buffer);
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

    public Guid Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        Span<byte> buffer = stackalloc byte[16];
        data.Slice(0, 16).CopyTo(buffer);
        return new Guid(buffer, bigEndian: true);
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

    public double Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        Span<byte> buffer = stackalloc byte[8];
        data.Slice(0, 8).CopyTo(buffer);
        return BinaryPrimitives.ReadDoubleBigEndian(buffer);
    }
}

internal sealed class NullSerde<T> : ISerde<T?> where T : class
{
    public void Serialize<TWriter>(T? value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        // No-op
    }

    public T? Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
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

    public Ignore Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        return default;
    }
}

/// <summary>
/// Zero-copy serde that works directly with ReadOnlyMemory without allocations.
/// </summary>
internal sealed class RawBytesSerde : ISerde<ReadOnlyMemory<byte>>
{
    public void Serialize<TWriter>(ReadOnlyMemory<byte> value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var span = destination.GetSpan(value.Length);
        value.Span.CopyTo(span);
        destination.Advance(value.Length);
    }

    public ReadOnlyMemory<byte> Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        // Fast path: single segment - return memory slice directly (zero-copy)
        if (data.IsSingleSegment)
        {
            return data.First;
        }

        // Slow path: multi-segment - must copy to contiguous memory
        // This is rare in practice since consumer data is typically single-segment
        return data.ToArray();
    }
}
