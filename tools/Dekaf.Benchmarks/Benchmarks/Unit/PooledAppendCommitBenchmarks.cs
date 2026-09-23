using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Steady-state benchmark for the pooled append path (<c>RecordAccumulator.AppendAsync</c> with
/// pooled key/value memory): the path the slow-path append workers and the buffer-memory
/// pending-append drain take, and the commit point that validates a queued produce's
/// transactional append generation under the partition lock.
/// <para>
/// <see cref="Transactional"/> = false calls <c>AppendAsync</c> with its main-branch signature
/// (a non-transactional append, which skips the generation check). <see cref="Transactional"/> =
/// true passes a captured generation through a delegate bound once in setup: on a revision whose
/// <c>AppendAsync</c> takes <c>transactionalGeneration</c> it binds that overload, otherwise it
/// binds a non-capturing wrapper around the older overload, so both revisions build this source
/// and the comparison shows what the generation check costs. A background drainer recycles
/// published batches, so pooled arenas, ready batches and value buffers are reused; nothing is set
/// up per iteration.
/// </para>
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class PooledAppendCommitBenchmarks
{
    private const string Topic = "pooled-append-commit";
    private const int AppendsPerInvoke = 100;
    private const int ValueSize = 256;

    private delegate ValueTask<bool> GenerationAppend(
        RecordAccumulator accumulator,
        string topic,
        int partition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback,
        CancellationToken cancellationToken,
        int partitionCount,
        int transactionalGeneration);

    private RecordAccumulator _accumulator = null!;
    private GenerationAppend _appendWithGeneration = null!;
    private int _generation;
    private CancellationTokenSource _drainerCts = null!;
    private Thread _drainerThread = null!;

    [Params(false, true)]
    public bool Transactional { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _accumulator = new RecordAccumulator(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 1_048_576,
            BufferMemory = 256L * 1024 * 1024,
            LingerMs = 0,
        });

        _appendWithGeneration = BindGenerationAppend();
        _generation = typeof(RecordAccumulator)
            .GetProperty("TransactionalAppendGeneration", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(_accumulator) is int generation
                ? generation
                : 0;

        _drainerCts = new CancellationTokenSource();
        _drainerThread = new Thread(() => DrainLoop(_drainerCts.Token))
        {
            IsBackground = true,
            Name = "pooled-append-commit-drainer",
            Priority = ThreadPriority.Highest,
        };
        _drainerThread.Start();

        // Warm the pools (arenas, partition and ready batches, value buffers) before measuring.
        for (var i = 0; i < 50; i++)
            AppendBatch();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _drainerCts.Cancel();
        _drainerThread.Join();
        _drainerCts.Dispose();
        await _accumulator.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(OperationsPerInvoke = AppendsPerInvoke)]
    public void AppendBatch()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < AppendsPerInvoke; i++)
        {
            var value = RentValue();
            var append = Transactional
                ? _appendWithGeneration(
                    _accumulator, Topic, 0, timestamp, PooledMemory.Null, value, null, 0, null, null,
                    CancellationToken.None, 0, _generation)
                : _accumulator.AppendAsync(
                    Topic, 0, timestamp, PooledMemory.Null, value, null, 0, null, null,
                    CancellationToken.None);
            AwaitSync(append);
        }
    }

    private static PooledMemory RentValue()
    {
        var buffer = ProducerDataPool.BytePool.Rent(ValueSize);
        return new PooledMemory(buffer, ValueSize);
    }

    private static GenerationAppend BindGenerationAppend()
    {
        foreach (var method in typeof(RecordAccumulator).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            if (method.Name != nameof(RecordAccumulator.AppendAsync))
                continue;

            var parameters = method.GetParameters();
            if (parameters.Length == 12 && parameters[11].Name == "transactionalGeneration")
                return method.CreateDelegate<GenerationAppend>();
        }

        // A revision without the generation argument: the same append, without the check.
        return static (accumulator, topic, partition, timestamp, key, value, headers, headerCount,
                completionSource, callback, cancellationToken, partitionCount, _) =>
            accumulator.AppendAsync(topic, partition, timestamp, key, value, headers, headerCount,
                completionSource, callback, cancellationToken, partitionCount);
    }

    /// <summary>
    /// The append completes synchronously while the drainer keeps up; if it falls behind, the
    /// append waits for buffer memory and is blocked on as a Task (cold path).
    /// </summary>
    private static void AwaitSync(ValueTask<bool> append)
    {
        if (append.IsCompleted)
        {
            append.GetAwaiter().GetResult();
            return;
        }

        append.AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Recycles only published batches (<c>TryDrainPublishedBatch</c>, which exists on main too):
    /// the deque-polling drain can take a batch before the sealing thread's last touch
    /// (<c>StartPreSerialization</c>) and return it to the pool under it. The drainer allocates
    /// nothing per batch, which matters because MemoryDiagnoser counts every thread.
    /// </summary>
    private void DrainLoop(CancellationToken cancellationToken)
    {
        var spinner = new SpinWait();
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_accumulator.TryDrainPublishedBatch(out var batch))
            {
                _accumulator.OnBatchExitsPipeline(batch);
                _accumulator.ReleaseMemory(batch.DataSize);
                _accumulator.ReturnReadyBatch(batch);
                spinner.Reset();
            }
            else
            {
                spinner.SpinOnce();
            }
        }
    }
}
