using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class IntraRunThroughputReportingTests
{
    [Test]
    public async Task ToJson_SerializesMetricsThresholdsAndBreachFlags()
    {
        var result = CreateResult(
        [
            1700, 1800, 1750, 2000, 2050, 1950,
            1350, 1300, 1250, 1200, 1280, 1270
        ], elapsedSeconds: 900, sampledElapsedSeconds: 12);

        var json = result.ToJson();

        await Assert.That(json).Contains("\"steadyStatePeakRatioThreshold\": 0.85");
        await Assert.That(json).Contains("\"throughputSlopePercentPerMinuteThreshold\": -1");
        await Assert.That(json).Contains("\"steadyStatePeakThresholdBreached\": true");
        await Assert.That(json).Contains("\"throughputSlopeThresholdBreached\": true");
        await Assert.That(json).Contains("\"intraRunThroughputThresholdBreached\": true");
        await Assert.That(result.SteadyStatePeakRatio!.Value)
            .IsBetween(0.60975609, 0.60975610);
        await Assert.That(result.IntraRunDriftPercent!.Value)
            .IsBetween(-31.03448277, -31.03448275);
        await Assert.That(result.ThroughputSlopePercentPerMinute!.Value)
            .IsBetween(-229.872197, -229.872196);
    }

    [Test]
    public async Task SteadyStatePeakRatio_IgnoresSingleTransientSpike()
    {
        var samples = Enumerable.Repeat(1_000.0, 99).Append(10_000.0).ToList();
        var result = CreateResult(samples, elapsedSeconds: 100, sampledElapsedSeconds: 100);

        await Assert.That(result.SteadyStatePeakRatio!.Value).IsEqualTo(1.0);
        await Assert.That(result.SteadyStatePeakThresholdBreached).IsFalse();
    }

    [Test]
    public async Task IntraRunMetrics_FrontLoadedBurstThenZero_ReportsCollapse()
    {
        var samples = Enumerable.Repeat(1_000.0, 5)
            .Concat(Enumerable.Repeat(0.0, 95))
            .ToList();
        var result = CreateResult(samples, elapsedSeconds: 100, sampledElapsedSeconds: 100);

        await Assert.That(result.SteadyStatePeakRatio).IsEqualTo(0.0);
        await Assert.That(result.IntraRunDriftPercent).IsEqualTo(-100.0);
        await Assert.That(result.SteadyStatePeakThresholdBreached).IsTrue();
        await Assert.That(result.IntraRunThroughputThresholdBreached).IsTrue();
    }

    [Test]
    [Arguments("message-bounded")]
    [Arguments("zero-elapsed")]
    [Arguments("too-few-samples")]
    [Arguments("zero-baseline")]
    public async Task IntraRunMetrics_UnsupportedMeasurement_ReturnsNull(string guard)
    {
        var result = guard switch
        {
            "message-bounded" => CreateResult([100, 90, 80], isMessageBounded: true),
            "zero-elapsed" => CreateResult([100, 90, 80], elapsedSeconds: 0),
            "too-few-samples" => CreateResult([100, 90]),
            "zero-baseline" => CreateResult([0, 0, 100, 90, 80, 70]),
            _ => throw new ArgumentOutOfRangeException(nameof(guard), guard, null)
        };

        await Assert.That(result.SteadyStatePeakRatio).IsNull();
        await Assert.That(result.IntraRunDriftPercent).IsNull();
        await Assert.That(result.ThroughputSlopePercentPerMinute).IsNull();
        await Assert.That(result.IntraRunThroughputThresholdBreached).IsFalse();
    }

    private static StressTestResult CreateResult(
        List<double> samples,
        double elapsedSeconds = 12,
        double sampledElapsedSeconds = 12,
        bool isMessageBounded = false)
    {
        var now = DateTime.UtcNow;
        return new StressTestResult
        {
            Scenario = "producer-acks-all",
            Client = "Dekaf",
            DurationMinutes = 15,
            MessageSizeBytes = 1000,
            StartedAtUtc = now,
            CompletedAtUtc = now.AddMinutes(15),
            IsMessageBounded = isMessageBounded,
            Throughput = new ThroughputSnapshot
            {
                TotalMessages = 12_000,
                TotalBytes = 12_000_000,
                TotalErrors = 0,
                ElapsedSeconds = elapsedSeconds,
                AverageMessagesPerSecond = 1400,
                AverageMegabytesPerSecond = 1.34,
                SampledElapsedSeconds = sampledElapsedSeconds,
                MessagesPerSecondSamples = samples,
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
    }
}
