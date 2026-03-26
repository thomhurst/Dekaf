using System.Threading.Channels;
using Dekaf.Errors;
using Dekaf.Protocol;
using Microsoft.Extensions.Logging;

namespace Dekaf.Consumer;

/// <summary>
/// Encapsulates the pipelined prefetch state machine for testability.
/// The runner manages the in-flight prefetch task and the interaction between
/// assignment checks, memory limits, fetch execution, and error handling.
///
/// <para>Invariant: only one fetch executes at a time. The eager in-flight task
/// is always awaited before the next synchronous fetch call.</para>
/// </summary>
internal sealed class PrefetchPipelineRunner
{
    private readonly Func<CancellationToken, ValueTask> _ensureAssignment;
    private readonly Func<int> _getAssignmentCount;
    private readonly Func<long> _getMaxBytes;
    private readonly Func<long> _getPrefetchedBytes;
    private readonly Func<CancellationToken, ValueTask> _prefetchRecords;
    private readonly Func<CancellationToken, Task> _waitForMemoryAvailable;
    private readonly Action<Exception> _logError;
    private readonly Action<long, long> _logMemoryLimitPaused;
    private readonly ChannelWriter<PendingFetchData>? _channelWriter;

    /// <summary>
    /// The current in-flight prefetch task, if any. Exposed for testing.
    /// </summary>
    internal Task? InFlightPrefetch { get; private set; }

    /// <summary>
    /// The current consecutive error count. Exposed for testing.
    /// </summary>
    internal int ConsecutiveErrors { get; private set; }

    /// <summary>
    /// Maximum consecutive errors before the loop surfaces an error and exits.
    /// </summary>
    internal const int MaxConsecutiveErrors = 50;

    public PrefetchPipelineRunner(
        Func<CancellationToken, ValueTask> ensureAssignment,
        Func<int> getAssignmentCount,
        Func<long> getMaxBytes,
        Func<long> getPrefetchedBytes,
        Func<CancellationToken, ValueTask> prefetchRecords,
        Func<CancellationToken, Task> waitForMemoryAvailable,
        Action<Exception> logError,
        Action<long, long> logMemoryLimitPaused,
        ChannelWriter<PendingFetchData>? channelWriter = null)
    {
        _ensureAssignment = ensureAssignment;
        _getAssignmentCount = getAssignmentCount;
        _getMaxBytes = getMaxBytes;
        _getPrefetchedBytes = getPrefetchedBytes;
        _prefetchRecords = prefetchRecords;
        _waitForMemoryAvailable = waitForMemoryAvailable;
        _logError = logError;
        _logMemoryLimitPaused = logMemoryLimitPaused;
        _channelWriter = channelWriter;
    }

    /// <summary>
    /// Runs the pipelined prefetch loop until cancellation.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _ensureAssignment(cancellationToken).ConfigureAwait(false);

                    if (_getAssignmentCount() == 0)
                    {
                        // No assignment — drain any in-flight fetch before waiting.
                        // Use safe variant: in-flight fetch errors are cleanup, not fetch attempts.
                        await DrainInFlightPrefetchSafelyAsync().ConfigureAwait(false);
                        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    // Check memory limit
                    var maxBytes = _getMaxBytes();
                    var currentPrefetchedBytes = _getPrefetchedBytes();
                    if (currentPrefetchedBytes >= maxBytes)
                    {
                        // At memory limit — drain any in-flight fetch before pausing.
                        // Use safe variant: in-flight fetch errors are cleanup, not fetch attempts.
                        await DrainInFlightPrefetchSafelyAsync().ConfigureAwait(false);
                        _logMemoryLimitPaused(currentPrefetchedBytes, maxBytes);
                        await _waitForMemoryAvailable(cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    // If we have an in-flight fetch from the previous iteration, await it.
                    // Its network round-trip overlapped with the loop overhead above.
                    if (InFlightPrefetch is not null)
                    {
                        await DrainInFlightPrefetchAsync().ConfigureAwait(false);

                        // Re-check memory limit after the in-flight fetch added data.
                        currentPrefetchedBytes = _getPrefetchedBytes();
                        if (currentPrefetchedBytes >= maxBytes)
                            continue;
                    }

                    // Fetch records into prefetch channel
                    await _prefetchRecords(cancellationToken).ConfigureAwait(false);
                    ConsecutiveErrors = 0; // Reset on success

                    // Pipeline: eagerly start the next fetch if memory allows.
                    currentPrefetchedBytes = _getPrefetchedBytes();
                    if (currentPrefetchedBytes < maxBytes && !cancellationToken.IsCancellationRequested)
                    {
                        InFlightPrefetch = _prefetchRecords(cancellationToken).AsTask();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Drain in-flight fetch to observe its exception and prevent unobserved task leaks.
                    if (InFlightPrefetch is not null)
                    {
                        var pending = InFlightPrefetch;
                        InFlightPrefetch = null;
                        try { await pending.ConfigureAwait(false); }
                        catch (Exception inFlightEx)
                        {
                            ConsecutiveErrors++;
                            _logError(inFlightEx);
                        }
                    }
                    ConsecutiveErrors++;
                    _logError(ex);

                    if (ConsecutiveErrors >= MaxConsecutiveErrors)
                    {
                        _channelWriter?.TryComplete(
                            new KafkaException(ErrorCode.UnknownServerError,
                                $"Prefetch loop failed {ConsecutiveErrors} consecutive times, last error: {ex.Message}", ex));
                        return;
                    }

                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            // Drain any in-flight fetch to observe exceptions and prevent fire-and-forget leaks
            if (InFlightPrefetch is not null)
            {
                var pending = InFlightPrefetch;
                InFlightPrefetch = null;
                try
                {
                    await pending.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                }
                catch (Exception ex)
                {
                    _logError(ex);
                }
            }

            _channelWriter?.TryComplete();
        }
    }

    /// <summary>
    /// Drains and nulls the in-flight prefetch task.
    /// Exceptions propagate to the caller.
    /// </summary>
    private async Task DrainInFlightPrefetchAsync()
    {
        if (InFlightPrefetch is null)
            return;

        var pending = InFlightPrefetch;
        InFlightPrefetch = null;
        await pending.ConfigureAwait(false);
    }

    /// <summary>
    /// Drains and nulls the in-flight prefetch task, absorbing any exceptions.
    /// Used in cleanup paths (no-assignment, memory-limit) where the in-flight fetch error
    /// should not propagate or increment <see cref="ConsecutiveErrors"/>.
    /// </summary>
    private async Task DrainInFlightPrefetchSafelyAsync()
    {
        if (InFlightPrefetch is null)
            return;

        var pending = InFlightPrefetch;
        InFlightPrefetch = null;
        try
        {
            await pending.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logError(ex);
        }
    }
}
