using System.Reflection;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using Dekaf.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Exact remote hints at the end of small and large ownership snapshots.</summary>
[MemoryDiagnoser]
public class OutboxRemoteOwnershipBenchmarks
{
    [Params(8, 1024, 1000000)]
    public int OwnedCount { get; set; }

    [Params(false, true)]
    public bool Miss { get; set; }

    private ServiceProvider _provider = null!;
    private Action<int> _receive = null!;
    private ChannelReader<byte> _reader = null!;
    private int _bucket;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddDekafOutboxRelay(new OutboxRelayOptions { BucketCount = OwnedCount * 2 });
        _provider = services.BuildServiceProvider();
        var notifier = (IOutboxBucketNotifier)_provider.GetRequiredService<IOutboxNotifier>();
        var owned = new int[OwnedCount];
        for (var index = 0; index < owned.Length; index++)
            owned[index] = index * 2;
        notifier.SetOwnedBuckets(owned);
        _bucket = owned[^1] + (Miss ? 1 : 0);
        _receive = notifier.GetType().GetMethod("NotifyReceived", BindingFlags.NonPublic | BindingFlags.Instance)!
            .CreateDelegate<Action<int>>(notifier);
        _reader = ((Channel<byte>)notifier.GetType().GetField("_notifications", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(notifier)!).Reader;
        if (Receive() == Miss)
            throw new InvalidOperationException("Only owned bucket hints should signal the relay.");
    }

    [Benchmark]
    public bool Receive()
    {
        _receive(_bucket);
        return _reader.TryRead(out _);
    }

    [GlobalCleanup]
    public void Cleanup() => _provider.Dispose();
}
