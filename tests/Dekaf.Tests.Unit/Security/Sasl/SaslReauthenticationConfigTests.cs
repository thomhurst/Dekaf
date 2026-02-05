using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security.Sasl;

public sealed class SaslReauthenticationConfigTests
{
    [Test]
    public async Task DefaultConfig_HasExpectedDefaults()
    {
        var config = new SaslReauthenticationConfig();

        await Assert.That(config.Enabled).IsTrue();
        await Assert.That(config.ReauthenticationThreshold).IsEqualTo(0.9);
        await Assert.That(config.MinSessionLifetimeMs).IsEqualTo(10_000L);
    }

    [Test]
    public async Task Validate_WithValidConfig_DoesNotThrow()
    {
        var config = new SaslReauthenticationConfig
        {
            Enabled = true,
            ReauthenticationThreshold = 0.85,
            MinSessionLifetimeMs = 5000
        };

        await Assert.That(() => config.Validate()).ThrowsNothing();
    }

    [Test]
    public async Task Validate_WithThresholdAtZero_ThrowsArgumentOutOfRangeException()
    {
        var config = new SaslReauthenticationConfig
        {
            ReauthenticationThreshold = 0.0
        };

        await Assert.That(() => config.Validate())
            .Throws<ArgumentOutOfRangeException>()
            .WithMessageContaining("ReauthenticationThreshold");
    }

    [Test]
    public async Task Validate_WithThresholdAtOne_ThrowsArgumentOutOfRangeException()
    {
        var config = new SaslReauthenticationConfig
        {
            ReauthenticationThreshold = 1.0
        };

        await Assert.That(() => config.Validate())
            .Throws<ArgumentOutOfRangeException>()
            .WithMessageContaining("ReauthenticationThreshold");
    }

    [Test]
    public async Task Validate_WithNegativeThreshold_ThrowsArgumentOutOfRangeException()
    {
        var config = new SaslReauthenticationConfig
        {
            ReauthenticationThreshold = -0.5
        };

        await Assert.That(() => config.Validate())
            .Throws<ArgumentOutOfRangeException>()
            .WithMessageContaining("ReauthenticationThreshold");
    }

    [Test]
    public async Task Validate_WithNegativeMinSessionLifetime_ThrowsArgumentOutOfRangeException()
    {
        var config = new SaslReauthenticationConfig
        {
            MinSessionLifetimeMs = -1
        };

        await Assert.That(() => config.Validate())
            .Throws<ArgumentOutOfRangeException>()
            .WithMessageContaining("MinSessionLifetimeMs");
    }

    [Test]
    public async Task Validate_WithZeroMinSessionLifetime_DoesNotThrow()
    {
        var config = new SaslReauthenticationConfig
        {
            MinSessionLifetimeMs = 0
        };

        await Assert.That(() => config.Validate()).ThrowsNothing();
    }

    [Test]
    public async Task Config_CanBeDisabled()
    {
        var config = new SaslReauthenticationConfig
        {
            Enabled = false
        };

        await Assert.That(config.Enabled).IsFalse();
    }

    [Test]
    public async Task Config_CustomThreshold_IsPreserved()
    {
        var config = new SaslReauthenticationConfig
        {
            ReauthenticationThreshold = 0.75
        };

        await Assert.That(config.ReauthenticationThreshold).IsEqualTo(0.75);
    }
}
