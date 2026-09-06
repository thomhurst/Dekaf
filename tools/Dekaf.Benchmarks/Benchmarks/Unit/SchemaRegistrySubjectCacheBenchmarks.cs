using System.Net;
using System.Text;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class SchemaRegistrySubjectCacheBenchmarks
{
    private readonly Schema _schema = new() { SchemaString = "{}", SchemaType = SchemaType.Json };
    private SchemaRegistryClient _client = null!;
    private StaticSchemaHandler _handler = null!;

    [GlobalSetup]
    public void Setup()
    {
        _handler = new StaticSchemaHandler();
        _client = new SchemaRegistryClient(new SchemaRegistryConfig { Url = "https://registry.test" }, _handler);
        _client.CacheSchema(42, "events", _schema);
        _client.GetSchemaAsync(42, "events").GetAwaiter().GetResult();
    }

    [Benchmark]
    public Task<int> CachedRegistration() => _client.RegisterSchemaAsync("events", _schema);

    [Benchmark]
    public Task<int> CachedGetOrRegister() => _client.GetOrRegisterSchemaAsync("events", _schema);

    [Benchmark]
    public Schema CachedSubjectIdentity()
    {
        _client.TryGetCachedSchema(42, "events", out var schema);
        return schema;
    }

    [Benchmark]
    public Schema CachedGlobalIdentity()
    {
        _client.TryGetCachedSchema(42, out var schema);
        return schema;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _handler.Dispose();
    }

    private sealed class StaticSchemaHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"schema":"{}","schemaType":"JSON"}""", Encoding.UTF8, "application/json")
            });
    }
}
