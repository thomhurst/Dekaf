using Dekaf.Consumer;

namespace Dekaf.ShareConsumer;

internal sealed partial class KafkaShareConsumer<TKey, TValue>
{
    private ValueTask WaitForAssignmentChangeAsync(
        IReadOnlyCollection<TopicPartition> observedAssignment,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var subscription = _subscriptionSnapshot;
        var changed = _coordinator.GetAssignmentChangeTask();
        // Register first, then recheck every condition that can end this idle period.
        // Cancellation affects this wait only; a later poll can reuse the same signal.
        if (Volatile.Read(ref _closed) != 0 || Volatile.Read(ref _disposed) != 0 ||
            subscription.Count == 0 || !ReferenceEquals(subscription, _subscriptionSnapshot) ||
            _coordinator.State != CoordinatorState.Stable ||
            !ReferenceEquals(observedAssignment, _coordinator.Assignment))
            return ValueTask.CompletedTask;
        return new ValueTask(changed.WaitAsync(cancellationToken));
    }
}