using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security.Sasl;

public sealed class SaslSessionStateTests
{
    private static SaslReauthenticationConfig DefaultConfig => new();

    [Test]
    public async Task SessionLifetimeMs_IsStoredCorrectly()
    {
        var state = new SaslSessionState(60_000, DefaultConfig);

        await Assert.That(state.SessionLifetimeMs).IsEqualTo(60_000L);
    }

    [Test]
    public async Task ZeroSessionLifetime_DoesNotRequireReauthentication()
    {
        var state = new SaslSessionState(0, DefaultConfig);

        await Assert.That(state.RequiresReauthentication).IsFalse();
        await Assert.That(state.ReauthenticationDelayMs).IsEqualTo(0L);
        await Assert.That(state.ReauthenticationDeadline).IsNull();
        await Assert.That(state.SessionExpiryTime).IsNull();
    }

    [Test]
    public async Task NegativeSessionLifetime_DoesNotRequireReauthentication()
    {
        var state = new SaslSessionState(-1, DefaultConfig);

        await Assert.That(state.RequiresReauthentication).IsFalse();
        await Assert.That(state.ReauthenticationDelayMs).IsEqualTo(0L);
    }

    [Test]
    public async Task ShortSessionLifetime_BelowMinimum_DoesNotRequireReauthentication()
    {
        // Default minimum is 10,000ms; session lifetime of 5,000ms should be skipped
        var state = new SaslSessionState(5_000, DefaultConfig);

        await Assert.That(state.RequiresReauthentication).IsFalse();
        await Assert.That(state.ReauthenticationDelayMs).IsEqualTo(0L);
    }

    [Test]
    public async Task SessionLifetime_AtMinimum_RequiresReauthentication()
    {
        // Default minimum is 10,000ms; session lifetime of exactly 10,000ms should trigger re-auth
        var state = new SaslSessionState(10_000, DefaultConfig);

        await Assert.That(state.RequiresReauthentication).IsTrue();
    }

    [Test]
    public async Task ReauthenticationDelay_CalculatedFromThreshold()
    {
        // With default threshold of 0.9, a 100,000ms session should re-auth at 90,000ms
        var state = new SaslSessionState(100_000, DefaultConfig);

        await Assert.That(state.RequiresReauthentication).IsTrue();
        await Assert.That(state.ReauthenticationDelayMs).IsEqualTo(90_000L);
    }

    [Test]
    public async Task ReauthenticationDelay_CustomThreshold()
    {
        var config = new SaslReauthenticationConfig
        {
            ReauthenticationThreshold = 0.5
        };
        var state = new SaslSessionState(100_000, config);

        await Assert.That(state.ReauthenticationDelayMs).IsEqualTo(50_000L);
    }

    [Test]
    public async Task ReauthenticationDelay_LowThreshold()
    {
        var config = new SaslReauthenticationConfig
        {
            ReauthenticationThreshold = 0.1
        };
        var state = new SaslSessionState(100_000, config);

        await Assert.That(state.ReauthenticationDelayMs).IsEqualTo(10_000L);
    }

    [Test]
    public async Task ReauthenticationDeadline_IsAuthTimePlusDelay()
    {
        var authTime = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var state = new SaslSessionState(100_000, DefaultConfig, authTime);

        // Delay = 100_000 * 0.9 = 90_000ms = 90 seconds
        var expectedDeadline = authTime.AddMilliseconds(90_000);

        await Assert.That(state.ReauthenticationDeadline).IsEqualTo(expectedDeadline);
    }

    [Test]
    public async Task SessionExpiryTime_IsAuthTimePlusLifetime()
    {
        var authTime = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var state = new SaslSessionState(60_000, DefaultConfig, authTime);

        var expectedExpiry = authTime.AddMilliseconds(60_000);

        await Assert.That(state.SessionExpiryTime).IsEqualTo(expectedExpiry);
    }

    [Test]
    public async Task ShouldReauthenticateNow_BeforeDeadline_ReturnsFalse()
    {
        var authTime = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var state = new SaslSessionState(100_000, DefaultConfig, authTime);

        // Check at 80 seconds after auth (deadline is at 90 seconds)
        var checkTime = authTime.AddSeconds(80);
        var result = state.ShouldReauthenticateNow(checkTime);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ShouldReauthenticateNow_AtDeadline_ReturnsTrue()
    {
        var authTime = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var state = new SaslSessionState(100_000, DefaultConfig, authTime);

        // Check exactly at deadline (90 seconds after auth)
        var checkTime = authTime.AddMilliseconds(90_000);
        var result = state.ShouldReauthenticateNow(checkTime);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ShouldReauthenticateNow_AfterDeadline_ReturnsTrue()
    {
        var authTime = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var state = new SaslSessionState(100_000, DefaultConfig, authTime);

        // Check 95 seconds after auth (deadline is at 90 seconds)
        var checkTime = authTime.AddSeconds(95);
        var result = state.ShouldReauthenticateNow(checkTime);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ShouldReauthenticateNow_NoReauthRequired_ReturnsFalse()
    {
        var state = new SaslSessionState(0, DefaultConfig);

        var result = state.ShouldReauthenticateNow();

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task AuthenticationTime_DefaultsToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var state = new SaslSessionState(60_000, DefaultConfig);
        var after = DateTimeOffset.UtcNow;

        await Assert.That(state.AuthenticationTime).IsGreaterThanOrEqualTo(before);
        await Assert.That(state.AuthenticationTime).IsLessThanOrEqualTo(after);
    }

    [Test]
    public async Task AuthenticationTime_CanBeExplicitlySet()
    {
        var authTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var state = new SaslSessionState(60_000, DefaultConfig, authTime);

        await Assert.That(state.AuthenticationTime).IsEqualTo(authTime);
    }

    [Test]
    public async Task Constructor_NullConfig_ThrowsArgumentNullException()
    {
        await Assert.That(() => new SaslSessionState(60_000, null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task CustomMinSessionLifetime_AllowsShorterSessions()
    {
        var config = new SaslReauthenticationConfig
        {
            MinSessionLifetimeMs = 1000 // 1 second minimum
        };

        // 5 seconds should now require re-auth (above 1 second minimum)
        var state = new SaslSessionState(5_000, config);

        await Assert.That(state.RequiresReauthentication).IsTrue();
    }

    [Test]
    public async Task CustomMinSessionLifetime_ZeroAllowsAllSessions()
    {
        var config = new SaslReauthenticationConfig
        {
            MinSessionLifetimeMs = 0
        };

        // Even 1ms session should require re-auth
        var state = new SaslSessionState(1, config);

        await Assert.That(state.RequiresReauthentication).IsTrue();
        await Assert.That(state.ReauthenticationDelayMs).IsGreaterThanOrEqualTo(1L);
    }

    [Test]
    public async Task LargeSessionLifetime_CalculatesCorrectly()
    {
        // 1 hour session = 3,600,000ms
        var state = new SaslSessionState(3_600_000, DefaultConfig);

        // Delay = 3,600,000 * 0.9 = 3,240,000ms = 54 minutes
        await Assert.That(state.RequiresReauthentication).IsTrue();
        await Assert.That(state.ReauthenticationDelayMs).IsEqualTo(3_240_000L);
    }

    [Test]
    public async Task VeryLargeSessionLifetime_24Hours_CalculatesCorrectly()
    {
        // 24 hour session = 86,400,000ms
        var authTime = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var state = new SaslSessionState(86_400_000, DefaultConfig, authTime);

        // Delay = 86,400,000 * 0.9 = 77,760,000ms
        await Assert.That(state.RequiresReauthentication).IsTrue();
        await Assert.That(state.ReauthenticationDelayMs).IsEqualTo(77_760_000L);

        var expectedDeadline = authTime.AddMilliseconds(77_760_000);
        await Assert.That(state.ReauthenticationDeadline).IsEqualTo(expectedDeadline);

        var expectedExpiry = authTime.AddMilliseconds(86_400_000);
        await Assert.That(state.SessionExpiryTime).IsEqualTo(expectedExpiry);
    }
}
