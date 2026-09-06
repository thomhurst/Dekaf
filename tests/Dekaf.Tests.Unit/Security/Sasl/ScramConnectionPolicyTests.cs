using System.Reflection;
using System.Text;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security.Sasl;

public sealed class ScramConnectionPolicyTests
{
    [Test]
    [Arguments(SaslMechanism.ScramSha256, false)]
    [Arguments(SaslMechanism.ScramSha256, true)]
    [Arguments(SaslMechanism.ScramSha512, false)]
    [Arguments(SaslMechanism.ScramSha512, true)]
    public async Task ConnectionFactory_RepeatedAttemptsHonorIterationLimit(
        SaslMechanism mechanism, bool useCredentialProvider)
    {
        await using var connection = new KafkaConnection("localhost", 9092, options: new ConnectionOptions
        {
            SaslMechanism = mechanism,
            SaslUsername = "user",
            SaslPassword = "password",
            SaslScramMaxIterations = 1,
            SaslCredentialProvider = useCredentialProvider
                ? static _ => new ValueTask<SaslCredentials>(new SaslCredentials("user", "password"))
                : null
        });
        // Initial authentication and reauthentication use this same factory. Binding it
        // avoids a network fixture and checks both fixed and provider-based credentials.
        var create = typeof(KafkaConnection)
            .GetMethod("CreateSaslAuthenticatorAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<CancellationToken, ValueTask<ISaslAuthenticator>>>(connection);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var rejected = await create(default);
            var excessiveChallenge = CreateChallenge(rejected, 2);
            await Assert.That(() => rejected.EvaluateChallenge(excessiveChallenge))
                .Throws<AuthenticationException>();

            var accepted = await create(default);
            var validChallenge = CreateChallenge(accepted, 1);
            await Assert.That(accepted.EvaluateChallenge(validChallenge)).IsNotNull();
        }
    }

    [Test]
    public async Task SharedOAuthProvider_ClonedOptionsPreserveScramIterationLimit()
    {
        var options = new ConnectionOptions
        {
            SaslMechanism = SaslMechanism.OAuthBearer,
            SaslScramMaxIterations = 8192,
            OAuthBearerConfig = new OAuthBearerConfig
            {
                TokenEndpointUrl = "http://localhost/token",
                ClientId = "test-client"
            }
        };
        var clone = typeof(ConnectionPool)
            .GetMethod("ConfigureSharedOAuthBearerProvider", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<CloneOptions>();

        var copied = clone(options, out var provider);
        using (provider)
        {
            await Assert.That(ReferenceEquals(copied, options)).IsFalse();
            await Assert.That(copied.SaslScramMaxIterations).IsEqualTo(8192);
            await Assert.That(copied.OAuthBearerTokenProvider is not null).IsTrue();
        }
    }

    private static byte[] CreateChallenge(ISaslAuthenticator authenticator, int iterations)
    {
        var initial = Encoding.UTF8.GetString(authenticator.GetInitialResponse());
        var nonce = initial[(initial.IndexOf(",r=", StringComparison.Ordinal) + 3)..];
        return Encoding.UTF8.GetBytes($"r={nonce}server,s=c2FsdA==,i={iterations}");
    }

    private delegate ConnectionOptions CloneOptions(
        ConnectionOptions options, out OAuthBearerTokenProvider? provider);
}
