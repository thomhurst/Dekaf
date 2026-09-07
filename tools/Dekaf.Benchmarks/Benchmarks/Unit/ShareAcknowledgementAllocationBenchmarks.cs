using BenchmarkDotNet.Attributes;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Separates fresh acknowledgement accumulation from wire materialization.</summary>
[MemoryDiagnoser]
public class ShareAcknowledgementAllocationBenchmarks
{
    private readonly TopicPartition _partition = new("share-benchmark", 0);
    private AcknowledgementTracker _pending = null!;

    [Params(64, 1024)]
    public int RecordCount { get; set; }

    [Params(ShareAcknowledgementMode.Implicit, ShareAcknowledgementMode.Explicit)]
    public ShareAcknowledgementMode Mode { get; set; }

    [GlobalSetup]
    public void Validate()
    {
        var tracker = Accumulate();
        var wire = tracker.Flush();
        if (tracker.HasPending || wire.Count != 1 || wire[_partition].Count != 1)
            throw new InvalidOperationException("Acknowledgements did not form one bounded partition batch.");
        var batch = wire[_partition][0];
        if (batch.FirstOffset != 0 || batch.LastOffset != RecordCount - 1 || batch.AcknowledgeTypes.Length != RecordCount)
            throw new InvalidOperationException("Acknowledgements did not cover every delivered offset.");
        for (var index = 0; index < RecordCount; index++)
        {
            var expected = Mode == ShareAcknowledgementMode.Implicit ? AcknowledgeType.Accept : Disposition(index);
            if (batch.AcknowledgeTypes[index] != (byte)expected)
                throw new InvalidOperationException("An acknowledgement disposition changed.");
        }
    }

    [IterationSetup(Target = nameof(MaterializeWireBatch))]
    public void SetupPending() => _pending = Accumulate();

    [Benchmark]
    public bool AccumulateFreshBatch() => Accumulate().HasPending;

    // Single invocation is intentional: Flush consumes the tracker. Allocation evidence is
    // useful at this boundary; its short single-invocation timing is not an acceptance gate.
    [Benchmark]
    [InvocationCount(1)]
    public int MaterializeWireBatch() => _pending.Flush()[_partition][0].AcknowledgeTypes.Length;

    private AcknowledgementTracker Accumulate()
    {
        // Include fresh storage growth. Reusing warmed keys would conceal per-record costs.
        var tracker = new AcknowledgementTracker();
        for (var index = 0; index < RecordCount; index++)
        {
            if (Mode == ShareAcknowledgementMode.Implicit)
                tracker.TrackDeliveredRecords(_partition, index, index);
            else
                tracker.Acknowledge(_partition, index, Disposition(index), requireTracked: false);
        }
        return tracker;
    }

    private static AcknowledgeType Disposition(int index) => (index % 3) switch
    {
        0 => AcknowledgeType.Accept,
        1 => AcknowledgeType.Release,
        _ => AcknowledgeType.Reject
    };
}
