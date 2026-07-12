using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using Dekaf.Compression;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Microsoft.Extensions.Logging;

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
    private static readonly MethodInfo CoalesceBatchMethod = typeof(BrokerSender).GetMethod(
        "CoalesceBatch",
        BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly Type PartitionCarryOverType = typeof(BrokerSender).GetNestedType(
        "PartitionCarryOver",
        BindingFlags.NonPublic)!;

    private static readonly Type BatchReferenceType = typeof(BrokerSender).GetNestedType(
        "BatchReference",
        BindingFlags.NonPublic)!;

    private static ProducerOptions CreateOptions(Acks acks = Acks.All, int maxInFlight = 1,
        int retryBackoffMs = 0, int retryBackoffMaxMs = 0,
        int deliveryTimeoutMs = 30_000, int requestTimeoutMs = 30_000,
        int maxRequestSize = 1_048_576, bool enableIdempotence = true) => new()
        {
            BootstrapServers = ["localhost:9092"],
            MaxInFlightRequestsPerConnection = maxInFlight,
            EnableIdempotence = enableIdempotence,
            Acks = acks,
            DeliveryTimeoutMs = deliveryTimeoutMs,
            RetryBackoffMs = retryBackoffMs,
            RetryBackoffMaxMs = retryBackoffMaxMs,
            RequestTimeoutMs = requestTimeoutMs,
            MaxRequestSize = maxRequestSize,
            LingerMs = 0
        };

    private static (IConnectionPool pool, TestKafkaConnection connection) CreateMockConnection(
        Queue<TaskCompletionSource<ProduceResponse>> responseQueue,
        Action? onSend = null)
    {
        var connection = new TestKafkaConnection();

        Task<ProduceResponse> DequeueResponse()
        {
            var task = responseQueue.Dequeue().Task;
            onSend?.Invoke();
            return task;
        }

        connection.SendProducePipelinedAfterWrite = () => new ValueTask<Task<ProduceResponse>>(DequeueResponse());

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
            dataSize: 100);

        batch.TrySetMemoryReleased();
        return batch;
    }

    private static async Task WaitForDiagAsync(
        ReadyBatch batch,
        char marker,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency);
        while (!batch.DiagTrace.Contains(marker))
        {
            if (Stopwatch.GetTimestamp() >= deadline)
                throw new TimeoutException($"Batch did not reach '{marker}': {batch.DiagTrace}");

            await Task.Delay(1, cancellationToken);
        }
    }

    private static ProduceResponse CreateSuccessResponse(string topic, int partition, long baseOffset) =>
        new()
        {
            TopicCount = 1,
            Responses =
            [
                new ProduceResponseTopicData
                {
                    Name = topic,
                    PartitionCount = 1,
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
            TopicCount = 1,
            Responses =
            [
                new ProduceResponseTopicData
                {
                    Name = topic,
                    PartitionCount = 1,
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

    private static ProduceResponse CreateInlineLeaderErrorResponse(
        string topic,
        int partition,
        int leaderId,
        int leaderEpoch) =>
        new()
        {
            TopicCount = 1,
            NodeEndpoints =
            [
                new NodeEndpoint { NodeId = leaderId, Host = $"broker-{leaderId}", Port = 9092 + leaderId }
            ],
            Responses =
            [
                new ProduceResponseTopicData
                {
                    Name = topic,
                    PartitionCount = 1,
                    PartitionResponses =
                    [
                        new ProduceResponsePartitionData
                        {
                            Index = partition,
                            ErrorCode = ErrorCode.NotLeaderOrFollower,
                            BaseOffset = -1,
                            CurrentLeader = new LeaderIdAndEpoch
                            {
                                LeaderId = leaderId,
                                LeaderEpoch = leaderEpoch
                            }
                        }
                    ]
                }
            ]
        };

    private static ProduceResponse CreateMultiPartitionResponse(
        string topic, params (int partition, ErrorCode errorCode, long baseOffset)[] partitions) =>
        new()
        {
            TopicCount = 1,
            Responses =
            [
                new ProduceResponseTopicData
                {
                    Name = topic,
                    PartitionCount = partitions.Length,
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
        Action<TopicPartition, long, DateTimeOffset, int, Exception?> onAcknowledgement,
        MetadataManager? metadataManager = null,
        Action<ReadyBatch, int>? rerouteBatch = null,
        ILogger? logger = null) =>
        new(
            brokerId: 1, pool,
            metadataManager ?? new MetadataManager(pool, options.BootstrapServers),
            accumulator, options,
            new CompressionCodecRegistry(),
            inflightTracker: new PartitionInflightTracker(),
            getProduceApiVersion: () => 9,
            setProduceApiVersion: _ => { },
            isTransactional: () => false,
            ensurePartitionInTransaction: null,
            bumpEpoch: null,
            getCurrentEpoch: null,
            rerouteBatch: rerouteBatch,
            onAcknowledgement: onAcknowledgement,
            logger: logger);

    private static object CreateCarryOver()
    {
        return Activator.CreateInstance(PartitionCarryOverType)!;
    }

    private static int GetCarryOverCount(object carryOver)
    {
        return (int)PartitionCarryOverType.GetProperty("Count")!.GetValue(carryOver)!;
    }

    private static object CreateBatchReference(ReadyBatch batch, int generation)
    {
        return Activator.CreateInstance(BatchReferenceType, batch, generation)!;
    }

    private static void InvokeCoalesceBatch(
        BrokerSender sender,
        ReadyBatch batch,
        ReadyBatch[] coalescedBatches,
        int[] coalescedGenerations,
        ref int coalescedCount,
        HashSet<TopicPartition> coalescedPartitions,
        object carryOver,
        int? capturedGeneration = null)
    {
        var coalescedRequestBudgetUsed = 0L;
        for (var i = 0; i < coalescedCount; i++)
        {
            var existingBatch = coalescedBatches[i];
            coalescedRequestBudgetUsed += ProduceRequestSizeCalculator.GetSingleBatchRequestBodySize(
                transactionalId: null,
                existingBatch.TopicPartition.Topic,
                existingBatch.EncodedSize);
        }

        var args = new object?[]
        {
            CreateBatchReference(batch, capturedGeneration ?? batch.Generation),
            coalescedBatches,
            coalescedGenerations,
            coalescedCount,
            coalescedRequestBudgetUsed,
            coalescedPartitions,
            carryOver
        };

        CoalesceBatchMethod.Invoke(sender, args);
        coalescedCount = (int)args[3]!;
    }

    private static ReadyBatch CreateMinimalBatch(string topic, int partition)
    {
        var batch = new ReadyBatch();
        batch.Initialize(
            new TopicPartition(topic, partition),
            new RecordBatch { Records = Array.Empty<Record>() },
            completionSourcesArray: null,
            completionSourcesCount: 0,
            dataSize: 100);
        return batch;
    }

    [Test]
    // Mock response continuations intentionally run on the ThreadPool. Isolate this timing-sensitive
    // integration of the sender state machine so unrelated concurrency tests cannot starve its wakeups.
    [NotInParallel]
    [Timeout(30_000)]
    public async Task NonIdempotentProducer_MultipleInFlight_SerializesSamePartitionBatches(
        CancellationToken ct)
    {
        var responses = Enumerable.Range(0, 2)
            .Select(_ => new TaskCompletionSource<ProduceResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(responses);
        var (pool, _) = CreateMockConnection(responseQueue);
        var options = CreateOptions(maxInFlight: 2, enableIdempotence: false);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var sendLogger = new PipelinedSendLogger(expectedSends: 2);
        var acknowledgedOffsets = new List<long>();
        var allAcknowledged = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, offset, _, _, ex) =>
            {
                if (ex is not null)
                    return;

                lock (acknowledgedOffsets)
                {
                    acknowledgedOffsets.Add(offset);
                    if (acknowledgedOffsets.Count == 2)
                        allAcknowledged.TrySetResult();
                }
            },
            logger: sendLogger);

        try
        {
            var firstBatch = CreateTestBatch(vtPool, "test-topic", partition: 0);
            sender.Enqueue(firstBatch);
            await sendLogger.SendSignals[0].Task.WaitAsync(ct);

            await Assert.That(accumulator.IsMuted(firstBatch.TopicPartition)).IsTrue();

            var secondBatch = CreateTestBatch(vtPool, "test-topic", partition: 0);
            sender.Enqueue(secondBatch);

            await WaitForDiagAsync(secondBatch, 'O', TimeSpan.FromSeconds(5), ct);
            await Assert.That(sendLogger.SendCount).IsEqualTo(1);

            responses[0].SetResult(CreateSuccessResponse("test-topic", partition: 0, baseOffset: 100));
            await sendLogger.SendSignals[1].Task.WaitAsync(ct);
            responses[1].SetResult(CreateSuccessResponse("test-topic", partition: 0, baseOffset: 101));

            await allAcknowledged.Task.WaitAsync(ct);
            await Assert.That(acknowledgedOffsets[0]).IsEqualTo(100);
            await Assert.That(acknowledgedOffsets[1]).IsEqualTo(101);
        }
        finally
        {
            responses[0].TrySetResult(CreateSuccessResponse("test-topic", partition: 0, baseOffset: 100));
            responses[1].TrySetResult(CreateSuccessResponse("test-topic", partition: 0, baseOffset: 101));
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [NotInParallel]
    [Timeout(30_000)]
    public async Task NonIdempotentProducer_ScaleUp_PinsPartitionUntilPendingRequestCompletes(
        CancellationToken ct)
    {
        var response = new TaskCompletionSource<ProduceResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>([response]);
        var (pool, _) = CreateMockConnection(responseQueue);
        var options = CreateOptions(maxInFlight: 2, enableIdempotence: false);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var sendLogger = new PipelinedSendLogger(expectedSends: 1);
        var acknowledged = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, ex) =>
            {
                if (ex is null)
                    acknowledged.TrySetResult();
            },
            logger: sendLogger);

        try
        {
            sender.Enqueue(CreateTestBatch(vtPool, "test-topic", partition: 1));
            await sendLogger.SendSignals[0].Task.WaitAsync(ct);

            typeof(BrokerSender).GetMethod(
                "ApplyScaleUp",
                BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(sender, [2]);

            var getConnection = typeof(BrokerSender).GetMethod(
                "GetConnectionForPartition",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var topicPartition = new TopicPartition("test-topic", 1);
            var routeWhilePending = (int)getConnection.Invoke(sender, [topicPartition])!;

            await Assert.That(routeWhilePending).IsEqualTo(0)
                .Because("scale-up must not move a non-idempotent partition before its old request is acknowledged");

            response.SetResult(CreateSuccessResponse("test-topic", partition: 1, baseOffset: 100));
            await acknowledged.Task.WaitAsync(ct);

            var deadline = Stopwatch.GetTimestamp() + (5 * Stopwatch.Frequency);
            while ((int)getConnection.Invoke(sender, [topicPartition])! != 1
                   && Stopwatch.GetTimestamp() < deadline)
            {
                await Task.Delay(1, ct);
            }

            await Assert.That((int)getConnection.Invoke(sender, [topicPartition])!).IsEqualTo(1)
                .Because("partition should migrate after its old request is acknowledged");
        }
        finally
        {
            response.TrySetResult(CreateSuccessResponse("test-topic", partition: 1, baseOffset: 100));
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    public async Task NonIdempotentFireAndForget_DisablesAdaptiveConnectionRemapping()
    {
        var pool = Substitute.For<IConnectionPool>();
        var options = CreateOptions(
            acks: Acks.None,
            maxInFlight: 2,
            enableIdempotence: false);
        var accumulator = new RecordAccumulator(options);
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { });

        try
        {
            var adaptiveScalingEnabled = (bool)typeof(BrokerSender).GetField(
                "_adaptiveScalingEnabled",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sender)!;

            await Assert.That(adaptiveScalingEnabled).IsFalse()
                .Because("Acks.None has no broker acknowledgement that makes cross-connection remapping safe");
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task NonIdempotentFireAndForget_SerializesSamePartitionWrites(
        CancellationToken ct)
    {
        var firstWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWriteStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new TestKafkaConnection();
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);

        var options = CreateOptions(
            acks: Acks.None,
            maxInFlight: 2,
            enableIdempotence: false);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var acknowledgedCount = 0;
        var allAcknowledged = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        BrokerSender? sender = null;
        var secondBatch = CreateTestBatch(vtPool, "test-topic", partition: 0);
        var sendCount = 0;

        connection.SendProduceFireAndForgetWithCallerTimeout = () =>
        {
            var current = Interlocked.Increment(ref sendCount);
            if (current == 1)
            {
                sender!.Enqueue(secondBatch);
                return new ValueTask(firstWrite.Task);
            }

            secondWriteStarted.TrySetResult();
            return new ValueTask(secondWrite.Task);
        };

        sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, ex) =>
            {
                if (ex is null && Interlocked.Increment(ref acknowledgedCount) == 2)
                    allAcknowledged.TrySetResult();
            });

        try
        {
            sender.Enqueue(CreateTestBatch(vtPool, "test-topic", partition: 0));

            await WaitForDiagAsync(secondBatch, 'O', TimeSpan.FromSeconds(5), ct);
            await Assert.That(secondWriteStarted.Task.IsCompleted).IsFalse();

            firstWrite.SetResult();
            await secondWriteStarted.Task.WaitAsync(ct);
            secondWrite.SetResult();
            await allAcknowledged.Task.WaitAsync(ct);
        }
        finally
        {
            firstWrite.TrySetResult();
            secondWrite.TrySetResult();
            if (sender is not null)
                await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    public async Task CoalesceBatch_WhenFull_CarriesOverNormalBatch()
    {
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        var (pool, _) = CreateMockConnection(responseQueue);
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { });

        try
        {
            var firstBatch = CreateTestBatch(vtPool, "test-topic", 0);
            var overflowBatch = CreateTestBatch(vtPool, "test-topic", 1);
            var coalescedBatches = new[] { firstBatch };
            var coalescedGenerations = new[] { firstBatch.Generation };
            var coalescedCount = 1;
            var coalescedPartitions = new HashSet<TopicPartition> { firstBatch.TopicPartition };
            var carryOver = CreateCarryOver();

            InvokeCoalesceBatch(
                sender,
                overflowBatch,
                coalescedBatches,
                coalescedGenerations,
                ref coalescedCount,
                coalescedPartitions,
                carryOver);

            await Assert.That(coalescedCount).IsEqualTo(1);
            await Assert.That(coalescedBatches[0]).IsSameReferenceAs(firstBatch);
            await Assert.That(GetCarryOverCount(carryOver)).IsEqualTo(1);
            await Assert.That(coalescedPartitions.Contains(overflowBatch.TopicPartition)).IsFalse();
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    public async Task CoalesceBatch_SeparateDrainPasses_RespectsMaxRequestSizeBudget()
    {
        const string topic = "test-topic";
        const int encodedBatchSize = 100;
        var maxRequestSize = ProduceRequestSizeCalculator.GetSingleBatchRequestBodySize(
            transactionalId: null,
            topic,
            encodedBatchSize);
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        var (pool, _) = CreateMockConnection(responseQueue);
        var options = CreateOptions(maxRequestSize: maxRequestSize);
        var accumulator = new RecordAccumulator(options);
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { });

        try
        {
            var firstBatch = CreateMinimalBatch(topic, partition: 0);
            var secondBatch = CreateMinimalBatch(topic, partition: 1);
            var coalescedBatches = new ReadyBatch[4];
            var coalescedGenerations = new int[4];
            var coalescedCount = 0;
            var coalescedPartitions = new HashSet<TopicPartition>();
            var carryOver = CreateCarryOver();

            // Simulate batches arriving from separate accumulator drain passes. Each batch
            // exactly fills MaxRequestSize after ProduceRequest framing, so only one can be sent.
            InvokeCoalesceBatch(
                sender,
                firstBatch,
                coalescedBatches,
                coalescedGenerations,
                ref coalescedCount,
                coalescedPartitions,
                carryOver);
            InvokeCoalesceBatch(
                sender,
                secondBatch,
                coalescedBatches,
                coalescedGenerations,
                ref coalescedCount,
                coalescedPartitions,
                carryOver);

            await Assert.That(firstBatch.EncodedSize).IsEqualTo(encodedBatchSize);
            await Assert.That(maxRequestSize).IsGreaterThan(encodedBatchSize);
            await Assert.That(coalescedCount).IsEqualTo(1);
            await Assert.That(coalescedBatches[0]).IsSameReferenceAs(firstBatch);
            await Assert.That(GetCarryOverCount(carryOver)).IsEqualTo(1);
            await Assert.That(coalescedPartitions.Contains(secondBatch.TopicPartition)).IsFalse();
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task CoalesceBatch_StaleGenerationSkipsReRentedBatch()
    {
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        var (pool, _) = CreateMockConnection(responseQueue);
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { });

        try
        {
            var batch = CreateMinimalBatch("old-topic", 0);
            var staleGeneration = batch.Generation;
            Interlocked.Exchange(ref batch._returnedToPool, 1);
            batch.Initialize(
                new TopicPartition("new-topic", 1),
                new RecordBatch { Records = Array.Empty<Record>() },
                completionSourcesArray: null,
                completionSourcesCount: 0,
                dataSize: 100);

            var coalescedBatches = new ReadyBatch[4];
            var coalescedGenerations = new int[4];
            var coalescedCount = 0;
            var coalescedPartitions = new HashSet<TopicPartition>();
            var carryOver = CreateCarryOver();

            InvokeCoalesceBatch(
                sender,
                batch,
                coalescedBatches,
                coalescedGenerations,
                ref coalescedCount,
                coalescedPartitions,
                carryOver,
                staleGeneration);

            await Assert.That(coalescedCount).IsEqualTo(0);
            await Assert.That(GetCarryOverCount(carryOver)).IsEqualTo(0);
            await Assert.That(coalescedPartitions.Contains(batch.TopicPartition)).IsFalse();
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task CoalesceBatch_WhenFull_CarriesOverRetryBatchWithoutClearingRetryState()
    {
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        var (pool, _) = CreateMockConnection(responseQueue);
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { });

        try
        {
            var firstBatch = CreateTestBatch(vtPool, "test-topic", 0);
            var overflowBatch = CreateTestBatch(vtPool, "test-topic", 1);
            overflowBatch.IsRetry = true;

            var coalescedBatches = new[] { firstBatch };
            var coalescedGenerations = new[] { firstBatch.Generation };
            var coalescedCount = 1;
            var coalescedPartitions = new HashSet<TopicPartition> { firstBatch.TopicPartition };
            var carryOver = CreateCarryOver();

            InvokeCoalesceBatch(
                sender,
                overflowBatch,
                coalescedBatches,
                coalescedGenerations,
                ref coalescedCount,
                coalescedPartitions,
                carryOver);

            await Assert.That(coalescedCount).IsEqualTo(1);
            await Assert.That(coalescedBatches[0]).IsSameReferenceAs(firstBatch);
            await Assert.That(GetCarryOverCount(carryOver)).IsEqualTo(1);
            await Assert.That(overflowBatch.IsRetry).IsTrue();
            await Assert.That(coalescedPartitions.Contains(overflowBatch.TopicPartition)).IsFalse();
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(60_000)]
    public async Task RetriableError_WithInlineLeader_ReroutesWithoutRetryBackoff(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs);

        var requestSent = new TaskCompletionSource();
        var (pool, _) = CreateMockConnection(responseQueue, onSend: () => requestSent.TrySetResult());
        var options = CreateOptions(retryBackoffMs: 10_000, retryBackoffMaxMs: 10_000);
        var accumulator = new RecordAccumulator(options);
        var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var rerouted = new TaskCompletionSource<ReadyBatch>();

        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9093 }],
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = "test-topic",
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 0,
                            LeaderId = 1,
                            LeaderEpoch = 5,
                            ReplicaNodes = [1],
                            IsrNodes = [1]
                        }
                    ]
                }
            ]
        });

        var sender = CreateSender(
            pool,
            options,
            accumulator,
            onAcknowledgement: (_, _, _, _, _) => { },
            metadataManager,
            rerouteBatch: (batch, _) => rerouted.TrySetResult(batch));

        try
        {
            var batch = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batch);

            await requestSent.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            tcs.SetResult(CreateInlineLeaderErrorResponse("test-topic", 0, leaderId: 2, leaderEpoch: 6));

            var reroutedBatch = await rerouted.Task.WaitAsync(TimeSpan.FromSeconds(3), ct);
            var leader = metadataManager.Metadata.GetPartitionLeader("test-topic", 0);

            await Assert.That(reroutedBatch).IsSameReferenceAs(batch);
            await Assert.That(leader).IsNotNull();
            await Assert.That(leader!.NodeId).IsEqualTo(2);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await metadataManager.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

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
            sender.Enqueue(batchA);

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
            sender.Enqueue(batchB);

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
            sender.Enqueue(batchA);
            sender.Enqueue(batchB);

            // Wait for send 1 (coalesced A+B)
            await sendSignals[0].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // p0 gets retriable error, p1 succeeds
            tcs1.SetResult(CreateMultiPartitionResponse("test-topic",
                (0, ErrorCode.NotLeaderOrFollower, -1),
                (1, ErrorCode.None, 200)));

            // Enqueue batch C (p1) — should NOT be blocked (p1 is not muted)
            var batchC = CreateTestBatch(vtPool, "test-topic", 1);
            sender.Enqueue(batchC);

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
            sender.Enqueue(batchA);
            sender.Enqueue(batchB);
            sender.Enqueue(batchC);

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
            sender.Enqueue(batchA);
            await sendSignals[0].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // Connection fault → HandleRetriableBatch with NetworkException → mutes p0
            tcs1.SetException(new IOException("Connection reset by peer"));

            // Enqueue B (p0) — should be blocked
            var batchB = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batchB);

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
            sender.Enqueue(batchA);
            await sendSignals[0].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // First error
            tcsList[0].SetResult(CreateRetriableErrorResponse("test-topic", 0));

            // Enqueue B while A is retrying
            var batchB = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batchB);

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
            sender.Enqueue(batchA);
            sender.Enqueue(batchB);

            await sendSignals[0].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // p0 fails, p1 succeeds → p0 muted
            tcs1.SetResult(CreateMultiPartitionResponse("test-topic",
                (0, ErrorCode.NotLeaderOrFollower, -1),
                (1, ErrorCode.None, 200)));

            // Enqueue C (p1) — p1 is NOT muted, should proceed
            var batchC = CreateTestBatch(vtPool, "test-topic", 1);
            sender.Enqueue(batchC);

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
    /// Verifies the actual unexpected-exit cleanup path publishes its partition barrier,
    /// preserves survivor FIFO, and reroutes a newer batch rejected by the dead sender
    /// behind both survivors.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task UnexpectedLoopExit_AfterRetiredSenderDisposal_RedeliveryPreemptsRejectedNewerBatch(CancellationToken ct)
    {
        var responses = Enumerable.Range(0, 3)
            .Select(_ => new TaskCompletionSource<ProduceResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(responses);
        var sendSignals = Enumerable.Range(0, 3)
            .Select(_ => new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        var sendCount = 0;
        var (pool, _) = CreateMockConnection(responseQueue, () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            if (index < sendSignals.Length)
                sendSignals[index].TrySetResult();
        });
        var options = CreateOptions(maxInFlight: 1);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var acknowledgedMessageCounts = new List<int>();
        var allAcknowledged = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var replacement = CreateSender(pool, options, accumulator, (tp, _, _, count, ex) =>
        {
            if (tp.Partition != 0 || ex is not null)
                return;

            lock (acknowledgedMessageCounts)
            {
                acknowledgedMessageCounts.Add(count);
                if (acknowledgedMessageCounts.Count == 3)
                    allAcknowledged.TrySetResult();
            }
        });
        var crashLogger = new ThrowOnArmedIterationLogger();
        var exited = CreateSender(
            pool,
            options,
            accumulator,
            onAcknowledgement: (_, _, _, _, _) => { },
            rerouteBatch: replacement.Enqueue,
            logger: crashLogger);

        try
        {
            var recoveredA = CreateTestBatch(vtPool, "test-topic", partition: 0, messageCount: 1);
            var recoveredB = CreateTestBatch(vtPool, "test-topic", partition: 0, messageCount: 2);
            var newer = CreateTestBatch(vtPool, "test-topic", partition: 0, messageCount: 3);
            var topicPartition = recoveredA.TopicPartition;

            // The logger holds the source at the first iteration boundary. Queue both batches
            // while it is blocked, then release it to throw before either event is drained.
            await crashLogger.FirstIteration.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            exited.EnqueueBulk([recoveredA, recoveredB]);
            crashLogger.Arm();

            while (exited.IsAlive)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            // Production disposes the crashed sender as soon as the first survivor creates
            // its replacement. A caller that already captured the old sender can still race
            // in afterward; its rejected batch must follow the replacement handoff, not fail.
            await exited.DisposeAsync();

            // Crash recovery references must keep this newer batch behind A and B.
            exited.Enqueue(newer);

            await sendSignals[0].Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            await WaitForDiagAsync(recoveredA, 'W', TimeSpan.FromSeconds(5), ct);
            responses[0].SetResult(CreateSuccessResponse("test-topic", partition: 0, baseOffset: 10));
            await sendSignals[1].Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            await WaitForDiagAsync(recoveredB, 'W', TimeSpan.FromSeconds(5), ct);
            responses[1].SetResult(CreateSuccessResponse("test-topic", partition: 0, baseOffset: 11));
            await sendSignals[2].Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            await WaitForDiagAsync(newer, 'W', TimeSpan.FromSeconds(5), ct);
            responses[2].SetResult(CreateSuccessResponse("test-topic", partition: 0, baseOffset: 12));

            try
            {
                await allAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            }
            catch (TimeoutException ex)
            {
                throw new InvalidOperationException(
                    $"Acknowledgements=[{string.Join(',', acknowledgedMessageCounts)}], " +
                    $"sends={Volatile.Read(ref sendCount)}, queuedResponses={responseQueue.Count}, " +
                    $"A={recoveredA.DiagTrace}, B={recoveredB.DiagTrace}, newer={newer.DiagTrace}", ex);
            }

            await Assert.That(acknowledgedMessageCounts).Count().IsEqualTo(3);
            await Assert.That(acknowledgedMessageCounts[0]).IsEqualTo(1);
            await Assert.That(acknowledgedMessageCounts[1]).IsEqualTo(2);
            await Assert.That(acknowledgedMessageCounts[2]).IsEqualTo(3);

            var unmuteDeadline = Stopwatch.GetTimestamp() + (5 * Stopwatch.Frequency);
            while (accumulator.IsMuted(topicPartition) && Stopwatch.GetTimestamp() < unmuteDeadline)
                await Task.Delay(1, ct);
            await Assert.That(accumulator.IsMuted(topicPartition)).IsFalse();
        }
        finally
        {
            crashLogger.Arm();
            for (var i = 0; i < responses.Length; i++)
                responses[i].TrySetResult(CreateSuccessResponse("test-topic", partition: 0, baseOffset: 10 + i));

            await exited.DisposeAsync();
            await replacement.DisposeAsync();
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
            sender.Enqueue(batchA);
            sender.Enqueue(batchB);

            await sendSignals[0].Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // Enqueue C(p0) and D(p1) while send 1 is in-flight
            // (maxInFlight=1, so the send loop is waiting for the response)
            var batchC = CreateTestBatch(vtPool, "test-topic", 0);
            var batchD = CreateTestBatch(vtPool, "test-topic", 1);
            sender.Enqueue(batchC);
            sender.Enqueue(batchD);

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

    private sealed class ThrowOnArmedIterationLogger : ILogger
    {
        private int _armed;
        private int _thrown;
        private readonly TaskCompletionSource _crashGate =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource FirstIteration { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Arm()
        {
            Volatile.Write(ref _armed, 1);
            _crashGate.TrySetResult();
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (eventId.Name != "LogSendLoopIteration"
                && !formatter(state, exception).Contains("send loop iteration", StringComparison.Ordinal))
            {
                return;
            }

            FirstIteration.TrySetResult();
            _crashGate.Task.GetAwaiter().GetResult();
            if (Volatile.Read(ref _armed) != 0 && Interlocked.Exchange(ref _thrown, 1) == 0)
                throw new InvalidOperationException("Injected send-loop crash");
        }
    }

    private sealed class PipelinedSendLogger : ILogger
    {
        private int _sendCount;

        public PipelinedSendLogger(int expectedSends)
        {
            SendSignals = Enumerable.Range(0, expectedSends)
                .Select(_ => new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously))
                .ToArray();
        }

        public TaskCompletionSource[] SendSignals { get; }

        public int SendCount => Volatile.Read(ref _sendCount);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Debug;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (eventId.Name != "LogPipelinedSend")
                return;

            var index = Interlocked.Increment(ref _sendCount) - 1;
            if ((uint)index < (uint)SendSignals.Length)
                SendSignals[index].TrySetResult();
        }
    }
}
