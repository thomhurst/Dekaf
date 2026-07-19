using Docker.DotNet;
using DotNet.Testcontainers.Containers;

namespace Dekaf.Tests.Integration;

internal static class ContainerStartupRetry
{
    private const int MaxAttempts = 3;

    internal static async Task RunAsync(
        Func<Task> startAttemptAsync,
        Func<ValueTask> cleanupAttemptAsync,
        Func<Exception, bool> isTransient,
        Func<TimeSpan, Task>? delayAsync = null)
    {
        delayAsync ??= static delay => Task.Delay(delay);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await startAttemptAsync().ConfigureAwait(false);
                return;
            }
            catch (Exception exception)
            {
                await TryCleanupAsync(cleanupAttemptAsync).ConfigureAwait(false);

                if (attempt == MaxAttempts || !isTransient(exception))
                    throw;

                Console.WriteLine(
                    $"[ContainerStartupRetry] Transient container startup failure on attempt {attempt}; retrying with fresh resources: {exception.Message}");

                await delayAsync(GetRetryDelay(attempt)).ConfigureAwait(false);
            }
        }
    }

    internal static bool IsKnownTransient(Exception exception) =>
        IsPortBindingCollision(exception) ||
        IsKafkaStartupScriptBusy(exception) ||
        IsRegistryUnavailable(exception);

    internal static bool IsPortBindingCollision(Exception exception) =>
        Contains(exception, static candidate =>
            candidate is DockerApiException &&
            (candidate.Message.Contains("port is already allocated", StringComparison.OrdinalIgnoreCase) ||
             candidate.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase) ||
             candidate.Message.Contains("ports are not available", StringComparison.OrdinalIgnoreCase) ||
             candidate.Message.Contains(
                 "Only one usage of each socket address",
                 StringComparison.OrdinalIgnoreCase)));

    internal static bool IsKafkaStartupScriptBusy(Exception exception) =>
        Contains(exception, static candidate =>
            candidate is ContainerNotRunningException &&
            candidate.Message.Contains(
                "/testcontainers.sh: Text file busy",
                StringComparison.Ordinal));

    internal static bool IsRegistryUnavailable(Exception exception) =>
        Contains(exception, static candidate =>
        {
            if (candidate is not DockerApiException dockerException)
                return false;

            var statusCode = (int)dockerException.StatusCode;
            return statusCode is >= 500 and <= 599 &&
                   candidate.Message.Contains("/manifests/", StringComparison.OrdinalIgnoreCase) &&
                   candidate.Message.Contains(
                       "received unexpected HTTP status",
                       StringComparison.OrdinalIgnoreCase);
        });

    private static TimeSpan GetRetryDelay(int failedAttempt) => failedAttempt switch
    {
        1 => TimeSpan.FromSeconds(5),
        _ => TimeSpan.FromSeconds(15)
    };

    private static bool Contains(Exception exception, Func<Exception, bool> predicate)
    {
        if (predicate(exception))
            return true;

        return exception switch
        {
            AggregateException aggregateException =>
                aggregateException.InnerExceptions.Any(inner => Contains(inner, predicate)),
            { InnerException: not null } => Contains(exception.InnerException, predicate),
            _ => false
        };
    }

    private static async ValueTask TryCleanupAsync(Func<ValueTask> cleanupAttemptAsync)
    {
        try
        {
            await cleanupAttemptAsync().ConfigureAwait(false);
        }
        catch (Exception cleanupException)
        {
            Console.Error.WriteLine(
                $"[ContainerStartupRetry] Failed to clean up a rejected startup attempt: {cleanupException}");
        }
    }
}
