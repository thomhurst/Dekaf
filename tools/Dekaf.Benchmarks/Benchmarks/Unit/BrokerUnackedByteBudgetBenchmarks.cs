using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures allocation and CPU cost of the producer acknowledgement budget update.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class BrokerUnackedByteBudgetBenchmarks
{
    private const int Operations = 1_000;
    private static readonly long RttTicks = Stopwatch.Frequency / 1_000;

    private BrokerUnackedByteBudget _budget = null!;
    private long _timestamp;

    [GlobalSetup]
    public void Setup()
    {
        _budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 2 * 1024 * 1024,
            initialCapBytes: 32 * 1024 * 1024);
        _timestamp = Stopwatch.GetTimestamp();
    }

    [Benchmark(OperationsPerInvoke = Operations)]
    public long RecordAcknowledgements()
    {
        for (var i = 0; i < Operations; i++)
        {
            _timestamp += RttTicks;
            _budget.OnAcked(ackedBytes: 1024 * 1024, RttTicks, _timestamp);
        }

        return _budget.BudgetBytes;
    }
}
