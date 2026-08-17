using System.Text;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Kms.Azure;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 10)]
public class AzureKeyVaultKmsProviderBenchmarks
{
    private const string KeyUri = "https://payments.vault.azure.net/keys/kek";
    private const string KeyVersion = "0123456789abcdef0123456789abcdef";

    private readonly byte[] _keyMaterial = [1, 2, 3];
    private readonly byte[] _versionlessEncryptedKeyMaterial = Encoding.ASCII.GetBytes("wrapped");
    private readonly byte[] _versionedEncryptedKeyMaterial = Encoding.ASCII.GetBytes($"azure:v1:{KeyVersion}:wrapped");
    private readonly SchemaRegistryKmsKeyReference _keyReference = new()
    {
        KmsType = AzureKeyVaultKmsProvider.DefaultType,
        KmsKeyId = KeyUri
    };
    private readonly SchemaRegistryKmsKeyReference _versionedKeyReference = new()
    {
        KmsType = AzureKeyVaultKmsProvider.DefaultType,
        KmsKeyId = KeyUri,
        KmsProps = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AzureKeyVaultKmsProvider.SaveVersionProperty] = bool.TrueString
        }
    };
    private readonly AzureKeyVaultKmsProvider _provider = new(new ClientFactory());

    [Benchmark]
    public ValueTask<byte[]> WrapVersionlessKey() =>
        _provider.WrapKeyAsync(_keyMaterial, _keyReference);

    [Benchmark]
    public ValueTask<byte[]> WrapVersionedKey() =>
        _provider.WrapKeyAsync(_keyMaterial, _versionedKeyReference);

    [Benchmark]
    public ValueTask<byte[]> UnwrapVersionlessKey() =>
        _provider.UnwrapKeyAsync(_versionlessEncryptedKeyMaterial, _keyReference);

    [Benchmark]
    public ValueTask<byte[]> UnwrapVersionedKey() =>
        _provider.UnwrapKeyAsync(_versionedEncryptedKeyMaterial, _keyReference);

    private sealed class ClientFactory : IAzureKeyVaultCryptographyClientFactory
    {
        private readonly CryptographyClient _client = new SuccessfulClient();

        public CryptographyClient CreateClient(Uri keyId) => _client;
    }

    private sealed class SuccessfulClient : CryptographyClient
    {
        private readonly Task<WrapResult> _wrapResult = Task.FromResult(CryptographyModelFactory.WrapResult(
            keyId: $"{KeyUri}/{KeyVersion}",
            key: [4, 5, 6],
            algorithm: KeyWrapAlgorithm.RsaOaep256));
        private readonly Task<UnwrapResult> _unwrapResult = Task.FromResult(CryptographyModelFactory.UnwrapResult(
            keyId: $"{KeyUri}/{KeyVersion}",
            key: [1, 2, 3],
            algorithm: KeyWrapAlgorithm.RsaOaep256));

        public override Task<WrapResult> WrapKeyAsync(
            KeyWrapAlgorithm algorithm,
            byte[] key,
            CancellationToken cancellationToken = default) => _wrapResult;

        public override Task<UnwrapResult> UnwrapKeyAsync(
            KeyWrapAlgorithm algorithm,
            byte[] ciphertext,
            CancellationToken cancellationToken = default) => _unwrapResult;
    }
}
