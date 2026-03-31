using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures per-message allocation in the RecordAccumulator's hot path (AppendFromSpansAsync).
/// No Kafka broker needed — this isolates the accumulator's append + seal logic.
///
/// A background drainer prevents buffer memory from filling, keeping messages on the hot path
/// (TryReserveMemory succeeds → synchronous append → zero async state machine).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, warmupCount: 3, iterationCount: 10)]
public class AccumulatorAppendBenchmarks
{
    private RecordAccumulator _accumulator = null!;
    private byte[] _keyBytes = null!;
    private byte[] _valueBytes = null!;
    private CancellationTokenSource _drainerCts = null!;
    private Task _drainerTask = null!;

    [Params(100, 1000)]
    public int MessageSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 1_048_576, // 1 MB
            BufferMemory = 256L * 1024 * 1024,
            LingerMs = 0,
        };

        _accumulator = new RecordAccumulator(options);
        _keyBytes = Encoding.UTF8.GetBytes("benchmark-key-0");
        _valueBytes = new byte[MessageSize];

        // Warmup: append + drain a few messages to initialize pools and thread-local caches
        for (var i = 0; i < 100; i++)
        {
            _accumulator.AppendFromSpansAsync(
                "bench-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                _keyBytes, false, _valueBytes, false,
                null, 0, null, CancellationToken.None).GetAwaiter().GetResult();
        }
        DrainAll();

        // Start background drainer to prevent buffer from filling
        _drainerCts = new CancellationTokenSource();
        _drainerTask = Task.Run(() => DrainLoop(_drainerCts.Token));
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _drainerCts.Cancel();
        try { await _drainerTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        await _accumulator.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Hot path: TryReserveMemory succeeds → synchronous append, no async state machine.
    /// Expected: zero allocation per call after warmup.
    /// </summary>
    [Benchmark(OperationsPerInvoke = 100)]
    public void AppendHotPath()
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < 100; i++)
        {
            _accumulator.AppendFromSpansAsync(
                "bench-topic", 0, ts,
                _keyBytes, false, _valueBytes, false,
                null, 0, null, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Same as AppendHotPath but spreads across multiple partitions.
    /// Tests whether partition-switching adds allocation (cache misses, new deques).
    /// </summary>
    [Benchmark(OperationsPerInvoke = 100)]
    public void AppendMultiPartition()
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < 100; i++)
        {
            _accumulator.AppendFromSpansAsync(
                "bench-topic", i % 10, ts,
                _keyBytes, false, _valueBytes, false,
                null, 0, null, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    private void DrainAll()
    {
        while (_accumulator.TryDrainBatch(out var batch))
        {
            _accumulator.ReleaseMemory(batch.DataSize);
            _accumulator.ReturnReadyBatch(batch);
        }
    }

    private async Task DrainLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_accumulator.TryDrainBatch(out var batch))
            {
                _accumulator.ReleaseMemory(batch.DataSize);
                _accumulator.ReturnReadyBatch(batch);
            }
            else
            {
                await Task.Delay(1, ct).ConfigureAwait(false);
            }
        }
    }
}
