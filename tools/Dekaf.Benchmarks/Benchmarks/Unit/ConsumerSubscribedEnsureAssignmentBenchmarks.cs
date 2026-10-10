using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// The steady-state assignment check a subscribed group consumer makes on its poll path
/// (<c>EnsureAssignmentForPollAsync</c>): a stable member, an unchanged subscription and an
/// assignment already synchronized return without the assignment lock or the network. Measures
/// the per-poll cost of that check, including the subscription-generation bookkeeping.
/// </summary>
[MemoryDiagnoser]
public class ConsumerSubscribedEnsureAssignmentBenchmarks
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _consumer = null!;
    private ConsumerCoordinator _coordinator = null!;

    [GlobalSetup]
    public void Setup()
    {
        _consumer = new KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "subscribed-ensure-assignment-benchmark",
                OffsetCommitMode = OffsetCommitMode.Manual
            },
            Serializers.RawBytes,
            Serializers.RawBytes);
        _consumer.Subscribe("benchmark-topic");

        var consumerType = typeof(KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>);
        _coordinator = (ConsumerCoordinator)consumerType.GetField("_coordinator", Flags)!.GetValue(_consumer)!;

        // The coordinator already sends the consumer's subscription (the same set instance, as
        // after a join) and is a stable member; the consumer synchronized its current assignment.
        var topics = consumerType.GetField("_subscriptionSnapshot", Flags)!.GetValue(_consumer)!;
        CoordinatorSubscription.Set(_coordinator, topics);
        typeof(ConsumerCoordinator).GetField("_state", Flags)!.SetValue(_coordinator, CoordinatorState.Stable);
        consumerType.GetField("_lastCoordinatorAssignmentVersion", Flags)!
            .SetValue(_consumer, _coordinator.AssignmentVersion);

        for (var i = 0; i < 2; i++)
        {
            var pending = EnsureAssignmentForPoll();
            if (!pending.IsCompletedSuccessfully)
                throw new InvalidOperationException("The steady assignment check did not complete synchronously.");
            pending.GetAwaiter().GetResult();
        }
    }

    [Benchmark]
    public ValueTask EnsureAssignmentForPoll() =>
        _consumer.EnsureAssignmentForPollAsync(CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        // Not a real member: nothing to leave.
        typeof(ConsumerCoordinator).GetField("_state", Flags)!.SetValue(_coordinator, CoordinatorState.Unjoined);
        _consumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}

/// <summary>
/// Seeds the subscription a <see cref="ConsumerCoordinator"/> sends, across its internal layouts
/// (separate fields, or one immutable subscription snapshot carrying the owner's generation).
/// </summary>
internal static class CoordinatorSubscription
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Set(ConsumerCoordinator coordinator, object topics)
    {
        var type = typeof(ConsumerCoordinator);
        if (type.GetField("_subscribedTopics", Flags) is { } topicsField)
        {
            topicsField.SetValue(coordinator, topics);
            return;
        }

        var stateField = type.GetField("_subscriptionState", Flags)
            ?? throw new InvalidOperationException("No coordinator subscription field found.");
        var generation = (int)(type.GetProperty("SubscriptionGeneration", Flags)?.GetValue(coordinator) ?? 0);
        var state = Activator.CreateInstance(
            stateField.FieldType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [topics, null, generation, 1],
            culture: null)!;
        stateField.SetValue(coordinator, state);
        type.GetField("_lastSentSubscriptionVersion", Flags)?.SetValue(coordinator, 1);
    }
}
