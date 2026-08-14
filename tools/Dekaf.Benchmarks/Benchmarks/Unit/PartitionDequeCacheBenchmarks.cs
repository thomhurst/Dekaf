using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures one partition-deque cache resolution per produced message. The dictionary is warmed
/// before measurement, isolating direct-cache hits, collision misses, and thread contention.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 3)]
public class PartitionDequeCacheBenchmarks
{
    private const int SequentialResolutionsPerInvoke = 100;
    private const int ConcurrentResolutionsPerWorker = 100_000;
    private const int PreviousShardReuseDistance = 64;

    private RecordAccumulator _accumulator = null!;
    private AutoResetEvent _partition0Start = null!;
    private AutoResetEvent _partition16Start = null!;
    private AutoResetEvent _partition0Done = null!;
    private AutoResetEvent _partition16Done = null!;
    private Thread _partition0Worker = null!;
    private Thread _partition16Worker = null!;
    private volatile bool _stopWorkers;
    private Exception? _workerException;
    private int _partition0Checksum;
    private int _partition16Checksum;

    [GlobalSetup]
    public void Setup()
    {
        _accumulator = new RecordAccumulator(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BufferMemory = ulong.MaxValue,
            BatchSize = 1_048_576,
            LingerMs = 0
        });

        _accumulator.ResolvePartitionDequeForTest("bench-topic", partition: 0);
        _accumulator.ResolvePartitionDequeForTest("bench-topic", partition: 16);

        _partition0Start = new AutoResetEvent(false);
        _partition16Start = new AutoResetEvent(false);
        _partition0Done = new AutoResetEvent(false);
        _partition16Done = new AutoResetEvent(false);
        _partition0Worker = StartWorker(partition: 0, _partition0Start, _partition0Done);
        RunWorker(_partition0Start, _partition0Done);

        // The previous modulo-64 assignment reused partition 0's live shard after this churn.
        for (var i = 1; i < PreviousShardReuseDistance; i++)
        {
            var churnThread = new Thread(static state =>
            {
                var accumulator = (RecordAccumulator)state!;
                _ = accumulator.ResolvePartitionDequeForTest("bench-topic", partition: 32);
            });
            churnThread.Start(_accumulator);
            churnThread.Join();
        }

        _partition16Worker = StartWorker(partition: 16, _partition16Start, _partition16Done);
        RunWorker(_partition16Start, _partition16Done);

        RunWorkers();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _stopWorkers = true;
        _partition0Start.Set();
        _partition16Start.Set();
        _partition0Worker.Join();
        _partition16Worker.Join();
        _partition0Start.Dispose();
        _partition16Start.Dispose();
        _partition0Done.Dispose();
        _partition16Done.Dispose();
        await _accumulator.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Alternates warmed partitions 0 and 16, which map to the same direct-cache slot.
    /// Every resolution is a cache miss and allocation must remain 0 B/message.
    /// </summary>
    [Benchmark(OperationsPerInvoke = SequentialResolutionsPerInvoke)]
    public int ResolveColdCacheCollision()
    {
        var checksum = 0;
        for (var i = 0; i < SequentialResolutionsPerInvoke; i++)
            checksum += _accumulator.ResolvePartitionDequeForTest("bench-topic", (i & 1) << 4);

        return checksum;
    }

    /// <summary>
    /// Resolves colliding slots concurrently after forcing the prior modulo-64 shard collision.
    /// Worker creation is outside measurement; synchronization is amortized over 100,000 resolutions.
    /// </summary>
    [Benchmark(OperationsPerInvoke = ConcurrentResolutionsPerWorker * 2)]
    public int ResolveConcurrentCacheCollisionAfterThreadChurn()
    {
        RunWorkers();

        if (_workerException is { } exception)
            throw new InvalidOperationException("Partition-deque cache worker failed.", exception);

        return _partition0Checksum + _partition16Checksum;
    }

    private Thread StartWorker(int partition, AutoResetEvent start, AutoResetEvent done)
    {
        var thread = new Thread(() => WorkerLoop(partition, start, done))
        {
            IsBackground = true,
            Name = $"PartitionDequeCacheBenchmark-p{partition}"
        };
        thread.Start();
        return thread;
    }

    private void WorkerLoop(int partition, AutoResetEvent start, AutoResetEvent done)
    {
        while (true)
        {
            start.WaitOne();
            if (_stopWorkers)
                return;

            try
            {
                var checksum = 0;
                for (var i = 0; i < ConcurrentResolutionsPerWorker; i++)
                    checksum += _accumulator.ResolvePartitionDequeForTest("bench-topic", partition);

                if (partition == 0)
                    _partition0Checksum = checksum;
                else
                    _partition16Checksum = checksum;
            }
            catch (Exception exception)
            {
                Interlocked.CompareExchange(ref _workerException, exception, null);
            }
            finally
            {
                done.Set();
            }
        }
    }

    private void RunWorkers()
    {
        _partition0Start.Set();
        _partition16Start.Set();
        _partition0Done.WaitOne();
        _partition16Done.WaitOne();
    }

    private static void RunWorker(AutoResetEvent start, AutoResetEvent done)
    {
        start.Set();
        done.WaitOne();
    }
}
