namespace Dekaf.SchemaRegistry;

internal static class SchemaRegistryOperationTimeout
{
    internal static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operationFactory,
        TimeSpan timeout,
        string timeoutMessage)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);
        var operation = operationFactory(timeoutSource.Token);
        try
        {
            return await operation.WaitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (timeoutSource.IsCancellationRequested)
        {
            ObserveAbandonedFault(operation);
            throw new TimeoutException(timeoutMessage, exception);
        }
    }

    private static void ObserveAbandonedFault<T>(Task<T> operation)
    {
        if (operation.IsCompleted)
        {
            _ = operation.Exception;
            return;
        }

        _ = operation.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
