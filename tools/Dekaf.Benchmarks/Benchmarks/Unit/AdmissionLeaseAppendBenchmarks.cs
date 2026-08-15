using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures both headerless span entry points with broker admission enabled. A background
/// drainer acknowledges sealed batches so the benchmark remains on the synchronous lease path.
/// </summary>
[MemoryDiagnoser]
[OperationsPerSecond]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class AdmissionLeaseAppendBenchmarks
{
    private const int BatchSize = 1_048_576;
    private const int OperationsPerInvocation = 100;

    private RecordAccumulator _accumulator = null!;
    private ValueTaskSourcePool<RecordMetadata> _completionPool = null!;
    private byte[] _keyBytes = null!;
    private byte[] _valueBytes = null!;
    private CancellationTokenSource _drainerCts = null!;
    private Task _drainerTask = null!;

    [GlobalSetup]
    public void Setup()
    {
        _accumulator = new RecordAccumulator(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                BatchSize = BatchSize,
                BufferMemory = 256L * 1024 * 1024,
                LingerMs = 0,
                DeliveryLatencyTargetMs = 10,
                UnackedByteBudgetCapOverride = 1L << 30,
            },
            resolveLeaderId: static (_, _) => 0);
        _completionPool = new ValueTaskSourcePool<RecordMetadata>();
        _keyBytes = Encoding.UTF8.GetBytes("benchmark-key-0");
        _valueBytes = new byte[1_000];

        var pooledSources = new PooledValueTaskSource<RecordMetadata>[_completionPool.MaxPoolSize];
        for (var i = 0; i < pooledSources.Length; i++)
            pooledSources[i] = _completionPool.Rent();
        for (var i = 0; i < pooledSources.Length; i++)
            _completionPool.Return(pooledSources[i]);

        _drainerCts = new CancellationTokenSource();
        _drainerTask = Task.Run(() => DrainLoop(_drainerCts.Token));

        for (var i = 0; i < 40; i++)
            AppendFromSpans();
        for (var i = 0; i < 40; i++)
            AppendFromSpansWithCompletion();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _drainerCts.Cancel();
        try { await _drainerTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        _drainerCts.Dispose();
        await _accumulator.DisposeAsync().ConfigureAwait(false);
        await _completionPool.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvocation)]
    public void AppendFromSpans()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < OperationsPerInvocation; i++)
        {
            var result = _accumulator.AppendFromSpansAsync(
                "bench-topic", 0, timestamp,
                _keyBytes, false, _valueBytes, false,
                null, 0, null, CancellationToken.None);
            if (!result.IsCompletedSuccessfully || !result.Result)
                throw new InvalidOperationException("Span append left the synchronous admission path.");
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvocation)]
    public void AppendFromSpansWithCompletion()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < OperationsPerInvocation; i++)
        {
            var completion = _completionPool.Rent();
            completion.SetRunContinuationsAsynchronously(false);
            if (!_accumulator.TryAppendFromSpansWithCompletion(
                "bench-topic", 0, timestamp,
                _keyBytes, false, _valueBytes, false,
                null, 0, completion))
            {
                _completionPool.Return(completion);
                throw new InvalidOperationException("Completion append left the synchronous admission path.");
            }

            completion.ObserveForFireAndForget();
        }
    }

    private void DrainLoop(CancellationToken cancellationToken)
    {
        var spinner = new SpinWait();
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_accumulator.TryDrainBatch(out var batch))
            {
                spinner.SpinOnce();
                continue;
            }

            var dataSize = batch.DataSize;
            _accumulator.OnBatchExitsPipeline(batch);
            batch.CompleteSend(0, DateTimeOffset.UtcNow);
            _accumulator.ReleaseMemory(dataSize);
            _accumulator.ReturnReadyBatch(batch);
            spinner.Reset();
        }
    }
}
