using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Dekaf.Errors;
using Dekaf.Metadata;

namespace Dekaf.Retry;

/// <summary>
/// Opts a <see cref="RetryHelper"/> call into deadline mode: instead of giving up after
/// <see cref="RetryHelper.MaxRetries"/> attempts, transport-level failures are retried with the
/// same KIP-580 backoff until the operation's deadline.
/// </summary>
/// <param name="Operation">Operation name used in the timeout message.</param>
/// <param name="Budget">
/// Time allowed for retries, measured from the first attempt.
/// <see cref="Timeout.InfiniteTimeSpan"/> when the cancellation token already is the operation's
/// aggregate deadline.
/// </param>
/// <param name="IsOwnerDisposed">
/// Reports whether the calling component is disposed. A connection retired by pool churn
/// (<see cref="ObjectDisposedException"/>) is retried only while it is not.
/// </param>
internal readonly record struct RetryDeadline(
    string Operation,
    TimeSpan Budget,
    Func<bool>? IsOwnerDisposed = null);

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
    /// <param name="shouldRefreshMetadata">
    /// Optional predicate that suppresses metadata refresh for errors recovered by other means.
    /// </param>
    /// <param name="deadline">
    /// Opt-in deadline mode (see <see cref="RetryDeadline"/>): transport-level failures are
    /// retried until the deadline, while <paramref name="maxRetries"/> still bounds retriable
    /// errors a broker answered with. Only for operations that are safe to repeat.
    /// </param>
    internal static async ValueTask WithRetryAsync(
        Func<ValueTask> operation,
        MetadataManager metadataManager,
        CancellationToken cancellationToken,
        int retryBackoffMs = 100,
        int retryBackoffMaxMs = 1000,
        Func<CancellationToken, ValueTask>? onRetry = null,
        int maxRetries = MaxRetries,
        Func<KafkaException, bool>? shouldRefreshMetadata = null,
        RetryDeadline? deadline = null)
    {
        if (deadline is { } retryDeadline)
        {
            await WithRetryUntilDeadlineAsync(
                static async state =>
                {
                    await state.Operation().ConfigureAwait(false);
                    return true;
                },
                static (failure, state, token) => state.RecoverAsync(failure, token),
                new MetadataRetryState<Func<ValueTask>>(operation, metadataManager, onRetry, shouldRefreshMetadata),
                retryBackoffMs,
                retryBackoffMaxMs,
                maxRetries,
                retryDeadline,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await operation().ConfigureAwait(false);
                return;
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

    /// <summary>
    /// Executes an async operation with retry logic for retriable Kafka errors,
    /// returning a result on success. On retriable failure, refreshes metadata,
    /// invokes the optional <paramref name="onRetry"/> callback, and retries after KIP-580 backoff.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retries. Defaults to <see cref="MaxRetries"/> (3).</param>
    /// <param name="shouldRefreshMetadata">
    /// Optional predicate that suppresses metadata refresh for errors recovered by <paramref name="onRetry"/>.
    /// </param>
    /// <param name="deadline">
    /// Opt-in deadline mode (see <see cref="RetryDeadline"/>): transport-level failures are
    /// retried until the deadline, while <paramref name="maxRetries"/> still bounds retriable
    /// errors a broker answered with. Only for operations that are safe to repeat.
    /// </param>
    internal static async ValueTask<T> WithRetryAsync<T>(
        Func<ValueTask<T>> operation,
        MetadataManager metadataManager,
        CancellationToken cancellationToken,
        int retryBackoffMs = 100,
        int retryBackoffMaxMs = 1000,
        Func<CancellationToken, ValueTask>? onRetry = null,
        int maxRetries = MaxRetries,
        Func<KafkaException, bool>? shouldRefreshMetadata = null,
        RetryDeadline? deadline = null)
    {
        if (deadline is { } retryDeadline)
        {
            return await WithRetryUntilDeadlineAsync(
                static state => state.Operation(),
                static (failure, state, token) => state.RecoverAsync(failure, token),
                new MetadataRetryState<Func<ValueTask<T>>>(operation, metadataManager, onRetry, shouldRefreshMetadata),
                retryBackoffMs,
                retryBackoffMaxMs,
                maxRetries,
                retryDeadline,
                cancellationToken).ConfigureAwait(false);
        }

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

    /// <summary>
    /// Deadline mode. A broker that was killed stays in cluster metadata until its session
    /// expires, so a count-bounded retry burns every attempt inside that window and hands the
    /// caller a raw socket failure with its API timeout unused. Here a transport-level failure,
    /// including one from the recovery step, is retried until the budget or the token ends. A
    /// retriable error a live broker answered with keeps the <paramref name="maxRetries"/> bound:
    /// it says nothing about an outage, and its typed failure should reach the caller promptly.
    /// The final error is always typed: the last Kafka failure, or a
    /// <see cref="KafkaTimeoutException"/> whose inner exception is the last transport failure.
    /// When the token ends the wait, the <see cref="OperationCanceledException"/> carries that
    /// failure as its inner exception so the owner of the token can report the cause.
    /// <para>
    /// <paramref name="operation"/> and <paramref name="recover"/> receive their inputs through
    /// <paramref name="state"/> so callers can pass static lambdas: a successful call allocates
    /// nothing here (consumer commits and offset fetches take this path on every call).
    /// <paramref name="recover"/> runs after each retriable failure, before the backoff; callers
    /// whose routing is not held by a <see cref="MetadataManager"/> (an admin client bootstrapped
    /// from controllers) supply their own.
    /// </para>
    /// </summary>
    internal static async ValueTask<T> WithRetryUntilDeadlineAsync<T, TState>(
        Func<TState, ValueTask<T>> operation,
        Func<Exception, TState, CancellationToken, ValueTask> recover,
        TState state,
        int retryBackoffMs,
        int retryBackoffMaxMs,
        int maxRetries,
        RetryDeadline deadline,
        CancellationToken cancellationToken)
    {
        // A budget-less loop (the caller's token is the deadline) never reads the clock.
        var startedAt = deadline.Budget == Timeout.InfiniteTimeSpan ? 0 : Stopwatch.GetTimestamp();
        Exception? lastFailure = null;
        var recoveryOwed = false;
        var brokerAnsweredRetries = 0;

        try
        {
            for (var attempt = 0; ; attempt++)
            {
                var recovering = false;
                try
                {
                    if (recoveryOwed)
                    {
                        // The recovery before the last backoff failed. The request is not
                        // repeated until a recovery succeeds: re-discovering a coordinator
                        // through stale metadata fails the same way the request did, and must
                        // consume the budget rather than escape it.
                        recovering = true;
                        await recover(lastFailure!, state, cancellationToken).ConfigureAwait(false);
                        recovering = false;
                        recoveryOwed = false;
                    }

                    return await operation(state).ConfigureAwait(false);
                }
                catch (Exception ex) when (IsRetriableUntilDeadline(ex, deadline))
                {
                    if (!HasTransportCause(ex) && ++brokerAnsweredRetries > maxRetries)
                        throw;

                    lastFailure = ex;

                    var delayMs = ExponentialRetryBackoff.CalculateDelayMilliseconds(
                        retryBackoffMs,
                        retryBackoffMaxMs,
                        attempt + 1);
                    ThrowIfBudgetSpent(ex, delayMs);

                    recoveryOwed = true;
                    if (!recovering)
                    {
                        // A failed request recovers before backing off, as count mode does: a
                        // recovery that blocks (a rejoin) must start while the budget still has
                        // room for it, not after a backoff that may spend the rest of it.
                        try
                        {
                            await recover(ex, state, cancellationToken).ConfigureAwait(false);
                            recoveryOwed = false;
                        }
                        catch (Exception recoveryFailure) when (IsRetriableUntilDeadline(recoveryFailure, deadline))
                        {
                            if (!HasTransportCause(recoveryFailure) && ++brokerAnsweredRetries > maxRetries)
                                throw;

                            lastFailure = recoveryFailure;
                        }

                        // The recovery spends the budget too. A caller with no token behind the
                        // budget (an offset lookup) would otherwise back off and repeat the
                        // request past its deadline, and could even succeed there.
                        ThrowIfBudgetSpent(lastFailure, delayMs);
                    }

                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException ex) when (
            lastFailure is not null
            && ex.InnerException is null
            && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                $"{deadline.Operation} was still failing when the wait was cancelled: {lastFailure.Message}",
                lastFailure,
                cancellationToken);
        }

        void ThrowIfBudgetSpent(Exception failure, int delayMs)
        {
            if (deadline.Budget == Timeout.InfiniteTimeSpan)
                return;

            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            if (elapsed + TimeSpan.FromMilliseconds(delayMs) < deadline.Budget)
                return;

            // A failure a broker answered with stays the final error. A transport failure, raw or
            // reported as a Kafka error (NETWORK_EXCEPTION, a lookup that exhausted its attempts
            // against unreachable brokers), becomes the API timeout it retried until.
            if (failure is KafkaException && !HasTransportCause(failure))
                ExceptionDispatchInfo.Capture(failure).Throw();

            throw new KafkaTimeoutException(
                TimeoutKind.Api,
                elapsed,
                deadline.Budget,
                $"{deadline.Operation} did not complete within {(int)deadline.Budget.TotalMilliseconds}ms: {failure.Message}",
                failure);
        }
    }

    // True for a transport-level failure and for a Kafka failure that wraps one (a coordinator
    // lookup that exhausted its own attempts against unreachable brokers). NETWORK_EXCEPTION is
    // the connection's own report that the peer went away before a response arrived, so no
    // broker answered it either.
    private static bool HasTransportCause(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is ObjectDisposedException
                or KafkaException { ErrorCode: Protocol.ErrorCode.NetworkException }
                || TransportFailureClassifier.IsSocketLevelFailure(current)
                || TransportFailureClassifier.IsClientRoutingFailure(current))
            {
                return true;
            }
        }

        return false;
    }

    // Inputs of a deadline-mode retry routed by a MetadataManager, carried to static callbacks.
    private readonly struct MetadataRetryState<TOperation>(
        TOperation operation,
        MetadataManager metadataManager,
        Func<CancellationToken, ValueTask>? onRetry,
        Func<KafkaException, bool>? shouldRefreshMetadata)
    {
        public TOperation Operation { get; } = operation;

        private MetadataManager MetadataManager { get; } = metadataManager;

        private Func<CancellationToken, ValueTask>? OnRetry { get; } = onRetry;

        private Func<KafkaException, bool>? ShouldRefreshMetadata { get; } = shouldRefreshMetadata;

        public ValueTask RecoverAsync(Exception failure, CancellationToken cancellationToken) =>
            RecoverAsync(failure, MetadataManager, OnRetry, ShouldRefreshMetadata, cancellationToken);

        private static async ValueTask RecoverAsync(
            Exception failure,
            MetadataManager metadataManager,
            Func<CancellationToken, ValueTask>? onRetry,
            Func<KafkaException, bool>? shouldRefreshMetadata,
            CancellationToken cancellationToken)
        {
            if (failure is not KafkaException kafkaException ||
                shouldRefreshMetadata?.Invoke(kafkaException) != false)
            {
                await RefreshMetadataForRetryAsync(metadataManager, cancellationToken).ConfigureAwait(false);
            }

            if (onRetry is not null)
                await onRetry(cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsRetriableUntilDeadline(Exception exception, RetryDeadline deadline) =>
        TransportFailureClassifier.IsRetriable(
            exception,
            TransportRetryPolicy.Request,
            ownerDisposed: deadline.IsOwnerDisposed?.Invoke() ?? false);

    /// <summary>
    /// Classifies a direct broker-operation failure for failover. Fatal exceptions and
    /// cancellation are not retried, even when they wrap a transient transport failure.
    /// Callers must separately check their cancellation token and remaining retry budget.
    /// </summary>
    internal static bool IsRetriableBrokerFailure(Exception exception) =>
        exception is KafkaTimeoutException
            or KafkaException { IsRetriable: true }
        || TransportFailureClassifier.IsSocketLevelFailure(exception);

    internal static bool IsRetriableRequestFailure(Exception exception)
    {
        // Request retries respect the outer Kafka error, including terminal operation
        // deadlines. Broker failover can retry a KafkaTimeoutException within its own budget.
        if (exception is KafkaException kafkaException)
            return kafkaException.IsRetriable;

        if (IsRetriableBrokerFailure(exception))
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

    /// <summary>
    /// Best-effort metadata refresh between retries of another operation. A refresh that fails
    /// for a transient reason is swallowed so the original operation's typed failure stays the
    /// final error; cancellation and non-transient failures propagate.
    /// </summary>
    internal static async ValueTask RefreshMetadataForRetryAsync(
        MetadataManager metadataManager,
        CancellationToken cancellationToken)
    {
        try
        {
            await metadataManager.RefreshMetadataAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (
            ex is not ObjectDisposedException)
        {
            // Convert only the metadata-refresh race. An unrelated operation invariant
            // must still propagate even if its caller cancels concurrently.
            cancellationToken.ThrowIfCancellationRequested();
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
