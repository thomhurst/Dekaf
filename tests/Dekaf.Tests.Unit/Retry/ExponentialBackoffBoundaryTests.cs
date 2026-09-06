using System.Numerics;
using Dekaf.Retry;

namespace Dekaf.Tests.Unit.Retry;

public class ExponentialBackoffBoundaryTests
{
    private static readonly Exception Failure = new InvalidOperationException("retry");

    [Test]
    [Arguments(63)]
    [Arguments(64)]
    [Arguments(65)]
    [Arguments(66)]
    [Arguments(67)]
    [Arguments(129)]
    [Arguments(1000000)]
    [Arguments(int.MaxValue)]
    public async Task CappedDelay_DoesNotRestartAtLargeAttempts(int attempt)
    {
        var policy = Create(TimeSpan.TicksPerSecond, 30 * TimeSpan.TicksPerSecond);

        await Assert.That(policy.GetNextDelay(attempt, Failure)).IsEqualTo(TimeSpan.FromSeconds(30));
    }

    [Test]
    [Arguments(1L, long.MaxValue)]
    [Arguments(4611686018427387905L, long.MaxValue)]
    [Arguments(long.MaxValue, long.MaxValue)]
    [Arguments(0L, long.MaxValue)]
    [Arguments(1L, 0L)]
    [Arguments(10L, 5L)]
    [Arguments(3L, 17L)]
    [Arguments(10000000L, 300000000L)]
    public async Task UnjitteredDelay_MatchesUnboundedIntegerFormula(long baseTicks, long maximumTicks)
    {
        var policy = Create(baseTicks, maximumTicks);
        for (var attempt = 1; attempt <= 130; attempt++)
        {
            var expected = (long)BigInteger.Min(maximumTicks, new BigInteger(baseTicks) << (attempt - 1));
            await Assert.That(policy.GetNextDelay(attempt, Failure)).IsEqualTo(TimeSpan.FromTicks(expected));
        }
    }

    [Test]
    [Arguments(0L, 17L)]
    [Arguments(3L, 17L)]
    [Arguments(17L, 3L)]
    [Arguments(long.MaxValue, long.MaxValue)]
    public async Task DelayConfiguration_IsIndependentOfInitializationOrder(long baseTicks, long maximumTicks)
    {
        var policy = new ExponentialBackoffRetryPolicy
        {
            MaxDelay = TimeSpan.FromTicks(maximumTicks),
            BaseDelay = TimeSpan.FromTicks(baseTicks),
            MaxAttempts = int.MaxValue,
            Jitter = false
        };

        for (var attempt = 1; attempt <= 130; attempt++)
        {
            var expected = (long)BigInteger.Min(maximumTicks, new BigInteger(baseTicks) << (attempt - 1));
            await Assert.That(policy.GetNextDelay(attempt, Failure)).IsEqualTo(TimeSpan.FromTicks(expected));
        }
    }

    [Test]
    public async Task ZeroBaseDelay_RemainsZeroAtMaximumAttempt()
    {
        await Assert.That(Create(0, long.MaxValue).GetNextDelay(int.MaxValue, Failure))
            .IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    [Arguments(int.MinValue)]
    public async Task NonpositiveAttempt_IsRejectedEvenWhenRetriesDisabled(int attempt)
    {
        var policy = new ExponentialBackoffRetryPolicy
        {
            BaseDelay = TimeSpan.Zero, MaxDelay = TimeSpan.Zero, MaxAttempts = 0
        };

        await Assert.That(() => policy.GetNextDelay(attempt, Failure)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments(-1L, 10L, 1, "BaseDelay")]
    [Arguments(1L, -1L, 1, "MaxDelay")]
    [Arguments(1L, 10L, -1, "MaxAttempts")]
    public async Task NegativeConfiguration_IsRejected(long baseTicks, long maximumTicks, int maximumAttempts, string parameter)
    {
        var exception = await Assert.That(() => new ExponentialBackoffRetryPolicy
        {
            BaseDelay = TimeSpan.FromTicks(baseTicks),
            MaxDelay = TimeSpan.FromTicks(maximumTicks),
            MaxAttempts = maximumAttempts
        }).Throws<ArgumentOutOfRangeException>();

        await Assert.That(exception!.ParamName).IsEqualTo(parameter);
    }

    [Test]
    public async Task ZeroMaximumAttempts_DisablesRetries()
    {
        var policy = new ExponentialBackoffRetryPolicy
        {
            BaseDelay = TimeSpan.Zero, MaxDelay = TimeSpan.Zero, MaxAttempts = 0
        };

        await Assert.That(policy.GetNextDelay(1, Failure)).IsNull();
        await Assert.That(policy.GetNextDelay(int.MaxValue, Failure)).IsNull();
    }

    [Test]
    [Arguments(1L)]
    [Arguments(300000000L)]
    [Arguments(long.MaxValue)]
    public async Task JitteredCap_StaysNonnegativeAndWithinDocumentedRange(long maximumTicks)
    {
        var policy = new ExponentialBackoffRetryPolicy
        {
            BaseDelay = TimeSpan.FromTicks(maximumTicks),
            MaxDelay = TimeSpan.FromTicks(maximumTicks),
            MaxAttempts = int.MaxValue,
            Jitter = true
        };

        for (var sample = 0; sample < 200; sample++)
        {
            var delay = policy.GetNextDelay(int.MaxValue, Failure)!.Value.Ticks;
            await Assert.That(delay).IsGreaterThanOrEqualTo(maximumTicks / 2);
            await Assert.That(delay).IsLessThanOrEqualTo(maximumTicks);
        }
    }

    private static ExponentialBackoffRetryPolicy Create(long baseTicks, long maximumTicks) => new()
    {
        BaseDelay = TimeSpan.FromTicks(baseTicks),
        MaxDelay = TimeSpan.FromTicks(maximumTicks),
        MaxAttempts = int.MaxValue,
        Jitter = false
    };
}
