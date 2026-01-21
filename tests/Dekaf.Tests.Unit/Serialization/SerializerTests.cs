using System.Buffers;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Serialization;

public class SerializerTests
{
    private static SerializationContext CreateContext(string topic = "test") =>
        new() { Topic = topic, Component = SerializationComponent.Value };

    [Test]
    public async Task StringSerializer_SerializesAndDeserializes()
    {
        var serializer = Serializers.String;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize("hello world", buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo("hello world");
    }

    [Test]
    public async Task Int32Serializer_SerializesAndDeserializes()
    {
        var serializer = Serializers.Int32;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(42, buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task Int64Serializer_SerializesAndDeserializes()
    {
        var serializer = Serializers.Int64;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(9876543210L, buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(9876543210L);
    }

    [Test]
    public async Task GuidSerializer_SerializesAndDeserializes()
    {
        var serializer = Serializers.Guid;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var guid = Guid.NewGuid();

        serializer.Serialize(guid, buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(guid);
    }

    [Test]
    public async Task ByteArraySerializer_SerializesAndDeserializes()
    {
        var serializer = Serializers.ByteArray;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var data = new byte[] { 1, 2, 3, 4, 5 };

        serializer.Serialize(data, buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEquivalentTo(data);
    }

    [Test]
    public async Task DoubleSerializer_SerializesAndDeserializes()
    {
        var serializer = Serializers.Double;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(3.14159, buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(3.14159);
    }
}
