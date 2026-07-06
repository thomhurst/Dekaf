namespace Dekaf.Consumer;

internal sealed class BrokerPrefetchScheduler
{
    private readonly Dictionary<(int BrokerId, int ConnectionIndex), Task> _inFlight = [];
    private readonly List<KeyValuePair<(int BrokerId, int ConnectionIndex), Task>> _completed = [];

    public int InFlightCount => _inFlight.Count;

    public bool HasInFlight => _inFlight.Count > 0;

    public bool TryStart(
        (int BrokerId, int ConnectionIndex) key,
        Func<Task> taskFactory)
    {
        if (_inFlight.ContainsKey(key))
            return false;

        _inFlight.Add(key, taskFactory());
        return true;
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
                    await task.ConfigureAwait(false);
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

        var tasks = _inFlight.Values.ToArray();
        await Task.WhenAny(tasks).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DrainAllSafelyAsync(Action<Exception> logError)
    {
        if (_inFlight.Count == 0)
            return;

        var tasks = _inFlight.Values.ToArray();
        _inFlight.Clear();

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
            }
        }
    }
}
