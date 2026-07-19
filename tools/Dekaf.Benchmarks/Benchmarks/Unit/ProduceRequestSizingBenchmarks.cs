using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class ProduceRequestSizingBenchmarks
{
    [Params("a", "topic-name-longer-than-a-topic-id")]
    public string Topic { get; set; } = null!;

    [Benchmark]
    public int GetSingleBatchRequestBodySize() =>
        ProduceRequestSizeCalculator.GetConservativeSingleBatchRequestBodySize(
            transactionalId: null,
            Topic,
            encodedBatchSize: 1000);
}
