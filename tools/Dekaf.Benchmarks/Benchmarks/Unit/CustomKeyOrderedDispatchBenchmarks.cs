using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Exercises the dispatcher custom-comparer adapter with the same keys, handler
/// scheduling and lifetime lengths as the default scalar comparer fixture.
/// </summary>
[MemoryDiagnoser]
public class CustomKeyOrderedDispatchBenchmarks
{
    private KeyOrderedDispatchBenchmarks _dispatch = null!;

    [Params(KeyOrderedDispatchBenchmarks.KeyPattern.Repeated,
        KeyOrderedDispatchBenchmarks.KeyPattern.Distinct,
        KeyOrderedDispatchBenchmarks.KeyPattern.PendingPairs)]
    public KeyOrderedDispatchBenchmarks.KeyPattern Pattern { get; set; }

    [Params(1, 16)]
    public int BatchSize { get; set; }

    [Params(128, 262144)]
    public int RecordCount { get; set; }

    [GlobalSetup]
    public Task Setup()
    {
        _dispatch = new KeyOrderedDispatchBenchmarks
        {
            Pattern = Pattern, BatchSize = BatchSize, RecordCount = RecordCount,
            KeyComparer = new Int32Comparer()
        };
        return _dispatch.Setup();
    }

    [Benchmark]
    public ValueTask<int> DispatchLifetime() => _dispatch.DispatchLifetime();

    private sealed class Int32Comparer : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => x == y;
        public int GetHashCode(int value) => value;
    }
}
