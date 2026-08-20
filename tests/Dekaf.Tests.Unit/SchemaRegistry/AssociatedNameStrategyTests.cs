using System.Buffers.Binary;
using Avro.Generic;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class AssociatedNameStrategyTests
{
    private const string ClusterId = "lkc-test";

    [Test]
    public async Task GetSubjectNameAsync_AssociationCachesSynchronousWarmResult()
    {
        using var client = new MockSchemaRegistryClient();
        await AssociateAsync(client, "orders", "orders-associated-value");
        var resolver = CreateResolver(client);

        var first = await resolver.GetSubjectNameAsync("orders", "Order", isKey: false);
        var warm = resolver.GetSubjectNameAsync("orders", "Order", isKey: false);

        await Assert.That(first).IsEqualTo("orders-associated-value");
        await Assert.That(warm.IsCompletedSuccessfully).IsTrue();
        await Assert.That(warm.Result).IsEqualTo("orders-associated-value");
        await Assert.That(client.AssociationLookupCallCount).IsEqualTo(1);
        await Assert.That(resolver.CachedSubjectCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetSubjectNameAsync_ConcurrentMissesCoalesce()
    {
        using var client = new MockSchemaRegistryClient();
        await AssociateAsync(client, "orders", "orders-associated-key", isKey: true);
        client.BlockNextAssociationLookup();
        var resolver = CreateResolver(client);
        var resolutions = new ValueTask<string>[32];

        for (var index = 0; index < resolutions.Length; index++)
            resolutions[index] = resolver.GetSubjectNameAsync("orders", "Order", isKey: true);

        await client.WaitForBlockedAssociationLookupAsync(TimeSpan.FromSeconds(5));
        await Assert.That(client.AssociationLookupCallCount).IsEqualTo(1);
        client.ReleaseBlockedAssociationLookup();

        for (var index = 0; index < resolutions.Length; index++)
            await Assert.That(await resolutions[index]).IsEqualTo("orders-associated-key");
        await Assert.That(client.AssociationLookupCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetSubjectNameAsync_CancelledWaiterDoesNotCancelSharedLookup()
    {
        using var client = new MockSchemaRegistryClient();
        await AssociateAsync(client, "orders", "orders-associated-value");
        client.BlockNextAssociationLookup();
        var resolver = CreateResolver(client);
        using var cancellation = new CancellationTokenSource();
        var cancelledWaiter = resolver.GetSubjectNameAsync(
            "orders", "Order", isKey: false, cancellation.Token);
        var sharedWaiter = resolver.GetSubjectNameAsync("orders", "Order", isKey: false);
        await client.WaitForBlockedAssociationLookupAsync(TimeSpan.FromSeconds(5));

        await cancellation.CancelAsync();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => cancelledWaiter.AsTask());
        client.ReleaseBlockedAssociationLookup();

        await Assert.That(await sharedWaiter).IsEqualTo("orders-associated-value");
        await Assert.That(client.AssociationLookupCallCount).IsEqualTo(1);
        await Assert.That(resolver.GetSubjectNameAsync("orders", "Order", isKey: false).IsCompletedSuccessfully)
            .IsTrue();
    }

    [Test]
    public async Task GetSubjectNameAsync_AssociationDoesNotRequireFallbackRecordType()
    {
        using var client = new MockSchemaRegistryClient();
        await AssociateAsync(client, "orders", "orders-associated-value");
        var resolver = CreateResolver(client, fallback: AssociatedNameFallbackStrategy.RecordName);

        var actual = await resolver.GetSubjectNameAsync("orders", recordType: null, isKey: false);

        await Assert.That(actual).IsEqualTo("orders-associated-value");
    }

    [Test]
    public async Task GetSubjectNameAsync_DefaultNamespaceMatchesAnyCluster()
    {
        using var client = new MockSchemaRegistryClient();
        await AssociateAsync(client, "orders", "orders-associated-value");
        var resolver = new AssociatedNameStrategy(client);

        var actual = await resolver.GetSubjectNameAsync("orders", "Order", isKey: false);

        await Assert.That(actual).IsEqualTo("orders-associated-value");
    }

    [Test]
    [Arguments(AssociatedNameFallbackStrategy.TopicName, "orders-value")]
    [Arguments(AssociatedNameFallbackStrategy.RecordName, "Order")]
    [Arguments(AssociatedNameFallbackStrategy.TopicRecordName, "orders-Order")]
    public async Task GetSubjectNameAsync_MissingAssociationUsesConfiguredFallback(
        AssociatedNameFallbackStrategy fallback,
        string expected)
    {
        using var client = new MockSchemaRegistryClient();
        var resolver = CreateResolver(client, fallback: fallback);

        var actual = await resolver.GetSubjectNameAsync("orders", "Order", isKey: false);

        await Assert.That(actual).IsEqualTo(expected);
        await Assert.That(client.AssociationLookupCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetSubjectNameAsync_NoFallbackRejectsMissingAssociationWithoutCaching()
    {
        using var client = new MockSchemaRegistryClient();
        var resolver = CreateResolver(client, fallback: AssociatedNameFallbackStrategy.None);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.GetSubjectNameAsync("orders", "Order", isKey: false).AsTask());
        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.GetSubjectNameAsync("orders", "Order", isKey: false).AsTask());

        await Assert.That(client.AssociationLookupCallCount).IsEqualTo(2);
        await Assert.That(resolver.CachedSubjectCount).IsEqualTo(0);
    }

    [Test]
    public async Task GetSubjectNameAsync_AmbiguousAssociationRejectsWithoutCaching()
    {
        using var client = new MockSchemaRegistryClient();
        await AssociateAsync(client, "orders", "orders-a");
        await AssociateAsync(client, "orders", "orders-b");
        var resolver = CreateResolver(client);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.GetSubjectNameAsync("orders", "Order", isKey: false).AsTask());
        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.GetSubjectNameAsync("orders", "Order", isKey: false).AsTask());

        await Assert.That(client.AssociationLookupCallCount).IsEqualTo(2);
        await Assert.That(resolver.CachedSubjectCount).IsEqualTo(0);
    }

    [Test]
    public async Task GetSubjectNameAsync_FailureIsRecoverableAndNotCached()
    {
        using var client = new MockSchemaRegistryClient { AssociationLookupFailuresRemaining = 1 };
        var resolver = CreateResolver(client);

        _ = await Assert.ThrowsAsync<SchemaRegistryException>(
            () => resolver.GetSubjectNameAsync("orders", "Order", isKey: false).AsTask());
        var recovered = await resolver.GetSubjectNameAsync("orders", "Order", isKey: false);

        await Assert.That(recovered).IsEqualTo("orders-value");
        await Assert.That(client.AssociationLookupCallCount).IsEqualTo(2);
        await Assert.That(resolver.CachedSubjectCount).IsEqualTo(1);
    }

    [Test]
    public async Task RefreshAndInvalidate_ExposeAssociationChangesAndDeletion()
    {
        using var client = new MockSchemaRegistryClient();
        await AssociateAsync(client, "orders", "orders-v1");
        var resolver = CreateResolver(client);
        await Assert.That(await resolver.GetSubjectNameAsync("orders", "Order", isKey: false))
            .IsEqualTo("orders-v1");

        await client.DeleteAssociationsAsync(
            ResourceId("orders"),
            "topic",
            ["value"]);
        await AssociateAsync(client, "orders", "orders-v2");

        await Assert.That(await resolver.GetSubjectNameAsync("orders", "Order", isKey: false))
            .IsEqualTo("orders-v1");
        await Assert.That(await resolver.RefreshAsync("orders", "Order", isKey: false))
            .IsEqualTo("orders-v2");

        await client.DeleteAssociationsAsync(
            ResourceId("orders"),
            "topic",
            ["value"]);
        await Assert.That(await resolver.RefreshAsync("orders", "Order", isKey: false))
            .IsEqualTo("orders-value");
        await Assert.That(resolver.Invalidate("orders", "Order", isKey: false)).IsTrue();
        await Assert.That(resolver.Invalidate("orders", "Order", isKey: false)).IsFalse();

        _ = await resolver.GetSubjectNameAsync("orders", "Order", isKey: false);
        resolver.ClearCache();
        await Assert.That(resolver.CachedSubjectCount).IsEqualTo(0);
    }

    [Test]
    public async Task RefreshAsync_DoesNotJoinOlderNormalLookup()
    {
        using var client = new MockSchemaRegistryClient();
        var staleResponse = new TaskCompletionSource<IReadOnlyList<Association>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.EnqueueAssociationLookup(staleResponse.Task);
        client.EnqueueAssociationLookup(Task.FromResult<IReadOnlyList<Association>>(
            [CreateAssociation("orders", "orders-v2")]));
        var resolver = CreateResolver(client);

        var normalLookup = resolver.GetSubjectNameAsync("orders", "Order", isKey: false);
        await Assert.That(() => client.AssociationLookupCallCount)
            .Eventually(count => count.IsEqualTo(1), TimeSpan.FromSeconds(5));
        var refresh = resolver.RefreshAsync("orders", "Order", isKey: false);

        await Assert.That(await refresh).IsEqualTo("orders-v2");
        staleResponse.SetResult([CreateAssociation("orders", "orders-v1")]);
        await Assert.That(await normalLookup).IsEqualTo("orders-v1");
        await Assert.That(await resolver.GetSubjectNameAsync("orders", "Order", isKey: false))
            .IsEqualTo("orders-v2");
        await Assert.That(client.AssociationLookupCallCount).IsEqualTo(2);
    }

    [Test]
    public async Task GetSubjectNameAsync_InternalTimeoutClearsPendingLookup()
    {
        using var client = new MockSchemaRegistryClient();
        client.BlockNextAssociationLookup();
        var resolver = CreateResolver(client, lookupTimeout: TimeSpan.FromMilliseconds(10));

        _ = await Assert.ThrowsAsync<TimeoutException>(
            () => resolver.GetSubjectNameAsync("orders", "Order", isKey: false).AsTask());
        client.ReleaseBlockedAssociationLookup();

        await AssociateAsync(client, "orders", "orders-recovered");
        await Assert.That(await resolver.GetSubjectNameAsync("orders", "Order", isKey: false))
            .IsEqualTo("orders-recovered");
        await Assert.That(client.AssociationLookupCallCount).IsEqualTo(2);
    }

    [Test]
    public async Task GenericSerializer_PrepareAsync_ResolvesAssociatedSubject()
    {
        using var client = new MockSchemaRegistryClient();
        await AssociateAsync(client, "orders", "orders-associated-value");
        await using var serializer = new SchemaRegistrySerializer<int>(
            client,
            static (_, _) => { },
            static () => new Schema { SchemaType = SchemaType.Json, SchemaString = "{\"type\":\"integer\"}" },
            SubjectNameStrategy.AssociatedName);

        var resolved = await serializer.PrepareAsync("orders", 42);

        await Assert.That(resolved.Subject).IsEqualTo("orders-associated-value");
        await Assert.That(client.AssociationLookupCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task GenericSerializer_PrepareAsync_ObservesAssociationCacheInvalidation()
    {
        using var client = new MockSchemaRegistryClient();
        await AssociateAsync(client, "orders", "orders-v1");
        var resolver = CreateResolver(client);
        await using var serializer = new SchemaRegistrySerializer<int>(
            client,
            static (_, _) => { },
            resolver,
            static () => new Schema { SchemaType = SchemaType.Json, SchemaString = "{\"type\":\"integer\"}" });

        var first = await serializer.PrepareAsync("orders", 42);

        await ReplaceAssociationAsync(client, "orders", "orders-v2");
        _ = await resolver.RefreshAsync("orders", typeof(int).FullName, isKey: false);
        var refreshed = await serializer.PrepareAsync("orders", 42);

        await ReplaceAssociationAsync(client, "orders", "orders-v3");
        await Assert.That(resolver.Invalidate("orders", typeof(int).FullName, isKey: false)).IsTrue();
        var invalidated = await serializer.PrepareAsync("orders", 42);

        await ReplaceAssociationAsync(client, "orders", "orders-v4");
        resolver.ClearCache();
        var cleared = await serializer.PrepareAsync("orders", 42);

        await Assert.That(first.Subject).IsEqualTo("orders-v1");
        await Assert.That(refreshed.Subject).IsEqualTo("orders-v2");
        await Assert.That(invalidated.Subject).IsEqualTo("orders-v3");
        await Assert.That(cleared.Subject).IsEqualTo("orders-v4");
    }

    [Test]
    public async Task JsonSerializer_PrepareAsync_ObservesAssociationCacheInvalidation()
    {
        using var client = new MockSchemaRegistryClient();
        await AssociateAsync(client, "orders", "json-v1");
        var resolver = CreateResolver(client);
        await using var serializer = new JsonSchemaRegistrySerializer<int>(
            client,
            resolver,
            """{"type":"integer"}""");

        var first = await serializer.PrepareAsync("orders", 42);
        await ReplaceAssociationAsync(client, "orders", "json-v2");
        resolver.ClearCache();
        var second = await serializer.PrepareAsync("orders", 42);

        await Assert.That(first.Subject).IsEqualTo("json-v1");
        await Assert.That(second.Subject).IsEqualTo("json-v2");
    }

    [Test]
    public async Task AvroSerializer_PrepareAsync_ObservesAssociationCacheInvalidation()
    {
        using var client = new MockSchemaRegistryClient();
        await AssociateAsync(client, "orders", "avro-v1");
        var resolver = CreateResolver(client);
        var schema = (global::Avro.RecordSchema)global::Avro.Schema.Parse(
            """{"type":"record","name":"Order","fields":[{"name":"id","type":"int"}]}""");
        var value = new GenericRecord(schema);
        value.Add("id", 42);
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            client,
            new AvroSerializerConfig { AsyncSubjectNameStrategy = resolver });

        var first = await serializer.PrepareAsync("orders", value);
        await ReplaceAssociationAsync(client, "orders", "avro-v2");
        resolver.ClearCache();
        var second = await serializer.PrepareAsync("orders", value);

        await Assert.That(first.Subject).IsEqualTo("avro-v1");
        await Assert.That(second.Subject).IsEqualTo("avro-v2");
    }

    [Test]
    public async Task ProtobufSerializer_PrepareAsync_ObservesAssociationCacheInvalidation()
    {
        using var client = new MockSchemaRegistryClient();
        await AssociateAsync(client, "orders", "protobuf-v1");
        var resolver = CreateResolver(client);
        var value = new TestMessage { Id = 42 };
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(
            client,
            new ProtobufSerializerConfig { AsyncSubjectNameStrategy = resolver });

        var first = await serializer.PrepareAsync("orders", value);
        await ReplaceAssociationAsync(client, "orders", "protobuf-v2");
        resolver.ClearCache();
        var second = await serializer.PrepareAsync("orders", value);

        await Assert.That(first.Subject).IsEqualTo("protobuf-v1");
        await Assert.That(second.Subject).IsEqualTo("protobuf-v2");
    }

    [Test]
    public async Task DeserializerCache_ObservesAssociationCacheInvalidation()
    {
        using var client = new MockSchemaRegistryClient();
        var schemaId = await client.RegisterSchemaAsync("schema-subject", new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{\"type\":\"integer\"}"
        });
        client.AddSchemaSubject(schemaId, "orders-v1");
        client.AddSchemaSubject(schemaId, "orders-v2");
        await AssociateAsync(client, "orders", "orders-v1");
        var resolver = CreateResolver(client);
        var subjects = DeserializerSubjectNameCache.Create(
            client,
            new SchemaRegistryDeserializerConfig { AsyncSubjectNameStrategy = resolver })!;

        await subjects.PrepareAsync(
            client,
            schemaId,
            "orders",
            isKey: false,
            typeof(int).FullName!,
            CancellationToken.None);
        await Assert.That(client.GetSchemaCallCount).IsEqualTo(2);
        var first = subjects.GetSubjectName(
            schemaId,
            schema: null,
            "orders",
            isKey: false,
            typeof(int).FullName!);

        await ReplaceAssociationAsync(client, "orders", "orders-v2");
        _ = await resolver.RefreshAsync("orders", typeof(int).FullName, isKey: false);

        await Assert.That(subjects.IsPrepared(schemaId, "orders", isKey: false)).IsFalse();
        await subjects.PrepareAsync(
            client,
            schemaId,
            "orders",
            isKey: false,
            typeof(int).FullName!,
            CancellationToken.None);
        var refreshed = subjects.GetSubjectName(
            schemaId,
            schema: null,
            "orders",
            isKey: false,
            typeof(int).FullName!);

        await Assert.That(first).IsEqualTo("orders-v1");
        await Assert.That(refreshed).IsEqualTo("orders-v2");
    }

    [Test]
    public async Task DeserializerCache_InvalidationDuringPreparationRetriesLookup()
    {
        using var client = new MockSchemaRegistryClient();
        var schemaId = await client.RegisterSchemaAsync("schema-subject", new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{\"type\":\"integer\"}"
        });
        client.AddSchemaSubject(schemaId, "orders-v1");
        await AssociateAsync(client, "orders", "orders-v1");
        client.BlockNextAssociationLookup();
        var resolver = CreateResolver(client);
        var subjects = DeserializerSubjectNameCache.Create(
            client,
            new SchemaRegistryDeserializerConfig { AsyncSubjectNameStrategy = resolver })!;
        var preparation = subjects.PrepareAsync(
            client,
            schemaId,
            "orders",
            isKey: false,
            typeof(int).FullName!,
            CancellationToken.None);
        await client.WaitForBlockedAssociationLookupAsync(TimeSpan.FromSeconds(5));

        await Assert.That(resolver.Invalidate("orders", typeof(int).FullName, isKey: false)).IsFalse();
        client.ReleaseBlockedAssociationLookup();
        await preparation;

        await Assert.That(subjects.IsPrepared(schemaId, "orders", isKey: false)).IsTrue();
        await Assert.That(client.AssociationLookupCallCount).IsEqualTo(2);
    }

    [Test]
    public async Task GenericDeserializer_AssociatedNamePreparesBeforeReadRules()
    {
        using var client = new MockSchemaRegistryClient();
        const string subject = "orders-associated-value";
        var schemaId = await client.RegisterSchemaAsync(subject, new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = """{"type":"integer"}"""
        });
        await AssociateAsync(client, "orders", subject);
        var executor = new CapturingRuleExecutor();
        await using var deserializer = new SchemaRegistryDeserializer<int>(
            client,
            static (_, _) => 42,
            ownsClient: false,
            executor,
            new SchemaRegistryDeserializerConfig
            {
                SubjectNameStrategy = SubjectNameStrategy.AssociatedName
            });
        var preparer = (IAsyncDeserializerPreparer<int>)deserializer;
        var context = new SerializationContext
        {
            Topic = "orders",
            Component = SerializationComponent.Value
        };
        var data = new byte[5];
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(1), schemaId);

        var cold = preparer.TryDeserialize(data, context, out _);
        await preparer.PrepareAsync(data, context);
        var warm = preparer.TryDeserialize(data, context, out var value);

        await Assert.That(preparer.RequiresPreparation).IsTrue();
        await Assert.That(cold).IsFalse();
        await Assert.That(warm).IsTrue();
        await Assert.That(value).IsEqualTo(42);
        await Assert.That(executor.Subject).IsEqualTo(subject);
        await Assert.That(client.AssociationLookupCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task GenericDeserializer_SynchronousStrategyDisablesPreparationPath()
    {
        using var client = new MockSchemaRegistryClient();
        await using var deserializer = new SchemaRegistryDeserializer<int>(
            client,
            static (_, _) => 42,
            ownsClient: false,
            ruleExecutor: new CapturingRuleExecutor(),
            config: new SchemaRegistryDeserializerConfig());

        var preparer = (IAsyncDeserializerPreparer<int>)deserializer;

        await Assert.That(preparer.RequiresPreparation).IsFalse();
    }

    [Test]
    public async Task SchemaDeserializers_ConfiguredAsyncStrategyEnablesPreparation()
    {
        using var client = new MockSchemaRegistryClient();
        var strategy = new FixedAsyncSubjectNameStrategy("configured-associated");
        var executor = new CapturingRuleExecutor();
        var genericConfig = new SchemaRegistryDeserializerConfig
        {
            AsyncSubjectNameStrategy = strategy
        };
        await using var json = new JsonSchemaRegistryDeserializer<int>(
            client,
            jsonOptions: null,
            config: genericConfig,
            ownsClient: false,
            ruleExecutor: executor);
        await using var avro = new AvroSchemaRegistryDeserializer<GenericRecord>(
            client,
            new AvroDeserializerConfig
            {
                AsyncSubjectNameStrategy = strategy,
                RuleExecutor = executor
            });
        await using var protobuf = new ProtobufSchemaRegistryDeserializer<TestMessage>(
            client,
            new ProtobufDeserializerConfig
            {
                AsyncSubjectNameStrategy = strategy,
                RuleExecutor = executor
            });

        await Assert.That(((IAsyncDeserializerPreparer<int>)json).RequiresPreparation).IsTrue();
        await Assert.That(((IAsyncDeserializerPreparer<GenericRecord>)avro).RequiresPreparation).IsTrue();
        await Assert.That(((IAsyncDeserializerPreparer<TestMessage>)protobuf).RequiresPreparation).IsTrue();
    }

    [Test]
    public async Task GenericSerializer_ConfiguredAsyncStrategyIsUsed()
    {
        using var client = new MockSchemaRegistryClient();
        var strategy = new FixedAsyncSubjectNameStrategy("configured-associated");
        await using var serializer = new SchemaRegistrySerializer<int>(
            client,
            static (_, _) => { },
            strategy,
            static () => new Schema
            {
                SchemaType = SchemaType.Json,
                SchemaString = """{"type":"integer"}"""
            });

        var resolved = await serializer.PrepareAsync("orders", 42);

        await Assert.That(resolved.Subject).IsEqualTo("configured-associated");
        await Assert.That(strategy.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Cache_EvictsOldestSuccessfulResolutionAtConfiguredBound()
    {
        using var client = new MockSchemaRegistryClient();
        var resolver = CreateResolver(client, maxCachedSubjects: 2);

        _ = await resolver.GetSubjectNameAsync("one", "Record", isKey: false);
        _ = await resolver.GetSubjectNameAsync("two", "Record", isKey: false);
        _ = await resolver.GetSubjectNameAsync("three", "Record", isKey: false);

        await Assert.That(resolver.CachedSubjectCount).IsEqualTo(2);
        _ = await resolver.GetSubjectNameAsync("one", "Record", isKey: false);
        await Assert.That(client.AssociationLookupCallCount).IsEqualTo(4);
        await Assert.That(resolver.CachedSubjectCount).IsEqualTo(2);
    }

    [Test]
    public async Task Invalidate_DuringLookupPreventsStalePublication()
    {
        using var client = new MockSchemaRegistryClient();
        await AssociateAsync(client, "orders", "orders-v1");
        client.BlockNextAssociationLookup();
        var resolver = CreateResolver(client);
        var lookup = resolver.GetSubjectNameAsync("orders", "Order", isKey: false);
        await client.WaitForBlockedAssociationLookupAsync(TimeSpan.FromSeconds(5));

        await Assert.That(resolver.Invalidate("orders", "Order", isKey: false)).IsFalse();
        client.ReleaseBlockedAssociationLookup();
        await Assert.That(await lookup).IsEqualTo("orders-v1");
        await Assert.That(resolver.CachedSubjectCount).IsEqualTo(0);

        _ = await resolver.GetSubjectNameAsync("orders", "Order", isKey: false);
        await Assert.That(client.AssociationLookupCallCount).IsEqualTo(2);
    }

    [Test]
    public async Task Invalidate_DuringLookupStartsFreshLookupForNewCaller()
    {
        using var client = new MockSchemaRegistryClient();
        var staleResponse = new TaskCompletionSource<IReadOnlyList<Association>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.EnqueueAssociationLookup(staleResponse.Task);
        client.EnqueueAssociationLookup(Task.FromResult<IReadOnlyList<Association>>(
            [CreateAssociation("orders", "orders-v2")]));
        var resolver = CreateResolver(client);
        var staleLookup = resolver.GetSubjectNameAsync("orders", "Order", isKey: false);
        await Assert.That(() => client.AssociationLookupCallCount)
            .Eventually(count => count.IsEqualTo(1), TimeSpan.FromSeconds(5));

        await Assert.That(resolver.Invalidate("orders", "Order", isKey: false)).IsFalse();
        var freshLookup = resolver.GetSubjectNameAsync("orders", "Order", isKey: false);
        try
        {
            await Assert.That(() => client.AssociationLookupCallCount)
                .Eventually(count => count.IsEqualTo(2), TimeSpan.FromSeconds(5));
        }
        finally
        {
            staleResponse.TrySetResult([CreateAssociation("orders", "orders-v1")]);
        }

        await Assert.That(await staleLookup).IsEqualTo("orders-v1");
        await Assert.That(await freshLookup).IsEqualTo("orders-v2");
        await Assert.That(await resolver.GetSubjectNameAsync("orders", "Order", isKey: false))
            .IsEqualTo("orders-v2");
    }

    [Test]
    public async Task Invalidate_DoesNotSuppressUnrelatedInFlightPublication()
    {
        using var client = new MockSchemaRegistryClient();
        client.BlockNextAssociationLookup();
        var resolver = CreateResolver(client);
        var lookup = resolver.GetSubjectNameAsync("orders", "Order", isKey: false);
        await client.WaitForBlockedAssociationLookupAsync(TimeSpan.FromSeconds(5));

        await Assert.That(resolver.Invalidate("payments", "Payment", isKey: false)).IsFalse();
        client.ReleaseBlockedAssociationLookup();
        await Assert.That(await lookup).IsEqualTo("orders-value");

        await Assert.That(resolver.CachedSubjectCount).IsEqualTo(1);
        await Assert.That(resolver.GetSubjectNameAsync("orders", "Order", isKey: false).IsCompletedSuccessfully)
            .IsTrue();
        await Assert.That(client.AssociationLookupCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task ConstructorAndResolution_RejectInvalidConfiguration()
    {
        using var client = new MockSchemaRegistryClient();

        await Assert.That(() => new AssociatedNameStrategy(client, new AssociatedNameStrategyOptions
        {
            MaxCachedSubjects = 0
        })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new AssociatedNameStrategy(client, new AssociatedNameStrategyOptions
        {
            FallbackStrategy = (AssociatedNameFallbackStrategy)int.MaxValue
        })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new AssociatedNameStrategy(client, new AssociatedNameStrategyOptions
        {
            LookupTimeout = TimeSpan.Zero
        })).Throws<ArgumentOutOfRangeException>();
        var resolver = CreateResolver(client, fallback: AssociatedNameFallbackStrategy.RecordName);
        await Assert.That(() =>
            {
                _ = resolver.GetSubjectNameAsync(" ", "Order", isKey: false);
            })
            .Throws<ArgumentException>();
        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.GetSubjectNameAsync("orders", recordType: null, isKey: false).AsTask());
    }

    [Test]
    public async Task AssociatedName_RequiresAsyncResolution()
    {
        await Assert.That(() => SubjectNameResolver.GetSubjectName(
                SubjectNameStrategy.AssociatedName,
                "orders",
                "Order",
                isKey: false,
                useLegacySubjectNames: false))
            .Throws<InvalidOperationException>();
    }

    private static AssociatedNameStrategy CreateResolver(
        ISchemaRegistryClient client,
        AssociatedNameFallbackStrategy fallback = AssociatedNameFallbackStrategy.TopicName,
        int maxCachedSubjects = 1000,
        TimeSpan? lookupTimeout = null) =>
        new(client, new AssociatedNameStrategyOptions
        {
            KafkaClusterId = ClusterId,
            FallbackStrategy = fallback,
            MaxCachedSubjects = maxCachedSubjects,
            LookupTimeout = lookupTimeout ?? TimeSpan.FromSeconds(30)
        });

    private static async Task ReplaceAssociationAsync(
        MockSchemaRegistryClient client,
        string topic,
        string subject)
    {
        await client.DeleteAssociationsAsync(ResourceId(topic), "topic", ["value"]);
        await AssociateAsync(client, topic, subject);
    }

    private static Association CreateAssociation(string topic, string subject) => new()
    {
        Subject = subject,
        Guid = $"guid-{subject}",
        ResourceName = topic,
        ResourceNamespace = ClusterId,
        ResourceId = ResourceId(topic),
        ResourceType = "topic",
        AssociationType = "value",
        Lifecycle = "WEAK"
    };

    private static Task<AssociationResponse> AssociateAsync(
        ISchemaRegistryClient client,
        string topic,
        string subject,
        bool isKey = false) =>
        client.CreateAssociationAsync(new AssociationCreateOrUpdateRequest
        {
            ResourceName = topic,
            ResourceNamespace = ClusterId,
            ResourceId = ResourceId(topic),
            ResourceType = "topic",
            Associations =
            [
                new AssociationCreateOrUpdateInfo
                {
                    Subject = subject,
                    AssociationType = isKey ? "key" : "value",
                    Lifecycle = "WEAK"
                }
            ]
        });

    private static string ResourceId(string topic) => $"{ClusterId}:{topic}";

    private sealed class CapturingRuleExecutor : ISchemaRegistryRuleExecutor
    {
        public string? Subject { get; private set; }

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
        {
            Subject = context.Subject;
            return payload;
        }
    }

    private sealed class FixedAsyncSubjectNameStrategy(string subject) : IAsyncSubjectNameStrategy
    {
        public int CallCount { get; private set; }

        public ValueTask<string> GetSubjectNameAsync(
            string topic,
            string? recordType,
            bool isKey,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return new ValueTask<string>(subject);
        }
    }
}
