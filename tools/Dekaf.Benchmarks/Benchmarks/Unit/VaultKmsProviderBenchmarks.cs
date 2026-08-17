using System.Net;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Kms.Vault;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class VaultKmsProviderBenchmarks
{
    private static readonly Uri VaultAddress = new("https://vault.example:8200/");
    private readonly byte[] _ciphertext = "vault:v1:benchmark"u8.ToArray();
    private readonly byte[] _plaintext = new byte[32];
    private readonly SchemaRegistryKmsKeyReference _keyReference = new()
    {
        KmsType = VaultKmsProvider.DefaultType,
        KmsKeyId = "https://vault.example:8200/transit/keys/benchmark-kek"
    };
    private SynchronousTransitClient _client = null!;
    private VaultKmsProvider _provider = null!;

    [GlobalSetup]
    public void Setup()
    {
        _client = new SynchronousTransitClient(_ciphertext, _plaintext);
        _provider = new VaultKmsProvider(_client, VaultAddress, "benchmarks");
    }

    [Benchmark(Baseline = true)]
    public ValueTask<byte[]> DirectWrapAsync() =>
        _client.EncryptAsync(
            VaultAddress,
            "transit",
            "benchmark-kek",
            "benchmarks",
            _plaintext);

    [Benchmark]
    public ValueTask<byte[]> ProviderWrapAsync() =>
        _provider.WrapKeyAsync(_plaintext, _keyReference);

    [Benchmark]
    public ValueTask<byte[]> DirectUnwrapAsync() =>
        _client.DecryptAsync(
            VaultAddress,
            "transit",
            "benchmark-kek",
            "benchmarks",
            _ciphertext);

    [Benchmark]
    public ValueTask<byte[]> ProviderUnwrapAsync() =>
        _provider.UnwrapKeyAsync(_ciphertext, _keyReference);

    private sealed class SynchronousTransitClient(byte[] ciphertext, byte[] plaintext)
        : IVaultTransitClient
    {
        public ValueTask<byte[]> EncryptAsync(
            Uri vaultAddress,
            string mountPoint,
            string keyName,
            string? vaultNamespace,
            ReadOnlyMemory<byte> value,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ciphertext);

        public ValueTask<byte[]> DecryptAsync(
            Uri vaultAddress,
            string mountPoint,
            string keyName,
            string? vaultNamespace,
            ReadOnlyMemory<byte> value,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(plaintext);
    }
}

[MemoryDiagnoser]
public class VaultTransitHttpClientBenchmarks
{
    private static readonly Uri VaultAddress = new("https://vault.example:8200/");
    private static readonly byte[] Plaintext = new byte[32];
    private HttpClient _httpClient = null!;
    private VaultTransitHttpClient _client = null!;

    [GlobalSetup]
    public void Setup()
    {
        _httpClient = new HttpClient(new VaultResponseHandler());
        _client = new VaultTransitHttpClient(_httpClient, new VaultStaticTokenProvider("token"));
    }

    [GlobalCleanup]
    public void Cleanup() => _httpClient.Dispose();

    [Benchmark]
    public ValueTask<byte[]> EncryptAsync() =>
        _client.EncryptAsync(VaultAddress, "transit", "benchmark-kek", null, Plaintext);

    private sealed class VaultResponseHandler : HttpMessageHandler
    {
        private static readonly byte[] Response =
            "{\"data\":{\"ciphertext\":\"vault:v1:benchmark\"}}"u8.ToArray();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Response)
            });
    }
}
