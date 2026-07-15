using Dekaf.Producer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class ConnectionScaleReportingTests
{
    [Test]
    public async Task Generate_LongScaleTimeline_SamplesWholeRunAndReportsOmissions()
    {
        var startedAt = new DateTimeOffset(2026, 7, 12, 2, 0, 0, TimeSpan.Zero);
        var events = Enumerable.Range(0, 150)
            .Select(index => new ProducerConnectionScaleDiagnostic
            {
                OccurredAtUtc = startedAt.AddSeconds(index * 6),
                BrokerId = 1,
                OldConnectionCount = 6,
                NewConnectionCount = 6,
                PartitionLimited = true,
                BufferUtilization = 0,
                BufferPressureDelta = 0,
                SendLoopPressureDelta = 100,
                ObservationCount = 10,
                ObservedDurationMs = 6_000
            })
            .ToList();
        var result = new StressTestResult
        {
            Scenario = "producer",
            Client = "Dekaf",
            DurationMinutes = 15,
            BrokerCount = 1,
            MessageSizeBytes = 1000,
            StartedAtUtc = startedAt.UtcDateTime,
            CompletedAtUtc = startedAt.AddMinutes(15).UtcDateTime,
            Throughput = new ThroughputSnapshot
            {
                TotalMessages = 1_000,
                TotalBytes = 1_000_000,
                TotalErrors = 0,
                ElapsedSeconds = 900,
                AverageMessagesPerSecond = 1_000,
                AverageMegabytesPerSecond = 1,
                MessagesPerSecondSamples = [1_000, 900],
                IntervalSamples =
                [
                    new ThroughputIntervalSample
                    {
                        CapturedAtUtc = startedAt,
                        ElapsedSeconds = 0,
                        MessagesPerSecond = 1_000
                    },
                    new ThroughputIntervalSample
                    {
                        CapturedAtUtc = startedAt.AddMinutes(15),
                        ElapsedSeconds = 900,
                        MessagesPerSecond = 900
                    }
                ]
            },
            GcStats = new GcSnapshot
            {
                Gen0Collections = 0,
                Gen1Collections = 0,
                Gen2Collections = 0,
                AllocatedBytes = 0
            },
            ProducerDeliveryDiagnostics = new ProducerDeliveryDiagnosticsSnapshot
            {
                DiagnosticsEnabled = true,
                CapturedAtUtc = startedAt.AddMinutes(15),
                ConnectionScaleEvents = events
            }
        };

        var markdown = MarkdownReporter.Generate(new StressTestResults
        {
            RunStartedAtUtc = result.StartedAtUtc,
            RunCompletedAtUtc = result.CompletedAtUtc,
            MachineName = "test",
            ProcessorCount = 4,
            Results = [result]
        });

        await Assert.That(markdown).Contains("50 scale event(s) omitted");
        await Assert.That(markdown).Contains("02:00:00.000");
        await Assert.That(markdown).Contains("02:14:54.000");
    }

    [Test]
    public async Task Generate_ConnectionScaleEvent_CorrelatesNearestThroughputSample()
    {
        var startedAt = new DateTimeOffset(2026, 7, 12, 2, 0, 0, TimeSpan.Zero);
        var result = new StressTestResult
        {
            Scenario = "producer",
            Client = "Dekaf",
            DurationMinutes = 15,
            BrokerCount = 1,
            MessageSizeBytes = 1000,
            StartedAtUtc = startedAt.UtcDateTime,
            CompletedAtUtc = startedAt.AddMinutes(15).UtcDateTime,
            Throughput = new ThroughputSnapshot
            {
                TotalMessages = 10_000,
                TotalBytes = 10_000_000,
                TotalErrors = 0,
                ElapsedSeconds = 900,
                AverageMessagesPerSecond = 1_000,
                AverageMegabytesPerSecond = 1,
                MessagesPerSecondSamples = [1_000, 2_500],
                IntervalSamples =
                [
                    new ThroughputIntervalSample
                    {
                        CapturedAtUtc = startedAt.AddSeconds(2),
                        ElapsedSeconds = 2,
                        MessagesPerSecond = 1_000,
                        Gen2Collections = 0,
                        GcPauseDurationMs = 0
                    },
                    new ThroughputIntervalSample
                    {
                        CapturedAtUtc = startedAt.AddSeconds(6),
                        ElapsedSeconds = 6,
                        MessagesPerSecond = 2_500,
                        Gen2Collections = 1,
                        GcPauseDurationMs = 12.5
                    },
                    new ThroughputIntervalSample
                    {
                        CapturedAtUtc = startedAt.AddSeconds(10),
                        ElapsedSeconds = 10,
                        MessagesPerSecond = 2_400,
                        Gen2Collections = 2,
                        GcPauseDurationMs = 25
                    },
                    new ThroughputIntervalSample
                    {
                        CapturedAtUtc = startedAt.AddSeconds(14),
                        ElapsedSeconds = 14,
                        MessagesPerSecond = 2_300,
                        Gen2Collections = 3,
                        GcPauseDurationMs = 37.5
                    },
                    new ThroughputIntervalSample
                    {
                        CapturedAtUtc = startedAt.AddSeconds(18),
                        ElapsedSeconds = 18,
                        MessagesPerSecond = 400,
                        Gen2Collections = 3,
                        GcPauseDurationMs = 37.5
                    }
                ]
            },
            Latency = new LatencySnapshot
            {
                Count = 1,
                MinUs = 2_000_000,
                MaxUs = 2_000_000,
                P50Us = 2_000_000,
                P95Us = 2_000_000,
                P99Us = 2_000_000,
                OverflowCount = 0,
                DroppedOutlierSamples = 3,
                OutlierSamples =
                [
                    new LatencyOutlierSample
                    {
                        MessageIndex = 42,
                        StartedAtUtc = startedAt.AddSeconds(4),
                        CompletedAtUtc = startedAt.AddSeconds(6),
                        LatencyUs = 2_000_000
                    },
                    new LatencyOutlierSample
                    {
                        MessageIndex = 43,
                        StartedAtUtc = startedAt.AddSeconds(8),
                        CompletedAtUtc = startedAt.AddSeconds(10),
                        LatencyUs = 2_000_000
                    },
                    new LatencyOutlierSample
                    {
                        MessageIndex = 44,
                        StartedAtUtc = startedAt.AddSeconds(7),
                        CompletedAtUtc = startedAt.AddSeconds(14),
                        LatencyUs = 7_000_000
                    },
                    new LatencyOutlierSample
                    {
                        MessageIndex = 45,
                        StartedAtUtc = startedAt.AddSeconds(15),
                        CompletedAtUtc = startedAt.AddSeconds(18),
                        LatencyUs = 3_000_000
                    }
                ]
            },
            GcStats = new GcSnapshot
            {
                Gen0Collections = 0,
                Gen1Collections = 0,
                Gen2Collections = 0,
                AllocatedBytes = 0
            },
            ProducerDeliveryDiagnostics = new ProducerDeliveryDiagnosticsSnapshot
            {
                DiagnosticsEnabled = true,
                CapturedAtUtc = startedAt.AddMinutes(15),
                ProduceRequestCount = 2_500,
                ProduceRequestElapsedSeconds = 5,
                ProduceRequestsPerSecond = 500,
                BrokerProduceRequests =
                [
                    new ProducerBrokerRequestDiagnostic
                    {
                        BrokerId = 1,
                        RequestCount = 2_500,
                        RequestsPerSecond = 500,
                        AverageRequestBytes = 262_144
                    }
                ],
                CoalesceWidthHistogram =
                [
                    new ProducerCoalesceWidthDiagnostic
                    {
                        MinimumWidth = 1,
                        MaximumWidth = 1,
                        RequestCount = 2_000
                    },
                    new ProducerCoalesceWidthDiagnostic
                    {
                        MinimumWidth = 2,
                        MaximumWidth = 2,
                        RequestCount = 500
                    }
                ],
                ConnectionScaleEvents =
                [
                    new ProducerConnectionScaleDiagnostic
                    {
                        OccurredAtUtc = startedAt.AddSeconds(5),
                        BrokerId = 1,
                        OldConnectionCount = 1,
                        NewConnectionCount = 3,
                        BufferUtilization = 0.75,
                        BufferPressureDelta = 120,
                        SendLoopPressureDelta = 340
                    }
                ],
                BudgetProbeEvents =
                [
                    new ProducerBudgetProbeDiagnostic
                    {
                        OccurredAtUtc = startedAt.AddSeconds(6),
                        BrokerId = 1,
                        ProbeType = "min-rtt",
                        Outcome = "succeeded",
                        DurationMilliseconds = 2_000,
                        BudgetBytes = 8 * 1024 * 1024,
                        UnackedBytes = 6 * 1024 * 1024
                    }
                ],
                BrokerBudgets =
                [
                    new ProducerBrokerBudgetDiagnostic
                    {
                        CapturedAtUtc = startedAt.AddMinutes(15),
                        BrokerId = 1,
                        BudgetBytes = 8 * 1024 * 1024,
                        UnackedBytes = 0,
                        MinRttMicros = 800,
                        MaxRateBytesPerSec = 750_000_000,
                        AdmissionBlockCount = 12,
                        DeliveryLatencyEwmaMicros = 2_500,
                        LatencyBudgetScale = 1.0,
                        AdmissionBlockMicrosLog2Histogram =
                        [
                            0, 0, 0, 0, 0, 0, 0, 0,
                            0, 0, 0, 0, 3
                        ]
                    }
                ],
                BrokerBudgetSamples =
                [
                    new ProducerBrokerBudgetDiagnostic
                    {
                        CapturedAtUtc = startedAt.AddSeconds(6),
                        BrokerId = 1,
                        BudgetBytes = 8 * 1024 * 1024,
                        UnackedBytes = 6 * 1024 * 1024,
                        MinRttMicros = 800,
                        MaxRateBytesPerSec = 750_000_000,
                        AdmissionBlockCount = 12,
                        DeliveryLatencyEwmaMicros = 2_500,
                        LatencyBudgetScale = 1.0,
                        CapacityProbeSuccessCount = 3,
                        CapacityProbeFailureCount = 2
                    }
                ]
            }
        };
        var results = new StressTestResults
        {
            RunStartedAtUtc = result.StartedAtUtc,
            RunCompletedAtUtc = result.CompletedAtUtc,
            MachineName = "test",
            ProcessorCount = 4,
            Results = [result]
        };

        var markdown = MarkdownReporter.Generate(results);
        var json = result.ToJson();

        await Assert.That(markdown).Contains("Connection Scale Timeline - Fire-and-Forget");
        await Assert.That(markdown).Contains("1→3");
        await Assert.That(markdown).Contains("75%");
        await Assert.That(markdown).Contains("120/340");
        await Assert.That(markdown).Contains("6.0s / 2,500 msg/s");
        await Assert.That(markdown).Contains("Producer Request Diagnostics - Fire-and-Forget");
        await Assert.That(markdown).Contains("| Dekaf | 1 | 2,500 | 500.00 | 256.0 KiB |");
        await Assert.That(markdown).Contains("Producer Budget Timeline - Fire-and-Forget");
        await Assert.That(markdown).Contains("8.0 MiB");
        await Assert.That(markdown).Contains("750.0 MB/s");
        await Assert.That(markdown).Contains("3/2");
        await Assert.That(markdown).Contains("Producer Budget Probe Events - Fire-and-Forget");
        await Assert.That(markdown).Contains("min-rtt");
        await Assert.That(markdown).Contains("Producer Admission Block Durations - Fire-and-Forget");
        await Assert.That(markdown).Contains("4.096–8.192ms");
        await Assert.That(markdown).Contains("Delivery Latency Outliers - Fire-and-Forget");
        await Assert.That(markdown).Contains("42");
        await Assert.That(markdown).Contains("Probe overlap is temporal correlation only");
        await Assert.That(markdown).Contains("1:min-rtt/succeeded");
        await Assert.That(markdown).Contains("Gen2 +1 / pause +12.5ms");
        await Assert.That(markdown).Contains("43");
        await Assert.That(markdown).Contains("GC pause");
        await Assert.That(markdown).Contains("44");
        await Assert.That(markdown).Contains("Gen2 +2 / pause +25.0ms");
        await Assert.That(markdown).Contains("| Dekaf | 45 | 02:00:15.000 | 3.0s | throughput collapse |");
        await Assert.That(markdown).Contains("3 additional latency outlier sample(s) exceeded the bounded diagnostic capacity");
        await Assert.That(json).Contains("\"produceRequestCount\": 2500");
        await Assert.That(json).Contains("\"produceRequestsPerSecond\": 500");
        await Assert.That(json).Contains("\"averageRequestBytes\": 262144");
        await Assert.That(json).Contains("\"coalesceWidthHistogram\"");
        await Assert.That(json).Contains("\"requestCount\": 2000");

        result.ProducerDeliveryDiagnostics.BudgetProbeEvents.Clear();
        var markdownWithoutProbeEvents = MarkdownReporter.Generate(results);

        await Assert.That(markdownWithoutProbeEvents).DoesNotContain("Producer Budget Probe Events");
        await Assert.That(markdownWithoutProbeEvents).Contains("Producer Admission Block Durations");
    }
}
