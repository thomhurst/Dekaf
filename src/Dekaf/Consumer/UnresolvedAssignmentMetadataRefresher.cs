using System.Diagnostics;
using Dekaf.Metadata;
using Dekaf.Retry;
using Microsoft.Extensions.Logging;

namespace Dekaf.Consumer;

/// <summary>
/// Refreshes metadata for a group assignment that names topic IDs this client does not know yet
/// (a topic created after the last refresh), off the heartbeat path. At most one refresh runs at
/// a time, and consecutive refreshes for the same unresolved assignment back off exponentially
/// from the retry backoff up to <see cref="MaxIntervalMs"/>, so a topic ID that never resolves
/// (a deleted topic, say) costs a Metadata request only rarely. Used by the KIP-848 and share
/// group coordinators; nothing here runs while every assigned topic is known.
/// </summary>
/// <remarks>
/// Called at heartbeat cadence, and only while an assignment is unresolved, so a plain lock
/// guards the state; the steady heartbeat never reaches this type.
/// </remarks>
internal sealed partial class UnresolvedAssignmentMetadataRefresher
{
    /// <summary>
    /// The longest wait between refreshes for one unresolved assignment, unless the configured
    /// maximum retry backoff is longer.
    /// </summary>
    internal const int MaxIntervalMs = 30_000;

    private readonly MetadataManager _metadataManager;
    private readonly ILogger _logger;
    private readonly int _initialBackoffMs;
    private readonly int _maxBackoffMs;
    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _gate = new();
    // Bound once, so starting a refresh allocates no delegate or closure.
    private readonly Func<Task> _refresh;

    // All fields below are guarded by _gate.
    private Task _task = Task.CompletedTask;
    private bool _inFlight;
    private bool _stopped;
    // Bumped by Reset for each new assignment. A refresh started for an older generation neither
    // applies its backoff to the current one nor stands in for the refresh it asked for.
    private int _generation;
    // The latest generation that requested a refresh.
    private int _requestedGeneration = -1;
    // The generation the refresh in flight serves. Written only while no refresh is in flight.
    private int _refreshGeneration;
    // Refreshes completed for the current generation, and the Stopwatch timestamp before which
    // the next may not start.
    private int _attempts;
    private long _notBefore;

    public UnresolvedAssignmentMetadataRefresher(
        MetadataManager metadataManager,
        ILogger logger,
        int retryBackoffMs,
        int retryBackoffMaxMs)
    {
        _metadataManager = metadataManager;
        _logger = logger;
        _initialBackoffMs = Math.Max(retryBackoffMs, 1);
        _maxBackoffMs = Math.Max(retryBackoffMaxMs, MaxIntervalMs);
        _refresh = RefreshAsync;
    }

    /// <summary>
    /// The latest refresh (completed when none ran). Never faults.
    /// </summary>
    public Task Current
    {
        get
        {
            lock (_gate)
                return _task;
        }
    }

    /// <summary>
    /// Starts a refresh unless the backoff has not elapsed or the owner stopped. While one is
    /// running, a refresh for an earlier generation starts another for this one when it ends.
    /// Returns without waiting for it.
    /// </summary>
    public void Request()
    {
        lock (_gate)
        {
            _requestedGeneration = _generation;
            StartIfDueLocked();
        }
    }

    /// <summary>
    /// A new assignment arrived, or the pending one was dropped: later refreshes start without the
    /// backoff the previous one built up, and a refresh still running for it neither counts for
    /// the next assignment nor starts a follow-up.
    /// </summary>
    public void Reset()
    {
        lock (_gate)
        {
            _generation++;
            _attempts = 0;
            _notBefore = 0;
        }
    }

    /// <summary>
    /// Cancels a running refresh and waits for it. No refresh starts afterwards.
    /// </summary>
    public async ValueTask StopAsync()
    {
        Task task;
        lock (_gate)
        {
            // Starting a refresh and publishing its task happen under the lock, so the task read
            // here is the last one that will ever run.
            _stopped = true;
            task = _task;
        }

        await _cts.CancelAsync().ConfigureAwait(false);
        await task.ConfigureAwait(false);
        // Not disposed: a heartbeat racing its owner's disposal may still read the token, and a
        // source without timers or linked registrations holds nothing to release.
    }

    private void StartIfDueLocked()
    {
        if (_stopped || _inFlight || Stopwatch.GetTimestamp() < _notBefore)
            return;

        _inFlight = true;
        _refreshGeneration = _generation;
        // Task.Run: the refresh may wait for the metadata refresh lock or a slow broker, and none
        // of that may run inline on the heartbeat. Only queues the work, so it is safe under the lock.
        // One task per refresh: at most one runs at a time, backoff-limited, and only while an
        // assignment is unresolved, so the steady heartbeat never gets here.
        _task = Task.Run(_refresh);
    }

    private async Task RefreshAsync()
    {
        int generation;
        lock (_gate)
            generation = _refreshGeneration;

        var cancellationToken = _cts.Token;
        try
        {
            await _metadataManager.RefreshMetadataAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The owner is disposing.
        }
        catch (Exception ex)
        {
            // Best effort: the assignment stays pending and a later heartbeat requests again.
            LogRefreshFailed(_logger, ex);
        }
        finally
        {
            Complete(generation);
        }
    }

    private void Complete(int generation)
    {
        lock (_gate)
        {
            _inFlight = false;
            if (generation == _generation)
            {
                var delayMs = ExponentialRetryBackoff.CalculateDelayMilliseconds(
                    _initialBackoffMs, _maxBackoffMs, ++_attempts);
                _notBefore = Stopwatch.GetTimestamp() + (delayMs * Stopwatch.Frequency / 1000);
                return;
            }

            // A newer assignment asked for a refresh while this one ran: that request found the
            // refresher busy, so start it now rather than wait for a later heartbeat.
            if (_requestedGeneration == _generation)
                StartIfDueLocked();
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Metadata refresh for an assignment with unknown topic IDs failed; a later heartbeat retries")]
    private static partial void LogRefreshFailed(ILogger logger, Exception exception);
}
