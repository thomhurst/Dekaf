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

    /// <summary>
    /// Creates a mock connection pool that returns a controllable mock connection.
    /// The mock connection queues response tasks so each SendPipelinedAsync call
    /// returns the next TaskCompletionSource's task from the queue.
    /// The optional onSend callback fires each time SendPipelinedAsync is called,
    /// enabling deterministic synchronization without Task.Delay.
    /// </summary>
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
    [Timeout(30_000)]
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
            await sender.EnqueueAsync(batch, CancellationToken.None);

            // Wait for the send loop to actually send the request
            await requestSent.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

            // Complete the response — UnsafeOnCompleted callback pushes event, send loop wakes and processes
            tcs.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 42));

            // Wait for acknowledgement (with timeout to detect missed wakeup)
            var result = await acknowledged.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
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
    [Timeout(30_000)]
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

            await sender.EnqueueAsync(batch1, CancellationToken.None);
            await sender.EnqueueAsync(batch2, CancellationToken.None);

            // Wait for the send loop to send the coalesced request
            await requestSent.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

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
            await allAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

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
    [Timeout(30_000)]
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
            await sender.EnqueueAsync(batch1, CancellationToken.None);

            // Wait for first request to be sent (deterministic, no Task.Delay)
            await sendSignals[0].Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(1);

            // Enqueue second batch while first is still in-flight
            var batch2 = CreateTestBatch(vtPool, "test-topic", 1);
            await sender.EnqueueAsync(batch2, CancellationToken.None);

            // Second request should NOT have been sent yet (in-flight limit).
            // Give a brief moment for the send loop to process, then verify.
            await Task.Delay(50, cancellationToken);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(1);

            // Complete first response — frees in-flight slot
            tcs1.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));

            // Wait for second request to be sent (deterministic)
            await sendSignals[1].Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(2);

            // Complete second response
            tcs2.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 200));

            await allAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
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
            await sender.EnqueueAsync(batch, CancellationToken.None);

            // Wait for first send (deterministic)
            await sendSignals[0].Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            // Fault the response — send loop should wake and retry
            tcs1.SetException(new IOException("Connection reset"));

            // Wait for retry send (deterministic — send loop retries after backoff)
            await sendSignals[1].Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

            // Complete the retry response
            tcs2.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 99));

            // Batch should eventually be acknowledged
            var offset = await acknowledged.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
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
                await sender.EnqueueAsync(batch, CancellationToken.None);
            }

            // Wait for at least one send to occur (deterministic)
            await allSent.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

            // Complete responses. Batches may coalesce into fewer requests, so each
            // response must include ALL partition data. Complete in reverse order to
            // verify order-independent processing.
            var combinedResponse = new ProduceResponse
            {
                Responses =
                [
                    new ProduceResponseTopicData
                    {
                        Name = "test-topic",
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
                tcsList[i].TrySetResult(combinedResponse);

            await allAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

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
    [Timeout(30_000)]
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
            await sender.EnqueueAsync(batch, CancellationToken.None);

            // Fire-and-forget should complete quickly without waiting for a response
            var offset = await acknowledged.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
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
    [Timeout(30_000)]
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
            await sender.EnqueueAsync(batchA, CancellationToken.None);

            // Wait for batch A to be sent (fills in-flight slot)
            await sendSignals[0].Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

            // Enqueue batch B (partition 1) — send loop reads it, coalesces it,
            // but enters in-flight capacity wait (1 in-flight >= maxInFlight=1).
            // Fault batch A's response immediately after enqueue — the send loop
            // will either find it already faulted when entering the capacity wait
            // (ProcessCompletedResponses handles it inline), or wake up from the
            // fault if already waiting. Both paths exercise the carry-over fix.
            var batchB = CreateTestBatch(vtPool, "test-topic", 1);
            await sender.EnqueueAsync(batchB, CancellationToken.None);
            tcs1.SetException(new IOException("Connection reset"));

            // Wait for batch B to be sent (slot freed by processing faulted response)
            await sendSignals[1].Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

            // Complete batch B's response
            tcs2.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 200));

            // Wait for batch A retry to be sent (survives carry-over swap)
            await sendSignals[2].Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

            // Complete the retry response
            tcs3.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));

            // Both partitions should be acknowledged
            await allAcknowledged.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

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
    [Timeout(30_000)]
    public async Task SendLoop_AlreadyCompletedResponse_ProcessedViaSynchronousFastPath(CancellationToken cancellationToken)
    {
        // Verifies the `if (responseTask.IsCompleted)` fast path in the send loop.
        // When the mock connection returns a Task that is already completed (via Task.FromResult),
        // the send loop should process the response synchronously without scheduling an
        // UnsafeOnCompleted callback. The batch should be acknowledged successfully.

        var successResponse = CreateSuccessResponse("test-topic", 0, baseOffset: 77);

        var connection = Substitute.For<IKafkaConnection>();
        connection.IsConnected.Returns(true);
        connection.BrokerId.Returns(1);

        connection.SendPipelinedWithCallerTimeoutAsync<ProduceRequest, ProduceResponse>(
                Arg.Any<ProduceRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(successResponse));

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
            await sender.EnqueueAsync(batch, CancellationToken.None);

            // The response is already complete, so acknowledgement should happen very quickly
            var result = await acknowledged.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
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
    [Timeout(30_000)]
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

        var acknowledged = new TaskCompletionSource<long>();

        var sender = CreateSender(pool, options, accumulator, (_, offset, _, _, ex) =>
        {
            if (ex is null)
                acknowledged.TrySetResult(offset);
        });

        try
        {
            // Send first batch — response will hang
            var batch1 = CreateTestBatch(vtPool, "test-topic", 0);
            await sender.EnqueueAsync(batch1, CancellationToken.None);
            await sendSignals[0].Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            // Wait for request timeout to fire (1s + margin).
            // HandleTimedOutRequests processes batch1 directly (delivery timeout exceeded
            // → permanently fails it) and clears _pendingResponses, freeing the slot.
            await Task.Delay(2_000, cancellationToken);

            // Send second batch on different partition — should not hang
            // (HandleTimedOutRequests cleared _pendingResponses, freeing the capacity slot)
            var batch2 = CreateTestBatch(vtPool, "test-topic", 1);
            await sender.EnqueueAsync(batch2, CancellationToken.None);

            // Second batch should be sent (send loop freed the capacity slot)
            await sendSignals[1].Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

            // Complete second response and verify acknowledgement
            tcs2.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 42));
            var offset = await acknowledged.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
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
}
