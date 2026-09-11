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
    [Params(1, 4)] public int BatchCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _batches = CreateBatches(RecordCount, Sparse, BatchCount);
        var offsets = new ShareAcknowledgedOffsets(_batches);
        if (offsets.Length != (Sparse ? (RecordCount + 1) / 2 : RecordCount))
            throw new InvalidOperationException("The callback included an unacknowledged offset.");
        long expectedSum = 0;
        foreach (var batch in _batches)
            for (var index = 0; index < batch.AcknowledgeTypes.Length; index++)
                if (batch.AcknowledgeTypes[index] != (byte)AcknowledgeType.Gap)
                    expectedSum += batch.FirstOffset + index;
        if (CreateAndEnumerate() != expectedSum || CreateAndIndex() != expectedSum)
            throw new InvalidOperationException("Indexed and enumerated offsets must cover the same acknowledged records.");
    }

    internal static List<AcknowledgementBatchData> CreateBatches(int recordCount, bool sparse, int batchCount)
    {
        var batches = new List<AcknowledgementBatchData>(batchCount);
        var recordsPerBatch = recordCount / batchCount;
        for (var batchIndex = 0; batchIndex < batchCount; batchIndex++)
        {
            var types = new byte[recordsPerBatch];
            for (var index = 0; index < types.Length; index++)
                types[index] = sparse && index % 2 != 0 ? (byte)AcknowledgeType.Gap : (byte)AcknowledgeType.Accept;
            var firstOffset = batchIndex * (recordsPerBatch + 10L);
            batches.Add(new AcknowledgementBatchData(firstOffset, firstOffset + recordsPerBatch - 1, types));
        }
        return batches;
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
