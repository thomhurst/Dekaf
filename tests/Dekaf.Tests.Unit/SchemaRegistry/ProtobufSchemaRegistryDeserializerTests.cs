using System.Buffers;
using System.Buffers.Binary;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Google.Protobuf;
using NSubstitute;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public class ProtobufSchemaRegistryDeserializerTests
{
    private static SerializationContext CreateContext(string topic = "test-topic", bool isKey = false) =>
        new() { Topic = topic, Component = isKey ? SerializationComponent.Key : SerializationComponent.Value };

    [Test]
    public async Task Deserialize_ReadsCorrectWireFormat()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        schemaRegistry.GetSchemaAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Schema
            {
                SchemaType = SchemaType.Protobuf,
                SchemaString = "syntax = \"proto3\";"
            }));

        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(schemaRegistry);

        // Create a message to serialize
        var originalMessage = new TestMessage { Id = 42, Name = "Hello", Value = 3.14 };

        // Build the wire format manually
        var protoBytes = originalMessage.ToByteArray();
        var wireBytes = new byte[1 + 4 + 2 + protoBytes.Length]; // magic + schemaId + indexes + proto

        wireBytes[0] = 0x00; // Magic byte
        BinaryPrimitives.WriteInt32BigEndian(wireBytes.AsSpan(1, 4), 123); // Schema ID
        wireBytes[5] = 1; // Array length (1 index)
        wireBytes[6] = 0; // Index 0
        protoBytes.CopyTo(wireBytes.AsSpan(7));

        // Act
        var result = deserializer.Deserialize(new ReadOnlySequence<byte>(wireBytes), CreateContext());

        // Assert
        await Assert.That(result.Id).IsEqualTo(42);
        await Assert.That(result.Name).IsEqualTo("Hello");
        await Assert.That(result.Value).IsEqualTo(3.14);
    }

    [Test]
    public async Task Deserialize_ThrowsOnInvalidMagicByte()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(schemaRegistry);

        var invalidBytes = new byte[] { 0x01, 0, 0, 0, 123, 1, 0 }; // Invalid magic byte

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.FromResult(deserializer.Deserialize(new ReadOnlySequence<byte>(invalidBytes), CreateContext())));

        await Assert.That(exception!.Message).Contains("Unknown magic byte");
    }

    [Test]
    public async Task Deserialize_ThrowsOnTooShortMessage()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(schemaRegistry);

        var shortBytes = new byte[] { 0x00, 0, 0 }; // Only 3 bytes

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.FromResult(deserializer.Deserialize(new ReadOnlySequence<byte>(shortBytes), CreateContext())));

        await Assert.That(exception!.Message).Contains("too short");
    }

    [Test]
    public async Task Deserialize_ValidatesSchemaType()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        schemaRegistry.GetSchemaAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Schema
            {
                SchemaType = SchemaType.Json, // Wrong type!
                SchemaString = "{}"
            }));

        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(schemaRegistry);

        var wireBytes = new byte[] { 0x00, 0, 0, 0, 42, 1, 0, 8, 1 }; // Valid format with minimal proto

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.FromResult(deserializer.Deserialize(new ReadOnlySequence<byte>(wireBytes), CreateContext())));

        await Assert.That(exception!.Message).Contains("not a Protobuf schema");
    }

    [Test]
    public async Task Deserialize_SkipsSchemaValidation()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        var config = new ProtobufDeserializerConfig { SkipSchemaValidation = true };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(schemaRegistry, config);

        // Create a valid wire format message
        var originalMessage = new TestMessage { Id = 42, Name = "Hello", Value = 3.14 };
        var protoBytes = originalMessage.ToByteArray();
        var wireBytes = new byte[1 + 4 + 2 + protoBytes.Length];

        wireBytes[0] = 0x00;
        BinaryPrimitives.WriteInt32BigEndian(wireBytes.AsSpan(1, 4), 123);
        wireBytes[5] = 1;
        wireBytes[6] = 0;
        protoBytes.CopyTo(wireBytes.AsSpan(7));

        // Act
        var result = deserializer.Deserialize(new ReadOnlySequence<byte>(wireBytes), CreateContext());

        // Assert - schema registry should not be called
        await schemaRegistry.DidNotReceive().GetSchemaAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await Assert.That(result.Id).IsEqualTo(42);
    }

    [Test]
    public async Task Deserialize_HandlesMultiSegmentSequence()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        schemaRegistry.GetSchemaAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Schema
            {
                SchemaType = SchemaType.Protobuf,
                SchemaString = "syntax = \"proto3\";"
            }));

        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(schemaRegistry);

        // Create a message
        var originalMessage = new TestMessage { Id = 42, Name = "Test", Value = 1.5 };
        var protoBytes = originalMessage.ToByteArray();
        var wireBytes = new byte[1 + 4 + 2 + protoBytes.Length];

        wireBytes[0] = 0x00;
        BinaryPrimitives.WriteInt32BigEndian(wireBytes.AsSpan(1, 4), 123);
        wireBytes[5] = 1;
        wireBytes[6] = 0;
        protoBytes.CopyTo(wireBytes.AsSpan(7));

        // Split into multiple segments
        var firstSegment = new TestSegment(wireBytes.AsMemory(0, 5));
        var secondSegment = new TestSegment(wireBytes.AsMemory(5));
        firstSegment.SetNext(secondSegment);

        var sequence = new ReadOnlySequence<byte>(firstSegment, 0, secondSegment, secondSegment.Memory.Length);

        // Act
        var result = deserializer.Deserialize(sequence, CreateContext());

        // Assert
        await Assert.That(result.Id).IsEqualTo(42);
        await Assert.That(result.Name).IsEqualTo("Test");
    }

    [Test]
    public async Task DisposeAsync_DisposesOwnedClient()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(schemaRegistry, ownsClient: true);

        // Act
        await deserializer.DisposeAsync();

        // Assert
        schemaRegistry.Received(1).Dispose();
    }

    [Test]
    public async Task DisposeAsync_DoesNotDisposeNonOwnedClient()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(schemaRegistry, ownsClient: false);

        // Act
        await deserializer.DisposeAsync();

        // Assert
        schemaRegistry.DidNotReceive().Dispose();
    }

    /// <summary>
    /// Helper class to create multi-segment sequences for testing.
    /// </summary>
    private sealed class TestSegment : ReadOnlySequenceSegment<byte>
    {
        public TestSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public void SetNext(TestSegment next)
        {
            Next = next;
            next.RunningIndex = RunningIndex + Memory.Length;
        }
    }
}
