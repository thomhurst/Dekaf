using Microsoft.Extensions.Logging;

namespace Dekaf.Outbox;

public sealed partial class OutboxRelayService
{
    private Task? _renewalDelay;
    private CancellationTokenSource? _renewalDelayCancellation;
    private long _renewalDelayStarted;
    private TimeSpan _renewalDelayDuration;
    private int _leasesReleased;

    /// <summary>
    /// Stops the relay and, for an <see cref="IOutboxLeaseOwnershipStore"/>, hands its leases
    /// back once the publish loop has ended.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await ReleaseLeasesAsync(cancellationToken).ConfigureAwait(false);
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
        if (!_acquisitionAttempted || Interlocked.Exchange(ref _leasesReleased, 1) != 0)
            return;

        try
        {
            await _ownershipStore.ReleaseBucketLeasesAsync(_leaseRequest, _previousBuckets, cancellationToken)
                .ConfigureAwait(false);
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
        var renewed = await _renewalStore!.RenewBucketLeasesAsync(_leaseRequest, _ownedBuckets, cancellationToken)
            .ConfigureAwait(false);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox relay {RelayId} released its bucket leases")]
    private partial void LogLeasesReleased(string relayId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox relay {RelayId} did not stop before the shutdown deadline, so its bucket leases were not released; peers take over after LeaseDuration ({LeaseDuration})")]
    private partial void LogLeaseReleaseSkipped(string relayId, TimeSpan leaseDuration);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox relay {RelayId} failed to release its bucket leases; peers take over after LeaseDuration ({LeaseDuration})")]
    private partial void LogLeaseReleaseFailed(Exception ex, string relayId, TimeSpan leaseDuration);
}
