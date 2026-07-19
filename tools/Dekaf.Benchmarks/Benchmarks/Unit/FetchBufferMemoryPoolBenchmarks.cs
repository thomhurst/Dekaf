using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[ShortRunJob]
public class FetchBufferMemoryPoolBenchmarks
{
    private FetchBufferMemoryPool _pool = null!;

    [GlobalSetup]
    public void Setup()
    {
        _pool = new FetchBufferMemoryPool(100 * 1024 * 1024);

        // Seed the pooled reservation owner before measurement.
        var reservation = _pool.ReserveAsync(1024, CancellationToken.None).Result;
        reservation.Dispose();
    }

    [GlobalCleanup]
    public void Cleanup() => _pool.Dispose();

    [Benchmark]
    public long ReserveAndRelease()
    {
        var reservation = _pool.ReserveAsync(1024, CancellationToken.None);
        if (!reservation.IsCompletedSuccessfully)
            throw new InvalidOperationException("Uncontended reservation did not complete synchronously");

        reservation.Result.Dispose();
        return _pool.UsedBytes;
    }
}
