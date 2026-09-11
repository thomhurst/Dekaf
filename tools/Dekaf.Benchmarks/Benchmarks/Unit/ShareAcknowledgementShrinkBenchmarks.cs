using BenchmarkDotNet.Attributes;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// A steady one-partition release workload after a larger acknowledgement window.
/// Historical partition counts must not amplify each subsequent flush's cleanup work.
/// </summary>
[MemoryDiagnoser]
public class ShareAcknowledgementShrinkBenchmarks
{
    private readonly TopicPartition _partition = new("topic", 0);
    private AcknowledgementTracker _tracker = null!;

    [Params(1, 4096)]
    public int PeakPartitionCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _tracker = new();
        for (var index = 0; index < PeakPartitionCount; index++)
            _tracker.TrackDeliveredRecords(new TopicPartition("topic", index), 0, 127);
        _tracker.Flush(releaseImplicit: true);

        var result = (Dictionary<TopicPartition, List<AcknowledgementBatchData>>)ReleaseAndFlush();
        if (result.Count != 1 || _tracker.HasPending)
            throw new InvalidOperationException("Only the current partition must remain in the flush.");
        var batches = result[_partition];
        if (batches.Count != 1 || batches[0].FirstOffset != 0 || batches[0].LastOffset != 127)
            throw new InvalidOperationException("The complete released range must remain represented.");
        foreach (var type in batches[0].AcknowledgeTypes)
            if (type != (byte)AcknowledgeType.Release)
                throw new InvalidOperationException("The current range must contain only release dispositions.");
    }

    [Benchmark]
    public object ReleaseAndFlush()
    {
        _tracker.TrackDeliveredRecords(_partition, 0, 127);
        return _tracker.Flush(releaseImplicit: true);
    }
}
