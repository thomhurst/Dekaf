using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using Dekaf.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Transport-enabled commits through the real notifier, draining both local hints and
/// the outbound buffer each invocation. Measures commit/coalescing work without network
/// or worker scheduling. Requires the additive transport API; pre-transport revisions
/// cannot run this fixture, while subsequent revisions compare the same steady workload.
/// </summary>
[MemoryDiagnoser]
public class OutboxTransportNotificationBenchmarks
{
    private static readonly int[] Owned = [0];
    [Params(8, 64)]
    public int BucketCount { get; set; }

    [Params(2, 8)]
    public int CommittedCount { get; set; }

    [Params("HashSet", "SortedSet", "ImmutableHashSet")]
    public string SetType { get; set; } = "HashSet";

    private ServiceProvider _provider = null!;
    private IOutboxBucketNotifier _notifier = null!;
    private IReadOnlySet<int> _committed = null!;
    private ChannelReader<byte> _localSignal = null!;
    private DrainHints _drainLocal = null!;
    private Func<Memory<int>, CancellationToken, ValueTask<int>> _drainRemote = null!;
    private int[] _local = null!;
    private int[] _remote = null!;
    private delegate int DrainHints(Span<int> destination, out bool unknown);

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddDekafOutboxRelay(new OutboxRelayOptions { BucketCount = BucketCount });
        _provider = services.BuildServiceProvider();
        _notifier = (IOutboxBucketNotifier)_provider.GetRequiredService<IOutboxNotifier>();
        _notifier.SetOwnedBuckets(Owned);
        var committed = new int[CommittedCount];
        for (var index = 0; index < committed.Length - 1; index++)
            committed[index] = index;
        committed[^1] = BucketCount - 1;
        _committed = SetType switch
        {
            "HashSet" => new HashSet<int>(committed),
            "SortedSet" => new SortedSet<int>(committed),
            _ => ImmutableHashSet.CreateRange(committed)
        };
        _local = new int[BucketCount];
        _remote = new int[BucketCount];
        // Bind implementation details once. No reflection occurs in the measured path.
        var type = typeof(IOutboxNotificationTransport).Assembly.GetType("Dekaf.Outbox.OutboxRemoteNotifications")!;
        var outbound = Activator.CreateInstance(type, BucketCount)!;
        _notifier.GetType().GetMethod("SetRemoteNotifications", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(_notifier, [outbound]);
        _drainRemote = type.GetMethod("ReadAsync")!
            .CreateDelegate<Func<Memory<int>, CancellationToken, ValueTask<int>>>(outbound);
        _drainLocal = _notifier.GetType().GetMethod("DrainHints", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<DrainHints>(_notifier);
        _localSignal = ((Channel<byte>)_notifier.GetType().GetField("_notifications", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(_notifier)!).Reader;
        var count = CommitAndDrain().GetAwaiter().GetResult();
        if (count != CommittedCount && !(count == 1 && _remote[0] == -1))
            throw new InvalidOperationException("Committed buckets must reach the transport buffer as exact or unknown hints.");
    }

    [Benchmark]
    public ValueTask<int> CommitAndDrain()
    {
        _notifier.NotifyCommitted(_committed);
        _localSignal.TryRead(out _);
        _drainLocal(_local, out _);
        return _drainRemote(_remote, default);
    }

    [GlobalCleanup]
    public void Cleanup() => _provider.Dispose();
}
