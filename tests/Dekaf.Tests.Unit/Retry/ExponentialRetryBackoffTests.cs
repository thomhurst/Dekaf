using Dekaf.Retry;

namespace Dekaf.Tests.Unit.Retry;

public sealed class ExponentialRetryBackoffTests
{
    [Test]
    [Arguments(0.0, 80.0)]
    [Arguments(0.5, 100.0)]
    [Arguments(1.0, 120.0)]
    public async Task FirstFailure_AppliesUnbiasedTwentyPercentJitter(
        double randomValue,
        double expectedDelayMs)
    {
        var delayMs = ExponentialRetryBackoff.CalculateDelayMilliseconds(
            initialDelayMs: 100,
            maximumDelayMs: 1000,
            failureCount: 1,
            randomValue);

        await Assert.That(delayMs).IsEqualTo(expectedDelayMs).Within(0.001);
    }

    [Test]
    public async Task RepeatedFailures_DoubleBeforeJitterAndCap()
    {
        var secondFailure = ExponentialRetryBackoff.CalculateDelayMilliseconds(100, 1000, 2, 0.5);
        var fourthFailureLowJitter = ExponentialRetryBackoff.CalculateDelayMilliseconds(100, 1000, 4, 0.0);
        var fifthFailureLowJitter = ExponentialRetryBackoff.CalculateDelayMilliseconds(100, 1000, 5, 0.0);

        await Assert.That(secondFailure).IsEqualTo(200);
        await Assert.That(fourthFailureLowJitter).IsEqualTo(640);
        await Assert.That(fifthFailureLowJitter).IsEqualTo(1000);
    }

    [Test]
    public async Task InitialAtOrAboveMaximum_UsesMaximumWithoutJitter()
    {
        var equal = ExponentialRetryBackoff.CalculateDelayMilliseconds(1000, 1000, 1, 0.0);
        var above = ExponentialRetryBackoff.CalculateDelayMilliseconds(2000, 1000, 10, 1.0);

        await Assert.That(equal).IsEqualTo(1000);
        await Assert.That(above).IsEqualTo(1000);
    }

    [Test]
    public async Task ExtremeFailureCount_CapsWithoutOverflow()
    {
        var delayMs = ExponentialRetryBackoff.CalculateDelayMilliseconds(
            initialDelayMs: 1,
            maximumDelayMs: int.MaxValue,
            failureCount: int.MaxValue,
            randomValue: 0.0);

        await Assert.That(delayMs).IsEqualTo(int.MaxValue);
    }

    [Test]
    [Arguments(0, 1000)]
    [Arguments(100, 0)]
    public async Task ZeroBound_DisablesDelay(int initialDelayMs, int maximumDelayMs)
    {
        var delayMs = ExponentialRetryBackoff.CalculateDelayMilliseconds(
            initialDelayMs,
            maximumDelayMs,
            failureCount: 10,
            randomValue: 0.5);

        await Assert.That(delayMs).IsEqualTo(0);
    }
}
