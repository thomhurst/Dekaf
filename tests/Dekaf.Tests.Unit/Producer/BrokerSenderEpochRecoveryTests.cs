using System.Reflection;
using Dekaf.Metadata;
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
    public async Task ProducerIdReset_StillPendingAtTheDeliveryDeadline_FailsTheBatchOnTime(CancellationToken cancellationToken)
    {
        // Replacing an exhausted producer ID is a network round trip the producer retries for up
        // to max.block.ms. The send loop used to stay suspended on it for all that time, so no
        // batch of this broker could be expired: delivery.timeout.ms was overrun by up to
        // max.block.ms. The wait is bounded by the earliest delivery deadline the loop owns.
        var rejected = NewResponseSource();
        var (pool, connection) = CreateMockConnection(new Queue<TaskCompletionSource<ProduceResponse>>([rejected]));
        connection.CaptureProduceRequests = true;
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(deliveryTimeoutMs: 1_000);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var state = new ProducerIdAndEpoch(1234, short.MaxValue);
        accumulator.PublishProducerState(state);
        var resetStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resetNeverCompletes = new TaskCompletionSource<ProducerIdAndEpoch>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            bumpEpoch: (_, _) =>
            {
                resetStarted.TrySetResult();
                return new ValueTask<ProducerIdAndEpoch>(resetNeverCompletes.Task);
            },
            getProducerState: () => state);

        try
        {
            var (batch, delivery) = CreateTrackedBatch(valueTaskSourcePool, producerId: 1234, producerEpoch: short.MaxValue);
            sender.Enqueue(batch);
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 1, cancellationToken);
            rejected.SetResult(CreateErrorResponse(Topic, 0, ErrorCode.OutOfOrderSequenceNumber));
            await resetStarted.Task.WaitAsync(cancellationToken);

            var failure = await Assert.That(async () => await delivery.WaitAsync(cancellationToken))
                .ThrowsExactly<Dekaf.Errors.KafkaTimeoutException>();
            await Assert.That(failure!.TimeoutKind).IsEqualTo(Dekaf.Errors.TimeoutKind.Delivery);
            await Assert.That(Volatile.Read(ref connection.SendPipelinedAfterWriteCalls)).IsEqualTo(1);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Assert.Fail($"A wait was cancelled before the send loop got there. Requests written so far:{Environment.NewLine}{DescribeRequests(connection)}");
        }
        finally
        {
            resetNeverCompletes.TrySetCanceled(CancellationToken.None);
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

    [Test]
    public async Task AmbiguousRetry_OnPartitionThatDidNotTriggerTheBump_KeepsItsStamp_AndHoldsTheRestart(CancellationToken cancellationToken)
    {
        // A request whose response is lost may have been appended. Partition 1 did not trigger
        // the bump, so the broker still holds its epoch 5 entry: the retry must go out as
        // (epoch 5, sequence 20) and be deduplicated. Re-stamped to (epoch 6, sequence 0) the
        // broker would accept it as new and append the records a second time. The partition
        // restarts under epoch 6 only once that retry is answered.
        var partition0Rejected = NewResponseSource();
        var partition1Lost = NewResponseSource();
        var partition0Retried = NewResponseSource();
        var partition1Retried = NewResponseSource();
        var partition1Fresh = NewResponseSource();
        var (pool, connection) = CreateMockConnection(new Queue<TaskCompletionSource<ProduceResponse>>(
            [partition0Rejected, partition1Lost, partition0Retried, partition1Retried, partition1Fresh]));
        connection.CaptureProduceRequests = true;
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(maxInFlightRequestsPerConnection: 3);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var state = new ProducerStateHolder(new ProducerIdAndEpoch(1234, 5));
        accumulator.PublishProducerState(state.Value);
        var bumpRequests = new List<short>();
        var held = new TaskCompletionSource<TopicPartition>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            bumpEpoch: LocalBump(accumulator, bumpRequests, state),
            getProducerState: () => state.Read(),
            onSequenceRestartHeld: topicPartition => held.TrySetResult(topicPartition));

        try
        {
            // Partition 1 has produced 20 records under epoch 5 before its next batch.
            await Assert.That(accumulator.GetAndIncrementSequence(Partition1, 20, state.Value, out _)).IsEqualTo(0);

            var (batch0, delivery0) = CreateTrackedBatch(valueTaskSourcePool, Partition0, producerId: 1234, producerEpoch: 5);
            sender.Enqueue(batch0);
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 1, cancellationToken);
            var (batch1, delivery1) = CreateTrackedBatch(valueTaskSourcePool, Partition1, producerId: 1234, producerEpoch: 5, recordCount: 3);
            sender.Enqueue(batch1);
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 2, cancellationToken);

            // Partition 0 triggers the bump; its retry goes out under epoch 6.
            partition0Rejected.SetResult(CreateErrorResponse(Topic, 0, ErrorCode.OutOfOrderSequenceNumber));
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 3, cancellationToken);

            // A partition 1 batch sealed under epoch 6 waits behind the unanswered epoch 5 request.
            var (batch1Fresh, delivery1Fresh) = CreateTrackedBatch(valueTaskSourcePool, Partition1, producerId: 1234, producerEpoch: 6, recordCount: 2);
            sender.Enqueue(batch1Fresh);
            partition0Retried.SetResult(CreateSuccessResponse(Topic, 0, baseOffset: 0));
            await delivery0.WaitAsync(cancellationToken);
            await Assert.That(await held.Task.WaitAsync(cancellationToken)).IsEqualTo(Partition1);

            // The epoch 5 response is lost: the broker may or may not have appended the batch.
            partition1Lost.SetException(new IOException("connection reset"));
            await WaitUntilAsync(() => connection.CapturedProduceRequestCount == 4, cancellationToken);
            (long ProducerId, short ProducerEpoch, int BaseSequence)[] ambiguousRetryStamps;
            lock (connection.CapturedProduceRequests)
                ambiguousRetryStamps = connection.CapturedProduceRequests[3].ProducerStamps.ToArray();
            await Assert.That(ambiguousRetryStamps).IsEquivalentTo([(1234L, (short)5, 20)]);
            await Assert.That(Volatile.Read(ref connection.SendPipelinedAfterWriteCalls)).IsEqualTo(4);

            // Only once the retry is answered does the partition restart at 0 under epoch 6.
            partition1Retried.SetResult(CreateSuccessResponse(Topic, 1, baseOffset: 20));
            await delivery1.WaitAsync(cancellationToken);
            await WaitUntilAsync(() => connection.CapturedProduceRequestCount == 5, cancellationToken);
            partition1Fresh.SetResult(CreateSuccessResponse(Topic, 1, baseOffset: 23));
            await delivery1Fresh.WaitAsync(cancellationToken);

            (long ProducerId, short ProducerEpoch, int BaseSequence)[] freshStamps;
            lock (connection.CapturedProduceRequests)
                freshStamps = connection.CapturedProduceRequests[4].ProducerStamps.ToArray();
            await Assert.That(freshStamps).IsEquivalentTo([(1234L, (short)6, 0)]);

            short[] observedBumpRequests;
            lock (bumpRequests)
                observedBumpRequests = bumpRequests.ToArray();
            await Assert.That(observedBumpRequests).IsEquivalentTo([(short)5]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Assert.Fail($"A wait was cancelled before the send loop got there. Requests written so far:{Environment.NewLine}{DescribeRequests(connection)}");
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    public async Task LeaderMoveDuringBump_NewLeaderHoldsRestart_UntilReroutedOldEpochBatchIsAnswered(CancellationToken cancellationToken)
    {
        // Regression for #3385. Partition 1's epoch 5 request is pending on broker 1 when a
        // partition 0 rejection bumps the epoch to 6 and partition 1's leader moves to broker 2.
        // Broker 2's send loop has nothing of partition 1 pending, so its own hold did not apply:
        // it sent the partition's next batch as (epoch 6, sequence 0). The epoch 5 response is
        // then lost, the retry is rerouted to broker 2, and, the partition having restarted, was
        // re-stamped (epoch 6, sequence 2): a broker that had appended the first send appended
        // the records a second time. Broker 2 must hold the restart until the rerouted batch,
        // sent under its original stamp, is answered.
        var partition0Rejected = NewResponseSource();
        var partition1Lost = NewResponseSource();
        var partition0Retried = NewResponseSource();
        var (poolA, connectionA) = CreateMockConnection(new Queue<TaskCompletionSource<ProduceResponse>>(
            [partition0Rejected, partition1Lost, partition0Retried]));
        connectionA.CaptureProduceRequests = true;
        var partition1Retried = NewResponseSource();
        var partition1Fresh = NewResponseSource();
        var (poolB, connectionB) = CreateMockConnection(new Queue<TaskCompletionSource<ProduceResponse>>(
            [partition1Retried, partition1Fresh]));
        connectionB.CaptureProduceRequests = true;
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(maxInFlightRequestsPerConnection: 3);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var state = new ProducerStateHolder(new ProducerIdAndEpoch(1234, 5));
        accumulator.PublishProducerState(state.Value);
        var bumpRequests = new List<short>();
        // Shared by both loops, as KafkaProducer shares its metadata and inflight tracker.
        await using var metadataManager = new MetadataManager(poolA, options.BootstrapServers);
        using var inflightTracker = new PartitionInflightTracker();
        var heldOnB = new TaskCompletionSource<TopicPartition>(TaskCreationOptions.RunContinuationsAsynchronously);
        var senderB = CreateSender(
            poolB, options, accumulator, (_, _, _, _, _) => { },
            metadataManager,
            bumpEpoch: LocalBump(accumulator, bumpRequests, state),
            getProducerState: () => state.Read(),
            onSequenceRestartHeld: topicPartition => heldOnB.TrySetResult(topicPartition),
            brokerId: 2,
            inflightTracker: inflightTracker);
        var senderA = CreateSender(
            poolA, options, accumulator, (_, _, _, _, _) => { },
            metadataManager,
            rerouteBatch: senderB.Enqueue,
            bumpEpoch: LocalBump(accumulator, bumpRequests, state),
            getProducerState: () => state.Read(),
            brokerId: 1,
            inflightTracker: inflightTracker);

        try
        {
            // Broker 1 leads both partitions: partition 0, then partition 1, on the wire under epoch 5.
            metadataManager.Metadata.Update(CreateLeaderMetadata(partition1Leader: 1));
            var (batch0, delivery0) = CreateTrackedBatch(valueTaskSourcePool, Partition0, producerId: 1234, producerEpoch: 5);
            senderA.Enqueue(batch0);
            await WaitForSendsAsync(connectionA, 1, cancellationToken);
            var (batch1, delivery1) = CreateTrackedBatch(valueTaskSourcePool, Partition1, producerId: 1234, producerEpoch: 5, recordCount: 2);
            senderA.Enqueue(batch1);
            await WaitForSendsAsync(connectionA, 2, cancellationToken);

            // Partition 0 triggers the bump to epoch 6; its retry is answered.
            partition0Rejected.SetResult(CreateErrorResponse(Topic, 0, ErrorCode.OutOfOrderSequenceNumber));
            await WaitForSendsAsync(connectionA, 3, cancellationToken);
            partition0Retried.SetResult(CreateSuccessResponse(Topic, 0, baseOffset: 0));
            await delivery0.WaitAsync(cancellationToken);

            // Partition 1 moves to broker 2, which gets the partition's next batch while the
            // epoch 5 request is still unanswered on broker 1.
            metadataManager.Metadata.Update(CreateLeaderMetadata(partition1Leader: 2));
            var (batch1Fresh, delivery1Fresh) = CreateTrackedBatch(valueTaskSourcePool, Partition1, producerId: 1234, producerEpoch: 6, recordCount: 3);
            senderB.Enqueue(batch1Fresh);
            await Assert.That(await heldOnB.Task.WaitAsync(cancellationToken)).IsEqualTo(Partition1);
            await Assert.That(connectionB.CapturedProduceRequestCount).IsEqualTo(0);

            // The epoch 5 response is lost. Broker 1 reroutes the retry to broker 2, which sends
            // it under its original stamp, where the broker deduplicates it.
            partition1Lost.SetException(new IOException("connection reset"));
            await WaitForSendsAsync(connectionB, 1, cancellationToken);
            await Assert.That(StampsOf(connectionB, request: 0)).IsEquivalentTo([(1, 2, 1234L, (short)5, 0)]);

            // Only once it is answered does the partition restart at 0 under epoch 6.
            partition1Retried.SetResult(CreateSuccessResponse(Topic, 1, baseOffset: 0));
            await delivery1.WaitAsync(cancellationToken);
            await WaitForSendsAsync(connectionB, 2, cancellationToken);
            await Assert.That(StampsOf(connectionB, request: 1)).IsEquivalentTo([(1, 3, 1234L, (short)6, 0)]);
            partition1Fresh.SetResult(CreateSuccessResponse(Topic, 1, baseOffset: 2));
            await delivery1Fresh.WaitAsync(cancellationToken);

            short[] observedBumpRequests;
            lock (bumpRequests)
                observedBumpRequests = bumpRequests.ToArray();
            await Assert.That(observedBumpRequests).IsEquivalentTo([(short)5]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Assert.Fail($"A wait was cancelled before the send loops got there. Broker 1:{Environment.NewLine}{DescribeRequests(connectionA)}{Environment.NewLine}Broker 2:{Environment.NewLine}{DescribeRequests(connectionB)}");
        }
        finally
        {
            await senderA.DisposeAsync();
            await senderB.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    public async Task BatchStampedUnderOutdatedSnapshot_AfterAnotherLoopRestartedItsPartition_FollowsTheRestartUnderTheNewEpoch(CancellationToken cancellationToken)
    {
        // A send loop reads its producer state snapshot once per iteration. When the epoch is
        // bumped after that read, and another loop (the partition's new leader after a move)
        // restarts the partition at sequence 0 under the new epoch before this loop registers the
        // batch it coalesced, this loop still stamps under the old snapshot. It used to take the
        // next sequence of the new epoch's counter with the old epoch: a sequence of one epoch
        // under another. The batch was never on the wire, so it follows the restart instead.
        // The snapshot is pinned here to make that window deterministic.
        var responses = Enumerable.Range(0, 1).Select(_ => NewResponseSource()).ToArray();
        var (pool, connection) = CreateMockConnection(new Queue<TaskCompletionSource<ProduceResponse>>(responses));
        connection.CaptureProduceRequests = true;
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var snapshot = new ProducerIdAndEpoch(1234, 5);
        accumulator.PublishProducerState(snapshot);
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            getProducerState: () => snapshot);

        try
        {
            // Partition 1 produced 4 records under epoch 5, all answered.
            accumulator.GetAndIncrementSequence(Partition1, 4, snapshot, out _);

            // The bump to epoch 6, and the other loop's restart of partition 1: (epoch 6, 0..2).
            var bumped = new ProducerIdAndEpoch(1234, 6);
            accumulator.PublishProducerState(bumped);
            await Assert.That(accumulator.GetAndIncrementSequence(Partition1, 3, bumped, out var restarted)).IsEqualTo(0);
            await Assert.That(restarted).IsTrue();

            // This loop's batch, sealed under epoch 5, goes out as (epoch 6, sequence 3), not (epoch 5, 3).
            var (batch, delivery) = CreateTrackedBatch(valueTaskSourcePool, Partition1, producerId: 1234, producerEpoch: 5, recordCount: 2);
            sender.Enqueue(batch);
            await WaitForSendsAsync(connection, 1, cancellationToken);
            await Assert.That(StampsOf(connection, request: 0)).IsEquivalentTo([(1, 2, 1234L, (short)6, 3)]);

            responses[0].SetResult(CreateSuccessResponse(Topic, 1, baseOffset: 7));
            await Assert.That((await delivery.WaitAsync(cancellationToken)).Offset).IsEqualTo(7L);
            await Assert.That(accumulator.GetAndIncrementSequence(Partition1, 1, bumped, out _)).IsEqualTo(5);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Assert.Fail($"A wait was cancelled before the send loop got there. Requests written so far:{Environment.NewLine}{DescribeRequests(connection)}");
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    public async Task BatchStampedUnderSnapshotTwoStatesBehind_StampsUnderTheStateItsPartitionRestartedUnder(CancellationToken cancellationToken)
    {
        // This loop keeps epoch 5 for its iteration while the producer advances twice: another
        // loop restarts partition 1 under epoch 6, then epoch 7 is published before anything
        // restarts the partition again. The counter hands out epoch 6's sequences, so the batch
        // must go out as (epoch 6, sequence 3). It used to go out as (epoch 5, sequence 3).
        var responses = Enumerable.Range(0, 1).Select(_ => NewResponseSource()).ToArray();
        var (pool, connection) = CreateMockConnection(new Queue<TaskCompletionSource<ProduceResponse>>(responses));
        connection.CaptureProduceRequests = true;
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var snapshot = new ProducerIdAndEpoch(1234, 5);
        accumulator.PublishProducerState(snapshot);
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            getProducerState: () => snapshot);

        try
        {
            accumulator.GetAndIncrementSequence(Partition1, 4, snapshot, out _);
            var intermediate = new ProducerIdAndEpoch(1234, 6);
            accumulator.PublishProducerState(intermediate);
            await Assert.That(accumulator.GetAndIncrementSequence(Partition1, 3, intermediate, out var restarted)).IsEqualTo(0);
            await Assert.That(restarted).IsTrue();
            accumulator.PublishProducerState(new ProducerIdAndEpoch(1234, 7));

            var (batch, delivery) = CreateTrackedBatch(valueTaskSourcePool, Partition1, producerId: 1234, producerEpoch: 5, recordCount: 2);
            sender.Enqueue(batch);
            await WaitForSendsAsync(connection, 1, cancellationToken);
            await Assert.That(StampsOf(connection, request: 0)).IsEquivalentTo([(1, 2, 1234L, (short)6, 3)]);

            responses[0].SetResult(CreateSuccessResponse(Topic, 1, baseOffset: 7));
            await Assert.That((await delivery.WaitAsync(cancellationToken)).Offset).IsEqualTo(7L);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Assert.Fail($"A wait was cancelled before the send loop got there. Requests written so far:{Environment.NewLine}{DescribeRequests(connection)}");
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    public async Task OldEpochBatchRegisteredByAnotherLoopAfterThisLoopsScan_HoldsTheRestartUntilItIsAnswered(CancellationToken cancellationToken)
    {
        // Regression for #3385. This loop already runs under epoch 6 and found nothing of an
        // older epoch in flight, so its hold is disarmed. Another loop, still under epoch 5, then
        // claims and registers partition 1's (epoch 5, sequence 4) and stalls before writing it.
        // This loop's next batch of partition 1 used to restart the partition as (epoch 6,
        // sequence 0), which the broker would take ahead of the older batch; continuing epoch 5
        // as (epoch 5, sequence 6) instead is no better, since the other loop's batch travels on
        // another connection and the leader can receive sequence 6 first and reject it as out of
        // order. The restart decision, made under the lock registration takes, sees the other
        // loop's batch and refuses: this loop holds its batch, writes nothing, and restarts the
        // partition at 0 under epoch 6 once that batch is answered.
        var partition0Sent = NewResponseSource();
        var partition1Restarted = NewResponseSource();
        var partition1Next = NewResponseSource();
        var (pool, connection) = CreateMockConnection(new Queue<TaskCompletionSource<ProduceResponse>>(
            [partition0Sent, partition1Restarted, partition1Next]));
        connection.CaptureProduceRequests = true;
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var previous = new ProducerIdAndEpoch(1234, 5);
        var current = new ProducerIdAndEpoch(1234, 6);
        accumulator.PublishProducerState(previous);
        accumulator.GetAndIncrementSequence(Partition1, 4, previous, out _);
        accumulator.PublishProducerState(current);
        using var inflightTracker = new PartitionInflightTracker(enablePruning: false);
        var held = new TaskCompletionSource<TopicPartition>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            getProducerState: () => current,
            onSequenceRestartHeld: topicPartition => held.TrySetResult(topicPartition),
            inflightTracker: inflightTracker);

        try
        {
            // An iteration under epoch 6 with nothing older in flight disarms the hold.
            var (batch0, delivery0) = CreateTrackedBatch(valueTaskSourcePool, Partition0, producerId: 1234, producerEpoch: 6);
            sender.Enqueue(batch0);
            await WaitForSendsAsync(connection, 1, cancellationToken);
            partition0Sent.SetResult(CreateSuccessResponse(Topic, 0, baseOffset: 0));
            await delivery0.WaitAsync(cancellationToken);

            // The other loop's epoch 5 batch: claimed and registered, not yet written.
            var otherLoopEntry = inflightTracker.Register(
                Partition1, accumulator.GetAndIncrementSequence(Partition1, 2, previous, out _), recordCount: 2);
            await Assert.That(otherLoopEntry.BaseSequence).IsEqualTo(4);

            var (batch1, delivery1) = CreateTrackedBatch(valueTaskSourcePool, Partition1, producerId: 1234, producerEpoch: 6, recordCount: 3);
            sender.Enqueue(batch1);
            await Assert.That(await held.Task.WaitAsync(cancellationToken)).IsEqualTo(Partition1);
            await Assert.That(connection.CapturedProduceRequestCount).IsEqualTo(1);
            await Assert.That(inflightTracker.GetInflightCount(Partition1)).IsEqualTo(1);

            // The other loop's batch answered: the held batch restarts the partition under epoch 6.
            inflightTracker.Complete(otherLoopEntry);
            await WaitForSendsAsync(connection, 2, cancellationToken);
            await Assert.That(StampsOf(connection, request: 1)).IsEquivalentTo([(1, 3, 1234L, (short)6, 0)]);
            partition1Restarted.SetResult(CreateSuccessResponse(Topic, 1, baseOffset: 6));
            await delivery1.WaitAsync(cancellationToken);

            var (batch2, delivery2) = CreateTrackedBatch(valueTaskSourcePool, Partition1, producerId: 1234, producerEpoch: 6, recordCount: 1);
            sender.Enqueue(batch2);
            await WaitForSendsAsync(connection, 3, cancellationToken);
            await Assert.That(StampsOf(connection, request: 2)).IsEquivalentTo([(1, 1, 1234L, (short)6, 3)]);
            partition1Next.SetResult(CreateSuccessResponse(Topic, 1, baseOffset: 9));
            await delivery2.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Assert.Fail($"A wait was cancelled before the send loop got there. Requests written so far:{Environment.NewLine}{DescribeRequests(connection)}");
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    public async Task UnaffectedPartition_WithRequestPendingUnderOldProducerId_IsHeldUntilItIsAnswered(CancellationToken cancellationToken)
    {
        // The producer ID reset is the same transition as a bump: a partition with a request still
        // pending under the old ID must not send (new ID, sequence 0) ahead of it, or a retry of
        // the old request lands behind the newer batch and reorders the partition.
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
        var state = new ProducerStateHolder(new ProducerIdAndEpoch(1234, short.MaxValue));
        accumulator.PublishProducerState(state.Value);
        var held = new TaskCompletionSource<TopicPartition>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (batch1Second, delivery1Second) = CreateTrackedBatch(valueTaskSourcePool, Partition1, producerId: 5678, producerEpoch: 0);
        BrokerSender sender = null!;
        sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            bumpEpoch: (_, _) =>
            {
                // The producer replaces its exhausted ID (KafkaProducer.PublishIdempotentProducerId).
                var replaced = new ProducerIdAndEpoch(5678, 0);
                accumulator.ResetSequenceNumbers(replaced);
                state.Write(replaced);
                sender.Enqueue(batch1Second);
                return new ValueTask<ProducerIdAndEpoch>(replaced);
            },
            getProducerState: () => state.Read(),
            onSequenceRestartHeld: topicPartition => held.TrySetResult(topicPartition));

        try
        {
            var (batch0, delivery0) = CreateTrackedBatch(valueTaskSourcePool, Partition0, producerId: 1234, producerEpoch: short.MaxValue);
            sender.Enqueue(batch0);
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 1, cancellationToken);
            var (batch1First, delivery1First) = CreateTrackedBatch(valueTaskSourcePool, Partition1, producerId: 1234, producerEpoch: short.MaxValue);
            sender.Enqueue(batch1First);
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 2, cancellationToken);

            partition0Rejected.SetResult(CreateErrorResponse(Topic, 0, ErrorCode.OutOfOrderSequenceNumber));
            await Assert.That(await held.Task.WaitAsync(cancellationToken)).IsEqualTo(Partition1);
            await WaitUntilAsync(() => connection.CapturedProduceRequestCount == 3, cancellationToken);
            (string Name, Guid TopicId, int Partition)[] retryTopics;
            lock (connection.CapturedProduceRequests)
                retryTopics = connection.CapturedProduceRequests[2].Topics.ToArray();
            await Assert.That(retryTopics).IsEquivalentTo([(Topic, Guid.Empty, 0)]);
            await Assert.That(Volatile.Read(ref connection.SendPipelinedAfterWriteCalls)).IsEqualTo(3);

            partition1First.SetResult(CreateSuccessResponse(Topic, 1, baseOffset: 0));
            await WaitUntilAsync(() => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 4, cancellationToken);
            partition0Retried.SetResult(CreateSuccessResponse(Topic, 0, baseOffset: 0));
            partition1Second.SetResult(CreateSuccessResponse(Topic, 1, baseOffset: 1));
            await delivery0.WaitAsync(cancellationToken);
            await delivery1First.WaitAsync(cancellationToken);
            await delivery1Second.WaitAsync(cancellationToken);

            (long ProducerId, short ProducerEpoch, int BaseSequence)[] firstStamps, retryStamps, secondStamps;
            lock (connection.CapturedProduceRequests)
            {
                firstStamps = connection.CapturedProduceRequests[1].ProducerStamps.ToArray();
                retryStamps = connection.CapturedProduceRequests[2].ProducerStamps.ToArray();
                secondStamps = connection.CapturedProduceRequests[3].ProducerStamps.ToArray();
            }

            await Assert.That(firstStamps).IsEquivalentTo([(1234L, short.MaxValue, 0)]);
            await Assert.That(retryStamps).IsEquivalentTo([(5678L, (short)0, 0)]);
            await Assert.That(secondStamps).IsEquivalentTo([(5678L, (short)0, 0)]);
            await Assert.That(GetEpochBumpRequestedForEpoch(sender)).IsEqualTo(-1);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Assert.Fail($"A wait was cancelled before the send loop got there. Requests written so far:{Environment.NewLine}{DescribeRequests(connection)}");
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    public async Task CoalescedRetry_PutBackForABump_NeverGoesAheadOfAnOlderRetryQueuedMeanwhile(CancellationToken cancellationToken)
    {
        // Responses are processed while a coalesced wave waits for in-flight capacity. Retry B
        // of partition 0 is parked in that wait when the older batch A, re-sent before it, comes
        // back with a retriable error in the same response that makes partition 1 request a
        // bump. The bump sends the wave back to carry-over: B used to go to the FRONT of its
        // queue, ahead of A. B then restarted the partition under the new epoch, and A, whose
        // first send the broker may hold, was re-stamped behind it: appended twice and out of
        // order. B belongs behind A, and A keeps its stamp until it is answered.
        var partition2 = new TopicPartition(Topic, 2);
        var responses = Enumerable.Range(0, 6).Select(_ => NewResponseSource()).ToArray();
        var (pool, connection) = CreateMockConnection(new Queue<TaskCompletionSource<ProduceResponse>>(responses));
        connection.CaptureProduceRequests = true;
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(maxInFlightRequestsPerConnection: 2, retryBackoffMs: 0);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var state = new ProducerStateHolder(new ProducerIdAndEpoch(1234, 5));
        accumulator.PublishProducerState(state.Value);
        var logger = new CapacityWaitLogger();
        var held = new TaskCompletionSource<TopicPartition>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            bumpEpoch: (expectedEpoch, _) =>
            {
                // Like KafkaProducer.BumpEpochForRecoveryAsync: only the first request for an epoch bumps.
                var current = state.Read();
                if (current.Epoch == expectedEpoch)
                {
                    current = new ProducerIdAndEpoch(1234, (short)(expectedEpoch + 1));
                    accumulator.PublishProducerState(current);
                    state.Write(current);
                }

                return new ValueTask<ProducerIdAndEpoch>(current);
            },
            getProducerState: () => state.Read(),
            onSequenceRestartHeld: topicPartition => held.TrySetResult(topicPartition),
            logger: logger);

        try
        {
            // A, then B, pipelined on partition 0 under epoch 5.
            var (batchA, deliveryA) = CreateTrackedBatch(valueTaskSourcePool, Partition0, producerId: 1234, producerEpoch: 5);
            sender.Enqueue(batchA);
            await WaitForSendsAsync(connection, 1, cancellationToken);
            // Two records, so that the requests show which of the two batches they carry.
            var (batchB, deliveryB) = CreateTrackedBatch(valueTaskSourcePool, Partition0, producerId: 1234, producerEpoch: 5, recordCount: 2);
            sender.Enqueue(batchB);
            await WaitForSendsAsync(connection, 2, cancellationToken);

            // B is rejected first: epoch 6. B waits for A, still pending under epoch 5.
            responses[1].SetResult(CreateErrorResponse(Topic, 0, ErrorCode.OutOfOrderSequenceNumber));
            await Assert.That(await held.Task.WaitAsync(cancellationToken)).IsEqualTo(Partition0);

            // Z fills the pipeline; X then waits, coalesced, for in-flight capacity.
            var (batchZ, deliveryZ) = CreateTrackedBatch(valueTaskSourcePool, partition2, producerId: 1234, producerEpoch: 6);
            sender.Enqueue(batchZ);
            await WaitForSendsAsync(connection, 3, cancellationToken);
            var (batchX, deliveryX) = CreateTrackedBatch(valueTaskSourcePool, Partition1, producerId: 1234, producerEpoch: 6);
            sender.Enqueue(batchX);
            await logger.WaitForCapacityWaitsAsync(1, cancellationToken);

            // A is rejected too. Its retry and X go out in one request, A as (epoch 6, sequence 0).
            responses[0].SetResult(CreateErrorResponse(Topic, 0, ErrorCode.OutOfOrderSequenceNumber));
            await WaitForSendsAsync(connection, 4, cancellationToken);
            await Assert.That(StampsOf(connection, request: 3)).IsEquivalentTo(
                [(0, 1, 1234L, (short)6, 0), (1, 1, 1234L, (short)6, 0)]);

            // The pipeline is full again (Z, and A with X), so B's retry waits coalesced.
            await logger.WaitForCapacityWaitsAsync(2, cancellationToken);

            // One response: A's outcome is unknown, and partition 1 requests the bump to epoch 7.
            responses[3].SetResult(CreateResponse(
                (0, ErrorCode.RequestTimedOut, -1L),
                (1, ErrorCode.OutOfOrderSequenceNumber, -1L)));

            // A goes first and keeps (epoch 6, sequence 0); X restarts partition 1 under epoch 7.
            await WaitForSendsAsync(connection, 5, cancellationToken);
            await Assert.That(StampsOf(connection, request: 4)).IsEquivalentTo(
                [(0, 1, 1234L, (short)6, 0), (1, 1, 1234L, (short)7, 0)]);

            // B restarts partition 0 under epoch 7 only once A is answered.
            responses[2].SetResult(CreateSuccessResponse(Topic, 2, baseOffset: 0));
            responses[4].SetResult(CreateResponse((0, ErrorCode.None, 0L), (1, ErrorCode.None, 0L)));
            await WaitForSendsAsync(connection, 6, cancellationToken);
            await Assert.That(StampsOf(connection, request: 5)).IsEquivalentTo([(0, 2, 1234L, (short)7, 0)]);
            responses[5].SetResult(CreateSuccessResponse(Topic, 0, baseOffset: 1));

            await deliveryA.WaitAsync(cancellationToken);
            await deliveryB.WaitAsync(cancellationToken);
            await deliveryZ.WaitAsync(cancellationToken);
            await deliveryX.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Assert.Fail($"A wait was cancelled before the send loop got there. Requests written so far:{Environment.NewLine}{DescribeRequests(connection)}");
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    public async Task ExpiredRetry_LeavesItsPartitionMuted_WhileAnotherRetryOfItIsQueued(CancellationToken cancellationToken)
    {
        // A retry that runs out of delivery time used to unmute its partition unconditionally.
        // With another retry of the partition still waiting out its backoff, the next fresh
        // batch was then sent ahead of that retry; re-stamped under a new epoch the two take
        // their sequences in send order and the broker appends them swapped.
        var responses = Enumerable.Range(0, 4).Select(_ => NewResponseSource()).ToArray();
        var (pool, connection) = CreateMockConnection(new Queue<TaskCompletionSource<ProduceResponse>>(responses));
        connection.CaptureProduceRequests = true;
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        // Long enough that the first retry is still backing off when the fresh batch arrives.
        var options = CreateOptions(maxInFlightRequestsPerConnection: 2, retryBackoffMs: 500);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var state = new ProducerIdAndEpoch(1234, 5);
        accumulator.PublishProducerState(state);
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            bumpEpoch: (_, _) => new ValueTask<ProducerIdAndEpoch>(state),
            getProducerState: () => state);

        try
        {
            var (first, firstDelivery) = CreateTrackedBatch(valueTaskSourcePool, Partition0, producerId: 1234, producerEpoch: 5);
            sender.Enqueue(first);
            await WaitForSendsAsync(connection, 1, cancellationToken);
            // Already past its delivery deadline: it fails at its first retry decision.
            var (expired, expiredDelivery) = IdempotentSequenceModelTests.CreateTrackedBatch(
                valueTaskSourcePool, Partition0, producerId: 1234, producerEpoch: 5, recordCount: 2,
                createdStopwatchTimestamp: System.Diagnostics.Stopwatch.GetTimestamp() - options.DeliveryTimeoutTicks - 1);
            sender.Enqueue(expired);
            await WaitForSendsAsync(connection, 2, cancellationToken);

            // The first batch goes to carry-over with a backoff, then the second expires.
            responses[0].SetResult(CreateErrorResponse(Topic, 0, ErrorCode.NotLeaderOrFollower));
            responses[1].SetResult(CreateErrorResponse(Topic, 0, ErrorCode.NotLeaderOrFollower));
            await Assert.That(async () => await expiredDelivery.WaitAsync(cancellationToken))
                .ThrowsExactly<Dekaf.Errors.KafkaTimeoutException>();

            var (fresh, freshDelivery) = CreateTrackedBatch(valueTaskSourcePool, Partition0, producerId: 1234, producerEpoch: 5, recordCount: 3);
            sender.Enqueue(fresh);

            // The retry (one record) is written before the fresh batch (three records).
            await WaitForSendsAsync(connection, 3, cancellationToken);
            await Assert.That(StampsOf(connection, request: 2)).IsEquivalentTo([(0, 1, 1234L, (short)5, 0)]);
            responses[2].SetResult(CreateSuccessResponse(Topic, 0, baseOffset: 0));
            await WaitForSendsAsync(connection, 4, cancellationToken);
            await Assert.That(StampsOf(connection, request: 3)).IsEquivalentTo([(0, 3, 1234L, (short)5, 3)]);
            responses[3].SetResult(CreateSuccessResponse(Topic, 0, baseOffset: 1));

            await firstDelivery.WaitAsync(cancellationToken);
            await freshDelivery.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Assert.Fail($"A wait was cancelled before the send loop got there. Requests written so far:{Environment.NewLine}{DescribeRequests(connection)}");
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

    private static ProducerOptions CreateOptions(
        int maxInFlightRequestsPerConnection = 1,
        int deliveryTimeoutMs = 30_000,
        int retryBackoffMs = 1) => new()
    {
        BootstrapServers = ["localhost:9092"],
        MaxInFlightRequestsPerConnection = maxInFlightRequestsPerConnection,
        ConnectionsPerBroker = 1,
        EnableAdaptiveConnections = false,
        EnableIdempotence = true,
        Acks = Acks.All,
        DeliveryTimeoutMs = deliveryTimeoutMs,
        RequestTimeoutMs = 30_000,
        // 0 disables the backoff and with it the jitter that decides which retries are due together.
        RetryBackoffMs = retryBackoffMs,
        RetryBackoffMaxMs = retryBackoffMs,
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
    /// A batch already stamped with the given producer ID and epoch, as the accumulator would seal
    /// it, with its delivery exposed as a task. It carries real records, so its sequence advances
    /// the partition's counter and a wrong non-zero stamp is visible on the wire.
    /// </summary>
    private static (ReadyBatch Batch, Task<RecordMetadata> Delivery) CreateTrackedBatch(
        ValueTaskSourcePool<RecordMetadata> pool, long producerId, short producerEpoch)
        => CreateTrackedBatch(pool, Partition0, producerId, producerEpoch);

    private static (ReadyBatch Batch, Task<RecordMetadata> Delivery) CreateTrackedBatch(
        ValueTaskSourcePool<RecordMetadata> pool, TopicPartition topicPartition, long producerId, short producerEpoch,
        int recordCount = 1)
        => IdempotentSequenceModelTests.CreateTrackedBatch(pool, topicPartition, producerId, producerEpoch, recordCount);

    /// <summary>One response for several partitions of <see cref="Topic"/>.</summary>
    private static ProduceResponse CreateResponse(params (int Partition, ErrorCode ErrorCode, long BaseOffset)[] partitions) =>
        new()
        {
            TopicCount = 1,
            Responses =
            [
                new ProduceResponseTopicData
                {
                    Name = Topic,
                    PartitionCount = partitions.Length,
                    PartitionResponses = partitions
                        .Select(partition => new ProduceResponsePartitionData
                        {
                            Index = partition.Partition,
                            ErrorCode = partition.ErrorCode,
                            BaseOffset = partition.BaseOffset
                        })
                        .ToArray()
                }
            ]
        };

    private static Task WaitForSendsAsync(TestKafkaConnection connection, int count, CancellationToken cancellationToken)
        => WaitUntilAsync(() => connection.CapturedProduceRequestCount >= count, cancellationToken);

    private static (int Partition, int RecordCount, long ProducerId, short ProducerEpoch, int BaseSequence)[] StampsOf(
        TestKafkaConnection connection, int request)
    {
        lock (connection.CapturedProduceRequests)
        {
            return connection.CapturedProduceRequests[request].Batches
                .Select(batch => (batch.Partition, batch.RecordCount, batch.ProducerId, batch.ProducerEpoch, batch.BaseSequence))
                .ToArray();
        }
    }

    /// <summary>Every request written so far with its stamps, for the message of a stalled test.</summary>
    private static string DescribeRequests(TestKafkaConnection connection)
    {
        lock (connection.CapturedProduceRequests)
        {
            return string.Join(
                Environment.NewLine,
                connection.CapturedProduceRequests.Select((request, index) =>
                    $"  request {index + 1}: " + string.Join(", ", request.Batches.Select(batch =>
                        $"{batch.Topic}-{batch.Partition} ({batch.ProducerId}, {batch.ProducerEpoch}, {batch.BaseSequence}) x{batch.RecordCount}"))));
        }
    }

    /// <summary>
    /// Signals each time the send loop parks a coalesced wave because every in-flight slot is
    /// taken: the state in which responses are processed with batches already coalesced.
    /// </summary>
    private sealed class CapacityWaitLogger : Microsoft.Extensions.Logging.ILogger
    {
        private int _capacityWaits;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (eventId.Name == "LogWaitingForInFlightCapacity")
                Interlocked.Increment(ref _capacityWaits);
        }

        public Task WaitForCapacityWaitsAsync(int count, CancellationToken cancellationToken)
            => WaitUntilAsync(() => Volatile.Read(ref _capacityWaits) >= count, cancellationToken);
    }

    /// <summary>Broker 1 leads partition 0; <paramref name="partition1Leader"/> leads partition 1.</summary>
    private static MetadataResponse CreateLeaderMetadata(int partition1Leader) => new()
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
                Name = Topic,
                Partitions =
                [
                    new PartitionMetadata
                    {
                        ErrorCode = ErrorCode.None,
                        PartitionIndex = 0,
                        LeaderId = 1,
                        LeaderEpoch = 1,
                        ReplicaNodes = [1],
                        IsrNodes = [1]
                    },
                    new PartitionMetadata
                    {
                        ErrorCode = ErrorCode.None,
                        PartitionIndex = 1,
                        LeaderId = partition1Leader,
                        LeaderEpoch = partition1Leader,
                        ReplicaNodes = [partition1Leader],
                        IsrNodes = [partition1Leader]
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
