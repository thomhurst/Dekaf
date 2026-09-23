using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Close must not let the final offset commit starve the LeaveGroup request (#3387): a
/// coordinator that answers nothing, or a cancellation while the commit backs off, still has the
/// member leave the group and the rest of close run.
/// </summary>
public sealed class ConsumerCloseLeaveBudgetTests
{
    private const string Topic = "topic-a";

    [Test]
    public async Task CloseAsync_BlackHoledCommit_StillSendsLeaveWithinTheCloseBudget()
    {
        const int closeBudgetMs = 2_000;
        var harness = new Harness(defaultApiTimeoutMs: closeBudgetMs);
        // The coordinator swallows the commit: no response ever comes, only the token ends it.
        harness.OnOffsetCommit = static async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new UnreachableException();
        };
        await using var consumer = harness.CreateConsumer();
        Harness.JoinGroup(consumer);
        consumer.StoreOffset(CreateConsumeResult(offset: 41));

        var stopwatch = Stopwatch.StartNew();
        await consumer.CloseAsync(CancellationToken.None);
        stopwatch.Stop();

        await Assert.That(harness.OffsetCommits).IsEqualTo(1);
        await Assert.That(harness.LeaveEpochs.ToArray()).IsEquivalentTo([-1]);
        // The commit gave up with the leave's reserve (half of a 2 s budget) still left, and the
        // answering leave then finished well inside the budget, so close did not time out.
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromMilliseconds(closeBudgetMs));
    }

    [Test]
    public async Task CloseAsync_CancelledDuringCommitBackoff_StillLeavesAndRunsCleanup()
    {
        using var closeCancellation = new CancellationTokenSource();
        var commitFailed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var harness = new Harness(defaultApiTimeoutMs: 60_000, retryBackoffMs: 30_000);
        harness.OnOffsetCommit = (request, _) =>
        {
            commitFailed.TrySetResult();
            return ValueTask.FromResult(CreateCommitResponse(request, ErrorCode.InvalidCommitOffsetSize));
        };
        await using var consumer = harness.CreateConsumer();
        Harness.JoinGroup(consumer);
        var partition = new TopicPartition(Topic, 0);
        consumer.Assign(partition);
        consumer.StoreOffset(CreateConsumeResult(offset: 41));

        var stopwatch = Stopwatch.StartNew();
        var close = consumer.CloseAsync(closeCancellation.Token).AsTask();

        // The first attempt failed, so close is now in its 30 s backoff before the second one.
        await commitFailed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await Task.Delay(100);
        closeCancellation.Cancel();

        await Assert.That(async () => await close).Throws<OperationCanceledException>();
        stopwatch.Stop();

        // No second attempt, the leave was still sent, and the cleanup steps after it ran.
        await Assert.That(harness.OffsetCommits).IsEqualTo(1);
        await Assert.That(harness.LeaveEpochs.ToArray()).IsEquivalentTo([-1]);
        await Assert.That(consumer.Assignment).IsEmpty();
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task CloseAsync_RemainInGroup_CommitIsNotShortenedForALeave()
    {
        // Without a leave to reserve time for, a slow commit may use the whole budget.
        var harness = new Harness(defaultApiTimeoutMs: 2_000);
        harness.OnOffsetCommit = static async (request, token) =>
        {
            await Task.Delay(1_500, token);
            return CreateCommitResponse(request, ErrorCode.None);
        };
        await using var consumer = harness.CreateConsumer();
        Harness.JoinGroup(consumer);
        consumer.StoreOffset(CreateConsumeResult(offset: 41));

        await consumer.CloseAsync(
            new ConsumerCloseOptions { GroupMembershipOperation = ConsumerGroupMembershipOperation.RemainInGroup },
            CancellationToken.None);

        await Assert.That(harness.OffsetCommits).IsEqualTo(1);
        await Assert.That(harness.CompletedOffsetCommits).IsEqualTo(1);
        await Assert.That(harness.LeaveEpochs).IsEmpty();
    }

    private static ConsumeResult<string, string> CreateConsumeResult(long offset) =>
        new(
            topic: Topic,
            partition: 0,
            offset: offset,
            keyData: ReadOnlyMemory<byte>.Empty,
            isKeyNull: true,
            valueData: ReadOnlyMemory<byte>.Empty,
            isValueNull: true,
            headers: null,
            timestampMs: 0,
            timestampType: TimestampType.NotAvailable,
            leaderEpoch: 7,
            keyDeserializer: null,
            valueDeserializer: null);

    private static OffsetCommitResponse CreateCommitResponse(OffsetCommitRequest request, ErrorCode error) =>
        new()
        {
            Topics = request.Topics
                .Select(topic => new OffsetCommitResponseTopic
                {
                    Name = topic.Name,
                    Partitions = topic.Partitions
                        .Select(partition => new OffsetCommitResponsePartition
                        {
                            PartitionIndex = partition.PartitionIndex,
                            ErrorCode = error
                        })
                        .ToArray()
                })
                .ToArray()
        };

    private sealed class Harness(int defaultApiTimeoutMs, int retryBackoffMs = 100)
    {
        private int _offsetCommits;
        private int _completedOffsetCommits;

        public Func<OffsetCommitRequest, CancellationToken, ValueTask<OffsetCommitResponse>>? OnOffsetCommit { get; set; }

        public ConcurrentQueue<int> LeaveEpochs { get; } = new();

        public int OffsetCommits => Volatile.Read(ref _offsetCommits);

        public int CompletedOffsetCommits => Volatile.Read(ref _completedOffsetCommits);

        public KafkaConsumer<string, string> CreateConsumer()
        {
            var connectionPool = Substitute.For<IConnectionPool>();
            var connection = Substitute.For<IKafkaConnection>();
            connectionPool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(connection));

            connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                    Arg.Any<FindCoordinatorRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(new FindCoordinatorResponse
                {
                    Coordinators =
                    [
                        new Coordinator { Key = "group-a", NodeId = 0, Host = "localhost", Port = 9092, ErrorCode = ErrorCode.None }
                    ]
                }));

            connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                    Arg.Any<OffsetCommitRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    Interlocked.Increment(ref _offsetCommits);
                    return CompleteCommitAsync(call.Arg<OffsetCommitRequest>(), call.Arg<CancellationToken>());
                });

            // Only the leave sends a heartbeat here: the member joined through JoinGroup, so no
            // heartbeat loop runs.
            connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                    Arg.Any<ConsumerGroupHeartbeatRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    LeaveEpochs.Enqueue(call.Arg<ConsumerGroupHeartbeatRequest>().MemberEpoch);
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.None,
                        MemberId = "member-1",
                        MemberEpoch = -1,
                        HeartbeatIntervalMs = 60_000
                    });
                });

            var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
            metadataManager.Metadata.Update(new MetadataResponse
            {
                Brokers = [new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }],
                Topics = []
            });
            metadataManager.SetApiVersion(
                ApiKey.FindCoordinator,
                FindCoordinatorRequest.LowestSupportedVersion,
                FindCoordinatorRequest.HighestSupportedVersion);
            metadataManager.SetApiVersion(ApiKey.OffsetCommit, OffsetCommitRequest.LowestSupportedVersion, 9);
            metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 0);

            return new KafkaConsumer<string, string>(
                new ConsumerOptions
                {
                    BootstrapServers = ["localhost:9092"],
                    GroupId = "group-a",
                    OffsetCommitMode = OffsetCommitMode.Auto,
                    EnableAutoOffsetStore = false,
                    DefaultApiTimeoutMs = defaultApiTimeoutMs,
                    RetryBackoffMs = retryBackoffMs,
                    RetryBackoffMaxMs = retryBackoffMs
                },
                Serializers.String,
                Serializers.String,
                connectionPool,
                metadataManager);
        }

        /// <summary>Makes the coordinator a joined member of coordinator 0, so close sends a leave.</summary>
        public static void JoinGroup(KafkaConsumer<string, string> consumer)
        {
            var coordinator = (ConsumerCoordinator)typeof(KafkaConsumer<string, string>)
                .GetField("_coordinator", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(consumer)!;
            typeof(ConsumerCoordinator)
                .GetField("_memberId", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(coordinator, "member-1");
            typeof(ConsumerCoordinator)
                .GetField("_coordinatorId", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(coordinator, 0);
        }

        private async ValueTask<OffsetCommitResponse> CompleteCommitAsync(
            OffsetCommitRequest request,
            CancellationToken cancellationToken)
        {
            var response = OnOffsetCommit is null
                ? CreateCommitResponse(request, ErrorCode.None)
                : await OnOffsetCommit(request, cancellationToken);
            Interlocked.Increment(ref _completedOffsetCommits);
            return response;
        }
    }
}
