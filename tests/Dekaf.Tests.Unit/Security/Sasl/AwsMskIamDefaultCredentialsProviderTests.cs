using System.Net;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security.Sasl;

[NotInParallel("AwsEnvironment")]
public sealed class AwsMskIamDefaultCredentialsProviderTests
{
    [Test]
    public async Task GetCredentialsAsync_UsesEnvironmentCredentials()
    {
        var snapshot = EnvironmentSnapshot.Capture(
            "AWS_ACCESS_KEY_ID",
            "AWS_SECRET_ACCESS_KEY",
            "AWS_SESSION_TOKEN");

        try
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "env-access");
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "env-secret");
            Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", "env-token");

            var provider = new AwsMskIamDefaultCredentialsProvider();

            var credentials = await provider.GetCredentialsAsync();

            await Assert.That(credentials.AccessKeyId).IsEqualTo("env-access");
            await Assert.That(credentials.SecretAccessKey).IsEqualTo("env-secret");
            await Assert.That(credentials.SessionToken).IsEqualTo("env-token");
        }
        finally
        {
            snapshot.Restore();
        }
    }

    [Test]
    public async Task GetCredentialsAsync_UsesConfiguredSharedCredentialsProfile()
    {
        var snapshot = EnvironmentSnapshot.Capture(
            "AWS_ACCESS_KEY_ID",
            "AWS_SECRET_ACCESS_KEY",
            "AWS_SESSION_TOKEN",
            "AWS_SHARED_CREDENTIALS_FILE",
            "AWS_PROFILE",
            "AWS_ROLE_ARN",
            "AWS_WEB_IDENTITY_TOKEN_FILE",
            "AWS_CONTAINER_CREDENTIALS_FULL_URI",
            "AWS_CONTAINER_CREDENTIALS_RELATIVE_URI",
            "AWS_EC2_METADATA_DISABLED");
        var credentialsFile = Path.GetTempFileName();

        try
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", null);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", null);
            Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", null);
            Environment.SetEnvironmentVariable("AWS_ROLE_ARN", null);
            Environment.SetEnvironmentVariable("AWS_WEB_IDENTITY_TOKEN_FILE", null);
            Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_FULL_URI", null);
            Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", null);
            Environment.SetEnvironmentVariable("AWS_EC2_METADATA_DISABLED", "true");
            Environment.SetEnvironmentVariable("AWS_SHARED_CREDENTIALS_FILE", credentialsFile);
            Environment.SetEnvironmentVariable("AWS_PROFILE", "msk-prod");

            await File.WriteAllTextAsync(
                credentialsFile,
                """
                [default]
                aws_access_key_id = default-access
                aws_secret_access_key = default-secret

                [msk-prod]
                aws_access_key_id = profile-access
                aws_secret_access_key = profile-secret
                aws_session_token = profile-token
                """);

            var provider = new AwsMskIamDefaultCredentialsProvider();

            var credentials = await provider.GetCredentialsAsync();

            await Assert.That(credentials.AccessKeyId).IsEqualTo("profile-access");
            await Assert.That(credentials.SecretAccessKey).IsEqualTo("profile-secret");
            await Assert.That(credentials.SessionToken).IsEqualTo("profile-token");
        }
        finally
        {
            File.Delete(credentialsFile);
            snapshot.Restore();
        }
    }

    [Test]
    public async Task IsTrustedContainerCredentialsFullUri_RejectsHttpNonMetadataHost()
    {
        var trusted = AwsMskIamDefaultCredentialsProvider.IsTrustedContainerCredentialsFullUri(
            new Uri("http://example.com/credentials"));

        await Assert.That(trusted).IsFalse();
    }

    [Test]
    public async Task IsTrustedContainerCredentialsFullUri_AllowsHttpsNonMetadataHost()
    {
        var trusted = AwsMskIamDefaultCredentialsProvider.IsTrustedContainerCredentialsFullUri(
            new Uri("https://example.com/credentials"));

        await Assert.That(trusted).IsTrue();
    }

    [Arguments("http://169.254.170.2/credentials")]
    [Arguments("http://169.254.170.23/credentials")]
    [Arguments("http://[fd00:ec2::23]/credentials")]
    [Test]
    public async Task IsTrustedContainerCredentialsFullUri_AllowsTrustedMetadataHosts(string uri)
    {
        var trusted = AwsMskIamDefaultCredentialsProvider.IsTrustedContainerCredentialsFullUri(new Uri(uri));

        await Assert.That(trusted).IsTrue();
    }

    [Test]
    public async Task BuildStsEndpoint_InvalidRegion_Throws()
    {
        var exception = await Assert.That(() => AwsMskIamDefaultCredentialsProvider.BuildStsEndpoint("evil.com/"))
            .Throws<InvalidOperationException>();

        await Assert.That(exception!.Message).Contains("region");
    }

    [Test]
    public async Task BuildStsEndpoint_ChinaRegion_UsesChinaSuffix()
    {
        var endpoint = AwsMskIamDefaultCredentialsProvider.BuildStsEndpoint("cn-north-1");

        await Assert.That(endpoint).IsEqualTo(new Uri("https://sts.cn-north-1.amazonaws.com.cn/"));
    }

    [Test]
    public async Task GetCredentialsAsync_WebIdentityHttpFailureFallsBackToProfile()
    {
        var snapshot = EnvironmentSnapshot.Capture(
            "AWS_ACCESS_KEY_ID",
            "AWS_SECRET_ACCESS_KEY",
            "AWS_SESSION_TOKEN",
            "AWS_ROLE_ARN",
            "AWS_ROLE_SESSION_NAME",
            "AWS_WEB_IDENTITY_TOKEN_FILE",
            "AWS_REGION",
            "AWS_DEFAULT_REGION",
            "AWS_SHARED_CREDENTIALS_FILE",
            "AWS_CONFIG_FILE",
            "AWS_PROFILE",
            "AWS_CONTAINER_CREDENTIALS_FULL_URI",
            "AWS_CONTAINER_CREDENTIALS_RELATIVE_URI",
            "AWS_EC2_METADATA_DISABLED");
        var tokenFile = Path.GetTempFileName();
        var credentialsFile = Path.GetTempFileName();

        try
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", null);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", null);
            Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", null);
            Environment.SetEnvironmentVariable("AWS_ROLE_ARN", "arn:aws:iam::123456789012:role/msk-client");
            Environment.SetEnvironmentVariable("AWS_ROLE_SESSION_NAME", "dekaf-test");
            Environment.SetEnvironmentVariable("AWS_WEB_IDENTITY_TOKEN_FILE", tokenFile);
            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
            Environment.SetEnvironmentVariable("AWS_DEFAULT_REGION", null);
            Environment.SetEnvironmentVariable("AWS_SHARED_CREDENTIALS_FILE", credentialsFile);
            Environment.SetEnvironmentVariable("AWS_CONFIG_FILE", null);
            Environment.SetEnvironmentVariable("AWS_PROFILE", "fallback");
            Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_FULL_URI", null);
            Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", null);
            Environment.SetEnvironmentVariable("AWS_EC2_METADATA_DISABLED", "true");

            await File.WriteAllTextAsync(tokenFile, "web-token");
            await File.WriteAllTextAsync(
                credentialsFile,
                """
                [fallback]
                aws_access_key_id = profile-access
                aws_secret_access_key = profile-secret
                aws_session_token = profile-token
                """);

            var handler = new CapturingHttpMessageHandler(_ => throw new HttpRequestException("STS unavailable"));
            using var httpClient = new HttpClient(handler);
            var provider = new AwsMskIamDefaultCredentialsProvider(httpClient);

            var credentials = await provider.GetCredentialsAsync();

            await Assert.That(credentials.AccessKeyId).IsEqualTo("profile-access");
            await Assert.That(credentials.SecretAccessKey).IsEqualTo("profile-secret");
            await Assert.That(credentials.SessionToken).IsEqualTo("profile-token");
            await Assert.That(handler.Requests.Count).IsEqualTo(1);
            await Assert.That(handler.Requests[0].Method).IsEqualTo(HttpMethod.Post);
            await Assert.That(handler.Requests[0].RequestUri!.Host).IsEqualTo("sts.us-east-1.amazonaws.com");
        }
        finally
        {
            File.Delete(tokenFile);
            File.Delete(credentialsFile);
            snapshot.Restore();
        }
    }

    [Test]
    public async Task GetCredentialsAsync_ImdsTokenFailureDoesNotFallbackToUnauthenticatedMetadata()
    {
        var snapshot = EnvironmentSnapshot.Capture(
            "AWS_ACCESS_KEY_ID",
            "AWS_SECRET_ACCESS_KEY",
            "AWS_SESSION_TOKEN",
            "AWS_ROLE_ARN",
            "AWS_WEB_IDENTITY_TOKEN_FILE",
            "AWS_SHARED_CREDENTIALS_FILE",
            "AWS_CONFIG_FILE",
            "AWS_PROFILE",
            "AWS_CONTAINER_CREDENTIALS_FULL_URI",
            "AWS_CONTAINER_CREDENTIALS_RELATIVE_URI",
            "AWS_EC2_METADATA_DISABLED");
        var credentialsFile = Path.GetTempFileName();
        var configFile = Path.GetTempFileName();

        try
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", null);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", null);
            Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", null);
            Environment.SetEnvironmentVariable("AWS_ROLE_ARN", null);
            Environment.SetEnvironmentVariable("AWS_WEB_IDENTITY_TOKEN_FILE", null);
            Environment.SetEnvironmentVariable("AWS_SHARED_CREDENTIALS_FILE", credentialsFile);
            Environment.SetEnvironmentVariable("AWS_CONFIG_FILE", configFile);
            Environment.SetEnvironmentVariable("AWS_PROFILE", "dekaf-missing-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_FULL_URI", null);
            Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", null);
            Environment.SetEnvironmentVariable("AWS_EC2_METADATA_DISABLED", null);

            var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
            using var httpClient = new HttpClient(handler);
            var provider = new AwsMskIamDefaultCredentialsProvider(httpClient);

            await Assert.That(async () => await provider.GetCredentialsAsync()).Throws<InvalidOperationException>();
            await Assert.That(handler.Requests.Count).IsEqualTo(1);
            await Assert.That(handler.Requests[0].Method).IsEqualTo(HttpMethod.Put);
            await Assert.That(handler.Requests[0].RequestUri!.AbsolutePath).IsEqualTo("/latest/api/token");
        }
        finally
        {
            File.Delete(credentialsFile);
            File.Delete(configFile);
            snapshot.Restore();
        }
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public List<CapturedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri));
            return Task.FromResult(_responseFactory(request));
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri? RequestUri);

    private sealed class EnvironmentSnapshot
    {
        private readonly Dictionary<string, string?> _values;

        private EnvironmentSnapshot(Dictionary<string, string?> values)
        {
            _values = values;
        }

        public static EnvironmentSnapshot Capture(params string[] names)
        {
            var values = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var name in names)
                values[name] = Environment.GetEnvironmentVariable(name);
            return new EnvironmentSnapshot(values);
        }

        public void Restore()
        {
            foreach (var (name, value) in _values)
                Environment.SetEnvironmentVariable(name, value);
        }
    }
}
