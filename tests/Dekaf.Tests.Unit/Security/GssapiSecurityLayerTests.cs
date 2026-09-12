using Dekaf.Errors;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security;

public class GssapiSecurityLayerTests
{
    [Test]
    [Arguments(new byte[] { 1, 0, 0, 0 })]
    [Arguments(new byte[] { 1, 1, 0, 0 })]
    [Arguments(new byte[] { 3, 0, 16, 0 })]
    [Arguments(new byte[] { 7, 255, 255, 255 })]
    public void AuthenticationOnlyOffered_AcceptsOffer(byte[] offer)
        => GssapiAuthenticator.ValidateSecurityLayerOffer(offer);

    [Test]
    [Arguments(new byte[] { })]
    [Arguments(new byte[] { 1, 0, 0 })]
    [Arguments(new byte[] { 1, 0, 0, 0, 0 })]
    [Arguments(new byte[] { 0, 0, 0, 0 })]
    [Arguments(new byte[] { 2, 0, 16, 0 })]
    [Arguments(new byte[] { 4, 0, 16, 0 })]
    public async Task MalformedOrUnsupportedOffer_RejectsAuthentication(byte[] offer)
        => await Assert.That(() => GssapiAuthenticator.ValidateSecurityLayerOffer(offer)).Throws<AuthenticationException>();
}
