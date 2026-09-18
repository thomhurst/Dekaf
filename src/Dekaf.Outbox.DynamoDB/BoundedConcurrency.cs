namespace Dekaf.Outbox.DynamoDB;

internal static class BoundedConcurrency
{
    /// <summary>
    /// Runs <paramref name="body"/> for every index in [0, <paramref name="count"/>) with at
    /// most <paramref name="maxConcurrency"/> requests in flight. DynamoDB addresses one item
    /// per conditional request, so per-bucket work is a loop of requests, not one statement.
    /// </summary>
    public static Task ForAsync(
        int count, int maxConcurrency, Func<int, CancellationToken, ValueTask> body, CancellationToken cancellationToken)
    {
        if (count == 0)
            return Task.CompletedTask;
        // The common case of one bucket needs no scheduling.
        if (count == 1)
            return body(0, cancellationToken).AsTask();

        return Parallel.ForAsync(0, count, new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrency,
            CancellationToken = cancellationToken
        }, body);
    }
}
