using Dekaf.Retry;

namespace Dekaf.Tests.Unit.Retry;

public class RetryPolicyTests
{
    #region NoRetryPolicy Tests

    [Test]
    public async Task NoRetryPolicy_AlwaysReturnsNull()
    {
        var policy = NoRetryPolicy.Instance;
        await Assert.That(policy.GetNextDelay(1, new InvalidOperationException("test"))).IsNull();
        await Assert.That(policy.GetNextDelay(100, new InvalidOperationException("test"))).IsNull();
    }

    [Test]
    public async Task NoRetryPolicy_IsSingleton()
    {
        await Assert.That(NoRetryPolicy.Instance).IsSameReferenceAs(NoRetryPolicy.Instance);
    }

    #endregion

    #region FixedDelayRetryPolicy Tests

    [Test]
    public async Task FixedDelay_ReturnsConstantDelay()
    {
        var policy = new FixedDelayRetryPolicy { Delay = TimeSpan.FromSeconds(1), MaxAttempts = 3 };

        await Assert.That(policy.GetNextDelay(1, new InvalidOperationException("test"))).IsEqualTo(TimeSpan.FromSeconds(1));
        await Assert.That(policy.GetNextDelay(2, new InvalidOperationException("test"))).IsEqualTo(TimeSpan.FromSeconds(1));
        await Assert.That(policy.GetNextDelay(3, new InvalidOperationException("test"))).IsEqualTo(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task FixedDelay_ReturnsNullAfterMaxAttempts()
    {
        var policy = new FixedDelayRetryPolicy { Delay = TimeSpan.FromSeconds(1), MaxAttempts = 2 };

        await Assert.That(policy.GetNextDelay(1, new InvalidOperationException("test"))).IsNotNull();
        await Assert.That(policy.GetNextDelay(2, new InvalidOperationException("test"))).IsNotNull();
        await Assert.That(policy.GetNextDelay(3, new InvalidOperationException("test"))).IsNull();
    }

    [Test]
    public async Task FixedDelay_MaxAttemptsOne_RetriesOnce()
    {
        var policy = new FixedDelayRetryPolicy { Delay = TimeSpan.FromMilliseconds(100), MaxAttempts = 1 };

        await Assert.That(policy.GetNextDelay(1, new InvalidOperationException("test"))).IsEqualTo(TimeSpan.FromMilliseconds(100));
        await Assert.That(policy.GetNextDelay(2, new InvalidOperationException("test"))).IsNull();
    }

    #endregion

    #region ExponentialBackoffRetryPolicy Tests

    [Test]
    public async Task ExponentialBackoff_DelaysGrowExponentially()
    {
        var policy = new ExponentialBackoffRetryPolicy
        {
            BaseDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(60),
            MaxAttempts = 5,
            Jitter = false
        };

        await Assert.That(policy.GetNextDelay(1, new InvalidOperationException("test"))).IsEqualTo(TimeSpan.FromSeconds(1));
        await Assert.That(policy.GetNextDelay(2, new InvalidOperationException("test"))).IsEqualTo(TimeSpan.FromSeconds(2));
        await Assert.That(policy.GetNextDelay(3, new InvalidOperationException("test"))).IsEqualTo(TimeSpan.FromSeconds(4));
        await Assert.That(policy.GetNextDelay(4, new InvalidOperationException("test"))).IsEqualTo(TimeSpan.FromSeconds(8));
        await Assert.That(policy.GetNextDelay(5, new InvalidOperationException("test"))).IsEqualTo(TimeSpan.FromSeconds(16));
    }

    [Test]
    public async Task ExponentialBackoff_CapsAtMaxDelay()
    {
        var policy = new ExponentialBackoffRetryPolicy
        {
            BaseDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(5),
            MaxAttempts = 10,
            Jitter = false
        };

        // 2^4 = 16 > 5, so attempt 5 should be capped at 5s
        await Assert.That(policy.GetNextDelay(5, new InvalidOperationException("test"))).IsEqualTo(TimeSpan.FromSeconds(5));
        await Assert.That(policy.GetNextDelay(10, new InvalidOperationException("test"))).IsEqualTo(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task ExponentialBackoff_ReturnsNullAfterMaxAttempts()
    {
        var policy = new ExponentialBackoffRetryPolicy
        {
            BaseDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(30),
            MaxAttempts = 3,
            Jitter = false
        };

        await Assert.That(policy.GetNextDelay(3, new InvalidOperationException("test"))).IsNotNull();
        await Assert.That(policy.GetNextDelay(4, new InvalidOperationException("test"))).IsNull();
    }

    [Test]
    public async Task ExponentialBackoff_WithJitter_DelayIsWithinBounds()
    {
        var policy = new ExponentialBackoffRetryPolicy
        {
            BaseDelay = TimeSpan.FromSeconds(10),
            MaxDelay = TimeSpan.FromSeconds(600),
            MaxAttempts = 5,
            Jitter = true
        };

        // Run multiple times to exercise randomness
        for (var i = 0; i < 100; i++)
        {
            var delay = policy.GetNextDelay(1, new InvalidOperationException("test"));
            // Base delay for attempt 1 = 10s, jitter range: 0.5x to 1.5x = 5s to 15s
            await Assert.That(delay).IsNotNull();
            await Assert.That(delay!.Value.TotalSeconds).IsGreaterThanOrEqualTo(5.0);
            await Assert.That(delay!.Value.TotalSeconds).IsLessThanOrEqualTo(15.0);
        }
    }

    [Test]
    public async Task ExponentialBackoff_WithJitter_NeverExceedsMaxDelay()
    {
        var policy = new ExponentialBackoffRetryPolicy
        {
            BaseDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(5),
            MaxAttempts = 10,
            Jitter = true
        };

        // At attempt 10, base delay = min(1*2^9=512, 5) = 5s
        // Jitter could produce up to 1.5x = 7.5s without the post-jitter clamp
        // Verify MaxDelay is a hard cap
        for (var i = 0; i < 200; i++)
        {
            var delay = policy.GetNextDelay(10, new InvalidOperationException("test"));
            await Assert.That(delay).IsNotNull();
            await Assert.That(delay!.Value).IsLessThanOrEqualTo(TimeSpan.FromSeconds(5));
            await Assert.That(delay!.Value.Ticks).IsGreaterThan(0);
        }
    }

    [Test]
    public async Task ExponentialBackoff_LargeAttempt_HandlesOverflow()
    {
        var policy = new ExponentialBackoffRetryPolicy
        {
            BaseDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(30),
            MaxAttempts = 100,
            Jitter = false
        };

        // Attempt 64+ would overflow long shift; should clamp to MaxDelay
        var delay = policy.GetNextDelay(64, new InvalidOperationException("test"));
        await Assert.That(delay).IsNotNull();
        await Assert.That(delay!.Value).IsEqualTo(TimeSpan.FromSeconds(30));
    }

    #endregion

    #region Interface Contract Tests

    [Test]
    public async Task AllPolicies_ImplementIRetryPolicy()
    {
        IRetryPolicy noRetry = NoRetryPolicy.Instance;
        IRetryPolicy fixedDelay = new FixedDelayRetryPolicy { Delay = TimeSpan.FromSeconds(1), MaxAttempts = 1 };
        IRetryPolicy exponential = new ExponentialBackoffRetryPolicy
        {
            BaseDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(30),
            MaxAttempts = 3
        };

        // All should be callable through the interface
        await Assert.That(noRetry.GetNextDelay(1, new InvalidOperationException("test"))).IsNull();
        await Assert.That(fixedDelay.GetNextDelay(1, new InvalidOperationException("test"))).IsNotNull();
        await Assert.That(exponential.GetNextDelay(1, new InvalidOperationException("test"))).IsNotNull();
    }

    #endregion
}
