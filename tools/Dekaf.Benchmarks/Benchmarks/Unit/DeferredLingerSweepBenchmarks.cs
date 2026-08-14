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

    [GlobalSetup]
    public void Setup()
    {
        _accumulator = new RecordAccumulator(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BufferMemory = ulong.MaxValue,
            BatchSize = 100_000,
            LingerMs = 0,
            EnableIdempotence = false,
            MaxInFlightRequestsPerConnection = 2
        });

        Append("first"u8);
        if (!_accumulator.TryDrainBatch(out _))
            throw new InvalidOperationException("Expected the first batch to enter the pipeline.");

        Append("second"u8);
        ExpireLinger();
    }

    [Benchmark]
    public void ExpiredBatchBlockedByPipelineBatch() => ExpireLinger();

    [GlobalCleanup]
    public async Task Cleanup() => await _accumulator.DisposeAsync();

    private void Append(ReadOnlySpan<byte> value)
    {
        var append = _accumulator.AppendFromSpansAsync(
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

    private void ExpireLinger()
    {
        var expire = _accumulator.ExpireLingerAsync(CancellationToken.None);
        if (!expire.IsCompletedSuccessfully)
            expire.GetAwaiter().GetResult();
    }
}
