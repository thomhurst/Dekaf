using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
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
    public async Task Generic_PrepareAsync_SubjectCacheTurnoverCompletesFromResolvedSchemaSynchronously()
    {
        using var registry = new MockSchemaRegistryClient();
        var schemaFactoryCalls = 0;
        await using var serializer = new SchemaRegistrySerializer<int>(
            registry,
            static (value, writer) =>
            {
                var span = writer.GetSpan(sizeof(int));
                BinaryPrimitives.WriteInt32BigEndian(span, value);
                writer.Advance(sizeof(int));
            },
            () =>
            {
                Interlocked.Increment(ref schemaFactoryCalls);
                return CreateReferencedSchema(version: 1);
            },
            subjectNameStrategy: SubjectNameStrategy.RecordName);

        for (var index = 0; index < SubjectSchemaIdCache.MaxCachedEntries; index++)
            _ = await serializer.PrepareAsync($"topic-{index}", 42);

        var overflow = serializer.PrepareAsync("overflow-a", 42);

        await Assert.That(overflow.IsCompletedSuccessfully).IsTrue();
        await Assert.That((await overflow).SchemaId).IsEqualTo(1);
        await Assert.That(schemaFactoryCalls).IsEqualTo(1);
        await Assert.That(registry.GetOrRegisterSchemaCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Generic_PrepareAsync_SubjectDependentFactoryCachesSchemaBeforeContextTurnover()
    {
        using var registry = new MockSchemaRegistryClient();
        var schemaFactoryCalls = 0;
        await using var serializer = new SchemaRegistrySerializer<int>(
            registry,
            static (value, writer) =>
            {
                var span = writer.GetSpan(sizeof(int));
                BinaryPrimitives.WriteInt32BigEndian(span, value);
                writer.Advance(sizeof(int));
            },
            _ =>
            {
                Interlocked.Increment(ref schemaFactoryCalls);
                return CreateDataContractSchema(owner: "payments");
            },
            subjectNameStrategy: SubjectNameStrategy.RecordName);

        for (var index = 0; index < SubjectSchemaIdCache.MaxCachedEntries; index++)
            _ = await serializer.PrepareAsync($"topic-{index}", 42);

        for (var index = 0; index < 6; index++)
        {
            var overflow = serializer.PrepareAsync($"overflow-{index % 3}", 42);
            await Assert.That(overflow.IsCompletedSuccessfully).IsTrue();
            await Assert.That((await overflow).SchemaId).IsEqualTo(1);
        }

        await Assert.That(schemaFactoryCalls).IsEqualTo(1);
        await Assert.That(registry.GetOrRegisterSchemaCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Generic_PrepareAsync_SubjectFactoryRetainsNewestOverflowSubject()
    {
        using var registry = new MockSchemaRegistryClient();
        var schemaFactoryCalls = 0;
        await using var serializer = new SchemaRegistrySerializer<int>(
            registry,
            static (value, writer) =>
            {
                var span = writer.GetSpan(sizeof(int));
                BinaryPrimitives.WriteInt32BigEndian(span, value);
                writer.Advance(sizeof(int));
            },
            _ =>
            {
                Interlocked.Increment(ref schemaFactoryCalls);
                return CreateDataContractSchema(owner: "payments");
            },
            subjectNameStrategy: SubjectNameStrategy.TopicName);

        for (var index = 0; index < SubjectSchemaIdCache.MaxCachedEntries; index++)
            _ = await serializer.PrepareAsync($"topic-{index}", 42);

        for (var index = 0; index < 4; index++)
            _ = await serializer.PrepareAsync($"overflow-{index}", 42);

        for (var index = 0; index < 30; index++)
            _ = await serializer.PrepareAsync("overflow-4", 42);

        await Assert.That(schemaFactoryCalls)
            .IsEqualTo(SubjectSchemaIdCache.MaxCachedEntries + 5);
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
            await Assert.That(registry.LastGetOrRegisterSchemaCancellationToken.CanBeCanceled).IsTrue();
            await Assert.That(registry.LastGetOrRegisterSchemaCancellationToken.IsCancellationRequested).IsFalse();
        }
        finally
        {
            registry.ReleaseBlockedGetOrRegisterSchema();
        }

        await Assert.That((await successfulWaiter).SchemaId).IsGreaterThan(0);
        await Assert.That(registry.GetOrRegisterSchemaCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Generic_PrepareAsync_UsesIndependentRegistryTimeout()
    {
        using var registry = new MockSchemaRegistryClient();
        registry.BlockNextGetOrRegisterSchema();
        await using var serializer = CreateGenericSerializer(
            registry,
            new Schema { SchemaType = SchemaType.Json, SchemaString = "{}" });

        await AssertRegistryResolutionUsesIndependentTimeoutAsync(registry, serializer, 42);
    }

    [Test]
    public async Task Json_PrepareAsync_UsesIndependentRegistryTimeout()
    {
        using var registry = new MockSchemaRegistryClient();
        registry.BlockNextGetOrRegisterSchema();
        await using var serializer = new JsonSchemaRegistrySerializer<PreparationPayload>(
            registry,
            "{\"type\":\"object\"}");

        await AssertRegistryResolutionUsesIndependentTimeoutAsync(
            registry,
            serializer,
            new PreparationPayload { Id = 42 });
    }

    [Test]
    public async Task Avro_PrepareAsync_UsesIndependentRegistryTimeout()
    {
        using var registry = new MockSchemaRegistryClient();
        registry.BlockNextGetOrRegisterSchema();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);

        await AssertRegistryResolutionUsesIndependentTimeoutAsync(
            registry,
            serializer,
            CreateAvroRecord(42));
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
    public async Task ResolutionCache_ComputesReferenceFingerprintOncePerSchema()
    {
        var cache = new SchemaResolutionCache<int>();
        var references = new CountingSchemaReferenceList(
            [new SchemaReference { Name = "dependency.proto", Subject = "dependency.proto", Version = 1 }]);
        var schema = new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = "root",
            References = references
        };

        _ = await cache.ResolveAsync(
            "orders-value",
            schema,
            0,
            static (_, _, _) => Task.FromResult(1),
            CancellationToken.None);
        var readsAfterResolution = references.IndexerReadCount;
        for (var index = 0; index < 100; index++)
        {
            _ = await cache.ResolveAsync(
                "orders-value",
                schema,
                0,
                static (_, _, _) => Task.FromResult(2),
                CancellationToken.None);
        }

        await Assert.That(readsAfterResolution).IsGreaterThan(0);
        await Assert.That(references.IndexerReadCount).IsEqualTo(readsAfterResolution);
    }

    [Test]
    public async Task ResolutionCache_DeduplicatesEquivalentMetadataAndRulesWithoutCollidingContents()
    {
        var cache = new SchemaResolutionCache<int>();
        var counter = new ResolutionCounter();
        var firstSchema = CreateDataContractSchema(owner: "payments");
        var equivalentSchema = CreateDataContractSchema(owner: "payments");
        var differentSchema = CreateDataContractSchema(owner: "fraud");

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
    public async Task ResolutionCache_FingerprintIncludesMetadataAndRules()
    {
        var firstSchema = CreateDataContractSchema(owner: "payments");
        var equivalentSchema = CreateDataContractSchema(owner: "payments");
        var differentMetadataSchema = CreateDataContractSchema(owner: "fraud");
        var differentRuleSchema = CreateDataContractSchema(owner: "payments", ruleType: "CEL");

        var firstFingerprint = SchemaDataContractFingerprintCache.GetHashCode(firstSchema);
        var equivalentFingerprint = SchemaDataContractFingerprintCache.GetHashCode(equivalentSchema);
        var differentMetadataFingerprint = SchemaDataContractFingerprintCache.GetHashCode(differentMetadataSchema);
        var differentRuleFingerprint = SchemaDataContractFingerprintCache.GetHashCode(differentRuleSchema);

        await Assert.That(equivalentFingerprint).IsEqualTo(firstFingerprint);
        await Assert.That(differentMetadataFingerprint).IsNotEqualTo(firstFingerprint);
        await Assert.That(differentRuleFingerprint).IsNotEqualTo(firstFingerprint);
        await Assert.That(SchemaDataContractFingerprintCache.GetHashCode(firstSchema)).IsEqualTo(firstFingerprint);
    }

    [Test]
    public async Task ResolutionCache_WarmHitCompletesDespiteCanceledWaiter()
    {
        var cache = new SchemaResolutionCache<int>();
        var counter = new ResolutionCounter();
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = "{}" };

        _ = await cache.ResolveAsync(
            "orders-value",
            schema,
            counter,
            static (state, _, _) => Task.FromResult(Interlocked.Increment(ref state.Count)),
            CancellationToken.None);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cached = cache.ResolveAsync(
            "orders-value",
            schema,
            counter,
            static (state, _, _) => Task.FromResult(Interlocked.Increment(ref state.Count)),
            cancellation.Token);

        await Assert.That(cached.IsCompletedSuccessfully).IsTrue();
        await Assert.That(await cached).IsEqualTo(1);
        await Assert.That(counter.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ResolutionCache_ColdMissHonorsCanceledWaiterBeforeStartingResolution()
    {
        var cache = new SchemaResolutionCache<int>();
        var counter = new ResolutionCounter();
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = "{}" };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.That(async () => await cache.ResolveAsync(
                "orders-value",
                schema,
                counter,
                static (state, _, _) => Task.FromResult(Interlocked.Increment(ref state.Count)),
                cancellation.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(counter.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ResolutionCache_EvictsOldestCompletedEntryAtCapacity()
    {
        var cache = new SchemaResolutionCache<int>(maxCachedEntries: 2);
        var counter = new ResolutionCounter();
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = "{}" };

        await cache.ResolveAsync(
            "orders-a",
            schema,
            counter,
            static (state, _, _) => Task.FromResult(Interlocked.Increment(ref state.Count)),
            CancellationToken.None);
        await cache.ResolveAsync(
            "orders-b",
            schema,
            counter,
            static (state, _, _) => Task.FromResult(Interlocked.Increment(ref state.Count)),
            CancellationToken.None);
        await cache.ResolveAsync(
            "orders-c",
            schema,
            counter,
            static (state, _, _) => Task.FromResult(Interlocked.Increment(ref state.Count)),
            CancellationToken.None);
        var reloaded = await cache.ResolveAsync(
            "orders-a",
            schema,
            counter,
            static (state, _, _) => Task.FromResult(Interlocked.Increment(ref state.Count)),
            CancellationToken.None);
        var retained = cache.ResolveAsync(
            "orders-c",
            schema,
            counter,
            static (state, _, _) => Task.FromResult(Interlocked.Increment(ref state.Count)),
            CancellationToken.None);

        await Assert.That(reloaded).IsEqualTo(4);
        await Assert.That(retained.IsCompletedSuccessfully).IsTrue();
        await Assert.That(await retained).IsEqualTo(3);
        await Assert.That(counter.Count).IsEqualTo(4);
        await Assert.That(cache.CachedEntryCount).IsEqualTo(2);
    }

    [Test]
    public async Task ResolutionCache_CoalescesConcurrentMissWhenAtCapacity()
    {
        var cache = new SchemaResolutionCache<int>(maxCachedEntries: 1);
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = "{}" };
        await cache.ResolveAsync(
            "cached",
            schema,
            0,
            static (_, _, _) => Task.FromResult(1),
            CancellationToken.None);

        var counter = new ResolutionCounter();
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var state = (Counter: counter, Completion: completion);
        var first = cache.ResolveAsync(
            "new",
            schema,
            state,
            static (arguments, _, _) =>
            {
                Interlocked.Increment(ref arguments.Counter.Count);
                return arguments.Completion.Task;
            },
            CancellationToken.None).AsTask();
        var second = cache.ResolveAsync(
            "new",
            schema,
            state,
            static (arguments, _, _) =>
            {
                Interlocked.Increment(ref arguments.Counter.Count);
                return arguments.Completion.Task;
            },
            CancellationToken.None).AsTask();

        await Assert.That(counter.Count).IsEqualTo(1);
        completion.SetResult(42);
        await Task.WhenAll(first, second);
        var cached = cache.ResolveAsync(
            "new",
            schema,
            state,
            static (arguments, _, _) =>
            {
                Interlocked.Increment(ref arguments.Counter.Count);
                return arguments.Completion.Task;
            },
            CancellationToken.None);

        await Assert.That(first.Result).IsEqualTo(42);
        await Assert.That(second.Result).IsEqualTo(42);
        await Assert.That(cached.IsCompletedSuccessfully).IsTrue();
        await Assert.That(await cached).IsEqualTo(42);
        await Assert.That(counter.Count).IsEqualTo(1);
        await Assert.That(cache.CachedEntryCount).IsEqualTo(1);
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

    [Test]
    public async Task OperationTimeout_BoundsOperationThatIgnoresCancellation()
    {
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = SchemaRegistryOperationTimeout.ExecuteAsync(
            _ => completion.Task,
            TimeSpan.FromMilliseconds(20),
            "timed out");

        try
        {
            await Assert.That(async () => await operation.WaitAsync(TimeSpan.FromSeconds(2)))
                .Throws<TimeoutException>();
        }
        finally
        {
            completion.TrySetResult(42);
        }
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

    private static async Task AssertRegistryResolutionUsesIndependentTimeoutAsync<TValue>(
        MockSchemaRegistryClient registry,
        IAsyncSerializerPreparer<TValue> serializer,
        TValue value)
    {
        var preparation = serializer.PrepareAsync(
            value,
            new SerializationContext
            {
                Topic = "orders",
                Component = SerializationComponent.Value
            });
        await registry.WaitForBlockedGetOrRegisterSchemaAsync(TimeSpan.FromSeconds(2));

        try
        {
            await Assert.That(registry.LastGetOrRegisterSchemaCancellationToken.CanBeCanceled).IsTrue();
        }
        finally
        {
            registry.ReleaseBlockedGetOrRegisterSchema();
        }

        await preparation;
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

    private static Schema CreateDataContractSchema(string owner, string ruleType = "ENCRYPT") =>
        new()
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            Metadata = new SchemaMetadata
            {
                Tags = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
                {
                    ["$.id"] = new HashSet<string>(["PII"], StringComparer.Ordinal)
                },
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["owner"] = owner
                },
                Sensitive = new HashSet<string>(["owner"], StringComparer.Ordinal)
            },
            RuleSet = new SchemaRuleSet
            {
                EnableAt = "1",
                EncodingRules =
                [
                    new SchemaRule
                    {
                        Name = "encrypt-id",
                        Doc = "Encrypt the identifier.",
                        Kind = SchemaRuleKind.Transform,
                        Mode = SchemaRuleMode.WriteRead,
                        Type = ruleType,
                        Tags = new HashSet<string>(["PII"], StringComparer.Ordinal),
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["encrypt.kek.name"] = "orders-kek"
                        },
                        Expr = "true",
                        OnSuccess = "NONE",
                        OnFailure = "ERROR"
                    }
                ]
            }
        };

    private sealed class ResolutionCounter
    {
        internal int Count;
    }

    private sealed class CountingSchemaReferenceList(IReadOnlyList<SchemaReference> values) :
        IReadOnlyList<SchemaReference>
    {
        private int _indexerReadCount;

        internal int IndexerReadCount => Volatile.Read(ref _indexerReadCount);

        public int Count => values.Count;

        public SchemaReference this[int index]
        {
            get
            {
                Interlocked.Increment(ref _indexerReadCount);
                return values[index];
            }
        }

        public IEnumerator<SchemaReference> GetEnumerator() => values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class PreparationPayload
    {
        public int Id { get; init; }
    }
}
