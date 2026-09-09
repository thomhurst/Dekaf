using BenchmarkDotNet.Attributes;
using Dekaf.Networking;

namespace Dekaf.Benchmarks;

[MemoryDiagnoser]
public class PendingRequestBenchmark
{
    private PendingRequestPool _pool = null!;

    [GlobalSetup]
    public void Setup()
    {
        _pool = new PendingRequestPool();
        RentReturn();
        Verify();
    }

    [Benchmark]
    public int RentReturn()
    {
        var request = _pool.Rent();
        _pool.Return(request);
        return _pool.ApproximateCount;
    }

    [GlobalCleanup]
    public void Verify()
    {
        if (_pool.ApproximateCount != 1)
            throw new InvalidOperationException("Pool count changed after successful return.");
    }
}
