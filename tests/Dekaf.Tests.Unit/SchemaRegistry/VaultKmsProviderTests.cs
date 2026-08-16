using System.Net;
using System.Text;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Kms.Vault;
using NSubstitute;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public class VaultKmsProviderTests
{
    private const string KeyReference = "hcvault://https://vault.example:8200/orders-kek";
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
        var provider = new VaultKmsProvider(client, "team/transit", "finance");
        var plaintext = new byte[] { 1, 2, 3 };

        var encrypted = await provider.WrapKeyAsync(plaintext, CreateKeyReference());
        var decrypted = await provider.UnwrapKeyAsync(encrypted, CreateKeyReference());

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
        var provider = new VaultKmsProvider(client);

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
        var provider = new VaultKmsProvider(client);
        using var cancellation = new CancellationTokenSource();
        var operation = provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(), cancellation.Token).AsTask();

        cancellation.Cancel();

        await Assert.That(async () => await operation).Throws<OperationCanceledException>();
        await client.Received(1).EncryptAsync(
            VaultAddress,
            VaultKmsProvider.DefaultMountPoint,
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
        var provider = new VaultKmsProvider(client);

        var operations = Enumerable.Range(1, 32)
            .Select(value => provider.WrapKeyAsync(new byte[] { (byte)value }, CreateKeyReference()).AsTask())
            .ToArray();
        var results = await Task.WhenAll(operations);

        for (var index = 0; index < results.Length; index++)
            await Assert.That(results[index]).IsEquivalentTo(new byte[] { (byte)(index + 1) });
        await client.Received(32).EncryptAsync(
            VaultAddress,
            VaultKmsProvider.DefaultMountPoint,
            "orders-kek",
            null,
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments("https://vault.example:8200/orders-kek")]
    [Arguments("hcvault://ftp://vault.example/orders-kek")]
    [Arguments("hcvault://https://vault.example/transit/orders-kek")]
    [Arguments("hcvault://https://user@vault.example/orders-kek")]
    public async Task InvalidKeyReference_IsRejectedBeforeVaultCall(string keyId)
    {
        var client = Substitute.For<IVaultTransitClient>();
        var provider = new VaultKmsProvider(client);

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
}
