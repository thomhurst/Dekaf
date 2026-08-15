using System.Diagnostics;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 3)]
public class WaveCoalesceProbeBenchmarks
{
    private readonly long _expiredDeadline = 0;
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    [GlobalSetup]
    public void Setup() => _channel.Writer.TryWrite(1);

    [Benchmark(Baseline = true)]
    public bool DeadlineFirst()
    {
        if (Stopwatch.GetTimestamp() >= _expiredDeadline)
            return false;

        return _channel.Reader.TryRead(out _);
    }

    [Benchmark]
    public bool ProbeFirst()
    {
        if (!_channel.Reader.TryRead(out var item))
            return false;

        return _channel.Writer.TryWrite(item);
    }
}

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class WaveCoalesceTailBenchmarks
{
    private long _configuredQuietTicks = 1_000;
    private long _maximumArrivalGapTicks = 100;
    private int _additionalBatchCount = 2;

    [Benchmark(Baseline = true)]
    public long FixedTail() => _configuredQuietTicks;

    [Benchmark]
    public long AdaptiveTail() => BrokerSender.SelectWaveCoalesceTailTicks(
        _configuredQuietTicks,
        _maximumArrivalGapTicks,
        _additionalBatchCount);
}
