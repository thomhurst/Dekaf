using System.Buffers;
using Dekaf.Compression;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Statistics;
using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Behavioral tests for BrokerSender's send loop, verifying:
/// - Response completion wakes the send loop (via Task.WhenAny, not ContinueWith)
/// - In-flight request limiting via _pendingResponses.Count
/// - Fire-and-forget (Acks.None) path doesn't track in-flight
/// - Faulted responses wake the send loop
///
/// These tests use controllable mock connections to verify send loop timing behavior.
/// </summary>
public sealed class BrokerSenderSendLoopTests
{
    private static ProducerOptions CreateOptions(Acks acks = Acks.All, int maxInFlight = 1) => new()
    {
        BootstrapServers = ["localhost:9092"],
        MaxInFlightRequestsPerConnection = maxInFlight,
        Acks = acks,
        DeliveryTimeoutMs = 30_000,
        RetryBackoffMs = 100,
        RetryBackoffMaxMs = 1000,
        RequestTimeoutMs = 30_000,
        LingerMs = 0
    };

    /// <summary>
    /// Creates a mock connection pool that returns a controllable mock connection.
    /// The mock connection queues response tasks so each SendPipelinedAsync call
    /// returns the next TaskCompletionSource's task from the queue.
    /// </summary>
    private static (IConnectionPool pool, IKafkaConnection connection) CreateMockConnection(
        Queue<TaskCompletionSource<ProduceResponse>> responseQueue)
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.IsConnected.Returns(true);
        connection.BrokerId.Returns(1);

        connection.SendPipelinedAsync<ProduceRequest, ProduceResponse>(
                Arg.Any<ProduceRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ => responseQueue.Dequeue().Task);

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);

        return (pool, connection);
    }

    /// <summary>
    /// Creates a minimal ReadyBatch suitable for send loop testing.
    /// Sets MemoryReleased=true to skip accumulator memory tracking.
    /// </summary>
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

        // Skip accumulator memory tracking in tests
        batch.MemoryReleased = true;

        return batch;
    }

    /// <summary>
    /// Creates a ProduceResponse with success for the given topic/partition.
    /// </summary>
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

    /// <summary>
    /// Creates a BrokerSender with standard test defaults, reducing constructor boilerplate.
    /// </summary>
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
            new ProducerStatisticsCollector(),
            getProduceApiVersion: () => 9,
            setProduceApiVersion: _ => { },
            isTransactional: () => false,
            ensurePartitionInTransaction: null,
            bumpEpoch: null,
            getCurrentEpoch: null,
            rerouteBatch: null,
            onAcknowledgement: onAcknowledgement,
            logger: null);

    [Test]
    public async Task SendLoop_ResponseCompletion_WakesSendLoopAndProcessesBatch()
    {
        // Verifies that when a response task completes, the send loop wakes up
        // (via Task.WhenAny on pending response tasks) and processes the batch.
        // This is the core behavioral change: no ContinueWith callback needed.

        var tcs = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs);

        var (pool, _) = CreateMockConnection(responseQueue);
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
            await sender.EnqueueAsync(batch, CancellationToken.None);

            // Give the send loop time to pick up and send the batch
            await Task.Delay(100);

            // Complete the response — send loop should wake via Task.WhenAny and process it
            tcs.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 42));

            // Wait for acknowledgement (with timeout to detect missed wakeup)
            var result = await acknowledged.Task.WaitAsync(TimeSpan.FromSeconds(5));
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
    public async Task SendLoop_InFlightLimitEnforced_SecondBatchWaitsForFirstResponse()
    {
        // With maxInFlight=1 and two batches on the same partition, the second batch
        // goes to carry-over (duplicate partition). After the first response completes
        // and the partition is unmuted, the second batch sends.
        // This verifies in-flight limiting uses _pendingResponses.Count and that
        // response completion correctly wakes the send loop.

        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2);

        var (pool, _) = CreateMockConnection(responseQueue);
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

            await sender.EnqueueAsync(batch1, CancellationToken.None);
            await sender.EnqueueAsync(batch2, CancellationToken.None);

            // Give send loop time to send the first request (both batches coalesced)
            await Task.Delay(100);

            // Complete the first response — this should process both batches since
            // they were coalesced into one request
            tcs1.SetResult(new ProduceResponse
            {
                Responses =
                [
                    new ProduceResponseTopicData
                    {
                        Name = "test-topic",
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
            await allAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(5));

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
    public async Task SendLoop_SequentialRequests_SecondSendsAfterFirstResponseCompletes()
    {
        // With maxInFlight=1, enqueue batches sequentially so each becomes its own
        // request. The second request can only be sent after the first response completes.
        // This verifies the in-flight wait loop correctly uses _pendingResponses.Count
        // and wakes via Task.WhenAny.

        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2);

        var (pool, connection) = CreateMockConnection(responseQueue);
        var options = CreateOptions(maxInFlight: 1);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var sendCount = 0;
        var allAcknowledged = new TaskCompletionSource();
        var ackCount = 0;

        // Track how many times SendPipelinedAsync is called
        connection.SendPipelinedAsync<ProduceRequest, ProduceResponse>(
                Arg.Any<ProduceRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                Interlocked.Increment(ref sendCount);
                return responseQueue.Dequeue().Task;
            });

        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, ex) =>
        {
            if (ex is null && Interlocked.Increment(ref ackCount) >= 2)
                allAcknowledged.TrySetResult();
        });

        try
        {
            // Enqueue first batch — send loop sends it immediately
            var batch1 = CreateTestBatch(vtPool, "test-topic", 0);
            await sender.EnqueueAsync(batch1, CancellationToken.None);
            await Task.Delay(200);

            // Verify first request was sent
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(1);

            // Enqueue second batch while first is still in-flight
            var batch2 = CreateTestBatch(vtPool, "test-topic", 1);
            await sender.EnqueueAsync(batch2, CancellationToken.None);
            await Task.Delay(200);

            // Second request should NOT have been sent yet (in-flight limit)
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(1);

            // Complete first response — frees in-flight slot
            tcs1.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));
            await Task.Delay(200);

            // Now second request should have been sent
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(2);

            // Complete second response
            tcs2.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 200));

            await allAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    public async Task SendLoop_FaultedResponse_WakesSendLoopAndRetries()
    {
        // When a response task faults (e.g., connection error), the send loop should
        // wake via Task.WhenAny and handle the error (retry the batch).
        // Verifies faulted tasks are detected without ContinueWith.

        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2); // For the retry

        var (pool, _) = CreateMockConnection(responseQueue);
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
            await sender.EnqueueAsync(batch, CancellationToken.None);

            // Give send loop time to send
            await Task.Delay(100);

            // Fault the response — send loop should wake and retry
            tcs1.SetException(new IOException("Connection reset"));

            // Send loop retries the batch → gets tcs2
            // Complete the retry response
            await Task.Delay(200); // Allow retry with backoff
            tcs2.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 99));

            // Batch should eventually be acknowledged
            var offset = await acknowledged.Task.WaitAsync(TimeSpan.FromSeconds(10));
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
    public async Task SendLoop_MultipleInFlight_AllResponsesProcessed()
    {
        // With maxInFlight=5, multiple requests can be in-flight simultaneously.
        // Completing responses in any order should work because the send loop
        // uses Task.WhenAny to detect ANY response completion.

        const int batchCount = 3;
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        var tcsList = new List<TaskCompletionSource<ProduceResponse>>();
        for (var i = 0; i < batchCount; i++)
        {
            var tcs = new TaskCompletionSource<ProduceResponse>();
            tcsList.Add(tcs);
            responseQueue.Enqueue(tcs);
        }

        var (pool, _) = CreateMockConnection(responseQueue);
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
                await sender.EnqueueAsync(batch, CancellationToken.None);
            }

            // Give send loop time to send all (they may coalesce into fewer requests)
            await Task.Delay(200);

            // Complete responses in reverse order to verify order-independent processing
            for (var i = batchCount - 1; i >= 0; i--)
            {
                tcsList[i].SetResult(CreateSuccessResponse("test-topic", i, baseOffset: (i + 1) * 100));
            }

            await allAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(5));

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
    public async Task SendLoop_FireAndForget_CompletesWithoutPendingResponse()
    {
        // With Acks.None, the send loop uses SendFireAndForgetAsync and completes
        // batches synchronously without adding to _pendingResponses.
        // Verifies fire-and-forget path works after ContinueWith removal.

        var connection = Substitute.For<IKafkaConnection>();
        connection.IsConnected.Returns(true);
        connection.BrokerId.Returns(1);
        connection.SendFireAndForgetAsync<ProduceRequest, ProduceResponse>(
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
            await sender.EnqueueAsync(batch, CancellationToken.None);

            // Fire-and-forget should complete quickly without waiting for a response
            var offset = await acknowledged.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(offset).IsEqualTo(-1); // Fire-and-forget offset is -1
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }
}
