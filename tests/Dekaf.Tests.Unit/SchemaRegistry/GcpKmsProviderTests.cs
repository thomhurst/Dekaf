using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Grpc.Core;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Kms.Gcp;
using NSubstitute;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public class GcpKmsProviderTests
{
    private const string KeyResourceName =
        "projects/payments/locations/europe-west2/keyRings/orders/cryptoKeys/kek";

    [Test]
    public async Task WrapAndUnwrap_UseConfiguredCryptoKey()
    {
        var client = Substitute.For<KeyManagementServiceClient>();
        ByteString? plaintext = null;
        ByteString? ciphertext = null;
        var ciphertextResponseBuffer = new byte[] { 4, 5, 6 };
        var plaintextResponseBuffer = new byte[] { 1, 2, 3 };
        client.EncryptAsync(
                Arg.Any<CryptoKeyName>(),
                Arg.Do<ByteString>(value => plaintext = value),
                Arg.Any<CancellationToken>())
            .Returns(new EncryptResponse
            {
                Ciphertext = UnsafeByteOperations.UnsafeWrap(ciphertextResponseBuffer)
            });
        client.DecryptAsync(
                Arg.Any<CryptoKeyName>(),
                Arg.Do<ByteString>(value => ciphertext = value),
                Arg.Any<CancellationToken>())
            .Returns(new DecryptResponse
            {
                Plaintext = UnsafeByteOperations.UnsafeWrap(plaintextResponseBuffer)
            });
        var provider = new GcpKmsProvider(client);

        var encrypted = await provider.WrapKeyAsync(new byte[] { 1, 2, 3 }, CreateKeyReference());
        var decrypted = await provider.UnwrapKeyAsync(encrypted, CreateKeyReference());

        await Assert.That(encrypted).IsEquivalentTo(new byte[] { 4, 5, 6 });
        await Assert.That(decrypted).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(plaintext!.ToByteArray()).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(ciphertext!.ToByteArray()).IsEquivalentTo(new byte[] { 4, 5, 6 });
        await Assert.That(ciphertextResponseBuffer).IsEquivalentTo(new byte[] { 0, 0, 0 });
        await Assert.That(plaintextResponseBuffer).IsEquivalentTo(new byte[] { 0, 0, 0 });
        await client.Received(1).EncryptAsync(
            CryptoKeyName.Parse(KeyResourceName),
            Arg.Any<ByteString>(),
            Arg.Any<CancellationToken>());
        await client.Received(1).DecryptAsync(
            CryptoKeyName.Parse(KeyResourceName),
            Arg.Any<ByteString>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(KeyResourceName)]
    [Arguments(GcpKmsProvider.KeyUriPrefix + KeyResourceName)]
    public async Task KeyReference_AcceptsRawResourceOrProviderUri(string keyId)
    {
        var client = Substitute.For<KeyManagementServiceClient>();
        client.EncryptAsync(Arg.Any<CryptoKeyName>(), Arg.Any<ByteString>(), Arg.Any<CancellationToken>())
            .Returns(new EncryptResponse { Ciphertext = ByteString.CopyFrom([9]) });
        var provider = new GcpKmsProvider(client);

        await provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(keyId));

        await client.Received(1).EncryptAsync(
            CryptoKeyName.Parse(KeyResourceName),
            Arg.Any<ByteString>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task WrongKey_IsReportedWithoutProviderResponse()
    {
        var client = Substitute.For<KeyManagementServiceClient>();
        client.DecryptAsync(Arg.Any<CryptoKeyName>(), Arg.Any<ByteString>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DecryptResponse>(CreateRpcException(StatusCode.NotFound, "wrong key")));
        var provider = new GcpKmsProvider(client);

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.UnwrapKeyAsync(new byte[] { 1 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).IsEqualTo("Google Cloud KMS unwrap failed.");
        await Assert.That(exception.Message).DoesNotContain("wrong key");
    }

    [Test]
    public async Task AuthorizationFailure_IsReportedWithoutProviderResponse()
    {
        var client = Substitute.For<KeyManagementServiceClient>();
        var denied = CreateRpcException(StatusCode.PermissionDenied, "authorization: sensitive");
        client.EncryptAsync(Arg.Any<CryptoKeyName>(), Arg.Any<ByteString>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EncryptResponse>(denied));
        var provider = new GcpKmsProvider(client);

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).IsEqualTo("Google Cloud KMS wrap failed.");
        await Assert.That(exception.Message).DoesNotContain("sensitive");
        await Assert.That(exception.InnerException).IsSameReferenceAs(denied);
    }

    [Test]
    public async Task MalformedCiphertext_IsReportedWithoutProviderResponse()
    {
        var client = Substitute.For<KeyManagementServiceClient>();
        client.DecryptAsync(Arg.Any<CryptoKeyName>(), Arg.Any<ByteString>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DecryptResponse>(
                CreateRpcException(StatusCode.InvalidArgument, "ciphertext: sensitive")));
        var provider = new GcpKmsProvider(client);

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.UnwrapKeyAsync(new byte[] { 1 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).IsEqualTo("Google Cloud KMS unwrap failed.");
        await Assert.That(exception.Message).DoesNotContain("sensitive");
    }

    [Test]
    public async Task Cancellation_IsPropagatedToInFlightGcpCall()
    {
        var client = Substitute.For<KeyManagementServiceClient>();
        client.EncryptAsync(Arg.Any<CryptoKeyName>(), Arg.Any<ByteString>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(call.Arg<CancellationToken>()));
        var provider = new GcpKmsProvider(client);
        using var cancellation = new CancellationTokenSource();
        var operation = provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(), cancellation.Token).AsTask();

        cancellation.Cancel();

        await Assert.That(async () => await operation).Throws<OperationCanceledException>();
        await client.Received(1).EncryptAsync(
            CryptoKeyName.Parse(KeyResourceName),
            Arg.Any<ByteString>(),
            cancellation.Token);
    }

    [Test]
    public async Task RpcCancellation_IsTranslatedWhenCallerTokenIsCanceled()
    {
        var completion = new TaskCompletionSource<EncryptResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = Substitute.For<KeyManagementServiceClient>();
        client.EncryptAsync(Arg.Any<CryptoKeyName>(), Arg.Any<ByteString>(), Arg.Any<CancellationToken>())
            .Returns(completion.Task);
        var provider = new GcpKmsProvider(client);
        using var cancellation = new CancellationTokenSource();
        var operation = provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(), cancellation.Token).AsTask();

        cancellation.Cancel();
        completion.SetException(CreateRpcException(StatusCode.Cancelled, "canceled"));

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => operation);
        await Assert.That(exception!.CancellationToken).IsEqualTo(cancellation.Token);
    }

    [Test]
    public async Task SharedProvider_UsesClientConcurrently()
    {
        var client = Substitute.For<KeyManagementServiceClient>();
        client.EncryptAsync(Arg.Any<CryptoKeyName>(), Arg.Any<ByteString>(), Arg.Any<CancellationToken>())
            .Returns(call => EchoAfterYieldAsync(call.Arg<ByteString>()));
        var provider = new GcpKmsProvider(client);

        var operations = Enumerable.Range(1, 32)
            .Select(value => provider.WrapKeyAsync(new byte[] { (byte)value }, CreateKeyReference()).AsTask())
            .ToArray();
        var results = await Task.WhenAll(operations);

        for (var index = 0; index < results.Length; index++)
            await Assert.That(results[index]).IsEquivalentTo(new byte[] { (byte)(index + 1) });
        await client.Received(32).EncryptAsync(
            CryptoKeyName.Parse(KeyResourceName),
            Arg.Any<ByteString>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InvalidResourceName_IsRejectedBeforeGcpCall()
    {
        var client = Substitute.For<KeyManagementServiceClient>();
        var provider = new GcpKmsProvider(client);
        var reference = CreateKeyReference("projects/payments/locations/europe-west2/keyRings/orders");

        await Assert.That(async () => await provider.WrapKeyAsync(new byte[] { 1 }, reference))
            .Throws<SchemaRegistryKmsException>();
        await client.DidNotReceive()
            .EncryptAsync(
                Arg.Any<CryptoKeyName>(),
                Arg.Any<ByteString>(),
                Arg.Any<CancellationToken>());
    }

    private static SchemaRegistryKmsKeyReference CreateKeyReference(string keyId = KeyResourceName) => new()
    {
        KmsType = GcpKmsProvider.DefaultType,
        KmsKeyId = keyId
    };

    private static RpcException CreateRpcException(StatusCode statusCode, string detail) =>
        new(new Status(statusCode, detail));

    private static async Task<EncryptResponse> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("Cancellation was not observed.");
    }

    private static async Task<EncryptResponse> EchoAfterYieldAsync(ByteString plaintext)
    {
        await Task.Yield();
        return new EncryptResponse { Ciphertext = ByteString.CopyFrom(plaintext.Span) };
    }
}
