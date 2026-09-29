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
    private Task _task = Task.CompletedTask;
    private int _inFlight;
    private int _stopped;
    // Bumped by Reset for each new assignment. A refresh started for an older generation neither
    // applies its backoff to the current one nor stands in for the refresh it asked for.
    private int _generation;
    // The latest generation that requested a refresh.
    private int _requestedGeneration = -1;
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
    }

    /// <summary>
    /// The latest refresh (completed when none ran). Never faults.
    /// </summary>
    public Task Current => Volatile.Read(ref _task);

    /// <summary>
    /// Starts a refresh unless the backoff has not elapsed or the owner stopped. While one is
    /// running, a refresh for an earlier generation starts another for this one when it ends.
    /// Returns without waiting for it.
    /// </summary>
    public void Request()
    {
        if (Volatile.Read(ref _stopped) != 0 ||
            Stopwatch.GetTimestamp() < Volatile.Read(ref _notBefore))
            return;

        // Record the demand before claiming: either the claim succeeds, or the running refresh
        // (whose release is a full fence before it reads this) sees it.
        Interlocked.Exchange(ref _requestedGeneration, Volatile.Read(ref _generation));
        if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0)
            return;

        // Task.Run: the refresh may wait for the metadata refresh lock or a slow broker, and none
        // of that may run inline on the heartbeat.
        var generation = Volatile.Read(ref _generation);
        Volatile.Write(ref _task, Task.Run(() => RefreshAsync(generation)));
    }

    /// <summary>
    /// A new assignment arrived: its refreshes start without the backoff the previous one built
    /// up, and a refresh still running for the previous one does not count for it.
    /// </summary>
    public void Reset()
    {
        Interlocked.Increment(ref _generation);
        ClearBackoff();
    }

    /// <summary>
    /// Cancels a running refresh and waits for it. No refresh starts afterwards.
    /// </summary>
    public async ValueTask StopAsync()
    {
        Volatile.Write(ref _stopped, 1);
        await _cts.CancelAsync().ConfigureAwait(false);
        // A refresh ending as this stops may already have started its follow-up.
        Task task;
        while (!(task = Current).IsCompleted)
            await task.ConfigureAwait(false);
        // Not disposed: a heartbeat racing its owner's disposal may still start (and immediately
        // cancel) a refresh that reads the token, and a source without timers or linked
        // registrations holds nothing to release.
    }

    private void ClearBackoff()
    {
        Volatile.Write(ref _attempts, 0);
        Volatile.Write(ref _notBefore, 0);
    }

    private async Task RefreshAsync(int generation)
    {
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
        var stale = Volatile.Read(ref _generation) != generation;
        if (!stale)
        {
            var delayMs = ExponentialRetryBackoff.CalculateDelayMilliseconds(
                _initialBackoffMs, _maxBackoffMs, Interlocked.Increment(ref _attempts));
            Volatile.Write(ref _notBefore, Stopwatch.GetTimestamp() + (delayMs * Stopwatch.Frequency / 1000));

            // A Reset between the check above and these writes cleared the backoff first; undo
            // what this stale refresh just wrote over it.
            stale = Volatile.Read(ref _generation) != generation;
            if (stale)
                ClearBackoff();
        }

        // Full fence, then read the demand. A Request records its generation (a full fence) before
        // it tries to claim the slot, so for any request this refresh could have turned away,
        // either its claim sees the slot free or the read below sees its generation. Checking
        // only after the release covers a Reset at any point up to here, including after the
        // checks above.
        Interlocked.Exchange(ref _inFlight, 0);

        // A newer assignment asked for a refresh while this one ran: that request found the
        // refresher busy, so start it now rather than wait for a later heartbeat.
        var requested = Volatile.Read(ref _requestedGeneration);
        if (requested != generation && requested == Volatile.Read(ref _generation))
            Request();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Metadata refresh for an assignment with unknown topic IDs failed; a later heartbeat retries")]
    private static partial void LogRefreshFailed(ILogger logger, Exception exception);
}
