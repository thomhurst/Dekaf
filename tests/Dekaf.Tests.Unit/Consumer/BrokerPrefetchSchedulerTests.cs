using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class BrokerPrefetchSchedulerTests
{
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
        await Assert.That(scheduler.TryStart((BrokerId: 2, ConnectionIndex: 0), static () => Task.CompletedTask)).IsTrue();
        await Assert.That(scheduler.TryStart((BrokerId: 1, ConnectionIndex: 0), static () => Task.CompletedTask)).IsFalse();

        slowBrokerCanComplete.SetResult();
        await scheduler.DrainAllSafelyAsync(static _ => { }).ConfigureAwait(false);
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
}
