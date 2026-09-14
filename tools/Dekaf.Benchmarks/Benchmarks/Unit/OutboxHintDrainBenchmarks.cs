using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Hint drains with and without notifications across small and large bucket domains.</summary>
[MemoryDiagnoser]
public class OutboxHintDrainBenchmarks
{
    [Params(8, 1024, 8193, 1000001)]
    public int BucketCount { get; set; }

    private Action<int> _add = null!;
    private Action _addUnknown = null!;
    private DrainHints _drain = null!;
    private readonly int[] _output = new int[1];
    private int _bucket;
    private ServiceProvider _provider = null!;
    private delegate int DrainHints(Span<int> destination, out bool unknown);

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddDekafOutboxRelay(new OutboxRelayOptions { BucketCount = BucketCount });
        _provider = services.BuildServiceProvider();
        var notifier = (IOutboxBucketNotifier)_provider.GetRequiredService<IOutboxNotifier>();
        _add = notifier.NotifyCommitted;
        _addUnknown = notifier.NotifyCommitted;
        _drain = notifier.GetType().GetMethod("DrainHints", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<DrainHints>(notifier);
        _bucket = BucketCount - 1;
    }

    [Benchmark]
    public int EmptyDrain() => _drain(_output, out _);

    [Benchmark]
    public int KnownHintAndDrain()
    {
        _add(_bucket);
        return _drain(_output, out _);
    }

    [Benchmark]
    public bool UnknownHintAndDrain()
    {
        _addUnknown();
        _drain(_output, out var unknown);
        return unknown;
    }

    [GlobalCleanup]
    public void Cleanup() => _provider.Dispose();
}
