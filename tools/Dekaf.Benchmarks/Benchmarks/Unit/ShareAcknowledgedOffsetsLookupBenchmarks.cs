using BenchmarkDotNet.Attributes;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Includes view construction in every operation, even for a single indexed lookup.</summary>
[MemoryDiagnoser]
public class ShareAcknowledgedOffsetsLookupBenchmarks
{
    private List<AcknowledgementBatchData> _batches = null!;

    [Params(64, 1024)] public int RecordCount { get; set; }
    [Params(false, true)] public bool Sparse { get; set; }
    [Params(1, 4)] public int BatchCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _batches = ShareAcknowledgedOffsetsBenchmarks.CreateBatches(RecordCount, Sparse, BatchCount);
        var expectedCount = Sparse ? RecordCount / 2 : RecordCount;
        var lastBatch = _batches[^1];
        var expectedLast = lastBatch.LastOffset - (Sparse ? 1 : 0);
        if (CreateAndCount() != expectedCount || CreateAndLookupLast() != expectedLast)
            throw new InvalidOperationException("Construction and lookup must preserve acknowledged offsets.");
    }

    [Benchmark]
    public int CreateAndCount() => new ShareAcknowledgedOffsets(_batches).Length;

    [Benchmark]
    public long CreateAndLookupLast()
    {
        var offsets = new ShareAcknowledgedOffsets(_batches);
        return offsets[offsets.Length - 1];
    }
}
