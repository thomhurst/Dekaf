using System.Runtime.CompilerServices;
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
/// <para><b>Ordering:</b> within a bucket, rows are submitted in ascending id order and are
/// marked front-to-back (contiguous acknowledged prefix). Later rows may already be delivered
/// when an earlier row fails. Retrying the retained rows can reorder consumer-observed first
/// deliveries; message-id deduplication removes duplicates but does not restore order.</para>
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
    private readonly IOutboxLeaseRenewalStore? _renewalStore;
    private readonly IOutboxNotifier? _notifier;

    private IReadOnlyList<int> _ownedBuckets = [];
    private long _leaseTimestamp;
    // Only one publisher call is in flight per relay. Its observer writes this
    // before completing the awaited operation, including during a blocked renewal.
    private long _observedPublishFinished;

    public OutboxRelayService(
        IOutboxStore store,
        IOutboxPublisher publisher,
        OutboxRelayOptions options,
        ILogger<OutboxRelayService> logger,
        TimeProvider? timeProvider = null)
        : this(store, publisher, options, logger, timeProvider, null)
    {
    }

    /// <summary>
    /// Creates a relay with optional local post-commit notifications. Use a separate
    /// notifier for each independently wired relay. The caller owns its lifetime.
    /// </summary>
    public OutboxRelayService(
        IOutboxStore store,
        IOutboxPublisher publisher,
        OutboxRelayOptions options,
        ILogger<OutboxRelayService> logger,
        TimeProvider? timeProvider,
        IOutboxNotifier? notifier)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        options.Validate();
        _renewalStore = store as IOutboxLeaseRenewalStore;
        if (_renewalStore is null && options.MaxPublishDuration is null)
        {
            throw new OutboxMisconfigurationException(
                "The outbox store must implement IOutboxLeaseRenewalStore or configure MaxPublishDuration " +
                "as a bound for the entire publisher call, including all rows, backpressure and delivery attempts. " +
                "One record's producer delivery timeout is not a whole-batch bound.");
        }

        _store = store;
        _publisher = publisher;
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _notifier = notifier;
        _metrics = new OutboxMetricState(options.MetricsName, _timeProvider);
        OutboxMetrics.Register(_metrics);
        _leaseRequest = new OutboxLeaseRequest
        {
            RelayId = options.RelayId,
            BucketCount = options.BucketCount,
            LeaseDuration = options.LeaseDuration
        };
    }

    private async Task RunRelayAsync(CancellationToken stoppingToken)
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
                if (cycle.HadError)
                {
                    // Commit notifications must never bypass failure backoff.
                    await Task.Delay(_options.ErrorBackoff, _timeProvider, stoppingToken).ConfigureAwait(false);
                }
                else
                {
                    // A long configured poll interval must not let idle leases expire.
                    var untilRenewal = _options.LeaseRenewInterval - LeaseAge();
                    if (untilRenewal <= TimeSpan.Zero)
                        continue;
                    var delay = untilRenewal < _options.PollInterval
                        ? untilRenewal : _options.PollInterval;
                    if (_notifier is null)
                        await Task.Delay(delay, _timeProvider, stoppingToken).ConfigureAwait(false);
                    else
                        await _notifier.WaitAsync(delay, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        LogRelayStopped(_options.RelayId);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<CycleResult> RunCycleAsync(CancellationToken cancellationToken)
    {
        var measureCycleDuration = OutboxMetrics.CycleDuration.Enabled;
        var started = _timeProvider.GetTimestamp();
        try
        {
            // The cycle boundary is also the initial lease-age observation.
            var leaseAge = _leaseTimestamp == 0 ? TimeSpan.Zero
                : _timeProvider.GetElapsedTime(_leaseTimestamp, started);
            if (_leaseTimestamp == 0 || leaseAge >= _options.LeaseRenewInterval)
            {
                await RefreshLeasesAsync(cancellationToken).ConfigureAwait(false);
                leaseAge = LeaseAge();
            }

            if (_ownedBuckets.Count > 0 && leaseAge >= _options.LeaseDuration)
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
            for (var bucketIndex = 0; bucketIndex < pendingBuckets.Count; bucketIndex++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (_ownedBuckets.Count == 0 || LeaseAge() >= _options.LeaseDuration)
                {
                    // A slow pending probe can outlast the lease. Pace re-acquisition like
                    // other lease failures; the cleared timestamp cannot bound an idle wait.
                    ResetLeaseState();
                    hadError = true;
                    break;
                }

                if (_renewalStore is null && !OwnsBucket(pendingBuckets[bucketIndex]))
                    continue;

                // Keep bucket draining in the cycle state machine. A pending publisher
                // needs one suspension instead of a second pooled async operation.
                var bucket = pendingBuckets[bucketIndex];
                var firstBatch = true;

                while (!cancellationToken.IsCancellationRequested)
                {
                    // A long backlog must not outlive the lease from inside this loop: stop as soon
                    // as renewal is due so the next cycle renews before the lease can expire and a
                    // peer relay could claim the bucket (which would break single-writer ordering).
                    // Fetch the first batch even after a slow acquisition. PreparePublishLeaseAsync
                    // then renews or reserves its whole-call budget before publishing begins.
                    if (!firstBatch && RenewalDue)
                        break;

                    var batch = await _store.GetNextBatchAsync(bucket, _options.BatchSize, cancellationToken)
                        .ConfigureAwait(false);
                    if (batch.Count == 0)
                        break;

                    firstBatch = false;

                    if (!await PreparePublishLeaseAsync(bucket, cancellationToken).ConfigureAwait(false))
                    {
                        hadError = true;
                        break;
                    }

                    var measurePublish = OutboxMetrics.PublishEnabled;
                    var measureDuration = measurePublish && OutboxMetrics.PublishDuration.Enabled;
                    var publishStarted = _options.MaxPublishDuration.HasValue || measureDuration
                        ? _timeProvider.GetTimestamp() : 0;
                    OutboxPublishResult result;
                    long publishFinished = 0;
                    var hadPublishMetrics = measurePublish;
                    var completionObserved = false;
                    Exception? renewalError = null;
                    try
                    {
                        try
                        {
                            var publish = _publisher.PublishAsync(batch, _options.MessageIdHeaderName, cancellationToken);
                            if (_renewalStore is not null && !publish.IsCompletedSuccessfully)
                            {
                                var publishTask = publish.AsTask();
                                ValueTask<OutboxPublishResult> observedPublish = default;
                                try
                                {
                                    while (!publishTask.IsCompleted)
                                    {
                                        cancellationToken.ThrowIfCancellationRequested();
                                        var remaining = _options.LeaseDuration - LeaseAge();
                                        if (remaining <= TimeSpan.Zero)
                                            throw new InvalidOperationException("The outbox lease expired during publishing.");

                                        var nextRenewal = GetRenewalDelay(remaining, cancellationToken);
                                        if (await Task.WhenAny(publishTask, nextRenewal).ConfigureAwait(false) == publishTask)
                                            break;

                                        await nextRenewal.ConfigureAwait(false);
                                        CancelRenewalDelay();
                                        if (!completionObserved && (measurePublish || _options.MaxPublishDuration.HasValue))
                                        {
                                            // Only an outstanding renewal can delay the normal
                                            // publisher await. Observe completion independently
                                            // before entering that cold store call.
                                            observedPublish = ObservePublishCompletionAsync(new(publishTask),
                                                measurePublish, measureDuration, publishStarted, cancellationToken);
                                            completionObserved = true;
                                            measurePublish = false;
                                            measureDuration = false;
                                        }
                                        if (!await RenewOwnedLeasesAsync(cancellationToken).ConfigureAwait(false))
                                            throw new InvalidOperationException("The outbox no longer owns all publishing leases.");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Store/timer failures must not bypass observation of the in-flight
                                    // publisher below. Preserve the error and retain rows after it finishes.
                                    renewalError = ex;
                                    ResetLeaseState();
                                }

                                // Always observe publishing before another cycle, including after lease
                                // loss. Cancellation cannot retract an already-appended Kafka record.
                                result = completionObserved
                                    ? await observedPublish.ConfigureAwait(false)
                                    : await publishTask.ConfigureAwait(false);
                            }
                            else
                            {
                                result = await publish.ConfigureAwait(false);
                            }
                        }
                        finally
                        {
                            publishFinished = completionObserved ? _observedPublishFinished : _timeProvider.GetTimestamp();
                        }
                        if (measurePublish)
                            RecordPublishResult(result, cancellationToken);
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
                            OutboxMetrics.RecordDuration(OutboxMetrics.PublishDuration, _metrics, publishStarted, publishFinished);
                    }

                    // Cooperative shutdown retains rows for at-least-once replay instead of
                    // misclassifying the elapsed publish budget as a fatal configuration error.
                    cancellationToken.ThrowIfCancellationRequested();
                    ValidatePublishDuration(publishStarted, publishFinished);
                    if (renewalError is OutboxMisconfigurationException misconfiguration)
                        throw misconfiguration;
                    // Listener callbacks and concurrent renewal can outlast publishing.
                    // They must not inflate its budget, but lease ownership still ages.
                    var leaseChecked = hadPublishMetrics || completionObserved ? _timeProvider.GetTimestamp() : publishFinished;
                    if (renewalError is not null
                        || _timeProvider.GetElapsedTime(_leaseTimestamp, leaseChecked) >= _options.LeaseDuration)
                        result = LostPublishLease(renewalError);

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
                        hadError = true;
                        break;
                    }

                    if (batch.Count < _options.BatchSize)
                        break;
                }
            }

            return new CycleResult(publishedAny, hadError);
        }
        finally
        {
            if (measureCycleDuration)
                OutboxMetrics.RecordDuration(OutboxMetrics.CycleDuration, _metrics, started);
        }
    }

    private void ResetLeaseState()
    {
        if (_ownedBuckets.Count > 0 && LeaseAge() >= _options.LeaseDuration)
            OutboxMetrics.Record(OutboxMetrics.LeaseExpirations, _metrics, 1);
        _ownedBuckets = [];
        Volatile.Write(ref _metrics.OwnedBuckets, 0);
        _leaseTimestamp = 0;
    }

    /// <summary>
    /// True once the current leases are due for renewal; the single definition of the
    /// freshness policy used both by the cycle-level refresh and the drain loop's yield.
    /// </summary>
    private bool RenewalDue => _leaseTimestamp == 0 || LeaseAge() >= _options.LeaseRenewInterval;

    private async Task RefreshLeasesAsync(CancellationToken cancellationToken)
    {
        // Observe a stalled relay's expired set before reacquisition replaces its timestamp.
        if (_ownedBuckets.Count > 0 && LeaseAge() >= _options.LeaseDuration)
            ResetLeaseState();

        // Captured before the store call: the database computes lease expiry when the call
        // starts, so a slow acquisition must age the lease, not refresh it. Assigned only
        // after success so a failed call never counts as a renewal.
        var acquisitionTimestamp = _timeProvider.GetTimestamp();
        var acquired = await _store.AcquireBucketLeasesAsync(_leaseRequest, cancellationToken).ConfigureAwait(false);
        // The old lease can expire during acquisition, including when no buckets are returned.
        // Observe that epoch before replacing either its ownership or timestamp.
        if (_ownedBuckets.Count > 0 && LeaseAge() >= _options.LeaseDuration)
            ResetLeaseState();
        _leaseTimestamp = acquisitionTimestamp;

        if (acquired.Count != _ownedBuckets.Count)
            LogLeasesChanged(_options.RelayId, acquired.Count, _options.BucketCount);

        _ownedBuckets = acquired;
        Volatile.Write(ref _metrics.OwnedBuckets, acquired.Count);
    }

    private TimeSpan LeaseAge() => _timeProvider.GetElapsedTime(_leaseTimestamp);

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
