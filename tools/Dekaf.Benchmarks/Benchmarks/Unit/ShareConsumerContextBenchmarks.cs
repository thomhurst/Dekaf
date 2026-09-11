using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures full batches with serializers that consume topic, component and raw-key context.</summary>
[MemoryDiagnoser]
public class ShareConsumerContextBenchmarks
{
    private ShareConsumerParsingBenchmarks _parsing = null!;

    [Params(64, 1024)]
    public int RecordCount { get; set; }

    [Params(0, 2)]
    public int HeaderCount { get; set; }

    [GlobalSetup]
    public ValueTask Setup()
    {
        _parsing = new ShareConsumerParsingBenchmarks
        {
            RecordCount = RecordCount, HeaderCount = HeaderCount, ContextDependentDeserialization = true
        };
        return _parsing.Setup();
    }

    [Benchmark]
    public long ParseSynchronousBatch() => _parsing.ParseSynchronousBatch();

    [Benchmark]
    public long ParseWarmPreparedBatch() => _parsing.ParseWarmPreparedBatch();

    [GlobalCleanup]
    public ValueTask Cleanup() => _parsing.Cleanup();
}
