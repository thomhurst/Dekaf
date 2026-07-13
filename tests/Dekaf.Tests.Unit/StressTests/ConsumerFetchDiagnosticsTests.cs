using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Networking;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class ConsumerFetchDiagnosticsTests
{
    [Test]
    [NotInParallel]
    public async Task Listener_CollectsFetchDurationAndTopicBytes()
    {
        var startedAt = new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
        using var tracker = new ConsumerFetchDiagnosticsTracker("consumer-topic");
        tracker.Start(CreateConsumerSnapshot(startedAt));

        DekafMetrics.FetchDuration.Record(0.02);
        DekafMetrics.BytesReceived.Add(
            2_048,
            new KeyValuePair<string, object?>(
                DekafDiagnostics.MessagingDestinationName,
                "consumer-topic"));
        DekafMetrics.BytesReceived.Add(
            8_192,
            new KeyValuePair<string, object?>(
                DekafDiagnostics.MessagingDestinationName,
                "different-topic"));
        tracker.TakeSample(CreateConsumerSnapshot(startedAt.AddSeconds(1)));

        var sample = tracker.GetSnapshot().Samples.Single();
        await Assert.That(sample.FetchRequestCount).IsEqualTo(1);
        await Assert.That(sample.BytesPerFetch).IsEqualTo(2_048);
        await Assert.That(sample.AverageFetchRttMs).IsBetween(19.9, 20.1);
    }

    [Test]
    [NotInParallel]
    public async Task Listener_RejectsConcurrentConsumerDiagnosticsTracker()
    {
        using var tracker = new ConsumerFetchDiagnosticsTracker("consumer-topic");

        await Assert.That(() => new ConsumerFetchDiagnosticsTracker("other-topic"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task TakeSample_ReportsIntervalFetchAndQueueMetrics()
    {
        var startedAt = new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
        using var tracker = new ConsumerFetchDiagnosticsTracker("consumer-topic", listenToMetrics: false);
        tracker.Start(CreateConsumerSnapshot(startedAt));

        tracker.RecordFetchDuration(0.1);
        tracker.RecordFetchDuration(0.3);
        tracker.RecordBytesReceived(1_000);
        tracker.TakeSample(CreateConsumerSnapshot(
            startedAt.AddMinutes(1),
            pendingFetchDepth: 2,
            prefetchBufferDepth: 3,
            prefetchedBytes: 4_096));

        var sample = tracker.GetSnapshot().Samples.Single();
        await Assert.That(sample.IntervalSeconds).IsEqualTo(60.0);
        await Assert.That(sample.FetchRequestCount).IsEqualTo(2);
        await Assert.That(sample.FetchRequestsPerSecond).IsBetween(0.0333, 0.0334);
        await Assert.That(sample.BytesPerFetch).IsEqualTo(500.0);
        await Assert.That(sample.AverageFetchRttMs).IsBetween(199.9, 200.1);
        await Assert.That(sample.PendingFetchDepth).IsEqualTo(2);
        await Assert.That(sample.PrefetchBufferDepth).IsEqualTo(3);
        await Assert.That(sample.PrefetchDepth).IsEqualTo(5);
        await Assert.That(sample.PrefetchedBytes).IsEqualTo(4_096);
    }

    [Test]
    public async Task TakeSample_ReportsPerIntervalGcDiagnostics()
    {
        var startedAt = new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
        var gcSnapshots = new Queue<GcDiagnosticSnapshot>(
        [
            new GcDiagnosticSnapshot(10, 4, 2, 100, 20 * 1024 * 1024, 2, 1, true),
            new GcDiagnosticSnapshot(13, 6, 3, 145, 24 * 1024 * 1024, 1, 1, true)
        ]);
        using var tracker = new ConsumerFetchDiagnosticsTracker(
            "consumer-topic",
            listenToMetrics: false,
            captureGcDiagnostics: gcSnapshots.Dequeue);
        tracker.Start(CreateConsumerSnapshot(startedAt));

        tracker.TakeSample(CreateConsumerSnapshot(startedAt.AddMinutes(1)));

        var sample = tracker.GetSnapshot().Samples.Single();
        await Assert.That(sample.Gen0Collections).IsEqualTo(3);
        await Assert.That(sample.Gen1Collections).IsEqualTo(2);
        await Assert.That(sample.Gen2Collections).IsEqualTo(1);
        await Assert.That(sample.GcPauseDurationMs).IsEqualTo(45);
        await Assert.That(sample.GcHeapSizeBytes).IsEqualTo(24 * 1024 * 1024);
        await Assert.That(sample.GcHeapCount).IsEqualTo(1);
        await Assert.That(sample.GcDynamicAdaptationMode).IsEqualTo(1);
        await Assert.That(sample.ServerGc).IsTrue();
    }

    [Test]
    public async Task TakeSample_UsesPerIntervalDeltasAndKeepsConnectionReaps()
    {
        var startedAt = new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
        using var tracker = new ConsumerFetchDiagnosticsTracker("consumer-topic", listenToMetrics: false);
        tracker.Start(CreateConsumerSnapshot(startedAt));

        tracker.RecordFetchDuration(0.1);
        tracker.RecordBytesReceived(900);
        tracker.TakeSample(CreateConsumerSnapshot(startedAt.AddMinutes(1)));

        tracker.RecordFetchDuration(0.25);
        tracker.RecordBytesReceived(400);
        var reap = new ConnectionReapDiagnostic
        {
            OccurredAtUtc = startedAt.AddMinutes(1.5),
            BrokerId = 2,
            ConnectionIndex = 1,
            IdleDurationMs = 540_010,
            IsBootstrapConnection = false
        };
        tracker.TakeSample(CreateConsumerSnapshot(startedAt.AddMinutes(2), connectionReaps: [reap]));

        var snapshot = tracker.GetSnapshot();
        var second = snapshot.Samples[1];
        await Assert.That(second.FetchRequestCount).IsEqualTo(1);
        await Assert.That(second.BytesPerFetch).IsEqualTo(400.0);
        await Assert.That(second.AverageFetchRttMs).IsBetween(249.9, 250.1);
        await Assert.That(snapshot.ConnectionReapEvents).HasSingleItem();
        await Assert.That(snapshot.ConnectionReapEvents[0].ConnectionIndex).IsEqualTo(1);
    }

    [Test]
    public async Task TryTakeSample_DoesNotPropagateDiagnosticFailure()
    {
        using var tracker = new ConsumerFetchDiagnosticsTracker("consumer-topic", listenToMetrics: false);

        tracker.TryTakeSample(() => throw new InvalidOperationException("diagnostic failure"));

        await Assert.That(tracker.GetSnapshot().Samples).IsEmpty();
    }

    [Test]
    public async Task Result_SerializesAndReportsConsumerFetchTimeline()
    {
        var capturedAt = new DateTimeOffset(2026, 7, 13, 10, 1, 0, TimeSpan.Zero);
        var result = CreateResult(new ConsumerFetchDiagnosticsSnapshot
        {
            Samples =
            [
                new ConsumerFetchDiagnosticSample
                {
                    CapturedAtUtc = capturedAt,
                    IntervalSeconds = 60,
                    FetchRequestCount = 120,
                    FetchRequestsPerSecond = 2,
                    BytesPerFetch = 524_288,
                    AverageFetchRttMs = 7.5,
                    PendingFetchDepth = 2,
                    PrefetchBufferDepth = 3,
                    PrefetchDepth = 5,
                    PrefetchedBytes = 8_388_608,
                    Gen0Collections = 3,
                    Gen1Collections = 2,
                    Gen2Collections = 1,
                    GcPauseDurationMs = 45,
                    GcHeapSizeBytes = 24 * 1024 * 1024,
                    GcHeapCount = 1,
                    GcDynamicAdaptationMode = 0,
                    ServerGc = true
                }
            ],
            ConnectionReapEvents =
            [
                new ConnectionReapDiagnostic
                {
                    OccurredAtUtc = capturedAt.AddSeconds(-5),
                    BrokerId = 1,
                    ConnectionIndex = 0,
                    IdleDurationMs = 540_000,
                    IsBootstrapConnection = false
                }
            ]
        });

        var json = result.ToJson();
        var markdown = MarkdownReporter.Generate(new StressTestResults
        {
            RunStartedAtUtc = capturedAt.UtcDateTime.AddMinutes(-1),
            RunCompletedAtUtc = capturedAt.UtcDateTime,
            MachineName = "test",
            ProcessorCount = 4,
            Results = [result]
        });

        await Assert.That(json).Contains("\"consumerFetchDiagnostics\"");
        await Assert.That(json).Contains("\"fetchRequestsPerSecond\": 2");
        await Assert.That(markdown).Contains("Consumer Fetch Timeline - Consumer");
        await Assert.That(markdown).Contains("512.0 KiB");
        await Assert.That(markdown).Contains("7.50ms");
        await Assert.That(markdown).Contains("1 reap");
        await Assert.That(markdown).Contains("2 / 3 / 5");
        await Assert.That(markdown).Contains("3 / 2 / 1");
        await Assert.That(markdown).Contains("45.0ms");
        await Assert.That(markdown).Contains("24.0 MiB / 1");
        await Assert.That(markdown).Contains("Server GC; DATAS=0");
    }

    [Test]
    public async Task Report_CapsConsumerFetchTimelineRows()
    {
        var capturedAt = new DateTimeOffset(2026, 7, 13, 10, 1, 0, TimeSpan.Zero);
        var result = CreateResult(new ConsumerFetchDiagnosticsSnapshot
        {
            Samples =
            [
                .. Enumerable.Range(0, 150).Select(index => new ConsumerFetchDiagnosticSample
                {
                    CapturedAtUtc = capturedAt.AddMinutes(index),
                    IntervalSeconds = 60,
                    FetchRequestCount = 0,
                    FetchRequestsPerSecond = 0,
                    BytesPerFetch = 0,
                    AverageFetchRttMs = 0,
                    PendingFetchDepth = 0,
                    PrefetchBufferDepth = 0,
                    PrefetchDepth = 0,
                    PrefetchedBytes = 0
                })
            ],
            ConnectionReapEvents = []
        });

        var markdown = MarkdownReporter.Generate(new StressTestResults
        {
            RunStartedAtUtc = capturedAt.UtcDateTime,
            RunCompletedAtUtc = capturedAt.UtcDateTime.AddMinutes(150),
            MachineName = "test",
            ProcessorCount = 4,
            Results = [result]
        });

        await Assert.That(markdown).Contains("50 fetch sample(s) omitted");
    }

    [Test]
    public async Task Report_PreservesGcTableUnitConvention()
    {
        var capturedAt = new DateTimeOffset(2026, 7, 13, 10, 1, 0, TimeSpan.Zero);
        var result = CreateResult(new ConsumerFetchDiagnosticsSnapshot
        {
            Samples =
            [
                new ConsumerFetchDiagnosticSample
                {
                    CapturedAtUtc = capturedAt,
                    IntervalSeconds = 60,
                    FetchRequestCount = 0,
                    FetchRequestsPerSecond = 0,
                    BytesPerFetch = 0,
                    AverageFetchRttMs = 0,
                    PendingFetchDepth = 0,
                    PrefetchBufferDepth = 0,
                    PrefetchDepth = 0,
                    PrefetchedBytes = 0
                }
            ],
            ConnectionReapEvents = []
        }, allocatedBytes: 1_048_576);

        var markdown = MarkdownReporter.Generate(new StressTestResults
        {
            RunStartedAtUtc = capturedAt.UtcDateTime.AddMinutes(-1),
            RunCompletedAtUtc = capturedAt.UtcDateTime,
            MachineName = "test",
            ProcessorCount = 4,
            Results = [result]
        });

        await Assert.That(markdown).Contains("1.00 MB");
        await Assert.That(markdown).Contains("1.02 KB");
    }

    private static ConsumerDiagnosticSnapshot CreateConsumerSnapshot(
        DateTimeOffset capturedAtUtc,
        int pendingFetchDepth = 0,
        int prefetchBufferDepth = 0,
        long prefetchedBytes = 0,
        ConnectionReapDiagnostic[]? connectionReaps = null) =>
        new()
        {
            CapturedAtUtc = capturedAtUtc,
            FetchPositions = [],
            Assignment = [],
            PrefetchedBytes = prefetchedBytes,
            PendingFetchDepth = pendingFetchDepth,
            PrefetchBufferDepth = prefetchBufferDepth,
            PrefetchDepth = pendingFetchDepth + prefetchBufferDepth,
            PendingRevocations = [],
            PendingRevocationMarkerPresent = false,
            PendingRevocationClearPending = false,
            PendingDivergingEpochResets = [],
            FetchBufferEpoch = 0,
            MinimumFetchBufferEpoch = 0,
            MinimumFetchBufferEpochsByPartition = [],
            AdaptivePartitionFetchBytes = null,
            AdaptiveFetchMaxBytes = null,
            ConnectionReapEvents = connectionReaps ?? []
        };

    private static StressTestResult CreateResult(
        ConsumerFetchDiagnosticsSnapshot diagnostics,
        long allocatedBytes = 0)
    {
        var capturedAt = diagnostics.Samples[0].CapturedAtUtc;
        return new StressTestResult
        {
            Scenario = "consumer",
            Client = "Dekaf",
            DurationMinutes = 15,
            MessageSizeBytes = 1_000,
            StartedAtUtc = capturedAt.UtcDateTime.AddMinutes(-1),
            CompletedAtUtc = capturedAt.UtcDateTime,
            Throughput = new ThroughputSnapshot
            {
                TotalMessages = 1_000,
                TotalBytes = 1_000_000,
                TotalErrors = 0,
                ElapsedSeconds = 60,
                AverageMessagesPerSecond = 1_000,
                AverageMegabytesPerSecond = 0.95,
                MessagesPerSecondSamples = [1_000, 1_000, 1_000],
                IntervalSamples =
                [
                    new ThroughputIntervalSample
                    {
                        CapturedAtUtc = capturedAt,
                        ElapsedSeconds = 60,
                        MessagesPerSecond = 1_000
                    }
                ],
                SampledElapsedSeconds = 60,
                ErrorSamples = []
            },
            GcStats = new GcSnapshot
            {
                Gen0Collections = 0,
                Gen1Collections = 0,
                Gen2Collections = 0,
                AllocatedBytes = allocatedBytes
            },
            ConsumerFetchDiagnostics = diagnostics
        };
    }
}
