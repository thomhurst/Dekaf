using Dekaf.SchemaRegistry;

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
        int maxCachedSubjects = 1000) =>
        new(client, new AssociatedNameStrategyOptions
        {
            KafkaClusterId = ClusterId,
            FallbackStrategy = fallback,
            MaxCachedSubjects = maxCachedSubjects
        });

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
}
