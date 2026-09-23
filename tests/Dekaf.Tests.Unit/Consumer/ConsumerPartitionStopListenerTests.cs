using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerPartitionStopListenerTests
{
    [Test]
    public async Task ConsumerOptions_PartitionStopTimeout_DefaultsToFiveSeconds()
    {
        var options = new ConsumerOptions { BootstrapServers = ["localhost:9092"] };

        await Assert.That(options.PartitionStopTimeout).IsEqualTo(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task ConsumerOptions_PartitionStopTimeout_RejectsInvalidValue()
    {
        await Assert.That(() => new KafkaConsumer<string, string>(
                new ConsumerOptions
                {
                    BootstrapServers = ["localhost:9092"],
                    QueuedMinMessages = 1,
                    PartitionStopTimeout = TimeSpan.Zero
                },
                Serializers.String,
                Serializers.String))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task CloseAsync_InvokesPartitionStopListenerWithCurrentAssignment()
    {
        var listener = new TrackingPartitionStopListener();
        await using var consumer = CreateConsumer(listener);
        var partition = new TopicPartition("topic-a", 0);
        consumer.Assign(partition);

        await consumer.CloseAsync(CancellationToken.None);

        await Assert.That(listener.StoppedPartitions).Count().IsEqualTo(1);
        await Assert.That(listener.StoppedPartitions[0]).IsEquivalentTo([partition]);
        await Assert.That(consumer.Assignment).IsEmpty();
    }

    [Test]
    public async Task DisposeAsync_InvokesPartitionStopListenerWithCurrentAssignment()
    {
        var listener = new TrackingPartitionStopListener();
        var consumer = CreateConsumer(listener);
        var partition = new TopicPartition("topic-a", 1);
        consumer.Assign(partition);

        await consumer.DisposeAsync();

        await Assert.That(listener.StoppedPartitions).Count().IsEqualTo(1);
        await Assert.That(listener.StoppedPartitions[0]).IsEquivalentTo([partition]);
        await Assert.That(consumer.Assignment).IsEmpty();
    }

    [Test]
    public async Task CloseAsync_PassesCancellationTokenToPartitionStopListener()
    {
        var listener = new TrackingPartitionStopListener
        {
            OnStopped = (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            }
        };
        await using var consumer = CreateConsumer(listener);
        consumer.Assign(new TopicPartition("topic-a", 0));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.That(async () => await consumer.CloseAsync(cts.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(listener.CancellationTokens).Count().IsEqualTo(1);
        await Assert.That(listener.CancellationTokens[0].IsCancellationRequested).IsTrue();
        await Assert.That(consumer.Assignment).IsEmpty();
    }

    [Test]
    public async Task CloseAsync_BlockingPartitionStopListener_UsesDefaultApiTimeout()
    {
        var listener = new TrackingPartitionStopListener
        {
            OnStopped = static async (_, cancellationToken) =>
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false)
        };
        await using var consumer = CreateConsumer(listener, defaultApiTimeoutMs: 100);
        consumer.Assign(new TopicPartition("topic-a", 0));

        var exception = await Assert.That(async () => await consumer.CloseAsync())
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);
        await Assert.That(exception.Configured).IsEqualTo(TimeSpan.FromMilliseconds(100));
        await Assert.That(listener.CancellationTokens[0].IsCancellationRequested).IsTrue();
    }

    [Test]
    [Timeout(30_000)]
    public async Task CloseAsync_PartitionStopTimeout_CancelsListenerAndContinuesCleanup(
        CancellationToken testTimeout)
    {
        var releaseListener = new TaskCompletionSource();
        var listenerCompleted = new TaskCompletionSource();
        var listener = new TrackingPartitionStopListener
        {
            OnStopped = async (_, _) =>
            {
                try
                {
                    await releaseListener.Task.ConfigureAwait(false);
                }
                finally
                {
                    listenerCompleted.TrySetResult();
                }
            }
        };
        var consumer = CreateConsumer(
            listener,
            // Keep the API backstop above telemetry's independent five-second stop bound.
            // The TUnit timeout remains the outer hang guard for the whole test.
            defaultApiTimeoutMs: 20_000,
            partitionStopTimeout: TimeSpan.FromMilliseconds(50));
        var partition = new TopicPartition("topic-a", 0);
        consumer.Assign(partition);
        Task? close = null;

        try
        {
            close = consumer.CloseAsync(CancellationToken.None).AsTask();

            await close.WaitAsync(testTimeout);

            await Assert.That(listener.CancellationTokens[0].IsCancellationRequested).IsTrue();
            await Assert.That(listenerCompleted.Task.IsCompleted).IsFalse();
            await Assert.That(consumer.Assignment).IsEmpty();
        }
        finally
        {
            releaseListener.TrySetResult();
            if (close is not null)
                await close.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            await consumer.DisposeAsync();
            try
            {
                await listenerCompleted.Task.WaitAsync(testTimeout);
            }
            catch (OperationCanceledException) when (testTimeout.IsCancellationRequested)
            {
                // Preserve the original timeout failure after cleanup completes.
            }
        }
    }

    [Test]
    public async Task CloseAsync_BlockingHeartbeatShutdown_ObservesAggregateCancellation()
    {
        await using var consumer = CreateGroupConsumer(defaultApiTimeoutMs: 60_000);
        var coordinator = GetCoordinator(consumer);
        SetField(coordinator, "_heartbeatTask", new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var close = consumer.CloseAsync(cts.Token);

        await Assert.That(close.IsCompleted).IsTrue();
        await Assert.That(async () => await close).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task CloseAsync_RemainInGroup_ReportsAnUndeliveredLoss()
    {
        var listener = new TrackingPartitionStopListener();
        await using var consumer = CreateGroupConsumer(defaultApiTimeoutMs: 60_000, listener);
        var coordinator = GetCoordinator(consumer);
        var partition = new TopicPartition("topic-a", 0);

        // A fenced member whose OnPartitionsLost has not completed (the heartbeat stop
        // interrupted it) closes without sending a leave.
        SetField(coordinator, "_assignedPartitions", new HashSet<TopicPartition> { partition });
        typeof(ConsumerCoordinator)
            .GetMethod("FenceMembership", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(coordinator, [false]);

        await consumer.CloseAsync(
            new ConsumerCloseOptions { GroupMembershipOperation = ConsumerGroupMembershipOperation.RemainInGroup },
            CancellationToken.None);

        await Assert.That(listener.LostPartitions).Count().IsEqualTo(1);
        await Assert.That(listener.LostPartitions[0]).IsEquivalentTo([partition]);
    }

    [Test]
    public async Task CloseAsync_LostListenerIgnoringCancellation_IsBoundedByTheApiTimeout()
    {
        var neverCompletes = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new TrackingPartitionStopListener
        {
            OnLost = (_, _) => new ValueTask(neverCompletes.Task)
        };
        var consumer = CreateGroupConsumer(defaultApiTimeoutMs: 200, listener);
        var coordinator = GetCoordinator(consumer);
        SetField(coordinator, "_assignedPartitions", new HashSet<TopicPartition> { new("topic-a", 0) });
        typeof(ConsumerCoordinator)
            .GetMethod("FenceMembership", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(coordinator, [false]);

        try
        {
            var close = consumer.CloseAsync(
                new ConsumerCloseOptions { GroupMembershipOperation = ConsumerGroupMembershipOperation.RemainInGroup },
                CancellationToken.None).AsTask();
            var completed = await Task.WhenAny(close, Task.Delay(TimeSpan.FromSeconds(10)));
            await Assert.That(completed).IsSameReferenceAs(close);
        }
        finally
        {
            neverCompletes.TrySetResult();
            await consumer.DisposeAsync();
        }
    }

    [Test]
    public async Task DisposeAsync_BlockingPartitionStopListener_UsesShorterDefaultApiTimeout()
    {
        var listener = new TrackingPartitionStopListener
        {
            OnStopped = static async (_, cancellationToken) =>
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false)
        };
        var consumer = CreateConsumer(listener, defaultApiTimeoutMs: 100);
        consumer.Assign(new TopicPartition("topic-a", 0));

        await consumer.DisposeAsync();

        await Assert.That(listener.CancellationTokens[0].IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task CloseAsync_SuppressesPartitionStopListenerNonCancellationException()
    {
        var listener = new TrackingPartitionStopListener
        {
            OnStopped = (_, _) => throw new InvalidOperationException("stop failed")
        };
        await using var consumer = CreateConsumer(listener);
        consumer.Assign(new TopicPartition("topic-a", 0));

        await Assert.That(async () => await consumer.CloseAsync(CancellationToken.None))
            .ThrowsNothing();
        await Assert.That(listener.StoppedPartitions).Count().IsEqualTo(1);
        await Assert.That(consumer.Assignment).IsEmpty();
    }

    private static KafkaConsumer<string, string> CreateConsumer(
        IRebalanceListener listener,
        int defaultApiTimeoutMs = 60_000,
        TimeSpan? partitionStopTimeout = null)
    {
        return new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1,
                RebalanceListener = listener,
                DefaultApiTimeoutMs = defaultApiTimeoutMs,
                PartitionStopTimeout = partitionStopTimeout ?? TimeSpan.FromSeconds(5)
            },
            Serializers.String,
            Serializers.String);
    }

    private static KafkaConsumer<string, string> CreateGroupConsumer(
        int defaultApiTimeoutMs,
        IRebalanceListener? listener = null)
    {
        return new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "group-a",
                RebalanceListener = listener,
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1,
                DefaultApiTimeoutMs = defaultApiTimeoutMs
            },
            Serializers.String,
            Serializers.String);
    }

    private static ConsumerCoordinator GetCoordinator(KafkaConsumer<string, string> consumer) =>
        (ConsumerCoordinator)GetField(consumer, "_coordinator");

    private static object GetField(object instance, string fieldName) =>
        instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(instance)
        ?? throw new InvalidOperationException($"{fieldName} field not found.");

    private static void SetField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{fieldName} field not found.");
        field.SetValue(instance, value);
    }

    private sealed class TrackingPartitionStopListener : IRebalanceListener, IPartitionStopListener
    {
        public List<List<TopicPartition>> StoppedPartitions { get; } = [];
        public List<CancellationToken> CancellationTokens { get; } = [];
        public List<List<TopicPartition>> LostPartitions { get; } = [];
        public Func<IEnumerable<TopicPartition>, CancellationToken, ValueTask>? OnStopped { get; init; }
        public Func<IEnumerable<TopicPartition>, CancellationToken, ValueTask>? OnLost { get; init; }

        public ValueTask OnPartitionsAssignedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask OnPartitionsRevokedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask OnPartitionsLostAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            LostPartitions.Add(partitions.ToList());
            return OnLost is null ? ValueTask.CompletedTask : OnLost(partitions, cancellationToken);
        }

        public ValueTask OnPartitionsStoppedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            StoppedPartitions.Add(partitions.ToList());
            CancellationTokens.Add(cancellationToken);
            return OnStopped is null
                ? ValueTask.CompletedTask
                : OnStopped(partitions, cancellationToken);
        }
    }
}
