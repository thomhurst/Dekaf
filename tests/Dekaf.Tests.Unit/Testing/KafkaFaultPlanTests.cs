using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class KafkaFaultPlanTests
{
    private static readonly KafkaFaultScope ProduceOrders = new(
        KafkaFaultOperation.Produce,
        topic: "orders");

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

        await AssertFaultAsync(
            plan,
            new KafkaFaultScope(KafkaFaultOperation.Fetch, "orders", 4, "billing"),
            failure);
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

        var removed = plan.Clear();

        await Assert.That(removed).IsEqualTo(2);
        await Assert.That(barrier.IsReleased).IsTrue();
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
