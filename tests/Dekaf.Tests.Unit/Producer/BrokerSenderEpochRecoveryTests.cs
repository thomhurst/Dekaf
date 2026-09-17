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
    private static readonly TopicPartition Partition1 = new(Topic, 1);

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
        accumulator.PublishProducerState(state);
        var bumpRequests = new List<short>();
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            bumpEpoch: (expectedEpoch, _) =>
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
        // Another sender bumped the epoch before this sender's OutOfOrderSequenceNumber was
        // processed. The re-stamped batch must restart its partition under the epoch the producer
        // is at now, not continue the counter the stale epoch left behind.
        var firstResponse = NewResponseSource();
        var secondResponse = NewResponseSource();
        var (pool, connection) = CreateMockConnection(new Queue<TaskCompletionSource<ProduceResponse>>([firstResponse, secondResponse]));
        connection.CaptureProduceRequests = true;
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var state = new ProducerIdAndEpoch(1234, 5);
        accumulator.PublishProducerState(state);
        var bumpRequests = new List<short>();
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            bumpEpoch: (expectedEpoch, _) =>
            {
                lock (bumpRequests)
                    bumpRequests.Add(expectedEpoch);
                return new ValueTask<ProducerIdAndEpoch>(Volatile.Read(ref state));
            },
            getProducerState: () => Volatile.Read(ref state));

        try
        {
            var (batch, delivery) = CreateTrackedBatch(valueTaskSourcePool, producerId: 1234, producerEpoch: 5);
            sender.Enqueue(batch);
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 1, cancellationToken);
            // The partition produced 20 records under epoch 5.
            accumulator.GetAndIncrementSequence(Partition0, 20);

            // Another sender's bump lands before this sender sees its rejection.
            var bumped = new ProducerIdAndEpoch(1234, 6);
            accumulator.PublishProducerState(bumped);
            Volatile.Write(ref state, bumped);
            firstResponse.SetResult(CreateErrorResponse(Topic, 0, ErrorCode.OutOfOrderSequenceNumber));

            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 2, cancellationToken);
            secondResponse.SetResult(CreateSuccessResponse(Topic, 0, baseOffset: 11));
            var metadata = await delivery.WaitAsync(cancellationToken);
            await Assert.That(metadata.Offset).IsEqualTo(11L);

            short[] observedBumpRequests;
            lock (bumpRequests)
                observedBumpRequests = bumpRequests.ToArray();
            await Assert.That(observedBumpRequests).IsEquivalentTo([(short)5]);

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
        accumulator.PublishProducerState(state);
        var bumpStarted = new TaskCompletionSource<short>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resetCompleted = new TaskCompletionSource<ProducerIdAndEpoch>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            bumpEpoch: (expectedEpoch, _) =>
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
            accumulator.ResetSequenceNumbers(replaced);
            Volatile.Write(ref state, replaced);
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
        accumulator.PublishProducerState(state);
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            bumpEpoch: (_, _) => new ValueTask<ProducerIdAndEpoch>(state),
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

    [Test]
    public async Task UnaffectedPartition_RestartsAtZeroUnderBumpedEpoch_WithoutSecondBump(CancellationToken cancellationToken)
    {
        // Regression for #3342: only the partition that triggered the bump had its counter
        // restarted. The next batch of any other partition went out under the new epoch with its
        // old non-zero sequence, the broker rejected it ("Invalid sequence number for new epoch"),
        // and every bump re-staled every other active partition into a chain of bumps.
        var partition0Rejected = NewResponseSource();
        var partition0Retried = NewResponseSource();
        var partition1Sent = NewResponseSource();
        var (pool, connection) = CreateMockConnection(new Queue<TaskCompletionSource<ProduceResponse>>(
            [partition0Rejected, partition0Retried, partition1Sent]));
        connection.CaptureProduceRequests = true;
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var state = new ProducerStateHolder(new ProducerIdAndEpoch(1234, 5));
        accumulator.PublishProducerState(state.Value);
        var bumpRequests = new List<short>();
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            bumpEpoch: LocalBump(accumulator, bumpRequests, state),
            getProducerState: () => state.Read());

        try
        {
            // Partition 1 has produced 20 records under epoch 5 before the bump.
            await Assert.That(accumulator.GetAndIncrementSequence(Partition1, 20, state.Value, out _)).IsEqualTo(0);

            var (batch0, delivery0) = CreateTrackedBatch(valueTaskSourcePool, Partition0, producerId: 1234, producerEpoch: 5);
            sender.Enqueue(batch0);
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 1, cancellationToken);
            partition0Rejected.SetResult(CreateErrorResponse(Topic, 0, ErrorCode.OutOfOrderSequenceNumber));
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 2, cancellationToken);
            partition0Retried.SetResult(CreateSuccessResponse(Topic, 0, baseOffset: 3));
            await delivery0.WaitAsync(cancellationToken);

            // Sealed after the bump, so it already carries epoch 6; its sequence must restart at 0.
            var (batch1, delivery1) = CreateTrackedBatch(valueTaskSourcePool, Partition1, producerId: 1234, producerEpoch: 6);
            sender.Enqueue(batch1);
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 3, cancellationToken);
            partition1Sent.SetResult(CreateSuccessResponse(Topic, 1, baseOffset: 20));
            await delivery1.WaitAsync(cancellationToken);

            short[] observedBumpRequests;
            lock (bumpRequests)
                observedBumpRequests = bumpRequests.ToArray();
            await Assert.That(observedBumpRequests).IsEquivalentTo([(short)5]);

            (long ProducerId, short ProducerEpoch, int BaseSequence)[] retryStamps, partition1Stamps;
            lock (connection.CapturedProduceRequests)
            {
                retryStamps = connection.CapturedProduceRequests[1].ProducerStamps.ToArray();
                partition1Stamps = connection.CapturedProduceRequests[2].ProducerStamps.ToArray();
            }

            await Assert.That(retryStamps).IsEquivalentTo([(1234L, (short)6, 0)]);
            await Assert.That(partition1Stamps).IsEquivalentTo([(1234L, (short)6, 0)]);
            await Assert.That(accumulator.HasStaleSequenceState(Partition1, state.Value)).IsFalse();
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
    public async Task UnaffectedPartition_WithRequestPendingUnderOldEpoch_IsHeldUntilItIsAnswered(CancellationToken cancellationToken)
    {
        // Java's shouldStopDrainBatchesForPartition: a partition whose counter must restart under
        // the new epoch does not send until its batches under the old epoch are answered.
        // Otherwise sequence 0 of the new epoch could be accepted ahead of an old-epoch batch that
        // is then retried, reordering the partition.
        var partition0Rejected = NewResponseSource();
        var partition1First = NewResponseSource();
        var partition0Retried = NewResponseSource();
        var partition1Second = NewResponseSource();
        var (pool, connection) = CreateMockConnection(new Queue<TaskCompletionSource<ProduceResponse>>(
            [partition0Rejected, partition1First, partition0Retried, partition1Second]));
        connection.CaptureProduceRequests = true;
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(maxInFlightRequestsPerConnection: 3);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var state = new ProducerStateHolder(new ProducerIdAndEpoch(1234, 5));
        accumulator.PublishProducerState(state.Value);
        var bumpRequests = new List<short>();
        var held = new TaskCompletionSource<TopicPartition>(TaskCreationOptions.RunContinuationsAsynchronously);
        // The partition 1 batch that arrives while the bump happens. Enqueued from the bump
        // callback, on the send loop thread, so it is coalesced right after the bump — before
        // the loop can park on a response-only wait — and after the state has moved on.
        var (batch1Second, delivery1Second) = CreateTrackedBatch(valueTaskSourcePool, Partition1, producerId: 1234, producerEpoch: 6);
        BrokerSender sender = null!;
        sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            bumpEpoch: LocalBump(accumulator, bumpRequests, state, afterBump: () => sender.Enqueue(batch1Second)),
            getProducerState: () => Volatile.Read(ref state.Value),
            onSequenceRestartHeld: topicPartition => held.TrySetResult(topicPartition));

        try
        {
            // Two requests on the wire under epoch 5: partition 0, then partition 1.
            var (batch0, delivery0) = CreateTrackedBatch(valueTaskSourcePool, Partition0, producerId: 1234, producerEpoch: 5);
            sender.Enqueue(batch0);
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 1, cancellationToken);
            var (batch1First, delivery1First) = CreateTrackedBatch(valueTaskSourcePool, Partition1, producerId: 1234, producerEpoch: 5);
            sender.Enqueue(batch1First);
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 2, cancellationToken);

            // Partition 0 is rejected while partition 1's request is still unanswered. The bump
            // lets partition 0's retry go out under epoch 6 right away; the partition 1 batch that
            // arrives with the bump must wait for the epoch 5 request, not overtake it.
            partition0Rejected.SetResult(CreateErrorResponse(Topic, 0, ErrorCode.OutOfOrderSequenceNumber));
            await Assert.That(await held.Task.WaitAsync(cancellationToken)).IsEqualTo(Partition1);
            await WaitUntilAsync(() => connection.CapturedProduceRequestCount == 3, cancellationToken);
            (string Name, Guid TopicId, int Partition)[] retryTopics;
            lock (connection.CapturedProduceRequests)
                retryTopics = connection.CapturedProduceRequests[2].Topics.ToArray();
            await Assert.That(retryTopics).IsEquivalentTo([(Topic, Guid.Empty, 0)]);
            await Assert.That(Volatile.Read(ref connection.SendPipelinedAfterWriteCalls)).IsEqualTo(3);

            // The old-epoch request is answered: the partition restarts at 0 and the batch goes out.
            partition1First.SetResult(CreateSuccessResponse(Topic, 1, baseOffset: 0));
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 4, cancellationToken);
            partition0Retried.SetResult(CreateSuccessResponse(Topic, 0, baseOffset: 0));
            partition1Second.SetResult(CreateSuccessResponse(Topic, 1, baseOffset: 1));
            await delivery0.WaitAsync(cancellationToken);
            await delivery1First.WaitAsync(cancellationToken);
            await delivery1Second.WaitAsync(cancellationToken);

            short[] observedBumpRequests;
            lock (bumpRequests)
                observedBumpRequests = bumpRequests.ToArray();
            await Assert.That(observedBumpRequests).IsEquivalentTo([(short)5]);

            (long ProducerId, short ProducerEpoch, int BaseSequence)[] firstStamps, retryStamps, secondStamps;
            lock (connection.CapturedProduceRequests)
            {
                firstStamps = connection.CapturedProduceRequests[1].ProducerStamps.ToArray();
                retryStamps = connection.CapturedProduceRequests[2].ProducerStamps.ToArray();
                secondStamps = connection.CapturedProduceRequests[3].ProducerStamps.ToArray();
            }

            await Assert.That(firstStamps).IsEquivalentTo([(1234L, (short)5, 0)]);
            await Assert.That(retryStamps).IsEquivalentTo([(1234L, (short)6, 0)]);
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

    /// <summary>
    /// The producer's local bump as the send loop sees it: epoch+1 published to the accumulator
    /// and to <paramref name="state"/>, with <paramref name="afterBump"/> run before the send loop
    /// continues.
    /// </summary>
    private static Func<short, CancellationToken, ValueTask<ProducerIdAndEpoch>> LocalBump(
        RecordAccumulator accumulator,
        List<short> bumpRequests,
        ProducerStateHolder state,
        Action? afterBump = null)
        => (expectedEpoch, _) =>
        {
            lock (bumpRequests)
                bumpRequests.Add(expectedEpoch);
            var bumped = new ProducerIdAndEpoch(1234, (short)(expectedEpoch + 1));
            accumulator.PublishProducerState(bumped);
            state.Write(bumped);
            afterBump?.Invoke();
            return new ValueTask<ProducerIdAndEpoch>(bumped);
        };

    /// <summary>The producer's published state as the test and the send loop share it.</summary>
    private sealed class ProducerStateHolder(ProducerIdAndEpoch initial)
    {
        public ProducerIdAndEpoch Value = initial;

        public ProducerIdAndEpoch Read() => Volatile.Read(ref Value);

        public void Write(ProducerIdAndEpoch value) => Volatile.Write(ref Value, value);
    }

    private static TaskCompletionSource<ProduceResponse> NewResponseSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static ProducerOptions CreateOptions(int maxInFlightRequestsPerConnection = 1) => new()
    {
        BootstrapServers = ["localhost:9092"],
        MaxInFlightRequestsPerConnection = maxInFlightRequestsPerConnection,
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
        => CreateTrackedBatch(pool, Partition0, producerId, producerEpoch);

    private static (ReadyBatch Batch, Task<RecordMetadata> Delivery) CreateTrackedBatch(
        ValueTaskSourcePool<RecordMetadata> pool, TopicPartition topicPartition, long producerId, short producerEpoch)
    {
        var batch = new ReadyBatch();
        var source = pool.Rent();
        var delivery = source.Task.AsTask();
        var sources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(1);
        sources[0] = source;
        batch.Initialize(
            topicPartition,
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
