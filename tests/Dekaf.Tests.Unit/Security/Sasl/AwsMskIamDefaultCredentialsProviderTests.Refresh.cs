using System.Net;
using System.Text.Json;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security.Sasl;

public sealed partial class AwsMskIamDefaultCredentialsProviderTests
{
    [Test]
    public async Task WebIdentity_ParsesNamespacedStsResponseAndCachesCredentials()
    {
        using var environment = new CredentialEnvironment();
        Environment.SetEnvironmentVariable("AWS_ROLE_ARN", "arn:aws:iam::123456789012:role/test");
        Environment.SetEnvironmentVariable("AWS_WEB_IDENTITY_TOKEN_FILE", environment.WriteFile("web-identity-token"));
        Environment.SetEnvironmentVariable("AWS_ROLE_SESSION_NAME", "test-session");
        var expiration = DateTimeOffset.UtcNow.AddHours(1);
        var calls = 0;
        using var http = new HttpClient(new CredentialHandler(async (request, token) =>
        {
            calls++;
            await Assert.That(request.RequestUri!.AbsoluteUri).IsEqualTo("https://sts.us-east-1.amazonaws.com/");
            await Assert.That(request.Method).IsEqualTo(HttpMethod.Post);
            var body = await request.Content!.ReadAsStringAsync(token);
            await Assert.That(body).Contains("Action=AssumeRoleWithWebIdentity");
            await Assert.That(body).Contains("WebIdentityToken=web-identity-token");
            await Assert.That(body).Contains("RoleSessionName=test-session");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"""
                    <AssumeRoleWithWebIdentityResponse xmlns="https://sts.amazonaws.com/doc/2011-06-15/">
                      <AssumeRoleWithWebIdentityResult><Credentials>
                        <AccessKeyId>sts-access</AccessKeyId><SecretAccessKey>sts-secret</SecretAccessKey>
                        <SessionToken>sts-token</SessionToken><Expiration>{expiration:O}</Expiration>
                      </Credentials></AssumeRoleWithWebIdentityResult>
                    </AssumeRoleWithWebIdentityResponse>
                    """)
            };
        }));
        var provider = new AwsMskIamDefaultCredentialsProvider(http);

        var credentials = await provider.GetCredentialsAsync();
        var cached = await provider.GetCredentialsAsync();

        await AssertCredentialsAsync(credentials, "sts-access", "sts-secret", "sts-token", expiration);
        await Assert.That(cached).IsSameReferenceAs(credentials);
        await Assert.That(calls).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Container_ParsesCredentialsAndSendsAuthorization(bool tokenFile)
    {
        using var environment = new CredentialEnvironment();
        Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", "/credentials/test");
        Environment.SetEnvironmentVariable(tokenFile ? "AWS_CONTAINER_AUTHORIZATION_TOKEN_FILE" : "AWS_CONTAINER_AUTHORIZATION_TOKEN",
            tokenFile ? environment.WriteFile("container-token\n") : "container-token");
        var expiration = DateTimeOffset.UtcNow.AddHours(1);
        using var http = new HttpClient(new CredentialHandler(async (request, _) =>
        {
            await Assert.That(request.RequestUri!.AbsoluteUri).IsEqualTo("http://169.254.170.2/credentials/test");
            await Assert.That(request.Method).IsEqualTo(HttpMethod.Get);
            await Assert.That(request.Headers.GetValues("Authorization").Single()).IsEqualTo("container-token");
            return CredentialResponse(expiration);
        }));

        var credentials = await new AwsMskIamDefaultCredentialsProvider(http).GetCredentialsAsync();

        await AssertCredentialsAsync(credentials, "access", "secret", "token", expiration);
    }

    [Test]
    [Arguments("http://localhost:8080/credentials?source=container")]
    [Arguments("http://127.0.0.1:8080/credentials?source=container")]
    [Arguments("http://[::1]:8080/credentials?source=container")]
    public async Task Container_FullUriUsesHttpLoopbackEndpoint(string endpoint)
    {
        using var environment = new CredentialEnvironment();
        Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_FULL_URI", endpoint);
        Environment.SetEnvironmentVariable("AWS_CONTAINER_AUTHORIZATION_TOKEN", "container-token");
        var expiration = DateTimeOffset.UtcNow.AddHours(1);
        var calls = 0;
        using var http = new HttpClient(new CredentialHandler(async (request, _) =>
        {
            calls++;
            await Assert.That(request.RequestUri).IsEqualTo(new Uri(endpoint));
            await Assert.That(request.Method).IsEqualTo(HttpMethod.Get);
            await Assert.That(request.Headers.GetValues("Authorization").Single()).IsEqualTo("container-token");
            return CredentialResponse(expiration);
        }));
        var provider = new AwsMskIamDefaultCredentialsProvider(http);

        var credentials = await provider.GetCredentialsAsync();
        var cached = await provider.GetCredentialsAsync();

        await AssertCredentialsAsync(credentials, "access", "secret", "token", expiration);
        await Assert.That(cached).IsSameReferenceAs(credentials);
        await Assert.That(calls).IsEqualTo(1);
    }

    [Test]
    [Arguments("http://169.254.170.23/credentials/other")]
    [Arguments("http://example.com/credentials/other")]
    [Arguments("not a URI")]
    public async Task Container_RelativeUriTakesPrecedenceOverFullUri(string fullUri)
    {
        using var environment = new CredentialEnvironment();
        Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", "/credentials/task");
        Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_FULL_URI", fullUri);
        var expiration = DateTimeOffset.UtcNow.AddHours(1);
        var calls = 0;
        using var http = new HttpClient(new CredentialHandler(async (request, _) =>
        {
            calls++;
            await Assert.That(request.RequestUri!.AbsoluteUri).IsEqualTo("http://169.254.170.2/credentials/task");
            return CredentialResponse(expiration);
        }));

        var credentials = await new AwsMskIamDefaultCredentialsProvider(http).GetCredentialsAsync();

        await AssertCredentialsAsync(credentials, "access", "secret", "token", expiration);
        await Assert.That(calls).IsEqualTo(1);
    }

    [Test]
    public async Task Container_FullUriIsUsedWhenRelativeUriIsAbsent()
    {
        using var environment = new CredentialEnvironment();
        Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_FULL_URI", "http://169.254.170.23/credentials/test");
        var expiration = DateTimeOffset.UtcNow.AddHours(1);
        using var http = new HttpClient(new CredentialHandler(async (request, _) =>
        {
            await Assert.That(request.RequestUri!.AbsoluteUri).IsEqualTo("http://169.254.170.23/credentials/test");
            return CredentialResponse(expiration);
        }));

        var credentials = await new AwsMskIamDefaultCredentialsProvider(http).GetCredentialsAsync();

        await AssertCredentialsAsync(credentials, "access", "secret", "token", expiration);
    }

    [Test]
    public async Task Container_UnreadableTokenFileDoesNotFallBackToStaticToken()
    {
        using var environment = new CredentialEnvironment();
        Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", "/credentials/test");
        Environment.SetEnvironmentVariable("AWS_CONTAINER_AUTHORIZATION_TOKEN", "stale-token");
        var tokenFile = environment.WriteFile("token");
        Environment.SetEnvironmentVariable("AWS_CONTAINER_AUTHORIZATION_TOKEN_FILE", tokenFile);
        using var lockedFile = new FileStream(tokenFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var calls = 0;
        using var http = new HttpClient(new CredentialHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(CredentialResponse(DateTimeOffset.UtcNow.AddHours(1)));
        }));
        var provider = new AwsMskIamDefaultCredentialsProvider(http);

        await Assert.That(async () => await provider.GetCredentialsAsync()).Throws<IOException>();

        await Assert.That(calls).IsEqualTo(0);
    }

    [Test]
    public async Task Container_TokenFileTakesPrecedenceAndRotationIsReadOnRefresh()
    {
        using var environment = new CredentialEnvironment();
        Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", "/credentials/test");
        Environment.SetEnvironmentVariable("AWS_CONTAINER_AUTHORIZATION_TOKEN", "stale-token");
        var tokenFile = environment.WriteFile("first-token\n");
        Environment.SetEnvironmentVariable("AWS_CONTAINER_AUTHORIZATION_TOKEN_FILE", tokenFile);
        var now = DateTimeOffset.UtcNow;
        var expiration = now.AddHours(1);
        var calls = 0;
        using var http = new HttpClient(new CredentialHandler(async (request, _) =>
        {
            calls++;
            await Assert.That(request.Headers.GetValues("Authorization").Single())
                .IsEqualTo(calls == 1 ? "first-token" : "rotated-token");
            return CredentialResponse(now.AddHours(1), calls == 1 ? "access" : "refreshed");
        }));
        var provider = new AwsMskIamDefaultCredentialsProvider(http, utcNow: () => now);

        var first = await provider.GetCredentialsAsync();
        await File.WriteAllTextAsync(tokenFile, "rotated-token\n");
        now = expiration - AwsMskIamDefaultCredentialsProvider.CacheRefreshInterval - TimeSpan.FromSeconds(1);
        var cached = await provider.GetCredentialsAsync();
        await Assert.That(cached).IsSameReferenceAs(first);
        await Assert.That(calls).IsEqualTo(1);
        now += TimeSpan.FromSeconds(1);
        var refreshed = await provider.GetCredentialsAsync();

        await Assert.That(refreshed.AccessKeyId).IsEqualTo("refreshed");
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task Container_MissingTokenFileDoesNotFallBackToStaticToken()
    {
        using var environment = new CredentialEnvironment();
        Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", "/credentials/test");
        Environment.SetEnvironmentVariable("AWS_CONTAINER_AUTHORIZATION_TOKEN", "stale-token");
        var tokenFile = environment.WriteFile("token");
        File.Delete(tokenFile);
        Environment.SetEnvironmentVariable("AWS_CONTAINER_AUTHORIZATION_TOKEN_FILE", tokenFile);
        var calls = 0;
        using var http = new HttpClient(new CredentialHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(CredentialResponse(DateTimeOffset.UtcNow.AddHours(1)));
        }));
        var provider = new AwsMskIamDefaultCredentialsProvider(http);

        await Assert.That(async () => await provider.GetCredentialsAsync()).Throws<FileNotFoundException>();

        await Assert.That(calls).IsEqualTo(0);
        await File.WriteAllTextAsync(tokenFile, "recovered-token");
        var recovered = await provider.GetCredentialsAsync();
        await Assert.That(recovered.AccessKeyId).IsEqualTo("access");
        await Assert.That(calls).IsEqualTo(1);
    }

    [Test]
    public async Task Container_EmptyTokenFileDoesNotFallBackToStaticToken()
    {
        using var environment = new CredentialEnvironment();
        Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", "/credentials/test");
        Environment.SetEnvironmentVariable("AWS_CONTAINER_AUTHORIZATION_TOKEN", "stale-token");
        Environment.SetEnvironmentVariable("AWS_CONTAINER_AUTHORIZATION_TOKEN_FILE", environment.WriteFile(" \n"));
        var expiration = DateTimeOffset.UtcNow.AddHours(1);
        using var http = new HttpClient(new CredentialHandler(async (request, _) =>
        {
            await Assert.That(request.Headers.Contains("Authorization")).IsFalse();
            return CredentialResponse(expiration);
        }));

        var credentials = await new AwsMskIamDefaultCredentialsProvider(http).GetCredentialsAsync();

        await AssertCredentialsAsync(credentials, "access", "secret", "token", expiration);
    }

    [Test]
    public async Task ImdsV2_UsesTokenForRoleDiscoveryAndCredentialRequest()
    {
        using var environment = new CredentialEnvironment();
        Environment.SetEnvironmentVariable("AWS_EC2_METADATA_DISABLED", "false");
        var expiration = DateTimeOffset.UtcNow.AddHours(1);
        var calls = 0;
        using var http = new HttpClient(new CredentialHandler(async (request, _) =>
        {
            var index = calls++;
            await Assert.That(request.RequestUri!.Host).IsEqualTo("169.254.169.254");
            if (index == 0)
            {
                await Assert.That(request.Method).IsEqualTo(HttpMethod.Put);
                await Assert.That(request.RequestUri.AbsolutePath).IsEqualTo("/latest/api/token");
                await Assert.That(request.Headers.GetValues("X-aws-ec2-metadata-token-ttl-seconds").Single()).IsEqualTo("21600");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("imds-token") };
            }
            await Assert.That(request.Method).IsEqualTo(HttpMethod.Get);
            await Assert.That(request.Headers.GetValues("X-aws-ec2-metadata-token").Single()).IsEqualTo("imds-token");
            await Assert.That(request.RequestUri.AbsolutePath).IsEqualTo(index == 1
                ? "/latest/meta-data/iam/security-credentials/"
                : "/latest/meta-data/iam/security-credentials/test-role");
            return index == 1
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("test-role\n") }
                : CredentialResponse(expiration);
        }));

        var credentials = await new AwsMskIamDefaultCredentialsProvider(http).GetCredentialsAsync();

        await AssertCredentialsAsync(credentials, "access", "secret", "token", expiration);
        await Assert.That(calls).IsEqualTo(3);
    }

    [Test]
    public async Task ExpiringCredentials_ConcurrentRefreshAndCanceledWaiterShareOneRequest()
    {
        using var environment = new CredentialEnvironment();
        Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", "/credentials/test");
        var refreshEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var http = new HttpClient(new CredentialHandler(async (_, token) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                return CredentialResponse(DateTimeOffset.UtcNow.AddMinutes(1));
            refreshEntered.SetResult();
            await releaseRefresh.Task.WaitAsync(token);
            return CredentialResponse(DateTimeOffset.UtcNow.AddHours(1), "refreshed");
        }));
        var provider = new AwsMskIamDefaultCredentialsProvider(http);
        _ = await provider.GetCredentialsAsync();
        var refresh = provider.GetCredentialsAsync().AsTask();
        await refreshEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var cancellation = new CancellationTokenSource();
        var canceled = provider.GetCredentialsAsync(cancellation.Token).AsTask();
        var waiters = Enumerable.Range(0, 16).Select(_ => provider.GetCredentialsAsync().AsTask()).ToArray();
        try
        {
            cancellation.Cancel();
            await Assert.That(async () => await canceled).Throws<OperationCanceledException>();
            await Assert.That(calls).IsEqualTo(2);
        }
        finally
        {
            releaseRefresh.TrySetResult();
        }
        var refreshed = await refresh;
        foreach (var credentials in await Task.WhenAll(waiters))
            await Assert.That(credentials).IsSameReferenceAs(refreshed);
        await Assert.That(refreshed.AccessKeyId).IsEqualTo("refreshed");
        await Assert.That(await provider.GetCredentialsAsync()).IsSameReferenceAs(refreshed);
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task CanceledRefresh_ReleasesGateAndLaterCallRecovers()
    {
        using var environment = new CredentialEnvironment();
        Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", "/credentials/test");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var http = new HttpClient(new CredentialHandler(async (_, token) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return CredentialResponse(DateTimeOffset.UtcNow.AddHours(1));
        }));
        var provider = new AwsMskIamDefaultCredentialsProvider(http);
        using var cancellation = new CancellationTokenSource();
        var refresh = provider.GetCredentialsAsync(cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.That(async () => await refresh).Throws<OperationCanceledException>();

        var recovered = await provider.GetCredentialsAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(recovered.AccessKeyId).IsEqualTo("access");
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task SharedProfile_RotatedFileIsReReadAfterRefreshInterval()
    {
        using var environment = new CredentialEnvironment();
        var credentialsFile = environment.WriteFile(ProfileFile("first"));
        Environment.SetEnvironmentVariable("AWS_SHARED_CREDENTIALS_FILE", credentialsFile);
        Environment.SetEnvironmentVariable("AWS_PROFILE", "msk");
        var now = DateTimeOffset.UtcNow;
        using var http = new HttpClient(new CredentialHandler((_, _) => throw new InvalidOperationException("No HTTP expected")));
        var provider = new AwsMskIamDefaultCredentialsProvider(http, utcNow: () => now);

        var first = await provider.GetCredentialsAsync();
        await File.WriteAllTextAsync(credentialsFile, ProfileFile("second"));
        now += AwsMskIamDefaultCredentialsProvider.CacheRefreshInterval - TimeSpan.FromSeconds(1);
        var beforeInterval = await provider.GetCredentialsAsync();
        now += TimeSpan.FromSeconds(1);
        var afterInterval = await provider.GetCredentialsAsync();

        await Assert.That(first.SessionToken).IsEqualTo("first-token");
        await Assert.That(beforeInterval).IsSameReferenceAs(first);
        await Assert.That(afterInterval.AccessKeyId).IsEqualTo("second-access");
        await Assert.That(afterInterval.SecretAccessKey).IsEqualTo("second-secret");
        await Assert.That(afterInterval.SessionToken).IsEqualTo("second-token");
    }

    [Test]
    public async Task Environment_RotatedVariablesAreReReadAfterRefreshInterval()
    {
        using var environment = new CredentialEnvironment();
        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "first-access");
        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "first-secret");
        Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", "first-token");
        var now = DateTimeOffset.UtcNow;
        using var http = new HttpClient(new CredentialHandler((_, _) => throw new InvalidOperationException("No HTTP expected")));
        var provider = new AwsMskIamDefaultCredentialsProvider(http, utcNow: () => now);

        var first = await provider.GetCredentialsAsync();
        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "second-access");
        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "second-secret");
        Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", "second-token");
        var cached = await provider.GetCredentialsAsync();
        now += AwsMskIamDefaultCredentialsProvider.CacheRefreshInterval;
        var rotated = await provider.GetCredentialsAsync();

        await Assert.That(cached).IsSameReferenceAs(first);
        await Assert.That(rotated.AccessKeyId).IsEqualTo("second-access");
        await Assert.That(rotated.SessionToken).IsEqualTo("second-token");
    }

    [Test]
    public async Task ExpiringCredentials_AreCachedUntilRefreshBufferBeforeExpiration()
    {
        using var environment = new CredentialEnvironment();
        Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", "/credentials/test");
        var now = DateTimeOffset.UtcNow;
        var expiration = now.AddHours(1);
        var calls = 0;
        using var http = new HttpClient(new CredentialHandler((_, _) =>
            Task.FromResult(CredentialResponse(expiration, ++calls == 1 ? "access" : "refreshed"))));
        var provider = new AwsMskIamDefaultCredentialsProvider(http, utcNow: () => now);

        var first = await provider.GetCredentialsAsync();
        now = expiration - AwsMskIamDefaultCredentialsProvider.CacheRefreshInterval - TimeSpan.FromSeconds(1);
        var cached = await provider.GetCredentialsAsync();
        now += TimeSpan.FromSeconds(1);
        var refreshed = await provider.GetCredentialsAsync();

        await Assert.That(cached).IsSameReferenceAs(first);
        await Assert.That(refreshed.AccessKeyId).IsEqualTo("refreshed");
        await Assert.That(calls).IsEqualTo(2);
    }

    private static string ProfileFile(string prefix) => $"""
        [msk]
        aws_access_key_id = {prefix}-access
        aws_secret_access_key = {prefix}-secret
        aws_session_token = {prefix}-token
        """;

    private static HttpResponseMessage CredentialResponse(DateTimeOffset expiration, string access = "access") => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["AccessKeyId"] = access, ["SecretAccessKey"] = "secret", ["Token"] = "token", ["Expiration"] = expiration.ToString("O")
        }))
    };

    private static async Task AssertCredentialsAsync(AwsCredentials credentials, string access, string secret, string token, DateTimeOffset expiration)
    {
        await Assert.That(credentials.AccessKeyId).IsEqualTo(access);
        await Assert.That(credentials.SecretAccessKey).IsEqualTo(secret);
        await Assert.That(credentials.SessionToken).IsEqualTo(token);
        await Assert.That(credentials.Expiration).IsEqualTo(expiration);
    }

    private sealed class CredentialHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }

    private sealed class CredentialEnvironment : IDisposable
    {
        private static readonly string[] Names =
        [
            "AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY", "AWS_SESSION_TOKEN", "AWS_ROLE_ARN", "AWS_ROLE_SESSION_NAME",
            "AWS_WEB_IDENTITY_TOKEN_FILE", "AWS_REGION", "AWS_DEFAULT_REGION", "AWS_SHARED_CREDENTIALS_FILE", "AWS_CONFIG_FILE",
            "AWS_PROFILE", "AWS_CONTAINER_CREDENTIALS_FULL_URI", "AWS_CONTAINER_CREDENTIALS_RELATIVE_URI",
            "AWS_CONTAINER_AUTHORIZATION_TOKEN", "AWS_CONTAINER_AUTHORIZATION_TOKEN_FILE", "AWS_EC2_METADATA_DISABLED"
        ];
        private readonly EnvironmentSnapshot _snapshot = EnvironmentSnapshot.Capture(Names);
        private readonly List<string> _files = [];

        public CredentialEnvironment()
        {
            foreach (var name in Names)
                Environment.SetEnvironmentVariable(name, null);
            Environment.SetEnvironmentVariable("AWS_PROFILE", "dekaf-test-missing-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("AWS_SHARED_CREDENTIALS_FILE", WriteFile(string.Empty));
            Environment.SetEnvironmentVariable("AWS_CONFIG_FILE", WriteFile(string.Empty));
            Environment.SetEnvironmentVariable("AWS_EC2_METADATA_DISABLED", "true");
        }

        public string WriteFile(string content)
        {
            var path = Path.GetTempFileName();
            _files.Add(path);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            _snapshot.Restore();
            foreach (var file in _files)
                File.Delete(file);
        }
    }
}
