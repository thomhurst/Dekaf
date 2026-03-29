using System.Threading.Channels;
using Dekaf.Errors;
using Dekaf.Protocol;

namespace Dekaf.Consumer;

/// <summary>
/// Encapsulates the pipelined prefetch state machine for testability.
/// The runner manages in-flight prefetch tasks and the interaction between
/// assignment checks, memory limits, fetch execution, and error handling.
///
/// <para>The pipeline uses a drain-fetch-fill pattern per iteration:
/// <list type="number">
///   <item>Drain the oldest in-flight fetch (its network round-trip overlapped with the
///   previous iteration's fetch and the loop overhead).</item>
///   <item>Execute a synchronous fetch that delivers data and updates positions.</item>
///   <item>Start one eager fetch in the background, up to <c>pipelineDepth - 1</c> in-flight.</item>
/// </list>
/// This keeps the network saturated: while the synchronous fetch blocks, the eager fetch
/// from the previous iteration's step 3 is completing in the background. When the sync
/// fetch finishes, a new eager fetch starts immediately, overlapping with the consume
/// loop's record processing.</para>
///
/// <para>With depth 1, fetches are purely sequential (no eager pipelining).
/// With depth 2, one eager fetch overlaps with the synchronous fetch.
/// With depth 3 (the default), two eager fetches can overlap with processing, keeping
/// the network saturated even when individual fetches stall briefly.
/// Higher depths (up to 8) allow more overlapping fetches for improved throughput.
/// Each in-flight fetch registers its own <c>CancellationTokenSource</c> in
/// <c>KafkaConsumer._activeWakeupSources</c>, so <c>Wakeup()</c> correctly cancels
/// all concurrent fetches regardless of pipeline depth.</para>
///
/// <para><b>Position safety:</b> Only one eager fetch is started per iteration to prevent
/// reading stale <c>_fetchPositions</c>. Each fetch updates positions inside its task
/// before completing, so the next iteration's drain reads fresh positions. See PR #648.</para>
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
    private readonly int _pipelineDepth;
    private readonly Action<int, int>? _onIterationComplete;
    private readonly Queue<Task> _inFlightQueue = new();

    /// <summary>
    /// The number of currently in-flight prefetch tasks. Exposed for testing.
    /// </summary>
    internal int InFlightPrefetchCount => _inFlightQueue.Count;

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
        ChannelWriter<PendingFetchData>? channelWriter = null,
        int pipelineDepth = 3,
        Action<int, int>? onIterationComplete = null)
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
        _pipelineDepth = pipelineDepth;
        _onIterationComplete = onIterationComplete;
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
                        // No assignment — drain any in-flight fetches before waiting.
                        // Use safe variant: in-flight fetch errors are cleanup, not fetch attempts.
                        await DrainAllInFlightSafelyAsync().ConfigureAwait(false);
                        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    // Check memory limit
                    var maxBytes = _getMaxBytes();
                    var currentPrefetchedBytes = _getPrefetchedBytes();
                    if (currentPrefetchedBytes >= maxBytes)
                    {
                        // At memory limit — pause starting new fetches and wait for the consume
                        // loop to free memory. In-flight fetches complete naturally; their results
                        // will be consumed, releasing memory without a costly drain-all restart.
                        // Note: in-flight fetches may push prefetchedBytes above maxBytes by up to
                        // (pipelineDepth-1) batch sizes before the consume loop drains them. This
                        // is intentional — discarding that work (drain-all) causes a costly cold
                        // restart with FetchMaxWaitMs broker-side delay.
                        _logMemoryLimitPaused(currentPrefetchedBytes, maxBytes);
                        await _waitForMemoryAvailable(cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    // If we have in-flight fetches from a previous iteration, drain the oldest.
                    // Its network round-trip overlapped with the loop overhead above
                    // (ensureAssignment, memory check) and the synchronous fetch of the
                    // previous iteration.
                    if (_inFlightQueue.Count > 0)
                    {
                        await DrainOldestInFlightAsync().ConfigureAwait(false);

                        // Re-check memory limit after the in-flight fetch added data.
                        currentPrefetchedBytes = _getPrefetchedBytes();
                        if (currentPrefetchedBytes >= maxBytes)
                            continue;
                    }

                    // Fetch records into prefetch channel (synchronous call).
                    // This is the primary fetch that delivers data immediately.
                    await _prefetchRecords(cancellationToken).ConfigureAwait(false);
                    ConsecutiveErrors = 0; // Reset on success

                    // Pipeline: eagerly start fetches to fill the pipeline, respecting position safety.
                    // Only one NEW fetch per iteration to avoid reading stale _fetchPositions.
                    // However, if a previously queued fetch has already completed (its task is done),
                    // it has already updated positions, so we can safely drain it and start another.
                    // This accelerates pipeline warm-up and handles fast-completing fetches
                    // (e.g., empty responses when caught up to topic end).
                    currentPrefetchedBytes = _getPrefetchedBytes();
                    if (_inFlightQueue.Count < _pipelineDepth - 1
                        && currentPrefetchedBytes < maxBytes
                        && !cancellationToken.IsCancellationRequested)
                    {
                        _inFlightQueue.Enqueue(_prefetchRecords(cancellationToken).AsTask());

                        // Fast-drain completed fetches and refill: if the oldest in-flight fetch
                        // already completed (e.g., fast empty response), drain it immediately
                        // and start another. This keeps the pipeline full without waiting for
                        // the next iteration's loop overhead.
                        while (_inFlightQueue.Count > 0
                               && _inFlightQueue.Count < _pipelineDepth - 1
                               && _inFlightQueue.Peek().IsCompleted
                               && !cancellationToken.IsCancellationRequested)
                        {
                            await DrainOldestInFlightAsync().ConfigureAwait(false);

                            currentPrefetchedBytes = _getPrefetchedBytes();
                            if (currentPrefetchedBytes >= maxBytes)
                                break;

                            _inFlightQueue.Enqueue(_prefetchRecords(cancellationToken).AsTask());
                        }
                    }

                    _onIterationComplete?.Invoke(_inFlightQueue.Count, _pipelineDepth);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Drain all in-flight fetches to observe their exceptions and prevent unobserved task leaks.
                    await DrainAllInFlightWithErrorCountingAsync().ConfigureAwait(false);
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
            // Drain any in-flight fetches to observe exceptions and prevent fire-and-forget leaks
            await DrainAllInFlightSafelyAsync().ConfigureAwait(false);

            _channelWriter?.TryComplete();
        }
    }

    /// <summary>
    /// Awaits and dequeues the oldest in-flight prefetch task.
    /// Exceptions propagate to the caller.
    /// </summary>
    private async Task DrainOldestInFlightAsync()
    {
        if (_inFlightQueue.TryDequeue(out var oldest))
        {
            await oldest.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Drains all in-flight prefetch tasks, absorbing any exceptions.
    /// Used in cleanup paths (no-assignment) where in-flight fetch errors
    /// should not propagate or increment <see cref="ConsecutiveErrors"/>.
    /// </summary>
    private async Task DrainAllInFlightSafelyAsync()
    {
        while (_inFlightQueue.TryDequeue(out var pending))
        {
            try
            {
                await pending.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected: eager fetch was woken up or cancelled during cleanup — not an error
            }
            catch (Exception ex)
            {
                _logError(ex);
            }
        }
    }

    /// <summary>
    /// Drains all in-flight prefetch tasks, counting errors toward <see cref="ConsecutiveErrors"/>.
    /// Used in the error handler catch block.
    /// </summary>
    private async Task DrainAllInFlightWithErrorCountingAsync()
    {
        while (_inFlightQueue.TryDequeue(out var pending))
        {
            try
            {
                await pending.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is not an error — expected during shutdown/wakeup
            }
            catch (Exception inFlightEx)
            {
                ConsecutiveErrors++;
                _logError(inFlightEx);
            }
        }
    }
}
