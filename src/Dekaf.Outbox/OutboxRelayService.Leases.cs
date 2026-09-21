using Microsoft.Extensions.Logging;

namespace Dekaf.Outbox;

public sealed partial class OutboxRelayService
{
    private Task? _renewalDelay;
    private CancellationTokenSource? _renewalDelayCancellation;
    private long _renewalDelayStarted;
    private TimeSpan _renewalDelayDuration;
    private int _leasesReleased;
    // The host's shutdown deadline, published before the stopping token fires so the loop
    // can finish its last store write inside it. Null when the relay is stopped by disposal.
    private StopDeadline? _stopDeadline;

    private sealed class StopDeadline(CancellationToken token)
    {
        internal CancellationToken Token { get; } = token;
    }

    /// <summary>
    /// Stops the relay and, for an <see cref="IOutboxLeaseOwnershipStore"/>, hands its leases
    /// back once the publish loop has ended.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Volatile.Write(ref _stopDeadline, new StopDeadline(cancellationToken));
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await ReleaseLeasesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Marks the rows Kafka acknowledged before a stop interrupted their batch. The stopping
    /// token is already cancelled, so the write runs under the host's shutdown deadline, as
    /// the release does; without one (a relay stopped by disposal) the rows are retained and
    /// published again, which is the at-least-once fallback for every failure here.
    /// </summary>
    private async ValueTask MarkPublishedBeforeStopAsync(int bucket, IReadOnlyList<OutboxMessage> published)
    {
        if (Volatile.Read(ref _stopDeadline) is not { } deadline || deadline.Token.IsCancellationRequested)
            return;

        try
        {
            // A store that ignores its token must not hold the loop, and with it the release,
            // past the deadline: the wait is abandoned there and a late fault stays observed.
            var mark = _store.MarkPublishedAsync(bucket, published, deadline.Token).AsTask();
            _ = mark.ContinueWith(static completed => _ = completed.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            await mark.WaitAsync(deadline.Token).ConfigureAwait(false);
            LogBatchPublished(bucket, published.Count);
        }
        catch (Exception ex)
        {
            LogMarkBeforeStopFailed(ex, bucket, published.Count);
        }
    }

    private async Task ReleaseLeasesAsync(CancellationToken cancellationToken)
    {
        var executeTask = ExecuteTask;
        if (_ownershipStore is null || executeTask is null)
            return;

        // The base stop also returns when the shutdown deadline fires first. A relay that is
        // still running can still be publishing, and releasing then would invite a peer
        // onto the same rows. Its leases expire instead, as they would without this capability.
        if (!executeTask.IsCompleted)
        {
            LogLeaseReleaseSkipped(_options.RelayId, _options.LeaseDuration);
            return;
        }

        // The completed loop is what makes its state safe to read from the stopping thread.
        if (!_acquisitionAttempted || Volatile.Read(ref _leasesReleased) != 0)
            return;

        // A deadline that has already passed fails every request of the release. Nothing is
        // started, and a later stop that has time left can still hand the leases back.
        if (cancellationToken.IsCancellationRequested)
        {
            LogLeaseReleaseOutOfTime(_options.RelayId, _options.LeaseDuration);
            return;
        }

        if (Interlocked.Exchange(ref _leasesReleased, 1) != 0)
            return;

        try
        {
            // Until here the stop was bounded by the shutdown deadline whatever the store does.
            // A release that ignores its token must not be the first call to hold the host
            // past it, so the wait is abandoned at the deadline and a late fault stays observed.
            var release = _ownershipStore.ReleaseBucketLeasesAsync(_leaseRequest, _previousBuckets, cancellationToken)
                .AsTask();
            _ = release.ContinueWith(static completed => _ = completed.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            await release.WaitAsync(cancellationToken).ConfigureAwait(false);
            LogLeasesReleased(_options.RelayId);
        }
        catch (Exception ex)
        {
            // Best effort, including past the shutdown deadline: a failed handover must not
            // fail host shutdown, and it only delays takeover until the leases expire.
            LogLeaseReleaseFailed(ex, _options.RelayId, _options.LeaseDuration);
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        CancelRenewalDelay();
        _metrics.Dispose();
    }

    private void CancelRenewalDelay()
    {
        using var cancellation = Interlocked.Exchange(ref _renewalDelayCancellation, null);
        _renewalDelay = null;
        cancellation?.Cancel();
    }

    private Task GetRenewalDelay(TimeSpan remaining, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromTicks(Math.Max(1,
            Math.Min(_options.LeaseRenewInterval.Ticks, remaining.Ticks / 2)));
        // Carry one pending timer across quickly completed batches. Replacing it per
        // batch creates timer and cancellation allocations proportional to throughput.
        if (_renewalDelay is not null && (_renewalDelay.IsCompleted ||
            _renewalDelayDuration - _timeProvider.GetElapsedTime(_renewalDelayStarted) <= delay))
            return _renewalDelay;

        CancelRenewalDelay();
        _renewalDelayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _renewalDelayStarted = _timeProvider.GetTimestamp();
        _renewalDelayDuration = delay;
        return _renewalDelay = Task.Delay(delay, _timeProvider, _renewalDelayCancellation.Token);
    }

    private bool OwnsBucket(int bucket)
    {
        for (var i = 0; i < _ownedBuckets.Count; i++)
        {
            if (_ownedBuckets[i] == bucket)
                return true;
        }

        return false;
    }

    private ValueTask<bool> PreparePublishLeaseAsync(int bucket, CancellationToken cancellationToken)
    {
        var ownsBucket = _renewalStore is not null || OwnsBucket(bucket);
        var leaseAge = LeaseAge();
        // A stalled fetch must not publish under an expired lease. Use the same
        // observation for expiry and the remaining whole-call publication budget.
        if (leaseAge >= _options.LeaseDuration)
        {
            LogLeaseExpiredDuringBatchFetch(_options.LeaseDuration);
            ResetLeaseState();
            return new ValueTask<bool>(false);
        }
        if (_renewalStore is not null)
            return _leaseTimestamp == 0 || leaseAge >= _options.LeaseRenewInterval
                ? RenewOwnedLeasesAsync(cancellationToken) : new ValueTask<bool>(true);

        var publishBudget = _options.MaxPublishDuration!.Value;
        var latestStart = _options.LeaseDuration - publishBudget - _options.LeaseRenewInterval;
        if (leaseAge >= latestStart)
            return RefreshPublishLeaseAsync(bucket, publishBudget, latestStart, cancellationToken);

        if (ownsBucket)
            return new ValueTask<bool>(true);

        LogInsufficientPublishLease(publishBudget);
        ResetLeaseState();
        return new ValueTask<bool>(false);
    }

    private async ValueTask<bool> RefreshPublishLeaseAsync(int bucket, TimeSpan publishBudget,
        TimeSpan latestStart, CancellationToken cancellationToken)
    {
        await RefreshLeasesAsync(cancellationToken).ConfigureAwait(false);

        // An acquisition can rebalance this bucket away; a slow acquisition can also use
        // up the reserved publish budget. Neither case permits publishing the fetched rows.
        if (!OwnsBucket(bucket) || LeaseAge() >= latestStart)
        {
            LogInsufficientPublishLease(publishBudget);
            ResetLeaseState();
            return false;
        }

        return true;
    }

    private async ValueTask<bool> RenewOwnedLeasesAsync(CancellationToken cancellationToken)
    {
        if (LeaseAge() >= _options.LeaseDuration)
        {
            ResetLeaseState();
            return false;
        }

        var previousTimestamp = _leaseTimestamp;
        var timestamp = _timeProvider.GetTimestamp();
        _leaseCallInFlight = true;
        var renewed = await _renewalStore!.RenewBucketLeasesAsync(_leaseRequest, _ownedBuckets, cancellationToken)
            .ConfigureAwait(false);
        _leaseCallInFlight = false;
        // A response arriving after the old lease expired cannot prove continuous
        // ownership, even if the database eventually extended that lease successfully.
        if (!renewed || _timeProvider.GetElapsedTime(previousTimestamp) >= _options.LeaseDuration)
        {
            ResetLeaseState();
            return false;
        }

        _leaseTimestamp = timestamp;
        UpdateMetricsLease();
        return true;
    }

    private void ValidatePublishDuration(long started, long finished)
    {
        if (_options.MaxPublishDuration is { } budget && _timeProvider.GetElapsedTime(started, finished) > budget)
        {
            ResetLeaseState();
            throw new OutboxMisconfigurationException(
                $"PublishAsync exceeded MaxPublishDuration ({budget}). Configure a bound for the entire " +
                "batch, or use an IOutboxLeaseRenewalStore. Cancellation cannot fence already-appended records.");
        }
    }

    private OutboxPublishResult LostPublishLease(Exception? error = null)
    {
        ResetLeaseState();
        LogLeaseLostDuringPublish();
        return new OutboxPublishResult(0, error ??
            new InvalidOperationException("The outbox lease expired before publishing completed."));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Not enough remaining outbox lease time for MaxPublishDuration ({PublishBudget}); fetched rows remain unpublished. Increase LeaseDuration or implement IOutboxLeaseRenewalStore")]
    private partial void LogInsufficientPublishLease(TimeSpan publishBudget);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox lease lost during publishing; rows retained for at-least-once takeover. Already-appended Kafka records may still be delivered")]
    private partial void LogLeaseLostDuringPublish();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox lease renewal failed while bucket {Bucket} was publishing. The publish finished inside the lease that was last confirmed, so its acknowledged rows are marked; the leases are reacquired before anything else is published")]
    private partial void LogRenewalFailedDuringPublish(Exception ex, int bucket);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not mark {Count} acknowledged outbox row(s) of bucket {Bucket} before stopping; they stay in the store and are published again")]
    private partial void LogMarkBeforeStopFailed(Exception ex, int bucket, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox relay {RelayId} released its bucket leases")]
    private partial void LogLeasesReleased(string relayId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox relay {RelayId} did not stop before the shutdown deadline, so its bucket leases were not released; peers take over after LeaseDuration ({LeaseDuration})")]
    private partial void LogLeaseReleaseSkipped(string relayId, TimeSpan leaseDuration);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox relay {RelayId} stopped with no time left before the shutdown deadline, so its bucket leases were not released; peers take over after LeaseDuration ({LeaseDuration})")]
    private partial void LogLeaseReleaseOutOfTime(string relayId, TimeSpan leaseDuration);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox relay {RelayId} failed to release its bucket leases; peers take over after LeaseDuration ({LeaseDuration})")]
    private partial void LogLeaseReleaseFailed(Exception ex, string relayId, TimeSpan leaseDuration);
}
