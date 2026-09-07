using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures the completed-cache work used when a finite migration-plan TTL expires.
/// Expiration is represented by explicit invalidation; registry I/O, time checks, and
/// cache construction are excluded. Cached lookups provide unchanged-path controls.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
[GenericTypeArguments(typeof(int))]
[GenericTypeArguments(typeof(object))]
public class SchemaResolutionFiniteTtlBenchmarks<TValue>
{
    private readonly SchemaResolutionCache<TValue> _cache = new(16);
    private readonly Schema _schema = new() { SchemaType = SchemaType.Json, SchemaString = "{}" };
    private readonly string _subject = "finite-ttl-value";
    private Task<TValue> _resolved = null!;
    private TValue _value = default!;

    [GlobalSetup]
    public async Task Setup()
    {
        _value = typeof(TValue) == typeof(int) ? (TValue)(object)42 : (TValue)new object();
        _resolved = Task.FromResult(_value);
        await Resolve();

        // Warm the reusable eviction slot and verify each invocation really invalidates
        // and publishes a completed value. Setup is outside the measured operation.
        for (var i = 0; i < 32; i++)
        {
            if (!_cache.TryRemove(_subject, _schema, _value) || _cache.CachedEntryCount != 0)
                throw new InvalidOperationException("The completed cache entry was not invalidated.");
            var refreshed = Resolve();
            if (!refreshed.IsCompletedSuccessfully)
                throw new InvalidOperationException("The fixture must resolve synchronously.");
            if (!EqualityComparer<TValue>.Default.Equals(await refreshed, _value) ||
                _cache.CachedEntryCount != 1)
                throw new InvalidOperationException("The refreshed cache entry was not published.");
        }
    }

    [Benchmark]
    public ValueTask<TValue> Refresh()
    {
        _cache.TryRemove(_subject, _schema, _value);
        return Resolve();
    }

    [Benchmark]
    public ValueTask<TValue> ResolveHit() => Resolve();

    [Benchmark]
    public TValue LookupHit()
    {
        _cache.TryGet(_subject, _schema, out var value);
        return value;
    }

    private ValueTask<TValue> Resolve() => _cache.ResolveAsync(
        _subject, _schema, _resolved, static (resolved, _, _) => resolved, CancellationToken.None);
}
