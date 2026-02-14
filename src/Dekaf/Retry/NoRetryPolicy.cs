namespace Dekaf.Retry;

/// <summary>
/// Retry policy that never retries. Useful for explicit opt-out.
/// </summary>
public sealed class NoRetryPolicy : IRetryPolicy
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static NoRetryPolicy Instance { get; } = new();

    private NoRetryPolicy() { }

    /// <inheritdoc />
    public TimeSpan? GetNextDelay(int attemptNumber, Exception exception) => null;
}
