using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using Microsoft.Extensions.Logging;
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

    [Test]
    public async Task CloseAsync_CancelledWhileLeaveIsGettingItsConnection_StillSendsLeave()
    {
        // The cancellation lands after the leave started but before its request was written.
        using var closeCancellation = new CancellationTokenSource();
        var harness = new Harness(defaultApiTimeoutMs: 60_000)
        {
            OnGetConnection = closeCancellation.Cancel
        };
        await using var consumer = harness.CreateConsumer();
        Harness.JoinGroup(consumer);

        var stopwatch = Stopwatch.StartNew();
        await Assert.That(async () => await consumer.CloseAsync(closeCancellation.Token))
            .Throws<OperationCanceledException>();
        stopwatch.Stop();

        await Assert.That(harness.LeaveEpochs.ToArray()).IsEquivalentTo([-1]);
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
    }

    [Test]
    [Timeout(30_000)]
    public async Task CloseAsync_CancelledWhileDeliveringAQueuedCallback_StillSendsLeave(
        CancellationToken cancellationToken)
    {
        // A lost callback is queued when close starts, and its listener runs until its token is
        // cancelled. Close is cancelled while step 2 delivers it.
        using var closeCancellation = new CancellationTokenSource();
        var harness = new Harness(defaultApiTimeoutMs: 60_000);
        await using var consumer = harness.CreateConsumer();
        Harness.JoinGroup(consumer);
        var coordinator = Harness.KnowCoordinator(consumer);
        var listener = new BlockingLostListener();
        using var registration = coordinator.RegisterRuntimeRebalanceListener(listener);
        Harness.QueueLostCallback(coordinator, new TopicPartition(Topic, 0));

        var stopwatch = Stopwatch.StartNew();
        var close = consumer.CloseAsync(closeCancellation.Token).AsTask();
        await listener.FirstCallStarted.WaitAsync(cancellationToken);
        await closeCancellation.CancelAsync();

        await Assert.That(async () => await close).Throws<OperationCanceledException>();
        stopwatch.Stop();

        // The leave went out straight away instead of the callback being delivered again under
        // the leave's grace, using it up and leaving no time to send the leave.
        await Assert.That(harness.LeaveEpochs.ToArray()).IsEquivalentTo([-1]);
        await Assert.That(listener.Calls).IsEqualTo(1);
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(4));
    }

    [Test]
    [Timeout(30_000)]
    public async Task CloseAsync_CancelledWhileDeliveringAQueuedCallback_WithoutMemberId_TakesNoLeaveGrace(
        CancellationToken cancellationToken)
    {
        // A fenced member lost its member id, so no leave can be sent, and its lost callback is
        // queued. Its listener runs until its token is cancelled, and close is cancelled while
        // step 2 delivers it.
        using var closeCancellation = new CancellationTokenSource();
        var harness = new Harness(defaultApiTimeoutMs: 60_000);
        await using var consumer = harness.CreateConsumer();
        var coordinator = Harness.KnowCoordinator(consumer);
        var listener = new BlockingLostListener();
        using var registration = coordinator.RegisterRuntimeRebalanceListener(listener);
        Harness.QueueLostCallback(coordinator, new TopicPartition(Topic, 0));

        var stopwatch = Stopwatch.StartNew();
        var close = consumer.CloseAsync(closeCancellation.Token).AsTask();
        await listener.FirstCallStarted.WaitAsync(cancellationToken);
        await closeCancellation.CancelAsync();

        await Assert.That(async () => await close).Throws<OperationCanceledException>();
        stopwatch.Stop();

        // The leave's grace is kept for sending a leave. With none to send, the callback is not
        // delivered again under it, which would hold close up to the whole grace after it was
        // cancelled; it stays queued for disposal.
        await Assert.That(harness.LeaveEpochs).IsEmpty();
        await Assert.That(listener.Calls).IsEqualTo(1);
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(4));
    }

    [Test]
    [Timeout(30_000)]
    public async Task CloseAsync_CancelledWhileWaitingForLeaderRefresh_StillSendsLeave(
        CancellationToken cancellationToken)
    {
        using var closeCancellation = new CancellationTokenSource();
        var harness = new Harness(defaultApiTimeoutMs: 60_000);
        await using var consumer = harness.CreateConsumer();
        Harness.JoinGroup(consumer);
        // A leader refresh that never finishes, so close waits in step 3.
        var refresh = new TaskCompletionSource();
        var refreshTasks = (ConcurrentDictionary<string, Task>)typeof(KafkaConsumer<string, string>)
            .GetField("_pendingLeaderRefreshTasks", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(consumer)!;
        refreshTasks[Topic] = refresh.Task;

        var close = consumer.CloseAsync(closeCancellation.Token).AsTask();
        await Task.Delay(200, cancellationToken);
        await closeCancellation.CancelAsync();

        await Assert.That(async () => await close).Throws<OperationCanceledException>();
        await Assert.That(harness.LeaveEpochs.ToArray()).IsEquivalentTo([-1]);
        refresh.TrySetResult();
    }

    [Test]
    public async Task CloseAsync_MemberThatNeverJoined_CommitIsNotShortenedForALeave()
    {
        // A group consumer with manual assignment has no membership, so no leave is sent and the
        // commit keeps the whole budget.
        var harness = new Harness(defaultApiTimeoutMs: 2_000);
        harness.OnOffsetCommit = static async (request, token) =>
        {
            await Task.Delay(1_500, token);
            return CreateCommitResponse(request, ErrorCode.None);
        };
        await using var consumer = harness.CreateConsumer();
        Harness.KnowCoordinator(consumer);
        consumer.Assign(new TopicPartition(Topic, 0));
        consumer.StoreOffset(CreateConsumeResult(offset: 41));

        await consumer.CloseAsync(CancellationToken.None);

        await Assert.That(harness.OffsetCommits).IsEqualTo(1);
        await Assert.That(harness.CompletedOffsetCommits).IsEqualTo(1);
        await Assert.That(harness.LeaveEpochs).IsEmpty();
    }

    [Test]
    [Timeout(30_000)]
    public async Task CloseAsync_CancelledAfterTheLeaveWasWritten_StopsWaitingForTheResponse(
        CancellationToken cancellationToken)
    {
        // A real connection, so the leave takes the path where close's token only bounds the
        // wait for the response once the frame is written.
        await using var broker = await LoopbackCoordinator.StartAsync(cancellationToken);
        using var closeCancellation = new CancellationTokenSource();
        var logs = new CapturingLoggerFactory();
        var harness = new Harness(defaultApiTimeoutMs: 60_000)
        {
            Connection = broker.Connection,
            LoggerFactory = logs
        };
        await using var consumer = harness.CreateConsumer();
        Harness.JoinGroup(consumer);

        var close = consumer.CloseAsync(closeCancellation.Token).AsTask();

        // The coordinator received the whole leave and never answers it.
        var leave = await broker.ReadRequestAsync(cancellationToken);
        await Assert.That(leave).IsEqualTo(ApiKey.ConsumerGroupHeartbeat);
        var stopwatch = Stopwatch.StartNew();
        await closeCancellation.CancelAsync();

        await Assert.That(async () => await close).Throws<OperationCanceledException>();
        stopwatch.Stop();

        // Close stopped waiting for the response instead of holding on for the leave's 5 s
        // grace, and reported the leave as sent rather than failed.
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(5));
        await Assert.That(logs.Contains("stopped waiting for its response")).IsTrue();
        await Assert.That(logs.Contains("Failed to send LeaveGroup request")).IsFalse();
        await Assert.That(broker.PendingRequestCount).IsEqualTo(0);
    }

    [Test]
    [Timeout(30_000)]
    public async Task CloseAsync_CancelledWhileTheLeaveIsBeingWritten_WaitsForTheWriteWithinTheGrace(
        CancellationToken cancellationToken)
    {
        await using var broker = await LoopbackCoordinator.StartAsync(cancellationToken);
        var stalledWrite = broker.StallNextWrite();
        using var closeCancellation = new CancellationTokenSource();
        var logs = new CapturingLoggerFactory();
        var harness = new Harness(defaultApiTimeoutMs: 60_000)
        {
            Connection = broker.Connection,
            LoggerFactory = logs
        };
        await using var consumer = harness.CreateConsumer();
        Harness.JoinGroup(consumer);

        var close = consumer.CloseAsync(closeCancellation.Token).AsTask();

        // The leave's frame write has started and is held in the socket write when close's
        // deadline passes.
        await stalledWrite.Entered.WaitAsync(cancellationToken);
        await closeCancellation.CancelAsync();

        // The write keeps the leave's own token (with its grace), so close is still waiting on it
        // rather than abandoning the frame.
        await Assert.That(close.IsCompleted).IsFalse();

        stalledWrite.Release();
        var leave = await broker.ReadRequestAsync(cancellationToken);
        await Assert.That(leave).IsEqualTo(ApiKey.ConsumerGroupHeartbeat);

        await Assert.That(async () => await close).Throws<OperationCanceledException>();
        await Assert.That(logs.Contains("stopped waiting for its response")).IsTrue();
        await Assert.That(logs.Contains("Failed to send LeaveGroup request")).IsFalse();
        await Assert.That(broker.PendingRequestCount).IsEqualTo(0);
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

        /// <summary>Runs whenever the consumer gets a connection to the coordinator.</summary>
        public Action? OnGetConnection { get; init; }

        /// <summary>The coordinator connection; a substitute when not set.</summary>
        public IKafkaConnection? Connection { get; init; }

        public ILoggerFactory? LoggerFactory { get; init; }

        public int OffsetCommits => Volatile.Read(ref _offsetCommits);

        public int CompletedOffsetCommits => Volatile.Read(ref _completedOffsetCommits);

        public KafkaConsumer<string, string> CreateConsumer()
        {
            var connectionPool = Substitute.For<IConnectionPool>();
            var connection = Connection ?? Substitute.For<IKafkaConnection>();
            connectionPool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    OnGetConnection?.Invoke();
                    return ValueTask.FromResult(connection);
                });

            // A real connection answers for itself.
            if (Connection is null)
            {
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
                        // A cancelled request never reaches the wire.
                        if (call.Arg<CancellationToken>().IsCancellationRequested)
                            return ValueTask.FromCanceled<ConsumerGroupHeartbeatResponse>(call.Arg<CancellationToken>());
                        LeaveEpochs.Enqueue(call.Arg<ConsumerGroupHeartbeatRequest>().MemberEpoch);
                        return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                        {
                            ErrorCode = ErrorCode.None,
                            MemberId = "member-1",
                            MemberEpoch = -1,
                            HeartbeatIntervalMs = 60_000
                        });
                    });
            }

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
                metadataManager,
                LoggerFactory);
        }

        /// <summary>Makes the coordinator a joined member of coordinator 0, so close sends a leave.</summary>
        public static void JoinGroup(KafkaConsumer<string, string> consumer)
        {
            var coordinator = KnowCoordinator(consumer);
            typeof(ConsumerCoordinator)
                .GetField("_memberId", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(coordinator, "member-1");
        }

        /// <summary>Makes coordinator 0 known without the consumer joining the group.</summary>
        public static ConsumerCoordinator KnowCoordinator(KafkaConsumer<string, string> consumer)
        {
            var coordinator = (ConsumerCoordinator)typeof(KafkaConsumer<string, string>)
                .GetField("_coordinator", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(consumer)!;
            typeof(ConsumerCoordinator)
                .GetField("_coordinatorId", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(coordinator, 0);
            return coordinator;
        }

        /// <summary>Queues an OnPartitionsLost notification, as a fence the heartbeat saw would.</summary>
        public static void QueueLostCallback(ConsumerCoordinator coordinator, TopicPartition partition)
        {
            var pendingType = typeof(ConsumerCoordinator).GetNestedType("PendingRebalanceCallback", BindingFlags.NonPublic)!;
            var pending = Activator.CreateInstance(pendingType, nonPublic: true)!;
            pendingType.GetProperty("Lost")!.SetValue(pending, new[] { partition });
            pendingType.GetProperty("Assignment")!.SetValue(pending, new HashSet<TopicPartition> { partition });
            typeof(ConsumerCoordinator)
                .GetMethod("EnqueuePendingRebalanceCallback", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(coordinator, [pending, false]);
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

    /// <summary>
    /// A coordinator on a loopback socket: it completes the connection handshake, then only reads
    /// requests and never answers them.
    /// </summary>
    private sealed class LoopbackCoordinator : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly TcpClient _client;

        private LoopbackCoordinator(TcpListener listener, TcpClient client, KafkaConnection connection)
        {
            _listener = listener;
            _client = client;
            Connection = connection;
        }

        public KafkaConnection Connection { get; }

        public int PendingRequestCount => (int)typeof(KafkaConnection)
            .GetField("_pendingRequestCount", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(Connection)!;

        public static async Task<LoopbackCoordinator> StartAsync(CancellationToken cancellationToken)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            KafkaConnection? connection = null;
            try
            {
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var accept = AcceptAndCompleteHandshakeAsync(listener, cancellationToken);
                connection = new KafkaConnection(0, IPAddress.Loopback.ToString(), port);
                await connection.ConnectAsync(cancellationToken);
                return new LoopbackCoordinator(listener, await accept, connection);
            }
            catch
            {
                if (connection is not null)
                    await connection.DisposeAsync();
                listener.Stop();
                throw;
            }
        }

        /// <summary>Holds the connection's next frame write once it has started.</summary>
        public StalledWrite StallNextWrite()
        {
            var field = typeof(KafkaConnection).GetField("_stream", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var stream = new StallingWriteStream((Stream)field.GetValue(Connection)!);
            field.SetValue(Connection, stream);
            return stream.Stall;
        }

        /// <summary>Reads one whole request frame and returns its API key.</summary>
        public async Task<ApiKey> ReadRequestAsync(CancellationToken cancellationToken)
        {
            var frame = await ReadFrameAsync(_client.GetStream(), cancellationToken);
            return (ApiKey)BinaryPrimitives.ReadInt16BigEndian(frame);
        }

        public async ValueTask DisposeAsync()
        {
            await Connection.DisposeAsync();
            _client.Dispose();
            _listener.Stop();
        }

        private static async Task<TcpClient> AcceptAndCompleteHandshakeAsync(
            TcpListener listener,
            CancellationToken cancellationToken)
        {
            var client = await listener.AcceptTcpClientAsync(cancellationToken);
            try
            {
                var stream = client.GetStream();
                var request = await ReadFrameAsync(stream, cancellationToken);
                if ((ApiKey)BinaryPrimitives.ReadInt16BigEndian(request) != ApiKey.ApiVersions)
                    throw new InvalidOperationException("Expected the ApiVersions handshake");
                await stream.WriteAsync(
                    BuildApiVersionsResponseFrame(BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4))),
                    cancellationToken);
                return client;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        private static async Task<byte[]> ReadFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var lengthBuffer = new byte[4];
            await stream.ReadExactlyAsync(lengthBuffer, cancellationToken);
            var frame = new byte[BinaryPrimitives.ReadInt32BigEndian(lengthBuffer)];
            await stream.ReadExactlyAsync(frame, cancellationToken);
            return frame;
        }

        // ApiVersions v3 response advertising ApiVersions v0-3 and ConsumerGroupHeartbeat v0.
        private static byte[] BuildApiVersionsResponseFrame(int correlationId)
        {
            var body = new ArrayBufferWriter<byte>();
            var writer = new KafkaProtocolWriter(body);
            writer.WriteInt16(0);
            writer.WriteUnsignedVarInt(3);
            writer.WriteInt16((short)ApiKey.ApiVersions);
            writer.WriteInt16(0);
            writer.WriteInt16(3);
            writer.WriteEmptyTaggedFields();
            writer.WriteInt16((short)ApiKey.ConsumerGroupHeartbeat);
            writer.WriteInt16(0);
            writer.WriteInt16(0);
            writer.WriteEmptyTaggedFields();
            writer.WriteInt32(0);
            writer.WriteEmptyTaggedFields();

            var frame = new byte[8 + body.WrittenCount];
            BinaryPrimitives.WriteInt32BigEndian(frame, frame.Length - 4);
            BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(4), correlationId);
            body.WrittenSpan.CopyTo(frame.AsSpan(8));
            return frame;
        }
    }

    private sealed class StalledWrite
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _claimed;

        public Task Entered => _entered.Task;

        public void Release() => _released.TrySetResult();

        /// <summary>The gate the first write waits on; null for every later write.</summary>
        public Task? Claim()
        {
            if (Interlocked.Exchange(ref _claimed, 1) != 0)
                return null;
            _entered.TrySetResult();
            return _released.Task;
        }
    }

    /// <summary>Passes writes through to the socket stream, holding the first one until released.</summary>
    private sealed class StallingWriteStream(Stream inner) : Stream
    {
        public StalledWrite Stall { get; } = new();

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Stall.Claim() is { } gate)
                await gate.WaitAsync(cancellationToken);
            await inner.WriteAsync(buffer, cancellationToken);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);

        public override void Flush() => inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>An OnPartitionsLost that runs until its token is cancelled.</summary>
    private sealed class BlockingLostListener : IRebalanceListener
    {
        private readonly TaskCompletionSource _firstCallStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;

        public Task FirstCallStarted => _firstCallStarted.Task;

        public int Calls => Volatile.Read(ref _calls);

        public ValueTask OnPartitionsAssignedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask OnPartitionsRevokedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public async ValueTask OnPartitionsLostAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            _firstCallStarted.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public bool Contains(string text)
        {
            foreach (var message in _messages)
            {
                if (message.Contains(text, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public ILogger CreateLogger(string categoryName) => new Logger(_messages);

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }

        private sealed class Logger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
        }
    }
}
