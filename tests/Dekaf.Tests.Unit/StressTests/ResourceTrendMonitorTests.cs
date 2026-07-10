#if NET10_0
using Dekaf.StressTests.Metrics;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class ResourceTrendMonitorTests
{
    [Test]
    [Arguments(0.01, 60, false)]
    [Arguments(29.99, 60, false)]
    [Arguments(30, 60, true)]
    [Arguments(60, 60, true)]
    public async Task ShouldCaptureFinalSample_RequiresMeaningfulPartialInterval(
        double elapsedSeconds,
        double sampleIntervalSeconds,
        bool expected)
    {
        var result = ResourceTrendMonitor.ShouldCaptureFinalSample(
            TimeSpan.FromSeconds(elapsedSeconds),
            TimeSpan.FromSeconds(sampleIntervalSeconds));

        await Assert.That(result).IsEqualTo(expected);
    }
}
#endif
