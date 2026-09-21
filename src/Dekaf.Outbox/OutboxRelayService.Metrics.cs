using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Dekaf.Outbox;

public sealed partial class OutboxRelayService
{
    private readonly OutboxMetricState _metrics;
    private MetricsLease? _metricsLease;
    private readonly object _metricsLeaseLock = new();
    // Completed when this relay is handed bucket zero, so an idle sampler starts at once
    // instead of at its next interval. Null while no sampler is waiting.
    private TaskCompletionSource? _metricsLeaseGained;

    private sealed class MetricsLease(long timestamp)
    {
        public long Timestamp = timestamp;
    }

    private void UpdateMetricsLease()
    {
        if (!_options.CollectMetricsOnBucketZeroOwnerOnly)
            return;
        lock (_metricsLeaseLock)
        {
            if (OwnsBucket(0))
            {
                if (_metricsLease is null)
                {
                    Volatile.Write(ref _metricsLease, new MetricsLease(_leaseTimestamp));
                    Interlocked.Exchange(ref _metricsLeaseGained, null)?.TrySetResult();
                }
                else
                    Volatile.Write(ref _metricsLease.Timestamp, _leaseTimestamp);
            }
            else
            {
                Volatile.Write(ref _metricsLease, null);
                Volatile.Write(ref _metrics.Pending, null);
            }
        }
    }

    private void ClearMetricsLease()
    {
        if (!_options.CollectMetricsOnBucketZeroOwnerOnly)
            return;
        lock (_metricsLeaseLock)
        {
            Volatile.Write(ref _metricsLease, null);
            Volatile.Write(ref _metrics.Pending, null);
        }
    }

    private bool CanSample(MetricsLease? lease) => !_options.CollectMetricsOnBucketZeroOwnerOnly
        || (lease is not null && _timeProvider.GetElapsedTime(Volatile.Read(ref lease.Timestamp)) < _options.LeaseDuration);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var samplingCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        Task? sampling = null;
        try
        {
            if (_store is IOutboxMetricsStore metricsStore)
            {
                // One worker for the relay lifetime. Even a synchronously slow custom
                // metrics store cannot run on the publication thread.
                sampling = Task.Run(() => CollectPendingMetricsAsync(metricsStore, samplingCancellation.Token), CancellationToken.None);
            }
            await RunRelayAsync(stoppingToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                samplingCancellation.Cancel();
                if (sampling is not null)
                    await sampling.ConfigureAwait(false);
            }
            finally
            {
                ResetLeaseState();
                _metrics.Dispose();
            }
        }
    }

    private async Task CollectPendingMetricsAsync(IOutboxMetricsStore store, CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Registered before the lease is read: a handover in between is then seen
                // by the read, and one after it completes the wait.
                TaskCompletionSource? leaseGained = null;
                if (_options.CollectMetricsOnBucketZeroOwnerOnly)
                {
                    leaseGained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    Volatile.Write(ref _metricsLeaseGained, leaseGained);
                }

                var lease = Volatile.Read(ref _metricsLease);
                if (OutboxMetrics.PendingEnabled && CanSample(lease))
                {
                    leaseGained = null;
                    using var timeout = new CancellationTokenSource(_options.MetricsCollectionTimeout, _timeProvider);
                    using var queryCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeout.Token);
                    try
                    {
                        var snapshot = await store.GetPendingMetricsAsync(queryCancellation.Token).ConfigureAwait(false);
                        queryCancellation.Token.ThrowIfCancellationRequested();
                        if (!_options.CollectMetricsOnBucketZeroOwnerOnly)
                            Volatile.Write(ref _metrics.Pending, snapshot);
                        else
                        {
                            // Serialize cache publication with ownership loss so an old
                            // in-flight sample cannot restore a cleared observation.
                            lock (_metricsLeaseLock)
                                Volatile.Write(ref _metrics.Pending,
                                    CanSample(lease) && ReferenceEquals(lease, _metricsLease) ? snapshot : null);
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Volatile.Write(ref _metrics.Pending, null);
                        LogMetricsCollectionFailed(ex);
                    }
                }
                else
                {
                    Volatile.Write(ref _metrics.Pending, null);
                }
                // Delay starts after completion: one outstanding query at most, never a
                // catch-up burst after a slow query or a long process suspension.
                // Only a wait that followed no query ends early, so the interval still
                // separates every two queries of this relay.
                if (leaseGained is null)
                    await Task.Delay(_options.MetricsCollectionInterval, _timeProvider, stoppingToken).ConfigureAwait(false);
                else
                    await WaitForMetricsLeaseAsync(leaseGained.Task, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal sampler shutdown; the relay still observes this task before disposal.
        }
        finally
        {
            Volatile.Write(ref _metrics.Pending, null);
        }
    }

    private async Task WaitForMetricsLeaseAsync(Task leaseGained, CancellationToken stoppingToken)
    {
        using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var delay = Task.Delay(_options.MetricsCollectionInterval, _timeProvider, delayCancellation.Token);
        await Task.WhenAny(delay, leaseGained).ConfigureAwait(false);

        // Releases the timer of a wait that the lease ended early. The delay is observed
        // either way, so that a cancelled one cannot fault unseen.
        delayCancellation.Cancel();
        await delay.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        stoppingToken.ThrowIfCancellationRequested();
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<OutboxPublishResult> ObservePublishCompletionAsync(ValueTask<OutboxPublishResult> publish,
        bool measurePublish, bool measureDuration, long started, CancellationToken cancellationToken)
    {
        try
        {
            OutboxPublishResult result;
            try
            {
                result = await publish.ConfigureAwait(false);
            }
            finally
            {
                _observedPublishFinished = _timeProvider.GetTimestamp();
            }
            if (measurePublish)
                RecordPublishResult(result, cancellationToken);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            if (measurePublish)
                OutboxMetrics.Record(OutboxMetrics.Failures, _metrics, 1);
            throw;
        }
        finally
        {
            if (measureDuration)
                OutboxMetrics.RecordDuration(OutboxMetrics.PublishDuration, _metrics, started, _observedPublishFinished);
        }
    }

    private void RecordPublishResult(OutboxPublishResult result, CancellationToken cancellationToken)
    {
        // Count the acknowledged prefix before lease/delete decisions. Retries
        // count repeated acknowledgements, not unique deliveries.
        OutboxMetrics.Record(OutboxMetrics.Acknowledged, _metrics, result.AckedCount);
        if (result.FirstError is not null &&
            !(result.FirstError is OperationCanceledException && cancellationToken.IsCancellationRequested))
            OutboxMetrics.Record(OutboxMetrics.Failures, _metrics, 1);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox metrics collection failed; pending observations are unavailable until the next successful sample")]
    private partial void LogMetricsCollectionFailed(Exception exception);
}
