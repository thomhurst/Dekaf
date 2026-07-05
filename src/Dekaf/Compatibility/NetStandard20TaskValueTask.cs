#if NETSTANDARD2_0
namespace System.Threading.Tasks;

internal static class ValueTaskCompatibility
{
    public static ValueTask CompletedTask => default;

    public static ValueTask FromCanceled(System.Threading.CancellationToken cancellationToken)
    {
        var source = new TaskCompletionSource<bool>();
        source.TrySetCanceled(cancellationToken);
        return new ValueTask(source.Task);
    }

    public static ValueTask FromException(Exception exception)
    {
        var source = new TaskCompletionSource<bool>();
        source.SetException(exception);
        return new ValueTask(source.Task);
    }

    public static ValueTask<T> FromException<T>(Exception exception)
    {
        var source = new TaskCompletionSource<T>();
        source.SetException(exception);
        return new ValueTask<T>(source.Task);
    }
}
#endif
