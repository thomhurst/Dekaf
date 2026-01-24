using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security;

public class GssapiAuthenticatorTests
{
    [Test]
    public async Task MechanismName_ReturnsGssapi()
    {
        var config = new GssapiConfig();
        using var authenticator = new GssapiAuthenticator(config, "broker.example.com");

        await Assert.That(authenticator.MechanismName).IsEqualTo("GSSAPI");
    }

    [Test]
    public async Task IsComplete_InitiallyFalse()
    {
        var config = new GssapiConfig();
        using var authenticator = new GssapiAuthenticator(config, "broker.example.com");

        await Assert.That(authenticator.IsComplete).IsFalse();
    }

    [Test]
    public async Task Constructor_ThrowsOnNullConfig()
    {
        var exception = await Assert.That(() => new GssapiAuthenticator(null!, "broker.example.com"))
            .Throws<ArgumentNullException>();

        await Assert.That(exception!.ParamName).IsEqualTo("config");
    }

    [Test]
    public async Task Constructor_ThrowsOnNullTargetHost()
    {
        var config = new GssapiConfig();

        var exception = await Assert.That(() => new GssapiAuthenticator(config, null!))
            .Throws<ArgumentNullException>();

        await Assert.That(exception!.ParamName).IsEqualTo("targetHost");
    }

    [Test]
    public async Task GetInitialResponse_ThrowsWhenCalledTwice()
    {
        var config = new GssapiConfig();
        using var authenticator = new GssapiAuthenticator(config, "broker.example.com");

        // First call may succeed or fail depending on Kerberos availability,
        // but we need to handle both cases
        try
        {
            authenticator.GetInitialResponse();
        }
        catch (AuthenticationException)
        {
            // Expected on systems without Kerberos configured
            // In this case, the state has still changed, so second call should throw InvalidOperationException
        }

        // Second call should always throw InvalidOperationException
        var exception = await Assert.That(() => authenticator.GetInitialResponse())
            .Throws<InvalidOperationException>();

        await Assert.That(exception!.Message).Contains("GetInitialResponse can only be called once");
    }

    [Test]
    public async Task EvaluateChallenge_ThrowsWhenCalledBeforeGetInitialResponse()
    {
        var config = new GssapiConfig();
        using var authenticator = new GssapiAuthenticator(config, "broker.example.com");

        var exception = await Assert.That(() => authenticator.EvaluateChallenge([]))
            .Throws<InvalidOperationException>();

        await Assert.That(exception!.Message).Contains("GetInitialResponse must be called before EvaluateChallenge");
    }

    [Test]
    public Task Dispose_CanBeCalledMultipleTimes()
    {
        var config = new GssapiConfig();
        var authenticator = new GssapiAuthenticator(config, "broker.example.com");

        // Dispose should not throw when called multiple times
        authenticator.Dispose();
        authenticator.Dispose();
        authenticator.Dispose();

        // Test passes if no exception thrown
        return Task.CompletedTask;
    }

    [Test]
    public Task Dispose_CanBeCalledAfterGetInitialResponse()
    {
        var config = new GssapiConfig();
        var authenticator = new GssapiAuthenticator(config, "broker.example.com");

        try
        {
            authenticator.GetInitialResponse();
        }
        catch (AuthenticationException)
        {
            // Expected on systems without Kerberos configured
        }

        // Dispose should not throw even after authentication attempt
        authenticator.Dispose();

        // Test passes if no exception thrown
        return Task.CompletedTask;
    }
}

public class GssapiConfigTests
{
    [Test]
    public async Task ServiceName_DefaultsToKafka()
    {
        var config = new GssapiConfig();

        await Assert.That(config.ServiceName).IsEqualTo("kafka");
    }

    [Test]
    public async Task ServiceName_CanBeCustomized()
    {
        var config = new GssapiConfig { ServiceName = "custom-service" };

        await Assert.That(config.ServiceName).IsEqualTo("custom-service");
    }

    [Test]
    public async Task Principal_DefaultsToNull()
    {
        var config = new GssapiConfig();

        await Assert.That(config.Principal).IsNull();
    }

    [Test]
    public async Task Principal_CanBeSet()
    {
        var config = new GssapiConfig { Principal = "user@REALM.COM" };

        await Assert.That(config.Principal).IsEqualTo("user@REALM.COM");
    }

    [Test]
    public async Task KeytabPath_DefaultsToNull()
    {
        var config = new GssapiConfig();

        await Assert.That(config.KeytabPath).IsNull();
    }

    [Test]
    public async Task KeytabPath_CanBeSet()
    {
        var config = new GssapiConfig { KeytabPath = "/etc/security/keytabs/kafka.keytab" };

        await Assert.That(config.KeytabPath).IsEqualTo("/etc/security/keytabs/kafka.keytab");
    }

    [Test]
    public async Task Realm_DefaultsToNull()
    {
        var config = new GssapiConfig();

        await Assert.That(config.Realm).IsNull();
    }

    [Test]
    public async Task Realm_CanBeSet()
    {
        var config = new GssapiConfig { Realm = "EXAMPLE.COM" };

        await Assert.That(config.Realm).IsEqualTo("EXAMPLE.COM");
    }
}
