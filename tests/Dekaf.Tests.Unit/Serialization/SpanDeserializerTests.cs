using System.Buffers;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Serialization;

/// <summary>
/// Verifies that IDeserializer&lt;T&gt;.Deserialize works correctly with ReadOnlyMemory&lt;byte&gt;
/// across all built-in serdes.
/// </summary>
public class SpanDeserializerTests
{
    private static SerializationContext CreateContext(string topic = "test") =>
        new() { Topic = topic, Component = SerializationComponent.Value };

    [Test]
    public async Task StringSerde_Deserialize_RoundTrips()
    {
        var serde = Serializers.String;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serde.Serialize("hello world", ref buffer, context);

        var result = serde.Deserialize(buffer.WrittenMemory, context);

        await Assert.That(result).IsEqualTo("hello world");
    }

    [Test]
    public async Task Int32Serde_Deserialize_RoundTrips()
    {
        var serde = Serializers.Int32;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serde.Serialize(42, ref buffer, context);

        var result = serde.Deserialize(buffer.WrittenMemory, context);

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task Int64Serde_Deserialize_RoundTrips()
    {
        var serde = Serializers.Int64;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serde.Serialize(9876543210L, ref buffer, context);

        var result = serde.Deserialize(buffer.WrittenMemory, context);

        await Assert.That(result).IsEqualTo(9876543210L);
    }

    [Test]
    public async Task GuidSerde_Deserialize_RoundTrips()
    {
        var serde = Serializers.Guid;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var guid = Guid.NewGuid();

        serde.Serialize(guid, ref buffer, context);

        var result = serde.Deserialize(buffer.WrittenMemory, context);

        await Assert.That(result).IsEqualTo(guid);
    }

    [Test]
    public async Task DoubleSerde_Deserialize_RoundTrips()
    {
        var serde = Serializers.Double;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serde.Serialize(3.14159, ref buffer, context);

        var result = serde.Deserialize(buffer.WrittenMemory, context);

        await Assert.That(result).IsEqualTo(3.14159);
    }

    [Test]
    public async Task FloatSerde_Deserialize_RoundTrips()
    {
        var serde = Serializers.Float;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serde.Serialize(1.5f, ref buffer, context);

        var result = serde.Deserialize(buffer.WrittenMemory, context);

        await Assert.That(result).IsEqualTo(1.5f);
    }

    [Test]
    public async Task ByteArraySerde_Deserialize_RoundTrips()
    {
        var serde = Serializers.ByteArray;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var input = new byte[] { 1, 2, 3, 4, 5 };

        serde.Serialize(input, ref buffer, context);

        var result = serde.Deserialize(buffer.WrittenMemory, context);

        await Assert.That(result).IsEquivalentTo(input);
    }

    [Test]
    public async Task DateTimeSerde_Deserialize_RoundTrips()
    {
        var serde = Serializers.DateTime;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var value = new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc);

        serde.Serialize(value, ref buffer, context);

        var result = serde.Deserialize(buffer.WrittenMemory, context);

        await Assert.That(result).IsEqualTo(value);
    }

    [Test]
    public async Task DateTimeOffsetSerde_Deserialize_RoundTrips()
    {
        var serde = Serializers.DateTimeOffset;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var value = new DateTimeOffset(2024, 6, 15, 14, 30, 45, new TimeSpan(5, 30, 0));

        serde.Serialize(value, ref buffer, context);

        var result = serde.Deserialize(buffer.WrittenMemory, context);

        await Assert.That(result).IsEqualTo(value);
    }

    [Test]
    public async Task TimeSpanSerde_Deserialize_RoundTrips()
    {
        var serde = Serializers.TimeSpan;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var value = TimeSpan.FromHours(2.5);

        serde.Serialize(value, ref buffer, context);

        var result = serde.Deserialize(buffer.WrittenMemory, context);

        await Assert.That(result).IsEqualTo(value);
    }

    [Test]
    public async Task NullableStringSerde_Deserialize_NonNull()
    {
        var serde = Serializers.NullableString;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serde.Serialize("test value", ref buffer, context);

        var result = serde.Deserialize(buffer.WrittenMemory, context);

        await Assert.That(result).IsEqualTo("test value");
    }

    [Test]
    public async Task NullableStringSerde_Deserialize_Null()
    {
        var serde = Serializers.NullableString;
        var context = new SerializationContext
        {
            Topic = "test",
            Component = SerializationComponent.Value,
            IsNull = true
        };

        var result = serde.Deserialize(ReadOnlyMemory<byte>.Empty, context);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task RawBytesSerde_Deserialize_ReturnsMemory()
    {
        var serde = Serializers.RawBytes;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var input = new ReadOnlyMemory<byte>([1, 2, 3, 4, 5]);

        serde.Serialize(input, ref buffer, context);

        var result = serde.Deserialize(buffer.WrittenMemory, context);

        await Assert.That(result.ToArray()).IsEquivalentTo(input.ToArray());
    }
}
