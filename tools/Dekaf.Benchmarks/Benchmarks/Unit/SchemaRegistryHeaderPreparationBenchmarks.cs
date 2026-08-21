using Avro.Generic;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Google.Protobuf.WellKnownTypes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Protects cached routed-header preparation from steady-state allocations.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class SchemaRegistryHeaderPreparationBenchmarks
{
    private static readonly Guid SchemaGuid = new("11111111-2222-3333-4444-555555555555");
    private static readonly byte[] AvroPayload = [84];
    private static readonly byte[] ProtobufPayload = [0x0A, 0x01, (byte)'x'];

    private IRecordHeaderAsyncDeserializerPreparer<GenericRecord> _avroHeaderPreparer = null!;
    private IRecordHeaderAsyncDeserializerPreparer<StringValue> _protobufHeaderPreparer = null!;
    private SerializationContext _avroContext;
    private SerializationContext _protobufContext;
    private RecordHeaderRoutingLookup _avroHeaders;
    private RecordHeaderRoutingLookup _protobufHeaders;
    private AvroSchemaRegistryDeserializer<GenericRecord> _avro = null!;
    private ProtobufSchemaRegistryDeserializer<StringValue> _protobuf = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        const string subject = "benchmark-orders-value";
        var strategy = new FixedSubjectNameStrategy(subject);
        _avro = new AvroSchemaRegistryDeserializer<GenericRecord>(
            new BenchmarkSchemaRegistryClient(new Schema
            {
                SchemaType = SchemaType.Avro,
                SchemaString =
                    "{\"type\":\"record\",\"name\":\"Order\",\"fields\":[{\"name\":\"id\",\"type\":\"int\"}]}"
            }),
            new AvroDeserializerConfig
            {
                SchemaIdStrategy = SchemaIdDeserializerStrategy.Header,
                AsyncSubjectNameStrategy = strategy,
                RuleExecutor = PassThroughRuleExecutor.Instance
            });
        _protobuf = new ProtobufSchemaRegistryDeserializer<StringValue>(
            new BenchmarkSchemaRegistryClient(new Schema
            {
                SchemaType = SchemaType.Protobuf,
                SchemaString = string.Empty
            }),
            new ProtobufDeserializerConfig
            {
                SchemaIdStrategy = SchemaIdDeserializerStrategy.Header,
                AsyncSubjectNameStrategy = strategy,
                RuleExecutor = PassThroughRuleExecutor.Instance
            });
        _avroHeaderPreparer = _avro;
        _protobufHeaderPreparer = _protobuf;

        var avroHeader = new Header(
            SchemaIdentityHeaderNames.Value,
            SchemaIdentityFraming.CreateSchemaGuidFrame(SchemaGuid));
        var protobufIdentity = SchemaIdentityFraming.CreateSchemaGuidFrame(SchemaGuid);
        var protobufHeaderValue = new byte[protobufIdentity.Length + 1];
        protobufIdentity.CopyTo(protobufHeaderValue, 0);
        var protobufHeader = new Header(SchemaIdentityHeaderNames.Value, protobufHeaderValue);
        _avroContext = CreateContext(avroHeader);
        _protobufContext = CreateContext(protobufHeader);
        _avroHeaders = CreateLookup(_avro, avroHeader);
        _protobufHeaders = CreateLookup(_protobuf, protobufHeader);

        await PrepareAvroGuidHeaderCached().ConfigureAwait(false);
        await PrepareProtobufGuidHeaderCached().ConfigureAwait(false);
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await _avro.DisposeAsync().ConfigureAwait(false);
        await _protobuf.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(Baseline = true)]
    public ValueTask PrepareAvroGuidHeaderCached() =>
        _avroHeaderPreparer.PrepareAsync(
            AvroPayload,
            _avroContext,
            _avroHeaders,
            CancellationToken.None);

    [Benchmark]
    public ValueTask PrepareProtobufGuidHeaderCached() =>
        _protobufHeaderPreparer.PrepareAsync(
            ProtobufPayload,
            _protobufContext,
            _protobufHeaders,
            CancellationToken.None);

    private static SerializationContext CreateContext(Header identityHeader) => new()
    {
        Topic = "benchmark-orders",
        Component = SerializationComponent.Value,
        Headers = new Headers(1).Add(identityHeader)
    };

    private static RecordHeaderRoutingLookup CreateLookup<T>(IDeserializer<T> deserializer, Header identityHeader)
    {
        var headers = new[] { identityHeader };
        var plan = RecordHeaderRoutingPlan.Create<string, T>(null, deserializer)!;
        return new RecordHeaderRoutingLookup(
            plan,
            headers,
            headers.Length,
            firstIndex: 0,
            secondIndex: 1,
            routedHeaderTailOffset: RecordHeaderRoutingPlan.FullyIndexedWithoutTail);
    }

    private sealed class FixedSubjectNameStrategy(string subject) : IAsyncSubjectNameStrategy
    {
        public ValueTask<string> GetSubjectNameAsync(
            string topic,
            string? recordType,
            bool isKey,
            CancellationToken cancellationToken = default) => new(subject);
    }

    private sealed class PassThroughRuleExecutor : ISchemaRegistryRuleExecutor
    {
        internal static PassThroughRuleExecutor Instance { get; } = new();

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;
    }

    private sealed class BenchmarkSchemaRegistryClient(Schema schema) : ISchemaRegistryClient
    {
        public Task<Schema> GetSchemaByGuidAsync(
            string guid,
            string? format = null,
            CancellationToken cancellationToken = default) => Task.FromResult(schema);

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(schema);

        public Task<Schema> GetSchemaAsync(
            int id,
            string subject,
            CancellationToken cancellationToken = default) => Task.FromResult(schema);

        public Task<RegisteredSchema> LookupSchemaAsync(
            string subject,
            Schema candidate,
            bool ignoreDeletedSchemas = true,
            bool normalize = false,
            CancellationToken cancellationToken = default) => Task.FromResult(new RegisteredSchema
        {
            Id = 1,
            Guid = SchemaGuid.ToString("D"),
            Subject = subject,
            Version = 1,
            Schema = schema
        });

        public Task<int> RegisterSchemaAsync(
            string subject,
            Schema candidate,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            Schema candidate,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<int>> GetVersionsAsync(
            string subject,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> IsCompatibleAsync(
            string subject,
            Schema candidate,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(
            string subject,
            bool permanent = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
