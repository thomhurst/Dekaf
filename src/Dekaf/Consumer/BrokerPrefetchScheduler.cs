using Dekaf.Internal;

namespace Dekaf.Consumer;

/// <summary>
/// Tracks the single in-flight prefetch task per <c>(broker, connection)</c> key on behalf of
/// the central prefetch loop and wakes that loop when any task returns.
/// </summary>
/// <remarks>
/// <para><b>Loop ownership.</b> The central prefetch loop (<c>KafkaConsumer.PrefetchLoopAsync</c>)
/// owns everything that decides <em>which</em> partitions are fetched from <em>where</em>:
/// assignment changes and rebalances, pause/resume, seeks and fetch-buffer epochs, connection
/// routing width and its transitions, fetch-session cleanup for keys that left the plan, error
/// backoff, and prefetch-memory admission. It starts exactly one task for every planned key
/// that has no running task.</para>
/// <para>Each task owns its connection for as long as the plan it was started with stays valid.
/// After publishing a response it re-validates that plan with a handful of volatile reads
/// (<c>KafkaConsumer.CanReissueBrokerPrefetch</c>) and, when nothing changed, immediately
/// issues the next fetch on the same leased connection without returning here. It hands
/// control back to the loop only when that validation fails, when a response carries anything
/// the loop must react to (session or partition errors, epoch resets, topic identity or
/// preferred-replica changes, stale partitions), when memory admission is exhausted, or when it
/// is cancelled. KIP-227 semantics hold because a key never has more than one task and a task
/// never has more than one request in flight on its fetch session.</para>
/// <para><b>Wake-up.</b> Completion of any task fires one reusable
/// <see cref="AsyncAutoResetSignal"/>; <see cref="WaitForAnyAsync"/> awaits that signal instead
/// of snapshotting the in-flight tasks into a <c>Task.WhenAny</c>, so waking the loop allocates
/// nothing after the first wait. The signal is bound to the first cancellation token it waits
/// with, which is the prefetch loop's lifetime token.</para>
/// <para>Apart from the completion callback, every member is called only by the loop that owns
/// this scheduler, so no other synchronization is needed.</para>
/// </remarks>
internal sealed class BrokerPrefetchScheduler : IDisposable
{
    private readonly Dictionary<(int BrokerId, int ConnectionIndex), Task> _inFlight = [];
    private readonly List<KeyValuePair<(int BrokerId, int ConnectionIndex), Task>> _completed = [];
    // A completed task signals from its own continuation and has nothing left to do, so the loop
    // resumes inline on that thread instead of paying a thread-pool hop per wake, exactly as the
    // former Task.WhenAny/WaitAsync promises resumed it.
    private readonly AsyncAutoResetSignal _completionSignal = new(inlineSignalContinuations: true);
    private readonly Action _signalCompletion;
    // Only callbacks share the completion counter; removals belong to the loop, so
    // draining needs no atomic write. Removals may lead queued callbacks temporarily:
    // those callbacks repay the difference instead of waking a reused key spuriously.
    private long _completedTaskCount;
    private long _removedTaskCount;

    public BrokerPrefetchScheduler()
    {
        _signalCompletion = OnTaskCompleted;
    }

    public int InFlightCount => _inFlight.Count;

    public bool HasInFlight => _inFlight.Count > 0;

    public bool TryStart(
        (int BrokerId, int ConnectionIndex) key,
        Func<Task> taskFactory)
        => TryStart(key, taskFactory, static factory => factory());

    public bool TryStart<TState>(
        (int BrokerId, int ConnectionIndex) key,
        TState state,
        Func<TState, Task> taskFactory)
    {
        if (_inFlight.ContainsKey(key))
            return false;

        var task = taskFactory(state);
        _inFlight.Add(key, task);
        // Completion only wakes the loop; the task's result and any exception are harvested
        // by DrainCompletedAsync. A first continuation on a task stores the delegate directly,
        // so this registration does not allocate.
        if (task.IsCompleted)
            Interlocked.Increment(ref _completedTaskCount);
        else
            task.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(_signalCompletion);
        return true;
    }

    private void OnTaskCompleted()
    {
        Interlocked.Increment(ref _completedTaskCount);
        _completionSignal.Signal();
    }

    public async ValueTask<int> DrainCompletedAsync()
    {
        foreach (var entry in _inFlight)
        {
            if (entry.Value.IsCompleted)
                _completed.Add(entry);
        }

        try
        {
            for (var i = 0; i < _completed.Count; i++)
            {
                var (key, task) = _completed[i];
                if (_inFlight.Remove(key))
                {
                    _removedTaskCount++;
                    await task.ConfigureAwait(false);
                }
            }

            return _completed.Count;
        }
        finally
        {
            _completed.Clear();
        }
    }

    /// <summary>
    /// Returns once at least one tracked task has completed. Returns immediately when a
    /// completed task is already waiting to be drained; otherwise awaits the completion signal.
    /// A signal left over from a drained task is checked against the completion balance,
    /// so it never produces a wake-up with nothing to drain. The check is O(1).
    /// </summary>
    public async ValueTask WaitForAnyAsync(CancellationToken cancellationToken)
    {
        if (_inFlight.Count == 0)
            return;

        _completionSignal.RegisterShutdownToken(cancellationToken);

        while (Volatile.Read(ref _completedTaskCount) <= _removedTaskCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _completionSignal.WaitAsync(Timeout.Infinite).ConfigureAwait(false);
        }
    }

    public async ValueTask<Exception?> DrainAllSafelyAsync(
        Action<Exception> logError,
        Func<Exception, bool> shouldReturn)
    {
        if (_inFlight.Count == 0)
            return null;

        var tasks = _inFlight.Values.ToArray();
        _inFlight.Clear();
        _removedTaskCount += tasks.Length;
        Exception? firstFailure = null;

        for (var i = 0; i < tasks.Length; i++)
        {
            try
            {
                await tasks[i].ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                logError(ex);
                if (firstFailure is null && shouldReturn(ex))
                    firstFailure = ex;
            }
        }

        return firstFailure;
    }

    public void Dispose() => _completionSignal.Dispose();
}
