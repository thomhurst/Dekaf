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
/// deliveries; message-id deduplication removes duplicates but does not restore order.
/// After a failed row the relay retries only that row until it goes through, so the rows
/// behind it are delivered once more at most, however long it keeps failing.</para>
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
    private readonly IOutboxLeaseOwnershipStore? _ownershipStore;
    private readonly IOutboxNotifier? _notifier;

    private IReadOnlyList<int> _ownedBuckets = [];
    // The last acquisition's result. Unlike _ownedBuckets it survives ResetLeaseState:
    // it orders the store's probes and never authorizes publishing.
    private IReadOnlyList<int> _previousBuckets = [];
    private bool _acquisitionAttempted;
    private long _leaseTimestamp;
    private long _rebalanceTimestamp;
    private long _probeTimestamp;
    private int _leaseGeneration;
    private readonly int[] _pendingBuckets;
    private int _pendingBucketCount;
    private readonly int[]? _hintBuckets;
    private readonly bool[]? _readyBuckets;
    private readonly bool[]? _ownedBucketFlags;
    private bool _discoveryRequired = true;
    // Only one publisher call is in flight per relay. Its observer writes this
    // before completing the awaited operation, including during a blocked renewal.
    private long _observedPublishFinished;
    // Failed publish attempts of the row at the head of each bucket; zero while the bucket
    // publishes whole batches. Kept across lease resets: what it records is the row, and a
    // reacquired bucket that went back to whole batches would deliver the rows behind a
    // still-failing head once more.
    private readonly int[] _headRowFailures;
    private readonly Guid[] _headRowMessageIds;
    // When the head row of each bucket last failed, and how long the bucket waits before its
    // retry. A failing bucket waits on its own, so the other buckets keep draining at full
    // speed in the meantime.
    private readonly long[] _headRowFailedAt;
    private readonly TimeSpan[] _headRowBackoff;
    // Cycles in a row that failed without publishing anything. Drives the error backoff.
    private int _fruitlessCycles;
    // Seeded from the relay id: relays back off out of step with each other, and one relay
    // backs off the same way on every run.
    private readonly Random _backoffJitter;
    // Set around the store calls that write leases. A failure anywhere else says nothing
    // about who owns the buckets.
    private bool _leaseCallInFlight;

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
        _ownershipStore = store as IOutboxLeaseOwnershipStore;
        _publisher = publisher;
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _notifier = notifier;
        _pendingBuckets = new int[options.BucketCount];
        _headRowFailures = new int[options.BucketCount];
        _headRowMessageIds = new Guid[options.BucketCount];
        _headRowFailedAt = new long[options.BucketCount];
        _headRowBackoff = new TimeSpan[options.BucketCount];
        _backoffJitter = new Random(StableSeed(options.RelayId));
        if (notifier is OutboxNotifier)
        {
            _hintBuckets = new int[options.BucketCount];
            _readyBuckets = new bool[options.BucketCount];
            _ownedBucketFlags = new bool[options.BucketCount];
        }
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
                if (_leaseCallInFlight)
                {
                    // A lease write that failed may or may not have been applied, so what the
                    // store holds is unknown; force re-acquisition.
                    _leaseCallInFlight = false;
                    ResetLeaseState();
                }
                else
                {
                    // A failed probe, fetch, publish or delete wrote no lease. Ownership still
                    // rests on the age of the last confirmed lease, which every publish checks,
                    // so the leases are kept: reacquiring them would add a heartbeat, a
                    // coordination read and a write per bucket to every retry, from every
                    // relay, against a store that is already failing.
                    ResetDiscoveryState();
                }
                cycle = new CycleResult(PublishedAny: false, HadError: true);
            }

            if (!cycle.HadError || cycle.PublishedAny)
                _fruitlessCycles = 0;

            if (cycle.PublishedAny && !cycle.HadError)
                continue;

            try
            {
                if (cycle.HadError)
                {
                    // Commit notifications must never bypass failure backoff.
                    await Task.Delay(NextErrorBackoff(cycle.PublishedAny), _timeProvider, stoppingToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    // A long configured poll interval must not let idle leases expire.
                    var untilRenewal = _options.LeaseRenewInterval - _timeProvider.GetElapsedTime(_rebalanceTimestamp);
                    if (untilRenewal <= TimeSpan.Zero)
                        continue;
                    var untilPoll = _options.PollInterval;
                    if (_notifier is OutboxNotifier && !_discoveryRequired)
                        untilPoll -= _timeProvider.GetElapsedTime(_probeTimestamp);
                    var delay = _ownedBuckets.Count == 0 || untilRenewal < untilPoll
                        ? untilRenewal : untilPoll;
                    // A bucket backing off from a rejected row is retried when its own backoff
                    // ends. The wait stays the idle one, so a commit to any other bucket still
                    // ends it at once. The retry counts from the cycle's start, so a later
                    // bucket's slow publish does not push an earlier bucket's retry back.
                    if (cycle.RetryAfter > TimeSpan.Zero)
                    {
                        var untilRetry = cycle.RetryAfter - _timeProvider.GetElapsedTime(cycle.Started);
                        if (untilRetry < delay)
                            delay = untilRetry;
                    }
                    if (delay <= TimeSpan.Zero)
                        continue;
                    if (_notifier is null)
                        await Task.Delay(delay, _timeProvider, stoppingToken).ConfigureAwait(false);
                    else
                        await WaitForNotificationAsync(_notifier, delay, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        LogRelayStopped(_options.RelayId);
    }

    /// <summary>
    /// Notifications are advisory, so a notifier that throws must not end the relay: the wait
    /// falls back to the timer it would have raced, for what is left of the delay. The delay
    /// ends no later than the next lease renewal, and a notifier that fails late must not
    /// push the renewal out by a second full wait.
    /// </summary>
    private async ValueTask WaitForNotificationAsync(IOutboxNotifier notifier, TimeSpan delay, CancellationToken stoppingToken)
    {
        var started = _timeProvider.GetTimestamp();
        try
        {
            await notifier.WaitAsync(delay, stoppingToken).ConfigureAwait(false);
            return;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogNotifierFailed(ex);
        }

        var remaining = delay - _timeProvider.GetElapsedTime(started);
        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining, _timeProvider, stoppingToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The delay after a failed cycle. A cycle that still published something waits
    /// <see cref="OutboxRelayOptions.ErrorBackoff"/>, so one failing bucket does not slow the
    /// others. Cycles that fail without publishing anything double the ceiling each time, up
    /// to <see cref="OutboxRelayOptions.LeaseRenewInterval"/>, and wait a random time between
    /// the configured backoff and that ceiling, so the relays sharing a failing store do not
    /// retry in step. While leases are held the wait also ends at their next renewal, as the
    /// idle wait does, but never sooner than the configured backoff.
    /// </summary>
    private TimeSpan NextErrorBackoff(bool publishedAny)
    {
        var floor = _options.ErrorBackoff;
        if (publishedAny)
            return floor;

        var failures = ++_fruitlessCycles;
        var backoff = JitteredBackoff(failures);
        if (failures <= 1 || _ownedBuckets.Count == 0)
            return backoff;

        // The ceiling is a whole renew interval, but the kept leases are already part of the
        // way through theirs: with a renew interval close to the lease duration, a wait that
        // ignored their age would let them run out, and a peer claim them, before the retry.
        var untilRenewal = _options.LeaseRenewInterval - _timeProvider.GetElapsedTime(_rebalanceTimestamp);
        if (untilRenewal >= backoff)
            return backoff;
        return untilRenewal > floor ? untilRenewal : floor;
    }

    /// <summary>
    /// <see cref="OutboxRelayOptions.ErrorBackoff"/> after the first failure. After each
    /// further one the ceiling doubles, up to <see cref="OutboxRelayOptions.LeaseRenewInterval"/>,
    /// and the delay is a random time between the configured backoff and that ceiling.
    /// </summary>
    private TimeSpan JitteredBackoff(int failures)
    {
        var floor = _options.ErrorBackoff;
        var cap = _options.LeaseRenewInterval;
        if (failures <= 1 || cap <= floor)
            return floor;

        // The shift is bounded so the ceiling cannot overflow before it is capped.
        var doublings = Math.Min(failures - 1, 30);
        var ceilingTicks = floor.Ticks > (cap.Ticks >> doublings) ? cap.Ticks : floor.Ticks << doublings;
        return floor + TimeSpan.FromTicks((long)((ceilingTicks - floor.Ticks) * _backoffJitter.NextDouble()));
    }

    private static int StableSeed(string relayId)
    {
        // FNV-1a: string.GetHashCode differs per process, which would make a run unrepeatable.
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in relayId)
                hash = (hash ^ character) * 16777619u;
            return (int)hash;
        }
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
            if (_rebalanceTimestamp == 0 || _timeProvider.GetElapsedTime(_rebalanceTimestamp, started) >= _options.LeaseRenewInterval)
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

            // Keep full buckets ready between sweeps, without an extra discovery query
            // per batch. Poll periodically even while busy to discover newly active buckets.
            var discover = _discoveryRequired || _timeProvider.GetElapsedTime(_probeTimestamp) >= _options.PollInterval;
            if (_notifier is OutboxNotifier hints)
            {
                var hintCount = hints.DrainHints(_hintBuckets, out var unknown);
                discover |= unknown;
                if (!discover && hintCount > 0)
                {
                    var readyCount = _pendingBucketCount;
                    for (var index = 0; index < readyCount; index++)
                        _readyBuckets![_pendingBuckets[index]] = true;
                    for (var index = 0; index < hintCount; index++)
                    {
                        var bucket = _hintBuckets![index];
                        if ((uint)bucket < (uint)_options.BucketCount && _ownedBucketFlags![bucket] && !_readyBuckets![bucket])
                        {
                            _pendingBuckets[_pendingBucketCount++] = bucket;
                        }
                    }
                    // Hints are already distinct; only the carried readiness needs clearing.
                    for (var index = 0; index < readyCount; index++)
                        _readyBuckets![_pendingBuckets[index]] = false;
                }
            }
            else
            {
                // Existing custom notifiers carry no consumable bucket identity.
                discover |= _pendingBucketCount == 0;
            }
            if (discover)
            {
                var pendingBuckets = await _store.GetBucketsWithPendingAsync(_ownedBuckets, cancellationToken)
                    .ConfigureAwait(false);
                _pendingBucketCount = pendingBuckets.Count;
                for (var index = 0; index < pendingBuckets.Count; index++)
                    _pendingBuckets[index] = pendingBuckets[index];
                _probeTimestamp = started;
                _discoveryRequired = false;
            }

            var publishedAny = false;
            var hadError = false;
            var backingOff = false;
            // The earliest retry of a backing-off bucket, measured from the cycle's start.
            var retryAfter = TimeSpan.MaxValue;
            var generation = _leaseGeneration;
            var pendingCount = _pendingBucketCount;
            var retainedCount = 0;
            for (var bucketIndex = 0; bucketIndex < pendingCount; bucketIndex++)
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

                if (_renewalStore is null && !OwnsBucket(_pendingBuckets[bucketIndex]))
                    continue;

                // Keep bucket draining in the cycle state machine. A pending publisher
                // needs one suspension instead of a second pooled async operation.
                var bucket = _pendingBuckets[bucketIndex];
                if (_headRowFailures[bucket] > 0)
                {
                    var retryDue = _headRowBackoff[bucket] - _timeProvider.GetElapsedTime(_headRowFailedAt[bucket], started);
                    if (retryDue > _timeProvider.GetElapsedTime(started))
                    {
                        _pendingBuckets[retainedCount++] = bucket;
                        backingOff = true;
                        if (retryDue < retryAfter)
                            retryAfter = retryDue;
                        continue;
                    }
                }

                // One batch per bucket per sweep when multiple buckets are owned.
                while (!cancellationToken.IsCancellationRequested)
                {
                    // The publisher starts every row of a batch at once, so the rows behind a
                    // failed row are delivered although they stay in the store. Fetching them
                    // again with every retry would deliver them again with every retry, for
                    // as long as the head row keeps failing. Only that row is retried until
                    // it goes through.
                    var headOnly = _headRowFailures[bucket] > 0;
                    var batch = await _store.GetNextBatchAsync(bucket, headOnly ? 1 : _options.BatchSize, cancellationToken)
                        .ConfigureAwait(false);
                    if (batch.Count == 0)
                    {
                        _headRowFailures[bucket] = 0;
                        break;
                    }

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
                    var leaseLost = false;
                    long confirmedLeaseTimestamp = 0;
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
                                        {
                                            leaseLost = true;
                                            throw new InvalidOperationException("The outbox lease expired during publishing.");
                                        }

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
                                        {
                                            leaseLost = true;
                                            throw new InvalidOperationException("The outbox no longer owns all publishing leases.");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Store/timer failures must not bypass observation of the in-flight
                                    // publisher below. Preserve the error until it finishes.
                                    renewalError = ex;
                                    // Only a refused renewal or an expired lease proves the lease lost.
                                    // A renewal that threw, or a stop, leaves the last confirmed lease
                                    // running on this relay's clock; its start is kept to decide below
                                    // whether the acknowledged rows may still be marked.
                                    confirmedLeaseTimestamp = leaseLost ? 0 : _leaseTimestamp;
                                    _leaseCallInFlight = false;
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

                    // The lease the publish ran under: the current one, or the one a failed
                    // renewal or a stop left behind. Zero once the lease is proven lost.
                    var leaseTimestamp = renewalError is null ? _leaseTimestamp : confirmedLeaseTimestamp;
                    if (cancellationToken.IsCancellationRequested)
                    {
                        // Cooperative shutdown skips the publish budget check instead of
                        // misclassifying the elapsed budget as a fatal configuration error.
                        // The rows Kafka acknowledged before the stop are still marked, within
                        // the shutdown deadline: the graceful release that follows hands the
                        // bucket to a peer at once, which would publish them all again.
                        if (result.AckedCount > 0 && leaseTimestamp != 0
                            && _timeProvider.GetElapsedTime(leaseTimestamp) < _options.LeaseDuration)
                        {
                            await MarkPublishedBeforeStopAsync(bucket, AckedPrefix(batch, result.AckedCount))
                                .ConfigureAwait(false);
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    ValidatePublishDuration(publishStarted, publishFinished);
                    if (renewalError is OutboxMisconfigurationException misconfiguration)
                        throw misconfiguration;
                    // Listener callbacks and concurrent renewal can outlast publishing.
                    // They must not inflate its budget, but lease ownership still ages.
                    var leaseChecked = hadPublishMetrics || completionObserved ? _timeProvider.GetTimestamp() : publishFinished;
                    if (leaseTimestamp == 0
                        || _timeProvider.GetElapsedTime(leaseTimestamp, leaseChecked) >= _options.LeaseDuration)
                    {
                        leaseLost = true;
                        result = LostPublishLease(renewalError);
                    }
                    else if (renewalError is not null)
                    {
                        // The renewal failed, but the publish finished inside the lease it
                        // could not extend, so its rows are this relay's to mark. Local
                        // ownership is already dropped; the next cycle reacquires.
                        LogRenewalFailedDuringPublish(renewalError, bucket);
                    }

                    if (result.AckedCount > 0)
                    {
                        var published = AckedPrefix(batch, result.AckedCount);
                        try
                        {
                            await _store.MarkPublishedAsync(bucket, published, cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            // A stop that lands during the mark cancels it, and the graceful
                            // release that follows hands the bucket to a peer that would
                            // publish these rows again. Marking is idempotent, so the rows are
                            // marked once more within the shutdown deadline, as above.
                            if (_timeProvider.GetElapsedTime(leaseTimestamp) < _options.LeaseDuration)
                                await MarkPublishedBeforeStopAsync(bucket, published).ConfigureAwait(false);
                            throw;
                        }
                        publishedAny = true;
                        LogBatchPublished(bucket, result.AckedCount);
                    }

                    if (headOnly)
                        OutboxMetrics.Record(OutboxMetrics.HeadRowRetries, _metrics, 1);

                    if (result.FirstError is not null)
                    {
                        // Unacked rows stay in the store; the error backoff applies before the next cycle.
                        if (leaseLost || result.AckedCount >= batch.Count)
                        {
                            LogBatchPublishFailed(result.FirstError, bucket, batch.Count - result.AckedCount);
                            hadError = true;
                            break;
                        }

                        // A row Kafka rejects backs off its own bucket only. Pacing the whole
                        // relay by it would drain every healthy bucket at one batch per
                        // ErrorBackoff for as long as that row keeps failing. The bucket's
                        // backoff grows with the row's failures as the relay's does, so a
                        // broker that rejects every bucket is still retried less and less often.
                        RecordHeadRowFailure(bucket, batch[result.AckedCount], result.FirstError, batch.Count - result.AckedCount);
                        var backoff = JitteredBackoff(_headRowFailures[bucket]);
                        var failedAt = _timeProvider.GetTimestamp();
                        _headRowFailedAt[bucket] = failedAt;
                        _headRowBackoff[bucket] = backoff;
                        _pendingBuckets[retainedCount++] = bucket;
                        backingOff = true;
                        var retryDue = backoff + _timeProvider.GetElapsedTime(started, failedAt);
                        if (retryDue < retryAfter)
                            retryAfter = retryDue;
                        break;
                    }

                    if (headOnly)
                        LogHeadRowRecovered(bucket, batch[0].MessageId, _headRowFailures[bucket] + 1);
                    _headRowFailures[bucket] = 0;

                    if (renewalError is not null)
                    {
                        hadError = true;
                        break;
                    }

                    // A head row that went through has the rest of its bucket waiting behind it.
                    if (headOnly || batch.Count == _options.BatchSize)
                    {
                        // With only one owned bucket there is no peer to starve. Avoid an
                        // extra cycle per batch, but still yield for fair-share acquisition.
                        if (_ownedBuckets.Count == 1 && _timeProvider.GetElapsedTime(_rebalanceTimestamp) < _options.LeaseRenewInterval)
                            continue;
                        _pendingBuckets[retainedCount++] = bucket;
                    }
                    break;
                }
            }

            // A legacy store may rebalance during PreparePublishLeaseAsync. Never carry
            // readiness from an earlier ownership epoch into the next sweep.
            _pendingBucketCount = !hadError && generation == _leaseGeneration ? retainedCount : 0;
            if (hadError)
                _discoveryRequired = true;
            if (hadError || publishedAny || !backingOff)
                return new CycleResult(publishedAny, hadError);

            // Only buckets backing off from a rejected row are left. That is not a relay-wide
            // failure: the relay idles until the earliest of their retries, and a commit to
            // any other bucket still wakes it. They stay ready, so the retry needs no probe,
            // but a notifier that names no bucket finds new work only by probing.
            if (_notifier is not OutboxNotifier)
                _discoveryRequired = true;
            return new CycleResult(PublishedAny: false, HadError: false, retryAfter, started);
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
        _rebalanceTimestamp = 0;
        ResetDiscoveryState();
        ClearMetricsLease();
        _leaseGeneration++;
        PublishOwnedBuckets(_ownedBuckets);
    }

    /// <summary>
    /// A cycle that ended in an exception leaves the ready list half rewritten, so the next
    /// cycle probes instead of trusting it.
    /// </summary>
    private void ResetDiscoveryState()
    {
        _pendingBucketCount = 0;
        _discoveryRequired = true;
    }

    private void PublishOwnedBuckets(IReadOnlyList<int> buckets)
    {
        if (_notifier is not IOutboxBucketNotifier bucketNotifier)
            return;

        try
        {
            bucketNotifier.SetOwnedBuckets(buckets);
        }
        catch (Exception ex)
        {
            // The snapshot only filters wake-ups. A relay that cannot publish it still finds
            // its rows by polling, and must not stop over a hint.
            LogNotifierFailed(ex);
        }
    }

    private static IReadOnlyList<OutboxMessage> AckedPrefix(IReadOnlyList<OutboxMessage> batch, int ackedCount)
    {
        // The store contract guarantees MarkPublishedAsync receives the same instances
        // GetNextBatchAsync returned, as a contiguous prefix, in order.
        if (ackedCount == batch.Count)
            return batch;

        var prefix = new OutboxMessage[ackedCount];
        for (var i = 0; i < ackedCount; i++)
            prefix[i] = batch[i];
        return prefix;
    }

    private void RecordHeadRowFailure(int bucket, OutboxMessage row, Exception error, int unackedCount)
    {
        if (_headRowFailures[bucket] == 0 || _headRowMessageIds[bucket] != row.MessageId)
        {
            // First failure of this row: the whole batch was attempted.
            _headRowFailures[bucket] = 1;
            _headRowMessageIds[bucket] = row.MessageId;
            LogBatchPublishFailed(error, bucket, unackedCount);
            return;
        }

        LogHeadRowStillFailing(error, bucket, row.MessageId, ++_headRowFailures[bucket]);
    }

    private async Task RefreshLeasesAsync(CancellationToken cancellationToken)
    {
        // Observe a stalled relay's expired set before reacquisition replaces its timestamp.
        if (_ownedBuckets.Count > 0 && LeaseAge() >= _options.LeaseDuration)
            ResetLeaseState();

        // Captured before the store call: the database computes lease expiry when the call
        // starts, so a slow acquisition must age the lease, not refresh it. Assigned only
        // after success so a failed call never counts as a renewal.
        var acquisitionTimestamp = _timeProvider.GetTimestamp();
        // Set before the call: a failed acquisition can still have written a heartbeat
        // or claimed leases that a graceful stop should hand back.
        _acquisitionAttempted = true;
        _leaseCallInFlight = true;
        var acquired = _ownershipStore is null
            ? await _store.AcquireBucketLeasesAsync(_leaseRequest, cancellationToken).ConfigureAwait(false)
            : await _ownershipStore.AcquireBucketLeasesAsync(_leaseRequest, _previousBuckets, cancellationToken)
                .ConfigureAwait(false);
        _leaseCallInFlight = false;
        // The old lease can expire during acquisition, including when no buckets are returned.
        // Observe that epoch before replacing either its ownership or timestamp.
        if (_ownedBuckets.Count > 0 && LeaseAge() >= _options.LeaseDuration)
            ResetLeaseState();
        _leaseTimestamp = acquisitionTimestamp;
        _rebalanceTimestamp = acquisitionTimestamp;
        _pendingBucketCount = 0;
        _discoveryRequired = true;
        _leaseGeneration++;

        if (acquired.Count != _ownedBuckets.Count)
            LogLeasesChanged(_options.RelayId, acquired.Count, _options.BucketCount);

        _ownedBuckets = acquired;
        _previousBuckets = acquired;
        if (_ownedBucketFlags is not null)
        {
            Array.Clear(_ownedBucketFlags);
            for (var index = 0; index < acquired.Count; index++)
                _ownedBucketFlags[acquired[index]] = true;
        }
        UpdateMetricsLease();
        PublishOwnedBuckets(acquired);
        Volatile.Write(ref _metrics.OwnedBuckets, acquired.Count);
    }

    private TimeSpan LeaseAge() => _timeProvider.GetElapsedTime(_leaseTimestamp);

    /// <param name="RetryAfter">
    /// When positive, the time from <paramref name="Started"/> until the earliest bucket backing
    /// off from a rejected row is due for its retry. The idle wait after the cycle ends no later
    /// than that.
    /// </param>
    /// <param name="Started">The cycle's start timestamp, which <paramref name="RetryAfter"/> counts from.</param>
    private readonly record struct CycleResult(bool PublishedAny, bool HadError, TimeSpan RetryAfter = default, long Started = 0);

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox bucket {Bucket} is blocked by its head row {MessageId}: publish attempt {Attempt} of that row failed. Only that row is retried, so the rows behind it are not delivered again; nothing in this bucket is published until it goes through or is removed from the store")]
    private partial void LogHeadRowStillFailing(Exception ex, int bucket, Guid messageId, int attempt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox bucket {Bucket} is publishing again: head row {MessageId} went through on attempt {Attempt}")]
    private partial void LogHeadRowRecovered(int bucket, Guid messageId, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox notifier failed; notifications are advisory, so the relay falls back to polling")]
    private partial void LogNotifierFailed(Exception ex);

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
