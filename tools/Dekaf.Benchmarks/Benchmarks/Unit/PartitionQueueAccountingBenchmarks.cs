using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class PartitionQueueAccountingBenchmarks
{
    private const int BatchBytes = 1_048_576;
    private readonly TopicPartition _topicPartition = new("queue-accounting", 0);
    private readonly ConcurrentDictionary<TopicPartition, long> _queueBytes = new();
    private readonly AtomicQueueBytes _atomicQueueBytes = new();
    private Thread _dictionaryDrainer = null!;
    private Thread _atomicDrainer = null!;
    private int _stopping;

    [GlobalSetup]
    public void Setup()
    {
        _queueBytes[_topicPartition] = 0;
        _dictionaryDrainer = StartDrainer(DrainDictionary);
        _atomicDrainer = StartDrainer(DrainAtomicCounter);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Volatile.Write(ref _stopping, 1);
        _dictionaryDrainer.Join();
        _atomicDrainer.Join();
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = 1_000)]
    public long ConcurrentDictionaryAddOrUpdate()
    {
        for (var i = 0; i < 1_000; i++)
        {
            _queueBytes.AddOrUpdate(
                _topicPartition,
                static (_, bytes) => bytes,
                static (_, current, bytes) => SaturatingAdd(current, bytes),
                BatchBytes);
        }

        return _queueBytes[_topicPartition];
    }

    [Benchmark(OperationsPerInvoke = 1_000)]
    public long AtomicPartitionCounter()
    {
        for (var i = 0; i < 1_000; i++)
            _atomicQueueBytes.Add(BatchBytes);

        return Volatile.Read(ref _atomicQueueBytes.Value);
    }

    private static Thread StartDrainer(ThreadStart action)
    {
        var thread = new Thread(action) { IsBackground = true };
        thread.Start();
        return thread;
    }

    private void DrainDictionary()
    {
        while (Volatile.Read(ref _stopping) == 0)
        {
            _queueBytes.AddOrUpdate(
                _topicPartition,
                static (_, _) => 0,
                static (_, current, bytes) => Math.Max(0, current - bytes),
                BatchBytes);
        }
    }

    private void DrainAtomicCounter()
    {
        while (Volatile.Read(ref _stopping) == 0)
            _atomicQueueBytes.Remove(BatchBytes);
    }

    private static long SaturatingAdd(long current, int value)
    {
        var result = current + value;
        return result < current ? long.MaxValue : result;
    }

    private sealed class AtomicQueueBytes
    {
        public long Value;

        public void Add(int bytes)
        {
            var current = Volatile.Read(ref Value);
            while (true)
            {
                var updated = SaturatingAdd(current, bytes);
                var observed = Interlocked.CompareExchange(ref Value, updated, current);
                if (observed == current)
                    return;

                current = observed;
            }
        }

        public void Remove(int bytes)
        {
            var current = Volatile.Read(ref Value);
            while (true)
            {
                var updated = Math.Max(0, current - bytes);
                var observed = Interlocked.CompareExchange(ref Value, updated, current);
                if (observed == current)
                    return;

                current = observed;
            }
        }
    }
}
