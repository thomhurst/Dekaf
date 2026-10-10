using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// The stable-member <see cref="ConsumerCoordinator.EnsureActiveGroupAsync(IReadOnlySet{string}, string?, CancellationToken)"/>
/// check that every prefetch loop iteration and poll makes: a joined member with an unchanged
/// subscription and no queued rebalance callbacks returns without touching the network. The
/// check includes the membership state reads that decide whether a leave (Unsubscribe or a switch
/// to manual assignment) is in progress; both entry points of the stable path are measured.
/// </summary>
[MemoryDiagnoser]
public class ConsumerCoordinatorStablePollBenchmarks
{
    private ConsumerCoordinator _coordinator = null!;
    private HashSet<string> _topics = null!;

    [GlobalSetup]
    public void Setup()
    {
        _topics = ["benchmark-topic"];
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "benchmark-group"
        };

        // The stable path never reaches the connection pool or metadata.
        _coordinator = new ConsumerCoordinator(options, null!, null!);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        CoordinatorSubscription.Set(_coordinator, _topics);
        typeof(ConsumerCoordinator).GetField("_state", flags)!.SetValue(_coordinator, CoordinatorState.Stable);

        var pending = EnsureActiveGroup();
        if (!pending.IsCompletedSuccessfully)
            throw new InvalidOperationException("The stable EnsureActiveGroupAsync path did not complete synchronously.");
    }

    [Benchmark]
    public ValueTask EnsureActiveGroup() =>
        _coordinator.EnsureActiveGroupAsync(_topics, null, CancellationToken.None);

    [Benchmark]
    public ValueTask EnsureActiveGroupTopicsOnly() =>
        _coordinator.EnsureActiveGroupAsync(_topics, CancellationToken.None);
}
