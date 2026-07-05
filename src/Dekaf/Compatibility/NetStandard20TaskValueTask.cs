#if NETSTANDARD2_0
namespace System.Threading.Tasks;

internal static class ValueTaskCompatibility
{
    public static ValueTask CompletedTask => default;

    public static ValueTask<T> FromException<T>(Exception exception)
    {
        var source = new TaskCompletionSource<T>();
        source.SetException(exception);
        return new ValueTask<T>(source.Task);
    }
}
#endif
