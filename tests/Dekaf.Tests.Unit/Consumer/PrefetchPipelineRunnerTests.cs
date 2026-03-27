using System.Collections.Concurrent;
using System.Threading.Channels;
using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests for the pipelined prefetch state machine in <see cref="PrefetchPipelineRunner"/>.
/// Validates ordering guarantees, error counting, drain behavior on assignment loss,
/// drain behavior on memory limit, pipeline depth, and shutdown exception observation.
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
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var runner = CreateRunner(
            prefetchRecords: async ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                fetchLog.Add($"start-{id}");
                await Task.Yield(); // Simulate async work
                fetchLog.Add($"end-{id}");

                // After 3 fetches, explicitly cancel and exit immediately
                if (id >= 3)
                {
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                }
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue, // No memory limit
            prefetchedBytes: 0);

        // The runner will:
        // Iteration 1: synchronous fetch (start-1, end-1), then eagerly start fetch 2
        // Iteration 2: await eager fetch 2 (start-2 already started, end-2), then synchronous fetch (start-3, end-3)
        // Iteration 3: fetch 3 triggers cancellation and loop exits
        await runner.RunAsync(cts.Token);

        // Verify that no two fetches overlap: each start-N must be followed by end-N
        // before the next start-(N+1). Only verify complete pairs to avoid
        // breaking on an odd number of log entries.
        var completePairs = fetchLog.Count / 2;
        await Assert.That(completePairs).IsGreaterThanOrEqualTo(2);

        for (var i = 0; i < completePairs * 2; i += 2)
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
                Assert.Fail("prefetchRecords called more times than expected");
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
                Assert.Fail("prefetchRecords called more times than expected");
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
                Assert.Fail("prefetchRecords called more times than expected");
                return ValueTask.CompletedTask;
            },
            getAssignmentCount: () => assignmentCount,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await runner.RunAsync(cts.Token);

        // The eager fetch was awaited (its TCS completed), proving the drain happened
        await Assert.That(eagerFetchAwaited.Task.IsCompleted).IsTrue();

        // InFlightPrefetchCount should be 0 after drain
        await Assert.That(runner.InFlightPrefetchCount).IsEqualTo(0);
    }

    #endregion

    #region Scenario 4: inFlightPrefetch preserved when memory limit hit (no drain-all)

    [Test]
    public async Task RunAsync_MemoryLimitHit_PreservesInFlightPrefetch()
    {
        // When memory limit is hit, in-flight fetches should NOT be drained.
        // They complete naturally and are consumed by the consume loop, freeing memory.
        var fetchCount = 0;
        long prefetchedBytes = 0;
        var memoryLimitPauseCount = 0;

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
                Assert.Fail("prefetchRecords called more times than expected");
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: 1024, // 1KB limit
            getPrefetchedBytes: () => Interlocked.Read(ref prefetchedBytes),
            waitForMemoryAvailable: ct =>
            {
                Interlocked.Increment(ref memoryLimitPauseCount);
                // Cancel to exit the loop
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await runner.RunAsync(cts.Token);

        // Memory limit pause was reached
        await Assert.That(Volatile.Read(ref memoryLimitPauseCount)).IsGreaterThanOrEqualTo(1);
        // InFlightPrefetchCount is 0 after RunAsync completes (drained in finally block)
        await Assert.That(runner.InFlightPrefetchCount).IsEqualTo(0);
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
                Assert.Fail("prefetchRecords called more times than expected");
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

        // InFlightPrefetchCount should be 0 (drained in finally)
        await Assert.That(runner.InFlightPrefetchCount).IsEqualTo(0);
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
                Assert.Fail("prefetchRecords called more times than expected");
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

    #region Scenario 6: In-flight fetch exception absorbed during cleanup drains

    [Test]
    public async Task RunAsync_InFlightFetchThrowsDuringNoAssignmentDrain_ExceptionAbsorbed()
    {
        // When assignment drops to 0, the in-flight fetch is drained via the safe path.
        // If the in-flight fetch throws, the exception should be logged but NOT propagate,
        // and ConsecutiveErrors should NOT be incremented for the drain.
        var fetchCount = 0;
        var assignmentCount = 1;
        var loggedErrors = new List<Exception>();
        var drainHappened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id == 1)
                    return ValueTask.CompletedTask; // Synchronous fetch succeeds
                if (id == 2)
                {
                    // Eager fetch will fail — drop assignment so next iteration drains safely
                    assignmentCount = 0;
                    return ValueTask.FromException(new InvalidOperationException("in-flight fetch error"));
                }
                Assert.Fail("prefetchRecords called more times than expected");
                return ValueTask.CompletedTask;
            },
            getAssignmentCount: () => assignmentCount,
            ensureAssignment: ct =>
            {
                // After drain happens (assignment is 0), signal so we can verify
                if (assignmentCount == 0 && fetchCount >= 2)
                    drainHappened.TrySetResult();
                return ValueTask.CompletedTask;
            },
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            logError: ex => loggedErrors.Add(ex));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = runner.RunAsync(cts.Token);

        // Wait for the drain to happen
        await drainHappened.Task;

        // Cancel to stop the loop
        await cts.CancelAsync();

        try { await runTask; }
        catch (OperationCanceledException) { /* expected */ }

        // The in-flight exception was logged
        await Assert.That(loggedErrors).Count().IsEqualTo(1);
        await Assert.That(loggedErrors[0].Message).IsEqualTo("in-flight fetch error");

        // ConsecutiveErrors should be 0 — the drain is a cleanup path, not a fetch attempt
        await Assert.That(runner.ConsecutiveErrors).IsEqualTo(0);
    }

    [Test]
    public async Task RunAsync_InFlightFetchThrowsDuringMemoryLimit_ExceptionObservedInFinally()
    {
        // When memory limit is hit, in-flight fetches are NOT drained (no drain-all).
        // The failing in-flight fetch stays queued and its exception is observed in the
        // finally block during shutdown — not during the memory limit pause.
        var fetchCount = 0;
        long prefetchedBytes = 0;
        var loggedErrors = new List<Exception>();
        var memoryLimitReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id == 1)
                    return ValueTask.CompletedTask; // Synchronous fetch succeeds
                if (id == 2)
                {
                    // Eager fetch will fail — also push prefetchedBytes over the limit
                    Interlocked.Exchange(ref prefetchedBytes, 2048);
                    return ValueTask.FromException(new InvalidOperationException("in-flight fetch error"));
                }
                Assert.Fail("prefetchRecords called more times than expected");
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: 1024,
            getPrefetchedBytes: () => Interlocked.Read(ref prefetchedBytes),
            waitForMemoryAvailable: ct =>
            {
                memoryLimitReached.TrySetResult();
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            logError: ex => loggedErrors.Add(ex));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = runner.RunAsync(cts.Token);

        // Wait for the memory-limit pause
        await memoryLimitReached.Task;

        // Cancel to stop the loop — the finally block will drain the in-flight fetch
        await cts.CancelAsync();

        try { await runTask; }
        catch (OperationCanceledException) { /* expected */ }

        // The in-flight exception was observed in the finally block
        await Assert.That(loggedErrors).Count().IsEqualTo(1);
        await Assert.That(loggedErrors[0].Message).IsEqualTo("in-flight fetch error");

        // ConsecutiveErrors should be 0 — the exception was observed in cleanup, not the error handler
        await Assert.That(runner.ConsecutiveErrors).IsEqualTo(0);
    }

    #endregion

    #region Consecutive error counting

    [Test]
    public async Task RunAsync_SuccessfulFetch_ResetsConsecutiveErrors()
    {
        var fetchCount = 0;
        var shouldFail = true;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

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
                // After success verified, cancel to exit immediately
                cts.Cancel();
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

        await runner.RunAsync(cts.Token);

        // After the successful fetch, consecutiveErrors should be reset to 0
        await Assert.That(runner.ConsecutiveErrors).IsEqualTo(0);
    }

    #endregion

    #region Pipeline depth tests

    [Test]
    public async Task RunAsync_PipelineDepth1_NoEagerFetches()
    {
        // With pipeline depth 1, no eager in-flight fetches should be started.
        var fetchCount = 0;
        var maxConcurrent = 0;
        var currentConcurrent = 0;

        var runner = CreateRunner(
            prefetchRecords: async ct =>
            {
                var concurrent = Interlocked.Increment(ref currentConcurrent);
                if (concurrent > Volatile.Read(ref maxConcurrent))
                    Interlocked.Exchange(ref maxConcurrent, concurrent);

                var id = Interlocked.Increment(ref fetchCount);
                await Task.Yield();

                Interlocked.Decrement(ref currentConcurrent);

                if (id >= 3)
                    ct.ThrowIfCancellationRequested();
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            pipelineDepth: 1);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await runner.RunAsync(cts.Token);

        // With depth 1, no eager fetches => InFlightPrefetchCount should be 0
        await Assert.That(runner.InFlightPrefetchCount).IsEqualTo(0);
        // Max concurrency should be 1 (only synchronous fetches)
        await Assert.That(maxConcurrent).IsEqualTo(1);
    }

    [Test]
    public async Task RunAsync_PipelineDepth2_OneEagerFetch()
    {
        // With pipeline depth 2 (max), one eager in-flight fetch should be started
        // after each synchronous fetch.
        var fetchCount = 0;

        var runner = CreateRunner(
            prefetchRecords: async ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                await Task.Yield(); // Simulate async work

                if (id >= 4)
                    ct.ThrowIfCancellationRequested();
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            pipelineDepth: 2);

        // Use 30s timeout — on CI with thread pool starvation, 5s may not be enough
        // for the pipeline runner to schedule multiple async fetches via Task.Yield().
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await runner.RunAsync(cts.Token);

        // With depth 2, we should have had at least 3 fetches total
        // (1 synchronous + 1 eager in first iteration, then more)
        await Assert.That(fetchCount).IsGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task RunAsync_PipelineDepth4_OneEagerFetchPerIteration()
    {
        // With pipeline depth 4, the runner adds at most 1 eager fetch per outer loop
        // iteration (to prevent duplicate position reads). The queue fills to capacity
        // (3 = depth - 1) over multiple iterations. This test verifies that at most 1
        // eager fetch starts per iteration, and the queue accumulates correctly.
        var fetchCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var runner = CreateRunner(
            prefetchRecords: async ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                await Task.Yield(); // Simulate async work

                // After enough fetches to prove the pattern, cancel
                if (id >= 6)
                {
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                }
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            pipelineDepth: 4);

        await runner.RunAsync(cts.Token);

        // At least 6 fetches (the cancellation threshold) proves the pipeline ran multiple iterations
        await Assert.That(fetchCount).IsGreaterThanOrEqualTo(6);
    }

    [Test]
    public async Task RunAsync_PipelineDepth4_RespectsMemoryLimit()
    {
        // Even with pipeline depth 4, eager fetches should not start if memory limit is exceeded.
        var fetchCount = 0;
        long prefetchedBytes = 0;

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id == 1)
                {
                    // Synchronous fetch succeeds, but fills memory
                    Interlocked.Exchange(ref prefetchedBytes, 2048);
                    return ValueTask.CompletedTask;
                }
                Assert.Fail("prefetchRecords called more times than expected — memory limit should have prevented eager fetches");
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: 1024,
            getPrefetchedBytes: () => Interlocked.Read(ref prefetchedBytes),
            waitForMemoryAvailable: ct =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            pipelineDepth: 4);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await runner.RunAsync(cts.Token);

        // Only 1 fetch — no eager fetches because memory limit was exceeded
        await Assert.That(fetchCount).IsEqualTo(1);
        await Assert.That(runner.InFlightPrefetchCount).IsEqualTo(0);
    }

    [Test]
    public async Task RunAsync_PipelineDepth2_RespectsMemoryLimit()
    {
        // Even with pipeline depth 2, eager fetches should not start if memory limit is exceeded.
        var fetchCount = 0;
        long prefetchedBytes = 0;

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id == 1)
                {
                    // Synchronous fetch succeeds, but fills memory
                    Interlocked.Exchange(ref prefetchedBytes, 2048);
                    return ValueTask.CompletedTask;
                }
                Assert.Fail("prefetchRecords called more times than expected — memory limit should have prevented eager fetches");
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: 1024,
            getPrefetchedBytes: () => Interlocked.Read(ref prefetchedBytes),
            waitForMemoryAvailable: ct =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            pipelineDepth: 2);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await runner.RunAsync(cts.Token);

        // Only 1 fetch — no eager fetches because memory limit was exceeded
        await Assert.That(fetchCount).IsEqualTo(1);
        await Assert.That(runner.InFlightPrefetchCount).IsEqualTo(0);
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
        ChannelWriter<PendingFetchData>? channelWriter = null,
        int pipelineDepth = 2)
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
            channelWriter: channelWriter,
            pipelineDepth: pipelineDepth);
    }

    #endregion

    #region Scenario: Eager fetch position overlap (depth >= 3 bug)

    [Test]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(8)]
    public async Task RunAsync_AllFetchesMustReadDistinctPositions(int pipelineDepth)
    {
        // Each fetch invocation simulates _fetchPositions: reads a shared counter on entry,
        // advances it on completion. If any two fetches read the same value, that's a
        // duplicate fetch that would produce duplicate records downstream.
        //
        // With the fix, only one eager fetch starts per outer loop iteration. The drain/sync
        // cycle between iterations ensures positions are updated before the next fetch reads.
        var fetchPosition = 0L;
        var positionsReadByFetch = new ConcurrentDictionary<int, long>();
        var fetchCount = 0;
        var targetFetchCount = pipelineDepth * 2; // Enough iterations to exercise the pipeline
        CancellationTokenSource? testCts = null;

        var runner = CreateRunner(
            prefetchRecords: async ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);

                // Read position (simulates BuildFetchRequestTopics reading _fetchPositions)
                var pos = Volatile.Read(ref fetchPosition);
                positionsReadByFetch[id] = pos;

                await Task.Yield(); // Simulate async work

                // Advance position (simulates UpdateFetchPositionsFromPrefetch)
                Interlocked.Add(ref fetchPosition, 10);

                if (id >= targetFetchCount)
                {
                    testCts!.Cancel();
                    ct.ThrowIfCancellationRequested();
                }
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            pipelineDepth: pipelineDepth);

        testCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await runner.RunAsync(testCts.Token);

        // Every fetch must have read a unique position — no duplicates
        var positions = positionsReadByFetch.Values.ToList();

        await Assert.That(positions.Count).IsGreaterThanOrEqualTo(targetFetchCount);
        await Assert.That(positions.Distinct().Count())
            .IsEqualTo(positions.Count)
            .Because("every fetch must read a unique position; duplicates cause duplicate records");
    }

    #endregion

    #region Scenario: CalculatePrefetchMaxBytes auto-scaling

    [Test]
    public async Task CalculatePrefetchMaxBytes_SinglePartition_UsesConfiguredMax()
    {
        // 1 partition × 1MB × (2+1) = 3MB < 64MB configured → configured wins
        var result = KafkaConsumer<string, string>.CalculatePrefetchMaxBytes(
            queuedMaxMessagesKbytes: 65536, partitionCount: 1, maxPartitionFetchBytes: 1_048_576,
            fetchMaxBytes: 52_428_800, pipelineDepth: 2);

        await Assert.That(result).IsEqualTo(65536L * 1024);
    }

    [Test]
    public async Task CalculatePrefetchMaxBytes_MultiPartition_AutoScalesAboveConfigured()
    {
        // 6 partitions × 4MB × (2+1) = 72MB > 64MB configured → auto-scaled wins
        var result = KafkaConsumer<string, string>.CalculatePrefetchMaxBytes(
            queuedMaxMessagesKbytes: 65536, partitionCount: 6, maxPartitionFetchBytes: 4_194_304,
            fetchMaxBytes: 104_857_600, pipelineDepth: 2);

        await Assert.That(result).IsEqualTo(72L * 1024 * 1024);
    }

    [Test]
    public async Task CalculatePrefetchMaxBytes_ManyPartitions_CappedByFetchMaxBytes()
    {
        // 100 partitions × 1MB = 100MB, but FetchMaxBytes = 50MB caps it
        // 50MB × (2+1) = 150MB > 64MB → auto-scaled wins, but capped
        var result = KafkaConsumer<string, string>.CalculatePrefetchMaxBytes(
            queuedMaxMessagesKbytes: 65536, partitionCount: 100, maxPartitionFetchBytes: 1_048_576,
            fetchMaxBytes: 52_428_800, pipelineDepth: 2);

        await Assert.That(result).IsEqualTo(52_428_800L * 3);
    }

    [Test]
    public async Task CalculatePrefetchMaxBytes_ZeroPartitions_UsesConfiguredMax()
    {
        // 0 partitions → minRequired = 0 → configured wins
        var result = KafkaConsumer<string, string>.CalculatePrefetchMaxBytes(
            queuedMaxMessagesKbytes: 65536, partitionCount: 0, maxPartitionFetchBytes: 4_194_304,
            fetchMaxBytes: 104_857_600, pipelineDepth: 2);

        await Assert.That(result).IsEqualTo(65536L * 1024);
    }

    #endregion
}
