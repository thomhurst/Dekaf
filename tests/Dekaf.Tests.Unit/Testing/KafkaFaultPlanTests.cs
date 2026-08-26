using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class KafkaFaultPlanTests
{
    private static readonly KafkaFaultScope ProduceOrders = new(
        KafkaFaultOperation.Produce,
        topic: "orders");

    [Test]
    public async Task KafkaFaultBarrier_HasNoPublicConstructor() =>
        await Assert.That(typeof(KafkaFaultBarrier).GetConstructors()).IsEmpty();

    [Test]
    public async Task Fail_ConsumesOrderedOneShotAndNextNRules()
    {
        IKafkaFaultPlan plan = new KafkaFaultPlan();
        var first = new InvalidOperationException("first");
        var second = new TimeoutException("second");
        plan.Fail(ProduceOrders, first);
        plan.Fail(ProduceOrders, second, occurrenceCount: 2);

        await AssertFaultAsync(plan, ProduceOrders, first);
        await AssertFaultAsync(plan, ProduceOrders, second);
        await AssertFaultAsync(plan, ProduceOrders, second);
        await plan.ApplyAsync(ProduceOrders);

        await Assert.That(plan.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ApplyAsync_MatchesAllConfiguredSelectorsBeforeConsuming()
    {
        var plan = new KafkaFaultPlan();
        var failure = new InvalidOperationException("scoped");
        var rule = new KafkaFaultScope(
            KafkaFaultOperation.Commit,
            topic: "orders",
            partition: 2,
            groupId: "billing");
        plan.Fail(rule, failure);

        await plan.ApplyAsync(new KafkaFaultScope(KafkaFaultOperation.Commit, "orders", 1, "billing"));
        await plan.ApplyAsync(new KafkaFaultScope(KafkaFaultOperation.Commit, "orders", 2, "other"));
        await plan.ApplyAsync(new KafkaFaultScope(KafkaFaultOperation.Consume, "orders", 2, "billing"));
        await AssertFaultAsync(plan, rule, failure);

        await Assert.That(plan.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ApplyAsync_NullSelectorsMatchConcreteResources()
    {
        var plan = new KafkaFaultPlan();
        var failure = new InvalidOperationException("wildcard");
        plan.Fail(new KafkaFaultScope(KafkaFaultOperation.Fetch), failure);
        var concreteScope = new KafkaFaultScope(
            KafkaFaultOperation.Fetch,
            "orders",
            4,
            "billing");

        await Assert.That(plan.HasMatchingFault(concreteScope)).IsTrue();
        await AssertFaultAsync(plan, concreteScope, failure);
        await Assert.That(plan.HasMatchingFault(concreteScope)).IsFalse();
    }

    [Test]
    [Arguments(KafkaFaultOperation.JoinGroup)]
    [Arguments(KafkaFaultOperation.SyncGroup)]
    [Arguments(KafkaFaultOperation.Rebalance)]
    public async Task GroupTransitionScope_RejectsResourceSelectors(
        KafkaFaultOperation operation)
    {
        var topicError = Assert.Throws<ArgumentException>(() =>
            _ = new KafkaFaultScope(operation, topic: "orders", groupId: "workers"));
        var partitionError = Assert.Throws<ArgumentException>(() =>
            _ = new KafkaFaultScope(operation, partition: 0, groupId: "workers"));

        await Assert.That(topicError!.ParamName).IsEqualTo("topic");
        await Assert.That(partitionError!.ParamName).IsEqualTo("partition");
    }

    [Test]
    public async Task ScopeIndex_FiltersUnrelatedOperationsAndGroups()
    {
        var plan = new KafkaFaultPlan();
        plan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.Fetch, topic: "orders", groupId: "billing"),
            new InvalidOperationException("fetch"));

        await Assert.That(plan.HasPotentialMatch(KafkaFaultOperation.Admin, "billing")).IsFalse();
        await Assert.That(plan.HasPotentialMatch(KafkaFaultOperation.Fetch, "other")).IsFalse();
        await Assert.That(plan.HasPotentialMatch(KafkaFaultOperation.Fetch, "billing")).IsTrue();
        await Assert.That(plan.HasMatchingFault(
            new KafkaFaultScope(KafkaFaultOperation.Fetch, "payments", 0, "billing"))).IsFalse();
        await Assert.That(plan.HasMatchingFault(
            new KafkaFaultScope(KafkaFaultOperation.Fetch, "orders", 0, "billing"))).IsTrue();
    }

    [Test]
    public async Task ScopeIndex_FiltersUnassignedResourcesAndPublishesVersion()
    {
        var plan = new KafkaFaultPlan();
        var assignment = new HashSet<TopicPartition> { new("orders", 0) };
        plan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.Fetch, "payments", 0, "billing"),
            new InvalidOperationException("fetch"));

        var matched = plan.HasPotentialConsumerMatch(
            "billing",
            assignment,
            assignment,
            includeCommit: true,
            out var version);

        await Assert.That(matched).IsFalse();
        await Assert.That(version).IsEqualTo(plan.Version);

        plan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.Consume, "orders", 0, "billing"),
            new InvalidOperationException("consume"));

        await Assert.That(plan.Version).IsGreaterThan(version);
        await Assert.That(plan.HasPotentialConsumerMatch(
            "billing",
            assignment,
            assignment,
            includeCommit: true,
            out _)).IsTrue();
    }

    [Test]
    [Arguments(KafkaFaultOperation.JoinGroup)]
    [Arguments(KafkaFaultOperation.SyncGroup)]
    [Arguments(KafkaFaultOperation.Rebalance)]
    public async Task HasPotentialFault_EmptyResourcesMatchesResourceFreeRule(
        KafkaFaultOperation operation)
    {
        var plan = new KafkaFaultPlan();
        var resources = new HashSet<TopicPartition>();
        plan.FailPersistently(
            new KafkaFaultScope(operation, groupId: "billing"),
            new InvalidOperationException("group transition"));

        await Assert.That(plan.HasPotentialFault(operation, "billing", resources)).IsTrue();
        await Assert.That(plan.HasPotentialFault(operation, "other", resources)).IsFalse();
    }

    [Test]
    public async Task ScopeIndex_RemovesConsumedAndClearedRules()
    {
        var plan = new KafkaFaultPlan();
        var scope = new KafkaFaultScope(KafkaFaultOperation.Fetch, groupId: "billing");
        plan.Fail(scope, new InvalidOperationException("fetch"));

        await Assert.That(plan.HasPotentialMatch(KafkaFaultOperation.Fetch, "billing")).IsTrue();
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => plan.ApplyAsync(scope).AsTask());
        await Assert.That(plan.HasPotentialMatch(KafkaFaultOperation.Fetch, "billing")).IsFalse();

        plan.FailPersistently(scope, new InvalidOperationException("fetch"));
        plan.Clear(scope);
        await Assert.That(plan.HasMatchingFault(scope)).IsFalse();
    }

    [Test]
    public async Task ShareIndex_FiltersTopicPartitionAndGroupSelectors()
    {
        var plan = new KafkaFaultPlan();
        var assignment = new HashSet<TopicPartition> { new("shared", 0) };
        var failure = new InvalidOperationException("unrelated");
        plan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.ShareConsume, "other", 0, "workers"),
            failure);
        plan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.ShareConsume, "shared", 1, "workers"),
            failure);
        plan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.ShareConsume, "shared", 0, "other-group"),
            failure);

        await Assert.That(plan.HasPotentialShareMatch(
            KafkaFaultOperation.ShareConsume,
            "workers",
            assignment)).IsFalse();
        await Assert.That(plan.HasPotentialShareMatch(
            KafkaFaultOperation.ShareConsume,
            "shared",
            0,
            "workers")).IsFalse();

        plan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.ShareConsume, "shared", 0, "workers"),
            failure);

        await Assert.That(plan.HasPotentialShareMatch(
            KafkaFaultOperation.ShareConsume,
            "workers",
            assignment)).IsTrue();
        await Assert.That(plan.HasPotentialShareMatch(
            KafkaFaultOperation.ShareConsume,
            "shared",
            0,
            "workers")).IsTrue();
        await Assert.That(plan.HasPotentialShareMatch(
            KafkaFaultOperation.ShareAcknowledge,
            "workers",
            assignment)).IsFalse();
    }

    [Test]
    public async Task ShareIndex_MatchesWildcardSelectors()
    {
        var assignment = new HashSet<TopicPartition> { new("shared", 0) };
        KafkaFaultScope[] scopes =
        [
            new(KafkaFaultOperation.ShareConsume),
            new(KafkaFaultOperation.ShareConsume, topic: "shared"),
            new(KafkaFaultOperation.ShareConsume, partition: 0),
            new(KafkaFaultOperation.ShareConsume, groupId: "workers"),
            new(KafkaFaultOperation.ShareConsume, topic: "shared", partition: 0),
            new(KafkaFaultOperation.ShareConsume, topic: "shared", groupId: "workers"),
            new(KafkaFaultOperation.ShareConsume, partition: 0, groupId: "workers"),
            new(KafkaFaultOperation.ShareConsume, "shared", 0, "workers")
        ];

        for (var index = 0; index < scopes.Length; index++)
        {
            var plan = new KafkaFaultPlan();
            var failure = new InvalidOperationException($"wildcard-{index}");
            plan.Fail(scopes[index], failure);

            await Assert.That(plan.HasPotentialShareMatch(
                KafkaFaultOperation.ShareConsume,
                "workers",
                assignment)).IsTrue();
            await Assert.That(plan.HasPotentialShareMatch(
                KafkaFaultOperation.ShareConsume,
                "shared",
                0,
                "workers")).IsTrue();
            await AssertFaultAsync(
                plan,
                new KafkaFaultScope(KafkaFaultOperation.ShareConsume, "shared", 0, "workers"),
                failure);
            await Assert.That(plan.HasPotentialShareMatch(
                KafkaFaultOperation.ShareConsume,
                "workers",
                assignment)).IsFalse();
        }
    }

    [Test]
    public async Task ShareIndex_RemovesConsumedScopesIncrementally()
    {
        var plan = new KafkaFaultPlan();
        var scope = new KafkaFaultScope(
            KafkaFaultOperation.ShareConsume,
            "shared",
            partition: 0,
            groupId: "workers");
        var assignment = new HashSet<TopicPartition> { new("shared", 0) };
        var firstFailure = new InvalidOperationException("first");
        var secondFailure = new InvalidOperationException("second");
        plan.Fail(scope, firstFailure);
        var indexedVersion = plan.ShareFaultIndexVersion;
        plan.Fail(scope, secondFailure);

        await Assert.That(plan.ShareFaultIndexVersion).IsEqualTo(indexedVersion);
        await AssertFaultAsync(plan, scope, firstFailure);
        await Assert.That(plan.ShareFaultIndexVersion).IsEqualTo(indexedVersion);
        await Assert.That(plan.HasPotentialShareMatch(
            KafkaFaultOperation.ShareConsume,
            "workers",
            assignment)).IsTrue();

        await AssertFaultAsync(plan, scope, secondFailure);

        await Assert.That(plan.ShareFaultIndexVersion).IsGreaterThan(indexedVersion);
        await Assert.That(plan.HasPotentialShareMatch(
            KafkaFaultOperation.ShareConsume,
            "workers",
            assignment)).IsFalse();
        await Assert.That(plan.RetainedShareFaultSelectorCount).IsEqualTo(0);
    }

    [Test]
    public async Task ShareIndex_RemovesConsumedSelectorsWhilePersistentRuleRemains()
    {
        var plan = new KafkaFaultPlan();
        var persistentScope = new KafkaFaultScope(
            KafkaFaultOperation.ShareConsume,
            groupId: "persistent-workers");
        plan.FailPersistently(persistentScope, new InvalidOperationException("persistent"));

        for (var index = 0; index < 1_024; index++)
        {
            var scope = new KafkaFaultScope(
                KafkaFaultOperation.ShareConsume,
                $"shared-{index}",
                partition: index,
                groupId: $"workers-{index}");
            var barrier = plan.PauseNext(scope);
            barrier.Release();

            await plan.ApplyAsync(scope);
            await Assert.That(plan.RetainedShareFaultSelectorCount).IsEqualTo(1);
        }
    }

    [Test]
    public async Task ShareIndex_UnrelatedPlanChangesDoNotInvalidateVersion()
    {
        var plan = new KafkaFaultPlan();
        var version = plan.ShareFaultIndexVersion;

        plan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Admin),
            new InvalidOperationException("admin"));

        await Assert.That(plan.ShareFaultIndexVersion).IsEqualTo(version);
    }

    [Test]
    public async Task FailPersistently_RemainsUntilExactScopeIsCleared()
    {
        var plan = new KafkaFaultPlan();
        var failure = new InvalidOperationException("persistent");
        plan.FailPersistently(ProduceOrders, failure);

        await AssertFaultAsync(plan, ProduceOrders, failure);
        await AssertFaultAsync(plan, ProduceOrders, failure);
        await Assert.That(plan.Count).IsEqualTo(1);
        await Assert.That(plan.Clear(ProduceOrders)).IsEqualTo(1);

        await plan.ApplyAsync(ProduceOrders);
        await Assert.That(plan.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Fail_IsThreadSafeAndConsumesExactOccurrenceCount()
    {
        var plan = new KafkaFaultPlan();
        plan.Fail(ProduceOrders, new InvalidOperationException("concurrent"), occurrenceCount: 32);
        var consumed = 0;
        plan.FaultConsumed += observation =>
        {
            if (observation.Action == KafkaFaultAction.Throw)
                Interlocked.Increment(ref consumed);
        };

        var operations = new Task[64];
        for (var i = 0; i < operations.Length; i++)
            operations[i] = Task.Run(() => ApplyIgnoringFaultAsync(plan, ProduceOrders));

        await Task.WhenAll(operations);

        await Assert.That(consumed).IsEqualTo(32);
        await Assert.That(plan.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ApplyAsync_PreCanceledTokenDoesNotConsumeRule()
    {
        var plan = new KafkaFaultPlan();
        var failure = new InvalidOperationException("still queued");
        plan.Fail(ProduceOrders, failure);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            () => plan.ApplyAsync(ProduceOrders, cancellation.Token).AsTask());

        await Assert.That(plan.Count).IsEqualTo(1);
        await AssertFaultAsync(plan, ProduceOrders, failure);
    }

    [Test]
    public async Task ApplyAsync_EmptyPlanCompletesSynchronously()
    {
        var plan = new KafkaFaultPlan();

        var application = plan.ApplyAsync(ProduceOrders);

        await Assert.That(application.IsCompletedSuccessfully).IsTrue();
        await application;
    }

    [Test]
    public async Task Clear_ExactScopeLeavesOtherRulesQueued()
    {
        var plan = new KafkaFaultPlan();
        var otherScope = new KafkaFaultScope(KafkaFaultOperation.Produce, "payments");
        var otherFailure = new InvalidOperationException("payments");
        plan.Fail(ProduceOrders, new InvalidOperationException("orders"));
        plan.Fail(otherScope, otherFailure);

        var removed = plan.Clear(ProduceOrders);

        await Assert.That(removed).IsEqualTo(1);
        await Assert.That(plan.Count).IsEqualTo(1);
        await plan.ApplyAsync(ProduceOrders);
        await AssertFaultAsync(plan, otherScope, otherFailure);
    }

    [Test]
    public async Task Clear_ExactScopeCancelsUnenteredBarrierWaiter()
    {
        var plan = new KafkaFaultPlan();
        var barrier = plan.PauseNext(ProduceOrders);
        var entered = barrier.WaitUntilEnteredAsync().AsTask();

        var removed = plan.Clear(ProduceOrders);

        await Assert.That(removed).IsEqualTo(1);
        await Assert.That(barrier.IsReleased).IsTrue();
        _ = await Assert.ThrowsAsync<TaskCanceledException>(() => entered);
    }

    [Test]
    public async Task PauseNext_BlocksUntilReleasedAndPublishesObservation()
    {
        var plan = new KafkaFaultPlan();
        KafkaFaultObservation? observed = null;
        plan.FaultConsumed += observation => observed = observation;
        var barrier = plan.PauseNext(ProduceOrders);

        var operation = plan.ApplyAsync(ProduceOrders).AsTask();
        await barrier.WaitUntilEnteredAsync();

        await Assert.That(operation.IsCompleted).IsFalse();
        await Assert.That(observed).IsNotNull();
        await Assert.That(observed!.Value.Action).IsEqualTo(KafkaFaultAction.Pause);
        await Assert.That(barrier.Release()).IsTrue();
        await operation;
        await Assert.That(barrier.Release()).IsFalse();
    }

    [Test]
    public async Task PauseNext_CancellationStopsWaitWithoutTimingDelay()
    {
        var plan = new KafkaFaultPlan();
        var barrier = plan.PauseNext(ProduceOrders);
        using var cancellation = new CancellationTokenSource();

        var operation = plan.ApplyAsync(ProduceOrders, cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => operation);
        await Assert.That(barrier.Release()).IsTrue();
        await Assert.That(plan.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Clear_RemovesAllRulesAndReleasesPendingBarriers()
    {
        var plan = new KafkaFaultPlan();
        plan.Fail(ProduceOrders, new InvalidOperationException("failure"));
        var barrier = plan.PauseNext(new KafkaFaultScope(KafkaFaultOperation.Fetch));
        var entered = barrier.WaitUntilEnteredAsync().AsTask();

        var removed = plan.Clear();

        await Assert.That(removed).IsEqualTo(2);
        await Assert.That(barrier.IsReleased).IsTrue();
        _ = await Assert.ThrowsAsync<TaskCanceledException>(() => entered);
        await plan.ApplyAsync(ProduceOrders);
    }

    [Test]
    public async Task Configuration_RejectsInvalidArguments()
    {
        var plan = new KafkaFaultPlan();

        await Assert.That(() => new KafkaFaultScope(KafkaFaultOperation.Produce, partition: -1))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => plan.Fail(ProduceOrders, new InvalidOperationException(), occurrenceCount: 0))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => plan.Fail(ProduceOrders, null!))
            .Throws<ArgumentNullException>();
        await Assert.That(() => plan.Fail(default, new InvalidOperationException()))
            .Throws<ArgumentOutOfRangeException>();
    }

    private static async Task AssertFaultAsync(
        IKafkaFaultPlan plan,
        KafkaFaultScope context,
        Exception expected)
    {
        var actual = await Assert.ThrowsAsync<Exception>(() => plan.ApplyAsync(context).AsTask());
        await Assert.That(actual).IsSameReferenceAs(expected);
    }

    private static async Task ApplyIgnoringFaultAsync(IKafkaFaultPlan plan, KafkaFaultScope context)
    {
        try
        {
            await plan.ApplyAsync(context);
        }
        catch (InvalidOperationException)
        {
            return;
        }
    }
}
