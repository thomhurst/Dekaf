using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 10, iterationCount: 10)]
public class ProducerCarryOverBenchmarks
{
    private const int BatchCount = 8;

    private readonly BrokerSender.PartitionCarryOver _carryOver = new();
    private readonly List<BrokerSender.BatchReference> _drained = new(BatchCount);
    private ReadyBatch[] _batches = null!;

    [GlobalSetup]
    public void Setup()
    {
        _batches = new ReadyBatch[BatchCount];
        for (var i = 0; i < _batches.Length; i++)
        {
            var batch = new ReadyBatch();
            batch.Initialize(
                new TopicPartition("carry-over", 0),
                new RecordBatch { Records = [] },
                completionSourcesArray: null,
                completionSourcesCount: 0,
                recordCount: 0,
                dataSize: 0);
            batch.IsRetry = true;
            batch.MarkPreSerialized();
            _batches[i] = batch;
        }
    }

    [Benchmark(OperationsPerInvoke = BatchCount)]
    public int EnqueueAndDrainReadyRetries()
    {
        for (var i = 0; i < _batches.Length; i++)
        {
            var batch = _batches[i];
            _carryOver.AddAfterRetries(new BrokerSender.BatchReference(batch, batch.Generation));
        }

        _drained.Clear();
        _carryOver.DrainTo(_drained);
        return _drained.Count;
    }
}
