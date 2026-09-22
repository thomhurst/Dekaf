using BenchmarkDotNet.Attributes;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Steady-state share session bookkeeping for one poll or commit: every broker's epoch is read
/// when its request is built and advanced when its response arrives. Session resets are a fault
/// path and are not measured here.
/// </summary>
[MemoryDiagnoser]
public class ShareSessionEpochBenchmarks
{
    private ShareSessionManager _sessions = null!;

    [Params(1, 8)] public int BrokerCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _sessions = new ShareSessionManager();
        for (var brokerId = 0; brokerId < BrokerCount; brokerId++)
            _sessions.IncrementEpoch(brokerId);

        if (ReadAndAdvanceEveryBroker() != BrokerCount)
            throw new InvalidOperationException("Every broker must hold an open share session.");
    }

    [Benchmark]
    public int ReadAndAdvanceEveryBroker()
    {
        var open = 0;
        for (var brokerId = 0; brokerId < BrokerCount; brokerId++)
        {
            if (_sessions.GetSessionEpoch(brokerId) != 0)
                open++;
            _sessions.IncrementEpoch(brokerId);
        }

        return open;
    }

    [Benchmark]
    public int ReadEveryBroker()
    {
        var sum = 0;
        for (var brokerId = 0; brokerId < BrokerCount; brokerId++)
            sum += _sessions.GetSessionEpoch(brokerId) & 1;
        return sum;
    }
}
