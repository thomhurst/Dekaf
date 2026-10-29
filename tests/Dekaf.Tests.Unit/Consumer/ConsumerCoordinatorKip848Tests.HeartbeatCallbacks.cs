using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Telemetry;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

public sealed partial class ConsumerCoordinatorKip848Tests
{
    [Test]
    [Timeout(10_000)]
    public async Task ConsumerProtocol_HeartbeatCallbackFailure_RequestsRejoin(CancellationToken cancellationToken)
    {
        var script = new HeartbeatScript(this);
        var listener = Substitute.For<IRebalanceListener>();
        var failNextRevocation = 1;
        listener.OnPartitionsRevokedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Exchange(ref failNextRevocation, 0) == 1
                // Ordinary listener exceptions are logged and suppressed. Unexpected cancellation
                // escapes the listener wrapper and must reach the heartbeat's recovery path.
                ? ValueTask.FromException(new OperationCanceledException("callback failed"))
                : ValueTask.CompletedTask);
        await using var coordinator = await JoinAsync(script, listener);
        script.Respond = (_, _) => Joined("member-1", 6, CreateAssignment(TestTopicId, 0));

        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);
        await Assert.That(GetPrivateField<object?>(coordinator, "_heartbeatCallbackFailure")).IsNotNull();
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);
        await InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, cancellationToken).WaitAsync(cancellationToken);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(GetPrivateField<int>(coordinator, "_coordinatorId")).IsEqualTo(-1);
        await Assert.That(GetPrivateField<string?>(coordinator, "_lastHeartbeatFailure")).IsEqualTo("callback failed");
        await Assert.That(GetPrivateField<object?>(coordinator, "_heartbeatCallbackFailure")).IsNull();

        script.Respond = (_, _) => Joined("member-2", 1, CreateAssignment(TestTopicId, 1));
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, cancellationToken);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.MemberId).IsEqualTo("member-2");
        await Assert.That(coordinator.Assignment).IsEquivalentTo([new TopicPartition("test-topic", 1)]);
    }

    [Test]
    [Timeout(10_000)]
    public async Task ConsumerProtocol_HeartbeatCallbackFailure_FromOldMembership_IsIgnored(
        CancellationToken cancellationToken)
    {
        var script = new HeartbeatScript(this);
        var listener = Substitute.For<IRebalanceListener>();
        var failNextRevocation = 1;
        listener.OnPartitionsRevokedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Exchange(ref failNextRevocation, 0) == 1
                ? ValueTask.FromException(new OperationCanceledException("old callback failed"))
                : ValueTask.CompletedTask);
        await using var coordinator = await JoinAsync(script, listener);
        script.Respond = (_, _) => Joined("member-1", 6, CreateAssignment(TestTopicId, 0));
        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);
        await Assert.That(GetPrivateField<object?>(coordinator, "_heartbeatCallbackFailure")).IsNotNull();

        // A foreground fence replaces the membership before the heartbeat observes its failure.
        await InvokePartitionsLostAsync(coordinator, coordinator.Assignment.ToArray());
        await Assert.That(GetPrivateField<object?>(coordinator, "_heartbeatCallbackFailure")).IsNotNull();
        script.Respond = (_, _) => Joined("member-2", 1, CreateAssignment(TestTopicId, 1));
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, cancellationToken);
        await coordinator.StopHeartbeatAsync();

        // A successful heartbeat proves the stale failure did not terminate the replacement loop.
        var heartbeatSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        script.Respond = (_, _) =>
        {
            heartbeatSent.TrySetResult();
            return Joined("member-2", 1, CreateAssignment(TestTopicId, 1));
        };
        using var loopStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);
        var loop = InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, loopStop.Token);
        try
        {
            await Task.WhenAny(heartbeatSent.Task, loop).WaitAsync(cancellationToken);
            await Assert.That(heartbeatSent.Task.IsCompleted).IsTrue();
            await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
            await Assert.That(coordinator.MemberId).IsEqualTo("member-2");
            await Assert.That(GetPrivateField<string?>(coordinator, "_lastHeartbeatFailure")).IsNull();
            await Assert.That(GetPrivateField<object?>(coordinator, "_heartbeatCallbackFailure")).IsNull();
        }
        finally
        {
            await loopStop.CancelAsync();
            await loop.WaitAsync(cancellationToken);
        }
    }

    [Test]
    [Timeout(15_000)]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConsumerProtocol_StopHeartbeat_BoundsUncooperativeCallbackDrain(
        bool cancelStop, CancellationToken cancellationToken)
    {
        var script = new HeartbeatScript(this);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsRevokedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask(BlockAsync(call.Arg<CancellationToken>())));
        await using var coordinator = await JoinAsync(script, listener);
        script.Respond = (_, _) => Joined("member-1", 6, CreateAssignment(TestTopicId, 0));
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);
        await (ValueTask)typeof(ConsumerCoordinator).GetMethod(
            "StartConsumerProtocolHeartbeatAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(coordinator, null)!;
        try
        {
            await entered.Task.WaitAsync(cancellationToken);
            var heartbeat = GetPrivateField<Task>(coordinator, "_heartbeatTask");
            using var stopCancellation = new CancellationTokenSource();
            var stop = coordinator.StopHeartbeatAsyncCore(stopCancellation.Token).AsTask();
            await cancelled.Task.WaitAsync(cancellationToken);
            await Assert.That(stop.IsCompleted).IsFalse();
            if (cancelStop)
                await stopCancellation.CancelAsync();
            await stop.WaitAsync(cancellationToken);
            await heartbeat.WaitAsync(cancellationToken);

            var callbacks = GetPrivateField<Task>(coordinator, "_heartbeatRebalanceCallbacks");
            await Assert.That(callbacks.IsCompleted).IsFalse();
            var listenerLock = GetPrivateField<SemaphoreSlim>(coordinator, "_rebalanceListenerLock");
            await Assert.That(listenerLock.CurrentCount).IsEqualTo(0);

            release.TrySetResult();
            await callbacks.WaitAsync(cancellationToken);
            await Assert.That(listenerLock.CurrentCount).IsEqualTo(1);

            // The interrupted drain resumes and another reconciliation can acquire the lock.
            await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, cancellationToken);
            script.Respond = (_, _) => Joined("member-1", 7, CreateAssignment(TestTopicId, 1));
            await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator).AsTask().WaitAsync(cancellationToken);
            await Assert.That(coordinator.Assignment).IsEquivalentTo([new TopicPartition("test-topic", 1)]);
            await Assert.That(GetPrivateField<object?>(coordinator, "_heartbeatCallbackFailure")).IsNull();
        }
        finally
        {
            release.TrySetResult();
            await coordinator.StopHeartbeatAsync();
            await GetPrivateField<Task>(coordinator, "_heartbeatRebalanceCallbacks").WaitAsync(cancellationToken);
        }

        async Task BlockAsync(CancellationToken token)
        {
            using var registration = token.Register(() => cancelled.TrySetResult());
            entered.TrySetResult();
            await release.Task;
        }
    }

    [Test]
    [Timeout(10_000)]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    public async Task ConsumerProtocol_BlockedRevocation_KeepsHeartbeatingWithPreviousOwnership(
        bool synchronous, bool newerTarget, CancellationToken cancellationToken)
    {
        SetupFindCoordinator();
        var telemetry = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Consumer);
        var rebalancePrefix = StandardClientTelemetryMetrics.RebalancePrefix;
        telemetry.Subscribe([rebalancePrefix]);
        var revoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var synchronousRelease = new ManualResetEventSlim();
        var duringCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = new ConcurrentQueue<ConsumerGroupHeartbeatRequest>();
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsRevokedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                revoked.TrySetResult();
                if (synchronous)
                {
                    synchronousRelease.Wait(cancellationToken);
                    return ValueTask.CompletedTask;
                }
                return new ValueTask(release.Task);
            });
        var count = 0;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<ConsumerGroupHeartbeatRequest>();
                var current = Interlocked.Increment(ref count);
                requests.Enqueue(request);
                if (current >= 5 && !release.Task.IsCompleted)
                    duringCallback.TrySetResult();
                if (release.Task.IsCompleted && request.TopicPartitions is [{ } topic] &&
                    topic.Partitions.Count == 1 && topic.Partitions[0] == (newerTarget ? 1 : 0))
                    acknowledged.TrySetResult();
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = current == 1 ? 1 : 2,
                    HeartbeatIntervalMs = 10,
                    // Only the first response carrying the change includes its assignment.
                    Assignment = current == 1 ? CreateAssignment(TestTopicId, 0, 1)
                        : current == 2 ? CreateAssignment(TestTopicId, 0)
                        : current == 3 && newerTarget ? CreateAssignment(TestTopicId, 1) : null
                });
            });
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(rebalanceListener: listener), _connectionPool, _metadataManager)
        {
            TelemetryMetricCollector = telemetry
        };
        try
        {
            await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, cancellationToken);
            await revoked.Task.WaitAsync(cancellationToken);
            await duringCallback.Task.WaitAsync(cancellationToken);
            foreach (var request in requests.ToArray().Skip(2))
            {
                await Assert.That(request.MemberEpoch).IsEqualTo(2);
                // null also means unchanged: the last explicit set must still own both partitions.
                if (request.TopicPartitions is { } owned)
                    await Assert.That(owned.Single().Partitions).IsEquivalentTo([0, 1]);
            }
            release.TrySetResult();
            synchronousRelease.Set();
            await acknowledged.Task.WaitAsync(cancellationToken);
            await Assert.That(coordinator.Assignment).IsEquivalentTo([new TopicPartition("test-topic", newerTarget ? 1 : 0)]);
            var metrics = telemetry.Collect(new ClientTelemetrySubscription(
                Guid.NewGuid(), 1, 0, 1000, 10000, false, [rebalancePrefix])).Metrics;
            var average = metrics.Single(metric => metric.Name == rebalancePrefix + "avg").Value;
            var total = metrics.Single(metric => metric.Name == rebalancePrefix + "total").Value;
            await Assert.That(average).IsGreaterThan(0d);
            // Initial join, first revocation, and (when present) the deferred target each count once.
            await Assert.That(Math.Round(total / average)).IsEqualTo(newerTarget ? 3d : 2d);
            await listener.Received(newerTarget ? 2 : 1).OnPartitionsRevokedAsync(
                Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            release.TrySetResult();
            synchronousRelease.Set();
        }
    }

    [Test]
    [Timeout(10_000)]
    public async Task ConsumerProtocol_BlockedInitialAssignment_KeepsHeartbeating(
        CancellationToken cancellationToken)
    {
        SetupFindCoordinator();
        var assigned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var heartbeatDuringAssignment = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsAssignedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                assigned.TrySetResult();
                return new ValueTask(release.Task);
            });
        var count = 0;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref count) >= 4)
                    heartbeatDuringAssignment.TrySetResult();
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = 1,
                    HeartbeatIntervalMs = 10,
                    Assignment = CreateAssignment(TestTopicId, 0)
                });
            });
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(rebalanceListener: listener), _connectionPool, _metadataManager);
        var join = coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, cancellationToken).AsTask();
        try
        {
            await assigned.Task.WaitAsync(cancellationToken);
            await heartbeatDuringAssignment.Task.WaitAsync(cancellationToken);
            await Assert.That(join.IsCompleted).IsFalse();
        }
        finally
        {
            release.TrySetResult();
            await join;
        }
    }
}
