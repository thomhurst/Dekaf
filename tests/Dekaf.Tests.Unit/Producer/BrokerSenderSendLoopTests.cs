using System.Buffers;
using System.Diagnostics;
using Dekaf.Compression;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;

using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Behavioral tests for BrokerSender's send loop, verifying:
/// - Response completion wakes the send loop (via event channel, not Task.WhenAny)
/// - In-flight request limiting via _pendingResponses.Count
/// - Fire-and-forget (Acks.None) path doesn't track in-flight
/// - Faulted responses wake the send loop
///
/// These tests use controllable mock connections to verify send loop timing behavior.
/// Synchronization uses deterministic TCS signals from mock callbacks, not Task.Delay.
/// </summary>
public sealed class BrokerSenderSendLoopTests
{
    private static ProducerOptions CreateOptions(Acks acks = Acks.All, int maxInFlight = 1,
        int retryBackoffMs = 100, int retryBackoffMaxMs = 1000,
        int deliveryTimeoutMs = 30_000, int requestTimeoutMs = 30_000,
        int connectionsPerBroker = 1, bool enableAdaptiveConnections = true,
        bool enableIdempotence = true, int batchSize = 1_048_576,
        long? unackedByteBudgetCapOverride = null, long? scaleCooldownMsOverride = null) => new()
        {
            BootstrapServers = ["localhost:9092"],
            MaxInFlightRequestsPerConnection = maxInFlight,
            ConnectionsPerBroker = connectionsPerBroker,
            EnableAdaptiveConnections = enableAdaptiveConnections,
            EnableIdempotence = enableIdempotence,
            Acks = acks,
            DeliveryTimeoutMs = deliveryTimeoutMs,
            RetryBackoffMs = retryBackoffMs,
            RetryBackoffMaxMs = retryBackoffMaxMs,
            RequestTimeoutMs = requestTimeoutMs,
            BatchSize = batchSize,
            LingerMs = 0,
            UnackedByteBudgetCapOverride = unackedByteBudgetCapOverride,
            ScaleCooldownMsOverride = scaleCooldownMsOverride
        };

    /// <summary>
    /// Creates a mock connection pool that returns a controllable mock connection.
    /// The mock connection queues response tasks so each SendPipelinedAfterWriteAsync call
    /// returns the next TaskCompletionSource's task from the queue.
    /// The optional onSend callback fires each time SendPipelinedAfterWriteAsync is called,
    /// enabling deterministic synchronization without Task.Delay.
    /// </summary>
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
        pool.GetConnectionByIndexAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(connection);

        return (pool, connection);
    }

    /// <summary>
    /// Creates a mock pool whose ScaleConnectionGroupAsync resolves the requested width and
    /// signals the returned TCS — shared by the scale-up trigger tests.
    /// </summary>
    private static (IConnectionPool pool, TaskCompletionSource<int> scaleRequested) CreateScaleTrackingPool(
        TestKafkaConnection connection)
    {
        var scaleRequested = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);
        pool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);
        pool.ScaleConnectionGroupAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var targetCount = (int)callInfo[1]!;
                scaleRequested.TrySetResult(targetCount);
                return new ValueTask<int>(targetCount);
            });

        return (pool, scaleRequested);
    }

    /// <summary>
    /// Creates a minimal ReadyBatch suitable for send loop testing.
    /// Sets MemoryReleased=true to skip accumulator memory tracking.
    /// </summary>
    private static ReadyBatch CreateTestBatch(
        ValueTaskSourcePool<RecordMetadata> pool,
        string topic, int partition, int messageCount = 1,
        bool markMemoryReleased = true, int dataSize = 100)
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
            dataSize: dataSize);

        if (markMemoryReleased)
        {
            // Skip accumulator memory tracking in tests
            batch.TrySetMemoryReleased();
        }

        return batch;
    }

    /// <summary>
    /// Creates a ProduceResponse with success for the given topic/partition.
    /// </summary>
    private static ProduceResponse CreateSuccessResponse(
        string topic, int partition, long baseOffset, int throttleTimeMs = 0) =>
        new()
        {
            ThrottleTimeMs = throttleTimeMs,
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

    private static ProduceResponse CreateErrorResponse(string topic, int partition, ErrorCode errorCode) =>
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
                            ErrorCode = errorCode,
                            BaseOffset = -1
                        }
                    ]
                }
            ]
        };

    private static ProduceResponse CreateSuccessResponseForPartitions(string topic, int partitionCount) =>
        new()
        {
            TopicCount = 1,
            Responses =
            [
                new ProduceResponseTopicData
                {
                    Name = topic,
                    PartitionCount = partitionCount,
                    PartitionResponses = Enumerable.Range(0, partitionCount)
                        .Select(partition => new ProduceResponsePartitionData
                        {
                            Index = partition,
                            ErrorCode = ErrorCode.None,
                            BaseOffset = partition + 1
                        }).ToArray()
                }
            ]
        };

    private static MetadataResponse CreateLeaderMetadataResponse(
        string topic,
        int leaderId,
        int leaderEpoch,
        int partitionCount = 1) =>
        new()
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9093 },
                new BrokerMetadata { NodeId = 2, Host = "broker-2", Port = 9094 }
            ],
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = topic,
                    Partitions = Enumerable.Range(0, partitionCount)
                        .Select(partition => new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = partition,
                            LeaderId = leaderId,
                            LeaderEpoch = leaderEpoch,
                            ReplicaNodes = [1, 2],
                            IsrNodes = [1, 2]
                        }).ToArray()
                }
            ]
        };

    /// <summary>
    /// Creates a BrokerSender with standard test defaults, reducing constructor boilerplate.
    /// </summary>
    private static BrokerSender CreateSender(
        IConnectionPool pool,
        ProducerOptions options,
        RecordAccumulator accumulator,
        Action<TopicPartition, long, DateTimeOffset, int, Exception?> onAcknowledgement,
        MetadataManager? metadataManager = null,
        Action<ReadyBatch, int>? rerouteBatch = null,
        Action<int>? onBrokerThrottle = null,
        Func<long>? getTimestamp = null,
        Func<int, CancellationToken, ValueTask>? delayForThrottle = null,
        Action? onBlockedBucketRequeued = null,
        BrokerUnackedByteBudget? unackedBudget = null) =>
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
            rerouteBatch,
            onAcknowledgement: onAcknowledgement,
            logger: null,
            onBrokerThrottle: onBrokerThrottle,
            getTimestamp: getTimestamp,
            delayForThrottle: delayForThrottle,
            onBlockedBucketRequeued: onBlockedBucketRequeued,
            unackedBudget: unackedBudget);

    private static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken cancellationToken)
    {
        while (!predicate())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoopPressure_ScalesConnections_WhenCoalescingBacklogPersists(CancellationToken cancellationToken)
    {
        const int partitionCount = 8;
        const int batchCount = 512;

        var options = CreateOptions(maxInFlight: 1);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var connection = new TestKafkaConnection();
        var sendCount = 0;
        connection.SendProducePipelinedAfterWrite = () =>
        {
            Interlocked.Increment(ref sendCount);
            return new ValueTask<Task<ProduceResponse>>(
                Task.FromResult(CreateSuccessResponseForPartitions("test-topic", partitionCount)));
        };

        var (pool, scaleRequested) = CreateScaleTrackingPool(connection);

        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { });

        try
        {
            for (var i = 0; i < batchCount; i++)
                sender.Enqueue(CreateTestBatch(vtPool, "test-topic", i % partitionCount));

            var targetCount = await scaleRequested.Task.WaitAsync(cancellationToken);

            await Assert.That(targetCount).IsGreaterThan(1);
            await Assert.That(Volatile.Read(ref sendCount)).IsGreaterThan(0);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_PipelinedProduce_UsesConnectionOwnedTimeoutsForConcurrentSends(CancellationToken cancellationToken)
    {
        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2);

        var sendCount = 0;
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        var (pool, connection) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var idx = Interlocked.Increment(ref sendCount) - 1;
            if (idx < sendSignals.Length)
                sendSignals[idx].TrySetResult();
        });
        var options = CreateOptions(maxInFlight: 5);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var acknowledgedCount = 0;
        var allAcknowledged = new TaskCompletionSource();
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, ex) =>
        {
            if (ex is null && Interlocked.Increment(ref acknowledgedCount) == 2)
                allAcknowledged.TrySetResult();
        });

        try
        {
            sender.Enqueue(CreateTestBatch(vtPool, "test-topic", 0));

            await sendSignals[0].Task.WaitAsync(cancellationToken);
            sender.Enqueue(CreateTestBatch(vtPool, "test-topic", 1));
            await sendSignals[1].Task.WaitAsync(cancellationToken);

            await Assert.That(Volatile.Read(ref connection.SendPipelinedAfterWriteCalls)).IsEqualTo(2);
            await Assert.That(Volatile.Read(ref connection.SendPipelinedWithCallerTimeoutCalls)).IsEqualTo(0);
            await Assert.That(Volatile.Read(ref connection.SendPipelinedWithCallerTimeoutAfterWriteCalls)).IsEqualTo(0);
            await Assert.That(Volatile.Read(ref connection.SendPipelinedCalls)).IsEqualTo(0);

            tcs1.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 42));
            tcs2.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 43));
            await allAcknowledged.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_PipelinedProduce_ReleasesBufferMemoryOnlyAfterWriteCompletes(CancellationToken cancellationToken)
    {
        var responseTcs = new TaskCompletionSource<ProduceResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writeCanComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var connection = new TestKafkaConnection();

        async ValueTask<Task<ProduceResponse>> CompleteAfterWriteGate()
        {
            writeStarted.TrySetResult();
            await writeCanComplete.Task.ConfigureAwait(false);
            return responseTcs.Task;
        }

        connection.SendProducePipelinedAfterWrite = CompleteAfterWriteGate;

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);

        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var acknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, ex) =>
        {
            if (ex is null)
                acknowledged.TrySetResult();
        });

        try
        {
            var batch = CreateTestBatch(vtPool, "test-topic", 0, markMemoryReleased: false, dataSize: 0);
            await Assert.That(batch.MemoryReleased).IsFalse();

            sender.Enqueue(batch);
            await writeStarted.Task.WaitAsync(cancellationToken);

            await Assert.That(batch.MemoryReleased).IsFalse();
            await Assert.That(acknowledged.Task.IsCompleted).IsFalse();

            writeCanComplete.SetResult();
            await WaitUntilAsync(() => batch.MemoryReleased, cancellationToken);

            await Assert.That(acknowledged.Task.IsCompleted).IsFalse();

            responseTcs.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 42));
            await acknowledged.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_ResponseCompletion_WakesSendLoopAndProcessesBatch(CancellationToken cancellationToken)
    {
        // Verifies that when a response task completes, the response completion callback
        // pushes a ResponseCompleted event to the unified channel, waking the send loop.

        var tcs = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs);

        var requestSent = new TaskCompletionSource();
        var (pool, _) = CreateMockConnection(responseQueue, onSend: () => requestSent.TrySetResult());
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var acknowledged = new TaskCompletionSource<(long offset, int count)>();

        var sender = CreateSender(pool, options, accumulator, (tp, offset, _, count, ex) =>
        {
            if (ex is null)
                acknowledged.TrySetResult((offset, count));
        });

        try
        {
            var batch = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batch);

            // Wait for the send loop to actually send the request
            await requestSent.Task.WaitAsync(cancellationToken);

            // Complete the response — UnsafeOnCompleted callback pushes event, send loop wakes and processes
            tcs.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 42));

            // Wait for acknowledgement (with timeout to detect missed wakeup)
            var result = await acknowledged.Task.WaitAsync(cancellationToken);
            await Assert.That(result.offset).IsEqualTo(42);
            await Assert.That(result.count).IsEqualTo(1);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_InFlightLimitEnforced_SecondBatchWaitsForFirstResponse(CancellationToken cancellationToken)
    {
        // With maxInFlight=1 and two batches on different partitions, they coalesce into
        // one request. After the response completes, both batches are acknowledged.
        // This verifies in-flight limiting uses _pendingResponses.Count and that
        // response completion correctly wakes the send loop.

        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2);

        var requestSent = new TaskCompletionSource();
        var (pool, _) = CreateMockConnection(responseQueue, onSend: () => requestSent.TrySetResult());
        var options = CreateOptions(maxInFlight: 1);
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
            // Enqueue two batches on different partitions so both can coalesce
            // (same partition would cause carry-over due to one-per-partition rule)
            var batch1 = CreateTestBatch(vtPool, "test-topic", 0);
            var batch2 = CreateTestBatch(vtPool, "test-topic", 1);

            sender.Enqueue(batch1);
            sender.Enqueue(batch2);

            // Wait for the send loop to send the coalesced request
            await requestSent.Task.WaitAsync(cancellationToken);

            // Complete the first response — this should process both batches since
            // they were coalesced into one request
            tcs1.SetResult(new ProduceResponse
            {
                TopicCount = 1,
                Responses =
                [
                    new ProduceResponseTopicData
                    {
                        Name = "test-topic",
                        PartitionCount = 2,
                        PartitionResponses =
                        [
                            new ProduceResponsePartitionData
                            {
                                Index = 0,
                                ErrorCode = ErrorCode.None,
                                BaseOffset = 100
                            },
                            new ProduceResponsePartitionData
                            {
                                Index = 1,
                                ErrorCode = ErrorCode.None,
                                BaseOffset = 200
                            }
                        ]
                    }
                ]
            });

            // Wait for all acknowledgements
            await allAcknowledged.Task.WaitAsync(cancellationToken);

            await Assert.That(ackOffsets).Contains(100L);
            await Assert.That(ackOffsets).Contains(200L);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_SequentialRequests_SecondSendsAfterFirstResponseCompletes(CancellationToken cancellationToken)
    {
        // With maxInFlight=1, enqueue batches sequentially so each becomes its own
        // request. The second request can only be sent after the first response completes.
        // This verifies the in-flight wait loop correctly uses _pendingResponses.Count
        // and wakes via the event channel.

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
        var options = CreateOptions(maxInFlight: 1);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var allAcknowledged = new TaskCompletionSource();
        var ackCount = 0;

        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, ex) =>
        {
            if (ex is null && Interlocked.Increment(ref ackCount) >= 2)
                allAcknowledged.TrySetResult();
        });

        try
        {
            // Enqueue first batch — send loop sends it immediately
            var batch1 = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batch1);

            // Wait for first request to be sent (deterministic, no Task.Delay)
            await sendSignals[0].Task.WaitAsync(cancellationToken);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(1);

            // Enqueue second batch while first is still in-flight
            var batch2 = CreateTestBatch(vtPool, "test-topic", 1);
            sender.Enqueue(batch2);

            // Second request should NOT have been sent yet (in-flight limit).
            // Give a brief moment for the send loop to process, then verify.
            await Task.Delay(50, cancellationToken);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(1);

            // Complete first response — frees in-flight slot
            tcs1.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));

            // Wait for second request to be sent (deterministic)
            await sendSignals[1].Task.WaitAsync(cancellationToken);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(2);

            // Complete second response
            tcs2.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 200));

            await allAcknowledged.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_InFlightByteBudget_SecondLargeRequestWaitsForFirstResponse(
        CancellationToken cancellationToken)
    {
        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var secondResponse = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(
            [firstResponse, secondResponse]);
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        var sendCount = 0;

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });
        var options = CreateOptions(
            maxInFlight: 100,
            enableAdaptiveConnections: false,
            enableIdempotence: false,
            batchSize: 100);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { });

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0, dataSize: 5_000));
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 1, dataSize: 5_000));

            var observationWindow = Task.Delay(100, cancellationToken);
            var firstCompleted = await Task.WhenAny(sendSignals[1].Task, observationWindow);
            await Assert.That(firstCompleted).IsSameReferenceAs(observationWindow);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(1);

            firstResponse.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));
            await sendSignals[1].Task.WaitAsync(cancellationToken);
            secondResponse.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 200));
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_InFlightByteBudget_DoesNotBlockIdleConnection(
        CancellationToken cancellationToken)
    {
        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var secondResponse = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(
            [firstResponse, secondResponse]);
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        var sendCount = 0;

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });
        var options = CreateOptions(
            maxInFlight: 100,
            connectionsPerBroker: 2,
            enableAdaptiveConnections: false,
            enableIdempotence: false,
            batchSize: 100);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { });

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0, dataSize: 5_000));
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 1, dataSize: 5_000));
            await sendSignals[1].Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            firstResponse.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));
            secondResponse.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 200));
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_BlockedBucket_DoesNotPreventFreshWorkOnIdleConnection(
        CancellationToken cancellationToken)
    {
        var responses = new[]
        {
            new TaskCompletionSource<ProduceResponse>(),
            new TaskCompletionSource<ProduceResponse>(),
            new TaskCompletionSource<ProduceResponse>(),
            new TaskCompletionSource<ProduceResponse>()
        };
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(responses);
        var sendSignals = new[]
        {
            new TaskCompletionSource(),
            new TaskCompletionSource(),
            new TaskCompletionSource(),
            new TaskCompletionSource()
        };
        var sendCount = 0;

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });
        var options = CreateOptions(
            maxInFlight: 100,
            connectionsPerBroker: 2,
            enableAdaptiveConnections: false,
            enableIdempotence: false,
            batchSize: 100);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var blockedBucketRequeued = new TaskCompletionSource();
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, _) => { },
            onBlockedBucketRequeued: () => blockedBucketRequeued.TrySetResult());

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0, dataSize: 5_000));
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            // Partition 2 shares the saturated connection with partition 0. Partition 1
            // can send immediately, and partition 3 must remain serviceable afterward.
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 2, dataSize: 5_000));
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 1, dataSize: 10));
            await sendSignals[1].Task.WaitAsync(cancellationToken);

            await blockedBucketRequeued.Task.WaitAsync(cancellationToken);
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 3, dataSize: 10));
            await sendSignals[2].Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            responses[0].SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));
            responses[1].SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 200));
            responses[2].SetResult(CreateSuccessResponse("test-topic", 3, baseOffset: 300));
            await sendSignals[3].Task.WaitAsync(cancellationToken);
            responses[3].SetResult(CreateSuccessResponse("test-topic", 2, baseOffset: 400));
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_StaleAfterWrite_ByteAccountingSkipsNulledBatch(
        CancellationToken cancellationToken)
    {
        var responses = new[]
        {
            new TaskCompletionSource<ProduceResponse>(),
            new TaskCompletionSource<ProduceResponse>()
        };
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(responses);
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        ReadyBatch? staleBatch = null;
        var sendCount = 0;

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            if (index == 0)
                Interlocked.Exchange(ref staleBatch!._returnedToPool, 1);
            sendSignals[index].TrySetResult();
        });
        var options = CreateOptions(
            maxInFlight: 100,
            enableAdaptiveConnections: false,
            enableIdempotence: false);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var acknowledged = new TaskCompletionSource();
        var sender = CreateSender(pool, options, accumulator, (tp, _, _, _, ex) =>
        {
            if (tp.Partition == 1 && ex is null)
                acknowledged.TrySetResult();
        });

        try
        {
            staleBatch = CreateTestBatch(valueTaskSourcePool, "test-topic", 0);
            sender.Enqueue(staleBatch);
            await sendSignals[0].Task.WaitAsync(cancellationToken);
            responses[0].SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));

            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 1));
            await sendSignals[1].Task.WaitAsync(cancellationToken);
            responses[1].SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 200));
            await acknowledged.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_FaultedResponse_WakesSendLoopAndRetries(CancellationToken cancellationToken)
    {
        // When a response task faults (e.g., connection error), the response completion callback
        // pushes a ResponseCompleted event. The send loop processes it and retries the batch.

        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2); // For the retry

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

        var acknowledged = new TaskCompletionSource<long>();

        var sender = CreateSender(pool, options, accumulator, (_, offset, _, _, ex) =>
        {
            if (ex is null)
                acknowledged.TrySetResult(offset);
        });

        try
        {
            var batch = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batch);

            // Wait for first send (deterministic)
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            // Fault the response — send loop should wake and retry
            tcs1.SetException(new IOException("Connection reset"));

            // Wait for retry send (deterministic — send loop retries after backoff)
            await sendSignals[1].Task.WaitAsync(cancellationToken);

            // Complete the retry response
            tcs2.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 99));

            // Batch should eventually be acknowledged
            var offset = await acknowledged.Task.WaitAsync(cancellationToken);
            await Assert.That(offset).IsEqualTo(99);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task SendLoop_SendFailure_RefreshesMetadataAndReroutes(CancellationToken cancellationToken)
    {
        const string topic = "test-topic";
        var staleConnection = new TestKafkaConnection
        {
            SendProducePipelinedAfterWrite = () =>
                ValueTask.FromException<Task<ProduceResponse>>(new IOException("Leader connection closed"))
        };
        var metadataConnection = Substitute.For<IKafkaConnection>();
        var metadataRequests = 0;
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(1, Arg.Any<CancellationToken>())
            .Returns(staleConnection);
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(metadataConnection));
        metadataConnection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ApiVersionsResponse>(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(
                        ApiKey.Metadata,
                        MetadataRequest.LowestSupportedVersion,
                        MetadataRequest.HighestSupportedVersion)
                ]
            }));
        metadataConnection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref metadataRequests);
                return new ValueTask<MetadataResponse>(CreateLeaderMetadataResponse(topic, leaderId: 2, leaderEpoch: 2));
            });

        var options = CreateOptions(retryBackoffMs: 10, retryBackoffMaxMs: 10);
        var accumulator = new RecordAccumulator(options);
        await using var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        metadataManager.Metadata.Update(CreateLeaderMetadataResponse(topic, leaderId: 1, leaderEpoch: 1));
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var rerouted = new TaskCompletionSource<ReadyBatch>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            onAcknowledgement: (_, _, _, _, _) => { },
            metadataManager,
            rerouteBatch: (batch, _) => rerouted.TrySetResult(batch));

        try
        {
            var batch = CreateTestBatch(vtPool, topic, partition: 0);
            sender.Enqueue(batch);

            var reroutedBatch = await rerouted.Task.WaitAsync(cancellationToken);

            await Assert.That(reroutedBatch).IsSameReferenceAs(batch);
            await Assert.That(Volatile.Read(ref metadataRequests)).IsEqualTo(1);
            await Assert.That(metadataManager.Metadata.GetPartitionLeader(topic, 0)!.NodeId).IsEqualTo(2);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task SendLoop_CoalescedSendFailure_RefreshesMetadataOncePerTopic(
        CancellationToken cancellationToken)
    {
        const string topic = "test-topic";
        var staleConnection = new TestKafkaConnection
        {
            SendProducePipelinedAfterWrite = () =>
                ValueTask.FromException<Task<ProduceResponse>>(new IOException("Leader connection closed"))
        };
        var metadataConnection = Substitute.For<IKafkaConnection>();
        var metadataRequests = 0;
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(1, Arg.Any<CancellationToken>())
            .Returns(staleConnection);
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(metadataConnection));
        metadataConnection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ApiVersionsResponse>(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(
                        ApiKey.Metadata,
                        MetadataRequest.LowestSupportedVersion,
                        MetadataRequest.HighestSupportedVersion)
                ]
            }));
        metadataConnection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref metadataRequests);
                return new ValueTask<MetadataResponse>(CreateLeaderMetadataResponse(
                    topic, leaderId: 2, leaderEpoch: 2, partitionCount: 2));
            });

        var options = CreateOptions(retryBackoffMs: 10, retryBackoffMaxMs: 10);
        var accumulator = new RecordAccumulator(options);
        await using var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        metadataManager.Metadata.Update(CreateLeaderMetadataResponse(
            topic, leaderId: 1, leaderEpoch: 1, partitionCount: 2));
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var rerouted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reroutedCount = 0;
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            onAcknowledgement: (_, _, _, _, _) => { },
            metadataManager,
            rerouteBatch: (_, _) =>
            {
                if (Interlocked.Increment(ref reroutedCount) == 2)
                    rerouted.TrySetResult();
            });

        try
        {
            var firstBatch = CreateTestBatch(valueTaskSourcePool, topic, partition: 0);
            var secondBatch = CreateTestBatch(valueTaskSourcePool, topic, partition: 1);
            sender.EnqueueBulk(
            [
                firstBatch,
                secondBatch
            ]);

            await rerouted.Task.WaitAsync(cancellationToken);

            await Assert.That(Volatile.Read(ref metadataRequests)).IsEqualTo(1);
            await Assert.That(accumulator.IsMuted(firstBatch.TopicPartition)).IsFalse();
            await Assert.That(accumulator.IsMuted(secondBatch.TopicPartition)).IsFalse();
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task SendLoop_ParallelSendFailures_RefreshMetadataOncePerTopic(
        CancellationToken cancellationToken)
    {
        const string topic = "test-topic";
        var firstFailedConnection = new TestKafkaConnection
        {
            SendProducePipelinedAfterWrite = () =>
                ValueTask.FromException<Task<ProduceResponse>>(new IOException("First leader connection closed"))
        };
        var secondFailedConnection = new TestKafkaConnection
        {
            SendProducePipelinedAfterWrite = () =>
                ValueTask.FromException<Task<ProduceResponse>>(new IOException("Second leader connection closed"))
        };
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(firstFailedConnection));
        pool.GetConnectionByIndexAsync(1, 1, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(secondFailedConnection));

        var metadataConnection = Substitute.For<IKafkaConnection>();
        var metadataRequests = 0;
        var duplicateMetadataRequest = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(metadataConnection));
        metadataConnection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ApiVersionsResponse>(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(
                        ApiKey.Metadata,
                        MetadataRequest.LowestSupportedVersion,
                        MetadataRequest.HighestSupportedVersion)
                ]
            }));
        metadataConnection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref metadataRequests) > 1)
                    duplicateMetadataRequest.TrySetResult();

                return new ValueTask<MetadataResponse>(CreateLeaderMetadataResponse(
                    topic, leaderId: 2, leaderEpoch: 2, partitionCount: 2));
            });

        var options = CreateOptions(
            maxInFlight: 2,
            retryBackoffMs: 500,
            retryBackoffMaxMs: 500,
            connectionsPerBroker: 2);
        var accumulator = new RecordAccumulator(options);
        await using var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        metadataManager.Metadata.Update(CreateLeaderMetadataResponse(
            topic, leaderId: 1, leaderEpoch: 1, partitionCount: 2));
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var allRerouted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reroutedCount = 0;
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            onAcknowledgement: (_, _, _, _, _) => { },
            metadataManager,
            rerouteBatch: (_, _) =>
            {
                if (Interlocked.Increment(ref reroutedCount) == 2)
                    allRerouted.TrySetResult();
            });

        try
        {
            sender.EnqueueBulk(
            [
                CreateTestBatch(valueTaskSourcePool, topic, partition: 0),
                CreateTestBatch(valueTaskSourcePool, topic, partition: 1)
            ]);

            var firstResult = await Task.WhenAny(allRerouted.Task, duplicateMetadataRequest.Task)
                .WaitAsync(cancellationToken);

            await Assert.That(firstResult).IsSameReferenceAs(allRerouted.Task);
            await Assert.That(Volatile.Read(ref metadataRequests)).IsEqualTo(1);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task SendLoop_ParallelSendFailure_MutesPartitionBeforeSiblingWriteCompletes(
        CancellationToken cancellationToken)
    {
        const string topic = "test-topic";
        var failedConnection = new TestKafkaConnection
        {
            SendProducePipelinedAfterWrite = () =>
                ValueTask.FromException<Task<ProduceResponse>>(new IOException("Leader connection closed"))
        };
        var siblingWriteStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSiblingWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var siblingConnection = new TestKafkaConnection
        {
            SendProducePipelinedAfterWrite = async () =>
            {
                siblingWriteStarted.TrySetResult();
                await releaseSiblingWrite.Task;
                return Task.FromResult(CreateSuccessResponse(topic, partition: 1, baseOffset: 0));
            }
        };
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(failedConnection));
        pool.GetConnectionByIndexAsync(1, 1, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(siblingConnection));
        var metadataConnection = Substitute.For<IKafkaConnection>();
        var metadataRefreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(metadataConnection));
        metadataConnection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ApiVersionsResponse>(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(
                        ApiKey.Metadata,
                        MetadataRequest.LowestSupportedVersion,
                        MetadataRequest.HighestSupportedVersion)
                ]
            }));
        metadataConnection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                metadataRefreshStarted.TrySetResult();
                return new ValueTask<MetadataResponse>(CreateLeaderMetadataResponse(
                    topic, leaderId: 2, leaderEpoch: 2));
            });

        var options = CreateOptions(
            maxInFlight: 2,
            retryBackoffMs: 10_000,
            retryBackoffMaxMs: 10_000,
            connectionsPerBroker: 2);
        var accumulator = new RecordAccumulator(options);
        await using var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        metadataManager.Metadata.Update(CreateLeaderMetadataResponse(topic, leaderId: 1, leaderEpoch: 1));
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, _) => { },
            metadataManager);

        try
        {
            var failedBatch = CreateTestBatch(vtPool, topic, partition: 0);
            var siblingBatch = CreateTestBatch(vtPool, topic, partition: 1);
            sender.EnqueueBulk([failedBatch, siblingBatch]);

            await siblingWriteStarted.Task.WaitAsync(cancellationToken);
            await metadataRefreshStarted.Task.WaitAsync(cancellationToken);

            await Assert.That(accumulator.IsMuted(failedBatch.TopicPartition)).IsTrue();
            await Assert.That(failedBatch.RetryNotBefore).IsGreaterThan(0);
        }
        finally
        {
            releaseSiblingWrite.TrySetResult();
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_MultipleInFlight_AllResponsesProcessed(CancellationToken cancellationToken)
    {
        // With maxInFlight=5, multiple requests can be in-flight simultaneously.
        // Completing responses in any order should work because each response
        // pushes a ResponseCompleted event to the unified channel.

        const int batchCount = 3;
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        var tcsList = new List<TaskCompletionSource<ProduceResponse>>();
        for (var i = 0; i < batchCount; i++)
        {
            var tcs = new TaskCompletionSource<ProduceResponse>();
            tcsList.Add(tcs);
            responseQueue.Enqueue(tcs);
        }

        var sendCount = 0;
        var allSent = new TaskCompletionSource();

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            // Batches may coalesce into fewer requests, so signal when all TCSs are dequeued
            if (Interlocked.Increment(ref sendCount) >= 1)
                allSent.TrySetResult();
        });
        var options = CreateOptions(maxInFlight: 5);
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
                    if (ackOffsets.Count >= batchCount)
                        allAcknowledged.TrySetResult();
                }
            }
        });

        try
        {
            // Enqueue batches on different partitions
            for (var i = 0; i < batchCount; i++)
            {
                var batch = CreateTestBatch(vtPool, "test-topic", i);
                sender.Enqueue(batch);
            }

            // Wait for at least one send to occur (deterministic)
            await allSent.Task.WaitAsync(cancellationToken);

            // Complete responses. Batches may coalesce into fewer requests, so each
            // response must include ALL partition data. Complete in reverse order to
            // verify order-independent processing.
            //
            // Each TCS gets its OWN ProduceResponse instance. The send loop returns every
            // processed response to a shared pool (ProduceResponse.Return resets TopicCount
            // to 0). When the batches are sent as more than one request — a legal, timing-
            // dependent coalescing outcome — sharing a single instance across the TCSs would
            // let the first processed request blank the data before later requests read it,
            // leaving their batches unacknowledged and hanging the test.
            static ProduceResponse CreateCombinedResponse(int batchCount) => new()
            {
                TopicCount = 1,
                Responses =
                [
                    new ProduceResponseTopicData
                    {
                        Name = "test-topic",
                        PartitionCount = batchCount,
                        PartitionResponses = Enumerable.Range(0, batchCount)
                            .Select(i => new ProduceResponsePartitionData
                            {
                                Index = i,
                                ErrorCode = ErrorCode.None,
                                BaseOffset = (i + 1) * 100
                            }).ToArray()
                    }
                ]
            };
            for (var i = batchCount - 1; i >= 0; i--)
                tcsList[i].TrySetResult(CreateCombinedResponse(batchCount));

            await allAcknowledged.Task.WaitAsync(cancellationToken);

            await Assert.That(ackOffsets).Contains(100L);
            await Assert.That(ackOffsets).Contains(200L);
            await Assert.That(ackOffsets).Contains(300L);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_FireAndForget_CompletesWithoutPendingResponse(CancellationToken cancellationToken)
    {
        // With Acks.None, the send loop uses SendFireAndForgetAsync and completes
        // batches synchronously without adding to _pendingResponses.
        // Verifies fire-and-forget path works with UnsafeOnCompleted response callbacks.

        var connection = Substitute.For<IKafkaConnection>();
        connection.IsConnected.Returns(true);
        connection.BrokerId.Returns(1);
        connection.SendFireAndForgetWithCallerTimeoutAsync<ProduceRequest, ProduceResponse>(
                Arg.Any<ProduceRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);

        var options = CreateOptions(acks: Acks.None);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var acknowledged = new TaskCompletionSource<long>();

        var sender = CreateSender(pool, options, accumulator, (_, offset, _, _, ex) =>
        {
            acknowledged.TrySetResult(offset);
        });

        try
        {
            var batch = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batch);

            // Fire-and-forget should complete quickly without waiting for a response
            var offset = await acknowledged.Task.WaitAsync(cancellationToken);
            await Assert.That(offset).IsEqualTo(-1); // Fire-and-forget offset is -1
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_FireAndForget_StartsSecondWriteBeforeFirstFlushCompletes(CancellationToken cancellationToken)
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.IsConnected.Returns(true);
        connection.BrokerId.Returns(1);

        var firstWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWriteStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sendCount = 0;
        BrokerSender? sender = null;

        var options = CreateOptions(
            acks: Acks.None,
            maxInFlight: 5,
            enableIdempotence: false);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var secondBatch = CreateTestBatch(vtPool, "test-topic", 1);

        connection.SendFireAndForgetWithCallerTimeoutAsync<ProduceRequest, ProduceResponse>(
                Arg.Any<ProduceRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var current = Interlocked.Increment(ref sendCount);
                if (current == 1)
                {
                    sender!.Enqueue(secondBatch);
                    return new ValueTask(firstWrite.Task);
                }

                if (current == 2)
                {
                    secondWriteStarted.TrySetResult();
                    return new ValueTask(secondWrite.Task);
                }

                return ValueTask.CompletedTask;
            });

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);

        var acknowledgedCount = 0;
        var allAcknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        sender = CreateSender(pool, options, accumulator, (_, _, _, _, ex) =>
        {
            if (ex is null && Interlocked.Increment(ref acknowledgedCount) == 2)
                allAcknowledged.TrySetResult();
        });

        try
        {
            sender.Enqueue(CreateTestBatch(vtPool, "test-topic", 0));

            await secondWriteStarted.Task.WaitAsync(cancellationToken);

            await Assert.That(firstWrite.Task.IsCompleted).IsFalse();
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(2);

            firstWrite.SetResult();
            secondWrite.SetResult();

            await allAcknowledged.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            if (sender is not null)
                await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_FaultedResponseDuringInFlightWait_RetryBatchSurvivesCarryOverSwap(CancellationToken cancellationToken)
    {
        // Regression test for the carry-over list swap bug:
        // With maxInFlight=1, batch A is sent (fills the slot). Batch B arrives in the
        // next iteration, gets coalesced, and enters the in-flight capacity wait (line 529).
        // When batch A's response faults during the wait, ProcessCompletedResponses adds
        // the retry batch to a carry-over list. The bug was using pendingCarryOver (already
        // cleared at line 492) instead of newCarryOver — the retry batch was silently lost.
        //
        // This test verifies the retry batch survives: we should see 3 sends
        // (batch A, batch B after slot freed, batch A retry) and both partitions acknowledged.

        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var tcs3 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1); // Batch A — will fault
        responseQueue.Enqueue(tcs2); // Batch B — sent after slot freed
        responseQueue.Enqueue(tcs3); // Batch A retry

        var sendCount = 0;
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource(), new TaskCompletionSource() };

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var idx = Interlocked.Increment(ref sendCount) - 1;
            if (idx < sendSignals.Length)
                sendSignals[idx].TrySetResult();
        });
        var options = CreateOptions(maxInFlight: 1, retryBackoffMs: 0, retryBackoffMaxMs: 0);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var ackPartitions = new List<int>();
        var allAcknowledged = new TaskCompletionSource();

        var sender = CreateSender(pool, options, accumulator, (tp, offset, _, _, ex) =>
        {
            if (ex is null)
            {
                lock (ackPartitions)
                {
                    ackPartitions.Add(tp.Partition);
                    if (ackPartitions.Count >= 2)
                        allAcknowledged.TrySetResult();
                }
            }
        });

        try
        {
            // Enqueue batch A (partition 0) — send loop sends it immediately
            var batchA = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batchA);

            // Wait for batch A to be sent (fills in-flight slot)
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            // Enqueue batch B (partition 1) — send loop reads it, coalesces it,
            // but enters in-flight capacity wait (1 in-flight >= maxInFlight=1).
            // Fault batch A's response immediately after enqueue — the send loop
            // will either find it already faulted when entering the capacity wait
            // (ProcessCompletedResponses handles it inline), or wake up from the
            // fault if already waiting. Both paths exercise the carry-over fix.
            var batchB = CreateTestBatch(vtPool, "test-topic", 1);
            sender.Enqueue(batchB);
            tcs1.SetException(new IOException("Connection reset"));

            // Wait for batch B to be sent (slot freed by processing faulted response)
            await sendSignals[1].Task.WaitAsync(cancellationToken);

            // Complete batch B's response
            tcs2.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 200));

            // Wait for batch A retry to be sent (survives carry-over swap)
            await sendSignals[2].Task.WaitAsync(cancellationToken);

            // Complete the retry response
            tcs3.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));

            // Both partitions should be acknowledged
            await allAcknowledged.Task.WaitAsync(cancellationToken);

            await Assert.That(ackPartitions).Contains(0);
            await Assert.That(ackPartitions).Contains(1);

            // Verify all 3 sends occurred (batch A, batch B, batch A retry)
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(3);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_AlreadyCompletedResponse_ProcessedViaSynchronousFastPath(CancellationToken cancellationToken)
    {
        // Verifies the `if (responseTask.IsCompleted)` fast path in the send loop.
        // When the mock connection returns a Task that is already completed (via Task.FromResult),
        // the send loop should process the response synchronously without scheduling an
        // UnsafeOnCompleted callback. The batch should be acknowledged successfully.

        var successResponse = CreateSuccessResponse("test-topic", 0, baseOffset: 77);

        var connection = new TestKafkaConnection
        {
            SendProducePipelinedAfterWrite = () => new ValueTask<Task<ProduceResponse>>(Task.FromResult(successResponse))
        };

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);

        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var acknowledged = new TaskCompletionSource<(long offset, int count)>();

        var sender = CreateSender(pool, options, accumulator, (tp, offset, _, count, ex) =>
        {
            if (ex is null)
                acknowledged.TrySetResult((offset, count));
        });

        try
        {
            var batch = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batch);

            // The response is already complete, so acknowledgement should happen very quickly
            var result = await acknowledged.Task.WaitAsync(cancellationToken);
            await Assert.That(result.offset).IsEqualTo(77);
            await Assert.That(result.count).IsEqualTo(1);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_PipeliningEnabled_SendsSecondBatchBeforeFirstResponse(CancellationToken cancellationToken)
    {
        // With maxInFlight=2, after sending batch A (in-flight=1), the canPipeline guard
        // evaluates to true (sentThisIteration=true, carryOver empty, 1 < 2). The send loop
        // waits on the event channel instead of WaitForAnyResponseAsync. When batch B arrives
        // via a NewBatch event, the loop wakes and sends B immediately — before A's response
        // arrives. This verifies the pipelining optimization introduced in PR #704.

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
        var options = CreateOptions(maxInFlight: 2);
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
            // Send batch A (partition 0) — in-flight count becomes 1
            var batchA = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batchA);
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            // Batch A's response is still pending. canPipeline = true because:
            // sentThisIteration=true, carryOver.Count=0, waitPendingCount(1) < _totalMaxInFlight(2).
            // The send loop is now waiting on the event channel (not WaitForAnyResponseAsync).

            // Enqueue batch B (partition 1) — this pushes a NewBatch event to the channel,
            // waking the send loop. It should send B immediately without waiting for A's response.
            var batchB = CreateTestBatch(vtPool, "test-topic", 1);
            sender.Enqueue(batchB);

            // Wait for batch B to be sent — this proves pipelining worked: B was sent
            // while A's response was still pending.
            await sendSignals[1].Task.WaitAsync(cancellationToken);

            // Both requests are now in-flight. Verify A's response hasn't arrived yet.
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(2);

            // Complete both responses
            tcs1.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));
            tcs2.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 200));

            await allAcknowledged.Task.WaitAsync(cancellationToken);

            await Assert.That(ackOffsets).Contains(100L);
            await Assert.That(ackOffsets).Contains(200L);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_HungResponseWithExpiredBatches_FreesCapacitySlot(CancellationToken cancellationToken)
    {
        // Regression test: when a response task never completes (hung connection),
        // HandleTimedOutRequests (v4) directly processes all batches from _pendingResponses
        // and clears the list — freeing the capacity slot without relying on connection
        // disposal. The response task is orphaned (nobody polls it).
        //
        // Scenario: maxInFlight=1, batch A is sent, response hangs. Request timeout
        // fires (1s). HandleTimedOutRequests processes batch A directly (delivery timeout
        // also exceeded → permanently failed), clears _pendingResponses. Second batch B
        // should then be sendable.

        var tcs1 = new TaskCompletionSource<ProduceResponse>(); // Simulates hung connection
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

        // Short request timeout (1s) so HandleTimedOutRequests fires quickly.
        // Delivery timeout also 1s so the batch is permanently failed rather than retried.
        var options = CreateOptions(maxInFlight: 1, deliveryTimeoutMs: 1_000, requestTimeoutMs: 1_000);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var batch1Failed = new TaskCompletionSource();
        var acknowledged = new TaskCompletionSource<long>();

        var sender = CreateSender(pool, options, accumulator, (_, offset, _, _, ex) =>
        {
            if (ex is not null)
                batch1Failed.TrySetResult();
            else
                acknowledged.TrySetResult(offset);
        });

        try
        {
            // Send first batch — response will hang
            var batch1 = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batch1);
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            // Wait for HandleTimedOutRequests to process batch1 (request timeout 1s +
            // delivery timeout 1s exceeded → permanently failed). Uses deterministic
            // signal instead of Task.Delay to avoid flakiness on slow CI runners.
            await batch1Failed.Task.WaitAsync(cancellationToken);

            // Send second batch on different partition — should not hang
            // (HandleTimedOutRequests cleared _pendingResponses, freeing the capacity slot)
            var batch2 = CreateTestBatch(vtPool, "test-topic", 1);
            sender.Enqueue(batch2);

            // Second batch should be sent (send loop freed the capacity slot)
            await sendSignals[1].Task.WaitAsync(cancellationToken);

            // Complete second response and verify acknowledgement
            tcs2.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 42));
            var offset = await acknowledged.Task.WaitAsync(cancellationToken);
            await Assert.That(offset).IsEqualTo(42);
        }
        finally
        {
            // tcs1 is orphaned (HandleTimedOutRequests doesn't await it), cancel to avoid
            // unobserved task exception during test cleanup.
            tcs1.TrySetCanceled(CancellationToken.None);
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Arguments(5, 100, 5)]
    [Arguments(20, 100, 20)]
    [Arguments(250, 100, 100)]
    [Arguments(20, 7, 7)]
    public async Task ComputeThrottledResponseWaitMs_BoundsEveryDeadline(
        int throttleDelayMs,
        int batchDeadlineMs,
        int expectedWaitMs)
    {
        var waitMs = BrokerSender.ComputeThrottledResponseWaitMs(
            throttleDelayMs,
            batchDeadlineMs);

        await Assert.That(waitMs).IsEqualTo(expectedWaitMs);
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_ThrottleObservedDuringConnectionCapacityWait_DelaysRemainingBucket(
        CancellationToken cancellationToken)
    {
        const int throttleTimeMs = 250;

        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var secondResponse = new TaskCompletionSource<ProduceResponse>();
        var thirdResponse = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(
            [firstResponse, secondResponse, thirdResponse]);
        var sendSignals = new[]
        {
            new TaskCompletionSource(),
            new TaskCompletionSource(),
            new TaskCompletionSource()
        };
        var sendCount = 0;
        var connection0 = new TestKafkaConnection
        {
            SendProducePipelinedAfterWrite = () =>
            {
                var response = responseQueue.Dequeue().Task;
                var index = Interlocked.Increment(ref sendCount) - 1;
                sendSignals[index].TrySetResult();
                return new ValueTask<Task<ProduceResponse>>(response);
            }
        };
        var connection1 = new TestKafkaConnection();
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection0);
        pool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new ValueTask<IKafkaConnection>(
                (int)callInfo[1]! == 0 ? connection0 : connection1));

        var options = CreateOptions(
            maxInFlight: 2,
            connectionsPerBroker: 2,
            enableAdaptiveConnections: false);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var throttleWaitStarted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseThrottleWait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timestamp = 0L;
        var acknowledgements = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acknowledgementCount = 0;
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, error) =>
            {
                if (error is null && Interlocked.Increment(ref acknowledgementCount) == 3)
                    acknowledgements.TrySetResult();
            },
            getTimestamp: () => Volatile.Read(ref timestamp),
            delayForThrottle: (delayMs, token) =>
            {
                throttleWaitStarted.TrySetResult(delayMs);
                return new ValueTask(releaseThrottleWait.Task.WaitAsync(token));
            });

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0));
            await sendSignals[0].Task.WaitAsync(cancellationToken);
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 2));
            await sendSignals[1].Task.WaitAsync(cancellationToken);

            var delayedBatch = CreateTestBatch(valueTaskSourcePool, "test-topic", 4);
            delayedBatch.EnableDeliveryDiagnostics();
            sender.Enqueue(delayedBatch);
            await WaitUntilAsync(() => delayedBatch.DiagTrace.Contains('S'), cancellationToken);

            firstResponse.SetResult(CreateSuccessResponse(
                "test-topic", 0, baseOffset: 10, throttleTimeMs));
            secondResponse.SetResult(CreateSuccessResponse("test-topic", 2, baseOffset: 11));

            var firstProgress = await Task.WhenAny(throttleWaitStarted.Task, sendSignals[2].Task)
                .WaitAsync(cancellationToken);
            await Assert.That(firstProgress).IsSameReferenceAs(throttleWaitStarted.Task);
            await Assert.That(await throttleWaitStarted.Task).IsEqualTo(throttleTimeMs);
            await Assert.That(sendSignals[2].Task.IsCompleted).IsFalse();

            Volatile.Write(ref timestamp, Stopwatch.Frequency);
            releaseThrottleWait.SetResult();

            await sendSignals[2].Task.WaitAsync(cancellationToken);
            thirdResponse.SetResult(CreateSuccessResponse("test-topic", 4, baseOffset: 12));
            await acknowledgements.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_PositiveBrokerThrottle_DelaysNextRequestWithoutDeliveryErrors(
        CancellationToken cancellationToken)
    {
        const int throttleTimeMs = 250;

        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var secondResponse = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>([firstResponse, secondResponse]);
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        var sendCount = 0;
        var (pool, _) = CreateMockConnection(responseQueue, () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });

        var options = CreateOptions(maxInFlight: 1);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var throttleWaitStarted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseThrottleWait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedThrottleTimes = new List<int>();
        var timestamp = 0L;
        var acknowledgements = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acknowledgementCount = 0;

        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, error) =>
            {
                if (error is null && Interlocked.Increment(ref acknowledgementCount) == 2)
                    acknowledgements.TrySetResult();
            },
            onBrokerThrottle: observedThrottleTimes.Add,
            getTimestamp: () => Volatile.Read(ref timestamp),
            delayForThrottle: (delayMs, token) =>
            {
                throttleWaitStarted.TrySetResult(delayMs);
                return new ValueTask(releaseThrottleWait.Task.WaitAsync(token));
            });

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0));
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 1));
            firstResponse.SetResult(CreateSuccessResponse(
                "test-topic", 0, baseOffset: 10, throttleTimeMs));

            var requestedDelayMs = await throttleWaitStarted.Task.WaitAsync(cancellationToken);
            await Assert.That(requestedDelayMs).IsEqualTo(throttleTimeMs);
            await Assert.That(sendSignals[1].Task.IsCompleted).IsFalse();

            Volatile.Write(ref timestamp, Stopwatch.Frequency);
            releaseThrottleWait.SetResult();

            await sendSignals[1].Task.WaitAsync(cancellationToken);
            secondResponse.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 11));
            await acknowledgements.Task.WaitAsync(cancellationToken);

            await Assert.That(observedThrottleTimes).IsEquivalentTo([throttleTimeMs, 0]);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(2);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_ZeroBrokerThrottle_DoesNotDelayNextRequest(
        CancellationToken cancellationToken)
    {
        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var secondResponse = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>([firstResponse, secondResponse]);
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        var sendCount = 0;
        var (pool, _) = CreateMockConnection(responseQueue, () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });

        var options = CreateOptions(maxInFlight: 1);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var throttleDelayCalled = false;
        var observedThrottleTimes = new List<int>();
        var acknowledgements = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acknowledgementCount = 0;

        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, error) =>
            {
                if (error is null && Interlocked.Increment(ref acknowledgementCount) == 2)
                    acknowledgements.TrySetResult();
            },
            onBrokerThrottle: observedThrottleTimes.Add,
            delayForThrottle: (_, _) =>
            {
                throttleDelayCalled = true;
                return ValueTask.CompletedTask;
            });

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0));
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 1));
            firstResponse.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 20));

            await sendSignals[1].Task.WaitAsync(cancellationToken);
            secondResponse.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 21));
            await acknowledgements.Task.WaitAsync(cancellationToken);

            await Assert.That(throttleDelayCalled).IsFalse();
            await Assert.That(observedThrottleTimes).IsEquivalentTo([0, 0]);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(2);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_InjectedClockControlsThrottleWakeupAndDeliveryExpiry(
        CancellationToken cancellationToken)
    {
        const int deliveryTimeoutMs = 10_000;
        const int throttleTimeMs = 20_000;

        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var secondResponse = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>([firstResponse, secondResponse]);
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        var sendCount = 0;
        var (pool, _) = CreateMockConnection(responseQueue, () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });

        var options = CreateOptions(maxInFlight: 1, deliveryTimeoutMs: deliveryTimeoutMs);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var expiringBatch = CreateTestBatch(valueTaskSourcePool, "test-topic", 1);
        var fakeTimestamp = expiringBatch.StopwatchCreatedTicks + options.DeliveryTimeoutTicks / 2;
        var firstDelayRequested = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var unexpectedSecondDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondDelayGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deliveryFailure = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var delayCount = 0;

        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (topicPartition, _, _, _, error) =>
            {
                if (topicPartition.Partition == 1 && error is not null)
                    deliveryFailure.TrySetResult(error);
            },
            getTimestamp: () => Volatile.Read(ref fakeTimestamp),
            delayForThrottle: (delayMs, token) =>
            {
                if (Interlocked.Increment(ref delayCount) == 1)
                {
                    firstDelayRequested.TrySetResult(delayMs);
                    Volatile.Write(
                        ref fakeTimestamp,
                        expiringBatch.StopwatchCreatedTicks + options.DeliveryTimeoutTicks);
                    return ValueTask.CompletedTask;
                }

                unexpectedSecondDelay.TrySetResult();
                return new ValueTask(secondDelayGate.Task.WaitAsync(token));
            });

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0));
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            sender.Enqueue(expiringBatch);
            firstResponse.SetResult(CreateSuccessResponse(
                "test-topic", 0, baseOffset: 10, throttleTimeMs));

            await Assert.That(await firstDelayRequested.Task.WaitAsync(cancellationToken))
                .IsEqualTo(deliveryTimeoutMs / 2);

            var progress = await Task.WhenAny(deliveryFailure.Task, unexpectedSecondDelay.Task)
                .WaitAsync(cancellationToken);
            await Assert.That(progress).IsSameReferenceAs(deliveryFailure.Task);
            await Assert.That(await deliveryFailure.Task).IsTypeOf<KafkaTimeoutException>();
            await Assert.That(sendSignals[1].Task.IsCompleted).IsFalse();
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    public async Task Constructor_AppliesUnackedBudgetCap()
    {
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        var (pool, _) = CreateMockConnection(responseQueue);
        var options = CreateOptions(unackedByteBudgetCapOverride: 4_096);
        var accumulator = new RecordAccumulator(options);
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 100, initialCapBytes: 1);
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { }, unackedBudget: budget);

        try
        {
            // Cold start: budget follows the cap the sender applied at construction.
            await Assert.That(budget.BudgetBytes).IsEqualTo(4_096);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task ProcessCompletedResponses_FeedsAckedBytesIntoUnackedBudget(
        CancellationToken cancellationToken)
    {
        const int batchCount = 1;
        const int dataSize = 5_000;

        var responses = Enumerable.Range(0, batchCount)
            .Select(_ => new TaskCompletionSource<ProduceResponse>())
            .ToArray();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(responses);
        var sendSignals = Enumerable.Range(0, batchCount)
            .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        var sendCount = 0;

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });

        // One successful request must feed its encoded bytes and request RTT into the budget.
        var options = CreateOptions(maxInFlight: 1, unackedByteBudgetCapOverride: 1_000_000);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.000001, floorBytes: 200, initialCapBytes: 1);
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { },
            unackedBudget: budget);

        try
        {
            for (var i = 0; i < batchCount; i++)
                sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0, dataSize: dataSize));

            for (var i = 0; i < batchCount; i++)
            {
                await sendSignals[i].Task.WaitAsync(cancellationToken);
                responses[i].SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: i * 10));
            }

            // The deliberately tiny target makes the 1.5 × RTT safety horizon dominate,
            // cancelling RTT from bytes/RTT × 1.5×RTT: budget = 5,000 × 1.5 = 7,500.
            await WaitUntilAsync(() => budget.BudgetBytes != 1_000_000, cancellationToken);
            await Assert.That(budget.BudgetBytes).IsBetween(7_490, 7_510);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task ProcessCompletedResponses_AggregatesConcurrentAckedBytes(
        CancellationToken cancellationToken)
    {
        const int dataSize = 5_000;
        var responses = Enumerable.Range(0, 2)
            .Select(_ => new TaskCompletionSource<ProduceResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(responses);
        var sendSignals = Enumerable.Range(0, 2)
            .Select(_ => new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        var sendCount = 0;
        var (pool, _) = CreateMockConnection(responseQueue, () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });
        var options = CreateOptions(maxInFlight: 2, unackedByteBudgetCapOverride: 1_000_000);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.000001,
            floorBytes: 200,
            initialCapBytes: 1);
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, _) => { },
            unackedBudget: budget);

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0, dataSize: dataSize));
            await sendSignals[0].Task.WaitAsync(cancellationToken);
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 1, dataSize: dataSize));
            await sendSignals[1].Task.WaitAsync(cancellationToken);

            // RunContinuationsAsynchronously keeps both completions available to the next
            // response pass, exercising broker-wide aggregation across concurrent requests.
            responses[0].SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 10));
            responses[1].SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 20));

            await WaitUntilAsync(() => budget.BudgetBytes != 1_000_000, cancellationToken);

            // 10,000 bytes / oldest request RTT × 1.5 × the same RTT = 15,000.
            await Assert.That(budget.BudgetBytes).IsBetween(14_980, 15_020);
        }
        finally
        {
            responses[0].TrySetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 10));
            responses[1].TrySetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 20));
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task FaultedResponses_DoNotFeedUnackedBudgetDrainRate(
        CancellationToken cancellationToken)
    {
        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var retryResponse = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(
            [firstResponse, retryResponse]);
        var sendSignals = new[]
        {
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var sendCount = 0;

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });
        var options = CreateOptions(maxInFlight: 1, unackedByteBudgetCapOverride: 1_000_000);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1);
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { },
            unackedBudget: budget);

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0, dataSize: 5_000));
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            firstResponse.SetException(new IOException("connection reset"));

            // The retry send proves the faulted response was fully processed — and a faulted
            // response must not count as drain, so the budget still has no rate sample.
            await sendSignals[1].Task.WaitAsync(cancellationToken);
            await Assert.That(budget.BudgetBytes).IsEqualTo(1_000_000);

            retryResponse.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 10));
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task PartitionErrors_DoNotFeedUnackedBudgetDrainRate(
        CancellationToken cancellationToken)
    {
        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var retryResponse = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(
            [firstResponse, retryResponse]);
        var sendSignals = new[]
        {
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var sendCount = 0;
        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });
        var options = CreateOptions(maxInFlight: 1, retryBackoffMs: 0,
            unackedByteBudgetCapOverride: 1_000_000);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1);
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { },
            unackedBudget: budget);

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0, dataSize: 5_000));
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            firstResponse.SetResult(CreateErrorResponse("test-topic", 0, ErrorCode.RequestTimedOut));

            // Retry proves the partition error was processed. It must not establish a
            // successful drain-rate sample from the failed request's encoded bytes.
            await sendSignals[1].Task.WaitAsync(cancellationToken);
            await Assert.That(budget.BudgetBytes).IsEqualTo(1_000_000);

            retryResponse.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 10));
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task AdmissionBlockPressure_TriggersScaleUp_WithoutBufferUtilization(
        CancellationToken cancellationToken)
    {
        const int partitionCount = 4;

        var options = CreateOptions(maxInFlight: 1,
            unackedByteBudgetCapOverride: 1_000_000, scaleCooldownMsOverride: 0);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var connection = new TestKafkaConnection();
        connection.SendProducePipelinedAfterWrite = () => new ValueTask<Task<ProduceResponse>>(
            Task.FromResult(CreateSuccessResponseForPartitions("test-topic", partitionCount)));

        var (pool, scaleRequested) = CreateScaleTrackingPool(connection);

        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1);
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { },
            unackedBudget: budget);

        try
        {
            // Simulate a workload throttled by the admission gate: buffer utilization stays
            // near zero, but blocked admissions accumulate. This alone must trigger scale-up.
            for (var i = 0; i < 200; i++)
                budget.RecordAdmissionBlock();

            // Keep the send loop iterating so it reaches the scale check.
            for (var i = 0; i < 32; i++)
                sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", i % partitionCount));

            var targetCount = await scaleRequested.Task.WaitAsync(cancellationToken);
            await Assert.That(targetCount).IsGreaterThan(1);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }
}
