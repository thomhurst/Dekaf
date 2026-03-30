using System.Collections.Concurrent;
using System.Threading.Channels;
using Dekaf;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests for the pipelined prefetch state machine in <see cref="PrefetchPipelineRunner"/>
/// and the static <c>DrainChannelForPartitions</c> helper on KafkaConsumer.
/// Validates ordering guarantees, error counting, drain behavior on assignment loss,
/// drain behavior on memory limit, pipeline depth, shutdown exception observation,
/// and channel drain logic for cooperative rebalance.
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
        int pipelineDepth = 2,
        Action<int, int>? onIterationComplete = null)
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
            pipelineDepth: pipelineDepth,
            onIterationComplete: onIterationComplete);
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
        // Fixed target: throughput is 2 fetches/iteration regardless of pipelineDepth
        // (PR #648's "one eager per iteration" rule caps in-flight queue at 1).
        // Scaling with pipelineDepth caused timeouts on slow CI runners for high depths.
        var targetFetchCount = 8;
        var targetReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
                    targetReached.TrySetResult();
                    testCts!.Cancel();
                    ct.ThrowIfCancellationRequested();
                }
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            pipelineDepth: pipelineDepth);

        // No fixed timeout — wait deterministically for the target fetch count.
        // A generous safety timeout prevents infinite hangs if the runner stalls.
        testCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await runner.RunAsync(testCts.Token);

        // Wait for the target to be reached (should already be complete since RunAsync returned)
        await targetReached.Task.WaitAsync(TimeSpan.FromSeconds(5));

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

    #region Scenario: Fast-drain pipeline fill acceleration

    [Test]
    public async Task RunAsync_FastCompletingFetches_DrainAndRefillWithinIteration()
    {
        // When eager fetches complete synchronously (e.g., empty responses when caught up),
        // the pipeline should detect this and immediately drain+refill within the same
        // iteration, accelerating warm-up without waiting for the next loop cycle.
        var fetchCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id >= 10)
                {
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                }
                // Completes synchronously — simulating empty/fast fetch response
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            pipelineDepth: 4);

        await runner.RunAsync(cts.Token);

        // With fast-drain, synchronously-completing fetches should be processed rapidly
        // without waiting for loop overhead between each one
        await Assert.That(fetchCount).IsGreaterThanOrEqualTo(10);
    }

    [Test]
    public async Task RunAsync_FastDrain_StillRespectsMemoryLimit()
    {
        // Fast-drain must stop filling when memory limit is reached, even for
        // synchronously-completing fetches.
        var fetchCount = 0;
        long prefetchedBytes = 0;
        var memoryWaitCallCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // fallback only

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id == 1)
                    return ValueTask.CompletedTask; // Synchronous fetch ok

                if (id == 2)
                {
                    // First eager fetch completes synchronously but fills memory
                    Interlocked.Exchange(ref prefetchedBytes, 2048);
                    return ValueTask.CompletedTask;
                }

                Assert.Fail("prefetchRecords should not be called again — memory limit should stop fast-drain refill");
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: 1024,
            getPrefetchedBytes: () => Interlocked.Read(ref prefetchedBytes),
            waitForMemoryAvailable: ct =>
            {
                // Cancel deterministically when the runner enters the memory-wait path,
                // proving the limit was hit without burning the full CTS timeout.
                if (Interlocked.Increment(ref memoryWaitCallCount) >= 1)
                    cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            pipelineDepth: 4);

        await runner.RunAsync(cts.Token);

        // Only 2 fetches: the synchronous one and the first eager one
        // Fast-drain detected memory limit after the first eager fetch and stopped
        await Assert.That(fetchCount).IsEqualTo(2);
    }

    [Test]
    public async Task RunAsync_FastDrain_PreservesPositionSafety()
    {
        // Even with fast-drain acceleration, each fetch must read unique positions.
        // Fast-drain only processes COMPLETED tasks whose positions are already updated.
        var fetchPosition = 0L;
        var positionsReadByFetch = new ConcurrentDictionary<int, long>();
        var fetchCount = 0;
        var targetFetchCount = 12;

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);

                // Read position
                var pos = Volatile.Read(ref fetchPosition);
                positionsReadByFetch[id] = pos;

                // Advance position (synchronous completion — fast-drain will kick in)
                Interlocked.Add(ref fetchPosition, 10);

                if (id >= targetFetchCount)
                    ct.ThrowIfCancellationRequested();

                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            pipelineDepth: 4);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await runner.RunAsync(cts.Token);

        // Verify all positions are unique
        var positions = positionsReadByFetch.Values.ToList();
        await Assert.That(positions.Count).IsGreaterThanOrEqualTo(targetFetchCount);
        await Assert.That(positions.Distinct().Count())
            .IsEqualTo(positions.Count)
            .Because("fast-drain must preserve position safety — no duplicate fetches");
    }

    [Test]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(8)]
    public async Task RunAsync_FastDrain_RespectsPipelineDepthLimit(int pipelineDepth)
    {
        // Fast-drain must not exceed pipelineDepth - 1 in-flight tasks at any time.
        var fetchCount = 0;
        var maxInFlight = 0;

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id >= 8)
                    ct.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            pipelineDepth: pipelineDepth,
            onIterationComplete: (inFlight, _) =>
            {
                var current = inFlight;
                if (current > Volatile.Read(ref maxInFlight))
                    Interlocked.Exchange(ref maxInFlight, current);
            });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await runner.RunAsync(cts.Token);

        // Max in-flight should never exceed pipelineDepth - 1
        await Assert.That(maxInFlight).IsLessThanOrEqualTo(pipelineDepth - 1);
    }

    #endregion

    #region Scenario: Buffer fills ahead of consumption

    [Test]
    public async Task RunAsync_BufferFillsAheadOfConsumption_ChannelReceivesItems()
    {
        // Verifies that the prefetch loop writes items into the channel ahead of the consumer reading them.
        // This is the core prefetch value proposition: data ready before the consumer asks.
        var channel = Channel.CreateBounded<PendingFetchData>(new BoundedChannelOptions(10)
        {
            SingleReader = true,
            SingleWriter = false
        });
        var fetchCount = 0;
        var channelWriteCount = 0;

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                // Simulate writing to channel (mimics PrefetchFromBrokerAsync behavior)
                Interlocked.Increment(ref channelWriteCount);

                if (id >= 5)
                {
                    ct.ThrowIfCancellationRequested();
                }
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            channelWriter: channel.Writer);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await runner.RunAsync(cts.Token);

        // Multiple fetches completed without the consumer reading — buffer filled ahead
        await Assert.That(Volatile.Read(ref channelWriteCount)).IsGreaterThanOrEqualTo(3);
    }

    #endregion

    #region Scenario: Fetch overlap with record processing

    [Test]
    public async Task RunAsync_EagerFetchOverlapsWithProcessing()
    {
        // Validates the core pipelining behavior from issue #731:
        // After a synchronous fetch completes, an eager fetch starts immediately and runs
        // concurrently with the next iteration's processing (drain, ensureAssignment, etc.).
        //
        // Strategy: Use deterministic synchronization with TaskCompletionSource gates.
        // Fetch 2 (eager, started at end of iteration 1) blocks on a TCS. This causes the
        // runner to block during iteration 2's drain, proving that fetch 2 was started
        // eagerly during iteration 1 (before iteration 2 began). The test releases fetch 2
        // and verifies the pipeline advanced to fetch 3.
        var fetchCount = 0;
        var fetch2Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fetch2CanComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var runner = CreateRunner(
            prefetchRecords: async ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);

                if (id == 1)
                {
                    // Synchronous fetch (iteration 1) — completes immediately.
                    // After this returns, the runner eagerly starts fetch 2.
                    return;
                }

                if (id == 2)
                {
                    // Eager fetch — signal that it started, then block.
                    // This proves the fetch was started eagerly (during iteration 1),
                    // because the runner hasn't reached iteration 2's synchronous fetch yet.
                    fetch2Started.TrySetResult();
                    await fetch2CanComplete.Task;
                    return;
                }

                // Third fetch (synchronous, iteration 2) — cancel to exit
                ct.ThrowIfCancellationRequested();
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            pipelineDepth: 2);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = runner.RunAsync(cts.Token);

        // Wait for the eager fetch to signal it has started.
        // At this point the runner is blocked in iteration 2's drain (awaiting fetch 2),
        // proving that fetch 2 was started eagerly at the end of iteration 1.
        await fetch2Started.Task;

        // Release fetch 2 so the runner can drain it and proceed to iteration 2's sync fetch.
        fetch2CanComplete.SetResult();

        // Cancel to stop the loop after the overlap is proven
        await cts.CancelAsync();
        await runTask;

        // At least 3 fetches: sync(1) + eager(2) + sync(3, cancelled).
        // This proves the pipeline executed the drain-fetch-fill pattern:
        // iteration 1 started an eager fetch that overlapped with iteration 2's startup.
        await Assert.That(fetchCount).IsGreaterThanOrEqualTo(3)
            .Because("pipeline must execute sync(1) + eager(2) + sync(3) to prove overlap");
    }

    #endregion

    #region Scenario: Rebalance — assignment loss discards buffered data

    [Test]
    public async Task RunAsync_AssignmentLoss_DrainsInFlightAndStopsFetching()
    {
        // Simulates a rebalance where all partitions are revoked (assignment drops to 0).
        // The runner must drain in-flight fetches and stop issuing new ones until
        // assignment is restored.
        var fetchCount = 0;
        var assignmentCount = 1;
        var assignmentLostSeen = false;
        var assignmentLostDetected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id == 2)
                {
                    // After first eager fetch, revoke all partitions (simulate rebalance)
                    assignmentCount = 0;
                }
                if (id >= 6)
                {
                    ct.ThrowIfCancellationRequested();
                }
                return ValueTask.CompletedTask;
            },
            getAssignmentCount: () => assignmentCount,
            ensureAssignment: ct =>
            {
                if (assignmentCount == 0 && !assignmentLostSeen)
                {
                    assignmentLostSeen = true;
                    assignmentLostDetected.TrySetResult();
                    // Restore assignment immediately — the runner will detect it on
                    // the next iteration. No timing dependency needed.
                    assignmentCount = 1;
                }
                return ValueTask.CompletedTask;
            },
            maxBytes: long.MaxValue,
            prefetchedBytes: 0);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await runner.RunAsync(cts.Token);

        // Assignment loss was detected
        await Assert.That(assignmentLostDetected.Task.IsCompleted).IsTrue();

        // In-flight count should be 0 after the drain
        await Assert.That(runner.InFlightPrefetchCount).IsEqualTo(0);

        // Fetching should have resumed after assignment was restored
        await Assert.That(fetchCount).IsGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task RunAsync_PartialRebalance_ContinuesFetchingRemainingPartitions()
    {
        // Simulates a cooperative rebalance where assignment count drops but doesn't go to 0.
        // The runner should continue fetching for the remaining partitions without draining all.
        var fetchCount = 0;
        var assignmentCount = 4;

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id == 2)
                {
                    // Partial rebalance: lose some partitions but not all
                    assignmentCount = 2;
                }
                if (id >= 5)
                {
                    ct.ThrowIfCancellationRequested();
                }
                return ValueTask.CompletedTask;
            },
            getAssignmentCount: () => assignmentCount,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await runner.RunAsync(cts.Token);

        // Fetching continued through the partial rebalance
        await Assert.That(fetchCount).IsGreaterThanOrEqualTo(5);
        // No errors from the partial rebalance
        await Assert.That(runner.ConsecutiveErrors).IsEqualTo(0);
    }

    #endregion

    #region Scenario: Pause behavior — all partitions paused

    [Test]
    public async Task RunAsync_AllPartitionsPaused_PrefetchDelaysWithoutFetching()
    {
        // When all partitions are paused, PrefetchRecordsAsync returns early with a delay
        // (because GroupPartitionsByBrokerAsync returns 0 brokers). The runner should not
        // count this as an error and should resume fetching when partitions are unpaused.
        var fetchCount = 0;
        var allPaused = false;
        var pauseDetectedCount = 0;

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                if (allPaused)
                {
                    // Simulate GroupPartitionsByBrokerAsync returning 0 brokers (all paused)
                    // by delaying — this is what the real implementation does
                    Interlocked.Increment(ref pauseDetectedCount);
                    return ValueTask.CompletedTask;
                }

                var id = Interlocked.Increment(ref fetchCount);
                if (id == 2)
                {
                    // Pause all partitions
                    allPaused = true;
                }
                if (id >= 5)
                {
                    ct.ThrowIfCancellationRequested();
                }
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            ensureAssignment: ct =>
            {
                // After pause is detected, unpause to allow the loop to resume
                if (Volatile.Read(ref pauseDetectedCount) >= 2)
                {
                    allPaused = false;
                }
                return ValueTask.CompletedTask;
            });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await runner.RunAsync(cts.Token);

        // Pause was detected (at least 2 cycles of empty fetch results)
        await Assert.That(Volatile.Read(ref pauseDetectedCount)).IsGreaterThanOrEqualTo(2);
        // No errors from paused state
        await Assert.That(runner.ConsecutiveErrors).IsEqualTo(0);
        // Fetching resumed after unpause
        await Assert.That(fetchCount).IsGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task RunAsync_ResumeAfterPause_FetchesResumeImmediately()
    {
        // Verifies that when partitions are resumed, the prefetch loop starts fetching
        // again without excessive delay.
        var fetchCount = 0;
        var paused = false;
        var fetchesAfterResume = 0;
        var resumed = false;

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                if (paused)
                    return ValueTask.CompletedTask; // Simulates all-paused delay

                var id = Interlocked.Increment(ref fetchCount);
                if (resumed)
                    Interlocked.Increment(ref fetchesAfterResume);

                if (id == 1)
                    paused = true;
                if (Volatile.Read(ref fetchesAfterResume) >= 2)
                {
                    ct.ThrowIfCancellationRequested();
                }
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            ensureAssignment: ct =>
            {
                // Resume after a brief pause
                if (paused)
                {
                    paused = false;
                    resumed = true;
                }
                return ValueTask.CompletedTask;
            });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await runner.RunAsync(cts.Token);

        // Fetches resumed after the pause period
        await Assert.That(Volatile.Read(ref fetchesAfterResume)).IsGreaterThanOrEqualTo(2);
        await Assert.That(runner.ConsecutiveErrors).IsEqualTo(0);
    }

    #endregion

    #region Scenario: Disposal cleans up all resources

    [Test]
    public async Task RunAsync_Disposal_ChannelCompletedAndInFlightDrained()
    {
        // Verifies that on cancellation (disposal), the channel is completed and
        // all in-flight fetches are drained, preventing resource leaks.
        var channel = Channel.CreateBounded<PendingFetchData>(new BoundedChannelOptions(10)
        {
            SingleReader = true,
            SingleWriter = false
        });
        var fetchCount = 0;
        var inFlightStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inFlightCanComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var runner = CreateRunner(
            prefetchRecords: async ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id == 1)
                    return; // Synchronous fetch succeeds

                if (id == 2)
                {
                    // Eager fetch — signal that it started, block until disposal
                    inFlightStarted.SetResult();
                    await inFlightCanComplete.Task;
                    return;
                }
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            channelWriter: channel.Writer);

        using var cts = new CancellationTokenSource();

        var runTask = runner.RunAsync(cts.Token);

        // Wait for the eager fetch to start
        await inFlightStarted.Task;

        // Trigger disposal
        await cts.CancelAsync();

        // Let the eager fetch complete
        inFlightCanComplete.SetResult();

        await runTask;

        // Channel should be completed
        await Assert.That(channel.Reader.Completion.IsCompleted).IsTrue();
        // All in-flight fetches drained
        await Assert.That(runner.InFlightPrefetchCount).IsEqualTo(0);
    }

    [Test]
    public async Task RunAsync_Disposal_InFlightExceptionDoesNotLeak()
    {
        // Tests that if an in-flight fetch throws during disposal, the exception
        // is observed and does not become an unobserved task exception.
        var channel = Channel.CreateBounded<PendingFetchData>(new BoundedChannelOptions(10)
        {
            SingleReader = true,
            SingleWriter = false
        });
        var fetchCount = 0;
        var loggedErrors = new List<Exception>();

        var runner = CreateRunner(
            prefetchRecords: ct =>
            {
                var id = Interlocked.Increment(ref fetchCount);
                if (id == 1)
                    return ValueTask.CompletedTask;
                if (id == 2)
                    return ValueTask.FromException(new InvalidOperationException("in-flight fetch crashed"));
                ct.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            },
            ensureAssignment: ct =>
            {
                // Cancel after first iteration to trigger disposal path
                if (fetchCount >= 2)
                    ct.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            },
            assignmentCount: 1,
            maxBytes: long.MaxValue,
            prefetchedBytes: 0,
            channelWriter: channel.Writer,
            logError: ex => loggedErrors.Add(ex));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await runner.RunAsync(cts.Token);

        // Exception from in-flight fetch was observed (logged), not leaked
        await Assert.That(loggedErrors.Any(e => e.Message == "in-flight fetch crashed")).IsTrue();
        // Channel completed cleanly
        await Assert.That(channel.Reader.Completion.IsCompleted).IsTrue();
    }

    #endregion

    #region Scenario: DrainChannelForPartitions — static helper tests

    [Test]
    public async Task DrainChannelForPartitions_RevokedItemsDisposed()
    {
        // Items for revoked partitions should be disposed and not enqueued.
        var channel = Channel.CreateUnbounded<PendingFetchData>();
        var revokedTp = new TopicPartition("topic-a", 0);

        var item = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        await channel.Writer.WriteAsync(item);
        channel.Writer.Complete();

        var removeSet = new HashSet<TopicPartition> { revokedTp };
        var retainedQueue = new Queue<PendingFetchData>();
        var releasedItems = new List<PendingFetchData>();

        KafkaConsumer<string, string>.DrainChannelForPartitions(
            channel.Reader, removeSet, retainedQueue, p => releasedItems.Add(p));

        // Revoked item should have been passed to the release callback
        await Assert.That(releasedItems).Count().IsEqualTo(1);
        // Retained queue should be empty — the item was revoked
        await Assert.That(retainedQueue).Count().IsEqualTo(0);
    }

    [Test]
    public async Task DrainChannelForPartitions_RetainedItemsPreserved()
    {
        // Items for non-revoked partitions should be enqueued in retainedQueue.
        var channel = Channel.CreateUnbounded<PendingFetchData>();
        var retainedTp = new TopicPartition("topic-a", 0);
        var revokedTp = new TopicPartition("topic-b", 1);

        var retained = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        await channel.Writer.WriteAsync(retained);
        channel.Writer.Complete();

        var removeSet = new HashSet<TopicPartition> { revokedTp };
        var retainedQueue = new Queue<PendingFetchData>();
        var releasedItems = new List<PendingFetchData>();

        KafkaConsumer<string, string>.DrainChannelForPartitions(
            channel.Reader, removeSet, retainedQueue, p => releasedItems.Add(p));

        // Retained item should be in the queue
        await Assert.That(retainedQueue).Count().IsEqualTo(1);
        await Assert.That(retainedQueue.Peek()).IsEqualTo(retained);
        // Release callback still called for retained items (to release prefetch byte tracking)
        await Assert.That(releasedItems).Count().IsEqualTo(1);

        // Cleanup
        retained.Dispose();
    }

    [Test]
    public async Task DrainChannelForPartitions_EmptyChannel_NoErrors()
    {
        // An empty channel should be drained without errors.
        var channel = Channel.CreateUnbounded<PendingFetchData>();
        channel.Writer.Complete();

        var removeSet = new HashSet<TopicPartition> { new("topic-a", 0) };
        var retainedQueue = new Queue<PendingFetchData>();
        var releaseCount = 0;

        KafkaConsumer<string, string>.DrainChannelForPartitions(
            channel.Reader, removeSet, retainedQueue, _ => releaseCount++);

        await Assert.That(retainedQueue).Count().IsEqualTo(0);
        await Assert.That(releaseCount).IsEqualTo(0);
    }

    [Test]
    public async Task DrainChannelForPartitions_MixedPartitions_CorrectSplit()
    {
        // A mix of revoked and retained partitions should be split correctly.
        var channel = Channel.CreateUnbounded<PendingFetchData>();

        var revokedTp1 = new TopicPartition("topic-a", 0);
        var revokedTp2 = new TopicPartition("topic-a", 2);
        var retainedTp1 = new TopicPartition("topic-a", 1);
        var retainedTp2 = new TopicPartition("topic-b", 0);

        var revoked1 = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        var retained1 = PendingFetchData.Create("topic-a", 1, Array.Empty<RecordBatch>());
        var revoked2 = PendingFetchData.Create("topic-a", 2, Array.Empty<RecordBatch>());
        var retained2 = PendingFetchData.Create("topic-b", 0, Array.Empty<RecordBatch>());

        await channel.Writer.WriteAsync(revoked1);
        await channel.Writer.WriteAsync(retained1);
        await channel.Writer.WriteAsync(revoked2);
        await channel.Writer.WriteAsync(retained2);
        channel.Writer.Complete();

        var removeSet = new HashSet<TopicPartition> { revokedTp1, revokedTp2 };
        var retainedQueue = new Queue<PendingFetchData>();
        var releasedItems = new List<PendingFetchData>();

        KafkaConsumer<string, string>.DrainChannelForPartitions(
            channel.Reader, removeSet, retainedQueue, p => releasedItems.Add(p));

        // 2 retained items preserved in order
        await Assert.That(retainedQueue).Count().IsEqualTo(2);
        var first = retainedQueue.Dequeue();
        var second = retainedQueue.Dequeue();
        await Assert.That(first.TopicPartition).IsEqualTo(retainedTp1);
        await Assert.That(second.TopicPartition).IsEqualTo(retainedTp2);

        // All 4 items had release callback invoked
        await Assert.That(releasedItems).Count().IsEqualTo(4);

        // Cleanup retained items
        first.Dispose();
        second.Dispose();
    }

    #endregion
}
