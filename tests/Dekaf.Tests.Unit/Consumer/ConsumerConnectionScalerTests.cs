using System.Reflection;
using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerConnectionScalerTests
{
    [Test]
    public async Task MaybeScale_PipelineSaturated_ScalesUpAfterSustainedPeriod()
    {
        var scaleUpCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: ct => { scaleUpCount++; return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { return ValueTask.CompletedTask; });

        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(0);

        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(1);
        await Assert.That(scaler.CurrentConnectionCount).IsEqualTo(3);
    }

    [Test]
    public async Task MaybeScale_LowUtilization_ScalesDownAfterSustainedPeriod()
    {
        var scaleDownCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: ct => { return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { scaleDownCount++; return ValueTask.CompletedTask; });

        scaler.TestSetConnectionCount(3);
        scaler.ReportPipelineUtilization(0, 3);
        scaler.MaybeScale();
        await Assert.That(scaleDownCount).IsEqualTo(0);

        scaler.TestAdvanceTime(TimeSpan.FromSeconds(121));
        scaler.ReportPipelineUtilization(0, 3);
        scaler.MaybeScale();
        await Assert.That(scaleDownCount).IsEqualTo(1);
        await Assert.That(scaler.CurrentConnectionCount).IsEqualTo(2);
    }

    [Test]
    public async Task MaybeScale_RespectsMaxConnectionCount()
    {
        var scaleUpCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 3,
            scaleUpAsync: ct => { scaleUpCount++; return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { return ValueTask.CompletedTask; });

        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(1);
        await Assert.That(scaler.CurrentConnectionCount).IsEqualTo(3);

        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(1); // Still 1 — at max
    }

    [Test]
    public async Task MaybeScale_RespectsMinConnectionCount()
    {
        var scaleDownCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: ct => { return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { scaleDownCount++; return ValueTask.CompletedTask; });

        scaler.ReportPipelineUtilization(0, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(121));
        scaler.ReportPipelineUtilization(0, 3);
        scaler.MaybeScale();
        await Assert.That(scaleDownCount).IsEqualTo(0); // At initial — can't go lower
    }

    [Test]
    public async Task MaybeScale_CooldownPreventsRapidScaling()
    {
        var scaleUpCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: ct => { scaleUpCount++; return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { return ValueTask.CompletedTask; });

        // First scale-up
        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(1);

        // Within cooldown
        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(3));
        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(1); // Blocked by cooldown

        // After cooldown
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(2);
    }

    [Test]
    public async Task MaybeScale_SaturationReset_WhenPipelineNotFull()
    {
        var scaleUpCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: ct => { scaleUpCount++; return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { return ValueTask.CompletedTask; });

        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(3));
        scaler.ReportPipelineUtilization(2, 3); // Drop below full — resets timer

        scaler.TestAdvanceTime(TimeSpan.FromSeconds(3));
        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(0); // Interrupted saturation
    }

    [Test]
    public async Task Scaler_DisabledWhenMaxEqualsInitial()
    {
        var scaleUpCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 2, // Same as initial = effectively disabled
            scaleUpAsync: ct => { scaleUpCount++; return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { return ValueTask.CompletedTask; });

        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(0); // Cannot scale
    }

    [Test]
    public async Task MaybeScale_SynchronousScaleFailure_LogsError()
    {
        var exception = new InvalidOperationException("scale failed");
        Exception? logged = null;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: _ => ValueTask.FromException(exception),
            scaleDownAsync: _ => ValueTask.CompletedTask,
            logError: ex => logged = ex);

        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.MaybeScale();

        await TestWait.UntilAsync(() => logged is not null, TimeSpan.FromSeconds(5));
        await Assert.That(logged).IsSameReferenceAs(exception);
    }

    [Test]
    public async Task MaybeScale_SynchronousDelegateThrow_LogsError()
    {
        var exception = new InvalidOperationException("scale failed synchronously");
        Exception? logged = null;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: _ => throw exception,
            scaleDownAsync: _ => ValueTask.CompletedTask,
            logError: ex => logged = ex);

        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.MaybeScale();

        await Assert.That(logged).IsSameReferenceAs(exception);
    }

    [Test]
    public async Task MaybeScale_StopBeforeOperationLock_DoesNotChangeCountOrDispatch()
    {
        var scaleUpCount = 0;
        var beforeOperationLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var continueScale = new ManualResetEventSlim();
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: _ =>
            {
                Interlocked.Increment(ref scaleUpCount);
                return ValueTask.CompletedTask;
            },
            scaleDownAsync: _ => ValueTask.CompletedTask)
        {
            BeforeScaleOperationLockForTest = () =>
            {
                beforeOperationLock.TrySetResult();
                continueScale.Wait();
            }
        };

        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));

        var scaleTask = Task.Run(scaler.MaybeScale);
        try
        {
            await beforeOperationLock.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await scaler.StopAndDrainAsync(TimeSpan.FromSeconds(1));
        }
        finally
        {
            continueScale.Set();
        }

        await scaleTask.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(scaleUpCount).IsEqualTo(0);
        await Assert.That(scaler.CurrentConnectionCount).IsEqualTo(2);
    }

    [Test]
    public async Task StopAndDrainAsync_PendingOperation_ReturnsAfterTimeout()
    {
        var scaleCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: _ => new ValueTask(scaleCompletion.Task),
            scaleDownAsync: _ => ValueTask.CompletedTask);

        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.MaybeScale();

        try
        {
            await scaler.StopAndDrainAsync(TimeSpan.Zero);
            await Assert.That(scaleCompletion.Task.IsCompleted).IsFalse();
        }
        finally
        {
            scaleCompletion.TrySetResult();
            await scaler.StopAndDrainAsync(TimeSpan.FromSeconds(1));
        }
    }

    [Test]
    public async Task StopAndDrainAsync_WithoutTimeout_WaitsForPendingOperation()
    {
        var scaleCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: _ => new ValueTask(scaleCompletion.Task),
            scaleDownAsync: _ => ValueTask.CompletedTask);

        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.MaybeScale();

        var drainTask = scaler.StopAndDrainAsync().AsTask();
        await Assert.That(drainTask.IsCompleted).IsFalse();

        scaleCompletion.SetResult();
        await drainTask.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task StopAndDrainAsync_CancelsPendingOperation()
    {
        var operationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: async cancellationToken =>
            {
                operationStarted.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    cancellationObserved.TrySetResult();
                    throw;
                }
            },
            scaleDownAsync: _ => ValueTask.CompletedTask);

        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.MaybeScale();

        await operationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await scaler.StopAndDrainAsync(TimeSpan.FromSeconds(1));

        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task Dispose_DisposesOperationCancellationSource()
    {
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: _ => ValueTask.CompletedTask,
            scaleDownAsync: _ => ValueTask.CompletedTask);
        var field = typeof(ConsumerConnectionScaler).GetField(
            "_operationCancellationSource",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Cancellation source field not found");
        var source = (CancellationTokenSource)field.GetValue(scaler)!;

        scaler.Dispose();

        await Assert.That(source.Cancel).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task MaybeScale_SynchronousDelegatePrefix_DoesNotHoldOperationLock()
    {
        var operationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseOperation = new ManualResetEventSlim();
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: _ =>
            {
                operationStarted.TrySetResult();
                releaseOperation.Wait(CancellationToken.None);
                return ValueTask.CompletedTask;
            },
            scaleDownAsync: _ => ValueTask.CompletedTask);

        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));

        var firstScale = Task.Run(scaler.MaybeScale);
        await operationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var secondScale = Task.Run(scaler.MaybeScale);
        try
        {
            await secondScale.WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally
        {
            releaseOperation.Set();
            await firstScale.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Test]
    public async Task ReportFetchReissue_FetchBelowThreshold_NotDue_AndRestartsSaturationWindow()
    {
        var scaleUpCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: ct => { scaleUpCount++; return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { return ValueTask.CompletedTask; });

        // The loop's backlogged wait opens the window; the task then re-issues after two 3 s
        // fetches. The loop-owned path reset the window at every completion, so back-to-back
        // fetches must not read as one 6 s saturation window.
        scaler.ReportPipelineUtilization(1, 1);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(3));
        await Assert.That(scaler.ReportFetchReissue()).IsFalse();
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(3));
        await Assert.That(scaler.ReportFetchReissue()).IsFalse();

        scaler.ReportPipelineUtilization(1, 1);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(0);
        await Assert.That(scaler.CurrentConnectionCount).IsEqualTo(2);
    }

    [Test]
    public async Task ReportFetchReissue_FetchAboveThreshold_Due_AndLoopScalesUp()
    {
        var scaleUpCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: ct => { scaleUpCount++; return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { return ValueTask.CompletedTask; });

        scaler.ReportPipelineUtilization(1, 1);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        await Assert.That(scaler.ReportFetchReissue()).IsTrue();
        // The window is left for the loop, so the decision stays due until the loop acts.
        await Assert.That(scaler.ReportFetchReissue()).IsTrue();

        // The loop's wake-up sequence after the task hands control back.
        scaler.ReportPipelineUtilization(1, 1);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(1);
        await Assert.That(scaler.CurrentConnectionCount).IsEqualTo(3);

        // Re-dispatch, then a long fetch inside the cooldown: not due, window restarted.
        scaler.ReportPipelineUtilization(0, 1);
        scaler.ReportPipelineUtilization(1, 1);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(4));
        await Assert.That(scaler.ReportFetchReissue()).IsFalse();

        // Past the cooldown, the next over-threshold fetch is due again.
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        await Assert.That(scaler.ReportFetchReissue()).IsTrue();
        scaler.ReportPipelineUtilization(1, 1);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(2);
        await Assert.That(scaler.CurrentConnectionCount).IsEqualTo(4);
    }

    [Test]
    public async Task ReportFetchReissue_AtMaxConnections_NeverDue()
    {
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 2,
            scaleUpAsync: ct => { return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { return ValueTask.CompletedTask; });

        scaler.ReportPipelineUtilization(1, 1);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        await Assert.That(scaler.ReportFetchReissue()).IsFalse();
    }

    [Test]
    public async Task ReportFetchReissue_SustainedReissue_DoesNotAccumulateLowUtilization()
    {
        var scaleDownCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 3,
            scaleUpAsync: ct => { return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { scaleDownCount++; return ValueTask.CompletedTask; });
        scaler.TestSetConnectionCount(3);

        // Loop re-dispatch (slot idle for an instant) then the backlogged wait, as before the
        // first re-issue; then 160 s of back-to-back fetches on the busy slot.
        scaler.ReportPipelineUtilization(0, 1);
        scaler.ReportPipelineUtilization(1, 1);
        for (var i = 0; i < 40; i++)
        {
            scaler.TestAdvanceTime(TimeSpan.FromSeconds(4));
            await Assert.That(scaler.ReportFetchReissue()).IsFalse();
        }

        scaler.ReportPipelineUtilization(1, 1);
        scaler.MaybeScale();
        await Assert.That(scaleDownCount).IsEqualTo(0);
        await Assert.That(scaler.CurrentConnectionCount).IsEqualTo(3);
    }

    [Test]
    public async Task ReportPipelineUtilization_ConcurrentWithReissueAndMaybeScale_ScalesOnce()
    {
        var scaleUpCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: ct => { Interlocked.Increment(ref scaleUpCount); return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { return ValueTask.CompletedTask; });

        scaler.ReportPipelineUtilization(1, 1);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));

        // Broker tasks report re-issues while the loop reports and evaluates: every window
        // update and decision is serialized, so exactly one scale-up is taken for the one
        // over-threshold window and the cooldown holds after it.
        const int threadCount = 8;
        using var barrier = new Barrier(threadCount);
        var workers = new Task[threadCount];
        for (var t = 0; t < threadCount; t++)
        {
            var isBrokerTask = (t & 1) == 0;
            workers[t] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < 2_000; i++)
                {
                    if (isBrokerTask)
                        scaler.ReportFetchReissue();
                    else
                        scaler.ReportPipelineUtilization(1, 1);

                    if ((i & 63) == 0)
                        scaler.MaybeScale();
                }
            });
        }

        await Task.WhenAll(workers);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(1);
        await Assert.That(scaler.CurrentConnectionCount).IsEqualTo(3);
    }

    [Test]
    public async Task SplitPartitions_SingleConnection_ReturnsSingleGroup()
    {
        var groups = new (int StartIndex, int Count)[1];
        var count = ConsumerConnectionScaler.SplitPartitionsAcrossConnections(6, 1, groups);

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(groups[0].StartIndex).IsEqualTo(0);
        await Assert.That(groups[0].Count).IsEqualTo(6);
    }

    [Test]
    public async Task SplitPartitions_EvenSplit_DistributesEqually()
    {
        var groups = new (int StartIndex, int Count)[3];
        var count = ConsumerConnectionScaler.SplitPartitionsAcrossConnections(6, 3, groups);

        await Assert.That(count).IsEqualTo(3);
        await Assert.That(groups[0]).IsEqualTo((0, 2));
        await Assert.That(groups[1]).IsEqualTo((2, 2));
        await Assert.That(groups[2]).IsEqualTo((4, 2));
    }

    [Test]
    public async Task SplitPartitions_UnevenSplit_DistributesRemainderToFirstGroups()
    {
        var groups = new (int StartIndex, int Count)[3];
        var count = ConsumerConnectionScaler.SplitPartitionsAcrossConnections(7, 3, groups);

        await Assert.That(count).IsEqualTo(3);
        // 7 / 3 = 2 base + 1 remainder => first group gets 3, others get 2
        await Assert.That(groups[0]).IsEqualTo((0, 3));
        await Assert.That(groups[1]).IsEqualTo((3, 2));
        await Assert.That(groups[2]).IsEqualTo((5, 2));
    }

    [Test]
    public async Task SplitPartitions_MoreConnectionsThanPartitions_LimitsToPartitionCount()
    {
        var groups = new (int StartIndex, int Count)[5];
        var count = ConsumerConnectionScaler.SplitPartitionsAcrossConnections(2, 5, groups);

        await Assert.That(count).IsEqualTo(2);
        await Assert.That(groups[0]).IsEqualTo((0, 1));
        await Assert.That(groups[1]).IsEqualTo((1, 1));
    }

    [Test]
    public async Task SplitPartitions_SinglePartition_ReturnsSingleGroup()
    {
        var groups = new (int StartIndex, int Count)[3];
        var count = ConsumerConnectionScaler.SplitPartitionsAcrossConnections(1, 3, groups);

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(groups[0]).IsEqualTo((0, 1));
    }

    [Test]
    public async Task SplitPartitions_CoversAllPartitions()
    {
        // Verify that all partitions are accounted for with various splits
        var groups = new (int StartIndex, int Count)[8];
        for (var partitions = 1; partitions <= 20; partitions++)
        {
            for (var connections = 1; connections <= 5; connections++)
            {
                var groupCount = ConsumerConnectionScaler.SplitPartitionsAcrossConnections(
                    partitions, connections, groups);

                var totalPartitions = 0;
                for (var i = 0; i < groupCount; i++)
                    totalPartitions += groups[i].Count;

                await Assert.That(totalPartitions).IsEqualTo(partitions);
            }
        }
    }
}
