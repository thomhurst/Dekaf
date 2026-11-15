using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures the per-record cost of the transactional read gate (one CAS enter and one decrement
/// exit per read) against an ungated lane, with the gate open as it is between commits.
/// </summary>
[MemoryDiagnoser]
public class PartitionedTransactionalReadBenchmarks
{
    private PartitionLane<string, string> _ungated = null!;
    private PartitionLane<string, string> _gated = null!;
    private TransactionReadGate _gate = null!;
    private long _ungatedOffset;
    private long _gatedOffset;

    [Params(64, 1024)]
    public int Records { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _gate = new TransactionReadGate();
        _ungated = CreateLane(null);
        _gated = CreateLane(_gate);
    }

    [GlobalCleanup]
    public void Cleanup() => _gate.Dispose();

    // One operation is a complete batch of enqueue, read and completion.
    [Benchmark(Baseline = true)]
    public TopicPartitionOffset? Ungated() => RunBatch(_ungated, ref _ungatedOffset);

    [Benchmark]
    public TopicPartitionOffset? Gated() => RunBatch(_gated, ref _gatedOffset);

    private TopicPartitionOffset? RunBatch(PartitionLane<string, string> lane, ref long offset)
    {
        var batch = lane.CreateCompletionBatch(Records);
        for (var index = 0; index < Records; index++)
        {
            lane.TryEnqueue(new ConsumeResult<string, string>("transactional", 0, ++offset,
                "key", "value", null, 0, TimestampType.CreateTime, 1), batch);
            lane.TryReadMessage(out var message);
            lane.MarkProcessed(message);
        }
        lane.EndBatch(batch, Records);
        return lane.GetCommitOffset();
    }

    private static PartitionLane<string, string> CreateLane(TransactionReadGate? gate)
        => new(new TopicPartition("transactional", 0), 32,
            static (_, _) => default, static _ => { }, static (_, _) => { }, readGate: gate);
}
