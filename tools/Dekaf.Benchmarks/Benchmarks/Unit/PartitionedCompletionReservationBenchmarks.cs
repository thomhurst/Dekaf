using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures a large reservation with a short published prefix in steady state.
/// One operation includes reservation, publication, completion and storage return.
/// Cold chunk initialization is covered separately by the completion storage tests.
/// </summary>
[MemoryDiagnoser]
public class PartitionedCompletionReservationBenchmarks
{
    private PartitionLane<string, string> _lane = null!;
    private long _offset;

    [Params(131072)]
    public int Capacity { get; set; }

    [Params(0, 1, 1025)]
    public int PublishedRecords { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _lane = new(new TopicPartition("reservation", 0), 32,
            static (_, _) => default, static _ => { }, static (_, _) => { });
    }

    [Benchmark]
    public TopicPartitionOffset? PublishPrefix()
    {
        var batch = _lane.CreateCompletionBatch(Capacity)
            ?? throw new InvalidOperationException("The manual lane did not create its completion reservation.");
        for (var index = 0; index < PublishedRecords; index++)
        {
            var record = new ConsumeResult<string, string>("reservation", 0, ++_offset,
                "key", "value", null, 0, TimestampType.CreateTime, 1);
            if (!_lane.TryEnqueue(record, batch) || !_lane.TryReadMessage(out var delivered))
                throw new InvalidOperationException("Unable to publish the reserved prefix.");
            _lane.MarkProcessed(delivered);
        }
        _lane.EndBatch(batch, PublishedRecords);
        if (batch.Nodes is not null || _lane.GetCommitOffset()?.Offset != (PublishedRecords == 0 ? (long?)null : _offset + 1))
            throw new InvalidOperationException("The completed prefix retained storage or lost progress.");
        return _lane.GetCommitOffset();
    }
}
