using Dekaf.Consumer;
#if NETSTANDARD2_0
using TopicPartitionSet = System.Collections.Generic.IReadOnlyCollection<Dekaf.TopicPartition>;
#else
using TopicPartitionSet = System.Collections.Generic.IReadOnlySet<Dekaf.TopicPartition>;
#endif

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
        if (HasPollStateChanged(subscription, observedAssignment))
            return ValueTask.CompletedTask;
        return new ValueTask(changed.WaitAsync(cancellationToken));
    }

    private async ValueTask PrepareMissingLeaderRetryAsync(
        TopicPartitionSet observedAssignment,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var subscription = _subscriptionSnapshot;
        var changed = _coordinator.GetAssignmentChangeTask();
        if (HasPollStateChanged(subscription, observedAssignment))
            return;

        using var retryCancellation = cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : new CancellationTokenSource();
        var retry = PrepareRequestRetryAsync(0, retryCancellation.Token, observedAssignment);
        if (retry.IsCompletedSuccessfully)
        {
            await retry.ConfigureAwait(false);
            return;
        }

        var pending = retry.AsTask();
        try
        {
            if (await Task.WhenAny(pending, changed).ConfigureAwait(false) != pending)
                retryCancellation.Cancel();
        }
        finally
        {
            // Cancel and join the refresh/backoff before another poll or disposal
            // can reuse its state. Caller cancellation and non-cancellation failures
            // still propagate; only this recovery's state-change cancellation ends quietly.
            try
            {
                await pending.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                retryCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
            }
        }
    }

    private bool HasPollStateChanged(
        IReadOnlyCollection<string> observedSubscription,
        IReadOnlyCollection<TopicPartition> observedAssignment)
        => Volatile.Read(ref _closed) != 0 || Volatile.Read(ref _disposed) != 0 ||
           observedSubscription.Count == 0 || !ReferenceEquals(observedSubscription, _subscriptionSnapshot) ||
           _coordinator.State != CoordinatorState.Stable ||
           !ReferenceEquals(observedAssignment, _coordinator.Assignment);
}
