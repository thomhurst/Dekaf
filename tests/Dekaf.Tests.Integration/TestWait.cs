using System.Diagnostics;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Shared polling waits for integration tests. Lives outside <see cref="KafkaIntegrationTest"/>
/// so test classes bound to other container fixtures (rack-aware, scale-out, ACL, SASL) can use
/// the same mechanism instead of hand-rolling per-class copies.
/// </summary>
internal static class TestWait
{
    /// <summary>
    /// Polls until a condition is true, replacing fixed <c>Task.Delay</c> waits.
    /// Returns as soon as the condition is met, avoiding unnecessary delays. On timeout it
    /// throws a <see cref="TimeoutException"/> naming <paramref name="description"/> rather
    /// than a bare cancellation, so a stuck test reports what it was waiting for.
    /// </summary>
    public static async Task WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan timeout,
        int pollIntervalMs = 100,
        string? description = null)
    {
        var startedAt = Stopwatch.GetTimestamp();
        while (!condition())
        {
            var remaining = timeout - Stopwatch.GetElapsedTime(startedAt);
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException(
                    $"Condition not met within {timeout.TotalSeconds:F0}s" +
                    $"{(description is null ? "" : $": {description}")}");
            }

            await Task.Delay(Math.Min(pollIntervalMs, (int)Math.Ceiling(remaining.TotalMilliseconds)));
        }
    }

    /// <summary>
    /// Polls an async check with linear backoff until the condition holds. Admin operations in
    /// Kafka are eventually consistent, so a change may lag the acknowledged mutation. When the
    /// retries are exhausted this throws a <see cref="TimeoutException"/> carrying the last
    /// observed value instead of returning it, so an unmet condition fails here with the stale
    /// state — not at a later assert, and never silently.
    /// </summary>
    public static async Task<T> WaitForConditionAsync<T>(
        Func<Task<T>> check,
        Func<T, bool> condition,
        int maxRetries = 10,
        int initialDelayMs = 500,
        string? description = null,
        Func<T, string>? formatObserved = null)
    {
        T result = default!;
        for (var i = 0; i < maxRetries; i++)
        {
            await Task.Delay(initialDelayMs * (i + 1));
            result = await check();
            if (condition(result))
                return result;
        }

        var observed = formatObserved is null ? result?.ToString() : formatObserved(result);
        throw new TimeoutException(
            $"Condition not met after {maxRetries} checks" +
            $"{(description is null ? "" : $": {description}")}; last observed: {observed ?? "<null>"}");
    }
}
