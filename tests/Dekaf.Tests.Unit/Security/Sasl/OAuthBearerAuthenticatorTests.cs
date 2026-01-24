using System.Text;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security.Sasl;

public class OAuthBearerAuthenticatorTests
{
    [Test]
    public async Task MechanismName_ReturnsOAuthBearer()
    {
        var token = CreateValidToken();
        var authenticator = new OAuthBearerAuthenticator(token);

        await Assert.That(authenticator.MechanismName).IsEqualTo("OAUTHBEARER");
    }

    [Test]
    public async Task GetInitialResponse_WithValidToken_ReturnsCorrectFormat()
    {
        var token = new OAuthBearerToken
        {
            TokenValue = "test-jwt-token",
            Expiration = DateTimeOffset.UtcNow.AddHours(1),
            PrincipalName = "test-user"
        };
        var authenticator = new OAuthBearerAuthenticator(token);

        var response = authenticator.GetInitialResponse();
        var responseStr = Encoding.UTF8.GetString(response);

        // Format should be: n,,\x01auth=Bearer <token>\x01
        // GS2 header
        await Assert.That(responseStr.StartsWith("n,,", StringComparison.Ordinal)).IsTrue();
        // SOH separator after GS2 header
        await Assert.That(responseStr[3]).IsEqualTo('\x01');
        // Authorization header
        await Assert.That(responseStr).Contains("auth=Bearer test-jwt-token");
        // Single SOH terminator (RFC 7628)
        await Assert.That(responseStr.EndsWith('\x01')).IsTrue();
        // Verify it's not double SOH
        await Assert.That(responseStr.EndsWith("\x01\x01", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task GetInitialResponse_WithExtensions_IncludesExtensions()
    {
        var token = new OAuthBearerToken
        {
            TokenValue = "test-jwt-token",
            Expiration = DateTimeOffset.UtcNow.AddHours(1),
            PrincipalName = "test-user",
            Extensions = new Dictionary<string, string>
            {
                ["ext1"] = "value1",
                ["ext2"] = "value2"
            }
        };
        var authenticator = new OAuthBearerAuthenticator(token);

        var response = authenticator.GetInitialResponse();
        var responseStr = Encoding.UTF8.GetString(response);

        await Assert.That(responseStr).Contains("ext1=value1");
        await Assert.That(responseStr).Contains("ext2=value2");
    }

    [Test]
    public async Task GetInitialResponse_SetsIsCompleteToTrue()
    {
        var token = CreateValidToken();
        var authenticator = new OAuthBearerAuthenticator(token);

        await Assert.That(authenticator.IsComplete).IsFalse();

        _ = authenticator.GetInitialResponse();

        await Assert.That(authenticator.IsComplete).IsTrue();
    }

    [Test]
    public async Task GetInitialResponse_WithExpiredToken_ThrowsAuthenticationException()
    {
        var token = new OAuthBearerToken
        {
            TokenValue = "expired-token",
            Expiration = DateTimeOffset.UtcNow.AddMinutes(-5),
            PrincipalName = "test-user"
        };
        var authenticator = new OAuthBearerAuthenticator(token);

        await Assert.That(() => authenticator.GetInitialResponse())
            .Throws<AuthenticationException>()
            .WithMessageContaining("expired");
    }

    [Test]
    public async Task EvaluateChallenge_WithEmptyChallenge_ReturnsNullAndCompletesAuthentication()
    {
        var token = CreateValidToken();
        var authenticator = new OAuthBearerAuthenticator(token);
        _ = authenticator.GetInitialResponse();

        var result = authenticator.EvaluateChallenge([]);

        await Assert.That(result).IsNull();
        await Assert.That(authenticator.IsComplete).IsTrue();
    }

    [Test]
    public async Task EvaluateChallenge_WithJsonError_ThrowsAuthenticationException()
    {
        var token = CreateValidToken();
        var authenticator = new OAuthBearerAuthenticator(token);
        _ = authenticator.GetInitialResponse();

        var errorChallenge = Encoding.UTF8.GetBytes("{\"status\":\"invalid_token\",\"scope\":\"required_scope\"}");

        await Assert.That(() => authenticator.EvaluateChallenge(errorChallenge))
            .Throws<AuthenticationException>()
            .WithMessageContaining("invalid_token");
    }

    [Test]
    public async Task Constructor_WithNullToken_ThrowsArgumentNullException()
    {
        await Assert.That(() => new OAuthBearerAuthenticator((OAuthBearerToken)null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullTokenProvider_ThrowsArgumentNullException()
    {
        await Assert.That(() => new OAuthBearerAuthenticator((Func<CancellationToken, ValueTask<OAuthBearerToken>>)null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task GetTokenAsync_WithStaticToken_ReturnsSameToken()
    {
        var token = CreateValidToken();
        var authenticator = new OAuthBearerAuthenticator(token);

        var result = await authenticator.GetTokenAsync();

        await Assert.That(ReferenceEquals(result, token)).IsTrue();
    }

    [Test]
    public async Task GetTokenAsync_WithTokenProvider_InvokesProvider()
    {
        var token = CreateValidToken();
        var providerCalled = false;
        var authenticator = new OAuthBearerAuthenticator(ct =>
        {
            providerCalled = true;
            return new ValueTask<OAuthBearerToken>(token);
        });

        var result = await authenticator.GetTokenAsync();

        await Assert.That(providerCalled).IsTrue();
        await Assert.That(ReferenceEquals(result, token)).IsTrue();
    }

    [Test]
    public async Task GetInitialResponse_WithTokenProvider_WithoutCallingGetTokenAsync_ThrowsInvalidOperationException()
    {
        var token = CreateValidToken();
        var authenticator = new OAuthBearerAuthenticator(_ => new ValueTask<OAuthBearerToken>(token));

        await Assert.That(() => authenticator.GetInitialResponse())
            .Throws<InvalidOperationException>()
            .WithMessageContaining("GetTokenAsync");
    }

    private static OAuthBearerToken CreateValidToken() => new()
    {
        TokenValue = "valid-test-token",
        Expiration = DateTimeOffset.UtcNow.AddHours(1),
        PrincipalName = "test-user"
    };
}
