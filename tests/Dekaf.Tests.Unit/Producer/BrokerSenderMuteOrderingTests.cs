using System.Buffers;
using Dekaf.Compression;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;

using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for the BrokerSender mute/coalesce/carry-over state machine.
/// Verifies per-partition ordering guarantees when retriable errors cause retries:
/// - Muted partitions block normal batches until retry completes
/// - Unmuted partitions proceed independently
/// - FinalizeCoalescedRetries clears mute state only when batch actually sends
/// - Carry-over ordering preserves retry-before-normal invariant
/// </summary>
public sealed class BrokerSenderMuteOrderingTests
{
    private static ProducerOptions CreateOptions(Acks acks = Acks.All, int maxInFlight = 1,
        int retryBackoffMs = 0, int retryBackoffMaxMs = 0,
        int deliveryTimeoutMs = 30_000, int requestTimeoutMs = 30_000) => new()
    {
        BootstrapServers = ["localhost:9092"],
        MaxInFlightRequestsPerConnection = maxInFlight,
        Acks = acks,
        DeliveryTimeoutMs = deliveryTimeoutMs,
        RetryBackoffMs = retryBackoffMs,
        RetryBackoffMaxMs = retryBackoffMaxMs,
        RequestTimeoutMs = requestTimeoutMs,
        LingerMs = 0
    };

    private static (IConnectionPool pool, IKafkaConnection connection) CreateMockConnection(
        Queue<TaskCompletionSource<ProduceResponse>> responseQueue,
        Action? onSend = null)
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.IsConnected.Returns(true);
        connection.BrokerId.Returns(1);

        connection.SendPipelinedWithCallerTimeoutAsync<ProduceRequest, ProduceResponse>(
                Arg.Any<ProduceRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var task = responseQueue.Dequeue().Task;
                onSend?.Invoke();
                return task;
            });

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);

        return (pool, connection);
    }

    private static ReadyBatch CreateTestBatch(
        ValueTaskSourcePool<RecordMetadata> pool,
        string topic, int partition, int messageCount = 1)
    {
        var batch = new ReadyBatch();
        var sources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(messageCount);
        for (var i = 0; i < messageCount; i++)
            sources[i] = pool.Rent();

        batch.Initialize(
            new TopicPartition(topic, partition),
            new RecordBatch { Records = Array.Empty<Record>() },
            sources,
            messageCount,
            pooledDataArrays: null,
            pooledDataArraysCount: 0,
            pooledHeaderArrays: null,
            pooledHeaderArraysCount: 0,
            dataSize: 100);

        batch.TrySetMemoryReleased();
        return batch;
    }

    private static ProduceResponse CreateSuccessResponse(string topic, int partition, long baseOffset) =>
        new()
        {
            Responses =
            [
                new ProduceResponseTopicData
                {
                    Name = topic,
                    PartitionResponses =
                    [
                        new ProduceResponsePartitionData
                        {
                            Index = partition,
                            ErrorCode = ErrorCode.None,
                            BaseOffset = baseOffset
                        }
                    ]
                }
            ]
        };

    private static ProduceResponse CreateRetriableErrorResponse(string topic, int partition) =>
        new()
        {
            Responses =
            [
                new ProduceResponseTopicData
                {
                    Name = topic,
                    PartitionResponses =
                    [
                        new ProduceResponsePartitionData
                        {
                            Index = partition,
                            ErrorCode = ErrorCode.NotLeaderOrFollower,
                            BaseOffset = -1
                        }
                    ]
                }
            ]
        };

    private static ProduceResponse CreateMultiPartitionResponse(
        string topic, params (int partition, ErrorCode errorCode, long baseOffset)[] partitions) =>
        new()
        {
            Responses =
            [
                new ProduceResponseTopicData
                {
                    Name = topic,
                    PartitionResponses = partitions
                        .Select(p => new ProduceResponsePartitionData
                        {
                            Index = p.partition,
                            ErrorCode = p.errorCode,
                            BaseOffset = p.baseOffset
                        }).ToArray()
                }
            ]
        };

    private static BrokerSender CreateSender(
        IConnectionPool pool,
        ProducerOptions options,
        RecordAccumulator accumulator,
        Action<TopicPartition, long, DateTimeOffset, int, Exception?> onAcknowledgement) =>
        new(
            brokerId: 1, pool,
            new MetadataManager(pool, options.BootstrapServers),
            accumulator, options,
            new CompressionCodecRegistry(),
            inflightTracker: new PartitionInflightTracker(),
            getProduceApiVersion: () => 9,
            setProduceApiVersion: _ => { },
            isTransactional: () => false,
            ensurePartitionInTransaction: null,
            bumpEpoch: null,
            getCurrentEpoch: null,
            rerouteBatch: null,
            onAcknowledgement: onAcknowledgement,
            logger: null);

    /// <summary>
    /// Core muting test: a retriable error on partition 0 should mute it, blocking
    /// subsequent normal batches for partition 0 until the retry succeeds.
    ///
    /// Flow: batch A (p0) → NotLeaderOrFollower → p0 muted → batch B (p0) enqueued
    /// but blocked → A retry succeeds → p0 unmuted → B proceeds.
    ///
    /// Verifies: batch A acknowledged before batch B (ordering preserved).
    /// </summary>
    [Test]
    [Timeout(60_000)]
    public async Task RetriableError_MutesPartition_BlocksNormalBatchUntilRetrySucceeds(CancellationToken ct)
    {
        // Send 1: batch A (p0) → retriable error
        // Send 2: batch A retry (p0) → success (batch B is blocked, not coalesced)
        // Send 3: batch B (p0) → success (unmuted after retry)
        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var tcs3 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2);
        responseQueue.Enqueue(tcs3);

        var sendCount = 0;
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource(), new TaskCompletionSource() };

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var idx = Interlocked.Increment(ref sendCount) - 1;
            if (idx < sendSignals.Length)
                sendSignals[idx].TrySetResult();
        });
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var ackOrder = new List<(int partition, long offset)>();
        var allAcknowledged = new TaskCompletionSource();

        var sender = CreateSender(pool, options, accumulator, (tp, offset, _, _, ex) =>
        {
            if (ex is null)
            {
                lock (ackOrder)
                {
                    ackOrder.Add((tp.Partition, offset));
                    if (ackOrder.Count >= 2)
                        allAcknowledged.TrySetResult();
                }
            }
        });

        try
        {
            // Enqueue batch A (p0)
            var batchA = CreateTestBatch(vtPool, "test-topic", 0);
            await sender.EnqueueAsync(batchA, CancellationToken.None);

            // Wait for send 1
            await sendSignals[0].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // Return retriable error → triggers HandleRetriableBatch → mutes p0
            tcs1.SetResult(CreateRetriableErrorResponse("test-topic", 0));

            // Wait for send 2 (retry of batch A) — do this BEFORE enqueuing B to ensure
            // the send loop has processed the retriable error and started the retry.
            // On slow CI runners, enqueuing B before the retry is processed can cause the
            // send loop to enter extra poll cycles (100ms each) while B sits muted in
            // carry-over, accumulating latency.
            await sendSignals[1].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // Now enqueue batch B (p0) — retry already in flight, p0 is muted by _muteOnSend
            var batchB = CreateTestBatch(vtPool, "test-topic", 0);
            await sender.EnqueueAsync(batchB, CancellationToken.None);

            // Complete retry → FinalizeCoalescedRetries unmutes p0
            tcs2.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));

            // Wait for send 3 (batch B, now that p0 is unmuted)
            await sendSignals[2].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // Complete batch B
            tcs3.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 101));

            await allAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // Verify ordering: A acknowledged before B
            await Assert.That(ackOrder).Count().IsEqualTo(2);
            await Assert.That(ackOrder[0].offset).IsEqualTo(100); // batch A
            await Assert.That(ackOrder[1].offset).IsEqualTo(101); // batch B
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(3);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    /// <summary>
    /// Partition independence test: a retriable error on partition 0 should NOT
    /// block batches for partition 1.
    ///
    /// Flow: batch A (p0) + batch B (p1) coalesced → p0 gets retriable error,
    /// p1 succeeds → batch C (p1) proceeds immediately, batch D (p0) blocked.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task RetriableError_OnOnePartition_DoesNotBlockOtherPartitions(CancellationToken ct)
    {
        // Send 1: batch A (p0) + batch B (p1) coalesced → p0 error, p1 success
        // Send 2: batch A retry (p0) + batch C (p1) coalesced → both succeed
        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2);

        var sendCount = 0;
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var idx = Interlocked.Increment(ref sendCount) - 1;
            if (idx < sendSignals.Length)
                sendSignals[idx].TrySetResult();
        });
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var ackPartitions = new List<(int partition, long offset)>();
        var allAcknowledged = new TaskCompletionSource();

        var sender = CreateSender(pool, options, accumulator, (tp, offset, _, _, ex) =>
        {
            if (ex is null)
            {
                lock (ackPartitions)
                {
                    ackPartitions.Add((tp.Partition, offset));
                    // batch B (p1) + batch A retry (p0) + batch C (p1) = 3 acks
                    if (ackPartitions.Count >= 3)
                        allAcknowledged.TrySetResult();
                }
            }
        });

        try
        {
            // Enqueue batch A (p0) and batch B (p1) — they'll coalesce
            var batchA = CreateTestBatch(vtPool, "test-topic", 0);
            var batchB = CreateTestBatch(vtPool, "test-topic", 1);
            await sender.EnqueueAsync(batchA, CancellationToken.None);
            await sender.EnqueueAsync(batchB, CancellationToken.None);

            // Wait for send 1 (coalesced A+B)
            await sendSignals[0].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // p0 gets retriable error, p1 succeeds
            tcs1.SetResult(CreateMultiPartitionResponse("test-topic",
                (0, ErrorCode.NotLeaderOrFollower, -1),
                (1, ErrorCode.None, 200)));

            // Enqueue batch C (p1) — should NOT be blocked (p1 is not muted)
            var batchC = CreateTestBatch(vtPool, "test-topic", 1);
            await sender.EnqueueAsync(batchC, CancellationToken.None);

            // Wait for send 2 (batch A retry + batch C coalesced — both partitions)
            await sendSignals[1].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // Both succeed
            tcs2.SetResult(CreateMultiPartitionResponse("test-topic",
                (0, ErrorCode.None, 100),
                (1, ErrorCode.None, 201)));

            await allAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // Verify p1 was acknowledged first (from send 1), then p0 retry + p1 from send 2
            await Assert.That(ackPartitions).Count().IsEqualTo(3);
            // First ack must be p1 (from the first response where p1 succeeded)
            await Assert.That(ackPartitions[0]).IsEqualTo((1, 200L));
            // Remaining: A retry (p0) and C (p1) from send 2
            var remaining = ackPartitions.Skip(1).OrderBy(a => a.partition).ToList();
            await Assert.That(remaining[0]).IsEqualTo((0, 100L));
            await Assert.That(remaining[1]).IsEqualTo((1, 201L));
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(2);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    /// <summary>
    /// Multiple partitions muted independently: retriable errors on p0 and p1
    /// should mute both, but p2 should still proceed.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task MultiplePartitionsMuted_IndependentlyBlocked_OtherPartitionsProceed(CancellationToken ct)
    {
        // Send 1: A(p0) + B(p1) + C(p2) → p0 error, p1 error, p2 success
        // Send 2: A retry(p0) + B retry(p1) → both succeed
        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2);

        var sendCount = 0;
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var idx = Interlocked.Increment(ref sendCount) - 1;
            if (idx < sendSignals.Length)
                sendSignals[idx].TrySetResult();
        });
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var ackPartitions = new List<(int partition, long offset)>();
        var allAcknowledged = new TaskCompletionSource();

        var sender = CreateSender(pool, options, accumulator, (tp, offset, _, _, ex) =>
        {
            if (ex is null)
            {
                lock (ackPartitions)
                {
                    ackPartitions.Add((tp.Partition, offset));
                    if (ackPartitions.Count >= 3)
                        allAcknowledged.TrySetResult();
                }
            }
        });

        try
        {
            var batchA = CreateTestBatch(vtPool, "test-topic", 0);
            var batchB = CreateTestBatch(vtPool, "test-topic", 1);
            var batchC = CreateTestBatch(vtPool, "test-topic", 2);
            await sender.EnqueueAsync(batchA, CancellationToken.None);
            await sender.EnqueueAsync(batchB, CancellationToken.None);
            await sender.EnqueueAsync(batchC, CancellationToken.None);

            await sendSignals[0].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // p0 and p1 fail, p2 succeeds
            tcs1.SetResult(CreateMultiPartitionResponse("test-topic",
                (0, ErrorCode.NotLeaderOrFollower, -1),
                (1, ErrorCode.NotLeaderOrFollower, -1),
                (2, ErrorCode.None, 300)));

            // Wait for retries of p0 and p1
            await sendSignals[1].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            tcs2.SetResult(CreateMultiPartitionResponse("test-topic",
                (0, ErrorCode.None, 100),
                (1, ErrorCode.None, 200)));

            await allAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // p2 acknowledged first (succeeded in send 1)
            await Assert.That(ackPartitions[0]).IsEqualTo((2, 300L));
            // p0 and p1 acknowledged after retry (order between them is implementation detail)
            var retryAcks = ackPartitions.Skip(1).OrderBy(a => a.partition).ToList();
            await Assert.That(retryAcks[0]).IsEqualTo((0, 100L));
            await Assert.That(retryAcks[1]).IsEqualTo((1, 200L));
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    /// <summary>
    /// Connection failure (faulted response) triggers retry with muting, just like
    /// a retriable error code. Verifies that IOException-style failures also mute
    /// the partition and preserve ordering.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task FaultedConnection_MutesPartition_RetryPreservesOrdering(CancellationToken ct)
    {
        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var tcs3 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2);
        responseQueue.Enqueue(tcs3);

        var sendCount = 0;
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource(), new TaskCompletionSource() };

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var idx = Interlocked.Increment(ref sendCount) - 1;
            if (idx < sendSignals.Length)
                sendSignals[idx].TrySetResult();
        });
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var ackOffsets = new List<long>();
        var allAcknowledged = new TaskCompletionSource();

        var sender = CreateSender(pool, options, accumulator, (_, offset, _, _, ex) =>
        {
            if (ex is null)
            {
                lock (ackOffsets)
                {
                    ackOffsets.Add(offset);
                    if (ackOffsets.Count >= 2)
                        allAcknowledged.TrySetResult();
                }
            }
        });

        try
        {
            var batchA = CreateTestBatch(vtPool, "test-topic", 0);
            await sender.EnqueueAsync(batchA, CancellationToken.None);
            await sendSignals[0].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // Connection fault → HandleRetriableBatch with NetworkException → mutes p0
            tcs1.SetException(new IOException("Connection reset by peer"));

            // Enqueue B (p0) — should be blocked
            var batchB = CreateTestBatch(vtPool, "test-topic", 0);
            await sender.EnqueueAsync(batchB, CancellationToken.None);

            // Retry of A
            await sendSignals[1].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            tcs2.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 10));

            // B proceeds
            await sendSignals[2].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            tcs3.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 11));

            await allAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            await Assert.That(ackOffsets[0]).IsEqualTo(10); // A retry
            await Assert.That(ackOffsets[1]).IsEqualTo(11); // B
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(3);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    /// <summary>
    /// Multiple consecutive retriable errors: the partition stays muted through
    /// successive retries, and the normal batch only proceeds after final success.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task MultipleConsecutiveRetries_PartitionStaysMuted_NormalBatchWaits(CancellationToken ct)
    {
        // Send 1: A (p0) → error
        // Send 2: A retry 1 → error again
        // Send 3: A retry 2 → success
        // Send 4: B (p0) → success (finally unmuted)
        var tcsList = new List<TaskCompletionSource<ProduceResponse>>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        for (var i = 0; i < 4; i++)
        {
            var tcs = new TaskCompletionSource<ProduceResponse>();
            tcsList.Add(tcs);
            responseQueue.Enqueue(tcs);
        }

        var sendCount = 0;
        var sendSignals = Enumerable.Range(0, 4).Select(_ => new TaskCompletionSource()).ToArray();

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var idx = Interlocked.Increment(ref sendCount) - 1;
            if (idx < sendSignals.Length)
                sendSignals[idx].TrySetResult();
        });
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var ackOffsets = new List<long>();
        var allAcknowledged = new TaskCompletionSource();

        var sender = CreateSender(pool, options, accumulator, (_, offset, _, _, ex) =>
        {
            if (ex is null)
            {
                lock (ackOffsets)
                {
                    ackOffsets.Add(offset);
                    if (ackOffsets.Count >= 2)
                        allAcknowledged.TrySetResult();
                }
            }
        });

        try
        {
            var batchA = CreateTestBatch(vtPool, "test-topic", 0);
            await sender.EnqueueAsync(batchA, CancellationToken.None);
            await sendSignals[0].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // First error
            tcsList[0].SetResult(CreateRetriableErrorResponse("test-topic", 0));

            // Enqueue B while A is retrying
            var batchB = CreateTestBatch(vtPool, "test-topic", 0);
            await sender.EnqueueAsync(batchB, CancellationToken.None);

            // Retry 1 → error again
            await sendSignals[1].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            tcsList[1].SetResult(CreateRetriableErrorResponse("test-topic", 0));

            // Retry 2 → success
            await sendSignals[2].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            tcsList[2].SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 70));

            // B proceeds
            await sendSignals[3].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            tcsList[3].SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 71));

            await allAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            await Assert.That(ackOffsets[0]).IsEqualTo(70);
            await Assert.That(ackOffsets[1]).IsEqualTo(71);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(4);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    /// <summary>
    /// With maxInFlight=1, a retriable error on one partition in a coalesced request
    /// should not prevent the next request from including batches for unmuted partitions.
    /// The muted partition's normal batches are held back while other partitions proceed.
    ///
    /// Flow: A(p0)+B(p1) coalesced → p0 error, p1 success → enqueue C(p1) →
    /// next send includes A retry(p0) + C(p1) coalesced → both succeed.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task MutedPartitionHeldBack_UnmutedPartitionProceed(CancellationToken ct)
    {
        // Send 1: A(p0) + B(p1) coalesced → p0 error, p1 success
        // Send 2: A retry(p0) + C(p1) coalesced → both succeed
        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2);

        var sendCount = 0;
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var idx = Interlocked.Increment(ref sendCount) - 1;
            if (idx < sendSignals.Length)
                sendSignals[idx].TrySetResult();
        });
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var ackList = new List<(int partition, long offset)>();
        var allAcknowledged = new TaskCompletionSource();

        var sender = CreateSender(pool, options, accumulator, (tp, offset, _, _, ex) =>
        {
            if (ex is null)
            {
                lock (ackList)
                {
                    ackList.Add((tp.Partition, offset));
                    if (ackList.Count >= 3)
                        allAcknowledged.TrySetResult();
                }
            }
        });

        try
        {
            var batchA = CreateTestBatch(vtPool, "test-topic", 0);
            var batchB = CreateTestBatch(vtPool, "test-topic", 1);
            await sender.EnqueueAsync(batchA, CancellationToken.None);
            await sender.EnqueueAsync(batchB, CancellationToken.None);

            await sendSignals[0].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // p0 fails, p1 succeeds → p0 muted
            tcs1.SetResult(CreateMultiPartitionResponse("test-topic",
                (0, ErrorCode.NotLeaderOrFollower, -1),
                (1, ErrorCode.None, 200)));

            // Enqueue C (p1) — p1 is NOT muted, should proceed
            var batchC = CreateTestBatch(vtPool, "test-topic", 1);
            await sender.EnqueueAsync(batchC, CancellationToken.None);

            // Send 2: A retry (p0) + C (p1) coalesced
            await sendSignals[1].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            tcs2.SetResult(CreateMultiPartitionResponse("test-topic",
                (0, ErrorCode.None, 100),
                (1, ErrorCode.None, 201)));

            await allAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // p1 from send 1 must be first ack
            await Assert.That(ackList[0]).IsEqualTo((1, 200L));
            // Remaining: A retry (p0) and C (p1) from send 2
            var remaining = ackList.Skip(1).OrderBy(a => a.partition).ToList();
            await Assert.That(remaining[0]).IsEqualTo((0, 100L));
            await Assert.That(remaining[1]).IsEqualTo((1, 201L));
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(2);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    /// <summary>
    /// Verifies that when the in-flight wait processes a response that mutes a partition,
    /// any already-coalesced normal batch for that partition is moved back to carry-over
    /// (the muted partition filter added in the fix). Without this filter, the normal batch
    /// would be sent alongside the retry, violating ordering.
    ///
    /// Flow: maxInFlight=1, send A(p0)+B(p1), in-flight wait blocks for response.
    /// Response: p0 error, p1 success. Meanwhile C(p0) was coalesced. The muted partition
    /// filter must move C back to carry-over.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task InFlightWait_MutedPartitionFilter_MovesCoalescedNormalBatchToCarryOver(CancellationToken ct)
    {
        // With maxInFlight=1:
        // Send 1: A(p0) + B(p1) coalesced
        // While waiting for response, C(p0) + D(p1) arrive and get coalesced for next send
        // Response arrives: p0 error → p0 muted, p1 success
        // Muted partition filter removes C from coalesced (it's a normal p0 batch on muted partition)
        // Send 2: A retry(p0) + D(p1) (C was moved to carry-over)
        // Send 3: C(p0) (after unmute)
        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var tcs3 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2);
        responseQueue.Enqueue(tcs3);

        var sendCount = 0;
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource(), new TaskCompletionSource() };

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var idx = Interlocked.Increment(ref sendCount) - 1;
            if (idx < sendSignals.Length)
                sendSignals[idx].TrySetResult();
        });
        var options = CreateOptions(maxInFlight: 1);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var ackList = new List<(int partition, long offset)>();
        var allAcknowledged = new TaskCompletionSource();

        var sender = CreateSender(pool, options, accumulator, (tp, offset, _, _, ex) =>
        {
            if (ex is null)
            {
                lock (ackList)
                {
                    ackList.Add((tp.Partition, offset));
                    // B(p1) + A retry(p0) + D(p1) + C(p0) = 4
                    if (ackList.Count >= 4)
                        allAcknowledged.TrySetResult();
                }
            }
        });

        try
        {
            // Phase 1: enqueue A(p0) and B(p1) — coalesced into send 1
            var batchA = CreateTestBatch(vtPool, "test-topic", 0);
            var batchB = CreateTestBatch(vtPool, "test-topic", 1);
            await sender.EnqueueAsync(batchA, CancellationToken.None);
            await sender.EnqueueAsync(batchB, CancellationToken.None);

            await sendSignals[0].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // Enqueue C(p0) and D(p1) while send 1 is in-flight
            // (maxInFlight=1, so the send loop is waiting for the response)
            var batchC = CreateTestBatch(vtPool, "test-topic", 0);
            var batchD = CreateTestBatch(vtPool, "test-topic", 1);
            await sender.EnqueueAsync(batchC, CancellationToken.None);
            await sender.EnqueueAsync(batchD, CancellationToken.None);

            // Complete send 1: p0 fails, p1 succeeds
            // This triggers the muted partition filter on already-coalesced batches
            tcs1.SetResult(CreateMultiPartitionResponse("test-topic",
                (0, ErrorCode.NotLeaderOrFollower, -1),
                (1, ErrorCode.None, 200)));

            // Send 2: should include A retry (p0) and D (p1)
            // C (p0) should be in carry-over because the muted partition filter caught it
            await sendSignals[1].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            tcs2.SetResult(CreateMultiPartitionResponse("test-topic",
                (0, ErrorCode.None, 100),
                (1, ErrorCode.None, 201)));

            // Send 3: C(p0) proceeds after unmute
            await sendSignals[2].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            tcs3.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 101));

            await allAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // Verify: exactly 3 sends occurred
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(3);

            // Verify ordering: A(p0, offset=100) must come before C(p0, offset=101)
            var p0Acks = ackList.Where(a => a.partition == 0).ToList();
            await Assert.That(p0Acks).Count().IsEqualTo(2);
            await Assert.That(p0Acks[0].offset).IsEqualTo(100); // A retry
            await Assert.That(p0Acks[1].offset).IsEqualTo(101); // C
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }
}
