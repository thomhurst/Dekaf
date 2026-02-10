namespace Dekaf.Statistics;

/// <summary>
/// Background timer that periodically emits statistics.
/// </summary>
internal sealed class StatisticsEmitter<TStatistics> : IAsyncDisposable
{
    private readonly TimeSpan _interval;
    private readonly Func<TStatistics> _collectStatistics;
    private readonly Action<TStatistics> _handler;
    private readonly CancellationTokenSource _cts;
    private readonly Task _emitTask;
    private volatile bool _disposed;

    public StatisticsEmitter(
        TimeSpan interval,
        Func<TStatistics> collectStatistics,
        Action<TStatistics> handler)
    {
        _interval = interval;
        _collectStatistics = collectStatistics;
        _handler = handler;
        _cts = new CancellationTokenSource();
        _emitTask = EmitLoopAsync(_cts.Token);
    }

    private async Task EmitLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, cancellationToken).ConfigureAwait(false);

                var stats = _collectStatistics();
                _handler(stats);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Swallow exceptions in statistics handler to not affect main client operation
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        _cts.Cancel();

        try
        {
            await _emitTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch
        {
            // Ignore cancellation exceptions
        }

        _cts.Dispose();
    }
}
