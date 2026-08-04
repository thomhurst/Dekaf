#if NET10_0
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.StressTests.Diagnostics;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Scenarios;
using NSubstitute;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class SoakStressTestTests
{
    [Test]
    public async Task TrackMeasurementProgress_StallCapturesSoakProducerDiagnostics()
    {
        var outputDirectory = CreateOutputDirectory();

        try
        {
            var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var throughput = new ThroughputTracker();
            throughput.Start();

            using var watchdog = new ProgressWatchdog(
                outputDirectory,
                captureAfter: TimeSpan.FromMilliseconds(20),
                exitAfter: TimeSpan.FromMilliseconds(60),
                pollInterval: TimeSpan.FromMilliseconds(10),
                exitProcess: code => exited.TrySetResult(code),
                captureManagedStackReport: () => "fake managed stack");
            using var registration = new SoakStressTest().TrackMeasurementProgress(
                watchdog,
                throughput,
                () => new ProducerDeliveryDiagnosticsSnapshot
                {
                    DiagnosticsEnabled = true,
                    CapturedAtUtc = DateTimeOffset.UtcNow
                });

            await exited.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var producerArtifact = Directory.GetFiles(
                Path.Combine(outputDirectory, ProgressWatchdog.ArtifactsDirectoryName),
                "*-fatal-producer.json").Single();
            var contents = await File.ReadAllTextAsync(producerArtifact);
            await Assert.That(contents).Contains("\"scenario\": \"soak\"");
            await Assert.That(contents).Contains("\"diagnosticsEnabled\": true");
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Test]
    public async Task TrackConsumerMeasurementProgress_StallCapturesConsumerDiagnostics()
    {
        var outputDirectory = CreateOutputDirectory();

        try
        {
            var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var consumerThroughput = new ThroughputTracker();
            consumerThroughput.Start();

            using var watchdog = new ProgressWatchdog(
                outputDirectory,
                captureAfter: TimeSpan.FromMilliseconds(20),
                exitAfter: TimeSpan.FromMilliseconds(60),
                pollInterval: TimeSpan.FromMilliseconds(10),
                exitProcess: code => exited.TrySetResult(code),
                captureManagedStackReport: () => "fake managed stack");
            using var registration = new SoakStressTest().TrackConsumerMeasurementProgress(
                watchdog,
                consumerThroughput,
                () => new ConsumerDiagnosticSnapshot
                {
                    CapturedAtUtc = DateTimeOffset.UtcNow,
                    Assignment = [],
                    FetchPositions = [],
                    PrefetchedBytes = 0,
                    PendingFetchDepth = 0,
                    PrefetchBufferDepth = 0,
                    PrefetchDepth = 0,
                    PendingRevocations = [],
                    PendingRevocationMarkerPresent = false,
                    PendingRevocationClearPending = false,
                    PendingDivergingEpochResets = [],
                    FetchBufferEpoch = 0,
                    MinimumFetchBufferEpoch = 0,
                    MinimumFetchBufferEpochsByPartition = [],
                    AdaptivePartitionFetchBytes = null,
                    AdaptiveFetchMaxBytes = null
                });

            await exited.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var consumerArtifact = Directory.GetFiles(
                Path.Combine(outputDirectory, ProgressWatchdog.ArtifactsDirectoryName),
                "*-fatal-consumer.json").Single();
            var contents = await File.ReadAllTextAsync(consumerArtifact);
            await Assert.That(contents).Contains("\"scenario\": \"soak-consumer\"");
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Test]
    public async Task DisposeWithTimeoutAsync_WhenProducerHangs_RecordsError()
    {
        var producer = Substitute.For<IKafkaProducer<string, string>>();
        var disposeCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.DisposeAsync().Returns(new ValueTask(disposeCompletion.Task));
        var throughput = new ThroughputTracker();

        await StressTestHelpers.DisposeWithTimeoutAsync(
            producer,
            throughput,
            TimeSpan.FromMilliseconds(20));
        disposeCompletion.TrySetResult();

        var snapshot = throughput.GetSnapshot();
        await Assert.That(snapshot.TotalErrors).IsEqualTo(1);
        await Assert.That(snapshot.ErrorSamples.Single().ExceptionType).IsEqualTo("DisposeTimeout");
    }

    [Test]
    [Arguments(1_000L, 1_000L, true)]
    [Arguments(1_000L, 999L, false)]
    [Arguments(1_000L, 1_001L, false)]
    public async Task IsBrokerDeliveryExact_RequiresAcceptedCount(
        long acceptedMessages,
        long deliveredMessages,
        bool expected)
    {
        var result = SoakStressTest.IsBrokerDeliveryExact(acceptedMessages, deliveredMessages);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments(95, 100, true)]
    [Arguments(94.9, 100, false)]
    [Arguments(100, 100, true)]
    [Arguments(0, 100, false)]
    public async Task MeetsMinimumPacingRate_RequiresNinetyFivePercentOfTarget(
        double acceptedRate,
        int targetMessagesPerSecond,
        bool expected)
    {
        var result = SoakStressTest.MeetsMinimumPacingRate(
            acceptedRate,
            targetMessagesPerSecond);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments(0, true)]
    [Arguments(1, false)]
    [Arguments(5_000, false)]
    public async Task SoakUnbounded_IsTrueOnlyForZeroTargetRate(int messagesPerSecond, bool expected)
    {
        var outputDirectory = CreateOutputDirectory();

        try
        {
            using var watchdog = new ProgressWatchdog(outputDirectory);
            var options = CreateOptions(watchdog, messagesPerSecond);

            await Assert.That(options.SoakUnbounded).IsEqualTo(expected);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Test]
    public async Task RunUnboundedProducerAsync_ProducesUntilCancelled()
    {
        var outputDirectory = CreateOutputDirectory();

        try
        {
            using var watchdog = new ProgressWatchdog(outputDirectory);
            var options = CreateOptions(watchdog, messagesPerSecond: 0);
            var throughput = new ThroughputTracker();
            throughput.Start();

            using var cts = new CancellationTokenSource();
            var calls = 0;
            var producer = Substitute.For<IKafkaProducer<string, string>>();
            producer.FireAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(_ =>
                {
                    if (Interlocked.Increment(ref calls) >= 5)
                    {
                        cts.Cancel();
                    }

                    return ValueTask.CompletedTask;
                });

            await SoakStressTest.RunUnboundedProducerAsync(producer, options, "value", throughput, cts.Token);

            var snapshot = throughput.GetSnapshot();
            await Assert.That(throughput.MessageCount).IsEqualTo(5);
            await Assert.That(snapshot.TotalErrors).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Test]
    public async Task RunUnboundedProducerAsync_RecordsProduceFailuresAndContinues()
    {
        var outputDirectory = CreateOutputDirectory();

        try
        {
            using var watchdog = new ProgressWatchdog(outputDirectory);
            var options = CreateOptions(watchdog, messagesPerSecond: 0);
            var throughput = new ThroughputTracker();
            throughput.Start();

            using var cts = new CancellationTokenSource();
            var calls = 0;
            var producer = Substitute.For<IKafkaProducer<string, string>>();
            producer.FireAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(_ =>
                {
                    var call = Interlocked.Increment(ref calls);
                    if (call == 1)
                    {
                        throw new InvalidOperationException("transient produce failure");
                    }

                    if (call >= 3)
                    {
                        cts.Cancel();
                    }

                    return ValueTask.CompletedTask;
                });

            await SoakStressTest.RunUnboundedProducerAsync(producer, options, "value", throughput, cts.Token);

            var snapshot = throughput.GetSnapshot();
            await Assert.That(throughput.MessageCount).IsEqualTo(2);
            await Assert.That(snapshot.TotalErrors).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static string CreateOutputDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dekaf-soak-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static StressTestOptions CreateOptions(ProgressWatchdog watchdog, int messagesPerSecond) => new()
    {
        BootstrapServers = "localhost:9092",
        Topic = "soak-topic",
        DurationMinutes = 1,
        MessageSizeBytes = 100,
        ProgressWatchdog = watchdog,
        SoakMessagesPerSecond = messagesPerSecond
    };
}
#endif
