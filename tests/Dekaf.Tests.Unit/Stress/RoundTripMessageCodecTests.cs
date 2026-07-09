using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Scenarios;

namespace Dekaf.Tests.Unit.Stress;

public class RoundTripMessageCodecTests
{
    [Test]
    public async Task ExpectedPartition_MatchesJavaMurmur2Vector()
    {
        await Assert.That(RoundTripMessageCodec.GetExpectedPartition("user-123", partitionCount: 10))
            .IsEqualTo(8);
    }

    [Test]
    public async Task CreateAndDecode_RoundTripsMetadataAtRequestedSize()
    {
        const int partitionCount = 6;
        const int payloadSize = 256;
        var factory = new RoundTripMessageFactory("test-run", partitionCount, payloadSize);

        var message = factory.Create(partition: 4);
        var decoded = RoundTripMessageCodec.TryDecode(message.Key, message.Value, out var metadata);

        await Assert.That(decoded).IsTrue();
        await Assert.That(message.Value).Count().IsEqualTo(payloadSize);
        await Assert.That(message.ExpectedPartition).IsEqualTo(4);
        await Assert.That(RoundTripMessageCodec.GetExpectedPartition(message.Key, partitionCount)).IsEqualTo(4);
        await Assert.That(metadata.Partition).IsEqualTo(4);
        await Assert.That(metadata.Sequence).IsEqualTo(0);
        await Assert.That(metadata.Ordinal).IsEqualTo(4);
        await Assert.That(factory.ExpectedPerPartition.Sum()).IsEqualTo(1);
        await Assert.That(factory.ExpectedPerPartition[4]).IsEqualTo(1);
    }

    [Test]
    public async Task TryDecode_WhenPayloadOrKeyChanges_RejectsMessage()
    {
        var factory = new RoundTripMessageFactory("test-run", partitionCount: 3, payloadSize: 128);
        var message = factory.Create(partition: 2);
        var corruptedValue = message.Value.ToArray();
        corruptedValue[RoundTripMessageCodec.HeaderSize] ^= 0x5a;

        var valueDecoded = RoundTripMessageCodec.TryDecode(message.Key, corruptedValue, out _);
        var keyDecoded = RoundTripMessageCodec.TryDecode($"{message.Key}-changed", message.Value, out _);

        await Assert.That(valueDecoded).IsFalse();
        await Assert.That(keyDecoded).IsFalse();
    }

    [Test]
    public async Task Validator_ReportsGapsDuplicatesReorderingAndMispartitioning()
    {
        var factory = new RoundTripMessageFactory("validator-run", partitionCount: 2, payloadSize: 128);
        var sequenceZero = factory.Create(partition: 0);
        var sequenceOne = factory.Create(partition: 0);
        _ = factory.Create(partition: 0);
        var sequenceThree = factory.Create(partition: 0);
        var validator = new RoundTripValidator(factory.ExpectedPerPartition);

        validator.Record(sequenceOne.Key, sequenceOne.Value, actualPartition: 0, actualOffset: 0);
        validator.Record(sequenceZero.Key, sequenceZero.Value, actualPartition: 0, actualOffset: 1);
        validator.Record(sequenceOne.Key, sequenceOne.Value, actualPartition: 0, actualOffset: 2);
        validator.Record(sequenceThree.Key, sequenceThree.Value, actualPartition: 1, actualOffset: 0);

        var snapshot = validator.CreateSnapshot(timedOut: false);

        await Assert.That(snapshot.ExpectedMessages).IsEqualTo(4);
        await Assert.That(snapshot.ConsumedMessages).IsEqualTo(4);
        await Assert.That(snapshot.MissingMessages).IsEqualTo(1);
        await Assert.That(snapshot.DuplicateMessages).IsEqualTo(1);
        await Assert.That(snapshot.OutOfOrderMessages).IsEqualTo(1);
        await Assert.That(snapshot.MispartitionedMessages).IsEqualTo(1);
        await Assert.That(snapshot.IsSuccess).IsFalse();
    }

    [Test]
    public async Task Validator_AcceptsEverySequenceExactlyOnceInOffsetOrder()
    {
        var factory = new RoundTripMessageFactory("complete-run", partitionCount: 3, payloadSize: 128);
        var messages = Enumerable.Range(0, 3)
            .SelectMany(_ => Enumerable.Range(0, 3).Select(factory.Create))
            .ToArray();
        var validator = new RoundTripValidator(factory.ExpectedPerPartition);
        var offsets = new long[3];

        foreach (var message in messages)
        {
            validator.Record(
                message.Key,
                message.Value,
                message.ExpectedPartition,
                offsets[message.ExpectedPartition]++);
        }

        var snapshot = validator.CreateSnapshot(timedOut: false);

        await Assert.That(snapshot.ExpectedMessages).IsEqualTo(9);
        await Assert.That(snapshot.ConsumedMessages).IsEqualTo(9);
        await Assert.That(snapshot.MissingMessages).IsEqualTo(0);
        await Assert.That(snapshot.IsSuccess).IsTrue();
    }

    [Test]
    public async Task Validator_WhenOrdinalDoesNotMatchPartitionSequence_ReportsCorruption()
    {
        var factory = new RoundTripMessageFactory("ordinal-run", partitionCount: 2, payloadSize: 128);
        var message = factory.Create(partition: 0);
        var payload = RoundTripMessageCodec.Encode(
            message.Key,
            partition: 0,
            sequence: 0,
            ordinal: 9,
            payloadSize: 128);
        var validator = new RoundTripValidator(factory.ExpectedPerPartition);

        validator.Record(message.Key, payload, actualPartition: 0, actualOffset: 0);

        var snapshot = validator.CreateSnapshot(timedOut: false);
        await Assert.That(snapshot.CorruptMessages).IsEqualTo(1);
        await Assert.That(snapshot.MissingMessages).IsEqualTo(1);
        await Assert.That(snapshot.IsSuccess).IsFalse();
    }

    [Test]
    public async Task Validator_RecordUnexpected_CountsOutOfWindowRecordAsFailure()
    {
        var factory = new RoundTripMessageFactory("unexpected-run", partitionCount: 1, payloadSize: 128);
        _ = factory.Create(partition: 0);
        var validator = new RoundTripValidator(factory.ExpectedPerPartition);

        validator.RecordUnexpected();

        var snapshot = validator.CreateSnapshot(timedOut: false);
        await Assert.That(snapshot.ConsumedMessages).IsEqualTo(1);
        await Assert.That(snapshot.UnexpectedMessages).IsEqualTo(1);
        await Assert.That(snapshot.MissingMessages).IsEqualTo(1);
        await Assert.That(snapshot.IsSuccess).IsFalse();
    }

    [Test]
    public async Task CompletionTracker_HandlesEmptyRangesAndUnexpectedPartitions()
    {
        var tracker = new RoundTripCompletionTracker(
            startOffsets: [0, 5],
            endOffsets: [0, 7]);

        await Assert.That(tracker.RemainingPartitions).IsEqualTo(1);
        await Assert.That(tracker.Record(partition: 9, offset: 0)).IsFalse();
        await Assert.That(tracker.Record(partition: 1, offset: 4)).IsFalse();
        await Assert.That(tracker.Record(partition: 1, offset: 5)).IsTrue();
        await Assert.That(tracker.IsComplete).IsFalse();
        await Assert.That(tracker.Record(partition: 1, offset: 6)).IsTrue();
        await Assert.That(tracker.IsComplete).IsTrue();
    }

    [Test]
    public async Task TryRecordProduceTimeout_WhenDeadlineExpires_RecordsHardError()
    {
        var throughput = new ThroughputTracker();

        var timedOut = RoundTripScenarioHelpers.TryRecordProduceTimeout(
            true,
            throughput,
            client: "Dekaf",
            ordinal: 42,
            cancellationToken: CancellationToken.None);

        await Assert.That(timedOut).IsTrue();
        await Assert.That(throughput.ErrorCount).IsEqualTo(1);
        await Assert.That(throughput.GetSnapshot().ErrorSamples.Single().Message)
            .IsEqualTo("Dekaf round-trip produce phase exceeded its timeout.");
    }

    [Test]
    public async Task TryRecordProduceTimeout_BeforeDeadline_DoesNothing()
    {
        var throughput = new ThroughputTracker();

        var timedOut = RoundTripScenarioHelpers.TryRecordProduceTimeout(
            false,
            throughput,
            client: "Dekaf",
            ordinal: 0,
            cancellationToken: CancellationToken.None);

        await Assert.That(timedOut).IsFalse();
        await Assert.That(throughput.ErrorCount).IsEqualTo(0);
    }

    [Test]
    public async Task TryRecordProduceTimeout_WhenRunIsCancelled_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var throughput = new ThroughputTracker();

        await Assert.That(() => RoundTripScenarioHelpers.TryRecordProduceTimeout(
                true,
                throughput,
                client: "Dekaf",
                ordinal: 42,
                cancellationToken: cancellation.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(throughput.ErrorCount).IsEqualTo(0);
    }
}
