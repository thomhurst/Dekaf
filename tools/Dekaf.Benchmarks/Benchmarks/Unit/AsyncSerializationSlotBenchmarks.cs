using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Verifies the uncontended async-serialization admission primitive remains allocation-free.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 3)]
public class AsyncSerializationSlotBenchmarks
{
    private readonly SemaphoreSlim _slots = new(128, 128);

    [Benchmark(OperationsPerInvoke = 1000)]
    public void AcquireReleaseUncontended()
    {
        for (var i = 0; i < 1000; i++)
        {
            if (!_slots.Wait(0, CancellationToken.None))
                throw new InvalidOperationException("Uncontended slot acquisition failed.");

            _slots.Release();
        }
    }
}
