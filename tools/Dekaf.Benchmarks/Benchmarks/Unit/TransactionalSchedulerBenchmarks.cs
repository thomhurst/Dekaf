using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 3)]
public class TransactionalSchedulerBenchmarks
{
    private const int LegacyMicroLingerMaxSpins = 20;
    private ReadyBatch[] _awaitedBatch = null!;

    [GlobalSetup]
    public void Setup()
    {
        var batch = new ReadyBatch();
        batch.Initialize(
            new TopicPartition("benchmark-transaction-scheduler", 0),
            new RecordBatch(),
            completionSourcesArray: null,
            completionSourcesCount: 1,
            dataSize: 1,
            recordCount: 1);
        _awaitedBatch = [batch];
    }

    [Benchmark(Baseline = true)]
    public void LegacySerialAwaitedMicroLinger()
    {
        var spinWait = new SpinWait();
        for (var spin = 0; spin < LegacyMicroLingerMaxSpins; spin++)
            spinWait.SpinOnce();
    }

    [Benchmark]
    public bool AwaitedTransactionalDecision() =>
        BrokerSender.ShouldMicroLinger(_awaitedBatch, 1, isTransactional: true);
}
