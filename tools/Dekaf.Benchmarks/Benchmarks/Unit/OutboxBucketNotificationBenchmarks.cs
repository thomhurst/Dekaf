using System.Reflection;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using Dekaf.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Commit hints with local versus remote ownership. Reflection binds the optional
/// capability only at setup so the same fixture also compiles against the baseline.
/// Consume any buffered notification each time, keeping both revisions steady-state.
/// </summary>
[MemoryDiagnoser]
public class OutboxBucketNotificationBenchmarks
{
    private static readonly int[] OwnedBuckets = [0, 1];
    private ServiceProvider _provider = null!;
    private Action<IReadOnlySet<int>> _notify = null!;
    private ChannelReader<byte> _reader = null!;
    private readonly HashSet<int> _local = [0, 1];
    private readonly HashSet<int> _remote = [2, 3];

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddDekafOutboxRelay();
        _provider = services.BuildServiceProvider();
        var notifier = _provider.GetRequiredService<IOutboxNotifier>();
        var capability = typeof(IOutboxNotifier).Assembly.GetType("Dekaf.Outbox.IOutboxBucketNotifier");
        if (capability is null)
            _notify = _ => notifier.NotifyCommitted();
        else
        {
            capability.GetMethod("SetOwnedBuckets")!.Invoke(notifier, new object[] { OwnedBuckets });
            _notify = capability.GetMethod("NotifyCommitted", [typeof(IReadOnlySet<int>)])!
                .CreateDelegate<Action<IReadOnlySet<int>>>(notifier);
        }
        _reader = ((Channel<byte>)notifier.GetType().GetField("_notifications", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(notifier)!).Reader;
        if (!LocalCommit())
            throw new InvalidOperationException("A local commit must signal the relay.");
    }

    [Benchmark]
    public bool LocalCommit()
    {
        _notify(_local);
        return _reader.TryRead(out _);
    }

    [Benchmark]
    public bool RemoteCommit()
    {
        _notify(_remote);
        return _reader.TryRead(out _);
    }

    [GlobalCleanup]
    public void Cleanup() => _provider.Dispose();
}
