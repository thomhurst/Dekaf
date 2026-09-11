using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures borrowed parsing separately to keep each parsing fixture within sixteen cases.</summary>
[MemoryDiagnoser]
public class ShareConsumerBorrowedParsingBenchmarks
{
    private ShareConsumerParsingBenchmarks _parsing = null!;

    [Params(64, 1024)]
    public int RecordCount { get; set; }

    [Params(0, 2)]
    public int HeaderCount { get; set; }

    [GlobalSetup]
    public ValueTask Setup()
    {
        _parsing = new ShareConsumerParsingBenchmarks { RecordCount = RecordCount, HeaderCount = HeaderCount };
        return _parsing.Setup();
    }

    [Benchmark]
    public ValueTask<long> ParseBorrowedSynchronousBatch() => _parsing.ParseBorrowedSynchronousBatch();

    [Benchmark]
    public ValueTask<long> ParseBorrowedWarmPreparedBatch() => _parsing.ParseBorrowedWarmPreparedBatch();

    [Benchmark]
    public ValueTask<long> ParseBorrowedColdPreparedBatch() => _parsing.ParseBorrowedColdPreparedBatch();

    [GlobalCleanup]
    public ValueTask Cleanup() => _parsing.Cleanup();
}
