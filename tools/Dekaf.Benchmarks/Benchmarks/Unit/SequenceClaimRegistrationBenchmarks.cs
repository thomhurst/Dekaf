using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// The send loop's per-batch sequence claim and inflight registration for an idempotent producer
/// with epoch recovery (RecordAccumulator.RegisterWithNextSequence): the sequence is claimed
/// inside the inflight tracker's partition lock and the entry registered in the same step, then
/// completed as the response would. Kept apart from <see cref="InflightTrackingBenchmarks"/>
/// because the API is newer than that fixture: a baseline without it measures this class alone,
/// while InflightTrackingBenchmarks still compares Register/Complete and the sequence counter
/// against the baseline.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 3)]
public class SequenceClaimRegistrationBenchmarks
{
    private PartitionInflightTracker _tracker = null!;
    private RecordAccumulator _accumulator = null!;
    private TopicPartition[] _partitions = null!;
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
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _tracker.Dispose();
        await _accumulator.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Steady state: every partition already restarted under the published state.
    /// Expected: zero allocation and the cost of InflightTrackingBenchmarks.RegisterAndComplete
    /// plus GetAndIncrementSequence_CurrentProducerState, with no extra atomic or lock.
    /// </summary>
    [Benchmark(OperationsPerInvoke = 100)]
    public int RegisterWithNextSequenceAndComplete_CurrentProducerState()
    {
        var last = 0;
        for (var i = 0; i < 100; i++)
        {
            var tp = _partitions[i % PartitionCount];
            var state = (ProducerIdAndEpoch?)_producerState;
            var entry = _accumulator.RegisterWithNextSequence(_tracker, tp, 100, -1, ref state, out _)!;
            last = entry.BaseSequence;
            _tracker.Complete(entry);
        }

        return last;
    }
}
