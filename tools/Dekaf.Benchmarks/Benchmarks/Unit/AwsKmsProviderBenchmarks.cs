using Amazon;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Amazon.Runtime;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Kms.Aws;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 10)]
public class AwsKmsProviderBenchmarks
{
    private readonly byte[] _encryptedKeyMaterial = [4, 5, 6];
    private readonly SchemaRegistryKmsKeyReference _keyReference = new()
    {
        KmsType = AwsKmsProvider.DefaultType,
        KmsKeyId = "arn:aws:kms:eu-west-2:123456789012:key/1234"
    };
    private readonly AwsKmsProvider _provider = new(new SuccessfulClient(), ownsClient: true);

    [Benchmark]
    public ValueTask<byte[]> UnwrapNonExposableResponse() =>
        _provider.UnwrapKeyAsync(_encryptedKeyMaterial, _keyReference);

    [GlobalCleanup]
    public void Cleanup() => _provider.Dispose();

    private sealed class SuccessfulClient()
        : AmazonKeyManagementServiceClient(new AnonymousAWSCredentials(), RegionEndpoint.EUWest2)
    {
        public override Task<DecryptResponse> DecryptAsync(
            DecryptRequest request,
            CancellationToken cancellationToken = default)
        {
            var plaintext = new byte[] { 1, 2, 3 };
            return Task.FromResult(new DecryptResponse { Plaintext = new MemoryStream(plaintext) });
        }
    }
}
