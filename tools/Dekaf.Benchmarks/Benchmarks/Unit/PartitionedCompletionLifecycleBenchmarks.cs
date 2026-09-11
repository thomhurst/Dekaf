using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures completion tracking across partition assignment lifetimes. One operation
/// creates a lane, publishes batches, and retires its completion tracking. Repeated
/// operations expose whether returned storage can be reused by another assignment.
/// </summary>
[MemoryDiagnoser]
public class PartitionedCompletionLifecycleBenchmarks
{
    [Params(1, 1024)]
    public int Records { get; set; }

    [Params(false, true)]
    public bool MarkRecords { get; set; }

    [Params(1, 16)]
    public int Batches { get; set; }

    [Benchmark]
    public TopicPartitionOffset? CreateAndRetireLane()
    {
        var lane = new PartitionLane<string, string>(new TopicPartition("lifecycle", 0), 32,
            static (_, _) => default, static _ => { }, static (_, _) => { });
        for (var batchIndex = 0; batchIndex < Batches; batchIndex++)
        {
            var batch = lane.CreateCompletionBatch(Records)!;
            for (var index = 0; index < Records; index++)
            {
                var record = new ConsumeResult<string, string>("lifecycle", 0, (batchIndex * Records + index) * 2L,
                    "key", "value", null, 0, TimestampType.CreateTime, 1);
                if (!lane.TryEnqueue(record, batch) || !lane.TryReadMessage(out var delivered))
                    throw new InvalidOperationException("The assignment lost a record.");
                if (MarkRecords)
                    lane.MarkProcessed(delivered);
            }
            lane.EndBatch(batch, Records);
        }
        var checkpoint = lane.GetCommitOffset();
        if (checkpoint?.Offset != (MarkRecords ? (Batches * Records - 1) * 2L + 1 : (long?)null))
            throw new InvalidOperationException("The assignment reported an incorrect checkpoint.");
        lane.EnableAutomaticCompletion();
        return checkpoint;
    }
}
