using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security.Sasl;

public class OAuthBearerTokenTests
{
    [Test]
    public async Task IsExpired_WithFutureExpiration_ReturnsFalse()
    {
        var token = new OAuthBearerToken
        {
            TokenValue = "test-token",
            Expiration = DateTimeOffset.UtcNow.AddHours(1),
            PrincipalName = "test-user"
        };

        await Assert.That(token.IsExpired()).IsFalse();
    }

    [Test]
    public async Task IsExpired_WithPastExpiration_ReturnsTrue()
    {
        var token = new OAuthBearerToken
        {
            TokenValue = "test-token",
            Expiration = DateTimeOffset.UtcNow.AddMinutes(-5),
            PrincipalName = "test-user"
        };

        await Assert.That(token.IsExpired()).IsTrue();
    }

    [Test]
    public async Task IsExpired_WithBuffer_ConsidersBuffer()
    {
        // Token expires in 30 seconds
        var token = new OAuthBearerToken
        {
            TokenValue = "test-token",
            Expiration = DateTimeOffset.UtcNow.AddSeconds(30),
            PrincipalName = "test-user"
        };

        // Without buffer, not expired
        await Assert.That(token.IsExpired(bufferSeconds: 0)).IsFalse();

        // With 60 second buffer, considered expired
        await Assert.That(token.IsExpired(bufferSeconds: 60)).IsTrue();
    }

    [Test]
    public async Task Extensions_CanBeNullOrPopulated()
    {
        var tokenWithoutExtensions = new OAuthBearerToken
        {
            TokenValue = "test-token",
            Expiration = DateTimeOffset.UtcNow.AddHours(1),
            PrincipalName = "test-user"
        };

        var tokenWithExtensions = new OAuthBearerToken
        {
            TokenValue = "test-token",
            Expiration = DateTimeOffset.UtcNow.AddHours(1),
            PrincipalName = "test-user",
            Extensions = new Dictionary<string, string> { ["key"] = "value" }
        };

        await Assert.That(tokenWithoutExtensions.Extensions).IsNull();
        await Assert.That(tokenWithExtensions.Extensions).IsNotNull();
        await Assert.That(tokenWithExtensions.Extensions!.Count).IsEqualTo(1);
    }
}
