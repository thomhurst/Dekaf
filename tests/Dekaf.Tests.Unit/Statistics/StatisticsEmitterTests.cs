using Dekaf.Statistics;

namespace Dekaf.Tests.Unit.Statistics;

public class StatisticsEmitterTests
{
    [Test]
    public async Task Emitter_CallsHandler_AtInterval()
    {
        var callCount = 0;
        var emitter = new StatisticsEmitter<int>(
            TimeSpan.FromMilliseconds(50),
            () => 42,
            _ => Interlocked.Increment(ref callCount));

        // Wait for a few emissions
        await Task.Delay(180);

        await emitter.DisposeAsync().ConfigureAwait(false);

        // Should have been called at least 2-3 times (50ms interval, 180ms wait)
        await Assert.That(callCount).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Emitter_PassesCollectedStatistics_ToHandler()
    {
        var receivedValues = new List<string>();
        var counter = 0;
        var emitter = new StatisticsEmitter<string>(
            TimeSpan.FromMilliseconds(30),
            () => $"stats-{Interlocked.Increment(ref counter)}",
            value => { lock (receivedValues) { receivedValues.Add(value); } });

        await Task.Delay(100);

        await emitter.DisposeAsync().ConfigureAwait(false);

        await Assert.That(receivedValues.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(receivedValues[0]).IsEqualTo("stats-1");
        await Assert.That(receivedValues[1]).IsEqualTo("stats-2");
    }

    [Test]
    public async Task Emitter_StopsEmitting_AfterDisposal()
    {
        var callCount = 0;
        var emitter = new StatisticsEmitter<int>(
            TimeSpan.FromMilliseconds(20),
            () => 1,
            _ => Interlocked.Increment(ref callCount));

        await Task.Delay(80);
        await emitter.DisposeAsync().ConfigureAwait(false);

        var countAfterDispose = callCount;

        // Wait more and verify no additional calls
        await Task.Delay(100);

        await Assert.That(callCount).IsEqualTo(countAfterDispose);
    }

    [Test]
    public async Task Emitter_SwallowsExceptions_InHandler()
    {
        var successCount = 0;
        var emitter = new StatisticsEmitter<int>(
            TimeSpan.FromMilliseconds(20),
            () => Interlocked.Increment(ref successCount),
            _ => throw new InvalidOperationException("Test exception"));

        // Should not throw and continue emitting
        await Task.Delay(100);

        await emitter.DisposeAsync().ConfigureAwait(false);

        // Collector should have been called multiple times despite handler throwing
        await Assert.That(successCount).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Emitter_SwallowsExceptions_InCollector()
    {
        var handlerCallCount = 0;
        var collectorCallCount = 0;
        var emitter = new StatisticsEmitter<int>(
            TimeSpan.FromMilliseconds(20),
            () =>
            {
                var count = Interlocked.Increment(ref collectorCallCount);
                if (count % 2 == 0)
                {
                    throw new InvalidOperationException("Test exception on even calls");
                }
                return count;
            },
            _ => Interlocked.Increment(ref handlerCallCount));

        await Task.Delay(100);

        await emitter.DisposeAsync().ConfigureAwait(false);

        // Collector should continue to be called despite exceptions
        await Assert.That(collectorCallCount).IsGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task Emitter_CanBeDisposed_Immediately()
    {
        var emitter = new StatisticsEmitter<int>(
            TimeSpan.FromSeconds(10),
            () => 1,
            _ => { });

        // Should complete quickly without waiting for the 10-second interval
        var disposeTask = emitter.DisposeAsync().AsTask();
        var completedInTime = await Task.WhenAny(disposeTask, Task.Delay(500)) == disposeTask;

        await Assert.That(completedInTime).IsTrue();
    }

    [Test]
    public async Task Emitter_CanBeDisposed_MultipleTimes()
    {
        var emitter = new StatisticsEmitter<int>(
            TimeSpan.FromMilliseconds(50),
            () => 1,
            _ => { });

        await emitter.DisposeAsync().ConfigureAwait(false);

        // Second dispose should not throw
        await emitter.DisposeAsync().ConfigureAwait(false);
    }
}
