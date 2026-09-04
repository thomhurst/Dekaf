using System.Buffers;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;

using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// The send loop's wave-coalesce spin waits up to a quiet window for sibling batches before
/// writing a request. A batch the accumulator sealed as the producer's sole demand (#2510
/// bypass) cannot gain a sibling during that wait — its only caller is awaiting it — so a
/// single-batch wave carrying <see cref="ReadyBatch.SealedAsSoleDemand"/> skips the spin
/// without entering <see cref="WaveCoalesceGate"/> accounting. Unflagged waves keep spinning.
/// </summary>
public sealed class BrokerSenderSoleDemandWaveTests : ScriptedProduceResponseFixture
{
    private const string Topic = "sole-demand-topic";

    [Test]
    public async Task ShouldMicroLinger_CancelledSoleWaiter_KeepsSpinningAfterSourceReuse()
    {
        var source = new PooledValueTaskSource<RecordMetadata>();
        var delivery = source.Task;
        var batch = new ReadyBatch();
        batch.Initialize(new TopicPartition(Topic, 0), new RecordBatch { Records = [] },
            [source], completionSourcesCount: 1, recordCount: 1, dataSize: 100);
        batch.SealedAsSoleDemand = true;
        using var cancellation = new CancellationTokenSource();
        source.RegisterCancellation(cancellation.Token);

        await Assert.That(BrokerSender.ShouldMicroLinger([batch], 1, isTransactional: false)).IsFalse();
        cancellation.Cancel();
        await Assert.That(BrokerSender.ShouldMicroLinger([batch], 1, isTransactional: false)).IsTrue();
        await Assert.That(BrokerSender.ShouldMicroLinger([batch], 1, isTransactional: true)).IsTrue();

        await Assert.That(async () => await delivery).Throws<OperationCanceledException>();
        // GetResult resets the source to Pending. The original batch must not mistake
        // this new incarnation for its own still-waiting caller.
        await Assert.That(source.Task.IsCompleted).IsFalse();
        await Assert.That(BrokerSender.ShouldMicroLinger([batch], 1, isTransactional: false)).IsTrue();

        var nextBatch = new ReadyBatch();
        nextBatch.Initialize(new TopicPartition(Topic, 1), new RecordBatch { Records = [] },
            [source], completionSourcesCount: 1, recordCount: 1, dataSize: 100);
        nextBatch.SealedAsSoleDemand = true;
        await Assert.That(BrokerSender.ShouldMicroLinger([nextBatch], 1, isTransactional: false)).IsFalse();
    }

    [Test]
    public async Task ShouldMicroLinger_SoleDemandSingleBatchWave_SkipsSpin()
    {
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        try
        {
            var (batch, _) = CreateBatch(pool, partition: 0, soleDemand: true);

            await Assert.That(BrokerSender.ShouldMicroLinger([batch], 1, isTransactional: false)).IsFalse();
            await Assert.That(BrokerSender.ShouldMicroLinger([batch], 1, isTransactional: true)).IsFalse();
        }
        finally
        {
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task ShouldMicroLinger_UnflaggedSingleBatchWave_KeepsSpinning()
    {
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        try
        {
            var (batch, _) = CreateBatch(pool, partition: 0, soleDemand: false);

            await Assert.That(BrokerSender.ShouldMicroLinger([batch], 1, isTransactional: false)).IsTrue();
            // Pre-existing skip: serial transactional produce is the same shape, proven by
            // the transaction's single-caller protocol rather than the accumulator gate.
            await Assert.That(BrokerSender.ShouldMicroLinger([batch], 1, isTransactional: true)).IsFalse();
        }
        finally
        {
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task ShouldMicroLinger_MultiBatchWave_KeepsSpinningEvenWhenFlagged()
    {
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        try
        {
            var (first, _) = CreateBatch(pool, partition: 0, soleDemand: true);
            var (second, _) = CreateBatch(pool, partition: 1, soleDemand: true);

            await Assert.That(BrokerSender.ShouldMicroLinger([first, second], 2, isTransactional: false)).IsTrue();
        }
        finally
        {
            await pool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(60_000)]
    public async Task SendLoop_SoleDemandBatch_SkipsWaveCoalesceSpin_UnflaggedBatchStillSpins(
        CancellationToken cancellationToken)
    {
        var responses = new Queue<TaskCompletionSource<ProduceResponse>>(
        [
            CompletedResponse(partition: 0, baseOffset: 10),
            CompletedResponse(partition: 0, baseOffset: 11),
        ]);
        var (pool, connection) = CreateMockConnection(responses);
        connection.CaptureProduceRequests = true;
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var spinsStarted = 0;
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            static (_, _, _, _, _) => { },
            onWaveCoalesceStarted: () => Interlocked.Increment(ref spinsStarted));

        try
        {
            // Two known partitions with one coalesced: the only remaining spin gate is the
            // per-wave predicate under test.
            SeedKnownPartitions(sender, new TopicPartition(Topic, 0), new TopicPartition(Topic, 1));

            var (soleDemand, soleDemandDelivery) = CreateBatch(vtPool, partition: 0, soleDemand: true);
            sender.Enqueue(soleDemand);
            var metadata = await soleDemandDelivery.WaitAsync(cancellationToken);

            await Assert.That(metadata.Offset).IsEqualTo(10);
            await Assert.That(Volatile.Read(ref spinsStarted)).IsEqualTo(0);

            var (unflagged, unflaggedDelivery) = CreateBatch(vtPool, partition: 0, soleDemand: false);
            sender.Enqueue(unflagged);
            metadata = await unflaggedDelivery.WaitAsync(cancellationToken);

            await Assert.That(metadata.Offset).IsEqualTo(11);
            await Assert.That(Volatile.Read(ref spinsStarted)).IsEqualTo(1);
            int capturedRequestCount;
            lock (connection.CapturedProduceRequests)
            {
                capturedRequestCount = connection.CapturedProduceRequests.Count;
            }

            await Assert.That(capturedRequestCount).IsEqualTo(2);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    /// <summary>
    /// A skipped wave must not be booked as a fruitless spin: after more skipped waves than
    /// <see cref="WaveCoalesceGate.FruitlessSpinSuppressThreshold"/>, the next unflagged wave
    /// still spins. (The gate re-probes on a 50 ms clock, so a regression here is only
    /// masked if the flagged cycles themselves take longer than that — far above what this
    /// in-process script needs.)
    /// </summary>
    [Test]
    [Timeout(60_000)]
    public async Task SendLoop_SkippedSoleDemandWaves_DoNotConsumeWaveCoalesceGateBudget(
        CancellationToken cancellationToken)
    {
        const int flaggedWaves = WaveCoalesceGate.FruitlessSpinSuppressThreshold + 2;
        var responses = new Queue<TaskCompletionSource<ProduceResponse>>();
        for (var i = 0; i < flaggedWaves; i++)
            responses.Enqueue(CompletedResponse(partition: i % 2, baseOffset: i));
        responses.Enqueue(CompletedResponse(partition: 0, baseOffset: flaggedWaves));

        var (pool, _) = CreateMockConnection(responses);
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var spinsStarted = 0;
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            static (_, _, _, _, _) => { },
            onWaveCoalesceStarted: () => Interlocked.Increment(ref spinsStarted));

        try
        {
            SeedKnownPartitions(sender, new TopicPartition(Topic, 0), new TopicPartition(Topic, 1));

            for (var i = 0; i < flaggedWaves; i++)
            {
                var (batch, delivery) = CreateBatch(vtPool, partition: i % 2, soleDemand: true);
                sender.Enqueue(batch);
                await delivery.WaitAsync(cancellationToken);
            }

            await Assert.That(Volatile.Read(ref spinsStarted)).IsEqualTo(0);

            var (unflagged, unflaggedDelivery) = CreateBatch(vtPool, partition: 0, soleDemand: false);
            sender.Enqueue(unflagged);
            await unflaggedDelivery.WaitAsync(cancellationToken);

            await Assert.That(Volatile.Read(ref spinsStarted)).IsEqualTo(1);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    private static ProducerOptions CreateOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        MaxInFlightRequestsPerConnection = 1,
        ConnectionsPerBroker = 1,
        EnableIdempotence = true,
        Acks = Acks.All,
        LingerMs = 5,
        RetryBackoffMs = 100,
        RetryBackoffMaxMs = 1000,
        DeliveryTimeoutMs = 30_000,
        RequestTimeoutMs = 30_000,
    };

    private (IConnectionPool pool, TestKafkaConnection connection) CreateMockConnection(
        Queue<TaskCompletionSource<ProduceResponse>> responseQueue)
    {
        var connection = new TestKafkaConnection();
        var scripted = RegisterScript(responseQueue);
        connection.SendProducePipelinedAfterWrite = () => new ValueTask<Task<ProduceResponse>>(scripted.Dequeue());

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);
        pool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);
        return (pool, connection);
    }

    private static TaskCompletionSource<ProduceResponse> CompletedResponse(int partition, long baseOffset)
    {
        var response = new TaskCompletionSource<ProduceResponse>();
        response.SetResult(CreateSuccessResponse(Topic, partition, baseOffset));
        return response;
    }

    /// <summary>
    /// One awaited record, mirroring the shape the #2510 bypass seals; <paramref name="soleDemand"/>
    /// stands in for the accumulator having proven it the producer's only demand.
    /// </summary>
    private static (ReadyBatch Batch, Task<RecordMetadata> Delivery) CreateBatch(
        ValueTaskSourcePool<RecordMetadata> pool,
        int partition,
        bool soleDemand)
    {
        var batch = new ReadyBatch();
        var source = pool.Rent();
        var delivery = source.Task.AsTask();
        var sources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(1);
        sources[0] = source;
        batch.Initialize(
            new TopicPartition(Topic, partition),
            new RecordBatch { Records = Array.Empty<Record>() },
            sources,
            completionSourcesCount: 1,
            recordCount: 1,
            dataSize: 100);
        batch.TrySetMemoryReleased();
        batch.SealedAsSoleDemand = soleDemand;
        return (batch, delivery);
    }
}
