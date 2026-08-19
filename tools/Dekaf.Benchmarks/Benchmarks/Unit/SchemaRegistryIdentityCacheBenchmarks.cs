using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Guards allocation-free schema identity cache hits used by deserializers.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class SchemaRegistryIdentityCacheBenchmarks
{
    private readonly Guid _guid = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
    private readonly string _format = "serialized";
    private readonly int _id = 42;
    private SchemaRegistryClient _client = null!;

    [GlobalSetup]
    public void Setup()
    {
        _client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "https://schema-registry.example.test"
        });
        var schema = new Schema { SchemaString = "{}", SchemaType = SchemaType.Json };
        _client.CacheSchema(_id, subject: null, schema);
        _client.CacheGuidSchema(_guid, _format, schema);
    }

    [GlobalCleanup]
    public void Cleanup() => _client.Dispose();

    [Benchmark(Baseline = true)]
    public Schema LookupByIntegerId()
    {
        _client.TryGetCachedSchema(_id, out var schema);
        return schema;
    }

    [Benchmark]
    public Schema LookupByGuid()
    {
        _client.TryGetCachedSchema(_guid, _format, out var schema);
        return schema;
    }
}
