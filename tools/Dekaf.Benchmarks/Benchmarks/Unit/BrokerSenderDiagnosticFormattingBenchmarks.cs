using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class BrokerSenderDiagnosticFormattingBenchmarks
{
    private ReadyBatch[] _batches = null!;

    [Params(16)]
    public int BatchCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _batches = new ReadyBatch[BatchCount];
        for (var i = 0; i < BatchCount; i++)
            _batches[i] = CreateBatch("benchmark-topic", i);
    }

    [Benchmark(Baseline = true)]
    public string CapturingLinq() => string.Join(", ",
        Enumerable.Range(0, BatchCount)
            .Where(i => _batches[i] is not null)
            .Select(i => $"{_batches[i].TopicPartition.Topic}-{_batches[i].TopicPartition.Partition}"));

    [Benchmark]
    public string IndexedStringCreate() => BrokerSender.FormatBatchKeys(_batches, BatchCount);

    private static ReadyBatch CreateBatch(string topic, int partition)
    {
        var batch = new ReadyBatch();
        batch.Initialize(
            new TopicPartition(topic, partition),
            new RecordBatch { Records = [] },
            completionSourcesArray: null,
            completionSourcesCount: 0,
            recordCount: 0,
            dataSize: 0);
        return batch;
    }
}
