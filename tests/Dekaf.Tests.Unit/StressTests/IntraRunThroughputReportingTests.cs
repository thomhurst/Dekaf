using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class IntraRunThroughputReportingTests
{
    [Test]
    public async Task ToJson_SerializesMetricsThresholdsAndBreachFlags()
    {
        var now = DateTime.UtcNow;
        var result = new StressTestResult
        {
            Scenario = "producer-acks-all",
            Client = "Dekaf",
            DurationMinutes = 15,
            MessageSizeBytes = 1000,
            StartedAtUtc = now,
            CompletedAtUtc = now.AddMinutes(15),
            Throughput = new ThroughputSnapshot
            {
                TotalMessages = 12_000,
                TotalBytes = 12_000_000,
                TotalErrors = 0,
                ElapsedSeconds = 900,
                AverageMessagesPerSecond = 1400,
                AverageMegabytesPerSecond = 1.34,
                SampledElapsedSeconds = 12,
                MessagesPerSecondSamples =
                [
                    1700, 1800, 1750, 2000, 2050, 1950,
                    1350, 1300, 1250, 1200, 1280, 1270
                ],
                ErrorSamples = []
            },
            GcStats = new GcSnapshot
            {
                Gen0Collections = 0,
                Gen1Collections = 0,
                Gen2Collections = 0,
                AllocatedBytes = 0
            }
        };

        var json = result.ToJson();

        await Assert.That(json).Contains("\"steadyStatePeakRatioThreshold\": 0.85");
        await Assert.That(json).Contains("\"throughputSlopePercentPerMinuteThreshold\": -1");
        await Assert.That(json).Contains("\"steadyStatePeakThresholdBreached\": true");
        await Assert.That(json).Contains("\"throughputSlopeThresholdBreached\": true");
        await Assert.That(json).Contains("\"intraRunThroughputThresholdBreached\": true");
        var slope = result.ThroughputSlopePercentPerMinute;
        await Assert.That(slope).IsNotNull();
        await Assert.That(slope!.Value).IsLessThan(-100);
    }
}
