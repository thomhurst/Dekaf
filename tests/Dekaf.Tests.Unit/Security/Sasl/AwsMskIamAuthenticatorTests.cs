using System.Text;
using System.Text.Json;
using Dekaf.Errors;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security.Sasl;

public sealed class AwsMskIamAuthenticatorTests
{
    private const string Host = "b-1.test.c2.kafka.us-east-1.amazonaws.com";

    [Test]
    public async Task CreatePayload_ProducesExpectedSigV4Fields()
    {
        var credentials = new AwsCredentials(
            "AKIDEXAMPLE",
            "wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY");
        var now = new DateTimeOffset(2015, 8, 30, 12, 36, 0, TimeSpan.Zero);

        var payload = AwsMskIamSignedPayload.Create(
            credentials,
            Host,
            "us-east-1",
            "dekaf-test",
            now,
            TimeSpan.FromMinutes(15));

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        await Assert.That(root.GetProperty("version").GetString()).IsEqualTo("2020_10_22");
        await Assert.That(root.GetProperty("host").GetString()).IsEqualTo(Host);
        await Assert.That(root.GetProperty("user-agent").GetString()).IsEqualTo("dekaf-test");
        await Assert.That(root.GetProperty("action").GetString()).IsEqualTo("kafka-cluster:Connect");
        await Assert.That(root.GetProperty("x-amz-algorithm").GetString()).IsEqualTo("AWS4-HMAC-SHA256");
        await Assert.That(root.GetProperty("x-amz-credential").GetString())
            .IsEqualTo("AKIDEXAMPLE/20150830/us-east-1/kafka-cluster/aws4_request");
        await Assert.That(root.GetProperty("x-amz-date").GetString()).IsEqualTo("20150830T123600Z");
        await Assert.That(root.GetProperty("x-amz-expires").GetString()).IsEqualTo("900");
        await Assert.That(root.GetProperty("x-amz-signedheaders").GetString()).IsEqualTo("host");
        await Assert.That(root.TryGetProperty("x-amz-security-token", out _)).IsFalse();
        await Assert.That(root.GetProperty("x-amz-signature").GetString())
            .IsEqualTo("166d06165f73bde1f7f584d772d7a23bf5548f63444ca4e970fbec0a17baeb15");
    }

    [Test]
    public async Task CreatePayload_IncludesSessionTokenWhenPresent()
    {
        var credentials = new AwsCredentials("access", "secret", "session-token");

        var payload = AwsMskIamSignedPayload.Create(
            credentials,
            Host,
            "us-east-1",
            "dekaf-test",
            DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
            TimeSpan.FromMinutes(15));

        using var document = JsonDocument.Parse(payload);

        await Assert.That(document.RootElement.GetProperty("x-amz-security-token").GetString())
            .IsEqualTo("session-token");
    }

    [Test]
    public async Task GetInitialResponse_UsesCredentialProviderAndCompletesAfterBrokerResponse()
    {
        var authenticator = new AwsMskIamAuthenticator(
            new AwsMskIamConfig
            {
                Region = "us-east-1",
                UserAgent = "dekaf-test",
                CredentialsProvider = new StaticAwsCredentialsProvider(new AwsCredentials("access", "secret"))
            },
            Host);

        await authenticator.GetCredentialsAsync();

        var response = Encoding.UTF8.GetString(authenticator.GetInitialResponse());

        await Assert.That(response).Contains("\"host\":\"" + Host + "\"");
        await Assert.That(authenticator.IsComplete).IsFalse();
        await Assert.That(authenticator.EvaluateChallenge("""{"version":"2020_10_22","request-id":"req"}"""u8.ToArray()))
            .IsNull();
        await Assert.That(authenticator.IsComplete).IsTrue();
    }

    [Test]
    public async Task GetInitialResponse_InvalidConfiguredRegion_Throws()
    {
        var authenticator = new AwsMskIamAuthenticator(
            new AwsMskIamConfig
            {
                Region = "evil.com/",
                CredentialsProvider = new StaticAwsCredentialsProvider(new AwsCredentials("access", "secret"))
            },
            Host);

        await authenticator.GetCredentialsAsync();

        var exception = await Assert.That(() => authenticator.GetInitialResponse())
            .Throws<InvalidOperationException>();
        await Assert.That(exception!.Message).Contains("region");
    }

    [Test]
    public async Task TryResolveFromHost_ReturnsMskRegion()
    {
        var region = AwsMskIamRegionResolver.TryResolveFromHost(Host);

        await Assert.That(region).IsEqualTo("us-east-1");
    }

    [Test]
    public async Task TryResolveFromHost_IgnoresClusterNameThatLooksLikeRegion()
    {
        var region = AwsMskIamRegionResolver.TryResolveFromHost(
            "b-1.my-cluster-2.abcdef.c2.kafka.us-east-1.amazonaws.com");

        await Assert.That(region).IsEqualTo("us-east-1");
    }

    [Test]
    public async Task TryResolveFromHost_ReturnsNullWhenHostIsNotMskShape()
    {
        var region = AwsMskIamRegionResolver.TryResolveFromHost("b-1.test.c2.us-east-1.amazonaws.com");

        await Assert.That(region).IsNull();
    }

    [Test]
    public async Task TryResolveFromHost_ReturnsNullWhenAmazonAwsIsNotSuffix()
    {
        var region = AwsMskIamRegionResolver.TryResolveFromHost(
            "b-1.test.c2.kafka.us-east-1.amazonaws.com.example.com");

        await Assert.That(region).IsNull();
    }

    [Test]
    public async Task EvaluateChallenge_EmptyResponse_ThrowsAuthenticationException()
    {
        var authenticator = new AwsMskIamAuthenticator(
            new AwsMskIamConfig
            {
                Region = "us-east-1",
                CredentialsProvider = new StaticAwsCredentialsProvider(new AwsCredentials("access", "secret"))
            },
            Host);

        await Assert.That(() => authenticator.EvaluateChallenge([]))
            .Throws<AuthenticationException>();
    }
}
