using Dekaf.Networking;
using Dekaf.Protocol.Messages;

namespace Dekaf.ShareConsumer;

// A cancelled poll stops waiting for its ShareFetch, but the broker can still complete the
// request and acquire records for this member. Keep observing it and resume it on the next
// poll, as the Java consumer buffers in-flight fetches, instead of stranding those records
// until their acquisition locks expire. A cancellable poll adds one wait per fetch round;
// detaching costs are per cancelled poll. Nothing here is per message.
internal sealed partial class KafkaShareConsumer<TKey, TValue>
{
    private Dictionary<int, KafkaRequestWriteContext>? _fetchWriteContexts;
    private DetachedShareFetch? _detachedFetch;

    private KafkaRequestWriteContext GetFetchWriteContext(int brokerId)
    {
        var contexts = _fetchWriteContexts ??= [];
        if (!contexts.TryGetValue(brokerId, out var context))
        {
            context = new KafkaRequestWriteContext(default);
            contexts.Add(brokerId, context);
        }
        return context;
    }

    private static ValueTask<ShareFetchResponse> SendShareFetchAsync(
        IKafkaConnection connection, ShareFetchRequest request, short version,
        KafkaRequestWriteContext? requestContext, KafkaRequestWriteContext fetchContext,
        CancellationToken cancellationToken)
    {
        // A hosted response budget already keeps the poll waiting for a started request.
        if (requestContext is not { ResponseCancellationToken.CanBeCanceled: true }
            && connection is IKafkaRequestAfterWriteCancellationConnection detachable)
        {
            return detachable.SendWithResponseCancellationAfterWriteAsync<ShareFetchRequest, ShareFetchResponse>(
                request, version, requestContext ?? fetchContext, cancellationToken);
        }

        // Custom connections cannot separate the write from the response wait. Without a
        // context to observe, treat a failure after handing them the request as potentially submitted.
        if (requestContext is null)
            fetchContext.MarkWriteStarted();
        return SendHostedRequestAsync<ShareFetchRequest, ShareFetchResponse>(
            connection, request, version, requestContext, cancellationToken);
    }

    private async ValueTask<ShareFetchBrokerResult[]> AwaitPollFetchAsync(
        List<Task<ShareFetchBrokerResult>> fetchTasks,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? pendingAcks,
        int sentAcknowledgementPartitionCount,
        bool resumed,
        CancellationToken cancellationToken)
    {
        var fetches = Task.WhenAll(fetchTasks);
        ShareFetchBrokerResult[] results;
        // A hosted stop keeps waiting so that close can release what the replies acquired.
        if (_hostedProcessing || !cancellationToken.CanBeCanceled)
            results = await fetches.ConfigureAwait(false);
        else
        {
            try
            {
                results = await fetches.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The requests keep their acknowledgements and routing scratch until resumed.
                _detachedFetch = new DetachedShareFetch(fetchTasks, pendingAcks, sentAcknowledgementPartitionCount);
                throw;
            }
        }
        if (resumed)
            ForgetDetachedCancellation(results);
        return results;
    }

    // The poll that detached a request has already thrown its cancellation. A request cancelled
    // before its response keeps reporting an uncertain acknowledgement outcome, but must not
    // fail the poll or commit that resumes it.
    private static void ForgetDetachedCancellation(ShareFetchBrokerResult[] results)
    {
        for (var index = 0; index < results.Length; index++)
        {
            if (results[index].Error is OperationCanceledException)
                results[index] = results[index].WithoutRequestError();
        }
    }

    private DetachedShareFetch? TakeDetachedFetch()
    {
        var detached = _detachedFetch;
        _detachedFetch = null;
        return detached;
    }

    private bool IsDetached(List<Task<ShareFetchBrokerResult>> fetchTasks)
        => ReferenceEquals(_detachedFetch?.Tasks, fetchTasks);

    /// <summary>
    /// Completes bookkeeping for a detached fetch before another request uses its broker
    /// sessions. The responses stay detached so the next poll delivers their records.
    /// </summary>
    private async ValueTask SettleDetachedFetchAsync(CancellationToken cancellationToken)
    {
        if (_detachedFetch is not { Results: null } detached)
            return;
        var results = await Task.WhenAll(detached.Tasks).WaitAsync(cancellationToken).ConfigureAwait(false);
        if (!ReferenceEquals(_detachedFetch, detached) || detached.Results is not null)
            return;
        ForgetDetachedCancellation(results);
        detached.Results = results;
        ReturnPollPartitionGroups();
        try
        {
            CompletePollFetch(results, detached.PendingAcks, detached.SentAcknowledgementPartitionCount);
        }
        finally
        {
            _ackTracker.ReturnPollBatches(detached.PendingAcks);
        }
    }

    /// <summary>
    /// Settles a detached fetch during close. Closing the share sessions releases its acquisitions.
    /// </summary>
    private async ValueTask CloseDetachedFetchAsync(CancellationToken cancellationToken)
    {
        if (_detachedFetch is null)
            return;
        try
        {
            await SettleDetachedFetchAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogFlushAcksFailed(ex);
        }
        AbandonDetachedFetch();
    }

    /// <summary>
    /// Drops a detached fetch without delivering its records, disposing its responses once
    /// they arrive. Unsettled acknowledgements report the cancellation that detached them.
    /// </summary>
    private void AbandonDetachedFetch()
    {
        if (TakeDetachedFetch() is not { } detached)
            return;
        if (detached.Results is null)
        {
            // In-flight requests still read the routing scratch and acknowledgement lists.
            AbandonPollPartitionGroups();
            RequeueAcknowledgements(detached.PendingAcks);
            InvokeAcknowledgementCommitCallback(detached.PendingAcks, new OperationCanceledException(
                "The share consumer stopped before an in-flight ShareFetch completed."));
            _ = Task.WhenAll(detached.Tasks).ContinueWith(
                static (_, state) => new ShareFetchResponseScope((List<Task<ShareFetchBrokerResult>>)state!).Dispose(),
                detached.Tasks, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return;
        }
        new ShareFetchResponseScope(detached.Tasks).Dispose();
    }

    private sealed class DetachedShareFetch(
        List<Task<ShareFetchBrokerResult>> tasks,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? pendingAcks,
        int sentAcknowledgementPartitionCount)
    {
        internal List<Task<ShareFetchBrokerResult>> Tasks { get; } = tasks;
        internal Dictionary<TopicPartition, List<AcknowledgementBatchData>>? PendingAcks { get; } = pendingAcks;
        internal int SentAcknowledgementPartitionCount { get; } = sentAcknowledgementPartitionCount;
        // Set once CompletePollFetch has processed the responses outside a poll.
        internal ShareFetchBrokerResult[]? Results { get; set; }
    }
}
