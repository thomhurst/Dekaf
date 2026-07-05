#if NETSTANDARD2_0
namespace System.Net.Http;

internal static class HttpContentCompatibilityExtensions
{
    public static Task<string> ReadAsStringAsync(this HttpContent content, CancellationToken cancellationToken)
        => content.ReadAsStringAsync().WaitAsync(cancellationToken);
}
#endif
