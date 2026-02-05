using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security.Sasl;

/// <summary>
/// Tests for SASL re-authentication scheduling logic (KIP-368).
/// Verifies that re-authentication timing is computed correctly for various scenarios.
/// </summary>
public sealed class SaslReauthenticationSchedulingTests
{
    [Test]
    public async Task OAuthBearerScenario_OneHourToken_ReauthsAt54Minutes()
    {
        // Typical OAUTHBEARER scenario: token expires in 1 hour
        var config = new SaslReauthenticationConfig();
        var authTime = DateTimeOffset.UtcNow;

        var state = new SaslSessionState(3_600_000, config, authTime);

        // Should re-auth at 90% of 1 hour = 54 minutes = 3,240,000ms
        await Assert.That(state.RequiresReauthentication).IsTrue();
        await Assert.That(state.ReauthenticationDelayMs).IsEqualTo(3_240_000L);

        // Not time yet at 50 minutes
        var at50Minutes = authTime.AddMinutes(50);
        await Assert.That(state.ShouldReauthenticateNow(at50Minutes)).IsFalse();

        // Time to re-auth at 55 minutes (past 54 min deadline)
        var at55Minutes = authTime.AddMinutes(55);
        await Assert.That(state.ShouldReauthenticateNow(at55Minutes)).IsTrue();
    }

    [Test]
    public async Task ScramScenario_LongSession_ReauthsCorrectly()
    {
        // SCRAM with 24-hour session
        var config = new SaslReauthenticationConfig
        {
            ReauthenticationThreshold = 0.85
        };
        var authTime = DateTimeOffset.UtcNow;

        var state = new SaslSessionState(86_400_000, config, authTime);

        // Should re-auth at 85% of 24 hours = 20.4 hours = 73,440,000ms
        await Assert.That(state.RequiresReauthentication).IsTrue();
        await Assert.That(state.ReauthenticationDelayMs).IsEqualTo(73_440_000L);
    }

    [Test]
    public async Task PlainScenario_NoSessionLifetime_NoReauth()
    {
        // PLAIN auth with no session lifetime reported by broker
        var config = new SaslReauthenticationConfig();
        var state = new SaslSessionState(0, config);

        await Assert.That(state.RequiresReauthentication).IsFalse();
    }

    [Test]
    public async Task DisabledConfig_StillCreatesState_ButNoScheduling()
    {
        // When disabled, the state tracks the session but no timer is created
        // (timer creation happens in KafkaConnection, but state still reports correctly)
        var config = new SaslReauthenticationConfig { Enabled = false };
        var state = new SaslSessionState(60_000, config);

        // State itself still reports correctly - the Enabled flag is checked by the caller
        await Assert.That(state.RequiresReauthentication).IsTrue();
        await Assert.That(state.ReauthenticationDelayMs).IsEqualTo(54_000L);
    }

    [Test]
    public async Task ConsecutiveReauths_EachGetsNewState()
    {
        // Simulates consecutive re-authentications
        var config = new SaslReauthenticationConfig();

        // First auth: session lifetime 60 seconds
        var firstAuthTime = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var firstState = new SaslSessionState(60_000, config, firstAuthTime);

        await Assert.That(firstState.ReauthenticationDelayMs).IsEqualTo(54_000L);

        // Re-auth happens at 54 seconds. Broker reports new 60-second session
        var reauthTime = firstAuthTime.AddMilliseconds(54_000);
        var secondState = new SaslSessionState(60_000, config, reauthTime);

        // Second state should compute delay from its own auth time
        await Assert.That(secondState.AuthenticationTime).IsEqualTo(reauthTime);
        await Assert.That(secondState.ReauthenticationDelayMs).IsEqualTo(54_000L);

        // The new deadline should be 54 seconds after the re-auth time
        var expectedDeadline = reauthTime.AddMilliseconds(54_000);
        await Assert.That(secondState.ReauthenticationDeadline).IsEqualTo(expectedDeadline);
    }

    [Test]
    public async Task SessionLifetime_JustAboveMinimum_RequiresReauth()
    {
        var config = new SaslReauthenticationConfig
        {
            MinSessionLifetimeMs = 10_000
        };

        // 10,001ms is just above the minimum
        var state = new SaslSessionState(10_001, config);

        await Assert.That(state.RequiresReauthentication).IsTrue();
    }

    [Test]
    public async Task SessionLifetime_JustBelowMinimum_DoesNotRequireReauth()
    {
        var config = new SaslReauthenticationConfig
        {
            MinSessionLifetimeMs = 10_000
        };

        // 9,999ms is just below the minimum
        var state = new SaslSessionState(9_999, config);

        await Assert.That(state.RequiresReauthentication).IsFalse();
    }

    [Test]
    public async Task AggressiveThreshold_ReauthsEarly()
    {
        var config = new SaslReauthenticationConfig
        {
            ReauthenticationThreshold = 0.5
        };
        var authTime = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

        var state = new SaslSessionState(120_000, config, authTime);

        // Re-auth at 50% of 120s = 60s
        await Assert.That(state.ReauthenticationDelayMs).IsEqualTo(60_000L);

        // Should re-auth at 60 seconds
        await Assert.That(state.ShouldReauthenticateNow(authTime.AddSeconds(59))).IsFalse();
        await Assert.That(state.ShouldReauthenticateNow(authTime.AddSeconds(60))).IsTrue();
    }

    [Test]
    public async Task ConservativeThreshold_ReauthsLate()
    {
        var config = new SaslReauthenticationConfig
        {
            ReauthenticationThreshold = 0.95
        };
        var authTime = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

        var state = new SaslSessionState(100_000, config, authTime);

        // Re-auth at 95% of 100s = 95s
        await Assert.That(state.ReauthenticationDelayMs).IsEqualTo(95_000L);

        // Should not re-auth at 94 seconds
        await Assert.That(state.ShouldReauthenticateNow(authTime.AddSeconds(94))).IsFalse();
        // Should re-auth at 95 seconds
        await Assert.That(state.ShouldReauthenticateNow(authTime.AddSeconds(95))).IsTrue();
    }
}
