using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Reservoir;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures warm Reservoir rent/return and Dekaf's diagnostic wrapper.
/// One operation is one rent/return pair.
/// </summary>
[MemoryDiagnoser]
[OperationsPerSecond]
public class ObjectPoolMigrationBenchmarks
{
    private ObjectPool<PooledItem, PooledItemPolicy> _reservoir = null!;
    private TrackedPool _tracked = null!;

    [Params(32, 128)]
    public int Capacity { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _reservoir = new ObjectPool<PooledItem, PooledItemPolicy>(Capacity);
        _tracked = new TrackedPool(Capacity);

        var item = _reservoir.Rent();
        _reservoir.Return(item);
        _tracked.PreWarm(1);
    }

    [Benchmark(Baseline = true)]
    public PooledItem Reservoir()
    {
        var item = _reservoir.Rent();
        _reservoir.Return(item);
        return item;
    }

    [Benchmark]
    public PooledItem DekafTrackedPool()
    {
        var item = _tracked.Rent();
        _tracked.Return(item);
        return item;
    }

    public sealed class PooledItem;

    private sealed class TrackedPool(int capacity)
        : Dekaf.Producer.ObjectPool<PooledItem>(capacity)
    {
        protected override PooledItem Create() => new();

        protected override void Reset(PooledItem item) { }
    }

    private readonly struct PooledItemPolicy : IPooledObjectPolicy<PooledItem>
    {
        public PooledItem Create() => new();

        public bool TryReset(PooledItem obj) => true;
    }
}
