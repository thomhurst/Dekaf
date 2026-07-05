#if NETSTANDARD2_0
namespace System.Threading;

internal static class CancellationCompatibilityExtensions
{
    public static bool TryReset(this CancellationTokenSource source)
        => !source.IsCancellationRequested;

    public static CancellationTokenRegistration UnsafeRegister(
        this CancellationToken cancellationToken,
        Action<object?> callback,
        object? state)
        => cancellationToken.Register(callback, state);

    public static CancellationTokenRegistration UnsafeRegister(
        this CancellationToken cancellationToken,
        Action<object?, CancellationToken> callback,
        object? state)
        => cancellationToken.Register(s => callback(s, cancellationToken), state);
}
#endif
