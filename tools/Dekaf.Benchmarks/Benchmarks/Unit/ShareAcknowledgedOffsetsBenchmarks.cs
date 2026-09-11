using BenchmarkDotNet.Attributes;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures callback offset views separately from protocol-vector allocation.</summary>
[MemoryDiagnoser]
public class ShareAcknowledgedOffsetsBenchmarks
{
    private List<AcknowledgementBatchData> _batches = null!;

    [Params(64, 1024)] public int RecordCount { get; set; }
    [Params(false, true)] public bool Sparse { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var types = new byte[RecordCount];
        for (var index = 0; index < types.Length; index++)
            types[index] = Sparse && index % 2 != 0 ? (byte)AcknowledgeType.Gap : (byte)AcknowledgeType.Accept;
        _batches = [new AcknowledgementBatchData(0, RecordCount - 1, types)];
        var offsets = new ShareAcknowledgedOffsets(_batches);
        if (offsets.Length != (Sparse ? (RecordCount + 1) / 2 : RecordCount))
            throw new InvalidOperationException("The callback included an unacknowledged offset.");
        var expectedSum = Sparse
            ? (long)offsets.Length * (offsets.Length - 1)
            : (long)RecordCount * (RecordCount - 1) / 2;
        if (CreateAndEnumerate() != expectedSum || CreateAndIndex() != expectedSum)
            throw new InvalidOperationException("Indexed and enumerated offsets must cover the same acknowledged records.");
    }

    [Benchmark]
    public long CreateAndEnumerate()
    {
        var offsets = new ShareAcknowledgedOffsets(_batches);
        long sum = 0;
        foreach (var offset in offsets)
            sum += offset;
        return sum;
    }

    [Benchmark]
    public long CreateAndIndex()
    {
        var offsets = new ShareAcknowledgedOffsets(_batches);
        long sum = 0;
        for (var index = 0; index < offsets.Length; index++)
            sum += offsets[index];
        return sum;
    }
}
