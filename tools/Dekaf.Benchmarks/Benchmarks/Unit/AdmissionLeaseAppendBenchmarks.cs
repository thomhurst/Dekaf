using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures both headerless span entry points while one open batch repeatedly consumes and
/// replenishes broker admission leases. Iteration setup keeps sealing and delivery outside
/// the measured append path.
/// </summary>
[MemoryDiagnoser]
[OperationsPerSecond]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class AdmissionLeaseAppendBenchmarks
{
    private const int BatchSize = 1_048_576;
    private const int OperationsPerInvocation = 900;
    private const int CompletionSourceCount = 1_024;

    private RecordAccumulator _accumulator = null!;
    private IDisposable _bulkScope = null!;
    private PooledValueTaskSource<RecordMetadata>[] _completionSources = null!;
    private int _completionSourceIndex;
    private byte[] _keyBytes = null!;
    private byte[] _valueBytes = null!;
    [GlobalSetup]
    public void Setup()
    {
        _completionSources = new PooledValueTaskSource<RecordMetadata>[CompletionSourceCount];
        for (var i = 0; i < _completionSources.Length; i++)
            _completionSources[i] = new PooledValueTaskSource<RecordMetadata>();
        _keyBytes = Encoding.UTF8.GetBytes("benchmark-key-0");
        _valueBytes = new byte[1_000];
    }

    [IterationSetup]
    public void SetupIteration()
    {
        _completionSourceIndex = 0;
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
        var seeded = _accumulator.AppendFromSpansAsync(
            "bench-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            _keyBytes, false, _valueBytes, false,
            null, 0, null, CancellationToken.None);
        if (!seeded.IsCompletedSuccessfully || !seeded.Result)
            throw new InvalidOperationException("Admission lease seed append failed.");
    }

    [IterationCleanup]
    public void CleanupIteration()
    {
        _bulkScope.Dispose();
        _accumulator.DisposeAsync().GetAwaiter().GetResult();
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
}
