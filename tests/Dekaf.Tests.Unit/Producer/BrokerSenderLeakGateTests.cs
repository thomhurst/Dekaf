using System.Buffers;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Leak gates for <see cref="BrokerSender"/>'s cleanup paths — the layer below the
/// accumulator-level gates (<c>ProducerMemoryLeakGateTests</c> and friends), and the layer
/// where the #2187 double-return and #1578 send-loop-death leaks lived. Batches flow through
/// the real send loop against scripted produce responses
/// (<see cref="ScriptedProduceResponseFixture"/>), with real BufferMemory reservations
/// (<c>markMemoryReleased: false</c>) so the sender's own refund path is what returns them.
/// </summary>
/// <remarks>
/// Exactly-once refunds are asserted with a canary reservation: the accumulator holds a
/// canary of reserved bytes for the whole test, and after every delivery the buffered total
/// must land exactly back on the canary. A missed refund leaves it above; a double refund
/// pulls it below — visible even if the accounting clamps at zero. The accumulator's
/// pipeline counters (<c>InFlightBatchCount</c>) are deliberately not asserted here:
/// manually built batches never enter the accumulator pipeline, so the sender's
/// <c>OnBatchExitsPipeline</c> legitimately drives that counter negative in this harness.
/// </remarks>
public sealed class BrokerSenderLeakGateTests : ScriptedProduceResponseFixture
{
    private const string Topic = "leak-sender";
    private const int BatchDataSize = 100;
    private const int CanaryBytes = 1_000;

    /// <summary>
    /// Repeated send → ack cycles through the real send loop, each batch carrying a live
    /// BufferMemory reservation that only the sender can refund. By design the sender
    /// releases at TCP-send time (post-write, pre-response), with the error-path cleanup
    /// releasing for batches that never reached the wire and an idempotency guard making
    /// the overlap safe — the gate asserts the composed result: exactly one refund per
    /// batch, from whichever site fires. Any cycle that misses (or doubles) the refund
    /// moves <c>BufferedBytes</c> off the canary and fails immediately.
    /// </summary>
    [Test]
    [Timeout(60_000)]
    public async Task RepeatedSendAckCycles_SenderRefundsExactlyOnce(
        CancellationToken cancellationToken)
    {
        const int Cycles = 50;

        var responses = new Queue<TaskCompletionSource<ProduceResponse>>();
        for (var i = 0; i < Cycles; i++)
        {
            var response = new TaskCompletionSource<ProduceResponse>();
            response.SetResult(CreateSuccessResponse(Topic, partition: 0, baseOffset: i));
            responses.Enqueue(response);
        }

        var (pool, _) = CreateScriptedConnection(responses);
        cancellationToken = GuardUnscriptedSends(cancellationToken);

        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { });

        try
        {
            await accumulator.ReserveMemoryAsync(CanaryBytes, cancellationToken);

            for (var cycle = 0; cycle < Cycles; cycle++)
            {
                await accumulator.ReserveMemoryAsync(BatchDataSize, cancellationToken);
                var (batch, delivery) = CreateReservedBatchWithDelivery(valueTaskSourcePool);

                sender.Enqueue(batch);
                var metadata = await delivery.WaitAsync(cancellationToken);

                await Assert.That(metadata.Offset).IsEqualTo(cycle);
                await Assert.That(accumulator.BufferedBytes).IsEqualTo((long)CanaryBytes);
            }

            accumulator.ReleaseMemory(CanaryBytes);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0L);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    /// <summary>
    /// A faulted response task (connection failure mid-request) forces the sender through
    /// its retry path; the retry succeeds. The reservation must be refunded exactly once
    /// across the failure + retry — the #2187 double-return family raced exactly this
    /// hand-off between the failure cleanup and the retry's success cleanup.
    /// </summary>
    [Test]
    [Timeout(60_000)]
    public async Task ConnectionFailureThenRetrySuccess_RefundsExactlyOnce(
        CancellationToken cancellationToken)
    {
        var faulted = new TaskCompletionSource<ProduceResponse>();
        faulted.SetException(new IOException("simulated connection failure"));
        var success = new TaskCompletionSource<ProduceResponse>();
        success.SetResult(CreateSuccessResponse(Topic, partition: 0, baseOffset: 42));

        var (pool, _) = CreateScriptedConnection(
            new Queue<TaskCompletionSource<ProduceResponse>>([faulted, success]));
        cancellationToken = GuardUnscriptedSends(cancellationToken);

        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { });

        try
        {
            await accumulator.ReserveMemoryAsync(CanaryBytes, cancellationToken);
            await accumulator.ReserveMemoryAsync(BatchDataSize, cancellationToken);
            var (batch, delivery) = CreateReservedBatchWithDelivery(valueTaskSourcePool);

            sender.Enqueue(batch);
            var metadata = await delivery.WaitAsync(cancellationToken);

            await Assert.That(metadata.Offset).IsEqualTo(42);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo((long)CanaryBytes);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    /// <summary>
    /// A terminal (non-retriable) broker error must fail the batch's completion source and
    /// refund the reservation exactly once through the sender's failure cleanup path.
    /// </summary>
    [Test]
    [Timeout(60_000)]
    public async Task TerminalBrokerError_FailsDelivery_RefundsExactlyOnce(
        CancellationToken cancellationToken)
    {
        var errorResponse = new TaskCompletionSource<ProduceResponse>();
        errorResponse.SetResult(CreateErrorResponse(Topic, partition: 0, ErrorCode.MessageTooLarge));

        var (pool, _) = CreateScriptedConnection(
            new Queue<TaskCompletionSource<ProduceResponse>>([errorResponse]));
        cancellationToken = GuardUnscriptedSends(cancellationToken);

        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { });

        try
        {
            await accumulator.ReserveMemoryAsync(CanaryBytes, cancellationToken);
            await accumulator.ReserveMemoryAsync(BatchDataSize, cancellationToken);
            var (batch, delivery) = CreateReservedBatchWithDelivery(valueTaskSourcePool);

            sender.Enqueue(batch);

            Exception? observed = null;
            try
            {
                _ = await delivery.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                observed = exception;
            }

            await Assert.That(observed).IsNotNull();
            await Assert.That(accumulator.BufferedBytes).IsEqualTo((long)CanaryBytes);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    /// <summary>
    /// Disposing the sender while a request is in flight (response parked, never completed)
    /// must terminate the batch's completion source and refund the reservation — a stranded
    /// source or a lost refund here is exactly the shutdown-path leak class.
    /// </summary>
    [Test]
    [Timeout(60_000)]
    public async Task DisposeWithParkedInFlightRequest_TerminatesDelivery_RefundsExactlyOnce(
        CancellationToken cancellationToken)
    {
        var parked = new TaskCompletionSource<ProduceResponse>();
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (pool, _) = CreateScriptedConnection(
            new Queue<TaskCompletionSource<ProduceResponse>>([parked]),
            onSend: () => sent.TrySetResult());
        cancellationToken = GuardUnscriptedSends(cancellationToken);

        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { });

        try
        {
            await accumulator.ReserveMemoryAsync(CanaryBytes, cancellationToken);
            await accumulator.ReserveMemoryAsync(BatchDataSize, cancellationToken);
            var (batch, delivery) = CreateReservedBatchWithDelivery(valueTaskSourcePool);

            sender.Enqueue(batch);
            await sent.Task.WaitAsync(cancellationToken);

            await sender.DisposeAsync();

            // The source must reach a terminal state — success or failure — never strand.
            try
            {
                _ = await delivery.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Terminal failure from shutdown is the expected outcome.
            }

            await Assert.That(accumulator.BufferedBytes).IsEqualTo((long)CanaryBytes);
        }
        finally
        {
            parked.TrySetResult(CreateSuccessResponse(Topic, partition: 0, baseOffset: 0));
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    private static ProducerOptions CreateOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        MaxInFlightRequestsPerConnection = 1,
        ConnectionsPerBroker = 1,
        EnableIdempotence = true,
        Acks = Acks.All,
        DeliveryTimeoutMs = 30_000,
        RetryBackoffMs = 0,
        RetryBackoffMaxMs = 0,
        RequestTimeoutMs = 30_000,
        LingerMs = 0,
    };

    /// <summary>
    /// One-record batch carrying a live reservation (<c>markMemoryReleased: false</c>) whose
    /// single completion source is exposed as the delivery task. The caller must have
    /// reserved <see cref="BatchDataSize"/> bytes on the accumulator first.
    /// </summary>
    private static (ReadyBatch Batch, Task<RecordMetadata> Delivery) CreateReservedBatchWithDelivery(
        ValueTaskSourcePool<RecordMetadata> pool)
    {
        var source = pool.Rent();
        var delivery = source.Task.AsTask();
        var sources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(1);
        sources[0] = source;

        var batch = new ReadyBatch();
        batch.Initialize(
            new TopicPartition(Topic, 0),
            new Dekaf.Protocol.Records.RecordBatch { Records = Array.Empty<Dekaf.Protocol.Records.Record>() },
            sources,
            completionSourcesCount: 1,
            recordCount: 1,
            dataSize: BatchDataSize);
        batch.MarkPreSerialized();
        return (batch, delivery);
    }

    private (IConnectionPool Pool, TestKafkaConnection Connection) CreateScriptedConnection(
        Queue<TaskCompletionSource<ProduceResponse>> responses,
        Action? onSend = null)
    {
        var connection = new TestKafkaConnection();
        var scripted = RegisterScript(responses, onSend);
        connection.SendProducePipelinedAfterWrite = () => new ValueTask<Task<ProduceResponse>>(scripted.Dequeue());

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);
        pool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);
        return (pool, connection);
    }

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
}
