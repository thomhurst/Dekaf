using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Amazon.Runtime;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Kms.Aws;
using NSubstitute;
using System.Net;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public class AwsKmsProviderTests
{
    private const string KeyArn = "arn:aws:kms:eu-west-2:123456789012:key/1234";

    [Test]
    public async Task WrapAndUnwrap_UseAwsKmsEnvelopeOperations()
    {
        var client = Substitute.For<IAmazonKeyManagementService>();
        EncryptRequest? encryptRequest = null;
        DecryptRequest? decryptRequest = null;
        client.EncryptAsync(Arg.Do<EncryptRequest>(request => encryptRequest = request), Arg.Any<CancellationToken>())
            .Returns(new EncryptResponse { CiphertextBlob = new MemoryStream([4, 5, 6]) });
        client.DecryptAsync(Arg.Do<DecryptRequest>(request => decryptRequest = request), Arg.Any<CancellationToken>())
            .Returns(new DecryptResponse { Plaintext = new MemoryStream([1, 2, 3]) });
        using var provider = new AwsKmsProvider(client);
        var keyReference = CreateKeyReference();

        var encrypted = await provider.WrapKeyAsync(new byte[] { 1, 2, 3 }, keyReference);
        var decrypted = await provider.UnwrapKeyAsync(encrypted, keyReference);

        await Assert.That(encrypted).IsEquivalentTo(new byte[] { 4, 5, 6 });
        await Assert.That(decrypted).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(encryptRequest!.KeyId).IsEqualTo(KeyArn);
        await Assert.That(ReadAll(encryptRequest.Plaintext)).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(decryptRequest!.KeyId).IsEqualTo(KeyArn);
        await Assert.That(ReadAll(decryptRequest.CiphertextBlob)).IsEquivalentTo(new byte[] { 4, 5, 6 });
    }

    [Test]
    public async Task ConfluentKeyUri_StripsProviderPrefix()
    {
        var client = Substitute.For<IAmazonKeyManagementService>();
        EncryptRequest? captured = null;
        client.EncryptAsync(Arg.Do<EncryptRequest>(request => captured = request), Arg.Any<CancellationToken>())
            .Returns(new EncryptResponse { CiphertextBlob = new MemoryStream([9]) });
        using var provider = new AwsKmsProvider(client);

        await provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(AwsKmsProvider.KeyUriPrefix + KeyArn));

        await Assert.That(captured!.KeyId).IsEqualTo(KeyArn);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task AwsFailure_IsSanitizedWithoutSensitiveCause(bool wrap)
    {
        var client = Substitute.For<IAmazonKeyManagementService>();
        var failure = new AmazonKeyManagementServiceException("secret provider response");
        client.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<EncryptResponse>(failure));
        client.DecryptAsync(Arg.Any<DecryptRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<DecryptResponse>(failure));
        using var provider = new AwsKmsProvider(client);

        var exception = wrap
            ? await Assert.ThrowsAsync<SchemaRegistryKmsException>(() => provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference()).AsTask())
            : await Assert.ThrowsAsync<SchemaRegistryKmsException>(() => provider.UnwrapKeyAsync(new byte[] { 1 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).IsEqualTo(wrap ? "AWS KMS wrap failed." : "AWS KMS unwrap failed.");
        await Assert.That(exception.InnerException).IsNull();
        await Assert.That(exception.ToString()).DoesNotContain("secret provider response");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task AwsClientFailure_IsSanitizedWithoutSensitiveCause(bool wrap)
    {
        var client = Substitute.For<IAmazonKeyManagementService>();
        var failure = new AmazonClientException("credential provider response: sensitive");
        client.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EncryptResponse>(failure));
        client.DecryptAsync(Arg.Any<DecryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DecryptResponse>(failure));
        using var provider = new AwsKmsProvider(client);

        var exception = wrap
            ? await Assert.ThrowsAsync<SchemaRegistryKmsException>(
                () => provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference()).AsTask())
            : await Assert.ThrowsAsync<SchemaRegistryKmsException>(
                () => provider.UnwrapKeyAsync(new byte[] { 1 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message)
            .IsEqualTo(wrap ? "AWS KMS wrap failed." : "AWS KMS unwrap failed.");
        await Assert.That(exception.Message).DoesNotContain("sensitive");
        await Assert.That(exception.InnerException).IsNull();
        await Assert.That(exception.ToString()).DoesNotContain("sensitive");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task TransportFailure_IsSanitizedWithoutSensitiveCause(bool wrap)
    {
        var client = Substitute.For<IAmazonKeyManagementService>();
        Exception failure = wrap
            ? new HttpRequestException("transport response: sensitive")
            : new TimeoutException("timeout response: sensitive");
        client.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EncryptResponse>(failure));
        client.DecryptAsync(Arg.Any<DecryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DecryptResponse>(failure));
        using var provider = new AwsKmsProvider(client);

        var exception = wrap
            ? await Assert.ThrowsAsync<SchemaRegistryKmsException>(
                () => provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference()).AsTask())
            : await Assert.ThrowsAsync<SchemaRegistryKmsException>(
                () => provider.UnwrapKeyAsync(new byte[] { 1 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message)
            .IsEqualTo(wrap ? "AWS KMS wrap failed." : "AWS KMS unwrap failed.");
        await Assert.That(exception.InnerException).IsNull();
        await Assert.That(exception.ToString()).DoesNotContain("sensitive");
    }

    [Test]
    public async Task MalformedCiphertext_IsReportedWithoutMaterial()
    {
        var client = Substitute.For<IAmazonKeyManagementService>();
        client.DecryptAsync(Arg.Any<DecryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DecryptResponse>(new InvalidCiphertextException("ciphertext: sensitive")));
        using var provider = new AwsKmsProvider(client);

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.UnwrapKeyAsync(new byte[] { 1, 2, 3 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).IsEqualTo("AWS KMS unwrap failed.");
        await Assert.That(exception.Message).DoesNotContain("sensitive");
    }

    [Test]
    public async Task Unwrap_ZeroesNonExposablePlaintextStream()
    {
        var plaintext = new byte[] { 1, 2, 3 };
        var stream = new MemoryStream(plaintext);
        var client = Substitute.For<IAmazonKeyManagementService>();
        client.DecryptAsync(Arg.Any<DecryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DecryptResponse { Plaintext = stream });
        using var provider = new AwsKmsProvider(client);

        var unwrapped = await provider.UnwrapKeyAsync(new byte[] { 4, 5, 6 }, CreateKeyReference());

        await Assert.That(stream.TryGetBuffer(out _)).IsFalse();
        await Assert.That(unwrapped).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(plaintext).IsEquivalentTo(new byte[] { 0, 0, 0 });
    }

    [Test]
    public async Task WrongKey_IsReportedWithoutKeyMaterial()
    {
        var client = Substitute.For<IAmazonKeyManagementService>();
        client.DecryptAsync(Arg.Any<DecryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DecryptResponse>(new IncorrectKeyException("wrong key: sensitive")));
        using var provider = new AwsKmsProvider(client);

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.UnwrapKeyAsync(new byte[] { 1, 2, 3 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).IsEqualTo("AWS KMS unwrap failed.");
        await Assert.That(exception.Message).DoesNotContain("sensitive");
    }

    [Test]
    public async Task AuthorizationFailure_IsReportedWithoutProviderResponse()
    {
        var client = Substitute.For<IAmazonKeyManagementService>();
        var denied = new AmazonKeyManagementServiceException(
            "access response: sensitive",
            ErrorType.Sender,
            "AccessDeniedException",
            "request-id",
            HttpStatusCode.Forbidden);
        client.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EncryptResponse>(denied));
        using var provider = new AwsKmsProvider(client);

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).IsEqualTo("AWS KMS wrap failed.");
        await Assert.That(exception.Message).DoesNotContain("sensitive");
        await Assert.That(exception.InnerException).IsNull();
        await Assert.That(exception.ToString()).DoesNotContain("sensitive");
    }

    [Test]
    public async Task Cancellation_IsPropagatedToInFlightAwsCall()
    {
        var client = Substitute.For<IAmazonKeyManagementService>();
        using var cancellation = new CancellationTokenSource();
        client.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(call.Arg<CancellationToken>()));
        using var provider = new AwsKmsProvider(client);
        var operation = provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(), cancellation.Token).AsTask();

        cancellation.Cancel();

        await Assert.That(async () => await operation)
            .Throws<OperationCanceledException>();
        await client.Received(1).EncryptAsync(Arg.Any<EncryptRequest>(), cancellation.Token);
    }

    [Test]
    public async Task SharedProvider_IsThreadSafe()
    {
        var client = Substitute.For<IAmazonKeyManagementService>();
        client.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => EchoAfterYieldAsync(call.Arg<EncryptRequest>()));
        using var provider = new AwsKmsProvider(client);

        var operations = Enumerable.Range(1, 32)
            .Select(value => provider.WrapKeyAsync(new byte[] { (byte)value }, CreateKeyReference()).AsTask())
            .ToArray();
        var results = await Task.WhenAll(operations);

        for (var index = 0; index < results.Length; index++)
            await Assert.That(results[index]).IsEquivalentTo(new byte[] { (byte)(index + 1) });
    }

    [Test]
    public async Task OwnedClient_IsDisposedWithProvider()
    {
        var client = Substitute.For<IAmazonKeyManagementService>();
        using var provider = new AwsKmsProvider(client, ownsClient: true);

        provider.Dispose();
        provider.Dispose();

        client.Received(1).Dispose();
        await Assert.That(async () => await provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference()))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task InvalidReference_IsRejectedBeforeAwsCall()
    {
        var client = Substitute.For<IAmazonKeyManagementService>();
        using var provider = new AwsKmsProvider(client);
        var reference = new SchemaRegistryKmsKeyReference
        {
            KmsType = "gcp-kms",
            KmsKeyId = KeyArn
        };

        await Assert.That(async () => await provider.WrapKeyAsync(new byte[] { 1 }, reference))
            .Throws<SchemaRegistryKmsException>();
        await client.DidNotReceiveWithAnyArgs().EncryptAsync(default!, default);
    }

    [Test]
    public async Task CustomType_ResolvesMatchingRegionalProvider()
    {
        const string regionalType = "aws-kms-eu-west-2";
        var client = Substitute.For<IAmazonKeyManagementService>();
        client.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EncryptResponse { CiphertextBlob = new MemoryStream([9]) });
        using var provider = new AwsKmsProvider(client, type: regionalType);

        var encrypted = await provider.WrapKeyAsync(
            new byte[] { 1 },
            CreateKeyReference(KeyArn, regionalType));

        await Assert.That(provider.Type).IsEqualTo(regionalType);
        await Assert.That(encrypted).IsEquivalentTo(new byte[] { 9 });
        await client.Received(1).EncryptAsync(
            Arg.Is<EncryptRequest>(request => request.KeyId == KeyArn),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EmptyCustomType_IsRejected()
    {
        var client = Substitute.For<IAmazonKeyManagementService>();

        await Assert.That(() => new AwsKmsProvider(client, type: " "))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task EmptyKeyIdentifier_IsRejectedBeforeAwsCall()
    {
        var client = Substitute.For<IAmazonKeyManagementService>();
        using var provider = new AwsKmsProvider(client);
        var reference = new SchemaRegistryKmsKeyReference
        {
            KmsType = AwsKmsProvider.DefaultType,
            KmsKeyId = null!
        };

        await Assert.That(async () => await provider.WrapKeyAsync(new byte[] { 1 }, reference))
            .Throws<SchemaRegistryKmsException>();
        await client.DidNotReceiveWithAnyArgs().EncryptAsync(default!, default);
    }

    private static SchemaRegistryKmsKeyReference CreateKeyReference(
        string keyId = KeyArn,
        string type = AwsKmsProvider.DefaultType) => new()
    {
        KmsType = type,
        KmsKeyId = keyId
    };

    private static byte[] ReadAll(MemoryStream stream) => stream.ToArray();

    private static async Task<EncryptResponse> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("Cancellation was not observed.");
    }

    private static async Task<EncryptResponse> EchoAfterYieldAsync(EncryptRequest request)
    {
        await Task.Yield();
        return new EncryptResponse { CiphertextBlob = new MemoryStream(ReadAll(request.Plaintext)) };
    }
}
