using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security.Sasl;

public class SaslMechanismTests
{
    [Test]
    public async Task ToProtocolName_Plain_ReturnsPLAIN()
    {
        await Assert.That(SaslMechanism.Plain.ToProtocolName()).IsEqualTo("PLAIN");
    }

    [Test]
    public async Task ToProtocolName_ScramSha256_ReturnsSCRAMSHA256()
    {
        await Assert.That(SaslMechanism.ScramSha256.ToProtocolName()).IsEqualTo("SCRAM-SHA-256");
    }

    [Test]
    public async Task ToProtocolName_ScramSha512_ReturnsSCRAMSHA512()
    {
        await Assert.That(SaslMechanism.ScramSha512.ToProtocolName()).IsEqualTo("SCRAM-SHA-512");
    }

    [Test]
    public async Task ToProtocolName_OAuthBearer_ReturnsOAUTHBEARER()
    {
        await Assert.That(SaslMechanism.OAuthBearer.ToProtocolName()).IsEqualTo("OAUTHBEARER");
    }

    [Test]
    public async Task ToProtocolName_None_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() => SaslMechanism.None.ToProtocolName())
            .Throws<ArgumentOutOfRangeException>();
    }
}
