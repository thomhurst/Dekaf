using System.Security.Cryptography;
using System.Text;
using Dekaf.Internal;

namespace Dekaf.Tests.Unit.Internal;

public class CompatibilityBclTests
{
    [Test]
    public async Task GuidBigEndian_RoundTripsNetworkOrder()
    {
        var guid = new Guid("00112233-4455-6677-8899-aabbccddeeff");
        var bytes = new byte[16];

        var written = CompatibilityBcl.TryWriteGuidBigEndian(guid, bytes);
        var parsed = CompatibilityBcl.ReadGuidBigEndian(bytes);

        await Assert.That(written).IsTrue();
        await Assert.That(Convert.ToHexString(bytes)).IsEqualTo("00112233445566778899AABBCCDDEEFF");
        await Assert.That(parsed).IsEqualTo(guid);
    }

    [Test]
    public async Task Pbkdf2_Sha256_MatchesKnownVector()
    {
        var result = CompatibilityBcl.Pbkdf2(
            Encoding.ASCII.GetBytes("password"),
            Encoding.ASCII.GetBytes("salt"),
            iterations: 1,
            HashAlgorithmName.SHA256,
            outputLength: 32);

        await Assert.That(Convert.ToHexString(result)).IsEqualTo(
            "120FB6CFFCF8B32C43E7225256C4F837A86548C92CCC35480805987CB70BE17B");
    }

    [Test]
    public async Task Base64UrlEncode_UsesUrlAlphabetAndOmitsPadding()
    {
        var encoded = CompatibilityBcl.Base64UrlEncode([0xfb, 0xff]);

        await Assert.That(encoded).IsEqualTo("-_8");
    }
}
