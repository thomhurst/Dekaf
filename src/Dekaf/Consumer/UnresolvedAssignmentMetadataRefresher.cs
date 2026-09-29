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
    // Refreshes completed for the current unresolved assignment, and the Stopwatch timestamp
    // before which the next may not start.
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
    /// Starts a refresh unless one is running, the backoff has not elapsed, or the owner stopped.
    /// Returns without waiting for it.
    /// </summary>
    public void Request()
    {
        if (Volatile.Read(ref _stopped) != 0 ||
            Stopwatch.GetTimestamp() < Volatile.Read(ref _notBefore) ||
            Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0)
            return;

        // Task.Run: the refresh may wait for the metadata refresh lock or a slow broker, and none
        // of that may run inline on the heartbeat.
        Volatile.Write(ref _task, Task.Run(RefreshAsync));
    }

    /// <summary>
    /// A new assignment arrived: its refreshes start without the backoff the previous one built up.
    /// </summary>
    public void Reset()
    {
        Volatile.Write(ref _attempts, 0);
        Volatile.Write(ref _notBefore, 0);
    }

    /// <summary>
    /// Cancels a running refresh and waits for it. No refresh starts afterwards.
    /// </summary>
    public async ValueTask StopAsync()
    {
        Volatile.Write(ref _stopped, 1);
        await _cts.CancelAsync().ConfigureAwait(false);
        await Current.ConfigureAwait(false);
        // Not disposed: a heartbeat racing its owner's disposal may still start (and immediately
        // cancel) a refresh that reads the token, and a source without timers or linked
        // registrations holds nothing to release.
    }

    private async Task RefreshAsync()
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
            var delayMs = ExponentialRetryBackoff.CalculateDelayMilliseconds(
                _initialBackoffMs, _maxBackoffMs, Interlocked.Increment(ref _attempts));
            Volatile.Write(ref _notBefore, Stopwatch.GetTimestamp() + (delayMs * Stopwatch.Frequency / 1000));
            Volatile.Write(ref _inFlight, 0);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Metadata refresh for an assignment with unknown topic IDs failed; a later heartbeat retries")]
    private static partial void LogRefreshFailed(ILogger logger, Exception exception);
}
