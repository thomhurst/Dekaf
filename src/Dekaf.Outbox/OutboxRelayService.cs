using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dekaf.Outbox;

/// <summary>
/// Hosted service that drains the outbox: acquires bucket leases, publishes each owned
/// bucket's rows in order, and removes rows once the broker acknowledges them.
/// </summary>
/// <remarks>
/// <para><b>Delivery guarantee:</b> at-least-once. Rows are removed only after broker
/// acknowledgment, so a crash at any point republishes rather than loses. Consumers that
/// need effective exactly-once should deduplicate on the
/// <see cref="OutboxRelayOptions.MessageIdHeaderName"/> header.</para>
/// <para><b>Ordering:</b> within a bucket, rows publish in ascending id order and are only
/// ever marked front-to-back (contiguous acknowledged prefix), so records sharing a key keep
/// their enqueue order across retries.</para>
/// <para>The service does not own the publisher or store; their lifetimes belong to the
/// dependency injection container (or whoever constructed them).</para>
/// </remarks>
public sealed partial class OutboxRelayService : BackgroundService
{
    private readonly IOutboxStore _store;
    private readonly IOutboxPublisher _publisher;
    private readonly OutboxRelayOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutboxRelayService> _logger;
    private readonly OutboxLeaseRequest _leaseRequest;

    private IReadOnlyList<int> _ownedBuckets = [];
    private long _leaseTimestamp;

    public OutboxRelayService(
        IOutboxStore store,
        IOutboxPublisher publisher,
        OutboxRelayOptions options,
        ILogger<OutboxRelayService> logger,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        options.Validate();

        _store = store;
        _publisher = publisher;
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _leaseRequest = new OutboxLeaseRequest
        {
            RelayId = options.RelayId,
            BucketCount = options.BucketCount,
            LeaseDuration = options.LeaseDuration
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // A broker that is briefly unreachable at process start must not fault the relay:
        // committed outbox rows are already waiting, so initialization retries with the
        // same backoff as any other transient failure.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _publisher.InitializeAsync(stoppingToken).ConfigureAwait(false);
                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogPublisherInitializationFailed(ex);
                try
                {
                    await Task.Delay(_options.ErrorBackoff, _timeProvider, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        LogRelayStarted(_options.RelayId, _options.BucketCount);

        while (!stoppingToken.IsCancellationRequested)
        {
            CycleResult cycle;
            try
            {
                cycle = await RunCycleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (OutboxMisconfigurationException ex)
            {
                // Never self-heals; retrying would stall publishing silently forever.
                // Faulting the hosted service surfaces it via the host's default
                // BackgroundService exception behavior (stop the application).
                LogRelayMisconfigured(ex);
                throw;
            }
            catch (Exception ex)
            {
                LogRelayCycleFailed(ex);
                // Lease state is unknown after a failed store call; force re-acquisition.
                ResetLeaseState();
                cycle = new CycleResult(PublishedAny: false, HadError: true);
            }

            if (cycle.PublishedAny && !cycle.HadError)
                continue;

            try
            {
                var delay = cycle.HadError ? _options.ErrorBackoff : _options.PollInterval;
                await Task.Delay(delay, _timeProvider, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        LogRelayStopped(_options.RelayId);
    }

    private async Task<CycleResult> RunCycleAsync(CancellationToken cancellationToken)
    {
        await RefreshLeasesIfDueAsync(cancellationToken).ConfigureAwait(false);

        if (_ownedBuckets.Count > 0 && LeaseAge() >= _options.LeaseDuration)
        {
            // The acquisition itself outlasted the lease: the rows the store wrote are
            // already claimable by peers, so publishing would break single-writer ordering.
            // Correct but must be loud - sustained store latency at this level otherwise
            // stalls the relay silently. Treated as an error so ErrorBackoff paces retries.
            LogLeaseExpiredBeforeAcquisitionReturned(_options.LeaseDuration);
            ResetLeaseState();
            return new CycleResult(PublishedAny: false, HadError: true);
        }

        if (_ownedBuckets.Count == 0)
            return new CycleResult(PublishedAny: false, HadError: false);

        // One probe instead of one query per owned bucket, so an idle relay is cheap.
        var pendingBuckets = await _store.GetBucketsWithPendingAsync(_ownedBuckets, cancellationToken)
            .ConfigureAwait(false);

        var publishedAny = false;
        var hadError = false;
        for (var i = 0; i < pendingBuckets.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (LeaseAge() >= _options.LeaseDuration)
            {
                // Lease may have expired mid-cycle; stop publishing until re-acquired.
                ResetLeaseState();
                break;
            }

            var bucketResult = await DrainBucketAsync(pendingBuckets[i], cancellationToken).ConfigureAwait(false);
            publishedAny |= bucketResult.PublishedAny;
            hadError |= bucketResult.HadError;
        }

        return new CycleResult(publishedAny, hadError);
    }

    private void ResetLeaseState()
    {
        _ownedBuckets = [];
        _leaseTimestamp = 0;
    }

    /// <summary>
    /// True once the current leases are due for renewal; the single definition of the
    /// freshness policy used both by the cycle-level refresh and the drain loop's yield.
    /// </summary>
    private bool RenewalDue => _leaseTimestamp == 0 || LeaseAge() >= _options.LeaseRenewInterval;

    private async Task RefreshLeasesIfDueAsync(CancellationToken cancellationToken)
    {
        if (!RenewalDue)
            return;

        // Captured before the store call: the database computes lease expiry when the call
        // starts, so a slow acquisition must age the lease, not refresh it. Assigned only
        // after success so a failed call never counts as a renewal.
        var acquisitionTimestamp = _timeProvider.GetTimestamp();
        var acquired = await _store.AcquireBucketLeasesAsync(_leaseRequest, cancellationToken).ConfigureAwait(false);
        _leaseTimestamp = acquisitionTimestamp;

        if (acquired.Count != _ownedBuckets.Count)
            LogLeasesChanged(_options.RelayId, acquired.Count, _options.BucketCount);

        _ownedBuckets = acquired;
    }

    private TimeSpan LeaseAge() => _timeProvider.GetElapsedTime(_leaseTimestamp);

    /// <summary>
    /// Publishes batches for one bucket until it is empty, a publish fails, or the batch
    /// comes back partially filled.
    /// </summary>
    private async Task<CycleResult> DrainBucketAsync(int bucket, CancellationToken cancellationToken)
    {
        var publishedAny = false;
        var firstBatch = true;

        while (!cancellationToken.IsCancellationRequested)
        {
            // A long backlog must not outlive the lease from inside this loop: stop as soon
            // as renewal is due so the next cycle renews before the lease can expire and a
            // peer relay could claim the bucket (which would break single-writer ordering).
            // The first batch is always allowed: a slow-but-successful acquisition can
            // return a lease that is already renewal-due (its age is measured from before
            // the call, deliberately), and yielding before any work would leave the relay
            // renewing forever without publishing. The cycle-level expiry check still gates
            // truly dead leases.
            if (!firstBatch && RenewalDue)
                break;

            var batch = await _store.GetNextBatchAsync(bucket, _options.BatchSize, cancellationToken)
                .ConfigureAwait(false);
            if (batch.Count == 0)
                break;

            // The fetch itself may have stalled past the lease. Unlike an in-flight
            // publish (whose append cannot be fenced), this window closes for free with a
            // recheck before the first produce starts.
            if (LeaseAge() >= _options.LeaseDuration)
            {
                LogLeaseExpiredDuringBatchFetch(_options.LeaseDuration);
                ResetLeaseState();
                return new CycleResult(publishedAny, HadError: true);
            }

            firstBatch = false;

            var result = await _publisher.PublishAsync(batch, _options.MessageIdHeaderName, cancellationToken)
                .ConfigureAwait(false);

            if (result.AckedCount > 0)
            {
                // The store contract guarantees MarkPublishedAsync receives the same
                // instances GetNextBatchAsync returned, as a contiguous prefix, in order.
                IReadOnlyList<OutboxMessage> published;
                if (result.AckedCount == batch.Count)
                {
                    published = batch;
                }
                else
                {
                    var prefix = new OutboxMessage[result.AckedCount];
                    for (var i = 0; i < result.AckedCount; i++)
                        prefix[i] = batch[i];
                    published = prefix;
                }

                await _store.MarkPublishedAsync(bucket, published, cancellationToken).ConfigureAwait(false);
                publishedAny = true;
                LogBatchPublished(bucket, result.AckedCount);
            }

            if (result.FirstError is not null)
            {
                // Unacked rows stay in the store; ErrorBackoff applies before the next cycle.
                LogBatchPublishFailed(result.FirstError, bucket, batch.Count - result.AckedCount);
                return new CycleResult(publishedAny, HadError: true);
            }

            if (batch.Count < _options.BatchSize)
                break;
        }

        return new CycleResult(publishedAny, HadError: false);
    }

    private readonly record struct CycleResult(bool PublishedAny, bool HadError);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox relay {RelayId} started with {BucketCount} bucket(s)")]
    private partial void LogRelayStarted(string relayId, int bucketCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox relay {RelayId} stopped")]
    private partial void LogRelayStopped(string relayId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox relay {RelayId} now owns {OwnedCount} of {BucketCount} bucket(s)")]
    private partial void LogLeasesChanged(string relayId, int ownedCount, int bucketCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Published {Count} outbox row(s) from bucket {Bucket}")]
    private partial void LogBatchPublished(int bucket, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publish failed for bucket {Bucket}; {UnackedCount} row(s) will be retried")]
    private partial void LogBatchPublishFailed(Exception ex, int bucket, int unackedCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox relay cycle failed; backing off before retry")]
    private partial void LogRelayCycleFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Outbox relay is misconfigured and cannot make progress; stopping instead of retrying")]
    private partial void LogRelayMisconfigured(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox publisher initialization failed; retrying after backoff")]
    private partial void LogPublisherInitializationFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Lease acquisition took longer than LeaseDuration ({LeaseDuration}); the leases were expired before they could be used. Publishing is paused - raise LeaseDuration above the store's worst-case latency")]
    private partial void LogLeaseExpiredBeforeAcquisitionReturned(TimeSpan leaseDuration);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fetching a batch outlasted LeaseDuration ({LeaseDuration}); the fetched rows were discarded unpublished because the lease may already be claimed by a peer - raise LeaseDuration above the store's worst-case latency")]
    private partial void LogLeaseExpiredDuringBatchFetch(TimeSpan leaseDuration);
}
