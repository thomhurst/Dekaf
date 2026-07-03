using System.Text;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security.Sasl;

public sealed class PlainAuthenticatorTests
{
    [Test]
    public async Task GetInitialResponse_ReturnsPlainWireFormat()
    {
        var authenticator = new PlainAuthenticator("user", "pass", "authz");

        var response = authenticator.GetInitialResponse();

        await Assert.That(response).IsEquivalentTo(Encoding.UTF8.GetBytes("authz\0user\0pass"));
        await Assert.That(authenticator.IsComplete).IsTrue();
    }

    [Test]
    public async Task Clear_ZeroesInitialResponse()
    {
        var authenticator = new PlainAuthenticator("user", "pass");
        var response = authenticator.GetInitialResponse();

        SaslCredentialBuffers.Clear(response);

        await Assert.That(response).IsEquivalentTo(new byte[response.Length]);
    }
}
