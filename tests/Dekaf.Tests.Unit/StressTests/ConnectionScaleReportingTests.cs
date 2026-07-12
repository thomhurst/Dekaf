using Dekaf.Producer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class ConnectionScaleReportingTests
{
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
                        MessagesPerSecond = 1_000
                    },
                    new ThroughputIntervalSample
                    {
                        CapturedAtUtc = startedAt.AddSeconds(6),
                        ElapsedSeconds = 6,
                        MessagesPerSecond = 2_500
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

        await Assert.That(markdown).Contains("Connection Scale Timeline - Fire-and-Forget");
        await Assert.That(markdown).Contains("1→3");
        await Assert.That(markdown).Contains("75%");
        await Assert.That(markdown).Contains("120/340");
        await Assert.That(markdown).Contains("6.0s / 2,500 msg/s");
    }
}
