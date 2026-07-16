using System.Diagnostics;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Serialization;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;
using Dekaf.StressTests.Scenarios;
using NSubstitute;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.Tests.Unit.Stress;

public class RoundTripMessageCodecTests
{
    [Test]
    public async Task ScenarioRegistry_RegistersSteadyStateRoundTripPerClient()
    {
        var scenarios = Dekaf.StressTests.Program.CreateAllScenarios()
            .Where(scenario => scenario.Name.StartsWith("producer-roundtrip", StringComparison.Ordinal))
            .Select(scenario => (scenario.Name, scenario.Client))
            .ToArray();

        await Assert.That(scenarios).IsEquivalentTo(new[]
        {
            ("producer-roundtrip-steady", "Dekaf"),
            ("producer-roundtrip-steady", "Confluent")
        });
    }

    [Test]
    [Arguments(0, 59_999, false)]
    [Arguments(0, 60_000, true)]
    [Arguments(3_999_999_999, 0, false)]
    [Arguments(4_000_000_000, 0, true)]
    public async Task ProduceLimit_EndsAtDurationOrLogByteBudget(
        long estimatedLogBytes,
        int elapsedMilliseconds,
        bool expected)
    {
        var options = new StressTestOptions
        {
            BootstrapServers = "unused",
            Topic = "unused",
            DurationMinutes = 15,
            MessageSizeBytes = 128,
            RoundTripSteadySeconds = 60,
            ProgressWatchdog = null!
        };

        var reached = RoundTripScenarioHelpers.HasReachedProduceLimit(
            options,
            estimatedLogBytes,
            TimeSpan.FromMilliseconds(elapsedMilliseconds));

        await Assert.That(reached).IsEqualTo(expected);
    }

    [Test]
    public async Task EstimateLogBytes_ExceedsKeyAndValueSize()
    {
        var factory = new RoundTripMessageFactory("test-run", partitionCount: 6, payloadSize: 128);
        var message = factory.Create(partition: 0);

        var estimate = RoundTripScenarioHelpers.EstimateLogBytes(message);

        // The estimate must upper-bound the broker's real per-record log cost (key +
        // value + ~12 bytes of framing measured on the CI broker) or the log budget
        // cannot guarantee the produce phase fits the log dir.
        await Assert.That(estimate).IsGreaterThan((long)(message.Key.Length + message.Value.Length + 12));
    }

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
    public async Task TryQueryEndOffsets_WhenQueryFails_RecordsErrorAndReturnsNull()
    {
        var throughput = new ThroughputTracker();

        var offsets = await RoundTripScenarioHelpers.TryQueryEndOffsetsAsync(
            _ => Task.FromException<long[]>(new InvalidOperationException("query failed")),
            throughput,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        var error = throughput.GetSnapshot().ErrorSamples.Single();
        await Assert.That(offsets).IsNull();
        await Assert.That(throughput.ErrorCount).IsEqualTo(1);
        await Assert.That(error.Operation).IsEqualTo("Round-trip end offset query");
        await Assert.That(error.Message).IsEqualTo("query failed");
    }

    [Test]
    public async Task TryQueryEndOffsets_WhenQueryHangs_RecordsTimeoutAndReturnsNull()
    {
        var throughput = new ThroughputTracker();
        var neverCompletes = new TaskCompletionSource<long[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        var offsets = await RoundTripScenarioHelpers.TryQueryEndOffsetsAsync(
                _ => neverCompletes.Task,
                throughput,
                TimeSpan.Zero,
                CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(2));

        var error = throughput.GetSnapshot().ErrorSamples.Single();
        await Assert.That(offsets).IsNull();
        await Assert.That(throughput.ErrorCount).IsEqualTo(1);
        await Assert.That(error.ExceptionType).IsEqualTo(typeof(TimeoutException).FullName);
        await Assert.That(error.Operation).IsEqualTo("Round-trip end offset query");
    }

    [Test]
    public async Task TryQueryEndOffsets_WhenRunIsCancelled_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var throughput = new ThroughputTracker();

        await Assert.That(async () => await RoundTripScenarioHelpers.TryQueryEndOffsetsAsync(
                async token =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return [];
                },
                throughput,
                TimeSpan.FromSeconds(1),
                cancellation.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(throughput.ErrorCount).IsEqualTo(0);
    }

    [Test]
    public async Task StartSampler_WhenOperationThrows_StopsSamplerAndRethrows()
    {
        var throughput = new ThroughputTracker();
        throughput.Start();

        var runTask = RunFailingOperationAsync(throughput);

        await Assert.That(async () => await runTask.WaitAsync(TimeSpan.FromSeconds(2)))
            .Throws<InvalidOperationException>()
            .WithMessage("operation failed");

        static async Task RunFailingOperationAsync(ThroughputTracker throughput)
        {
            await using var sampler = StressTestHelpers.StartSampler(throughput, CancellationToken.None);
            await Task.Yield();
            throw new InvalidOperationException("operation failed");
        }
    }

    // The Confluent round-trip producer no longer attaches a per-message delivery-report
    // handler (fairness: librdkafka would invoke the managed delegate for every message,
    // work the Dekaf error-only listener never does). Loss is caught by the consume-side
    // completion tracker + CRC validation instead, so there is no handler left to test.

    [Test]
    [NotInParallel("MeterListener")]
    public async Task DekafDeliveryMetricFailure_CountsAsRoundTripDeliveryError()
    {
        var throughput = new ThroughputTracker();
        var topic = $"delivery-errors-{Guid.NewGuid():N}";
        using var listener = ProducerRoundTripStressTest.CreateDeliveryErrorListener(throughput, topic);

        DekafMetrics.ProduceErrors.Add(1, new TagList
        {
            { DekafDiagnostics.MessagingDestinationName, "unrelated-topic" }
        });
        DekafMetrics.ProduceErrors.Add(2, new TagList
        {
            { DekafDiagnostics.MessagingDestinationName, topic }
        });

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

    [Test]
    public async Task DekafConsume_UntrackedPartition_RecordsUnexpectedWithoutDecoding()
    {
        var factory = new RoundTripMessageFactory("untracked-partition", partitionCount: 1, payloadSize: 128);
        var message = factory.Create(partition: 0);
        var consumer = Substitute.For<IKafkaConsumer<string, byte[]>>();
        consumer.Partitions.Returns(Substitute.For<IConsumerPartitions>());
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(CreateConsumeResults(
                CreateConsumeResult(message, partition: 1, offset: 0),
                CreateConsumeResult(message, partition: 0, offset: 0)));
        var validator = new RoundTripValidator(factory.ExpectedPerPartition);
        var options = new StressTestOptions
        {
            BootstrapServers = "localhost:9092",
            Topic = "roundtrip-untracked-partition",
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
            new ThroughputTracker(),
            CancellationToken.None);

        var snapshot = validator.CreateSnapshot(timedOut);
        await Assert.That(timedOut).IsFalse();
        await Assert.That(snapshot.ConsumedMessages).IsEqualTo(2);
        await Assert.That(snapshot.UnexpectedMessages).IsEqualTo(1);
        await Assert.That(snapshot.DuplicateMessages).IsEqualTo(0);
        await Assert.That(snapshot.MispartitionedMessages).IsEqualTo(0);
        await Assert.That(snapshot.MissingMessages).IsEqualTo(0);
    }

    private static async IAsyncEnumerable<ConsumeResult<string, byte[]>> ThrowConsumeFailure()
    {
        await Task.FromException(new InvalidOperationException("consume failed"));
        yield break;
    }

    private static async IAsyncEnumerable<ConsumeResult<string, byte[]>> CreateConsumeResults(
        params ConsumeResult<string, byte[]>[] records)
    {
        foreach (var record in records)
        {
            yield return record;
            await Task.Yield();
        }
    }

    private static ConsumeResult<string, byte[]> CreateConsumeResult(
        RoundTripMessage message,
        int partition,
        long offset) =>
        new(
            topic: "roundtrip-untracked-partition",
            partition,
            offset,
            keyData: Encoding.UTF8.GetBytes(message.Key),
            isKeyNull: false,
            valueData: message.Value,
            isValueNull: false,
            headers: null,
            timestampMs: 0,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null,
            keyDeserializer: Serializers.String,
            valueDeserializer: Serializers.ByteArray);

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

        using var gcStats = new GcStats();
        var result = RoundTripScenarioHelpers.CreateResult(
            "producer-roundtrip-steady",
            "Dekaf",
            options,
            DateTime.UtcNow,
            new ThroughputTracker(),
            gcStats,
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
            RoundTripScenarioHelpers.CreatePhaseSnapshot(
                producedMessages: 200,
                produceElapsed: TimeSpan.FromSeconds(2),
                consumedMessages: 200,
                consumeElapsed: TimeSpan.FromSeconds(4)),
            snapshot);

        await Assert.That(result.ProducerDeliveryDiagnostics).IsSameReferenceAs(snapshot);
        await Assert.That(result.RoundTripSteadySeconds).IsEqualTo(options.RoundTripSteadySeconds);
        await Assert.That(result.RoundTripPhases!.ProduceMessagesPerSecond).IsEqualTo(100);
        await Assert.That(result.RoundTripPhases.ConsumeMessagesPerSecond).IsEqualTo(50);
    }

    [Test]
    public async Task RoundTripPhaseSnapshot_SerializesPhaseCountsDurationsAndRates()
    {
        var phases = RoundTripScenarioHelpers.CreatePhaseSnapshot(
            producedMessages: 250_000,
            produceElapsed: TimeSpan.FromSeconds(2),
            consumedMessages: 250_000,
            consumeElapsed: TimeSpan.FromSeconds(0.25));

        var result = CreateRoundTripResult(phases);
        var json = result.ToJson();
        var restored = StressTestResult.FromJson(json);

        await Assert.That(json).Contains("\"roundTripPhases\"");
        await Assert.That(json).Contains("\"produceMessagesPerSecond\": 125000");
        await Assert.That(json).Contains("\"consumeMessagesPerSecond\": 1000000");
        await Assert.That(restored!.RoundTripPhases!.ProducedMessages).IsEqualTo(250_000);
        await Assert.That(restored.RoundTripPhases.ProduceElapsedSeconds).IsEqualTo(2);
        await Assert.That(restored.RoundTripPhases.ConsumedMessages).IsEqualTo(250_000);
        await Assert.That(restored.RoundTripPhases.ConsumeElapsedSeconds).IsEqualTo(0.25);
    }

    [Test]
    public async Task RoundTripPhaseSnapshot_ZeroDurationReportsZeroRate()
    {
        var phases = RoundTripScenarioHelpers.CreatePhaseSnapshot(
            producedMessages: 1,
            produceElapsed: TimeSpan.Zero,
            consumedMessages: 1,
            consumeElapsed: TimeSpan.Zero);

        await Assert.That(phases.ProduceMessagesPerSecond).IsEqualTo(0);
        await Assert.That(phases.ConsumeMessagesPerSecond).IsEqualTo(0);
    }

    [Test]
    public async Task RoundTripReport_ShowsProduceAndConsumePhaseRates()
    {
        var resultWithoutValidation = CreateRoundTripResult(RoundTripScenarioHelpers.CreatePhaseSnapshot(
            producedMessages: 250_000,
            produceElapsed: TimeSpan.FromSeconds(2),
            consumedMessages: 250_000,
            consumeElapsed: TimeSpan.FromSeconds(0.25)));
        var result = WithValidation(resultWithoutValidation);
        var now = DateTime.UtcNow;

        var markdown = MarkdownReporter.Generate(new StressTestResults
        {
            RunStartedAtUtc = now,
            RunCompletedAtUtc = now.AddSeconds(2.25),
            MachineName = "test",
            ProcessorCount = 1,
            Results = [result]
        });

        await Assert.That(markdown).Contains("| Produce msg/s | Consume msg/s |");
        await Assert.That(markdown).Contains("125,000");
        await Assert.That(markdown).Contains("1,000,000");

        static StressTestResult WithValidation(StressTestResult source) => new()
        {
            Scenario = source.Scenario,
            Client = source.Client,
            DurationMinutes = source.DurationMinutes,
            MessageSizeBytes = source.MessageSizeBytes,
            StartedAtUtc = source.StartedAtUtc,
            CompletedAtUtc = source.CompletedAtUtc,
            Throughput = source.Throughput,
            GcStats = source.GcStats,
            RoundTripPhases = source.RoundTripPhases,
            RoundTripValidation = new RoundTripValidationSnapshot
            {
                ExpectedMessages = 250_000,
                ConsumedMessages = 250_000,
                MissingMessages = 0,
                DuplicateMessages = 0,
                CorruptMessages = 0,
                OutOfOrderMessages = 0,
                MispartitionedMessages = 0,
                UnexpectedMessages = 0,
                TimedOut = false
            }
        };
    }

    [Test]
    public async Task RoundTripResult_ExcludesProducerOnlyMedianRate()
    {
        // Round-trip results sample only their producer phase while the headline rate
        // includes validation, so the partial-window median must not be exposed.
        var result = CreateRoundTripResult(RoundTripScenarioHelpers.CreatePhaseSnapshot(
            producedMessages: 1_000,
            produceElapsed: TimeSpan.FromSeconds(10),
            consumedMessages: 1_000,
            consumeElapsed: TimeSpan.FromSeconds(20)));

        await Assert.That(result.MedianIntervalMessagesPerSecond).IsNull();
    }

    private static StressTestResult CreateRoundTripResult(RoundTripPhaseSnapshot phases)
    {
        var now = DateTime.UtcNow;
        return new StressTestResult
        {
            Scenario = "producer-roundtrip-steady",
            Client = "Dekaf",
            DurationMinutes = 1,
            MessageSizeBytes = 128,
            StartedAtUtc = now,
            CompletedAtUtc = now.AddSeconds(2.25),
            Throughput = new ThroughputSnapshot
            {
                TotalMessages = 250_000,
                TotalBytes = 32_000_000,
                TotalErrors = 0,
                ElapsedSeconds = 2.25,
                AverageMessagesPerSecond = 111_111.11,
                AverageMegabytesPerSecond = 13.56,
                MessagesPerSecondSamples = [100_000, 120_000],
                ErrorSamples = []
            },
            GcStats = new GcSnapshot
            {
                Gen0Collections = 0,
                Gen1Collections = 0,
                Gen2Collections = 0,
                AllocatedBytes = 0
            },
            RoundTripPhases = phases
        };
    }
}
