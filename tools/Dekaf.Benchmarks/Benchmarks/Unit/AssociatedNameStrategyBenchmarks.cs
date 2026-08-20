using System.Buffers;
using System.Net;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser(displayGenColumns: false)]
public class AssociatedNameStrategyBenchmarks
{
    private string _topic = null!;
    private string _recordType = null!;
    private SchemaRegistryClient _client = null!;
    private AssociatedNameStrategy _strategy = null!;
    private SchemaRegistrySerializer<int> _serializer = null!;
    private JsonSchemaRegistrySerializer<int> _jsonSerializer = null!;
    private DeserializerSubjectNameCache _deserializerSubjects = null!;
    private ArrayBufferWriter<byte> _jsonDestination = null!;
    private SerializationContext _serializationContext;

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
            _strategy,
            static () => new Schema { SchemaType = SchemaType.Json, SchemaString = "{\"type\":\"integer\"}" });
        _jsonSerializer = new JsonSchemaRegistrySerializer<int>(
            _client,
            _strategy,
            "{\"type\":\"integer\"}");
        _jsonDestination = new ArrayBufferWriter<byte>(16);
        _serializationContext = new SerializationContext
        {
            Topic = _topic,
            Component = SerializationComponent.Value
        };
        _ = _strategy.GetSubjectNameAsync(_topic, _recordType, isKey: false)
            .GetAwaiter()
            .GetResult();
        _ = _serializer.PrepareAsync(_topic, 42).GetAwaiter().GetResult();
        _ = _jsonSerializer.PrepareAsync(_topic, 42).GetAwaiter().GetResult();
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
        await _jsonSerializer.DisposeAsync().ConfigureAwait(false);
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
    public void JsonSerializerAssociatedNameCached()
    {
        _jsonDestination.Clear();
        _jsonSerializer.Serialize(42, ref _jsonDestination, _serializationContext);
    }

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
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var path = request.RequestUri!.AbsolutePath;
            var content = path switch
            {
                var value when value.Contains("/subjects/", StringComparison.Ordinal) =>
                    """{"subject":"benchmark-associated-value","version":1,"id":1,"schemaType":"JSON","schema":"{\"type\":\"integer\"}"}""",
                var value when value.Contains("/schemas/ids/", StringComparison.Ordinal) =>
                    """{"schemaType":"JSON","schema":"{\"type\":\"integer\"}"}""",
                "/associations/resources/-/benchmark-orders" => """
                    [{"subject":"benchmark-associated-value","guid":"benchmark-guid","resourceName":"benchmark-orders","resourceNamespace":"-","resourceId":"benchmark-orders","resourceType":"topic","associationType":"value","lifecycle":"WEAK","frozen":false}]
                    """,
                _ => throw new HttpRequestException($"Unexpected benchmark request path '{path}'.")
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            };
        }
    }
}
