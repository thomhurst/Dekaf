using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Metadata;
using Dekaf.Protocol;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class ApiVersionNegotiationBenchmarks
{
    private MetadataManager _metadataManager = null!;

    [GlobalSetup]
    public void Setup()
    {
        _metadataManager = new MetadataManager(
            connectionPool: null!,
            bootstrapServers: ["localhost:9092"]);
        _metadataManager.SetApiVersion(ApiKey.Fetch, 0, 18);

        _ = _metadataManager.GetNegotiatedApiVersion(ApiKey.Fetch, 4, 17);
    }

    [GlobalCleanup]
    public async Task Cleanup() => await _metadataManager.DisposeAsync().ConfigureAwait(false);

    [Benchmark]
    public short GetNegotiatedApiVersion() =>
        _metadataManager.GetNegotiatedApiVersion(ApiKey.Fetch, 4, 17);
}
