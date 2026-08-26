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
    private IAsyncSerializerPreparationAdmission<int> _jsonAdmissionSerializer = null!;
    private JsonSchemaRegistryDeserializer<int> _jsonDeserializer = null!;
    private IAsyncDeserializerPreparer<int> _jsonDeserializerPreparer = null!;
    private SerializerPreparationAdmission _jsonAdmission;
    private DeserializerSubjectNameCache _deserializerSubjects = null!;
    private ArrayBufferWriter<byte> _jsonDestination = null!;
    private SerializationContext _serializationContext;
    private SerializationContext _jsonDeserializationContext;
    private ReadOnlyMemory<byte> _jsonPayload;

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
        _jsonAdmissionSerializer = _jsonSerializer;
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
        _jsonAdmission = _jsonAdmissionSerializer.PrepareForSerializationAsync(42, _serializationContext)
            .GetAwaiter()
            .GetResult();
        _jsonDeserializer = new JsonSchemaRegistryDeserializer<int>(
            _client,
            jsonOptions: null,
            new SchemaRegistryDeserializerConfig
            {
                SchemaIdStrategy = SchemaIdDeserializerStrategy.Header,
                AsyncSubjectNameStrategy = _strategy
            });
        _jsonDeserializerPreparer = _jsonDeserializer;
        _jsonPayload = "42"u8.ToArray();
        var jsonHeaders = new Headers(33).Add(
            SchemaIdentityHeaderNames.Value,
            SchemaIdentityFraming.CreateSchemaGuidFrame(AssociationHandler.SchemaGuid));
        for (var index = 0; index < 32; index++)
            jsonHeaders.Add(new Header($"noise-{index}", ReadOnlyMemory<byte>.Empty));
        _jsonDeserializationContext = new SerializationContext
        {
            Topic = _topic,
            Component = SerializationComponent.Value,
            Headers = jsonHeaders
        };
        _jsonDeserializerPreparer.PrepareAsync(_jsonPayload, _jsonDeserializationContext)
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
    public async Task Cleanup()
    {
        await _serializer.DisposeAsync().ConfigureAwait(false);
        await _jsonSerializer.DisposeAsync().ConfigureAwait(false);
        await _jsonDeserializer.DisposeAsync().ConfigureAwait(false);
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
    public void JsonSerializerAdmittedCached()
    {
        _jsonDestination.Clear();
        _jsonAdmissionSerializer.SerializePrepared(
            42,
            ref _jsonDestination,
            _serializationContext,
            in _jsonAdmission);
    }

    [Benchmark]
    public string DeserializerAssociatedNameCached() =>
        _deserializerSubjects.GetSubjectName(
            schemaId: 1,
            schema: null,
            _topic,
            isKey: false,
            _recordType);

    [Benchmark]
    public bool JsonGuidDeserializerPrepared() =>
        _jsonDeserializerPreparer.TryDeserialize(_jsonPayload, _jsonDeserializationContext, out _);

    private sealed class AssociationHandler : HttpMessageHandler
    {
        internal static readonly Guid SchemaGuid = new("11111111-2222-3333-4444-555555555555");

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var path = request.RequestUri!.AbsolutePath;
            var content = path switch
            {
                var value when value.Contains("/schemas/guids/", StringComparison.Ordinal) =>
                    """{"schemaType":"JSON","schema":"{\"type\":\"integer\"}"}""",
                var value when value.Contains("/subjects/", StringComparison.Ordinal) =>
                    $$"""{"subject":"benchmark-associated-value","version":1,"id":1,"guid":"{{SchemaGuid:D}}","schemaType":"JSON","schema":"{\"type\":\"integer\"}"}""",
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
