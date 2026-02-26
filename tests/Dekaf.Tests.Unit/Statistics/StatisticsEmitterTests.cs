using Dekaf.Statistics;

namespace Dekaf.Tests.Unit.Statistics;

public class StatisticsEmitterTests
{
    [Test]
    public async Task Emitter_CallsHandler_AtInterval()
    {
        var callCount = 0;
        var semaphore = new SemaphoreSlim(0);
        var emitter = new StatisticsEmitter<int>(
            TimeSpan.FromMilliseconds(100),
            () => 42,
            _ =>
            {
                Interlocked.Increment(ref callCount);
                semaphore.Release();
            });

        // Wait for at least 2 emissions with generous timeout for CI
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        for (var i = 0; i < 2; i++)
        {
            await semaphore.WaitAsync(cts.Token);
        }

        await emitter.DisposeAsync().ConfigureAwait(false);

        // Should have been called at least 2 times
        await Assert.That(callCount).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Emitter_PassesCollectedStatistics_ToHandler()
    {
        var receivedValues = new List<string>();
        var counter = 0;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var emitter = new StatisticsEmitter<string>(
            TimeSpan.FromMilliseconds(100),
            () => $"stats-{Interlocked.Increment(ref counter)}",
            value =>
            {
                lock (receivedValues)
                {
                    receivedValues.Add(value);
                    if (receivedValues.Count >= 2)
                    {
                        tcs.TrySetResult(true);
                    }
                }
            });

        // Wait for at least 2 emissions with generous timeout for CI
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10))) == tcs.Task;
        await Assert.That(completed).IsTrue();

        await emitter.DisposeAsync().ConfigureAwait(false);

        await Assert.That(receivedValues.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(receivedValues[0]).IsEqualTo("stats-1");
        await Assert.That(receivedValues[1]).IsEqualTo("stats-2");
    }

    [Test]
    public async Task Emitter_StopsEmitting_AfterDisposal()
    {
        var callCount = 0;
        var firstCallTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var emitter = new StatisticsEmitter<int>(
            TimeSpan.FromMilliseconds(50),
            () => 1,
            _ =>
            {
                Interlocked.Increment(ref callCount);
                firstCallTcs.TrySetResult(true);
            });

        // Wait for at least one emission to confirm emitter is working
        var gotFirstCall = await Task.WhenAny(firstCallTcs.Task, Task.Delay(TimeSpan.FromSeconds(5))) == firstCallTcs.Task;
        await Assert.That(gotFirstCall).IsTrue();

        await emitter.DisposeAsync().ConfigureAwait(false);

        var countAfterDispose = callCount;

        // Wait more and verify no additional calls
        await Task.Delay(500);

        await Assert.That(callCount).IsEqualTo(countAfterDispose);
    }

    [Test]
    public async Task Emitter_SwallowsExceptions_InHandler()
    {
        var successCount = 0;
        var semaphore = new SemaphoreSlim(0);
        var emitter = new StatisticsEmitter<int>(
            TimeSpan.FromMilliseconds(100),
            () =>
            {
                Interlocked.Increment(ref successCount);
                semaphore.Release();
                return successCount;
            },
            _ => throw new InvalidOperationException("Test exception"));

        // Wait for at least 2 collector calls with generous timeout for CI
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        for (var i = 0; i < 2; i++)
        {
            await semaphore.WaitAsync(cts.Token);
        }

        await emitter.DisposeAsync().ConfigureAwait(false);

        // Collector should have been called multiple times despite handler throwing
        await Assert.That(successCount).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Emitter_SwallowsExceptions_InCollector()
    {
        var handlerCallCount = 0;
        var collectorCallCount = 0;
        var semaphore = new SemaphoreSlim(0);
        var emitter = new StatisticsEmitter<int>(
            TimeSpan.FromMilliseconds(100),
            () =>
            {
                var count = Interlocked.Increment(ref collectorCallCount);
                semaphore.Release();
                if (count % 2 == 0)
                {
                    throw new InvalidOperationException("Test exception on even calls");
                }
                return count;
            },
            _ => Interlocked.Increment(ref handlerCallCount));

        // Wait for at least 3 collector calls with generous timeout for CI
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        for (var i = 0; i < 3; i++)
        {
            await semaphore.WaitAsync(cts.Token);
        }

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
        var completedInTime = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(5))) == disposeTask;

        await Assert.That(completedInTime).IsTrue();
    }

    [Test]
    public async Task Emitter_CanBeDisposed_MultipleTimes()
    {
        var emitter = new StatisticsEmitter<int>(
            TimeSpan.FromMilliseconds(100),
            () => 1,
            _ => { });

        await emitter.DisposeAsync().ConfigureAwait(false);

        // Second dispose should not throw
        await emitter.DisposeAsync().ConfigureAwait(false);
    }
}
