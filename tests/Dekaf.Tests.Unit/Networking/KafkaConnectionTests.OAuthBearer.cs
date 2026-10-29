using System.Net;
using System.Reflection;
using Dekaf.Networking;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Networking;

public sealed partial class KafkaConnectionTests
{
    [Test]
    public async Task OAuthBearerAuthenticators_ReuseOwnedProviderAndCache_DisposeResourcesOnce()
    {
        await using var connection = new KafkaConnection("localhost", 9092, options: new ConnectionOptions
        {
            SaslMechanism = SaslMechanism.OAuthBearer,
            OAuthBearerConfig = CreateConnectionOAuthConfig()
        });
        var authenticator = CreateConnectionOAuthAuthenticator(connection);
        var provider = GetConnectionOwnedOAuthProvider(connection)!;
        var providerType = typeof(OAuthBearerTokenProvider);
        var httpClientField = providerType.GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic)!;
        // Keep the factory's ownership flag, but replace its transport to count requests/disposal.
        ((HttpClient)httpClientField.GetValue(provider)!).Dispose();
        var handler = new ConnectionOAuthTokenHandler();
        using var httpClient = new HttpClient(handler);
        httpClientField.SetValue(provider, httpClient);
        var refreshLock = (SemaphoreSlim)providerType
            .GetField("_refreshLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(provider)!;

        var token = await authenticator.GetTokenAsync();
        for (var i = 0; i < 3; i++)
        {
            authenticator.GetInitialResponse();
            authenticator.EvaluateChallenge([]);
            await Assert.That(authenticator.IsComplete).IsTrue();

            var next = CreateConnectionOAuthAuthenticator(connection);
            await Assert.That(next).IsNotSameReferenceAs(authenticator);
            await Assert.That(next.IsComplete).IsFalse();
            await Assert.That(GetConnectionOwnedOAuthProvider(connection)).IsSameReferenceAs(provider);
            await Assert.That(await next.GetTokenAsync()).IsSameReferenceAs(token);
            await Assert.That(handler.DisposeCount).IsEqualTo(0);
            authenticator = next;
        }
        await Assert.That(handler.RequestCount).IsEqualTo(1);

        await connection.DisposeAsync();
        await connection.DisposeAsync();
        await Assert.That(handler.DisposeCount).IsEqualTo(1);
        await Assert.That(() => refreshLock.Wait(0)).Throws<ObjectDisposedException>();
        await Assert.That(async () => await httpClient.GetAsync("http://localhost/token"))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OAuthBearerAuthenticators_PreserveCallerOwnershipAndPrecedence(bool useStaticToken)
    {
        var config = CreateConnectionOAuthConfig(refreshBufferSeconds: 7200);
        var handler = new ConnectionOAuthTokenHandler();
        using var httpClient = new HttpClient(handler);
        using var callerProvider = new OAuthBearerTokenProvider(config, httpClient);
        var staticToken = new OAuthBearerToken
        {
            TokenValue = "static-token",
            PrincipalName = "test-user",
            Expiration = DateTimeOffset.UtcNow.AddHours(1)
        };
        await using var connection = new KafkaConnection("localhost", 9092, options: new ConnectionOptions
        {
            SaslMechanism = SaslMechanism.OAuthBearer,
            OAuthBearerConfig = config,
            OAuthBearerTokenProvider = callerProvider.GetTokenAsync,
            OAuthBearerToken = useStaticToken ? staticToken : null
        });

        for (var i = 0; i < 3; i++)
        {
            var token = await CreateConnectionOAuthAuthenticator(connection).GetTokenAsync();
            await Assert.That(token.TokenValue).IsEqualTo(useStaticToken ? "static-token" : "endpoint-token");
            await Assert.That(GetConnectionOwnedOAuthProvider(connection)).IsNull();
        }
        await Assert.That(handler.RequestCount).IsEqualTo(useStaticToken ? 0 : 3);

        await connection.DisposeAsync();
        await Assert.That(handler.DisposeCount).IsEqualTo(0);
        // The refresh buffer forces a new request, proving the caller's semaphore is still usable.
        await callerProvider.GetTokenAsync();
        await Assert.That(handler.RequestCount).IsEqualTo(useStaticToken ? 1 : 4);
    }

    private static OAuthBearerConfig CreateConnectionOAuthConfig(int refreshBufferSeconds = 60) => new()
    {
        TokenEndpointUrl = "http://localhost/token",
        ClientId = "test-client",
        ClientSecret = "test-secret",
        TokenRefreshBufferSeconds = refreshBufferSeconds
    };

    private static OAuthBearerAuthenticator CreateConnectionOAuthAuthenticator(KafkaConnection connection) =>
        (OAuthBearerAuthenticator)typeof(KafkaConnection)
            .GetMethod("CreateOAuthBearerAuthenticator", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(connection, null)!;

    private static OAuthBearerTokenProvider? GetConnectionOwnedOAuthProvider(KafkaConnection connection) =>
        (OAuthBearerTokenProvider?)typeof(KafkaConnection)
            .GetField("_ownedTokenProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(connection);

    private sealed class ConnectionOAuthTokenHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public int DisposeCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"endpoint-token","expires_in":3600,"sub":"test-user"}""")
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeCount++;
            base.Dispose(disposing);
        }
    }
}
