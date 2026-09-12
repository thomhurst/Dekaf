using BenchmarkDotNet.Attributes;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class ShareAcknowledgementTrackingBenchmarks
{
    private readonly TopicPartition _partition = new("topic", 0);
    private AcknowledgementTracker _tracker = null!;
    private long _offset;
    private AcknowledgementTracker _flushTracker = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tracker = new AcknowledgementTracker();
        _flushTracker = new AcknowledgementTracker();
        _tracker.TrackDeliveredRecords(_partition, 0, 0);
        _tracker.Acknowledge(_partition, 0, AcknowledgeType.Accept);
    }

    [Benchmark]
    public bool TrackDelivery()
    {
        var offset = ++_offset;
        _tracker.TrackDeliveredRecords(_partition, offset, offset);
        return _tracker.HasPending;
    }

    [Benchmark]
    public bool UpdateExplicit()
    {
        _tracker.Acknowledge(_partition, 0, AcknowledgeType.Accept);
        return _tracker.HasPending;
    }

    [Benchmark]
    public int FlushReusedBatch()
    {
        _flushTracker.TrackDeliveredRecords(_partition, 0, 999);
        return _flushTracker.Flush()[_partition][0].AcknowledgeTypes.Length;
    }

    [Benchmark]
    public int FlushBatch()
    {
        var tracker = new AcknowledgementTracker();
        tracker.TrackDeliveredRecords(_partition, 0, 999);
        return tracker.Flush()[_partition][0].AcknowledgeTypes.Length;
    }
}
