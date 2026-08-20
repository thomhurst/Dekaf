using System.Net;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser(displayGenColumns: false)]
public class AssociatedNameStrategyBenchmarks
{
    private string _topic = null!;
    private string _recordType = null!;
    private SchemaRegistryClient _client = null!;
    private AssociatedNameStrategy _strategy = null!;
    private SchemaRegistrySerializer<int> _serializer = null!;
    private DeserializerSubjectNameCache _deserializerSubjects = null!;

    [GlobalSetup]
    public void Setup()
    {
        _topic = "benchmark-orders";
        _recordType = "Benchmark.Order";
        _client = new SchemaRegistryClient(
            new SchemaRegistryConfig { Url = "http://association-benchmark.invalid" },
            new AssociationHandler());
        _strategy = new AssociatedNameStrategy(_client);
        _serializer = new SchemaRegistrySerializer<int>(
            _client,
            static (_, _) => { },
            static () => new Schema { SchemaType = SchemaType.Json, SchemaString = "{\"type\":\"integer\"}" },
            _strategy);
        _ = _strategy.GetSubjectNameAsync(_topic, _recordType, isKey: false)
            .GetAwaiter()
            .GetResult();
        _ = _serializer.PrepareAsync(_topic, 42).GetAwaiter().GetResult();
        _deserializerSubjects = DeserializerSubjectNameCache.Create(
            _client,
            new SchemaRegistryDeserializerConfig { AsyncSubjectNameStrategy = _strategy })!;
        _deserializerSubjects.PrepareAsync(
                _client,
                schemaId: 1,
                _topic,
                isKey: false,
                _recordType,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _serializer.DisposeAsync().ConfigureAwait(false);
        _client.Dispose();
    }

    [Benchmark(Baseline = true)]
    public string TopicName() => SubjectNameResolver.GetTopicSubjectName(_topic, isKey: false);

    [Benchmark]
    public ValueTask<string> AssociatedNameCached() =>
        _strategy.GetSubjectNameAsync(_topic, _recordType, isKey: false);

    [Benchmark]
    public ValueTask<ResolvedSchemaContext> SerializerAssociatedNameCached() =>
        _serializer.PrepareAsync(_topic, 42);

    [Benchmark]
    public string DeserializerAssociatedNameCached() =>
        _deserializerSubjects.GetSubjectName(
            schemaId: 1,
            schema: null,
            _topic,
            isKey: false,
            _recordType);

    private sealed class AssociationHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var content = path.Contains("/subjects/", StringComparison.Ordinal)
                ? """{"subject":"benchmark-associated-value","version":1,"id":1,"schemaType":"JSON","schema":"{\"type\":\"integer\"}"}"""
                : path.Contains("/schemas/ids/", StringComparison.Ordinal)
                    ? """{"schemaType":"JSON","schema":"{\"type\":\"integer\"}"}"""
                    : """
                  [{"subject":"benchmark-associated-value","guid":"benchmark-guid","resourceName":"benchmark-orders","resourceNamespace":"-","resourceId":"benchmark-orders","resourceType":"topic","associationType":"value","lifecycle":"WEAK","frozen":false}]
                  """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
        }
    }
}
