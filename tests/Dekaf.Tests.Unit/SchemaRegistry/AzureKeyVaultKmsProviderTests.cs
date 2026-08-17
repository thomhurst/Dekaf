using System.Collections.Concurrent;
using System.Text;
using Azure;
using Azure.Core;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Kms.Azure;
using NSubstitute;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public class AzureKeyVaultKmsProviderTests
{
    private const string KeyUri = "https://payments.vault.azure.net/keys/kek";
    private const string KeyVersion = "0123456789abcdef0123456789abcdef";
    private const string VersionedKeyUri = KeyUri + "/" + KeyVersion;

    [Test]
    public async Task WrapAndUnwrap_UseRsaOaep256()
    {
        var client = CreateClient(KeyUri);
        byte[]? plaintext = null;
        byte[]? ciphertext = null;
        client.WrapKeyAsync(
                KeyWrapAlgorithm.RsaOaep256,
                Arg.Do<byte[]>(value => plaintext = value),
                Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.WrapResult(
                keyId: VersionedKeyUri,
                key: [4, 5, 6],
                algorithm: KeyWrapAlgorithm.RsaOaep256));
        client.UnwrapKeyAsync(
                KeyWrapAlgorithm.RsaOaep256,
                Arg.Do<byte[]>(value => ciphertext = value),
                Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.UnwrapResult(
                keyId: VersionedKeyUri,
                key: [1, 2, 3],
                algorithm: KeyWrapAlgorithm.RsaOaep256));
        var factory = new RecordingFactory(_ => client);
        var provider = new AzureKeyVaultKmsProvider(factory);

        var encrypted = await provider.WrapKeyAsync(new byte[] { 1, 2, 3 }, CreateKeyReference());
        var decrypted = await provider.UnwrapKeyAsync(encrypted, CreateKeyReference());

        await Assert.That(encrypted).IsEquivalentTo(new byte[] { 4, 5, 6 });
        await Assert.That(decrypted).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(plaintext).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(ciphertext).IsEquivalentTo(new byte[] { 4, 5, 6 });
        await Assert.That(factory.CreatedKeyIds).IsEquivalentTo(new[] { new Uri(KeyUri) });
        await client.Received(1).WrapKeyAsync(
            KeyWrapAlgorithm.RsaOaep256,
            Arg.Any<byte[]>(),
            Arg.Any<CancellationToken>());
        await client.Received(1).UnwrapKeyAsync(
            KeyWrapAlgorithm.RsaOaep256,
            Arg.Any<byte[]>(),
            Arg.Any<CancellationToken>());
        await client.DidNotReceiveWithAnyArgs().EncryptAsync(default, default!, default);
        await client.DidNotReceiveWithAnyArgs().DecryptAsync(default, default!, default);
    }

    [Test]
    [Arguments(AzureKeyVaultKmsProvider.KeyUriPrefix, AzureKeyVaultKmsProvider.DefaultType)]
    [Arguments(AzureKeyVaultKmsProvider.ConfluentKeyUriPrefix, AzureKeyVaultKmsProvider.ConfluentType)]
    public async Task ProviderKeyPrefix_IsStripped(string prefix, string type)
    {
        var client = CreateClient(KeyUri);
        client.WrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.WrapResult(
                keyId: VersionedKeyUri,
                key: [9],
                algorithm: KeyWrapAlgorithm.RsaOaep256));
        var factory = new RecordingFactory(_ => client);
        var provider = new AzureKeyVaultKmsProvider(factory, type);

        await provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(prefix + KeyUri, type));

        await Assert.That(factory.CreatedKeyIds).IsEquivalentTo(new[] { new Uri(KeyUri) });
    }

    [Test]
    public async Task SaveVersion_EmbedsVersionAndTargetsItDuringUnwrap()
    {
        var versionlessClient = CreateClient(KeyUri);
        versionlessClient.WrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.WrapResult(
                keyId: VersionedKeyUri,
                key: [4, 5, 6],
                algorithm: KeyWrapAlgorithm.RsaOaep256));
        var versionedClient = CreateClient(VersionedKeyUri);
        versionedClient.UnwrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.UnwrapResult(
                keyId: VersionedKeyUri,
                key: [1, 2, 3],
                algorithm: KeyWrapAlgorithm.RsaOaep256));
        var factory = new RecordingFactory(uri =>
            uri.AbsoluteUri == new Uri(VersionedKeyUri).AbsoluteUri ? versionedClient : versionlessClient);
        var provider = new AzureKeyVaultKmsProvider(factory);
        var keyReference = CreateKeyReference(saveVersion: true);

        var encrypted = await provider.WrapKeyAsync(new byte[] { 1, 2, 3 }, keyReference);
        var decrypted = await provider.UnwrapKeyAsync(encrypted, keyReference);

        var expectedHeader = Encoding.ASCII.GetBytes($"azure:v1:{KeyVersion}:");
        await Assert.That(encrypted.AsSpan(0, expectedHeader.Length).ToArray()).IsEquivalentTo(expectedHeader);
        await Assert.That(encrypted.AsSpan(expectedHeader.Length).ToArray()).IsEquivalentTo(new byte[] { 4, 5, 6 });
        await Assert.That(decrypted).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(factory.CreatedKeyIds).Contains(new Uri(KeyUri));
        await Assert.That(factory.CreatedKeyIds).Contains(new Uri(VersionedKeyUri));
    }

    [Test]
    public async Task WrongKey_IsReportedWithoutProviderResponse()
    {
        var client = CreateClient(KeyUri);
        client.UnwrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<UnwrapResult>(new RequestFailedException(404, "wrong key: sensitive")));
        var provider = new AzureKeyVaultKmsProvider(new RecordingFactory(_ => client));

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.UnwrapKeyAsync(new byte[] { 1 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).IsEqualTo("Azure Key Vault unwrap failed.");
        await Assert.That(exception.Message).DoesNotContain("sensitive");
    }

    [Test]
    public async Task AuthorizationFailure_IsReportedWithoutProviderResponse()
    {
        var client = CreateClient(KeyUri);
        var denied = new RequestFailedException(403, "authorization: sensitive");
        client.WrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<WrapResult>(denied));
        var provider = new AzureKeyVaultKmsProvider(new RecordingFactory(_ => client));

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).IsEqualTo("Azure Key Vault wrap failed.");
        await Assert.That(exception.Message).DoesNotContain("sensitive");
        await Assert.That(exception.InnerException).IsNull();
        await Assert.That(exception.ToString()).DoesNotContain("sensitive");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AzureSdkCancellation_WithoutCallerCancellation_IsSanitized(bool unwrap)
    {
        var client = CreateClient(KeyUri);
        var cancellation = new OperationCanceledException("internal timeout: sensitive");
        client.WrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<WrapResult>(cancellation));
        client.UnwrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<UnwrapResult>(cancellation));
        var factory = new RecordingFactory(_ => client);
        var provider = new AzureKeyVaultKmsProvider(factory);
        var material = unwrap
            ? Encoding.ASCII.GetBytes($"azure:v1:{KeyVersion}:wrapped")
            : new byte[] { 1 };
        SchemaRegistryKmsException? exception = null;

        for (var pass = 0; pass < 2; pass++)
        {
            exception = unwrap
                ? await Assert.ThrowsAsync<SchemaRegistryKmsException>(
                    () => provider.UnwrapKeyAsync(material, CreateKeyReference()).AsTask())
                : await Assert.ThrowsAsync<SchemaRegistryKmsException>(
                    () => provider.WrapKeyAsync(material, CreateKeyReference()).AsTask());
        }

        await Assert.That(exception!.Message)
            .IsEqualTo(unwrap ? "Azure Key Vault unwrap failed." : "Azure Key Vault wrap failed.");
        await Assert.That(exception.ToString()).DoesNotContain("sensitive");
        await Assert.That(factory.CreateCount).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FactoryCancellation_WithCallerCancellation_IsPropagated(bool unwrap)
    {
        using var cancellation = new CancellationTokenSource();
        var factory = new RecordingFactory(_ =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });
        var provider = new AzureKeyVaultKmsProvider(factory);
        var material = unwrap
            ? Encoding.ASCII.GetBytes($"azure:v1:{KeyVersion}:wrapped")
            : new byte[] { 1 };

        var exception = unwrap
            ? await Assert.ThrowsAsync<OperationCanceledException>(
                () => provider.UnwrapKeyAsync(material, CreateKeyReference(), cancellation.Token).AsTask())
            : await Assert.ThrowsAsync<OperationCanceledException>(
                () => provider.WrapKeyAsync(material, CreateKeyReference(), cancellation.Token).AsTask());

        await Assert.That(exception!.CancellationToken).IsEqualTo(cancellation.Token);
        await Assert.That(factory.CreateCount).IsEqualTo(1);
    }

    [Test]
    public async Task MalformedVersionHeader_IsRejectedBeforeAzureCall()
    {
        var client = CreateClient(KeyUri);
        var factory = new RecordingFactory(_ => client);
        var provider = new AzureKeyVaultKmsProvider(factory);
        var malformed = Encoding.ASCII.GetBytes("azure:v1:short");

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.UnwrapKeyAsync(malformed, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).Contains("invalid version header");
        await client.DidNotReceiveWithAnyArgs()
            .UnwrapKeyAsync(default, default!, default);
    }

    [Test]
    public async Task MalformedCiphertext_IsReportedWithoutProviderResponse()
    {
        var client = CreateClient(KeyUri);
        client.UnwrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<UnwrapResult>(new RequestFailedException(400, "ciphertext: sensitive")));
        var provider = new AzureKeyVaultKmsProvider(new RecordingFactory(_ => client));

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.UnwrapKeyAsync(new byte[] { 1 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).IsEqualTo("Azure Key Vault unwrap failed.");
        await Assert.That(exception.Message).DoesNotContain("sensitive");
    }

    [Test]
    public async Task PermanentEmbeddedVersionFailures_AreNotRetained()
    {
        var client = CreateClient(KeyUri);
        client.UnwrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<UnwrapResult>(new RequestFailedException(400, "invalid ciphertext")));
        var factory = new RecordingFactory(_ => client);
        var provider = new AzureKeyVaultKmsProvider(factory);

        for (var pass = 0; pass < 2; pass++)
        {
            for (var index = 0; index < 32; index++)
            {
                var version = index.ToString("x32");
                var ciphertext = Encoding.ASCII.GetBytes($"azure:v1:{version}:wrapped");

                await Assert.That(async () => await provider.UnwrapKeyAsync(ciphertext, CreateKeyReference()))
                    .Throws<SchemaRegistryKmsException>();
            }
        }

        await Assert.That(factory.CreateCount).IsEqualTo(64);
    }

    [Test]
    [Arguments(408)]
    [Arguments(429)]
    [Arguments(500)]
    [Arguments(503)]
    public async Task TransientEmbeddedVersionFailures_RetainClient(int status)
    {
        var client = CreateClient(VersionedKeyUri);
        client.UnwrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<UnwrapResult>(new RequestFailedException(status, "transient")));
        var factory = new RecordingFactory(_ => client);
        var provider = new AzureKeyVaultKmsProvider(factory);
        var ciphertext = Encoding.ASCII.GetBytes($"azure:v1:{KeyVersion}:wrapped");

        for (var pass = 0; pass < 2; pass++)
        {
            await Assert.That(async () => await provider.UnwrapKeyAsync(ciphertext, CreateKeyReference()))
                .Throws<SchemaRegistryKmsException>();
        }

        await Assert.That(factory.CreateCount).IsEqualTo(1);
    }

    [Test]
    public async Task TransientEmbeddedVersionFailures_KeepClientCacheBounded()
    {
        var client = CreateClient(VersionedKeyUri);
        client.UnwrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<UnwrapResult>(new RequestFailedException(503, "Unavailable")));
        var factory = new RecordingFactory(_ => client);
        var provider = new AzureKeyVaultKmsProvider(factory);

        for (var index = 0; index < 1_024; index++)
        {
            var version = index.ToString("x32");
            var ciphertext = Encoding.ASCII.GetBytes($"azure:v1:{version}:wrapped");
            await Assert.That(async () => await provider.UnwrapKeyAsync(ciphertext, CreateKeyReference()))
                .Throws<SchemaRegistryKmsException>();
        }

        await Assert.That(provider.EmbeddedVersionClientCount)
            .IsLessThanOrEqualTo(AzureKeyVaultKmsProvider.EmbeddedVersionClientCapacity);
    }

    [Test]
    public async Task SameVersionAcrossKeys_RetainsBothEmbeddedVersionClients()
    {
        const string otherKeyUri = "https://orders.vault.azure.net/keys/kek";
        var client = CreateClient(VersionedKeyUri);
        client.UnwrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<UnwrapResult>(new RequestFailedException(503, "Unavailable")));
        var factory = new RecordingFactory(_ => client);
        var provider = new AzureKeyVaultKmsProvider(factory);
        var ciphertext = Encoding.ASCII.GetBytes($"azure:v1:{KeyVersion}:wrapped");

        for (var pass = 0; pass < 2; pass++)
        {
            await Assert.That(async () => await provider.UnwrapKeyAsync(ciphertext, CreateKeyReference()))
                .Throws<SchemaRegistryKmsException>();
            await Assert.That(async () => await provider.UnwrapKeyAsync(
                    ciphertext,
                    CreateKeyReference(otherKeyUri)))
                .Throws<SchemaRegistryKmsException>();
        }

        await Assert.That(factory.CreateCount).IsEqualTo(2);
    }

    [Test]
    public async Task CancelledEmbeddedVersion_RetainsClient()
    {
        var client = CreateClient(VersionedKeyUri);
        client.UnwrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForUnwrapCancellationAsync(call.Arg<CancellationToken>()));
        var factory = new RecordingFactory(_ => client);
        var provider = new AzureKeyVaultKmsProvider(factory);
        var ciphertext = Encoding.ASCII.GetBytes($"azure:v1:{KeyVersion}:wrapped");

        for (var pass = 0; pass < 2; pass++)
        {
            using var cancellation = new CancellationTokenSource();
            var operation = provider.UnwrapKeyAsync(ciphertext, CreateKeyReference(), cancellation.Token).AsTask();

            cancellation.Cancel();

            await Assert.That(async () => await operation).Throws<OperationCanceledException>();
        }

        await Assert.That(factory.CreateCount).IsEqualTo(1);
    }

    [Test]
    public async Task Cancellation_IsPropagatedToInFlightAzureCall()
    {
        var client = CreateClient(KeyUri);
        client.WrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(call.Arg<CancellationToken>()));
        var provider = new AzureKeyVaultKmsProvider(new RecordingFactory(_ => client));
        using var cancellation = new CancellationTokenSource();
        var operation = provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(), cancellation.Token).AsTask();

        cancellation.Cancel();

        await Assert.That(async () => await operation).Throws<OperationCanceledException>();
        await client.Received(1)
            .WrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, Arg.Any<byte[]>(), cancellation.Token);
    }

    [Test]
    public async Task WrapKey_FactoryFailure_IsRetried()
    {
        var client = CreateClient(KeyUri);
        client.WrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.WrapResult(
                keyId: VersionedKeyUri,
                key: [4, 5, 6],
                algorithm: KeyWrapAlgorithm.RsaOaep256));
        var factory = new RecordingFactory(_ => client);
        factory.FailNextCreation(new InvalidOperationException("transient factory failure"));
        var provider = new AzureKeyVaultKmsProvider(factory);

        await Assert.That(async () => await provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference()))
            .Throws<InvalidOperationException>();
        var encrypted = await provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference());

        await Assert.That(encrypted).IsEquivalentTo(new byte[] { 4, 5, 6 });
        await Assert.That(factory.CreateCount).IsEqualTo(2);
    }

    [Test]
    public async Task VersionlessUnwrap_FactoryFailure_IsRetried()
    {
        var client = CreateClient(KeyUri);
        client.UnwrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.UnwrapResult(
                keyId: VersionedKeyUri,
                key: [1, 2, 3],
                algorithm: KeyWrapAlgorithm.RsaOaep256));
        var factory = new RecordingFactory(_ => client);
        factory.FailNextCreation(new RequestFailedException(503, "Unavailable"));
        var provider = new AzureKeyVaultKmsProvider(factory);

        await Assert.That(async () => await provider.UnwrapKeyAsync(new byte[] { 4, 5, 6 }, CreateKeyReference()))
            .Throws<SchemaRegistryKmsException>();
        var plaintext = await provider.UnwrapKeyAsync(new byte[] { 4, 5, 6 }, CreateKeyReference());

        await Assert.That(plaintext).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(factory.CreateCount).IsEqualTo(2);
    }

    [Test]
    public async Task SharedProvider_CreatesOneClientPerKey()
    {
        var client = CreateClient(KeyUri);
        client.WrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(call => EchoAfterYieldAsync(call.Arg<byte[]>()));
        var factory = new RecordingFactory(_ => client);
        var provider = new AzureKeyVaultKmsProvider(factory);

        var operations = Enumerable.Range(1, 32)
            .Select(value => provider.WrapKeyAsync(new byte[] { (byte)value }, CreateKeyReference()).AsTask())
            .ToArray();
        var results = await Task.WhenAll(operations);

        await Assert.That(factory.CreateCount).IsEqualTo(1);
        for (var index = 0; index < results.Length; index++)
            await Assert.That(results[index]).IsEquivalentTo(new byte[] { (byte)(index + 1) });
    }

    [Test]
    public async Task SharedProvider_ConcurrentDistinctKeys_KeepsClientCacheBounded()
    {
        var client = CreateClient(KeyUri);
        client.WrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.WrapResult(
                keyId: VersionedKeyUri,
                key: [4, 5, 6],
                algorithm: KeyWrapAlgorithm.RsaOaep256));
        var factory = new RecordingFactory(_ => client);
        var provider = new AzureKeyVaultKmsProvider(factory);
        var operations = Enumerable.Range(0, 256)
            .Select(index => provider.WrapKeyAsync(
                new byte[] { 1 },
                CreateKeyReference($"https://vault{index}.vault.azure.net/keys/kek")).AsTask())
            .ToArray();

        await Task.WhenAll(operations);

        await Assert.That(provider.ClientCount).IsEqualTo(AzureKeyVaultKmsProvider.ClientCacheCapacity);
        await Assert.That(factory.CreateCount).IsEqualTo(operations.Length);
    }

    [Test]
    [Arguments("https://payments.vault.azure.net/secrets/not-a-key")]
    [Arguments("http://payments.vault.azure.net/keys/kek")]
    [Arguments("https://attacker.example/keys/kek")]
    [Arguments("https://payments.vault.azure.net.attacker.example/keys/kek")]
    [Arguments("https://payments.vault.azure.net:8443/keys/kek")]
    public async Task InvalidKeyUri_IsRejectedBeforeClientCreation(string keyUri)
    {
        var factory = new RecordingFactory(_ => CreateClient(KeyUri));
        var provider = new AzureKeyVaultKmsProvider(factory);
        var reference = CreateKeyReference(keyUri);

        await Assert.That(async () => await provider.WrapKeyAsync(new byte[] { 1 }, reference))
            .Throws<SchemaRegistryKmsException>();
        await Assert.That(factory.CreateCount).IsEqualTo(0);
    }

    [Test]
    [Arguments("https://payments.vault.azure.net/keys/kek")]
    [Arguments("https://payments.vault.azure.net:443/keys/kek")]
    [Arguments("https://payments.vault.azure.cn/keys/kek")]
    [Arguments("https://payments.vault.usgovcloudapi.net/keys/kek")]
    [Arguments("https://payments.managedhsm.azure.net/keys/kek")]
    [Arguments("https://payments.managedhsm.azure.cn/keys/kek")]
    [Arguments("https://payments.managedhsm.usgovcloudapi.net/keys/kek")]
    public async Task SupportedAzureVaultAuthority_IsAccepted(string keyUri)
    {
        var client = CreateClient(KeyUri);
        client.WrapKeyAsync(Arg.Any<KeyWrapAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.WrapResult(
                keyId: VersionedKeyUri,
                key: [9],
                algorithm: KeyWrapAlgorithm.RsaOaep256));
        var factory = new RecordingFactory(_ => client);
        var provider = new AzureKeyVaultKmsProvider(factory);

        _ = await provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(keyUri));

        await Assert.That(factory.CreateCount).IsEqualTo(1);
        await Assert.That(factory.CreatedKeyIds).Contains(new Uri(keyUri));
    }

    private static CryptographyClient CreateClient(string keyUri)
    {
        var credential = Substitute.For<TokenCredential>();
        return Substitute.For<CryptographyClient>(new Uri(keyUri), credential);
    }

    private static SchemaRegistryKmsKeyReference CreateKeyReference(
        string keyId = KeyUri,
        string type = AzureKeyVaultKmsProvider.DefaultType,
        bool saveVersion = false) => new()
    {
        KmsType = type,
        KmsKeyId = keyId,
        KmsProps = saveVersion
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [AzureKeyVaultKmsProvider.SaveVersionProperty] = bool.TrueString
            }
            : null
    };

    private static async Task<WrapResult> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("Cancellation was not observed.");
    }

    private static async Task<UnwrapResult> WaitForUnwrapCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("Cancellation was not observed.");
    }

    private static async Task<WrapResult> EchoAfterYieldAsync(byte[] plaintext)
    {
        await Task.Yield();
        return CryptographyModelFactory.WrapResult(
            keyId: VersionedKeyUri,
            key: plaintext.ToArray(),
            algorithm: KeyWrapAlgorithm.RsaOaep256);
    }

    private sealed class RecordingFactory(Func<Uri, CryptographyClient> create) : IAzureKeyVaultCryptographyClientFactory
    {
        private int _createCount;
        private Exception? _nextCreationException;

        internal ConcurrentBag<Uri> CreatedKeyIds { get; } = [];

        internal int CreateCount => Volatile.Read(ref _createCount);

        internal void FailNextCreation(Exception exception) =>
            Interlocked.Exchange(ref _nextCreationException, exception);

        public CryptographyClient CreateClient(Uri keyId)
        {
            Interlocked.Increment(ref _createCount);
            CreatedKeyIds.Add(keyId);
            if (Interlocked.Exchange(ref _nextCreationException, null) is { } exception)
                throw exception;

            return create(keyId);
        }
    }
}
