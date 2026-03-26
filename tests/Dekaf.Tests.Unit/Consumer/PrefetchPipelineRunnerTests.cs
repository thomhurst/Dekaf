using System.Threading.Channels;
using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests for the pipelined prefetch state machine in <see cref="PrefetchPipelineRunner"/>.
/// Validates ordering guarantees, error counting, drain behavior on assignment loss,
/// drain behavior on memory limit, and shutdown exception observation.
/// </summary>
public class PrefetchPipelineRunnerTests
{
    #region Scenario 1: Eager fetch is awaited before the next synchronous fetch (ordering guarantee)

    [Test]
    public async Task RunAsync_EagerFetchIsAwaitedBeforeNextSynchronousFetch()
    {
        // Tracks the order of fetch start/completion to verify pipelining invariant:
        // the eager fetch must complete before the next synchronous fetch begins.
        var fetchLog = new List<string>();
        var fetchCount = 0;

        var runner = CreateRunner(
            prefetchRecords: async ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                fetchLog.Add($"start-{id}");
                await Task.Yield(); // Simulate async work
                fetchLog.Add($"end-{id}");

                // After 3 fetches, cancel to exit the loop
                if (id >= 3)
                    ct.ThrowIfCancellationRequested();
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue, // No memory limit
            prefetchedBytes: 0);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // The runner will:
        // Iteration 1: synchronous fetch (start-1, end-1), then eagerly start fetch 2
        // Iteration 2: await eager fetch 2 (start-2 already started, end-2), then synchronous fetch (start-3, end-3)
        // Iteration 3: fetch 3 triggers cancellation and loop exits
        await runner.RunAsync(cts.Token);

        // Verify that no two fetches overlap: each start-N must be followed by end-N
        // before the next start-(N+1)
        for (var i = 0; i < fetchLog.Count - 1; i += 2)
        {
            var startEntry = fetchLog[i];
            var endEntry = fetchLog[i + 1];

            // Extract the IDs — they must match (start-X followed by end-X)
            var startId = startEntry.Split('-')[1];
            var endId = endEntry.Split('-')[1];

            await Assert.That(startEntry).StartsWith("start-");
            await Assert.That(endEntry).StartsWith("end-");
            await Assert.That(startId).IsEqualTo(endId);
        }
    }

    #endregion

    #region Scenario 2: Exception from eager fetch is observed and counted exactly once

    [Test]
    public async Task RunAsync_EagerFetchException_CountedExactlyOnce()
    {
        // The eager fetch (in-flight) will fail. The next iteration's catch block drains it.
        // The error from the in-flight fetch should be counted once, and the error from
        // the main flow should also be counted once = total 2 per iteration with both failing.
        var fetchCount = 0;
        var errorObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id == 1)
                    return ValueTask.CompletedTask; // Synchronous fetch succeeds
                if (id == 2)
                    return ValueTask.FromException(new InvalidOperationException("eager fetch failed")); // Eager fetch fails
                // Should not be called again
                ct.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            },
            ensureAssignment: ct =>
            {
                // After the eager fetch has been set up, throw on the next iteration
                // to trigger the catch block which drains the in-flight task.
                if (fetchCount >= 2)
                    throw new InvalidOperationException("assignment check failed");
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            logError: _ =>
            {
                // The error is logged once per catch-block entry. Signal after first observation.
                errorObserved.TrySetResult();
            });

        // Run the loop in background; we'll cancel after verifying the error count
        var runTask = runner.RunAsync(cts.Token);

        // Wait for the error to be observed
        await errorObserved.Task;

        // Cancel to stop the loop (the delay in the catch block will throw OCE)
        await cts.CancelAsync();

        // RunAsync may throw TaskCanceledException from the Task.Delay in the catch block
        try { await runTask; }
        catch (OperationCanceledException) { /* expected */ }

        // The catch block drains the in-flight fetch (eager fetch error: +1) and counts the
        // main exception (+1) = 2 total. This verifies no triple-counting bug.
        await Assert.That(runner.ConsecutiveErrors).IsEqualTo(2);
    }

    [Test]
    public async Task RunAsync_EagerFetchException_OnlyInFlightFails_CountedOnce()
    {
        // Only the in-flight (eager) fetch fails; the main loop exception comes from
        // somewhere else (e.g. EnsureAssignmentAsync). The in-flight exception should
        // add exactly 1 to consecutiveErrors.
        var fetchCount = 0;
        var triggerAssignmentError = false;
        var errorObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id == 1)
                    return ValueTask.CompletedTask; // Synchronous fetch succeeds
                if (id == 2)
                {
                    // Eager fetch fails
                    triggerAssignmentError = true;
                    return ValueTask.FromException(new InvalidOperationException("eager fetch failed"));
                }
                // Should not reach here in this test
                ct.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            },
            ensureAssignment: ct =>
            {
                if (triggerAssignmentError)
                {
                    triggerAssignmentError = false;
                    throw new InvalidOperationException("assignment error");
                }
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            logError: _ => errorObserved.TrySetResult());

        var runTask = runner.RunAsync(cts.Token);

        // Wait for the error to be observed
        await errorObserved.Task;

        // Cancel to stop the loop
        await cts.CancelAsync();

        try { await runTask; }
        catch (OperationCanceledException) { /* expected */ }

        // in-flight error (+1) + main exception (+1) = 2
        // Not 3 (which would indicate double-counting the in-flight error)
        await Assert.That(runner.ConsecutiveErrors).IsEqualTo(2);
    }

    #endregion

    #region Scenario 3: inFlightPrefetch drained and nulled when no assignment

    [Test]
    public async Task RunAsync_NoAssignment_DrainsInFlightPrefetch()
    {
        var fetchCount = 0;
        var assignmentCount = 1; // Start with assignment
        var eagerFetchAwaited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id == 1)
                {
                    // Synchronous fetch succeeds
                    return ValueTask.CompletedTask;
                }
                if (id == 2)
                {
                    // Eager fetch — will be awaited when assignment drops to 0
                    // Remove assignment so the next iteration drains the in-flight task
                    assignmentCount = 0;
                    eagerFetchAwaited.SetResult();
                    return ValueTask.CompletedTask;
                }
                // No more fetches expected
                ct.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            },
            getAssignmentCount: () => assignmentCount,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await runner.RunAsync(cts.Token);

        // The eager fetch was awaited (its TCS completed), proving the drain happened
        await Assert.That(eagerFetchAwaited.Task.IsCompleted).IsTrue();

        // InFlightPrefetch should be null after drain
        await Assert.That((object?)runner.InFlightPrefetch).IsNull();
    }

    #endregion

    #region Scenario 4: inFlightPrefetch drained and nulled when memory limit hit

    [Test]
    public async Task RunAsync_MemoryLimitHit_DrainsInFlightPrefetch()
    {
        var fetchCount = 0;
        long prefetchedBytes = 0;

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id == 1)
                {
                    // Synchronous fetch succeeds
                    return ValueTask.CompletedTask;
                }
                if (id == 2)
                {
                    // Eager fetch — simulate it adding data that exceeds memory limit
                    Interlocked.Exchange(ref prefetchedBytes, 2048);
                    return ValueTask.CompletedTask;
                }
                ct.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: 1024, // 1KB limit
            getPrefetchedBytes: () => Interlocked.Read(ref prefetchedBytes),
            waitForMemoryAvailable: ct =>
            {
                // Cancel to exit the loop after we verify the drain happened
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await runner.RunAsync(cts.Token);

        // InFlightPrefetch should be null after drain at memory limit
        await Assert.That((object?)runner.InFlightPrefetch).IsNull();
    }

    [Test]
    public async Task RunAsync_MemoryLimitAfterInFlightComplete_SkipsEagerFetch()
    {
        // After the synchronous fetch succeeds and memory is below limit, an eager fetch starts.
        // After the eager fetch completes and memory now exceeds limit, the runner should
        // loop back to the memory check and NOT start another eager fetch.
        var fetchCount = 0;
        long prefetchedBytes = 0;

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id <= 2)
                {
                    // Both fetches succeed but the second one fills memory
                    if (id == 2)
                        Interlocked.Exchange(ref prefetchedBytes, 2048);
                    return ValueTask.CompletedTask;
                }
                ct.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: 1024,
            getPrefetchedBytes: () => Interlocked.Read(ref prefetchedBytes),
            waitForMemoryAvailable: ct =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await runner.RunAsync(cts.Token);

        // Only 2 fetches: the synchronous one and the eager one. No third fetch because
        // memory limit was hit after the eager fetch completed.
        await Assert.That(fetchCount).IsEqualTo(2);
    }

    #endregion

    #region Scenario 5: Shutdown (finally block) drains and observes exceptions

    [Test]
    public async Task RunAsync_Shutdown_DrainsInFlightPrefetchWithoutLeaking()
    {
        var fetchCount = 0;
        var eagerFetchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var eagerFetchCanComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var runner = CreateRunner(
            prefetchRecords: async ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id == 1)
                    return; // Synchronous fetch succeeds

                if (id == 2)
                {
                    // Eager fetch — signal that it started, then wait
                    eagerFetchStarted.SetResult();
                    await eagerFetchCanComplete.Task;
                    return;
                }
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0);

        using var cts = new CancellationTokenSource();

        var runTask = runner.RunAsync(cts.Token);

        // Wait for the eager fetch to start
        await eagerFetchStarted.Task;

        // Cancel to trigger shutdown
        await cts.CancelAsync();

        // Let the eager fetch complete
        eagerFetchCanComplete.SetResult();

        // The runner should complete without throwing (finally block drains the in-flight task)
        await runTask;

        // InFlightPrefetch should be null (drained in finally)
        await Assert.That((object?)runner.InFlightPrefetch).IsNull();
    }

    [Test]
    public async Task RunAsync_Shutdown_ObservesExceptionFromInFlightPrefetch()
    {
        // Tests that the finally block drains the in-flight fetch and logs its exception.
        // Strategy: the eager fetch throws a non-OCE exception. The ensureAssignment on the
        // next iteration cancels the CTS, causing the while-loop to break. But the in-flight
        // exception is drained either in the catch block or the finally block.
        var fetchCount = 0;
        var iterationCount = 0;
        var loggedErrors = new List<Exception>();
        CancellationTokenSource? testCts = null;

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id == 1)
                    return ValueTask.CompletedTask; // Synchronous fetch succeeds
                if (id == 2)
                    return ValueTask.FromException(new InvalidOperationException("in-flight fetch error"));
                ct.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            },
            ensureAssignment: ct =>
            {
                var iter = Interlocked.Increment(ref iterationCount);
                if (iter >= 2)
                {
                    // Cancel on the second iteration to trigger shutdown.
                    // The in-flight fetch from iteration 1 is still pending drain.
                    testCts!.Cancel();
                    ct.ThrowIfCancellationRequested();
                }
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            logError: ex => loggedErrors.Add(ex));

        testCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // The cancellation in ensureAssignment triggers OCE which goes to the
        // catch (OperationCanceledException) when (ct.IsCancellationRequested) => break.
        // But the in-flight fetch is still pending. The finally block drains it.
        //
        // However: the OCE from ensureAssignment is thrown inside the try block, and
        // the catch (OCE) when (ct.IsCancelled) catches it and breaks. The finally block
        // then drains the in-flight prefetch which has the InvalidOperationException.
        await runner.RunAsync(testCts.Token);

        // The error from the in-flight fetch was observed (logged or counted)
        // The in-flight error is observed in the finally block's catch (Exception ex) => logError
        await Assert.That(loggedErrors).Count().IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task RunAsync_Shutdown_CompletesChannel()
    {
        var channel = Channel.CreateUnbounded<PendingFetchData>();

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                // Cancel immediately after first fetch to trigger shutdown
                ct.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            channelWriter: channel.Writer);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await runner.RunAsync(cts.Token);

        // Channel should be completed after the runner exits
        await Assert.That(channel.Reader.Completion.IsCompleted).IsTrue();
    }

    #endregion

    #region Consecutive error counting

    [Test]
    public async Task RunAsync_SuccessfulFetch_ResetsConsecutiveErrors()
    {
        var fetchCount = 0;
        var shouldFail = true;

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id <= 2 && shouldFail)
                {
                    // First two fetches fail
                    return ValueTask.FromException(new InvalidOperationException($"fetch {id} failed"));
                }
                if (id == 3)
                {
                    // Third fetch succeeds
                    shouldFail = false;
                    return ValueTask.CompletedTask;
                }
                // After success, cancel to exit
                ct.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            },
            ensureAssignment: ct =>
            {
                if (fetchCount >= 3 && !shouldFail)
                    return ValueTask.CompletedTask;
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await runner.RunAsync(cts.Token);

        // After the successful fetch, consecutiveErrors should be reset to 0
        await Assert.That(runner.ConsecutiveErrors).IsEqualTo(0);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a <see cref="PrefetchPipelineRunner"/> with the specified behavior overrides.
    /// Defaults simulate a working consumer with assignment and no memory pressure.
    /// </summary>
    private static PrefetchPipelineRunner CreateRunner(
        Func<CancellationToken, ValueTask>? prefetchRecords = null,
        Func<CancellationToken, ValueTask>? ensureAssignment = null,
        int assignmentCount = 1,
        Func<int>? getAssignmentCount = null,
        long maxBytes = long.MaxValue,
        long prefetchedBytes = 0,
        Func<long>? getPrefetchedBytes = null,
        Func<CancellationToken, Task>? waitForMemoryAvailable = null,
        Action<Exception>? logError = null,
        Action<long, long>? logMemoryLimitPaused = null,
        ChannelWriter<PendingFetchData>? channelWriter = null)
    {
        return new PrefetchPipelineRunner(
            ensureAssignment: ensureAssignment ?? (ct => ValueTask.CompletedTask),
            getAssignmentCount: getAssignmentCount ?? (() => assignmentCount),
            getMaxBytes: () => maxBytes,
            getPrefetchedBytes: getPrefetchedBytes ?? (() => prefetchedBytes),
            prefetchRecords: prefetchRecords ?? (ct => ValueTask.CompletedTask),
            waitForMemoryAvailable: waitForMemoryAvailable ?? (ct =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }),
            logError: logError ?? (_ => { }),
            logMemoryLimitPaused: logMemoryLimitPaused ?? ((_, _) => { }),
            channelWriter: channelWriter);
    }

    #endregion
}
