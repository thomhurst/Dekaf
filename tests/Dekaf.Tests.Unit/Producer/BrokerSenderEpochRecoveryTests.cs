using System.Buffers;
using System.Reflection;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Send-loop behaviour around the epoch bump request for idempotent producers. The request flag
/// must never stay set — a set flag parks every coalesced wave at the pre-send epoch check and
/// livelocks the sender for all partitions — the bump may complete asynchronously when the
/// producer ID is replaced after its epoch space is exhausted, and stale batches are re-stamped
/// from a single producer ID/epoch snapshot before they reach the wire.
/// </summary>
[Timeout(30_000)]
public sealed class BrokerSenderEpochRecoveryTests : ScriptedProduceResponseFixture
{
    private const string Topic = "test-topic";
    private static readonly TopicPartition Partition0 = new(Topic, 0);

    [Test]
    public async Task EpochBumpFailure_ClearsRequest_AndRetriesBatchWithoutLivelock(CancellationToken cancellationToken)
    {
        // Regression: the epoch bump used to throw at short.MaxValue, the request flag stayed set,
        // and every subsequent wave was moved back to carry-over before it could be sent.
        var firstResponse = NewResponseSource();
        var secondResponse = NewResponseSource();
        var (pool, connection) = CreateMockConnection(new Queue<TaskCompletionSource<ProduceResponse>>([firstResponse, secondResponse]));
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var state = new ProducerIdAndEpoch(1234, 5);
        var bumpRequests = new List<short>();
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            bumpEpoch: (expectedEpoch, _, _) =>
            {
                lock (bumpRequests)
                    bumpRequests.Add(expectedEpoch);
                throw new InvalidOperationException("injected epoch bump failure");
            },
            getProducerState: () => state);

        try
        {
            var (batch, delivery) = CreateTrackedBatch(valueTaskSourcePool, producerId: 1234, producerEpoch: 5);
            sender.Enqueue(batch);
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 1, cancellationToken);
            firstResponse.SetResult(CreateErrorResponse(Topic, 0, ErrorCode.OutOfOrderSequenceNumber));

            // The retry is the proof that the loop kept sending after the failed bump.
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 2, cancellationToken);
            secondResponse.SetResult(CreateSuccessResponse(Topic, 0, baseOffset: 42));

            var metadata = await delivery.WaitAsync(cancellationToken);
            await Assert.That(metadata.Offset).IsEqualTo(42L);
            short[] observedBumpRequests;
            lock (bumpRequests)
                observedBumpRequests = bumpRequests.ToArray();
            await Assert.That(observedBumpRequests).IsEquivalentTo([(short)5]);
            await Assert.That(GetEpochBumpRequestedForEpoch(sender)).IsEqualTo(-1);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    public async Task EpochAlreadyAdvanced_StillReportsPartitionsToProducer(CancellationToken cancellationToken)
    {
        // Another sender bumped the epoch for its own partitions before this sender's
        // OutOfOrderSequenceNumber was processed. The send loop used to skip the bump call when the
        // producer was already past the stale epoch, so this sender's partition never had its
        // sequence counter restarted and its re-stamped batch went out with a non-zero sequence.
        var firstResponse = NewResponseSource();
        var secondResponse = NewResponseSource();
        var (pool, connection) = CreateMockConnection(new Queue<TaskCompletionSource<ProduceResponse>>([firstResponse, secondResponse]));
        connection.CaptureProduceRequests = true;
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var state = new ProducerIdAndEpoch(1234, 5);
        var bumpRequests = new List<(short ExpectedEpoch, TopicPartition[] Partitions)>();
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            bumpEpoch: (expectedEpoch, partitions, _) =>
            {
                lock (bumpRequests)
                    bumpRequests.Add((expectedEpoch, partitions.ToArray()));
                return new ValueTask<ProducerIdAndEpoch>(Volatile.Read(ref state));
            },
            getProducerState: () => Volatile.Read(ref state));

        try
        {
            var (batch, delivery) = CreateTrackedBatch(valueTaskSourcePool, producerId: 1234, producerEpoch: 5);
            sender.Enqueue(batch);
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 1, cancellationToken);

            // Another sender's bump lands before this sender sees its rejection.
            Volatile.Write(ref state, new ProducerIdAndEpoch(1234, 6));
            firstResponse.SetResult(CreateErrorResponse(Topic, 0, ErrorCode.OutOfOrderSequenceNumber));

            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 2, cancellationToken);
            secondResponse.SetResult(CreateSuccessResponse(Topic, 0, baseOffset: 11));
            var metadata = await delivery.WaitAsync(cancellationToken);
            await Assert.That(metadata.Offset).IsEqualTo(11L);

            (short ExpectedEpoch, TopicPartition[] Partitions)[] observedBumpRequests;
            lock (bumpRequests)
                observedBumpRequests = bumpRequests.ToArray();
            await Assert.That(observedBumpRequests.Length).IsEqualTo(1);
            await Assert.That(observedBumpRequests[0].ExpectedEpoch).IsEqualTo((short)5);
            await Assert.That(observedBumpRequests[0].Partitions).IsEquivalentTo([Partition0]);

            (long ProducerId, short ProducerEpoch, int BaseSequence)[] secondStamps;
            lock (connection.CapturedProduceRequests)
                secondStamps = connection.CapturedProduceRequests[1].ProducerStamps.ToArray();
            await Assert.That(secondStamps).IsEquivalentTo([(1234L, (short)6, 0)]);
            await Assert.That(GetEpochBumpRequestedForEpoch(sender)).IsEqualTo(-1);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    public async Task ExhaustedEpoch_AwaitsProducerIdReset_AndResendsUnderNewProducerId(CancellationToken cancellationToken)
    {
        var firstResponse = NewResponseSource();
        var secondResponse = NewResponseSource();
        var (pool, connection) = CreateMockConnection(new Queue<TaskCompletionSource<ProduceResponse>>([firstResponse, secondResponse]));
        connection.CaptureProduceRequests = true;
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var state = new ProducerIdAndEpoch(1234, short.MaxValue);
        var bumpStarted = new TaskCompletionSource<short>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resetCompleted = new TaskCompletionSource<ProducerIdAndEpoch>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            bumpEpoch: (expectedEpoch, _, _) =>
            {
                bumpStarted.TrySetResult(expectedEpoch);
                return new ValueTask<ProducerIdAndEpoch>(resetCompleted.Task);
            },
            getProducerState: () => Volatile.Read(ref state));

        try
        {
            var (batch, delivery) = CreateTrackedBatch(valueTaskSourcePool, producerId: 1234, producerEpoch: short.MaxValue);
            sender.Enqueue(batch);
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 1, cancellationToken);
            firstResponse.SetResult(CreateErrorResponse(Topic, 0, ErrorCode.OutOfOrderSequenceNumber));

            await Assert.That(await bumpStarted.Task.WaitAsync(cancellationToken)).IsEqualTo(short.MaxValue);

            // Producer ID replaced while the send loop is suspended on the bump.
            var replaced = new ProducerIdAndEpoch(5678, 0);
            Volatile.Write(ref state, replaced);
            accumulator.ResetSequenceNumbers();
            resetCompleted.SetResult(replaced);

            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 2, cancellationToken);
            secondResponse.SetResult(CreateSuccessResponse(Topic, 0, baseOffset: 7));
            var metadata = await delivery.WaitAsync(cancellationToken);
            await Assert.That(metadata.Offset).IsEqualTo(7L);

            // The re-sent batch carries the new producer ID, epoch 0 and sequence 0 — had it gone out
            // before the reset completed it would still carry the exhausted stamps.
            (long ProducerId, short ProducerEpoch, int BaseSequence)[] firstStamps, secondStamps;
            lock (connection.CapturedProduceRequests)
            {
                firstStamps = connection.CapturedProduceRequests[0].ProducerStamps.ToArray();
                secondStamps = connection.CapturedProduceRequests[1].ProducerStamps.ToArray();
            }

            await Assert.That(firstStamps).IsEquivalentTo([(1234L, short.MaxValue, 0)]);
            await Assert.That(secondStamps).IsEquivalentTo([(5678L, (short)0, 0)]);
            await Assert.That(GetEpochBumpRequestedForEpoch(sender)).IsEqualTo(-1);
        }
        finally
        {
            resetCompleted.TrySetCanceled(CancellationToken.None);
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    public async Task StaleProducerId_WithCurrentEpoch_IsRestampedBeforeSend(CancellationToken cancellationToken)
    {
        // A batch sealed while the accumulator's separate producer ID and epoch writes were in
        // progress can carry the previous producer ID with the new epoch. Epoch-only comparison
        // would send it under an ID the broker fences.
        var response = NewResponseSource();
        var (pool, connection) = CreateMockConnection(new Queue<TaskCompletionSource<ProduceResponse>>([response]));
        connection.CaptureProduceRequests = true;
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var state = new ProducerIdAndEpoch(5678, 0);
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            bumpEpoch: (_, _, _) => new ValueTask<ProducerIdAndEpoch>(state),
            getProducerState: () => state);

        try
        {
            var (batch, delivery) = CreateTrackedBatch(valueTaskSourcePool, producerId: 1234, producerEpoch: 0);
            sender.Enqueue(batch);
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 1, cancellationToken);
            response.SetResult(CreateSuccessResponse(Topic, 0, baseOffset: 3));
            await delivery.WaitAsync(cancellationToken);

            (long ProducerId, short ProducerEpoch, int BaseSequence)[] stamps;
            lock (connection.CapturedProduceRequests)
                stamps = connection.CapturedProduceRequests.Single().ProducerStamps.ToArray();

            await Assert.That(stamps).IsEquivalentTo([(5678L, (short)0, 0)]);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    private static TaskCompletionSource<ProduceResponse> NewResponseSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static ProducerOptions CreateOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        MaxInFlightRequestsPerConnection = 1,
        ConnectionsPerBroker = 1,
        EnableAdaptiveConnections = false,
        EnableIdempotence = true,
        Acks = Acks.All,
        DeliveryTimeoutMs = 30_000,
        RequestTimeoutMs = 30_000,
        RetryBackoffMs = 1,
        RetryBackoffMaxMs = 1,
        LingerMs = 0
    };

    private (IConnectionPool pool, TestKafkaConnection connection) CreateMockConnection(
        Queue<TaskCompletionSource<ProduceResponse>> responseQueue)
    {
        var connection = new TestKafkaConnection();
        var scripted = RegisterScript(responseQueue);
        connection.SendProducePipelinedAfterWrite = () => new ValueTask<Task<ProduceResponse>>(scripted.Dequeue());

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(connection);
        pool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(connection);
        return (pool, connection);
    }

    /// <summary>
    /// A one-record batch already stamped with the given producer ID and epoch, as the accumulator
    /// would seal it, with its delivery exposed as a task.
    /// </summary>
    private static (ReadyBatch Batch, Task<RecordMetadata> Delivery) CreateTrackedBatch(
        ValueTaskSourcePool<RecordMetadata> pool, long producerId, short producerEpoch)
    {
        var batch = new ReadyBatch();
        var source = pool.Rent();
        var delivery = source.Task.AsTask();
        var sources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(1);
        sources[0] = source;
        batch.Initialize(
            Partition0,
            new RecordBatch
            {
                Records = Array.Empty<Record>(),
                ProducerId = producerId,
                ProducerEpoch = producerEpoch
            },
            sources,
            completionSourcesCount: 1,
            recordCount: 1,
            dataSize: 100);
        // Carry-over only drains wire-ready batches; an unmarked batch would wait for
        // asynchronous compression that never runs in this harness.
        batch.MarkPreSerialized();
        batch.TrySetMemoryReleased();
        return (batch, delivery);
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

    private static int GetEpochBumpRequestedForEpoch(BrokerSender sender) =>
        (int)typeof(BrokerSender).GetField(
            "_epochBumpRequestedForEpoch",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sender)!;

    private static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken cancellationToken)
    {
        while (!predicate())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }
}
