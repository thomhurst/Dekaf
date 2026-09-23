using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Covers the per-batch pipeline bookkeeping that the FlushAsync checkpoint (#3386) extends:
/// every sealed batch takes an entry sequence when it joins the in-flight list, and every exit
/// reports whether the list head moved past a waiting flush's checkpoint.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BatchLifecycle"/> appends small records so almost every append seals a batch, and
/// retires the batches in FIFO order while keeping a few in flight, so both head and non-head
/// exits run. Single-threaded and broker-free: the benchmark thread drains and completes each
/// batch (CompleteSend before the pool return), so the cost is deterministic. Expected: 0 B per
/// append.
/// </para>
/// <para>
/// <see cref="FlushWithDelivery"/> is one flush of a partial batch: seal, wait at the checkpoint,
/// then the batch exits and wakes the flush. Per flush it allocates the async state machine and
/// the wait source, as before; nothing in it is per message.
/// </para>
/// <para>
/// <see cref="AppendWorkerHandoff"/> is the backpressure handoff: records queued for the append
/// workers (as a backpressured ProduceAsync does), then a flush that waits for the workers to
/// append them. Each handoff counts itself in and out of its worker's sequence so the flush
/// covers it. Expected: 0 B per handoff; the flush allocations are per flush.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class FlushCheckpointBenchmarks
{
    private const string Topic = "flush-checkpoint-bench";
    private const int AppendsPerInvoke = 4_096;
    private const int FlushesPerInvoke = 64;
    private const int RecordsPerFlush = 8;
    private const int InFlightWindow = 4;
    private const int HandoffsPerInvoke = 256;

    private RecordAccumulator _accumulator = null!;
    private byte[] _valueBytes = null!;
    private readonly Queue<ReadyBatch> _inFlight = new(InFlightWindow + 1);
    private long _nextOffset;
    private readonly CancellationTokenSource _workerCts = new();
    private readonly ValueTaskSourcePool<RecordMetadata> _completionPool = new();
    private readonly ValueTask<RecordMetadata>[] _handoffs = new ValueTask<RecordMetadata>[HandoffsPerInvoke];

    [GlobalSetup]
    public void Setup()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            // Two records per batch: nearly every other append rotates a batch into the pipeline.
            BatchSize = 192,
            BufferMemory = 64L * 1024 * 1024,
            // Seal on size (lifecycle) or flush only, never on linger.
            LingerMs = 60_000,
            DeliveryLatencyTargetMs = 0,
        };

        _accumulator = new RecordAccumulator(options);
        _accumulator.StartAppendWorkers(_workerCts.Token);
        _valueBytes = new byte[48];

        // Warm the arena, batch and ReadyBatch pools.
        BatchLifecycle();
        FlushWithDelivery();
        AppendWorkerHandoff();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        while (_inFlight.Count > 0)
            Retire(_inFlight.Dequeue());
        _accumulator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _workerCts.Cancel();
        _workerCts.Dispose();
        _completionPool.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(OperationsPerInvoke = AppendsPerInvoke)]
    public void BatchLifecycle()
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < AppendsPerInvoke; i++)
        {
            Append(ts);
            while (_accumulator.TryDrainPublishedBatch(out var batch))
            {
                _inFlight.Enqueue(batch);
                if (_inFlight.Count > InFlightWindow)
                    Retire(_inFlight.Dequeue());
            }
        }
    }

    [Benchmark(OperationsPerInvoke = FlushesPerInvoke)]
    public void FlushWithDelivery()
    {
        // Retire the lifecycle benchmark's window so each flush measures its own batch only.
        while (_inFlight.Count > 0)
            Retire(_inFlight.Dequeue());

        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var flush = 0; flush < FlushesPerInvoke; flush++)
        {
            for (var i = 0; i < RecordsPerFlush; i++)
                Append(ts);

            var pending = _accumulator.FlushAsync(CancellationToken.None);
            while (!pending.IsCompleted)
            {
                if (_accumulator.TryDrainPublishedBatch(out var batch))
                    Retire(batch);
                else
                    Thread.SpinWait(1);
            }

            pending.GetAwaiter().GetResult();
        }
    }

    [Benchmark(OperationsPerInvoke = HandoffsPerInvoke)]
    public void AppendWorkerHandoff()
    {
        while (_inFlight.Count > 0)
            Retire(_inFlight.Dequeue());

        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < HandoffsPerInvoke; i++)
        {
            var completion = _completionPool.Rent();
            _handoffs[i] = completion.Task;
            _accumulator.EnqueueAppend(
                Topic, 0, ts, PooledMemory.Null, PooledMemory.Null,
                null, 0, completion, CancellationToken.None);
        }

        var pending = _accumulator.FlushAsync(CancellationToken.None);
        while (!pending.IsCompleted)
        {
            if (_accumulator.TryDrainPublishedBatch(out var batch))
                Retire(batch);
            else
                Thread.SpinWait(1);
        }

        pending.GetAwaiter().GetResult();
        for (var i = 0; i < HandoffsPerInvoke; i++)
            _handoffs[i].GetAwaiter().GetResult();
    }

    private void Append(long ts)
    {
        var task = _accumulator.AppendFromSpansAsync(
            Topic, 0, ts,
            ReadOnlySpan<byte>.Empty, true, _valueBytes, false,
            null, 0, null, CancellationToken.None);

        if (!task.IsCompleted)
            throw new InvalidOperationException("Append left the synchronous path; the fixture fell behind.");

        task.GetAwaiter().GetResult();
    }

    private void Retire(ReadyBatch batch)
    {
        batch.CompleteSend(_nextOffset, DateTimeOffset.UnixEpoch);
        _nextOffset += batch.RecordCount;
        _accumulator.ReleaseBatchMemory(batch);
        _accumulator.OnBatchExitsPipeline(batch);
        _accumulator.ReturnReadyBatch(batch);
    }
}
