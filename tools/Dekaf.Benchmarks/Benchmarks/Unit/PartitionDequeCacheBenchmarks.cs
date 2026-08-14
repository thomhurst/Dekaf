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
    private const int ResolutionsPerWorker = 100;

    private RecordAccumulator _accumulator = null!;
    private AutoResetEvent _partition0Start = null!;
    private AutoResetEvent _partition16Start = null!;
    private AutoResetEvent _partition0Done = null!;
    private AutoResetEvent _partition16Done = null!;
    private Thread _partition0Worker = null!;
    private Thread _partition16Worker = null!;
    private volatile bool _stopWorkers;
    private Exception? _workerException;

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

        _accumulator.SetPartitionQueueBytesForTest("bench-topic", partition: 0, bytes: 0);
        _accumulator.SetPartitionQueueBytesForTest("bench-topic", partition: 16, bytes: 0);

        _partition0Start = new AutoResetEvent(false);
        _partition16Start = new AutoResetEvent(false);
        _partition0Done = new AutoResetEvent(false);
        _partition16Done = new AutoResetEvent(false);
        _partition0Worker = StartWorker(partition: 0, _partition0Start, _partition0Done);
        _partition16Worker = StartWorker(partition: 16, _partition16Start, _partition16Done);

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
    [Benchmark(OperationsPerInvoke = ResolutionsPerWorker)]
    public void ResolveColdCacheCollision()
    {
        for (var i = 0; i < ResolutionsPerWorker; i++)
            _accumulator.SetPartitionQueueBytesForTest("bench-topic", (i & 1) << 4, bytes: 0);
    }

    /// <summary>
    /// Resolves colliding slots concurrently from two partition-affine producer threads.
    /// Worker creation and synchronization objects are outside measurement allocations.
    /// </summary>
    [Benchmark(OperationsPerInvoke = ResolutionsPerWorker * 2)]
    public void ResolveConcurrentCacheCollision()
    {
        RunWorkers();

        if (_workerException is { } exception)
            throw new InvalidOperationException("Partition-deque cache worker failed.", exception);
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
                for (var i = 0; i < ResolutionsPerWorker; i++)
                    _accumulator.SetPartitionQueueBytesForTest("bench-topic", partition, bytes: 0);
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
}
