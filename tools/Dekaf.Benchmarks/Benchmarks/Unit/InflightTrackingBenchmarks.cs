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
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 3)]
public class InflightTrackingBenchmarks
{
    // A 64th of the sequence space per call: a partition wraps at least every seventh invocation.
    private const int SequenceWrapStride = int.MaxValue / 64;

    private PartitionInflightTracker _tracker = null!;
    private RecordAccumulator _accumulator = null!;
    private TopicPartition[] _partitions = null!;
    private InflightEntry[] _burstEntries = null!;
    private ProducerIdAndEpoch _producerState = null!;

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

        // Every partition restarted under the published producer state, as after the first send.
        _producerState = new ProducerIdAndEpoch(1234, 0);
        _accumulator.PublishProducerState(_producerState);
        foreach (var tp in _partitions)
            _accumulator.GetAndIncrementSequence(tp, 1, _producerState, out _);

        _burstEntries = new InflightEntry[1100];
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
    /// Send-time sequence assignment for an idempotent producer with epoch recovery: lookup,
    /// producer-state stamp check, interlocked add. Steady state — every partition already
    /// restarted under the published state, so the stamp check is a single reference compare
    /// and the restart path is never taken.
    /// Expected: zero allocation and the same cost as GetAndIncrementSequence.
    /// </summary>
    [Benchmark(OperationsPerInvoke = 100)]
    public void GetAndIncrementSequence_CurrentProducerState()
    {
        for (var i = 0; i < 100; i++)
        {
            var tp = _partitions[i % PartitionCount];
            _accumulator.GetAndIncrementSequence(tp, 100, _producerState, out _);
        }
    }

    /// <summary>
    /// The same assignment with batches so large that the counter passes the end of the sequence
    /// space (int.MaxValue, after which the next sequence is 0) every few calls. The wrap is part
    /// of the same branch-free expression as the increment, so cost and allocation must match
    /// <see cref="GetAndIncrementSequence_CurrentProducerState"/>.
    /// </summary>
    [Benchmark(OperationsPerInvoke = 100)]
    public int GetAndIncrementSequence_AcrossSequenceWrap()
    {
        var last = 0;
        for (var i = 0; i < 100; i++)
        {
            var tp = _partitions[i % PartitionCount];
            last = _accumulator.GetAndIncrementSequence(tp, SequenceWrapStride, _producerState, out _);
        }

        return last;
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
        for (var i = 0; i < 1100; i++)
        {
            var tp = _partitions[i % PartitionCount];
            _burstEntries[i] = _tracker.Register(tp, i * 100, 100);
        }

        for (var i = 0; i < 1100; i++)
        {
            _tracker.Complete(_burstEntries[i]);
        }
    }
}
