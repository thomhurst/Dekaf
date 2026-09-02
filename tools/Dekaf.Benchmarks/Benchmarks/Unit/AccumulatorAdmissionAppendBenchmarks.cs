using System.Reflection;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Isolates the producer append admission path: <see cref="RecordAccumulator.AppendFromSpansAsync"/>
/// with the per-broker unacked-byte admission window enabled, which is the
/// <c>DeliveryLatencyTargetMs &gt; 0</c> default every real producer runs with.
/// <see cref="AccumulatorAppendBenchmarks"/> constructs its accumulator without a leader resolver
/// and therefore takes the admission-disabled bypass, so it cannot see this path.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Shape"/> covers the uncontended case and the two contended shapes that stress the
/// partition lock differently: four threads appending to one partition (lock convoy) and four
/// threads appending to four distinct partitions (only the accumulator-wide counters are shared).
/// Every invocation appends <see cref="AppendsPerInvoke"/> records in total, split evenly across
/// the shape's threads, so ns/op is comparable between shapes.
/// </para>
/// <para>
/// No broker: a spinning drainer retires sealed batches the way the sender does
/// (drained in publication order via <see cref="RecordAccumulator.TryDrainPublishedBatch"/>;
/// <see cref="RecordAccumulator.OnBatchExitsPipeline"/> returns the broker-window charge,
/// <see cref="RecordAccumulator.ReleaseMemory"/> returns BufferMemory) so appends stay on the
/// synchronous hot path. Batches seal on size (16 per invocation), so rotation is amortized
/// 1:1000 deterministically. Expected allocation: 0 B per append after warmup; a nonzero
/// Allocated column can only come from the drainer-behind cold path (see AccumulatorAppendBenchmarks).
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 3)]
public class AccumulatorAdmissionAppendBenchmarks
{
    private const string Topic = "bench-topic";
    private const int MessageSize = 1000;
    private const int AppendsPerInvoke = 16_000;
    private const int LeaderNodeId = 0;
    private const long FixtureCapacityBytes = 1L << 30;

    public const string SingleThread = "1T";
    public const string FourThreadsSamePartition = "4T-same";
    public const string FourThreadsDistinctPartitions = "4T-split";

    private RecordAccumulator _accumulator = null!;
    private byte[] _keyBytes = null!;
    private byte[] _valueBytes = null!;
    private CancellationTokenSource _drainerCts = null!;
    private Thread _drainerThread = null!;
    private AppendWorker[] _workers = [];
    private Barrier? _barrier;
    private volatile bool _stopWorkers;

    [Params(SingleThread, FourThreadsSamePartition, FourThreadsDistinctPartitions)]
    public string Shape { get; set; } = SingleThread;

    [Params(false, true)]
    public bool AdmissionEnabled { get; set; }

    private int ThreadCount => Shape == SingleThread ? 1 : 4;
    private int PartitionCount => Shape == FourThreadsDistinctPartitions ? 4 : 1;

    [GlobalSetup]
    public void Setup()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 1_048_576, // 1 MB
            BufferMemory = 256L * 1024 * 1024,
            // Seal on size only. With LingerMs = 0 every append seals unless a predecessor batch
            // is still in the pipeline, so batch size — and the amortized rotation cost per
            // append — becomes a race between the appender and the drainer instead of a property
            // of the append path (ProducerFireHotPathBenchmarks pins linger for the same reason).
            LingerMs = 1_000,
            // 10 (the default) enables the admission window; 0 disables it.
            DeliveryLatencyTargetMs = AdmissionEnabled ? 10 : 0,
            // Keep admission non-blocking so this measures bookkeeping, not drainer scheduling.
            UnackedByteBudgetCapOverride = FixtureCapacityBytes,
        };

        _accumulator = AdmissionEnabled
            ? new RecordAccumulator(options, resolveLeaderId: static (_, _) => LeaderNodeId)
            : new RecordAccumulator(options);

        if (AdmissionEnabled && _accumulator.GetBrokerUnackedBudget(LeaderNodeId) is { } budget)
        {
            // The stopped sender cannot provide acknowledgement pacing, so pin the window at the
            // fixture cap while the drainer releases every charge (as ProducerFireHotPathBenchmarks does).
            typeof(BrokerUnackedByteBudget)
                .GetField("_budgetBytes", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(budget, FixtureCapacityBytes);
        }

        _keyBytes = Encoding.UTF8.GetBytes("benchmark-key-0");
        _valueBytes = new byte[MessageSize];

        // Warm the pools (arena, PartitionBatch, ReadyBatch, delivery arrays) by sealing and
        // retiring complete batches on every partition the shape touches.
        var msgsPerBatch = options.BatchSize / (MessageSize + 20);
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var partition = 0; partition < PartitionCount; partition++)
        {
            for (var i = 0; i < msgsPerBatch * 4; i++)
            {
                Append(partition, ts);
                if (i % msgsPerBatch == msgsPerBatch - 1)
                    DrainAll();
            }
            DrainAll();
        }

        _drainerCts = new CancellationTokenSource();
        _drainerThread = new Thread(() => DrainLoop(_drainerCts.Token))
        {
            IsBackground = true,
            Name = "accumulator-admission-drainer",
            Priority = ThreadPriority.Highest,
        };
        _drainerThread.Start();

        if (ThreadCount > 1)
        {
            _barrier = new Barrier(ThreadCount + 1);
            _workers = new AppendWorker[ThreadCount];
            for (var i = 0; i < ThreadCount; i++)
            {
                var partition = PartitionCount == 1 ? 0 : i;
                _workers[i] = new AppendWorker(this, partition, AppendsPerInvoke / ThreadCount, _barrier);
                _workers[i].Start();
            }
        }

        AppendBatch();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var worker in _workers)
            worker.Stop();
        if (_barrier is not null)
        {
            // Release workers parked on the start phase so they observe the stop flag.
            _barrier.SignalAndWait();
            foreach (var worker in _workers)
                worker.Join();
            _barrier.Dispose();
        }

        _drainerCts.Cancel();
        _drainerThread.Join();
        _drainerCts.Dispose();
        _accumulator.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Appends <see cref="AppendsPerInvoke"/> headerless records with the shape's thread count.
    /// Multi-threaded shapes hand the work to persistent workers through a barrier: one phase to
    /// start, one to finish, so no thread creation is measured.
    /// </summary>
    [Benchmark(OperationsPerInvoke = AppendsPerInvoke)]
    public void AppendBatch()
    {
        if (_barrier is null)
        {
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for (var i = 0; i < AppendsPerInvoke; i++)
                Append(partition: 0, ts);
            return;
        }

        _barrier.SignalAndWait(); // start
        _barrier.SignalAndWait(); // finish
    }

    private void Append(int partition, long ts)
    {
        var task = _accumulator.AppendFromSpansAsync(
            Topic, partition, ts,
            _keyBytes, false, _valueBytes, false,
            null, 0, null, CancellationToken.None);

        if (task.IsCompleted)
        {
            task.GetAwaiter().GetResult();
            return;
        }

        // Cold path (drainer fell behind): a pooled IValueTaskSource is not awaitable via
        // GetResult until completed, so block on a Task instead.
        task.AsTask().GetAwaiter().GetResult();
    }

    private void DrainAll()
    {
        while (_accumulator.TryDrainPublishedBatch(out var batch))
            Retire(batch);
    }

    private void Retire(ReadyBatch batch)
    {
        _accumulator.OnBatchExitsPipeline(batch);
        _accumulator.ReleaseMemory(batch.DataSize);
        _accumulator.ReturnReadyBatch(batch);
    }

    private void DrainLoop(CancellationToken cancellationToken)
    {
        var spinner = new SpinWait();
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_accumulator.TryDrainPublishedBatch(out var batch))
            {
                Retire(batch);
                spinner.Reset();
            }
            else
            {
                spinner.SpinOnce();
            }
        }
    }

    private sealed class AppendWorker(
        AccumulatorAdmissionAppendBenchmarks owner,
        int partition,
        int appendsPerInvoke,
        Barrier barrier)
    {
        private readonly Thread _thread = new(() => Run(owner, partition, appendsPerInvoke, barrier))
        {
            IsBackground = true,
            Name = $"accumulator-admission-append-{partition}",
        };

        public void Start() => _thread.Start();
        public void Stop() => owner._stopWorkers = true;
        public void Join() => _thread.Join();

        private static void Run(
            AccumulatorAdmissionAppendBenchmarks owner,
            int partition,
            int appendsPerInvoke,
            Barrier barrier)
        {
            while (true)
            {
                barrier.SignalAndWait(); // start
                if (owner._stopWorkers)
                    return;

                var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                for (var i = 0; i < appendsPerInvoke; i++)
                    owner.Append(partition, ts);

                barrier.SignalAndWait(); // finish
            }
        }
    }
}
