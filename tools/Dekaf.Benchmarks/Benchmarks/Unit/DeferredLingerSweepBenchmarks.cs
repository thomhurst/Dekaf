using BenchmarkDotNet.Attributes;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures a linger wake after an expired partial batch has been deferred behind an
/// earlier in-flight batch. The steady state models small batches under sustained load.
/// </summary>
[MemoryDiagnoser]
public class DeferredLingerSweepBenchmarks
{
    private RecordAccumulator _accumulator = null!;
    private RecordAccumulator _reactivationAccumulator = null!;
    private ReadyBatch _predecessor = null!;

    [GlobalSetup]
    public void Setup()
    {
        _accumulator = CreateAccumulator();
        _reactivationAccumulator = CreateAccumulator();

        Append(_accumulator, "first"u8);
        if (!_accumulator.TryDrainBatch(out _))
            throw new InvalidOperationException("Expected the first batch to enter the pipeline.");

        Append(_accumulator, "second"u8);
        ExpireLinger(_accumulator);

        Append(_reactivationAccumulator, "first"u8);
        if (!_reactivationAccumulator.TryDrainBatch(out var predecessor))
            throw new InvalidOperationException("Expected a reactivation predecessor.");
        _predecessor = predecessor!;
        Append(_reactivationAccumulator, "second"u8);
        ExpireLinger(_reactivationAccumulator);
    }

    private static RecordAccumulator CreateAccumulator() => new(new ProducerOptions
    {
        BootstrapServers = ["localhost:9092"],
        BufferMemory = ulong.MaxValue,
        BatchSize = 100_000,
        LingerMs = 0,
        EnableIdempotence = false,
        MaxInFlightRequestsPerConnection = 2
    });

    [Benchmark]
    public void ExpiredBatchBlockedByPipelineBatch() => ExpireLinger(_accumulator);

    [Benchmark]
    public void ReactivatedBatchSealsAfterPredecessorExit()
    {
        _predecessor.CompleteSend(baseOffset: 0, timestamp: DateTimeOffset.UtcNow);
        _reactivationAccumulator.ReleaseBatchMemory(_predecessor);
        _reactivationAccumulator.OnBatchExitsPipeline(_predecessor);
        _reactivationAccumulator.ReturnReadyBatch(_predecessor);
        ExpireLinger(_reactivationAccumulator);
        if (!_reactivationAccumulator.TryDrainBatch(out var successor))
            throw new InvalidOperationException("Expected the reactivated batch to seal.");
        _predecessor = successor!;

        // Restore the same deferred state for the next invocation without iteration setup.
        Append(_reactivationAccumulator, "next"u8);
        ExpireLinger(_reactivationAccumulator);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _accumulator.DisposeAsync();
        await _reactivationAccumulator.DisposeAsync();
    }

    private static void Append(RecordAccumulator accumulator, ReadOnlySpan<byte> value)
    {
        var append = accumulator.AppendFromSpansAsync(
            "benchmark-topic",
            partition: 0,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ReadOnlySpan<byte>.Empty,
            keyIsNull: true,
            value,
            valueIsNull: false,
            headers: null,
            headerCount: 0,
            callback: null,
            CancellationToken.None,
            partitionCount: 1);

        if (!append.IsCompletedSuccessfully || !append.Result)
            throw new InvalidOperationException("Expected synchronous append completion.");
    }

    private static void ExpireLinger(RecordAccumulator accumulator)
    {
        var expire = accumulator.ExpireLingerAsync(CancellationToken.None);
        if (!expire.IsCompletedSuccessfully)
            expire.GetAwaiter().GetResult();
    }
}
