#if !NET10_0_OR_GREATER
namespace Dekaf.Tests.Unit.Testing;

internal static class AsyncEnumerableTestExtensions
{
    internal static async ValueTask<T> FirstAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            return item;

        throw new InvalidOperationException("Sequence contains no elements.");
    }
}
#endif
