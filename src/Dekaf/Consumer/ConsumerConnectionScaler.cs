using System.Diagnostics;

namespace Dekaf.Consumer;

/// <summary>
/// Monitors prefetch pipeline utilization and triggers connection scaling.
/// Scale-up: when all pipeline slots are saturated for >5 seconds.
/// Scale-down: when utilization is below 30% for >120 seconds.
/// Cooldown: 5 seconds between any scale operations.
/// </summary>
internal sealed class ConsumerConnectionScaler
{
    /// <summary>
    /// Maximum number of fetch connections per broker. Used for stackalloc sizing
    /// and as an upper bound for connection scaling.
    /// </summary>
    internal const int MaxFetchConnectionsPerBroker = 8;

    private static readonly TimeSpan ScaleUpSustained = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ScaleDownSustained = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(5);

    private readonly int _initialConnectionCount;
    private readonly int _maxConnectionCount;
    private readonly Func<CancellationToken, ValueTask> _scaleUpAsync;
    private readonly Func<CancellationToken, ValueTask> _scaleDownAsync;
    private readonly Action<Exception>? _logError;
    private readonly object _operationLock = new();
    private readonly HashSet<Task> _pendingOperations = [];
    private readonly CancellationTokenSource _operationCancellationSource = new();

    private int _currentConnectionCount;
    private long _saturationStartTimestamp;
    private long _lowUtilizationStartTimestamp;
    private long _lastScaleTimestamp;
    private long _testTimeOffsetTicks;
    private int _stopping;

    internal Action? BeforeScaleOperationLockForTest { get; set; }

    public int CurrentConnectionCount => Interlocked.CompareExchange(ref _currentConnectionCount, 0, 0);

    public ConsumerConnectionScaler(
        int initialConnectionCount,
        int maxConnectionCount,
        Func<CancellationToken, ValueTask> scaleUpAsync,
        Func<CancellationToken, ValueTask> scaleDownAsync,
        Action<Exception>? logError = null)
    {
        _initialConnectionCount = initialConnectionCount;
        _maxConnectionCount = maxConnectionCount;
        _scaleUpAsync = scaleUpAsync;
        _scaleDownAsync = scaleDownAsync;
        _logError = logError;
        _currentConnectionCount = initialConnectionCount;
    }

    private long GetTimestamp() => Stopwatch.GetTimestamp() + _testTimeOffsetTicks;

    /// <summary>
    /// Reports current pipeline utilization. Call this each time a fetch completes or is dispatched.
    /// </summary>
    public void ReportPipelineUtilization(int inFlightCount, int pipelineDepth)
    {
        var now = GetTimestamp();
        var isSaturated = inFlightCount >= pipelineDepth;

        if (isSaturated)
        {
            if (_saturationStartTimestamp == 0)
                _saturationStartTimestamp = now;
        }
        else if (_saturationStartTimestamp != 0)
        {
            _saturationStartTimestamp = 0;
        }

        // Integer comparison equivalent to: (inFlightCount / pipelineDepth) < 0.3
        var isLowUtilization = pipelineDepth > 0 && inFlightCount * 10 < pipelineDepth * 3;

        if (isLowUtilization)
        {
            if (_lowUtilizationStartTimestamp == 0)
                _lowUtilizationStartTimestamp = now;
        }
        else if (_lowUtilizationStartTimestamp != 0)
        {
            _lowUtilizationStartTimestamp = 0;
        }
    }

    /// <summary>
    /// Evaluates scaling conditions and triggers scale-up or scale-down if thresholds are met.
    /// </summary>
    public void MaybeScale()
    {
        BeforeScaleOperationLockForTest?.Invoke();

        var now = GetTimestamp();
        Func<CancellationToken, ValueTask>? operation = null;
        TaskCompletionSource? operationReservation = null;
        lock (_operationLock)
        {
            if (_stopping != 0)
                return;

            if (_lastScaleTimestamp != 0 && Stopwatch.GetElapsedTime(_lastScaleTimestamp, now) < Cooldown)
                return;

            if (_saturationStartTimestamp != 0
                && _currentConnectionCount < _maxConnectionCount
                && Stopwatch.GetElapsedTime(_saturationStartTimestamp, now) >= ScaleUpSustained)
            {
                Interlocked.Increment(ref _currentConnectionCount);
                _saturationStartTimestamp = 0;
                _lastScaleTimestamp = now;
                operation = _scaleUpAsync;
            }
            else if (_lowUtilizationStartTimestamp != 0
                && _currentConnectionCount > _initialConnectionCount
                && Stopwatch.GetElapsedTime(_lowUtilizationStartTimestamp, now) >= ScaleDownSustained)
            {
                Interlocked.Decrement(ref _currentConnectionCount);
                _lowUtilizationStartTimestamp = 0;
                _lastScaleTimestamp = now;
                operation = _scaleDownAsync;
            }

            if (operation is not null)
            {
                operationReservation = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingOperations.Add(operationReservation.Task);
            }
        }

        if (operationReservation is not null)
        {
            ObserveOperation(operationReservation.Task);
            StartReservedOperation(operation!, operationReservation);
        }
    }

    /// <summary>
    /// Returns the number of connections available for fetch requests.
    /// When connectionsPerBroker is 1, all traffic shares the single connection.
    /// When > 1, the last connection is reserved for coordination.
    /// </summary>
    internal static int GetFetchConnectionCount(int connectionsPerBroker)
        => connectionsPerBroker > 1 ? connectionsPerBroker - 1 : 1;

    private void StartReservedOperation(
        Func<CancellationToken, ValueTask> action,
        TaskCompletionSource operationReservation)
    {
        try
        {
            var cancellationToken = _operationCancellationSource.Token;
            cancellationToken.ThrowIfCancellationRequested();
            var operation = action(cancellationToken);
            if (operation.IsCompletedSuccessfully)
            {
                operationReservation.TrySetResult();
                return;
            }

            _ = CompleteReservedOperationAsync(operation, operationReservation);
        }
        catch (OperationCanceledException ex)
        {
            operationReservation.TrySetCanceled(ex.CancellationToken);
        }
        catch (Exception ex)
        {
            // Preserve synchronous delegate semantics: report before MaybeScale returns.
            _logError?.Invoke(ex);
            operationReservation.TrySetResult();
        }
    }

    private static async Task CompleteReservedOperationAsync(
        ValueTask operation,
        TaskCompletionSource operationReservation)
    {
        try
        {
            await operation.ConfigureAwait(false);
            operationReservation.TrySetResult();
        }
        catch (OperationCanceledException ex)
        {
            operationReservation.TrySetCanceled(ex.CancellationToken);
        }
        catch (Exception ex)
        {
            operationReservation.TrySetException(ex);
        }
    }

    private void ObserveOperation(Task operationTask)
    {
        _ = operationTask.ContinueWith(
            static (task, state) => ((ConsumerConnectionScaler)state!).ObserveCompletedOperation(task),
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void ObserveCompletedOperation(Task task)
    {
        lock (_operationLock)
            _pendingOperations.Remove(task);

        if (task.Exception is not null)
            _logError?.Invoke(task.Exception.InnerException ?? task.Exception);
    }

    internal async ValueTask StopAndDrainAsync(TimeSpan timeout)
    {
        try
        {
            await StopAndDrainCoreAsync().AsTask().WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Core drain continues observing the pending operations after disposal moves on.
        }
    }

    internal ValueTask StopAndDrainAsync() => StopAndDrainCoreAsync();

    private async ValueTask StopAndDrainCoreAsync()
    {
        Task[] pendingOperations;
        bool cancelOperations;
        lock (_operationLock)
        {
            cancelOperations = _stopping == 0;
            Volatile.Write(ref _stopping, 1);
            pendingOperations = [.. _pendingOperations];
        }

        if (cancelOperations)
            _operationCancellationSource.Cancel();

        if (pendingOperations.Length == 0)
            return;

        try
        {
            await Task.WhenAll(pendingOperations).ConfigureAwait(false);
        }
        catch
        {
            // Each operation's continuation observes and logs its exception.
        }
    }

    /// <summary>
    /// Splits partitions across fetch connections using chunked distribution.
    /// Returns the number of groups written to <paramref name="groups"/>.
    /// Each group is a (startIndex, count) range into the original partition list.
    /// When <paramref name="fetchConnectionCount"/> is 1, returns a single group spanning all partitions.
    /// </summary>
    internal static int SplitPartitionsAcrossConnections(
        int partitionCount,
        int fetchConnectionCount,
        Span<(int StartIndex, int Count)> groups)
    {
        Debug.Assert(groups.Length >= Math.Min(fetchConnectionCount, partitionCount),
            $"groups span too small: {groups.Length} < Min({fetchConnectionCount}, {partitionCount})");

        if (fetchConnectionCount <= 1 || partitionCount <= 1)
        {
            groups[0] = (0, partitionCount);
            return 1;
        }

        // Limit connections to partition count (no empty groups)
        var effectiveConnections = Math.Min(fetchConnectionCount, partitionCount);
        var baseSize = partitionCount / effectiveConnections;
        var remainder = partitionCount % effectiveConnections;

        var offset = 0;
        for (var i = 0; i < effectiveConnections; i++)
        {
            var count = baseSize + (i < remainder ? 1 : 0);
            groups[i] = (offset, count);
            offset += count;
        }

        return effectiveConnections;
    }

    internal void TestAdvanceTime(TimeSpan duration)
        => _testTimeOffsetTicks += (long)(duration.TotalSeconds * Stopwatch.Frequency);

    internal void TestSetConnectionCount(int count)
        => _currentConnectionCount = count;
}
