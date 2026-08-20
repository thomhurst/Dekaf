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
        _ = _strategy.GetSubjectNameAsync(_topic, _recordType, isKey: false)
            .GetAwaiter()
            .GetResult();
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
    public void Cleanup() => _client.Dispose();

    [Benchmark(Baseline = true)]
    public string TopicName() => SubjectNameResolver.GetTopicSubjectName(_topic, isKey: false);

    [Benchmark]
    public ValueTask<string> AssociatedNameCached() =>
        _strategy.GetSubjectNameAsync(_topic, _recordType, isKey: false);

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
            var content = request.RequestUri!.AbsolutePath.Contains("/schemas/ids/", StringComparison.Ordinal)
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
