using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;
using Dekaf.Protocol.Records;
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
/// <para>
/// <see cref="AppendRotationIntoFullBatch"/> covers the pooled core's rotation exit (#3807): a
/// small record opens a lingering batch, and the next record does not fit beside it, so it rotates
/// that batch and fills the new one exactly, which seals it in the same call. Both batches publish
/// from one append, and the rotated batch's seal refund goes back to buffer memory on that exit.
/// </para>
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class PooledAppendCommitBenchmarks
{
    private const string Topic = "pooled-append-commit";
    private const int AppendsPerInvoke = 100;
    private const int ValueSize = 256;
    private const int SmallValueSize = 1;

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
    private RecordAccumulator _rotationAccumulator = null!;
    private GenerationAppend _appendWithGeneration = null!;
    private int _generation;
    private int _rotationGeneration;
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

        // Every batch holds exactly one ValueSize record, and LingerMs keeps a small record's
        // batch open until the next record rotates it. Unbounded buffer memory keeps a revision
        // that leaks the rotation refund from back-pressuring the run to a stop.
        _rotationAccumulator = new RecordAccumulator(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = RecordBatch.TotalBatchHeaderSize + EncodedRecordSize(ValueSize),
            BufferMemory = ulong.MaxValue,
            LingerMs = 10_000,
        });

        _appendWithGeneration = BindGenerationAppend();
        _generation = ReadGeneration(_accumulator);
        _rotationGeneration = ReadGeneration(_rotationAccumulator);

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
        {
            AppendBatch();
            AppendRotationIntoFullBatch();
        }
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _drainerCts.Cancel();
        _drainerThread.Join();
        _drainerCts.Dispose();
        await _accumulator.DisposeAsync().ConfigureAwait(false);
        await _rotationAccumulator.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(OperationsPerInvoke = AppendsPerInvoke)]
    public void AppendBatch()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < AppendsPerInvoke; i++)
            Append(_accumulator, _generation, timestamp, ValueSize);
    }

    [Benchmark(OperationsPerInvoke = AppendsPerInvoke)]
    public void AppendRotationIntoFullBatch()
    {
        // One timestamp per invoke keeps every record's encoded size constant, so each filling
        // record fills its batch exactly.
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < AppendsPerInvoke; i += 2)
        {
            Append(_rotationAccumulator, _rotationGeneration, timestamp, SmallValueSize);
            Append(_rotationAccumulator, _rotationGeneration, timestamp, ValueSize);
        }
    }

    private void Append(RecordAccumulator accumulator, int generation, long timestamp, int valueSize)
    {
        var value = RentValue(valueSize);
        var append = Transactional
            ? _appendWithGeneration(
                accumulator, Topic, 0, timestamp, PooledMemory.Null, value, null, 0, null, null,
                CancellationToken.None, 0, generation)
            : accumulator.AppendAsync(
                Topic, 0, timestamp, PooledMemory.Null, value, null, 0, null, null,
                CancellationToken.None);
        AwaitSync(append);
    }

    private static PooledMemory RentValue(int valueSize)
    {
        var buffer = ProducerDataPool.BytePool.Rent(valueSize);
        return new PooledMemory(buffer, valueSize);
    }

    private static int ReadGeneration(RecordAccumulator accumulator) =>
        typeof(RecordAccumulator)
            .GetProperty("TransactionalAppendGeneration", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(accumulator) is int generation
                ? generation
                : 0;

    private static int EncodedRecordSize(int valueLength)
    {
        var bodySize = Record.ComputeBodySize(
            timestampDelta: 0,
            offsetDelta: 0,
            isKeyNull: true,
            keyLength: 0,
            isValueNull: false,
            valueLength,
            headers: null,
            headerCount: 0);
        return Record.VarIntSize(bodySize) + bodySize;
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
            // Non-short-circuit OR: drain one batch from each accumulator per pass.
            if (TryRecycle(_accumulator) | TryRecycle(_rotationAccumulator))
                spinner.Reset();
            else
                spinner.SpinOnce();
        }
    }

    private static bool TryRecycle(RecordAccumulator accumulator)
    {
        if (!accumulator.TryDrainPublishedBatch(out var batch))
            return false;

        accumulator.OnBatchExitsPipeline(batch);
        accumulator.ReleaseMemory(batch.DataSize);
        accumulator.ReturnReadyBatch(batch);
        return true;
    }
}
