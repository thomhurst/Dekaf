namespace Dekaf.Statistics;

/// <summary>
/// Background timer that periodically emits statistics.
/// Uses <see cref="PeriodicTimer"/> instead of <c>Task.Delay</c> to avoid
/// timer allocations on each tick. The <c>PeriodicTimer.WaitForNextTickAsync</c>
/// call is allocation-free after the timer is constructed.
/// </summary>
internal sealed class StatisticsEmitter<TStatistics> : IAsyncDisposable
{
    private readonly Func<TStatistics> _collectStatistics;
    private readonly Action<TStatistics> _handler;
    private readonly CancellationTokenSource _cts;
    private readonly PeriodicTimer _timer;
    private readonly Task _emitTask;
    private volatile bool _disposed;

    public StatisticsEmitter(
        TimeSpan interval,
        Func<TStatistics> collectStatistics,
        Action<TStatistics> handler)
    {
        _collectStatistics = collectStatistics;
        _handler = handler;
        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(interval);
        _emitTask = EmitLoopAsync(_cts.Token);
    }

    private async Task EmitLoopAsync(CancellationToken cancellationToken)
    {
        while (await _timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
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
        _timer.Dispose();

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
