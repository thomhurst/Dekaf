using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Per-flush tracking and materialization of released acquisitions across partitions.
/// The tracker survives between operations, while every returned batch remains independently owned.
/// </summary>
[MemoryDiagnoser]
public class ShareAcknowledgementReleaseBenchmarks
{
    private AcknowledgementTracker _tracker = null!;
    private TopicPartition[] _partitions = null!;
    private Action<TopicPartition, long, long> _release = null!;

    [Params(1, 64)]
    public int PartitionCount { get; set; }

    [Params(16, 128)]
    public int RecordCount { get; set; }

    [Params(false, true)]
    public bool ExplicitOverrides { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _tracker = new();
        _partitions = new TopicPartition[PartitionCount];
        for (var index = 0; index < _partitions.Length; index++)
            _partitions[index] = new TopicPartition("topic", index);

        // Older revisions can express the same release through provisional ranges and
        // Flush(releaseImplicit: true). Bind either direct method outside measurement.
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var track = typeof(AcknowledgementTracker).GetMethod("ReleaseUndeliveredRecords", flags)
            ?? typeof(AcknowledgementTracker).GetMethod("TrackDeliveredRecords", flags)!;
        _release = track.CreateDelegate<Action<TopicPartition, long, long>>(_tracker);
        var result = (Dictionary<TopicPartition, List<AcknowledgementBatchData>>)ReleaseAndFlush();
        if (result.Count != PartitionCount || _tracker.HasPending)
            throw new InvalidOperationException("Every partition must be flushed and detached from the tracker.");
        foreach (var batches in result.Values)
        {
            if (batches.Count != 1 || batches[0].FirstOffset != 0 || batches[0].LastOffset != RecordCount - 1)
                throw new InvalidOperationException("The complete released range must remain represented.");
            var types = batches[0].AcknowledgeTypes;
            for (var index = 0; index < RecordCount; index++)
            {
                var expected = ExplicitOverrides && index == 0 ? AcknowledgeType.Accept
                    : ExplicitOverrides && index == RecordCount - 1 ? AcknowledgeType.Reject
                    : AcknowledgeType.Release;
                if (types[types.Length == 1 ? 0 : index] != (byte)expected)
                    throw new InvalidOperationException("Explicit dispositions must override the released range.");
            }
        }
    }

    [Benchmark]
    public object ReleaseAndFlush()
    {
        foreach (var partition in _partitions)
        {
            _release(partition, 0, RecordCount - 1);
            if (ExplicitOverrides)
            {
                _tracker.Acknowledge(partition, 0, AcknowledgeType.Accept);
                _tracker.Acknowledge(partition, RecordCount - 1, AcknowledgeType.Reject);
            }
        }
        return _tracker.Flush(releaseImplicit: true);
    }
}
