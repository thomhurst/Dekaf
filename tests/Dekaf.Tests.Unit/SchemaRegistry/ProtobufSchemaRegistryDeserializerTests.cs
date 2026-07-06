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

    private sealed class CapturingRuleExecutor(byte[]? deserializedPayload = null) : ISchemaRegistryRuleExecutor
    {
        public SchemaRegistryRuleContext? Context { get; private set; }

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
            => payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
        {
            Context = context;
            return deserializedPayload ?? payload;
        }
    }

    private static byte[] CreateWireBytes(int schemaId, TestMessage message)
    {
        var protoBytes = message.ToByteArray();
        var wireBytes = new byte[1 + 4 + 1 + protoBytes.Length];
        wireBytes[0] = 0x00;
        BinaryPrimitives.WriteInt32BigEndian(wireBytes.AsSpan(1, 4), schemaId);
        wireBytes[5] = 0;
        protoBytes.CopyTo(wireBytes.AsSpan(6));
        return wireBytes;
    }

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
        var wireBytes = new byte[1 + 4 + 1 + protoBytes.Length]; // magic + schemaId + indexes + proto

        wireBytes[0] = 0x00; // Magic byte
        BinaryPrimitives.WriteInt32BigEndian(wireBytes.AsSpan(1, 4), 123); // Schema ID
        wireBytes[5] = 0; // Single top-level message index [0]
        protoBytes.CopyTo(wireBytes.AsSpan(6));

        // Act
        var result = deserializer.Deserialize(wireBytes, CreateContext());

        // Assert
        await Assert.That(result.Id).IsEqualTo(42);
        await Assert.That(result.Name).IsEqualTo("Hello");
        await Assert.That(result.Value).IsEqualTo(3.14);
    }

    [Test]
    public async Task Deserialize_UsesCachedSchema_WhenValidationEnabled()
    {
        // Arrange
        var schemaRegistry = new MockSchemaRegistryClient();
        var schemaId = await schemaRegistry.RegisterSchemaAsync("test-topic-value", new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = "syntax = \"proto3\";"
        });

        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(schemaRegistry);

        var originalMessage = new TestMessage { Id = 42, Name = "Hello", Value = 3.14 };
        var protoBytes = originalMessage.ToByteArray();
        var wireBytes = new byte[1 + 4 + 1 + protoBytes.Length];

        wireBytes[0] = 0x00;
        BinaryPrimitives.WriteInt32BigEndian(wireBytes.AsSpan(1, 4), schemaId);
        wireBytes[5] = 0;
        protoBytes.CopyTo(wireBytes.AsSpan(6));

        // Act
        var result = deserializer.Deserialize(wireBytes, CreateContext());

        // Assert
        await Assert.That(result.Id).IsEqualTo(42);
        await Assert.That(schemaRegistry.TryGetCachedSchemaCallCount).IsEqualTo(1);
        await Assert.That(schemaRegistry.GetSchemaCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task Deserialize_RuleExecutor_TransformsMessageBytes_AfterMessageIndexes()
    {
        var schemaRegistry = new MockSchemaRegistryClient();
        var schema = new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = "syntax = \"proto3\";"
        };
        var schemaId = await schemaRegistry.RegisterSchemaAsync("test-topic-value", schema);
        var replacement = new TestMessage { Id = 9, Name = "Plain", Value = 1.25 };
        var executor = new CapturingRuleExecutor(replacement.ToByteArray());
        var config = new ProtobufDeserializerConfig { RuleExecutor = executor };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(schemaRegistry, config);
        var wireBytes = CreateWireBytes(schemaId, new TestMessage { Id = 1, Name = "Encrypted", Value = 3.14 });

        var result = deserializer.Deserialize(wireBytes, CreateContext());

        await Assert.That(result.Id).IsEqualTo(replacement.Id);
        await Assert.That(result.Name).IsEqualTo(replacement.Name);
        await Assert.That(result.Value).IsEqualTo(replacement.Value);
        await Assert.That(executor.Context).IsNotNull();
        await Assert.That(executor.Context!.PayloadFormat).IsEqualTo(SchemaRegistryPayloadFormat.Protobuf);
        await Assert.That(executor.Context.SchemaId).IsEqualTo(schemaId);
        await Assert.That(executor.Context.Schema).IsSameReferenceAs(schema);
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
            Task.FromResult(deserializer.Deserialize(invalidBytes, CreateContext())));

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
            Task.FromResult(deserializer.Deserialize(shortBytes, CreateContext())));

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
            Task.FromResult(deserializer.Deserialize(wireBytes, CreateContext())));

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
        var wireBytes = new byte[1 + 4 + 1 + protoBytes.Length];

        wireBytes[0] = 0x00;
        BinaryPrimitives.WriteInt32BigEndian(wireBytes.AsSpan(1, 4), 123);
        wireBytes[5] = 0;
        protoBytes.CopyTo(wireBytes.AsSpan(6));

        // Act
        var result = deserializer.Deserialize(wireBytes, CreateContext());

        // Assert - schema registry should not be called
        await schemaRegistry.DidNotReceive().GetSchemaAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await Assert.That(result.Id).IsEqualTo(42);
    }

    [Test]
    public async Task Deserialize_RuleExecutor_SkipSchemaValidation_PassesNullSchema()
    {
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        var executor = new CapturingRuleExecutor();
        var config = new ProtobufDeserializerConfig
        {
            SkipSchemaValidation = true,
            RuleExecutor = executor
        };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(schemaRegistry, config);
        var wireBytes = CreateWireBytes(123, new TestMessage { Id = 42, Name = "Hello", Value = 3.14 });

        var result = deserializer.Deserialize(wireBytes, CreateContext());

        await schemaRegistry.DidNotReceive().GetSchemaAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await Assert.That(result.Id).IsEqualTo(42);
        await Assert.That(executor.Context).IsNotNull();
        await Assert.That(executor.Context!.SchemaId).IsEqualTo(123);
        await Assert.That(executor.Context.Schema).IsNull();
    }

    [Test]
    public async Task Deserialize_SkipsConfluentZigZagEncodedMessageIndexes()
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
        var originalMessage = new TestMessage { Id = 42, Name = "Nested", Value = 6.28 };
        var protoBytes = originalMessage.ToByteArray();
        var wireBytes = new byte[1 + 4 + 3 + protoBytes.Length];

        wireBytes[0] = 0x00;
        BinaryPrimitives.WriteInt32BigEndian(wireBytes.AsSpan(1, 4), 123);
        wireBytes[5] = 4; // zigzag length: 2
        wireBytes[6] = 2; // zigzag index: 1
        wireBytes[7] = 0; // zigzag index: 0
        protoBytes.CopyTo(wireBytes.AsSpan(8));

        // Act
        var result = deserializer.Deserialize(wireBytes, CreateContext());

        // Assert
        await Assert.That(result.Id).IsEqualTo(42);
        await Assert.That(result.Name).IsEqualTo("Nested");
        await Assert.That(result.Value).IsEqualTo(6.28);
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

}
