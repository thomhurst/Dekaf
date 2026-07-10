using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Scenarios;
using NSubstitute;
using ConfluentKafka = Confluent.Kafka;

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

    [Test]
    public async Task ConfluentDeliveryReportFailure_CountsAsDeliveryError()
    {
        var throughput = new ThroughputTracker();

        ConfluentProducerRoundTripStressTest.RecordDeliveryReportError(
            throughput,
            new ConfluentKafka.Error(ConfluentKafka.ErrorCode.Local_MsgTimedOut));

        await Assert.That(throughput.DeliveryErrorCount).IsEqualTo(1);
        await Assert.That(throughput.ErrorCount).IsEqualTo(0);
    }

    [Test]
    [NotInParallel("MeterListener")]
    public async Task DekafDeliveryMetricFailure_CountsAsRoundTripDeliveryError()
    {
        var throughput = new ThroughputTracker();
        using var listener = ProducerRoundTripStressTest.CreateDeliveryErrorListener(throughput);

        DekafMetrics.ProduceErrors.Add(2);

        await Assert.That(throughput.DeliveryErrorCount).IsEqualTo(2);
        await Assert.That(throughput.ErrorCount).IsEqualTo(0);
    }

    [Test]
    public async Task DekafConsumeFailure_IsRecordedWithoutAbortingTheResult()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, byte[]>>();
        consumer.Partitions.Returns(Substitute.For<IConsumerPartitions>());
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(ThrowConsumeFailure());
        var throughput = new ThroughputTracker();
        var validator = new RoundTripValidator([1]);
        var options = new StressTestOptions
        {
            BootstrapServers = "localhost:9092",
            Topic = "roundtrip-consume-failure",
            Partitions = 1,
            DurationMinutes = 1,
            MessageSizeBytes = 128,
            ProgressWatchdog = null!
        };

        var timedOut = await ProducerRoundTripStressTest.ConsumeAndValidateAsync(
            consumer,
            options,
            startOffsets: [0],
            endOffsets: [1],
            validator,
            throughput,
            CancellationToken.None);

        var error = throughput.GetSnapshot().ErrorSamples.Single();
        await Assert.That(timedOut).IsFalse();
        await Assert.That(throughput.ErrorCount).IsEqualTo(1);
        await Assert.That(error.Operation).IsEqualTo("Round-trip consume");
        await Assert.That(error.Message).IsEqualTo("consume failed");
    }

    private static async IAsyncEnumerable<ConsumeResult<string, byte[]>> ThrowConsumeFailure()
    {
        await Task.FromException(new InvalidOperationException("consume failed"));
        yield break;
    }

    [Test]
    public async Task RoundTripResult_ConfiguresDiagnosticsAndMessageBound()
    {
        var options = new StressTestOptions
        {
            BootstrapServers = "localhost:9092",
            Topic = "roundtrip-diagnostics",
            DurationMinutes = 1,
            MessageSizeBytes = 128,
            EnableProducerDeliveryDiagnostics = true,
            ProgressWatchdog = null!
        };
        var builder = Kafka.CreateProducer<string, byte[]>()
            .WithBootstrapServers(options.BootstrapServers);
        StressTestHelpers.ConfigureProducerDeliveryDiagnostics(builder, options);

        await using var producer = builder.Build();
        var snapshot = StressTestHelpers.CaptureProducerDeliveryDiagnostics(producer, options);

        await Assert.That(snapshot).IsNotNull();
        await Assert.That(snapshot!.DiagnosticsEnabled).IsTrue();

        var result = RoundTripScenarioHelpers.CreateResult(
            "producer-roundtrip",
            "Dekaf",
            options,
            DateTime.UtcNow,
            new ThroughputTracker(),
            new GcStats(),
            delivered: 0,
            new RoundTripValidationSnapshot
            {
                ExpectedMessages = 0,
                ConsumedMessages = 0,
                MissingMessages = 0,
                DuplicateMessages = 0,
                CorruptMessages = 0,
                OutOfOrderMessages = 0,
                MispartitionedMessages = 0,
                UnexpectedMessages = 0,
                TimedOut = false
            },
            snapshot);

        await Assert.That(result.ProducerDeliveryDiagnostics).IsSameReferenceAs(snapshot);
        await Assert.That(result.IsMessageBounded).IsTrue();
    }
}
