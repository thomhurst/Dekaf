using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Internal;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class ProducerRetryWaitBenchmarks
{
    private readonly AsyncAutoResetSignal _signal = new();

    [Benchmark(Baseline = true)]
    public Task TimedPoll() => Task.Delay(1);

    [Benchmark]
    public ValueTask<bool> SignaledWait()
    {
        _signal.Signal();
        return _signal.WaitAsync(100);
    }

    [GlobalCleanup]
    public void Cleanup() => _signal.Dispose();
}

/// <summary>
/// One suspended sender-channel wait and wake. Sender lifetime ends by completing
/// the channel, so ordinary waits can reuse the single-reader channel's waiter.
/// Timed routing-refresh waits still need their individual cancellation token.
/// </summary>
[MemoryDiagnoser]
public class ProducerChannelWaitBenchmarks
{
    private readonly Channel<BrokerSender.SendLoopEvent> _channel =
        Channel.CreateUnbounded<BrokerSender.SendLoopEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    private readonly CancellationTokenSource _lifetime = new();

    [Benchmark(Baseline = true)]
    public ValueTask<bool> CancellableWait() => WaitAndWake(_lifetime.Token);

    [Benchmark]
    public ValueTask<bool> CompletionBoundWait() => WaitAndWake(CancellationToken.None);

    private ValueTask<bool> WaitAndWake(CancellationToken cancellationToken)
    {
        var wait = _channel.Reader.WaitToReadAsync(cancellationToken);
        _channel.Writer.TryWrite(default);
        _channel.Reader.TryRead(out _);
        return wait;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _channel.Writer.TryComplete();
        _lifetime.Dispose();
    }
}
