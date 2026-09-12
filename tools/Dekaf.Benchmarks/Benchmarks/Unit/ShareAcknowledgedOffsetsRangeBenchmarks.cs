using BenchmarkDotNet.Attributes;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures compact ranges alone and mixed with expanded sparse vectors.</summary>
[MemoryDiagnoser]
public class ShareAcknowledgedOffsetsRangeBenchmarks
{
    private List<AcknowledgementBatchData> _batches = null!;
    private long[] _destination = null!;

    [Params(64, 1024)] public int RecordCount { get; set; }
    [Params(false, true)] public bool Mixed { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _batches = [new(10, 10 + RecordCount - 1, [(byte)AcknowledgeType.Release])];
        var expected = new List<long>();
        for (var index = 0; index < RecordCount; index++)
            expected.Add(10 + index);
        if (Mixed)
        {
            _batches.Add(new(2000, 2000 + RecordCount - 1, [(byte)AcknowledgeType.Gap]));
            var types = new byte[RecordCount];
            for (var index = 0; index < RecordCount; index += 2)
            {
                types[index] = (byte)AcknowledgeType.Accept;
                expected.Add(4000 + index);
            }
            _batches.Add(new(4000, 4000 + RecordCount - 1, types));
        }

        _destination = new long[expected.Count];
        var offsets = new ShareAcknowledgedOffsets(_batches);
        if (offsets.Length != expected.Count)
            throw new InvalidOperationException("Compact ranges must count logical offsets.");
        offsets.CopyTo(_destination);
        var enumerator = offsets.GetEnumerator();
        for (var index = 0; index < expected.Count; index++)
            if (_destination[index] != expected[index] || offsets[index] != expected[index]
                || !enumerator.MoveNext() || enumerator.Current != expected[index])
                throw new InvalidOperationException("Every access path must preserve compact and sparse offsets.");
        if (enumerator.MoveNext() || enumerator.MoveNext())
            throw new InvalidOperationException("The enumerator must remain exhausted.");
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
    public long CreateAndCopy()
    {
        new ShareAcknowledgedOffsets(_batches).CopyTo(_destination);
        return _destination[^1];
    }
}
