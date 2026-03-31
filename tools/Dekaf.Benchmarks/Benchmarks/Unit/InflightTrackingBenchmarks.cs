using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures per-batch allocation in idempotent-specific code paths:
/// PartitionInflightTracker.Register/Complete and sequence number management.
/// These are the only code paths that differ between idempotent and non-idempotent.
///
/// All allocations here are per-batch (~400/sec at high throughput).
/// If any are non-zero, they could seed a GC feedback loop on low-core machines.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, warmupCount: 5, iterationCount: 15)]
public class InflightTrackingBenchmarks
{
    private PartitionInflightTracker _tracker = null!;
    private RecordAccumulator _accumulator = null!;
    private TopicPartition[] _partitions = null!;

    [Params(1, 10)]
    public int PartitionCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _tracker = new PartitionInflightTracker();
        _accumulator = new RecordAccumulator(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
        });

        _partitions = new TopicPartition[PartitionCount];
        for (var i = 0; i < PartitionCount; i++)
            _partitions[i] = new TopicPartition("bench-topic", i);

        // Warmup: populate ConcurrentDictionary entries and pool
        for (var i = 0; i < 1000; i++)
        {
            var tp = _partitions[i % PartitionCount];
            var entry = _tracker.Register(tp, i * 100, 100);
            _tracker.Complete(entry);
        }
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _accumulator.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Steady-state Register + Complete cycle (pool hit expected).
    /// This runs once per batch for idempotent producers.
    /// Expected: zero allocation when pool has entries.
    /// </summary>
    [Benchmark(OperationsPerInvoke = 100)]
    public void RegisterAndComplete()
    {
        for (var i = 0; i < 100; i++)
        {
            var tp = _partitions[i % PartitionCount];
            var entry = _tracker.Register(tp, i * 100, 100);
            _tracker.Complete(entry);
        }
    }

    /// <summary>
    /// Sequence number management (per-batch for idempotent).
    /// Expected: zero allocation (ConcurrentDictionary lookup + Interlocked.Add).
    /// </summary>
    [Benchmark(OperationsPerInvoke = 100)]
    public void GetAndIncrementSequence()
    {
        for (var i = 0; i < 100; i++)
        {
            var tp = _partitions[i % PartitionCount];
            _accumulator.GetAndIncrementSequence(tp, 100);
        }
    }

    /// <summary>
    /// Register only (no Complete) — simulates pool exhaustion scenario.
    /// When entries aren't returned fast enough (e.g., GC pause delays response processing),
    /// the pool runs empty and must allocate new entries.
    /// </summary>
    [Benchmark]
    public void RegisterBurst_PoolExhaustion()
    {
        // Register 1100 entries without completing (exceeds default pool size of 1024)
        var entries = new InflightEntry[1100];
        for (var i = 0; i < 1100; i++)
        {
            var tp = _partitions[i % PartitionCount];
            entries[i] = _tracker.Register(tp, i * 100, 100);
        }

        // Now complete them all
        for (var i = 0; i < 1100; i++)
        {
            _tracker.Complete(entries[i]);
        }
    }
}
