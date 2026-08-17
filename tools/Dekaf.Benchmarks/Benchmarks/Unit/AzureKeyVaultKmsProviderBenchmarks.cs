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

    private readonly byte[] _encryptedKeyMaterial = Encoding.ASCII.GetBytes($"azure:v1:{KeyVersion}:wrapped");
    private readonly SchemaRegistryKmsKeyReference _keyReference = new()
    {
        KmsType = AzureKeyVaultKmsProvider.DefaultType,
        KmsKeyId = KeyUri
    };
    private readonly AzureKeyVaultKmsProvider _provider = new(new ClientFactory());

    [Benchmark]
    public ValueTask<byte[]> UnwrapVersionedKey() =>
        _provider.UnwrapKeyAsync(_encryptedKeyMaterial, _keyReference);

    private sealed class ClientFactory : IAzureKeyVaultCryptographyClientFactory
    {
        private readonly CryptographyClient _client = new SuccessfulClient();

        public CryptographyClient CreateClient(Uri keyId) => _client;
    }

    private sealed class SuccessfulClient : CryptographyClient
    {
        private readonly Task<DecryptResult> _result = Task.FromResult(CryptographyModelFactory.DecryptResult(
            keyId: $"{KeyUri}/{KeyVersion}",
            plaintext: [1, 2, 3],
            algorithm: EncryptionAlgorithm.RsaOaep256));

        public override Task<DecryptResult> DecryptAsync(
            EncryptionAlgorithm algorithm,
            byte[] ciphertext,
            CancellationToken cancellationToken = default) => _result;
    }
}
