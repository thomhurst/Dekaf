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
        client.EncryptAsync(
                EncryptionAlgorithm.RsaOaep256,
                Arg.Do<byte[]>(value => plaintext = value),
                Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.EncryptResult(
                keyId: VersionedKeyUri,
                ciphertext: [4, 5, 6],
                algorithm: EncryptionAlgorithm.RsaOaep256));
        client.DecryptAsync(
                EncryptionAlgorithm.RsaOaep256,
                Arg.Do<byte[]>(value => ciphertext = value),
                Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.DecryptResult(
                keyId: VersionedKeyUri,
                plaintext: [1, 2, 3],
                algorithm: EncryptionAlgorithm.RsaOaep256));
        var factory = new RecordingFactory(_ => client);
        var provider = new AzureKeyVaultKmsProvider(factory);

        var encrypted = await provider.WrapKeyAsync(new byte[] { 1, 2, 3 }, CreateKeyReference());
        var decrypted = await provider.UnwrapKeyAsync(encrypted, CreateKeyReference());

        await Assert.That(encrypted).IsEquivalentTo(new byte[] { 4, 5, 6 });
        await Assert.That(decrypted).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(plaintext).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(ciphertext).IsEquivalentTo(new byte[] { 4, 5, 6 });
        await Assert.That(factory.CreatedKeyIds).IsEquivalentTo(new[] { new Uri(KeyUri) });
    }

    [Test]
    [Arguments(AzureKeyVaultKmsProvider.KeyUriPrefix, AzureKeyVaultKmsProvider.DefaultType)]
    [Arguments(AzureKeyVaultKmsProvider.ConfluentKeyUriPrefix, AzureKeyVaultKmsProvider.ConfluentType)]
    public async Task ProviderKeyPrefix_IsStripped(string prefix, string type)
    {
        var client = CreateClient(KeyUri);
        client.EncryptAsync(Arg.Any<EncryptionAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.EncryptResult(
                keyId: VersionedKeyUri,
                ciphertext: [9],
                algorithm: EncryptionAlgorithm.RsaOaep256));
        var factory = new RecordingFactory(_ => client);
        var provider = new AzureKeyVaultKmsProvider(factory, type);

        await provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(prefix + KeyUri, type));

        await Assert.That(factory.CreatedKeyIds).IsEquivalentTo(new[] { new Uri(KeyUri) });
    }

    [Test]
    public async Task SaveVersion_EmbedsVersionAndTargetsItDuringUnwrap()
    {
        var versionlessClient = CreateClient(KeyUri);
        versionlessClient.EncryptAsync(Arg.Any<EncryptionAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.EncryptResult(
                keyId: VersionedKeyUri,
                ciphertext: [4, 5, 6],
                algorithm: EncryptionAlgorithm.RsaOaep256));
        var versionedClient = CreateClient(VersionedKeyUri);
        versionedClient.DecryptAsync(Arg.Any<EncryptionAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.DecryptResult(
                keyId: VersionedKeyUri,
                plaintext: [1, 2, 3],
                algorithm: EncryptionAlgorithm.RsaOaep256));
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
        client.DecryptAsync(Arg.Any<EncryptionAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DecryptResult>(new RequestFailedException(404, "wrong key: sensitive")));
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
        client.EncryptAsync(Arg.Any<EncryptionAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EncryptResult>(denied));
        var provider = new AzureKeyVaultKmsProvider(new RecordingFactory(_ => client));

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).IsEqualTo("Azure Key Vault wrap failed.");
        await Assert.That(exception.Message).DoesNotContain("sensitive");
        await Assert.That(exception.InnerException).IsNull();
        await Assert.That(exception.ToString()).DoesNotContain("sensitive");
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
            .DecryptAsync(default, default!, default);
    }

    [Test]
    public async Task MalformedCiphertext_IsReportedWithoutProviderResponse()
    {
        var client = CreateClient(KeyUri);
        client.DecryptAsync(Arg.Any<EncryptionAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DecryptResult>(new RequestFailedException(400, "ciphertext: sensitive")));
        var provider = new AzureKeyVaultKmsProvider(new RecordingFactory(_ => client));

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.UnwrapKeyAsync(new byte[] { 1 }, CreateKeyReference()).AsTask());

        await Assert.That(exception!.Message).IsEqualTo("Azure Key Vault unwrap failed.");
        await Assert.That(exception.Message).DoesNotContain("sensitive");
    }

    [Test]
    public async Task Cancellation_IsPropagatedToInFlightAzureCall()
    {
        var client = CreateClient(KeyUri);
        client.EncryptAsync(Arg.Any<EncryptionAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(call.Arg<CancellationToken>()));
        var provider = new AzureKeyVaultKmsProvider(new RecordingFactory(_ => client));
        using var cancellation = new CancellationTokenSource();
        var operation = provider.WrapKeyAsync(new byte[] { 1 }, CreateKeyReference(), cancellation.Token).AsTask();

        cancellation.Cancel();

        await Assert.That(async () => await operation).Throws<OperationCanceledException>();
        await client.Received(1)
            .EncryptAsync(EncryptionAlgorithm.RsaOaep256, Arg.Any<byte[]>(), cancellation.Token);
    }

    [Test]
    public async Task SharedProvider_CreatesOneClientPerKey()
    {
        var client = CreateClient(KeyUri);
        client.EncryptAsync(Arg.Any<EncryptionAlgorithm>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
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
    [Arguments("https://payments.vault.azure.net/secrets/not-a-key")]
    [Arguments("http://payments.vault.azure.net/keys/kek")]
    public async Task InvalidKeyUri_IsRejectedBeforeClientCreation(string keyUri)
    {
        var factory = new RecordingFactory(_ => CreateClient(KeyUri));
        var provider = new AzureKeyVaultKmsProvider(factory);
        var reference = CreateKeyReference(keyUri);

        await Assert.That(async () => await provider.WrapKeyAsync(new byte[] { 1 }, reference))
            .Throws<SchemaRegistryKmsException>();
        await Assert.That(factory.CreateCount).IsEqualTo(0);
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

    private static async Task<EncryptResult> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("Cancellation was not observed.");
    }

    private static async Task<EncryptResult> EchoAfterYieldAsync(byte[] plaintext)
    {
        await Task.Yield();
        return CryptographyModelFactory.EncryptResult(
            keyId: VersionedKeyUri,
            ciphertext: plaintext.ToArray(),
            algorithm: EncryptionAlgorithm.RsaOaep256);
    }

    private sealed class RecordingFactory(Func<Uri, CryptographyClient> create) : IAzureKeyVaultCryptographyClientFactory
    {
        private int _createCount;

        internal ConcurrentBag<Uri> CreatedKeyIds { get; } = [];

        internal int CreateCount => Volatile.Read(ref _createCount);

        public CryptographyClient CreateClient(Uri keyId)
        {
            Interlocked.Increment(ref _createCount);
            CreatedKeyIds.Add(keyId);
            return create(keyId);
        }
    }
}
