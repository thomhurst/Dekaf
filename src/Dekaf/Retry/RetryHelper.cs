using Dekaf.Errors;
using Dekaf.Metadata;

namespace Dekaf.Retry;

/// <summary>
/// Centralized retry logic for retriable Kafka errors.
/// Catches <see cref="KafkaException"/> where <see cref="KafkaException.IsRetriable"/> is true,
/// refreshes metadata, and retries up to <see cref="MaxRetries"/> times with a fixed base delay.
/// </summary>
internal static class RetryHelper
{
    internal const int MaxRetries = 3;
    internal const int RetryDelayMs = 500;

    /// <summary>
    /// Executes an async operation with retry logic for retriable Kafka errors.
    /// On retriable failure, refreshes metadata and retries after a delay.
    /// </summary>
    internal static async ValueTask WithRetryAsync(
        Func<ValueTask> operation,
        MetadataManager metadataManager,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await operation().ConfigureAwait(false);
                return;
            }
            catch (KafkaException ex) when (ex.IsRetriable && attempt < MaxRetries)
            {
                await metadataManager.RefreshMetadataAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(RetryDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Executes an async operation with retry logic for retriable Kafka errors,
    /// returning a result on success.
    /// </summary>
    internal static async ValueTask<T> WithRetryAsync<T>(
        Func<ValueTask<T>> operation,
        MetadataManager metadataManager,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (KafkaException ex) when (ex.IsRetriable && attempt < MaxRetries)
            {
                await metadataManager.RefreshMetadataAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(RetryDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
