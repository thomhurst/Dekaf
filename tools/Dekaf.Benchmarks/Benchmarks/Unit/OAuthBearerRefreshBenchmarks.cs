using BenchmarkDotNet.Attributes;
using Dekaf.Security.Sasl;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Token-cache reads and synchronous refresh coordination, without identity-service I/O.</summary>
[MemoryDiagnoser]
public class OAuthBearerRefreshBenchmarks
{
    private static readonly OAuthBearerToken ValidToken = new()
    {
        TokenValue = "cached-token",
        PrincipalName = "benchmark",
        Expiration = DateTimeOffset.MaxValue
    };

    private static readonly OAuthBearerToken RefreshRequiredToken = new()
    {
        TokenValue = "refresh-token",
        PrincipalName = "benchmark",
        Expiration = DateTimeOffset.MinValue
    };

    private OAuthBearerAuthenticator _cached = null!;
    private OAuthBearerAuthenticator _refresh = null!;

    [GlobalSetup]
    public void Setup()
    {
        _cached = new OAuthBearerAuthenticator(ValidToken);
        _refresh = new OAuthBearerAuthenticator(static _ => ValueTask.FromResult(RefreshRequiredToken));
        _refresh.GetTokenAsync().GetAwaiter().GetResult();
    }

    [Benchmark]
    public ValueTask<OAuthBearerToken> CachedToken() => _cached.GetTokenAsync();

    [Benchmark]
    public ValueTask<OAuthBearerToken> SynchronousRefresh() => _refresh.GetTokenAsync();
}
