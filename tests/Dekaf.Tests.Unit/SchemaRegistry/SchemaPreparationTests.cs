using System.Buffers;
using System.Buffers.Binary;
using System.Reflection;
using Avro.Generic;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaPreparationTests
{
    private const string AvroSchemaText = """
        {
          "type": "record",
          "name": "PreparedRecord",
          "namespace": "dekaf.tests",
          "fields": [{ "name": "id", "type": "int" }]
        }
        """;

    [Test]
    public async Task Generic_PrepareAsync_ReturnsContextAndCachesSynchronously()
    {
        using var registry = new MockSchemaRegistryClient();
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = "{}" };
        await using var serializer = CreateGenericSerializer(registry, schema);

        var resolved = await serializer.PrepareAsync("orders", 42);
        var cached = serializer.PrepareAsync("orders", 42);

        await Assert.That(resolved.Subject).IsEqualTo("orders-value");
        await Assert.That(resolved.SchemaId).IsEqualTo(1);
        await Assert.That(resolved.Schema).IsSameReferenceAs(schema);
        await Assert.That(cached.IsCompletedSuccessfully).IsTrue();
        await Assert.That((await cached).SchemaId).IsEqualTo(resolved.SchemaId);
        await Assert.That(registry.GetOrRegisterSchemaCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Json_PrepareAsync_ReturnsKeyContextAndPreventsSerializeRefetch()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = new JsonSchemaRegistrySerializer<PreparationPayload>(
            registry,
            "{\"type\":\"object\"}");

        var resolved = await serializer.PrepareAsync(
            "orders",
            new PreparationPayload { Id = 42 },
            isKey: true);
        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(
            new PreparationPayload { Id = 42 },
            ref buffer,
            new SerializationContext { Topic = "orders", Component = SerializationComponent.Key });

        await Assert.That(resolved.Subject).IsEqualTo("orders-key");
        await Assert.That(resolved.Schema.SchemaType).IsEqualTo(SchemaType.Json);
        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(buffer.WrittenSpan.Slice(1, 4)))
            .IsEqualTo(resolved.SchemaId);
        await Assert.That(registry.GetOrRegisterSchemaCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Avro_WarmupAsync_DelegatesToResolvedContext()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        var record = CreateAvroRecord(42);

        var resolved = await serializer.PrepareAsync("orders", record);
        var warmedId = await serializer.WarmupAsync("orders", record);

        await Assert.That(resolved.Subject).IsEqualTo("orders-value");
        await Assert.That(resolved.SchemaId).IsEqualTo(warmedId);
        await Assert.That(resolved.Schema.SchemaType).IsEqualTo(SchemaType.Avro);
        await Assert.That(registry.GetOrRegisterSchemaCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Avro_PrepareAsync_UseLatestVersion_ReturnsRegisteredSchema()
    {
        using var registry = new MockSchemaRegistryClient();
        var registeredSchema = new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = "{\"type\":\"record\",\"name\":\"RemoteRecord\",\"fields\":[]}"
        };
        var schemaId = await registry.RegisterSchemaAsync("orders-value", registeredSchema);
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            registry,
            new AvroSerializerConfig { UseLatestVersion = true });

        var resolved = await serializer.PrepareAsync("orders", CreateAvroRecord(42));

        await Assert.That(resolved.SchemaId).IsEqualTo(schemaId);
        await Assert.That(resolved.Schema).IsSameReferenceAs(registeredSchema);
        await Assert.That(registry.GetOrRegisterSchemaCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task Protobuf_PrepareAsync_UseLatestVersion_ReturnsRegisteredSchema()
    {
        using var registry = new MockSchemaRegistryClient();
        var registeredSchema = new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = "registered-protobuf-schema"
        };
        var schemaId = await registry.RegisterSchemaAsync("orders-value", registeredSchema);
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(
            registry,
            new ProtobufSerializerConfig { UseLatestVersion = true });

        var resolved = await serializer.PrepareAsync("orders", new TestMessage { Id = 42 });

        await Assert.That(resolved.SchemaId).IsEqualTo(schemaId);
        await Assert.That(resolved.Schema).IsSameReferenceAs(registeredSchema);
        await Assert.That(registry.GetOrRegisterSchemaCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task Protobuf_PrepareAsync_ReturnsContextAndPreventsSerializeRefetch()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(registry);
        var value = new TestMessage { Id = 42, Name = "prepared" };

        var resolved = await serializer.PrepareAsync("orders", value, isKey: true);
        var cached = serializer.PrepareAsync("orders", value, isKey: true);
        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(
            value,
            ref buffer,
            new SerializationContext { Topic = "orders", Component = SerializationComponent.Key });

        await Assert.That(resolved.Subject).IsEqualTo("orders-key");
        await Assert.That(resolved.Schema.SchemaType).IsEqualTo(SchemaType.Protobuf);
        await Assert.That(cached.IsCompletedSuccessfully).IsTrue();
        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(buffer.WrittenSpan.Slice(1, 4)))
            .IsEqualTo(resolved.SchemaId);
        await Assert.That(registry.GetOrRegisterSchemaCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Generic_PrepareAsync_DifferentTopicsAndComponents_DoNotCollide()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = CreateGenericSerializer(
            registry,
            new Schema { SchemaType = SchemaType.Json, SchemaString = "{}" });

        var firstValue = await serializer.PrepareAsync("orders-a", 1);
        var firstKey = await serializer.PrepareAsync("orders-a", 1, isKey: true);
        var secondValue = await serializer.PrepareAsync("orders-b", 1);

        await Assert.That(firstValue.Subject).IsEqualTo("orders-a-value");
        await Assert.That(firstKey.Subject).IsEqualTo("orders-a-key");
        await Assert.That(secondValue.Subject).IsEqualTo("orders-b-value");
        await Assert.That(new[] { firstValue.SchemaId, firstKey.SchemaId, secondValue.SchemaId }.Distinct().Count())
            .IsEqualTo(3);
        await Assert.That(registry.GetOrRegisterSchemaCallCount).IsEqualTo(3);
    }

    [Test]
    public async Task Protobuf_PrepareAsync_DifferentTopicsAndComponents_DoNotCollide()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(registry);
        var value = new TestMessage { Id = 42 };

        var firstValue = await serializer.PrepareAsync("orders-a", value);
        var firstKey = await serializer.PrepareAsync("orders-a", value, isKey: true);
        var secondValue = await serializer.PrepareAsync("orders-b", value);

        await Assert.That(firstValue.Subject).IsEqualTo("orders-a-value");
        await Assert.That(firstKey.Subject).IsEqualTo("orders-a-key");
        await Assert.That(secondValue.Subject).IsEqualTo("orders-b-value");
        await Assert.That(new[] { firstValue.SchemaId, firstKey.SchemaId, secondValue.SchemaId }.Distinct().Count())
            .IsEqualTo(3);
        await Assert.That(registry.GetOrRegisterSchemaCallCount).IsEqualTo(3);
    }

    [Test]
    public async Task Generic_ProducerFirstUse_PreparesAsynchronously()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = CreateGenericSerializer(
            registry,
            new Schema { SchemaType = SchemaType.Json, SchemaString = "{}" });

        await AssertProducerPreparationIsAsync(registry, serializer, 42);
    }

    [Test]
    public async Task Json_ProducerFirstUse_PreparesAsynchronously()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = new JsonSchemaRegistrySerializer<PreparationPayload>(
            registry,
            "{\"type\":\"object\"}");

        await AssertProducerPreparationIsAsync(
            registry,
            serializer,
            new PreparationPayload { Id = 42 });
    }

    [Test]
    public async Task Avro_ProducerFirstUse_PreparesAsynchronously()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);

        await AssertProducerPreparationIsAsync(registry, serializer, CreateAvroRecord(42));
    }

    [Test]
    public async Task Protobuf_ProducerFirstUse_PreparesAsynchronously()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(registry);

        await AssertProducerPreparationIsAsync(
            registry,
            serializer,
            new TestMessage { Id = 42, Name = "prepared" });
    }

    [Test]
    public async Task Generic_PrepareAsync_ConcurrentFirstUse_IsSingleFlight()
    {
        using var registry = new MockSchemaRegistryClient();
        registry.BlockNextGetOrRegisterSchema();
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = "{}" };
        await using var serializer = CreateGenericSerializer(registry, schema);

        var first = serializer.PrepareAsync("orders", 1);
        await registry.WaitForBlockedGetOrRegisterSchemaAsync(TimeSpan.FromSeconds(2));
        var second = serializer.PrepareAsync("orders", 2);

        await Assert.That(first.IsCompleted).IsFalse();
        await Assert.That(second.IsCompleted).IsFalse();
        registry.ReleaseBlockedGetOrRegisterSchema();
        var results = await Task.WhenAll(first.AsTask(), second.AsTask());

        await Assert.That(results[0]).IsEqualTo(results[1]);
        await Assert.That(registry.GetOrRegisterSchemaCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Generic_PrepareAsync_FailureDoesNotPoisonRetry()
    {
        using var registry = new MockSchemaRegistryClient { GetOrRegisterSchemaFailuresRemaining = 1 };
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = "{}" };
        await using var serializer = CreateGenericSerializer(registry, schema);

        await Assert.That(async () => await serializer.PrepareAsync("orders", 1))
            .Throws<SchemaRegistryException>();
        var resolved = await serializer.PrepareAsync("orders", 1);

        await Assert.That(resolved.SchemaId).IsGreaterThan(0);
        await Assert.That(registry.GetOrRegisterSchemaCallCount).IsEqualTo(2);
    }

    [Test]
    public async Task Generic_PrepareAsync_CanceledWaiterDoesNotCancelSharedResolution()
    {
        using var registry = new MockSchemaRegistryClient();
        registry.BlockNextGetOrRegisterSchema();
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = "{}" };
        await using var serializer = CreateGenericSerializer(registry, schema);
        using var cancellation = new CancellationTokenSource();

        var canceledWaiter = serializer.PrepareAsync("orders", 1, cancellationToken: cancellation.Token);
        await registry.WaitForBlockedGetOrRegisterSchemaAsync(TimeSpan.FromSeconds(2));
        var successfulWaiter = serializer.PrepareAsync("orders", 2);
        cancellation.Cancel();

        try
        {
            await Assert.That(async () => await canceledWaiter).Throws<OperationCanceledException>();
        }
        finally
        {
            registry.ReleaseBlockedGetOrRegisterSchema();
        }

        await Assert.That((await successfulWaiter).SchemaId).IsGreaterThan(0);
        await Assert.That(registry.GetOrRegisterSchemaCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task ResolutionCache_DeduplicatesEquivalentReferencesWithoutCollidingVersions()
    {
        var cache = new SchemaResolutionCache<int>();
        var counter = new ResolutionCounter();
        var firstSchema = CreateReferencedSchema(version: 1);
        var equivalentSchema = CreateReferencedSchema(version: 1);
        var differentSchema = CreateReferencedSchema(version: 2);

        var first = await cache.ResolveAsync(
            "orders-value",
            firstSchema,
            counter,
            static (state, _, _) => Task.FromResult(Interlocked.Increment(ref state.Count)),
            CancellationToken.None);
        var equivalent = await cache.ResolveAsync(
            "orders-value",
            equivalentSchema,
            counter,
            static (state, _, _) => Task.FromResult(Interlocked.Increment(ref state.Count)),
            CancellationToken.None);
        var different = await cache.ResolveAsync(
            "orders-value",
            differentSchema,
            counter,
            static (state, _, _) => Task.FromResult(Interlocked.Increment(ref state.Count)),
            CancellationToken.None);

        await Assert.That(equivalent).IsEqualTo(first);
        await Assert.That(different).IsNotEqualTo(first);
        await Assert.That(counter.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ResolutionCache_CanceledResolutionDoesNotPoisonRetry()
    {
        var cache = new SchemaResolutionCache<int>();
        var counter = new ResolutionCounter();
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = "{}" };

        await Assert.That(async () => await cache.ResolveAsync(
                "orders-value",
                schema,
                counter,
                static (state, _, _) =>
                {
                    var attempt = Interlocked.Increment(ref state.Count);
                    return attempt == 1
                        ? Task.FromCanceled<int>(new CancellationToken(canceled: true))
                        : Task.FromResult(attempt);
                },
                CancellationToken.None))
            .Throws<OperationCanceledException>();
        var resolved = await cache.ResolveAsync(
            "orders-value",
            schema,
            counter,
            static (state, _, _) => Task.FromResult(Interlocked.Increment(ref state.Count)),
            CancellationToken.None);

        await Assert.That(resolved).IsEqualTo(2);
        await Assert.That(counter.Count).IsEqualTo(2);
    }

    private static SchemaRegistrySerializer<int> CreateGenericSerializer(
        ISchemaRegistryClient registry,
        Schema schema) =>
        new(
            registry,
            static (value, writer) =>
            {
                var span = writer.GetSpan(sizeof(int));
                BinaryPrimitives.WriteInt32BigEndian(span, value);
                writer.Advance(sizeof(int));
            },
            () => schema);

    private static GenericRecord CreateAvroRecord(int id)
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(AvroSchemaText);
        var record = new GenericRecord(schema);
        record.Add("id", id);
        return record;
    }

    private static async Task AssertProducerPreparationIsAsync<TValue>(
        MockSchemaRegistryClient registry,
        ISerializer<TValue> serializer,
        TValue value)
    {
        registry.BlockNextGetOrRegisterSchema();
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "schema-preparation-test",
            BufferMemory = ulong.MaxValue,
            BatchSize = 4096,
            LingerMs = 10,
            RequestTimeoutMs = 500,
            DeliveryTimeoutMs = 1000,
            CloseTimeoutMs = 1000
        };
        await using var producer = new KafkaProducer<string, TValue>(
            options,
            Serializers.String,
            serializer);
        await producer.StopSenderLoopsForTestingAsync();
        SetField(producer, "_initialized", true);
        using var cancellation = new CancellationTokenSource();

        var produce = producer.ProduceAsync(
            new ProducerMessage<string, TValue>
            {
                Topic = "orders",
                Key = "key",
                Value = value
            },
            cancellation.Token).AsTask();

        await registry.WaitForBlockedGetOrRegisterSchemaAsync(TimeSpan.FromSeconds(2));
        await Assert.That(produce.IsCompleted).IsFalse();
        cancellation.Cancel();

        try
        {
            await Assert.That(async () => await produce).Throws<OperationCanceledException>();
        }
        finally
        {
            registry.ReleaseBlockedGetOrRegisterSchema();
        }

        var preparer = (IAsyncSerializerPreparer<TValue>)serializer;
        await preparer.PrepareAsync(
            value,
            new SerializationContext
            {
                Topic = "orders",
                Component = SerializationComponent.Value
            });
    }

    private static void SetField<T>(object target, string name, T value)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        target.GetType().GetField(name, flags)!.SetValue(target, value);
    }

    private static Schema CreateReferencedSchema(int version) =>
        new()
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = "root",
            References =
            [
                new SchemaReference
                {
                    Name = "dependency.proto",
                    Subject = "dependency.proto",
                    Version = version
                }
            ]
        };

    private sealed class ResolutionCounter
    {
        internal int Count;
    }

    private sealed class PreparationPayload
    {
        public int Id { get; init; }
    }
}
