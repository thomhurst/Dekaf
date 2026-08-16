using Avro.Generic;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.SchemaRegistry;

[NotInParallel("AvroSerialization")]
public sealed class SchemaRegistryTombstoneDeserializerTests
{
    [Test]
    public async Task Avro_ValueTombstone_ReturnsNullWithoutRegistryAccess()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(registry);

        var result = deserializer.Deserialize(ReadOnlyMemory<byte>.Empty, CreateContext(isNull: true));

        await Assert.That(result).IsNull();
        await AssertNoRegistryAccess(registry);
    }

    [Test]
    public async Task Protobuf_ValueTombstone_ReturnsNullWithoutRegistryAccess()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(registry);

        var result = deserializer.Deserialize(ReadOnlyMemory<byte>.Empty, CreateContext(isNull: true));

        await Assert.That(result).IsNull();
        await AssertNoRegistryAccess(registry);
    }

    [Test]
    public async Task JsonSchema_ValueTombstone_ReturnsNullWithoutRegistryAccess()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var deserializer = new JsonSchemaRegistryDeserializer<string>(registry);

        var result = deserializer.Deserialize(ReadOnlyMemory<byte>.Empty, CreateContext(isNull: true));

        await Assert.That(result).IsNull();
        await AssertNoRegistryAccess(registry);
    }

    [Test]
    public async Task Avro_NonNullEmptyPayload_StillFailsFramingValidation()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(registry);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.FromResult(deserializer.Deserialize(ReadOnlyMemory<byte>.Empty, CreateContext())));

        await Assert.That(exception!.Message).Contains("too short");
    }

    [Test]
    public async Task Protobuf_NonNullEmptyPayload_StillFailsFramingValidation()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(registry);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.FromResult(deserializer.Deserialize(ReadOnlyMemory<byte>.Empty, CreateContext())));

        await Assert.That(exception!.Message).Contains("too short");
    }

    [Test]
    public async Task JsonSchema_NonNullEmptyPayload_StillFailsFramingValidation()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var deserializer = new JsonSchemaRegistryDeserializer<string>(registry);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.FromResult(deserializer.Deserialize(ReadOnlyMemory<byte>.Empty, CreateContext())));

        await Assert.That(exception!.Message).Contains("too short");
    }

    [Test]
    public async Task Avro_NullKey_StillFailsFramingValidation()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(registry);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.FromResult(deserializer.Deserialize(ReadOnlyMemory<byte>.Empty, CreateContext(isKey: true, isNull: true))));

        await Assert.That(exception!.Message).Contains("too short");
    }

    [Test]
    public async Task Protobuf_NullKey_StillFailsFramingValidation()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(registry);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.FromResult(deserializer.Deserialize(ReadOnlyMemory<byte>.Empty, CreateContext(isKey: true, isNull: true))));

        await Assert.That(exception!.Message).Contains("too short");
    }

    [Test]
    public async Task JsonSchema_NullKey_StillFailsFramingValidation()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var deserializer = new JsonSchemaRegistryDeserializer<string>(registry);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.FromResult(deserializer.Deserialize(ReadOnlyMemory<byte>.Empty, CreateContext(isKey: true, isNull: true))));

        await Assert.That(exception!.Message).Contains("too short");
    }

    private static SerializationContext CreateContext(bool isKey = false, bool isNull = false) =>
        new()
        {
            Topic = "tombstone-topic",
            Component = isKey ? SerializationComponent.Key : SerializationComponent.Value,
            IsNull = isNull
        };

    private static async Task AssertNoRegistryAccess(MockSchemaRegistryClient registry)
    {
        await Assert.That(registry.TryGetCachedSchemaCallCount).IsEqualTo(0);
        await Assert.That(registry.GetSchemaCallCount).IsEqualTo(0);
    }
}
