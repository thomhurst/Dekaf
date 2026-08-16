using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Grpc.Core;
using Dekaf.Protocol.Records;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Kms.Gcp;
using NSubstitute;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public class GcpKmsProviderTests
{
    private const string KeyResourceName =
        "projects/payments/locations/europe-west2/keyRings/orders/cryptoKeys/kek";
    private const string KeyVersionResourceName = KeyResourceName + "/cryptoKeyVersions/1";

    [Test]
    public async Task WrapAndUnwrap_UseConfiguredCryptoKey()
    {
        var client = Substitute.For<KeyManagementServiceClient>();
        EncryptRequest? encryptRequest = null;
        DecryptRequest? decryptRequest = null;
        var ciphertextResponseBuffer = new byte[] { 4, 5, 6 };
        var plaintextResponseBuffer = new byte[] { 1, 2, 3 };
        client.EncryptAsync(
                Arg.Do<EncryptRequest>(value => encryptRequest = value),
                Arg.Any<CancellationToken>())
            .Returns(new EncryptResponse
            {
                Name = KeyVersionResourceName,
                Ciphertext = UnsafeByteOperations.UnsafeWrap(ciphertextResponseBuffer),
                CiphertextCrc32C = Crc32C.Compute(ciphertextResponseBuffer),
                VerifiedPlaintextCrc32C = true
            });
        client.DecryptAsync(
                Arg.Do<DecryptRequest>(value => decryptRequest = value),
                Arg.Any<CancellationToken>())
            .Returns(new DecryptResponse
            {
                Plaintext = UnsafeByteOperations.UnsafeWrap(plaintextResponseBuffer),
                PlaintextCrc32C = Crc32C.Compute(plaintextResponseBuffer)
            });
        var provider = new GcpKmsProvider(client);

        var encrypted = await provider.WrapKeyAsync(new byte[] { 1, 2, 3 }, CreateKeyReference());
        var decrypted = await provider.UnwrapKeyAsync(encrypted, CreateKeyReference());

        await Assert.That(encrypted).IsEquivalentTo(new byte[] { 4, 5, 6 });
        await Assert.That(decrypted).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(encryptRequest!.Name).IsEqualTo(KeyResourceName);
        await Assert.That(encryptRequest.Plaintext.ToByteArray()).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(encryptRequest.PlaintextCrc32C).IsEqualTo(Crc32C.Compute([1, 2, 3]));
        await Assert.That(decryptRequest!.Name).IsEqualTo(KeyResourceName);
        await Assert.That(decryptRequest.Ciphertext.ToByteArray()).IsEquivalentTo(new byte[] { 4, 5, 6 });
        await Assert.That(decryptRequest.CiphertextCrc32C).IsEqualTo(Crc32C.Compute([4, 5, 6]));
        await Assert.That(ciphertextResponseBuffer).IsEquivalentTo(new byte[] { 0, 0, 0 });
        await Assert.That(plaintextResponseBuffer).IsEquivalentTo(new byte[] { 0, 0, 0 });
        await client.Received(1).EncryptAsync(
            Arg.Any<EncryptRequest>(),
            Arg.Any<CancellationToken>());
        await client.Received(1).DecryptAsync(
            Arg.Any<DecryptRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(KeyResourceName)]
    [Arguments(GcpKmsProvider.KeyUriPrefix + KeyResourceName)]
    public async Task KeyReference_AcceptsRawResourceOrProviderUri(string keyId)
    {
        var client = Substitute.For<KeyManagementServiceClient>();
        client.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EncryptResponse
            {
                Name = KeyVersionResourceName,
                Ciphertext = ByteString.CopyFrom([9]),
                CiphertextCrc32C = Crc32C.Compute([9]),
                VerifiedPlaintextCrc32C = true
            });
        var provider = new GcpKmsProvider(client);

        await provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(keyId));

        await client.Received(1).EncryptAsync(
            Arg.Is<EncryptRequest>(request => request.Name == KeyResourceName),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Wrap_UnverifiedRequestIntegrity_IsRejectedAndResponseIsCleared()
    {
        var client = Substitute.For<KeyManagementServiceClient>();
        var responseBuffer = new byte[] { 4, 5, 6 };
        client.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EncryptResponse
            {
                Name = KeyVersionResourceName,
                Ciphertext = UnsafeByteOperations.UnsafeWrap(responseBuffer),
                CiphertextCrc32C = Crc32C.Compute(responseBuffer),
                VerifiedPlaintextCrc32C = false
            });
        var provider = new GcpKmsProvider(client);

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.WrapKeyAsync(new byte[] { 1, 2, 3 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).Contains("request integrity");
        await Assert.That(responseBuffer).IsEquivalentTo(new byte[] { 0, 0, 0 });
    }

    [Test]
    [Arguments(null)]
    [Arguments(0L)]
    public async Task Wrap_InvalidResponseChecksum_IsRejectedAndResponseIsCleared(long? responseCrc32C)
    {
        var client = Substitute.For<KeyManagementServiceClient>();
        var responseBuffer = new byte[] { 4, 5, 6 };
        client.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EncryptResponse
            {
                Name = KeyVersionResourceName,
                Ciphertext = UnsafeByteOperations.UnsafeWrap(responseBuffer),
                CiphertextCrc32C = responseCrc32C,
                VerifiedPlaintextCrc32C = true
            });
        var provider = new GcpKmsProvider(client);

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.WrapKeyAsync(new byte[] { 1, 2, 3 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).Contains("Response integrity");
        await Assert.That(responseBuffer).IsEquivalentTo(new byte[] { 0, 0, 0 });
    }

    [Test]
    public async Task Wrap_UnexpectedKeyVersion_IsRejectedAndResponseIsCleared()
    {
        var client = Substitute.For<KeyManagementServiceClient>();
        var responseBuffer = new byte[] { 4, 5, 6 };
        client.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EncryptResponse
            {
                Name = "projects/other/locations/europe-west2/keyRings/orders/cryptoKeys/kek/cryptoKeyVersions/1",
                Ciphertext = UnsafeByteOperations.UnsafeWrap(responseBuffer),
                CiphertextCrc32C = Crc32C.Compute(responseBuffer),
                VerifiedPlaintextCrc32C = true
            });
        var provider = new GcpKmsProvider(client);

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.WrapKeyAsync(new byte[] { 1, 2, 3 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).Contains("unexpected key version");
        await Assert.That(responseBuffer).IsEquivalentTo(new byte[] { 0, 0, 0 });
    }

    [Test]
    public async Task Unwrap_CorruptResponse_IsRejectedAndResponseIsCleared()
    {
        var client = Substitute.For<KeyManagementServiceClient>();
        var responseBuffer = new byte[] { 1, 2, 3 };
        client.DecryptAsync(Arg.Any<DecryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DecryptResponse
            {
                Plaintext = UnsafeByteOperations.UnsafeWrap(responseBuffer),
                PlaintextCrc32C = 0
            });
        var provider = new GcpKmsProvider(client);

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.UnwrapKeyAsync(new byte[] { 4, 5, 6 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).Contains("Response integrity");
        await Assert.That(responseBuffer).IsEquivalentTo(new byte[] { 0, 0, 0 });
    }

    [Test]
    public async Task WrongKey_IsReportedWithoutProviderResponse()
    {
        var client = Substitute.For<KeyManagementServiceClient>();
        client.DecryptAsync(Arg.Any<DecryptRequest>(), Arg.Any<CancellationToken>())
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
        client.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EncryptResponse>(denied));
        var provider = new GcpKmsProvider(client);

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).IsEqualTo("Google Cloud KMS wrap failed.");
        await Assert.That(exception.Message).DoesNotContain("sensitive");
        await Assert.That(exception.InnerException).IsNull();
        await Assert.That(exception.ToString()).DoesNotContain("sensitive");
    }

    [Test]
    public async Task MalformedCiphertext_IsReportedWithoutProviderResponse()
    {
        var client = Substitute.For<KeyManagementServiceClient>();
        client.DecryptAsync(Arg.Any<DecryptRequest>(), Arg.Any<CancellationToken>())
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
        client.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(call.Arg<CancellationToken>()));
        var provider = new GcpKmsProvider(client);
        using var cancellation = new CancellationTokenSource();
        var operation = provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(), cancellation.Token).AsTask();

        cancellation.Cancel();

        await Assert.That(async () => await operation).Throws<OperationCanceledException>();
        await client.Received(1).EncryptAsync(
            Arg.Any<EncryptRequest>(),
            cancellation.Token);
    }

    [Test]
    public async Task RpcCancellation_IsTranslatedWhenCallerTokenIsCanceled()
    {
        var completion = new TaskCompletionSource<EncryptResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = Substitute.For<KeyManagementServiceClient>();
        client.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
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
        client.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => EchoAfterYieldAsync(call.Arg<EncryptRequest>()));
        var provider = new GcpKmsProvider(client);

        var operations = Enumerable.Range(1, 32)
            .Select(value => provider.WrapKeyAsync(new byte[] { (byte)value }, CreateKeyReference()).AsTask())
            .ToArray();
        var results = await Task.WhenAll(operations);

        for (var index = 0; index < results.Length; index++)
            await Assert.That(results[index]).IsEquivalentTo(new byte[] { (byte)(index + 1) });
        await client.Received(32).EncryptAsync(
            Arg.Any<EncryptRequest>(),
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
                Arg.Any<EncryptRequest>(),
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

    private static async Task<EncryptResponse> EchoAfterYieldAsync(EncryptRequest request)
    {
        await Task.Yield();
        return new EncryptResponse
        {
            Name = KeyVersionResourceName,
            Ciphertext = ByteString.CopyFrom(request.Plaintext.Span),
            CiphertextCrc32C = request.PlaintextCrc32C,
            VerifiedPlaintextCrc32C = true
        };
    }
}
