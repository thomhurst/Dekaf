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
    private const int CompletionSourceCount = 4_096;

    private RecordAccumulator _accumulator = null!;
    private IDisposable _bulkScope = null!;
    private PooledValueTaskSource<RecordMetadata>[] _completionSources = null!;
    private int _completionSourceIndex;
    private byte[] _keyBytes = null!;
    private byte[] _valueBytes = null!;
    private CancellationTokenSource _drainerCts = null!;
    private Thread _drainerThread = null!;

    [GlobalSetup]
    public void Setup()
    {
        _accumulator = new RecordAccumulator(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                BatchSize = BatchSize,
                BufferMemory = 256L * 1024 * 1024,
                LingerMs = 1_000,
                DeliveryLatencyTargetMs = 10,
                UnackedByteBudgetCapOverride = 1L << 30,
            },
            resolveLeaderId: static (_, _) => 0);
        _bulkScope = _accumulator.EnterBulkProduceScope();
        _completionSources = new PooledValueTaskSource<RecordMetadata>[CompletionSourceCount];
        for (var i = 0; i < _completionSources.Length; i++)
            _completionSources[i] = new PooledValueTaskSource<RecordMetadata>();
        _keyBytes = Encoding.UTF8.GetBytes("benchmark-key-0");
        _valueBytes = new byte[1_000];

        _drainerCts = new CancellationTokenSource();
        _drainerThread = new Thread(() => DrainLoop(_drainerCts.Token))
        {
            IsBackground = true,
            Name = "admission-lease-benchmark-drainer",
            Priority = ThreadPriority.Highest,
        };
        _drainerThread.Start();

        for (var i = 0; i < 40; i++)
            AppendFromSpans();
        for (var i = 0; i < 40; i++)
            AppendFromSpansWithCompletion();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _drainerCts.Cancel();
        _drainerThread.Join();
        _drainerCts.Dispose();
        _bulkScope.Dispose();
        await _accumulator.DisposeAsync().ConfigureAwait(false);
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
            while (!result.IsCompleted)
                Thread.SpinWait(32);
            if (!result.Result)
                throw new InvalidOperationException("Span append failed.");
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvocation)]
    public void AppendFromSpansWithCompletion()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < OperationsPerInvocation; i++)
        {
            var completion = _completionSources[_completionSourceIndex++ & (CompletionSourceCount - 1)];
            completion.SetRunContinuationsAsynchronously(false);
            while (!_accumulator.TryAppendFromSpansWithCompletion(
                       "bench-topic", 0, timestamp,
                       _keyBytes, false, _valueBytes, false,
                       null, 0, completion))
                Thread.SpinWait(32);

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
