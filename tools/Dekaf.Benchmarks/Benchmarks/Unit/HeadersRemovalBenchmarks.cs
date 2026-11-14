using BenchmarkDotNet.Attributes;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures per-record header conflict removal, including the fill and refill it needs.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class HeadersRemovalBenchmarks
{
    private static readonly byte[] Data = "value"u8.ToArray();
    private readonly Headers _headers = new(16);
    private readonly Headers _replacement = new();
    private Header[] _native = null!;

    [GlobalSetup]
    public void Setup()
    {
        _native = new Header[8];
        for (var index = 0; index < _native.Length; index++)
            _native[index] = new Header($"native-{index}", Data);
        _native[2] = new Header("tenant", Data);
        _native[5] = new Header("duplicate", Data);
        _replacement.Add("tenant", Data).Add("duplicate", Data).Add("duplicate", Data);
    }

    [Benchmark(Baseline = true)]
    public int RemoveReplacementKeysOneByOne()
    {
        Fill();
        var replacement = _replacement;
        for (var index = 0; index < replacement.Count; index++)
            _headers.Remove(replacement[index].Key);
        return _headers.Count;
    }

    [Benchmark]
    public int RemoveAllReplacementKeys()
    {
        Fill();
        _headers.RemoveAll(static (header, keys) => keys.GetFirst(header.Key) is not null, _replacement);
        return _headers.Count;
    }

    private void Fill()
    {
        _headers.Clear();
        foreach (var header in _native)
            _headers.Add(header);
    }
}
