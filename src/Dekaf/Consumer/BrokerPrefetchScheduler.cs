namespace Dekaf.Consumer;

internal sealed class BrokerPrefetchScheduler
{
    private readonly Dictionary<(int BrokerId, int ConnectionIndex), Task> _inFlight = [];
    private readonly List<KeyValuePair<(int BrokerId, int ConnectionIndex), Task>> _completed = [];
    // Maintained during add/drain so the common single-task wait needs no dictionary enumeration.
    private Task? _singleInFlightTask;

    public int InFlightCount => _inFlight.Count;

    public bool HasInFlight => _inFlight.Count > 0;

    public bool TryStart(
        (int BrokerId, int ConnectionIndex) key,
        Func<Task> taskFactory)
    {
        if (_inFlight.ContainsKey(key))
            return false;

        var task = taskFactory();
        _inFlight.Add(key, task);
        _singleInFlightTask = _inFlight.Count == 1 ? task : null;
        return true;
    }

    public async ValueTask<int> DrainCompletedAsync()
    {
        Task? solePendingTask = null;
        var pendingCount = 0;

        foreach (var entry in _inFlight)
        {
            if (entry.Value.IsCompleted)
                _completed.Add(entry);
            else
            {
                pendingCount++;
                solePendingTask = pendingCount == 1 ? entry.Value : null;
            }
        }

        try
        {
            for (var i = 0; i < _completed.Count; i++)
            {
                var (key, task) = _completed[i];
                if (_inFlight.Remove(key))
                {
                    _singleInFlightTask = _inFlight.Count == 1
                        ? solePendingTask ?? _completed[^1].Value
                        : null;
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

    public async ValueTask WaitForAnyAsync(CancellationToken cancellationToken)
    {
        if (_inFlight.Count == 0)
            return;

        if (_inFlight.Count == 1)
        {
            var task = _singleInFlightTask!;

            if (!task.IsCompleted)
            {
                var waitTask = task.WaitAsync(cancellationToken);
                await waitTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

                if (waitTask.IsCanceled && cancellationToken.IsCancellationRequested)
                    cancellationToken.ThrowIfCancellationRequested();
            }

            return;
        }

        var tasks = _inFlight.Values.ToArray();
        await Task.WhenAny(tasks).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Exception?> DrainAllSafelyAsync(
        Action<Exception> logError,
        Func<Exception, bool> shouldReturn)
    {
        if (_inFlight.Count == 0)
            return null;

        var tasks = _inFlight.Values.ToArray();
        _inFlight.Clear();
        _singleInFlightTask = null;
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
}
