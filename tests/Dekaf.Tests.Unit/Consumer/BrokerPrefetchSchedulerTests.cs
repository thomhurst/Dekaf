using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class BrokerPrefetchSchedulerTests
{
    [Test]
    public async Task WaitForAny_DrainedSignal_DoesNotCompleteWaitForReusedKey()
    {
        using var scheduler = new BrokerPrefetchScheduler();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var first = new TaskCompletionSource();
        scheduler.TryStart((1, 0), () => first.Task);
        first.SetResult(); // Inline notification leaves a signal with no waiter.
        await Assert.That(await scheduler.DrainCompletedAsync()).IsEqualTo(1);

        var next = new TaskCompletionSource();
        scheduler.TryStart((1, 0), () => next.Task);
        var wait = scheduler.WaitForAnyAsync(timeout.Token);
        await Assert.That(wait.IsCompleted).IsFalse();
        next.SetResult();
        await wait;
        await Assert.That(await scheduler.DrainCompletedAsync()).IsEqualTo(1);
    }

    [Test]
    public async Task WaitForAny_AfterDrainAll_DoesNotCountPreviousTasks()
    {
        using var scheduler = new BrokerPrefetchScheduler();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        scheduler.TryStart((1, 0), () => first.Task);
        var drain = scheduler.DrainAllSafelyAsync(static _ => { }, static _ => false);
        first.SetResult();
        await drain;

        var next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        scheduler.TryStart((1, 0), () => next.Task);
        var wait = scheduler.WaitForAnyAsync(timeout.Token);
        await Assert.That(wait.IsCompleted).IsFalse();
        next.SetResult();
        await wait;
        await Assert.That(await scheduler.DrainCompletedAsync()).IsEqualTo(1);
    }

    [Test]
    public async Task DrainCompleted_FaultLeavesRemainingCompletionAvailable()
    {
        using var scheduler = new BrokerPrefetchScheduler();
        scheduler.TryStart((1, 0), static () => Task.FromException(new InvalidOperationException()));
        scheduler.TryStart((2, 0), static () => Task.CompletedTask);
        await Assert.That(async () => await scheduler.DrainCompletedAsync()).Throws<InvalidOperationException>();
        await Assert.That(scheduler.WaitForAnyAsync(CancellationToken.None).IsCompletedSuccessfully).IsTrue();
        await Assert.That(await scheduler.DrainCompletedAsync()).IsEqualTo(1);
    }

    [Test]
    public async Task DrainCompleted_AllowsFastBrokerRestartWhileSlowBrokerRemainsInFlight()
    {
        var scheduler = new BrokerPrefetchScheduler();
        var slowBrokerCanComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var slowStarted = scheduler.TryStart((BrokerId: 1, ConnectionIndex: 0), async () =>
        {
            await slowBrokerCanComplete.Task.ConfigureAwait(false);
        });
        var fastStarted = scheduler.TryStart(
            (BrokerId: 2, ConnectionIndex: 0),
            static () => Task.CompletedTask);

        await Assert.That(slowStarted).IsTrue();
        await Assert.That(fastStarted).IsTrue();
        await Assert.That(scheduler.InFlightCount).IsEqualTo(2);

        var drained = await scheduler.DrainCompletedAsync().ConfigureAwait(false);
        await Assert.That(drained).IsEqualTo(1);

        await Assert.That(scheduler.InFlightCount).IsEqualTo(1);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var waitTask = scheduler.WaitForAnyAsync(cts.Token).AsTask();
        await Assert.That(waitTask.IsCompleted).IsFalse();

        slowBrokerCanComplete.SetResult();
        await waitTask.ConfigureAwait(false);

        await Assert.That(scheduler.TryStart((BrokerId: 2, ConnectionIndex: 0), static () => Task.CompletedTask)).IsTrue();
        await Assert.That(scheduler.TryStart((BrokerId: 1, ConnectionIndex: 0), static () => Task.CompletedTask)).IsFalse();

        await scheduler.DrainAllSafelyAsync(static _ => { }, static _ => false).ConfigureAwait(false);
    }

    [Test]
    public async Task WaitForAny_DoesNotObserveCompletedTaskException()
    {
        var scheduler = new BrokerPrefetchScheduler();
        var failure = new InvalidOperationException("broker failed");

        scheduler.TryStart((BrokerId: 1, ConnectionIndex: 0), () => Task.FromException(failure));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await scheduler.WaitForAnyAsync(cts.Token).ConfigureAwait(false);

        await Assert.That(async () => await scheduler.DrainCompletedAsync().ConfigureAwait(false))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task WaitForAny_SinglePendingTask_PropagatesWaitCancellation()
    {
        var scheduler = new BrokerPrefetchScheduler();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        scheduler.TryStart((BrokerId: 1, ConnectionIndex: 0), () => completion.Task);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.That(async () => await scheduler.WaitForAnyAsync(cts.Token).ConfigureAwait(false))
            .Throws<OperationCanceledException>();

        completion.SetResult();
        await scheduler.DrainAllSafelyAsync(static _ => { }, static _ => false).ConfigureAwait(false);
    }

    [Test]
    public async Task WaitForAny_SinglePendingTask_DefersFaultToDrain()
    {
        var scheduler = new BrokerPrefetchScheduler();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("broker failed");
        scheduler.TryStart((BrokerId: 1, ConnectionIndex: 0), () => completion.Task);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var waitTask = scheduler.WaitForAnyAsync(cts.Token).AsTask();
        await Assert.That(waitTask.IsCompleted).IsFalse();

        completion.SetException(failure);
        await waitTask.ConfigureAwait(false);

        var exception = await Assert.That(async () => await scheduler.DrainCompletedAsync().ConfigureAwait(false))
            .Throws<InvalidOperationException>();
        await Assert.That(exception).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task WaitForAny_MultiplePendingTasks_WakesWhenAnyOneCompletes()
    {
        var scheduler = new BrokerPrefetchScheduler();
        var completions = new TaskCompletionSource[4];
        for (var broker = 0; broker < completions.Length; broker++)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            completions[broker] = completion;
            scheduler.TryStart((BrokerId: broker, ConnectionIndex: 0), () => completion.Task);
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var waitTask = scheduler.WaitForAnyAsync(cts.Token).AsTask();
        await Assert.That(waitTask.IsCompleted).IsFalse();

        completions[2].SetResult();
        await waitTask.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(await scheduler.DrainCompletedAsync()).IsEqualTo(1);
        await Assert.That(scheduler.InFlightCount).IsEqualTo(3);
        await Assert.That(scheduler.TryStart((BrokerId: 2, ConnectionIndex: 0), () => completions[2].Task)).IsTrue();

        foreach (var completion in completions)
            completion.TrySetResult();
        await scheduler.DrainAllSafelyAsync(static _ => { }, static _ => false);
    }

    [Test]
    public async Task WaitForAny_RepeatedWaits_WakeForEachSuccessiveCompletion()
    {
        var scheduler = new BrokerPrefetchScheduler();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        for (var round = 0; round < 3; round++)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await Assert.That(scheduler.TryStart((BrokerId: 1, ConnectionIndex: 0), () => completion.Task)).IsTrue();

            var waitTask = scheduler.WaitForAnyAsync(cts.Token).AsTask();
            await Assert.That(waitTask.IsCompleted).IsFalse();

            completion.SetResult();
            await waitTask.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(await scheduler.DrainCompletedAsync()).IsEqualTo(1);
        }

        await Assert.That(scheduler.HasInFlight).IsFalse();
    }

    [Test]
    public async Task WaitForAny_CancelledDuringWait_Throws()
    {
        var scheduler = new BrokerPrefetchScheduler();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        scheduler.TryStart((BrokerId: 1, ConnectionIndex: 0), () => completion.Task);

        using var cts = new CancellationTokenSource();
        var waitTask = scheduler.WaitForAnyAsync(cts.Token).AsTask();
        await Assert.That(waitTask.IsCompleted).IsFalse();

        cts.Cancel();

        await Assert.That(async () => await waitTask.WaitAsync(TimeSpan.FromSeconds(10)))
            .Throws<OperationCanceledException>();

        completion.SetResult();
        await scheduler.DrainAllSafelyAsync(static _ => { }, static _ => false);
    }

    [Test]
    public async Task DrainAllSafely_ReturnsFirstMatchingFailureAndObservesAll()
    {
        var scheduler = new BrokerPrefetchScheduler();
        var firstFailure = new InvalidOperationException("first");
        var secondFailure = new InvalidOperationException("second");
        var loggedFailures = new List<Exception>();

        scheduler.TryStart(
            (BrokerId: 1, ConnectionIndex: 0),
            () => Task.FromException(firstFailure));
        scheduler.TryStart(
            (BrokerId: 2, ConnectionIndex: 0),
            () => Task.FromException(secondFailure));

        var drainedFailure = await scheduler.DrainAllSafelyAsync(
            loggedFailures.Add,
            exception => ReferenceEquals(exception, secondFailure)).ConfigureAwait(false);

        await Assert.That(drainedFailure).IsSameReferenceAs(secondFailure);
        await Assert.That(loggedFailures).Count().IsEqualTo(2);
        await Assert.That(loggedFailures[0]).IsSameReferenceAs(firstFailure);
        await Assert.That(loggedFailures[1]).IsSameReferenceAs(secondFailure);
        await Assert.That(scheduler.HasInFlight).IsFalse();
    }
}
