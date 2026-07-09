#if NET10_0
using Dekaf.StressTests.Scenarios;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class SoakStressTestTests
{
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
}
#endif
