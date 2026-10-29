using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class PartitionLaneStopTests
{
    [Test]
    [Arguments(PartitionStopPolicy.Cancel, false, false)]
    [Arguments(PartitionStopPolicy.Cancel, false, true)]
    [Arguments(PartitionStopPolicy.Drain, false, false)]
    [Arguments(PartitionStopPolicy.Cancel, true, false)]
    [Arguments(PartitionStopPolicy.Drain, true, false)]
    public async Task StopAsync_BoundsBlockedCancellationCallback(
        PartitionStopPolicy policy, bool cancelWait, bool processorExits)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var stopCancellation = new CancellationTokenSource();
        using var releaseCallback = new ManualResetEventSlim();
        var started = NewSignal();
        var callbackEntered = NewSignal();
        var callbackExited = NewSignal();
        var releaseProcessor = NewSignal();
        var processorExited = NewSignal();
        var lane = CreateLane();
        CancellationTokenRegistration registration = default;
        lane.Start(async (_, token) =>
        {
            registration = token.Register(() =>
            {
                callbackEntered.TrySetResult();
                releaseCallback.Wait();
                callbackExited.TrySetResult();
            });
            started.TrySetResult();
            await releaseProcessor.Task;
            processorExited.TrySetResult();
        });

        Task<Exception?>? stopping = null;
        try
        {
            await started.Task.WaitAsync(deadline.Token);
            stopping = lane.StopAsync(policy,
                cancelWait ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(100),
                stopCancellation.Token).AsTask();
            if (cancelWait && policy == PartitionStopPolicy.Drain)
                await stopCancellation.CancelAsync();

            await callbackEntered.Task.WaitAsync(deadline.Token);
            if (processorExits)
            {
                releaseProcessor.TrySetResult();
                await processorExited.Task.WaitAsync(deadline.Token);
            }
            if (cancelWait && policy == PartitionStopPolicy.Cancel)
                await stopCancellation.CancelAsync();

            var exception = await stopping.WaitAsync(deadline.Token);
            await Assert.That(exception).IsTypeOf<TimeoutException>();
            await Assert.That(callbackExited.Task.IsCompleted).IsFalse();
            await Assert.That(processorExited.Task.IsCompleted).IsEqualTo(processorExits);
        }
        finally
        {
            releaseCallback.Set();
            releaseProcessor.TrySetResult();
            if (stopping is not null)
                await stopping.WaitAsync(TimeSpan.FromSeconds(10));
            await lane.StopAsync(PartitionStopPolicy.Cancel, TimeSpan.FromSeconds(10));
            await callbackExited.Task.WaitAsync(TimeSpan.FromSeconds(10));
            registration.Dispose();
        }
    }

    [Test]
    public async Task StopAsync_WaitsForCancellationCallbacksWithinBudget()
    {
        using var releaseCallback = new ManualResetEventSlim();
        var callbackEntered = NewSignal();
        var lane = CreateLane();
        using var registration = lane.StoppingToken.Register(() =>
        {
            callbackEntered.TrySetResult();
            releaseCallback.Wait();
        });
        var stopping = lane.StopAsync(PartitionStopPolicy.Cancel, Timeout.InfiniteTimeSpan).AsTask();
        try
        {
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(stopping.IsCompleted).IsFalse();
        }
        finally
        {
            releaseCallback.Set();
        }
        await Assert.That(await stopping.WaitAsync(TimeSpan.FromSeconds(10))).IsNull();
    }

    [Test]
    public async Task StopAsync_ReportsCancellationCallbackFailure()
    {
        var lane = CreateLane();
        var failure = new InvalidOperationException("callback failed");
        using var registration = lane.StoppingToken.Register(() => throw failure);

        var exception = await lane.StopAsync(PartitionStopPolicy.Cancel, TimeSpan.FromSeconds(10));

        await Assert.That(exception).IsTypeOf<AggregateException>();
        await Assert.That(((AggregateException)exception!).InnerExceptions).Contains(failure);
    }

    [Test]
    [Arguments(PartitionStopPolicy.Cancel, false)]
    [Arguments(PartitionStopPolicy.Cancel, true)]
    [Arguments(PartitionStopPolicy.Drain, false)]
    [Arguments(PartitionStopPolicy.Drain, true)]
    public async Task RunPartitionedAsync_BlockedCallbackDoesNotPreventOtherLanesStopping(
        PartitionStopPolicy policy, bool revoke)
    {
        var consumer = new PartitionedConsumerRuntimeTests.TestConsumer();
        consumer.SetAssignment(new TopicPartition("stop", 0), new TopicPartition("stop", 1));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var stop = new CancellationTokenSource();
        using var releaseCallback = new ManualResetEventSlim();
        var started = new[] { NewSignal(), NewSignal() };
        var cancelled = new[] { NewSignal(), NewSignal() };
        var exited = new[] { NewSignal(), NewSignal() };
        var releaseProcessor = NewSignal();
        var running = consumer.RunPartitionedAsync(async (context, token) =>
        {
            var partition = context.TopicPartition.Partition;
            try
            {
                using var registration = token.Register(() =>
                {
                    cancelled[partition].TrySetResult();
                    if (partition == 0)
                        releaseCallback.Wait();
                });
                started[partition].TrySetResult();
                await releaseProcessor.Task;
            }
            finally
            {
                exited[partition].TrySetResult();
            }
        }, new PartitionedProcessingOptions
        {
            StopPolicy = policy,
            StopTimeout = TimeSpan.FromMilliseconds(100),
            CommitPolicy = PartitionCommitPolicy.UserManaged
        }, stop.Token).AsTask();

        try
        {
            await Task.WhenAll(started[0].Task, started[1].Task).WaitAsync(deadline.Token);
            if (revoke)
                consumer.SetAssignment();
            else
                await stop.CancelAsync();

            // Wait for completion without confusing the lane's TimeoutException with
            // the independent test deadline. Both lanes must cancel before either gate opens.
            await Task.WhenAny(running).WaitAsync(deadline.Token);
            await Task.WhenAll(cancelled[0].Task, cancelled[1].Task).WaitAsync(deadline.Token);
            await Assert.That(exited[0].Task.IsCompleted).IsFalse();
        }
        finally
        {
            releaseCallback.Set();
            releaseProcessor.TrySetResult();
            await stop.CancelAsync();
            try
            {
                await running.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException) when (running.IsFaulted) { }
            catch (OperationCanceledException) when (running.IsCanceled) { }
            await Task.WhenAll(exited[0].Task, exited[1].Task).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private static PartitionLane<string, string> CreateLane() => new(
        new TopicPartition("stop", 0), 1,
        static (_, _) => default, static _ => { }, static (_, _) => { });

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
