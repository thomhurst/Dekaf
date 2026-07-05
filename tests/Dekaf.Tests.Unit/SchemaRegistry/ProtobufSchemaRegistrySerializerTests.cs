using System.Buffers;
using System.Buffers.Binary;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Google.Protobuf;
using NSubstitute;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public class ProtobufSchemaRegistrySerializerTests
{
    private static SerializationContext CreateContext(string topic = "test-topic", bool isKey = false) =>
        new() { Topic = topic, Component = isKey ? SerializationComponent.Key : SerializationComponent.Value };

    private sealed class ReplacingRuleExecutor(byte[] serializedPayload) : ISchemaRegistryRuleExecutor
    {
        public SchemaRegistryRuleContext? Context { get; private set; }

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
        {
            Context = context;
            return serializedPayload;
        }

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
            => payload;
    }

    [Test]
    public async Task Serialize_WritesCorrectWireFormat()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        schemaRegistry.GetOrRegisterSchemaAsync(Arg.Any<string>(), Arg.Any<Schema>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(42));

        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(schemaRegistry);
        var buffer = new ArrayBufferWriter<byte>();

        var message = new TestMessage { Id = 1, Name = "Test", Value = 3.14 };

        // Act
        serializer.Serialize(message, ref buffer, CreateContext());

        // Assert
        var written = buffer.WrittenMemory.ToArray();

        // Magic byte
        await Assert.That(written[0]).IsEqualTo((byte)0x00);

        // Schema ID (big-endian)
        var schemaId = BinaryPrimitives.ReadInt32BigEndian(written.AsSpan(1, 4));
        await Assert.That(schemaId).IsEqualTo(42);

        // Message index array [0] is encoded as a single 0x00 byte in Confluent's Protobuf format.
        await Assert.That(written[5]).IsEqualTo((byte)0);

        var expectedPayload = message.ToByteArray();
        await Assert.That(written.AsSpan(6).ToArray()).IsEquivalentTo(expectedPayload);
    }

    [Test]
    public async Task Serialize_RuleExecutor_TransformsMessageBytes_AfterMessageIndexes()
    {
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        schemaRegistry.GetOrRegisterSchemaAsync(Arg.Any<string>(), Arg.Any<Schema>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(42));

        var replacement = new TestMessage { Id = 9, Name = "Encrypted", Value = 1.25 };
        var executor = new ReplacingRuleExecutor(replacement.ToByteArray());
        var config = new ProtobufSerializerConfig { RuleExecutor = executor };
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(schemaRegistry, config);

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(new TestMessage { Id = 1, Name = "Plain", Value = 3.14 }, ref buffer, CreateContext());

        var written = buffer.WrittenMemory.ToArray();
        await Assert.That(written[5]).IsEqualTo((byte)0);
        await Assert.That(written.AsSpan(6).ToArray()).IsEquivalentTo(replacement.ToByteArray());
        await Assert.That(executor.Context).IsNotNull();
        await Assert.That(executor.Context!.PayloadFormat).IsEqualTo(SchemaRegistryPayloadFormat.Protobuf);
        await Assert.That(executor.Context.Subject).IsEqualTo("test-topic-value");
        await Assert.That(executor.Context.SchemaId).IsEqualTo(42);
    }


    [Test]
    public async Task Serialize_CachesSchemaId()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        var callCount = 0;
        schemaRegistry.GetOrRegisterSchemaAsync(Arg.Any<string>(), Arg.Any<Schema>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return Task.FromResult(42);
            });

        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(schemaRegistry);
        var message = new TestMessage { Id = 1, Name = "Test", Value = 3.14 };
        var context = CreateContext();

        // Act - serialize multiple times
        var buffer1 = new ArrayBufferWriter<byte>();
        serializer.Serialize(message, ref buffer1, context);

        var buffer2 = new ArrayBufferWriter<byte>();
        serializer.Serialize(message, ref buffer2, context);

        var buffer3 = new ArrayBufferWriter<byte>();
        serializer.Serialize(message, ref buffer3, context);

        // Assert - schema registry should only be called once
        await Assert.That(callCount).IsEqualTo(1);
    }

    [Test]
    public async Task Serialize_UsesTopicNameSubjectStrategy()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        string? capturedSubject = null;
        schemaRegistry.GetOrRegisterSchemaAsync(Arg.Any<string>(), Arg.Any<Schema>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedSubject = callInfo.Arg<string>();
                return Task.FromResult(1);
            });

        var config = new ProtobufSerializerConfig { SubjectNameStrategy = SubjectNameStrategy.TopicName };
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(schemaRegistry, config);

        var message = new TestMessage { Id = 1, Name = "Test", Value = 3.14 };

        // Act
        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(message, ref buffer, CreateContext("my-topic"));

        // Assert
        await Assert.That(capturedSubject).IsEqualTo("my-topic-value");
    }

    [Test]
    public async Task Serialize_UsesRecordNameSubjectStrategy()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        string? capturedSubject = null;
        schemaRegistry.GetOrRegisterSchemaAsync(Arg.Any<string>(), Arg.Any<Schema>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedSubject = callInfo.Arg<string>();
                return Task.FromResult(1);
            });

        var config = new ProtobufSerializerConfig { SubjectNameStrategy = SubjectNameStrategy.RecordName };
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(schemaRegistry, config);

        var message = new TestMessage { Id = 1, Name = "Test", Value = 3.14 };

        // Act
        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(message, ref buffer, CreateContext("my-topic"));

        // Assert - should use full message name
        await Assert.That(capturedSubject).IsEqualTo("dekaf.tests.TestMessage-value");
    }

    [Test]
    public async Task Serialize_UsesKeySubjectSuffix()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        string? capturedSubject = null;
        schemaRegistry.GetOrRegisterSchemaAsync(Arg.Any<string>(), Arg.Any<Schema>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedSubject = callInfo.Arg<string>();
                return Task.FromResult(1);
            });

        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(schemaRegistry);

        var message = new TestMessage { Id = 1, Name = "Test", Value = 3.14 };

        // Act
        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(message, ref buffer, CreateContext("my-topic", isKey: true));

        // Assert
        await Assert.That(capturedSubject).IsEqualTo("my-topic-key");
    }

    [Test]
    public async Task Serialize_RegistersProtobufSchema()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        Schema? capturedSchema = null;
        schemaRegistry.GetOrRegisterSchemaAsync(Arg.Any<string>(), Arg.Any<Schema>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedSchema = callInfo.Arg<Schema>();
                return Task.FromResult(1);
            });

        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(schemaRegistry);

        var message = new TestMessage { Id = 1, Name = "Test", Value = 3.14 };

        // Act
        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(message, ref buffer, CreateContext());

        // Assert
        await Assert.That(capturedSchema).IsNotNull();
        await Assert.That(capturedSchema!.SchemaType).IsEqualTo(SchemaType.Protobuf);
        await Assert.That(capturedSchema.SchemaString).Contains("message TestMessage");
    }

    [Test]
    public async Task Serialize_DisablesAutoRegisterSchemas()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        var registeredSchema = new RegisteredSchema
        {
            Id = 99,
            Subject = "test-topic-value",
            Version = 1,
            Schema = new Schema { SchemaType = SchemaType.Protobuf, SchemaString = "syntax = \"proto3\";" }
        };
        schemaRegistry.GetSchemaBySubjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(registeredSchema));

        var config = new ProtobufSerializerConfig { AutoRegisterSchemas = false };
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(schemaRegistry, config);

        var message = new TestMessage { Id = 1, Name = "Test", Value = 3.14 };

        // Act
        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(message, ref buffer, CreateContext());

        // Assert - should call GetSchemaBySubjectAsync, not GetOrRegisterSchemaAsync
        await schemaRegistry.Received(1).GetSchemaBySubjectAsync(Arg.Any<string>(), "latest", Arg.Any<CancellationToken>());
        await schemaRegistry.DidNotReceive().GetOrRegisterSchemaAsync(Arg.Any<string>(), Arg.Any<Schema>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Serialize_NormalizeSchemas_PassesNormalizeToRegistry()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        schemaRegistry.GetOrRegisterSchemaAsync(
                Arg.Any<string>(),
                Arg.Any<Schema>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(123));

        var config = new ProtobufSerializerConfig { NormalizeSchemas = true };
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(schemaRegistry, config);

        var message = new TestMessage { Id = 1, Name = "Test", Value = 3.14 };

        // Act
        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(message, ref buffer, CreateContext());

        // Assert
        await schemaRegistry.Received(1).GetOrRegisterSchemaAsync(
            Arg.Any<string>(),
            Arg.Any<Schema>(),
            true,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Serialize_DisablesAutoRegisterSchemas_WhenLookupFaults_ThrowsSchemaRegistryException()
    {
        await AssertLookupFaultThrowsSchemaRegistryExceptionAsync(
            new ProtobufSerializerConfig { AutoRegisterSchemas = false },
            50001);
    }

    [Test]
    public async Task Serialize_UseLatestVersion_WhenLookupFaults_ThrowsSchemaRegistryException()
    {
        await AssertLookupFaultThrowsSchemaRegistryExceptionAsync(
            new ProtobufSerializerConfig { UseLatestVersion = true },
            50002);
    }

    private static async Task AssertLookupFaultThrowsSchemaRegistryExceptionAsync(
        ProtobufSerializerConfig config,
        int errorCode)
    {
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        schemaRegistry.GetSchemaBySubjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<RegisteredSchema>(new SchemaRegistryException(errorCode, "lookup failed")));

        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(schemaRegistry, config);

        var message = new TestMessage { Id = 1, Name = "Test", Value = 3.14 };
        var buffer = new ArrayBufferWriter<byte>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<SchemaRegistryException>(() =>
        {
            serializer.Serialize(message, ref buffer, CreateContext());
            return Task.CompletedTask;
        });

        await Assert.That(exception!.ErrorCode).IsEqualTo(errorCode);
    }

    [Test]
    public async Task DisposeAsync_DisposesOwnedClient()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(schemaRegistry, ownsClient: true);

        // Act
        await serializer.DisposeAsync();

        // Assert
        schemaRegistry.Received(1).Dispose();
    }

    [Test]
    public async Task DisposeAsync_DoesNotDisposeNonOwnedClient()
    {
        // Arrange
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(schemaRegistry, ownsClient: false);

        // Act
        await serializer.DisposeAsync();

        // Assert
        schemaRegistry.DidNotReceive().Dispose();
    }
}
