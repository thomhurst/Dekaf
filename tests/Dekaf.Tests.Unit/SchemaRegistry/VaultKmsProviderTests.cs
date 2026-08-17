using System.Net;
using System.Text;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Kms.Vault;
using NSubstitute;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public class VaultKmsProviderTests
{
    private const string KeyReference = "https://vault.example:8200/transit/keys/orders-kek";
    private const string NestedMountKeyReference =
        "https://vault.example:8200/team/transit/keys/orders-kek";
    private static readonly Uri VaultAddress = new("https://vault.example:8200/");
    private static readonly string[] ExpectedTokenHeader = ["token"];
    private static readonly string[] ExpectedNamespaceHeader = ["finance"];

    [Test]
    public async Task WrapAndUnwrap_ResolveAddressMountKeyAndNamespace()
    {
        var client = Substitute.For<IVaultTransitClient>();
        client.EncryptAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(Encoding.UTF8.GetBytes("vault:v1:wrapped")));
        client.DecryptAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new byte[] { 1, 2, 3 }));
        var provider = new VaultKmsProvider(client, VaultAddress, "finance");
        var plaintext = new byte[] { 1, 2, 3 };

        var keyReference = CreateKeyReference(NestedMountKeyReference);
        var encrypted = await provider.WrapKeyAsync(plaintext, keyReference);
        var decrypted = await provider.UnwrapKeyAsync(encrypted, keyReference);

        await Assert.That(encrypted).IsEquivalentTo(Encoding.UTF8.GetBytes("vault:v1:wrapped"));
        await Assert.That(decrypted).IsEquivalentTo(plaintext);
        await client.Received(1).EncryptAsync(
            VaultAddress,
            "team/transit",
            "orders-kek",
            "finance",
            Arg.Is<ReadOnlyMemory<byte>>(value => MemoryEquals(value, plaintext)),
            Arg.Any<CancellationToken>());
        await client.Received(1).DecryptAsync(
            VaultAddress,
            "team/transit",
            "orders-kek",
            "finance",
            Arg.Is<ReadOnlyMemory<byte>>(value => MemoryEquals(value, encrypted)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(HttpStatusCode.NotFound, "wrong key")]
    [Arguments(HttpStatusCode.Forbidden, "authorization: sensitive")]
    [Arguments(HttpStatusCode.BadRequest, "ciphertext: sensitive")]
    public async Task VaultFailure_IsReportedWithoutProviderResponse(HttpStatusCode statusCode, string detail)
    {
        var client = Substitute.For<IVaultTransitClient>();
        var failure = new HttpRequestException(detail, null, statusCode);
        client.DecryptAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<byte[]>(failure));
        var provider = new VaultKmsProvider(client, VaultAddress);

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.UnwrapKeyAsync(Encoding.UTF8.GetBytes("vault:v1:bad"), CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).IsEqualTo("Vault Transit unwrap failed.");
        await Assert.That(exception.Message).DoesNotContain("sensitive");
        await Assert.That(exception.Message).DoesNotContain("wrong key");
        await Assert.That(exception.InnerException).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task Cancellation_IsPropagatedToInFlightTransitCall()
    {
        var client = Substitute.For<IVaultTransitClient>();
        client.EncryptAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<byte[]>(WaitForCancellationAsync(call.Arg<CancellationToken>())));
        var provider = new VaultKmsProvider(client, VaultAddress);
        using var cancellation = new CancellationTokenSource();
        var operation = provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(), cancellation.Token).AsTask();

        cancellation.Cancel();

        await Assert.That(async () => await operation).Throws<OperationCanceledException>();
        await client.Received(1).EncryptAsync(
            VaultAddress,
            "transit",
            "orders-kek",
            null,
            Arg.Any<ReadOnlyMemory<byte>>(),
            cancellation.Token);
    }

    [Test]
    public async Task SharedProvider_UsesClientConcurrently()
    {
        var client = Substitute.For<IVaultTransitClient>();
        client.EncryptAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<byte[]>(EchoAfterYieldAsync(call.Arg<ReadOnlyMemory<byte>>())));
        var provider = new VaultKmsProvider(client, VaultAddress);

        var operations = Enumerable.Range(1, 32)
            .Select(value => provider.WrapKeyAsync(new byte[] { (byte)value }, CreateKeyReference()).AsTask())
            .ToArray();
        var results = await Task.WhenAll(operations);

        for (var index = 0; index < results.Length; index++)
            await Assert.That(results[index]).IsEquivalentTo(new byte[] { (byte)(index + 1) });
        await client.Received(32).EncryptAsync(
            VaultAddress,
            "transit",
            "orders-kek",
            null,
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ServerProvidedAuthority_IsRejectedBeforeVaultCall()
    {
        var client = Substitute.For<IVaultTransitClient>();
        var provider = new VaultKmsProvider(client, VaultAddress);

        await Assert.That(async () => await provider.WrapKeyAsync(
                new byte[] { 1 },
                CreateKeyReference("https://attacker.example:8200/transit/keys/orders-kek")))
            .Throws<SchemaRegistryKmsException>();
        await client.DidNotReceiveWithAnyArgs().EncryptAsync(
            default!, default!, default!, default, default, default);
    }

    [Test]
    [Arguments("hcvault://https://vault.example:8200/transit/keys/orders-kek")]
    [Arguments("ftp://vault.example/transit/keys/orders-kek")]
    [Arguments("https://vault.example/transit/orders-kek")]
    [Arguments("https://user@vault.example/transit/keys/orders-kek")]
    [Arguments("https://vault.example:8200/transit/keys/orders-kek/")]
    public async Task InvalidKeyReference_IsRejectedBeforeVaultCall(string keyId)
    {
        var client = Substitute.For<IVaultTransitClient>();
        var provider = new VaultKmsProvider(client, VaultAddress);

        await Assert.That(async () => await provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(keyId)))
            .Throws<SchemaRegistryKmsException>();
        await client.DidNotReceiveWithAnyArgs().EncryptAsync(
            default!, default!, default!, default, default, default);
    }

    [Test]
    public async Task HttpClient_UsesTransitApiHeadersAndPayloads()
    {
        var requestNumber = 0;
        var handler = new RecordingHandler(async (request, cancellationToken) =>
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            await Assert.That(request.Headers.GetValues("X-Vault-Token")).IsEquivalentTo(ExpectedTokenHeader);
            await Assert.That(request.Headers.GetValues("X-Vault-Namespace"))
                .IsEquivalentTo(ExpectedNamespaceHeader);

            requestNumber++;
            if (requestNumber == 1)
            {
                await Assert.That(request.RequestUri!.AbsolutePath)
                    .IsEqualTo("/v1/team/transit/encrypt/orders-kek");
                await Assert.That(body).IsEqualTo("{\"plaintext\":\"AQID\"}");
                return JsonResponse("{\"data\":{\"ciphertext\":\"vault:v1:wrapped\"}}");
            }

            await Assert.That(request.RequestUri!.AbsolutePath)
                .IsEqualTo("/v1/team/transit/decrypt/orders-kek");
            await Assert.That(body).IsEqualTo("{\"ciphertext\":\"vault:v1:wrapped\"}");
            return JsonResponse("{\"data\":{\"plaintext\":\"AQID\"}}");
        });
        using var httpClient = new HttpClient(handler);
        var client = new VaultTransitHttpClient(httpClient, new VaultStaticTokenProvider("token"));

        var encrypted = await client.EncryptAsync(
            VaultAddress, "team/transit", "orders-kek", "finance", new byte[] { 1, 2, 3 });
        var decrypted = await client.DecryptAsync(
            VaultAddress, "team/transit", "orders-kek", "finance", encrypted);

        await Assert.That(encrypted).IsEquivalentTo(Encoding.UTF8.GetBytes("vault:v1:wrapped"));
        await Assert.That(decrypted).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(requestNumber).IsEqualTo(2);
    }

    [Test]
    public async Task HttpClient_ForwardsCancellationToTransport()
    {
        var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Cancellation was not observed.");
        });
        using var httpClient = new HttpClient(handler);
        var client = new VaultTransitHttpClient(httpClient, new VaultStaticTokenProvider("token"));
        using var cancellation = new CancellationTokenSource();
        var operation = client.EncryptAsync(
            VaultAddress, "transit", "orders-kek", null, new byte[] { 1 }, cancellation.Token).AsTask();

        cancellation.Cancel();

        await Assert.That(async () => await operation).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task Provider_RejectsBaseAddressPath()
    {
        var client = Substitute.For<IVaultTransitClient>();
        var action = () => new VaultKmsProvider(
            client,
            new Uri("https://vault.example:8200/prefix/"));

        await Assert.That(action).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(".")]
    [Arguments("..")]
    public async Task HttpClient_RejectsDotSegmentKeyName(string keyName)
    {
        var handler = new RecordingHandler(static (_, _) =>
            throw new InvalidOperationException("HTTP request was not expected."));
        using var httpClient = new HttpClient(handler);
        var client = new VaultTransitHttpClient(httpClient, new VaultStaticTokenProvider("token"));

        await Assert.That(async () => await client.EncryptAsync(
                VaultAddress,
                "transit",
                keyName,
                null,
                new byte[] { 1 }))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task ResponseStream_AsyncDisposalDoesNotCaptureCallerContext()
    {
        var synchronizationContext = new QueueingSynchronizationContext();
        var operation = Task.Run(() =>
        {
            var previousContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(synchronizationContext);
                using var content = new StreamContent(new AsynchronousDisposeStream());
                return VaultTransitHttpClient
                    .ReadResponseBytesAsync(content, CancellationToken.None)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        });

        try
        {
            await Assert.That(await operation.WaitAsync(TimeSpan.FromSeconds(1))).IsEmpty();
        }
        finally
        {
            synchronizationContext.Drain();
        }
    }

    [Test]
    public async Task AppRoleProvider_LogsInOnceForConcurrentCallers()
    {
        var loginCount = 0;
        var handler = new RecordingHandler(async (request, cancellationToken) =>
        {
            Interlocked.Increment(ref loginCount);
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            await Assert.That(request.RequestUri!.AbsolutePath).IsEqualTo("/v1/auth/team-approle/login");
            await Assert.That(request.Headers.GetValues("X-Vault-Namespace"))
                .IsEquivalentTo(ExpectedNamespaceHeader);
            await Assert.That(body).Contains("\"role_id\":\"role\"");
            await Assert.That(body).Contains("\"secret_id\":\"secret\"");
            await Task.Yield();
            return JsonResponse(
                "{\"auth\":{\"client_token\":\"app-token\",\"lease_duration\":3600}}");
        });
        using var httpClient = new HttpClient(handler);
        var provider = new VaultAppRoleTokenProvider(httpClient, "role", "secret", "team-approle");

        var operations = Enumerable.Range(0, 32)
            .Select(_ => provider.GetTokenAsync(VaultAddress, "finance").AsTask())
            .ToArray();
        var tokens = await Task.WhenAll(operations);

        foreach (var token in tokens)
            await Assert.That(token).IsEqualTo("app-token");
        await Assert.That(loginCount).IsEqualTo(1);
    }

    [Test]
    public async Task AppRoleProvider_BasesExpiryOnLoginStartTime()
    {
        var loginCount = 0;
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 8, 17, 0, 0, 0, TimeSpan.Zero));
        var handler = new RecordingHandler((_, _) =>
        {
            Interlocked.Increment(ref loginCount);
            timeProvider.Advance(TimeSpan.FromSeconds(31));
            return Task.FromResult(JsonResponse(
                "{\"auth\":{\"client_token\":\"app-token\",\"lease_duration\":60}}"));
        });
        using var httpClient = new HttpClient(handler);
        var provider = new VaultAppRoleTokenProvider(
            httpClient,
            "role",
            "secret",
            "approle",
            timeProvider);

        _ = await provider.GetTokenAsync(VaultAddress, null);
        _ = await provider.GetTokenAsync(VaultAddress, null);

        await Assert.That(loginCount).IsEqualTo(2);
    }

    private static SchemaRegistryKmsKeyReference CreateKeyReference(string keyId = KeyReference) => new()
    {
        KmsType = VaultKmsProvider.DefaultType,
        KmsKeyId = keyId
    };

    private static bool MemoryEquals(ReadOnlyMemory<byte> actual, byte[] expected) =>
        actual.Span.SequenceEqual(expected);

    private static async Task<byte[]> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("Cancellation was not observed.");
    }

    private static async Task<byte[]> EchoAfterYieldAsync(ReadOnlyMemory<byte> plaintext)
    {
        await Task.Yield();
        return plaintext.ToArray();
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    private sealed class AsynchronousDisposeStream : MemoryStream
    {
        public override async ValueTask DisposeAsync()
        {
            await Task.Delay(10).ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class QueueingSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _callbacks = new();

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_callbacks)
                _callbacks.Enqueue((d, state));
        }

        internal void Drain()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? State) callback;
                lock (_callbacks)
                {
                    if (!_callbacks.TryDequeue(out callback))
                        return;
                }

                callback.Callback(callback.State);
            }
        }
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        internal void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
