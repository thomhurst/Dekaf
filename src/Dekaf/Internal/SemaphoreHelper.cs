namespace Dekaf.Internal;

/// <summary>
/// Shared helper methods for semaphore acquisition and release that convert
/// <see cref="ObjectDisposedException"/> from the semaphore into a more descriptive
/// exception naming the owning object, and safely suppress disposal races on release.
/// </summary>
internal static class SemaphoreHelper
{
    /// <summary>
    /// Acquires a semaphore asynchronously, converting <see cref="ObjectDisposedException"/>
    /// into one that names <paramref name="objectName"/> for clearer diagnostics.
    /// </summary>
    internal static async ValueTask AcquireOrThrowDisposedAsync(
        SemaphoreSlim semaphore, string objectName, CancellationToken cancellationToken)
    {
        try
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            throw new ObjectDisposedException(objectName);
        }
    }

    /// <summary>
    /// Acquires a semaphore synchronously, converting <see cref="ObjectDisposedException"/>
    /// into one that names <paramref name="objectName"/> for clearer diagnostics.
    /// </summary>
    internal static void AcquireOrThrowDisposed(SemaphoreSlim semaphore, string objectName)
    {
        try
        {
            semaphore.Wait();
        }
        catch (ObjectDisposedException)
        {
            throw new ObjectDisposedException(objectName);
        }
    }

    /// <summary>
    /// Releases a semaphore safely, suppressing <see cref="ObjectDisposedException"/> that can occur
    /// when DisposeAsync races with a finally block after a successful WaitAsync.
    /// </summary>
    internal static void ReleaseSafely(SemaphoreSlim semaphore)
    {
        try { semaphore.Release(); }
        catch (ObjectDisposedException) { }
    }
}
