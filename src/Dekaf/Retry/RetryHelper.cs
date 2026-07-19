using Dekaf.Metadata;
using Dekaf.Networking;

namespace Dekaf.Retry;

/// <summary>
/// Centralized retry logic for retriable Kafka errors.
/// Catches retriable Kafka and transport failures,
/// refreshes metadata, and retries up to <see cref="MaxRetries"/> times with KIP-580 backoff.
/// </summary>
internal static class RetryHelper
{
    internal const int MaxRetries = 3;

    /// <summary>
    /// Executes an async operation with retry logic for retriable Kafka errors.
    /// On retriable failure, refreshes metadata, invokes the optional <paramref name="onRetry"/>
    /// callback (e.g. to re-discover a coordinator), and retries after KIP-580 backoff.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retries. Defaults to <see cref="MaxRetries"/> (3).</param>
    internal static async ValueTask WithRetryAsync(
        Func<ValueTask> operation,
        MetadataManager metadataManager,
        CancellationToken cancellationToken,
        int retryBackoffMs = 100,
        int retryBackoffMaxMs = 1000,
        Func<CancellationToken, ValueTask>? onRetry = null,
        int maxRetries = MaxRetries)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await operation().ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (IsRetriableRequestFailure(ex) && attempt < maxRetries)
            {
                await RefreshMetadataForRetryAsync(metadataManager, cancellationToken).ConfigureAwait(false);

                if (onRetry is not null)
                    await onRetry(cancellationToken).ConfigureAwait(false);

                var delayMs = ExponentialRetryBackoff.CalculateDelayMilliseconds(
                    retryBackoffMs,
                    retryBackoffMaxMs,
                    attempt + 1);
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Executes an async operation with retry logic for retriable Kafka errors,
    /// returning a result on success. On retriable failure, refreshes metadata,
    /// invokes the optional <paramref name="onRetry"/> callback, and retries after KIP-580 backoff.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retries. Defaults to <see cref="MaxRetries"/> (3).</param>
    /// <param name="shouldRefreshMetadata">
    /// Optional predicate that suppresses metadata refresh for errors recovered by <paramref name="onRetry"/>.
    /// </param>
    internal static async ValueTask<T> WithRetryAsync<T>(
        Func<ValueTask<T>> operation,
        MetadataManager metadataManager,
        CancellationToken cancellationToken,
        int retryBackoffMs = 100,
        int retryBackoffMaxMs = 1000,
        Func<CancellationToken, ValueTask>? onRetry = null,
        int maxRetries = MaxRetries,
        Func<KafkaException, bool>? shouldRefreshMetadata = null)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception ex) when (IsRetriableRequestFailure(ex) && attempt < maxRetries)
            {
                if (ex is not KafkaException kafkaException ||
                    shouldRefreshMetadata?.Invoke(kafkaException) != false)
                {
                    await RefreshMetadataForRetryAsync(metadataManager, cancellationToken).ConfigureAwait(false);
                }

                if (onRetry is not null)
                    await onRetry(cancellationToken).ConfigureAwait(false);

                var delayMs = ExponentialRetryBackoff.CalculateDelayMilliseconds(
                    retryBackoffMs,
                    retryBackoffMaxMs,
                    attempt + 1);
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    internal static bool IsRetriableRequestFailure(Exception exception)
    {
        if (exception is KafkaException kafkaException)
            return kafkaException.IsRetriable;

        if (exception is IOException
            or System.Net.Sockets.SocketException
            or TimeoutException
            or DnsResolutionException)
            return true;

        if (exception is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.InnerExceptions)
            {
                if (IsRetriableRequestFailure(innerException))
                    return true;
            }
        }

        return exception.InnerException is not null
            && IsRetriableRequestFailure(exception.InnerException);
    }

    private static async ValueTask RefreshMetadataForRetryAsync(
        MetadataManager metadataManager,
        CancellationToken cancellationToken)
    {
        try
        {
            await metadataManager.RefreshMetadataAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (
            ex is not ObjectDisposedException
            && !cancellationToken.IsCancellationRequested)
        {
            // Refresh is best-effort. Keep retrying the original operation so its typed
            // Kafka failure remains the final error when every broker is unavailable.
        }
        catch (Exception ex) when (
            IsRetriableRequestFailure(ex)
            && !cancellationToken.IsCancellationRequested)
        {
            // Same best-effort behavior for transient transport failures.
        }
    }
}
