using System.Collections.Concurrent;
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
