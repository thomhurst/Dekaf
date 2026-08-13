using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol.Records;
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
    private ConcurrentStackPool _concurrentStack = null!;
    private ObjectPool<PooledItem, PooledItemPolicy> _reservoir = null!;
    private TrackedPool _tracked = null!;

    [Params(32, 128, 256)]
    public int Capacity { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _concurrentStack = new ConcurrentStackPool(Capacity);
        _reservoir = new ObjectPool<PooledItem, PooledItemPolicy>(Capacity);
        _tracked = new TrackedPool(Capacity);

        var concurrentStackItem = _concurrentStack.Rent();
        _concurrentStack.Return(concurrentStackItem);
        var item = _reservoir.Rent();
        _reservoir.Return(item);
        _tracked.PreWarm(1);
    }

    [Benchmark(Baseline = true)]
    public PooledItem ConcurrentStack()
    {
        var item = _concurrentStack.Rent();
        _concurrentStack.Return(item);
        return item;
    }

    [Benchmark]
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

    private sealed class ConcurrentStackPool(int capacity)
    {
        private readonly ConcurrentStack<PooledItem> _items = new();
        private int _count;

        public PooledItem Rent()
        {
            if (!_items.TryPop(out var item))
                return new PooledItem();

            Interlocked.Decrement(ref _count);
            return item;
        }

        public void Return(PooledItem item)
        {
            if (Interlocked.Increment(ref _count) <= capacity)
            {
                _items.Push(item);
                return;
            }

            Interlocked.Decrement(ref _count);
        }
    }

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

/// <summary>
/// Measures warm rent/return paths for pools changed by the Reservoir migration.
/// </summary>
[MemoryDiagnoser]
[OperationsPerSecond]
public class PoolHotPathBenchmarks
{
    private const int ArenaCapacity = 1024 * 1024;
    private static readonly IReadOnlyList<RecordBatch> EmptyBatches = Array.Empty<RecordBatch>();
    private ValueTaskSourcePool<int> _valueTaskSources = null!;
    private PendingRequestPool _pendingRequests = null!;
    private PipeMemoryPool _pipeMemory = null!;

    [GlobalSetup]
    public void Setup()
    {
        _valueTaskSources = new ValueTaskSourcePool<int>(4096);
        _pendingRequests = new PendingRequestPool();
        _pipeMemory = new PipeMemoryPool();

        var source = _valueTaskSources.Rent();
        source.SetResult(0);
        _ = source.Task.GetAwaiter().GetResult();

        var pendingRequest = _pendingRequests.Rent();
        _pendingRequests.Return(pendingRequest);

        _pipeMemory.Rent(4096).Dispose();

        var arena = BatchArena.RentOrCreate(ArenaCapacity);
        BatchArena.ReturnToPool(arena);

        PendingFetchData.Create("benchmark", 0, EmptyBatches).Dispose();
    }

    [Benchmark]
    public int ValueTaskSourceRentCompleteReturn()
    {
        var source = _valueTaskSources.Rent();
        source.SetResult(42);
        return source.Task.GetAwaiter().GetResult();
    }

    [Benchmark]
    public int PendingFetchDataRentReturn()
    {
        var data = PendingFetchData.Create("benchmark", 0, EmptyBatches);
        var partitionIndex = data.PartitionIndex;
        data.Dispose();
        return partitionIndex;
    }

    [Benchmark]
    public int BatchArenaRentReturn()
    {
        var arena = BatchArena.RentOrCreate(ArenaCapacity);
        var capacity = arena.Capacity;
        BatchArena.ReturnToPool(arena);
        return capacity;
    }

    [Benchmark]
    public int PendingRequestRentReturn()
    {
        var request = _pendingRequests.Rent();
        _pendingRequests.Return(request);
        return _pendingRequests.ApproximateCount;
    }

    [Benchmark]
    public int PipeMemoryOwnerRentReturn()
    {
        var owner = _pipeMemory.Rent(4096);
        var length = owner.Memory.Length;
        owner.Dispose();
        return length;
    }

    [GlobalCleanup]
    public void Cleanup() => _pipeMemory.Dispose();
}
