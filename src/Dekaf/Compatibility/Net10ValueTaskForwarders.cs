#if !NETSTANDARD2_0
namespace System.Threading.Tasks;

internal static class ValueTaskCompatibility
{
    public static ValueTask CompletedTask => ValueTask.CompletedTask;

    public static ValueTask<T> FromException<T>(Exception exception)
        => ValueTask.FromException<T>(exception);
}
#endif
