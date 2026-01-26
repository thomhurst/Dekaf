using System.Buffers;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public class ProtobufSchemaRegistryRoundTripTests
{
    private static SerializationContext CreateContext(string topic = "test-topic", bool isKey = false) =>
        new() { Topic = topic, Component = isKey ? SerializationComponent.Key : SerializationComponent.Value };

    [Test]
    public async Task RoundTrip_PreservesMessageData()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        schemaRegistry.GetOrRegisterSchemaAsync(Arg.Any<string>(), Arg.Any<Schema>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(42));
        schemaRegistry.GetSchemaAsync(42, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Schema
            {
                SchemaType = SchemaType.Protobuf,
                SchemaString = "syntax = \"proto3\";"
            }));

        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(schemaRegistry);
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(schemaRegistry);

        var originalMessage = new TestMessage
        {
            Id = 12345,
            Name = "Test Message",
            Value = 123.456
        };

        // Act - serialize
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext("my-topic");
        serializer.Serialize(originalMessage, ref buffer, context);

        // Act - deserialize
        var result = deserializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        // Assert
        await Assert.That(result.Id).IsEqualTo(originalMessage.Id);
        await Assert.That(result.Name).IsEqualTo(originalMessage.Name);
        await Assert.That(result.Value).IsEqualTo(originalMessage.Value);
    }

    [Test]
    public async Task RoundTrip_WithEmptyStrings()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        schemaRegistry.GetOrRegisterSchemaAsync(Arg.Any<string>(), Arg.Any<Schema>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));
        schemaRegistry.GetSchemaAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Schema
            {
                SchemaType = SchemaType.Protobuf,
                SchemaString = "syntax = \"proto3\";"
            }));

        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(schemaRegistry);
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(schemaRegistry);

        var originalMessage = new TestMessage
        {
            Id = 0,
            Name = "",
            Value = 0
        };

        // Act
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        serializer.Serialize(originalMessage, ref buffer, context);
        var result = deserializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        // Assert - defaults for proto3
        await Assert.That(result.Id).IsEqualTo(0);
        await Assert.That(result.Name).IsEqualTo("");
        await Assert.That(result.Value).IsEqualTo(0);
    }

    [Test]
    public async Task RoundTrip_WithNegativeNumbers()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        schemaRegistry.GetOrRegisterSchemaAsync(Arg.Any<string>(), Arg.Any<Schema>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));
        schemaRegistry.GetSchemaAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Schema
            {
                SchemaType = SchemaType.Protobuf,
                SchemaString = "syntax = \"proto3\";"
            }));

        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(schemaRegistry);
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(schemaRegistry);

        var originalMessage = new TestMessage
        {
            Id = -42,
            Name = "Negative",
            Value = -123.456
        };

        // Act
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        serializer.Serialize(originalMessage, ref buffer, context);
        var result = deserializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        // Assert
        await Assert.That(result.Id).IsEqualTo(-42);
        await Assert.That(result.Name).IsEqualTo("Negative");
        await Assert.That(result.Value).IsEqualTo(-123.456);
    }

    [Test]
    public async Task RoundTrip_WithSpecialCharacters()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        schemaRegistry.GetOrRegisterSchemaAsync(Arg.Any<string>(), Arg.Any<Schema>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));
        schemaRegistry.GetSchemaAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Schema
            {
                SchemaType = SchemaType.Protobuf,
                SchemaString = "syntax = \"proto3\";"
            }));

        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(schemaRegistry);
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(schemaRegistry);

        var originalMessage = new TestMessage
        {
            Id = 1,
            Name = "Hello, World! Unicode: \u4e2d\u6587 Emoji: \ud83d\ude00",
            Value = double.MaxValue
        };

        // Act
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        serializer.Serialize(originalMessage, ref buffer, context);
        var result = deserializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        // Assert
        await Assert.That(result.Id).IsEqualTo(1);
        await Assert.That(result.Name).IsEqualTo("Hello, World! Unicode: \u4e2d\u6587 Emoji: \ud83d\ude00");
        await Assert.That(result.Value).IsEqualTo(double.MaxValue);
    }

    [Test]
    public async Task RoundTrip_MultipleMessages()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        schemaRegistry.GetOrRegisterSchemaAsync(Arg.Any<string>(), Arg.Any<Schema>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));
        schemaRegistry.GetSchemaAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Schema
            {
                SchemaType = SchemaType.Protobuf,
                SchemaString = "syntax = \"proto3\";"
            }));

        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(schemaRegistry);
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(schemaRegistry);

        var messages = Enumerable.Range(1, 100)
            .Select(i => new TestMessage { Id = i, Name = $"Message {i}", Value = i * 1.5 })
            .ToList();

        var context = CreateContext();

        // Act & Assert
        foreach (var original in messages)
        {
            var buffer = new ArrayBufferWriter<byte>();
            serializer.Serialize(original, ref buffer, context);
            var result = deserializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

            await Assert.That(result.Id).IsEqualTo(original.Id);
            await Assert.That(result.Name).IsEqualTo(original.Name);
            await Assert.That(result.Value).IsEqualTo(original.Value);
        }
    }
}
