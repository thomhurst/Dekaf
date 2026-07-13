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
            floorBytes: 1,
            initialCapBytes: 32 * 1024 * 1024);
        _timestamp = Stopwatch.GetTimestamp();
    }

    [Benchmark(OperationsPerInvoke = Operations)]
    public long RecordAcknowledgements()
    {
        for (var i = 0; i < Operations; i++)
        {
            var snapshotAtSend = _budget.SnapshotDelivery(
                _timestamp,
                appLimited: false,
                oldestBatchTimestamp: _timestamp - RttTicks);
            _timestamp += RttTicks;
            _budget.OnAcked(ackedBytes: 1024 * 1024, snapshotAtSend, _timestamp);
        }

        _budget.CompleteAckedPass(_timestamp);
        return _budget.BudgetBytes;
    }

    /// <summary>
    /// Models the production response-pass shape: a handful of acks drained per pass followed
    /// by one budget publish, so the per-message cost of the publish shows up amortized.
    /// </summary>
    [Benchmark(OperationsPerInvoke = Operations)]
    public long RecordAcknowledgementPasses()
    {
        const int acksPerPass = 5;
        for (var i = 0; i < Operations / acksPerPass; i++)
        {
            for (var j = 0; j < acksPerPass; j++)
            {
                var snapshotAtSend = _budget.SnapshotDelivery(
                    _timestamp,
                    appLimited: false,
                    oldestBatchTimestamp: _timestamp - RttTicks);
                _timestamp += RttTicks;
                _budget.OnAcked(ackedBytes: 1024 * 1024, snapshotAtSend, _timestamp);
            }

            _budget.CompleteAckedPass(_timestamp);
        }

        return _budget.BudgetBytes;
    }
}
