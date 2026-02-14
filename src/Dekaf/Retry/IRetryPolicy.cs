namespace Dekaf.Retry;

/// <summary>
/// Defines a policy for retrying failed operations.
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Returns the delay before the next retry, or <c>null</c> to stop retrying.
    /// </summary>
    /// <param name="attemptNumber">The 1-based attempt number (1 = first failure, 2 = second failure, etc.).</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>The delay before retrying, or <c>null</c> to stop retrying.</returns>
    TimeSpan? GetNextDelay(int attemptNumber, Exception exception);
}
