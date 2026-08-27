using Avro.Generic;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Avro.Poco;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Google.Protobuf.WellKnownTypes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Protects cached routed-header preparation from steady-state allocations.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class SchemaRegistryHeaderPreparationBenchmarks
{
    private const string AvroValidationSchema =
        "{\"type\":\"record\",\"name\":\"ValidatedOrder\",\"fields\":[{\"name\":\"id\",\"type\":\"int\",\"confluent:rules\":[{\"name\":\"nonnegative\",\"expr\":\"this >= 0\"}]}]}";
    private const string AvroPocoValidationSchema =
        "{\"type\":\"record\",\"name\":\"PocoWriterUnionBenchmarkRecord\",\"namespace\":\"Dekaf.Benchmarks.Benchmarks.Unit\",\"fields\":[{\"name\":\"value\",\"type\":[\"int\",\"long\"]}]}";
    private static readonly Guid SchemaGuid = new("11111111-2222-3333-4444-555555555555");
    private static readonly byte[] AvroPayload = [84];
    private static readonly byte[] AvroPocoPayload = [0, 84];
    private static readonly byte[] AvroPrefixPayload = [0, 0, 0, 0, 1, 84];
    private static readonly byte[] ProtobufPayload = [0x0A, 0x01, (byte)'x'];
    private static readonly byte[] ProtobufPrefixPayload = [0, 0, 0, 0, 1, 0, 0x0A, 0x01, (byte)'x'];

    private IRecordHeaderAsyncDeserializerPreparer<GenericRecord> _avroHeaderPreparer = null!;
    private IRecordHeaderAsyncDeserializerPreparer<StringValue> _protobufHeaderPreparer = null!;
    private IRecordHeaderAsyncDeserializerPreparer<GenericRecord> _avroDualPreparer = null!;
    private IRecordHeaderAsyncDeserializerPreparer<StringValue> _protobufDualPreparer = null!;
    private IRecordHeaderAsyncDeserializerPreparer<GenericRecord> _avroDoubleReadPreparer = null!;
    private IRecordHeaderAsyncDeserializerPreparer<GenericRecord> _avroValidationPreparer = null!;
    private IRecordHeaderAsyncDeserializerPreparer<PocoWriterUnionBenchmarkRecord>
        _avroPocoValidationPreparer = null!;
    private IRecordHeaderAsyncDeserializerPreparer<StringValue> _protobufDoubleReadPreparer = null!;
    private SerializationContext _avroContext;
    private SerializationContext _protobufContext;
    private SerializationContext _avroPrefixContext;
    private SerializationContext _protobufPrefixContext;
    private RecordHeaderRoutingLookup _avroHeaders;
    private RecordHeaderRoutingLookup _protobufHeaders;
    private RecordHeaderRoutingLookup _avroPrefixHeaders;
    private RecordHeaderRoutingLookup _protobufPrefixHeaders;
    private RecordHeaderRoutingLookup _avroValidationHeaders;
    private RecordHeaderRoutingLookup _avroPocoValidationHeaders;
    private AvroSchemaRegistryDeserializer<GenericRecord> _avro = null!;
    private ProtobufSchemaRegistryDeserializer<StringValue> _protobuf = null!;
    private AvroSchemaRegistryDeserializer<GenericRecord> _avroDual = null!;
    private AvroSchemaRegistryDeserializer<GenericRecord> _avroValidation = null!;
    private AvroPocoSchemaRegistryDeserializer<
        PocoWriterUnionBenchmarkRecord,
        PocoWriterUnionBenchmarkRecord.AvroCodec> _avroPocoValidation = null!;
    private Avro.Schema _avroValidationSchema = null!;
    private ProtobufSchemaRegistryDeserializer<StringValue> _protobufDual = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        const string subject = "benchmark-orders-value";
        var strategy = new FixedSubjectNameStrategy(subject);
        var avroRegistry = new BenchmarkSchemaRegistryClient(new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString =
                "{\"type\":\"record\",\"name\":\"Order\",\"fields\":[{\"name\":\"id\",\"type\":\"int\"}]}"
        });
        var protobufRegistry = new BenchmarkSchemaRegistryClient(new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = string.Empty
        });
        _avro = new AvroSchemaRegistryDeserializer<GenericRecord>(
            avroRegistry,
            new AvroDeserializerConfig
            {
                SchemaIdStrategy = SchemaIdDeserializerStrategy.Header,
                AsyncSubjectNameStrategy = strategy,
                RuleExecutor = PassThroughRuleExecutor.Instance
            });
        _protobuf = new ProtobufSchemaRegistryDeserializer<StringValue>(
            protobufRegistry,
            new ProtobufDeserializerConfig
            {
                SchemaIdStrategy = SchemaIdDeserializerStrategy.Header,
                AsyncSubjectNameStrategy = strategy,
                RuleExecutor = PassThroughRuleExecutor.Instance
            });
        _avroHeaderPreparer = _avro;
        _protobufHeaderPreparer = _protobuf;
        _avroDual = new AvroSchemaRegistryDeserializer<GenericRecord>(
            avroRegistry,
            new AvroDeserializerConfig { SchemaIdStrategy = SchemaIdDeserializerStrategy.Dual });
        _protobufDual = new ProtobufSchemaRegistryDeserializer<StringValue>(
            protobufRegistry,
            new ProtobufDeserializerConfig
            {
                SchemaIdStrategy = SchemaIdDeserializerStrategy.Dual,
                SkipSchemaValidation = true
            });
        _avroDualPreparer = _avroDual;
        _protobufDualPreparer = _protobufDual;
        _avroDoubleReadPreparer = new DoubleReadPrefixPreparer<GenericRecord>(_avroDual);
        _protobufDoubleReadPreparer = new DoubleReadPrefixPreparer<StringValue>(_protobufDual);
        _avroValidation = new AvroSchemaRegistryDeserializer<GenericRecord>(
            new BenchmarkSchemaRegistryClient(new Schema
            {
                SchemaType = SchemaType.Avro,
                SchemaString = AvroValidationSchema
            }),
            new AvroDeserializerConfig
            {
                SchemaIdStrategy = SchemaIdDeserializerStrategy.Header,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            });
        _avroValidationPreparer = _avroValidation;
        _avroValidationSchema = Avro.Schema.Parse(AvroValidationSchema);
        _avroPocoValidation = PocoWriterUnionBenchmarkRecord.CreateAvroDeserializer(
            new BenchmarkSchemaRegistryClient(new Schema
            {
                SchemaType = SchemaType.Avro,
                SchemaString = AvroPocoValidationSchema
            }),
            new AvroDeserializerConfig
            {
                SchemaIdStrategy = SchemaIdDeserializerStrategy.Header,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            });
        _avroPocoValidationPreparer = _avroPocoValidation;

        var avroHeader = new Header(
            SchemaIdentityHeaderNames.Value,
            SchemaIdentityFraming.CreateSchemaGuidFrame(SchemaGuid));
        var protobufIdentity = SchemaIdentityFraming.CreateSchemaGuidFrame(SchemaGuid);
        var protobufHeaderValue = new byte[protobufIdentity.Length + 1];
        protobufIdentity.CopyTo(protobufHeaderValue, 0);
        var protobufHeader = new Header(SchemaIdentityHeaderNames.Value, protobufHeaderValue);
        _avroContext = CreateContext(avroHeader);
        _protobufContext = CreateContext(protobufHeader);
        _avroPrefixContext = CreateContext();
        _protobufPrefixContext = CreateContext();
        _avroHeaders = CreateLookup(_avro, avroHeader);
        _protobufHeaders = CreateLookup(_protobuf, protobufHeader);
        _avroPrefixHeaders = CreateLookup(_avroDual, identityHeader: null);
        _protobufPrefixHeaders = CreateLookup(_protobufDual, identityHeader: null);
        _avroValidationHeaders = CreateLookup(_avroValidation, avroHeader);
        _avroPocoValidationHeaders = CreateLookup(_avroPocoValidation, avroHeader);

        await PrepareAvroGuidHeaderCached().ConfigureAwait(false);
        await PrepareProtobufGuidHeaderCached().ConfigureAwait(false);
        await _avroValidationPreparer.PrepareAsync(
                AvroPayload,
                _avroContext,
                _avroValidationHeaders,
                CancellationToken.None)
            .ConfigureAwait(false);
        await _avroPocoValidationPreparer.PrepareAsync(
                AvroPocoPayload,
                _avroContext,
                _avroPocoValidationHeaders,
                CancellationToken.None)
            .ConfigureAwait(false);
        _ = RecordHeaderDeserializer.Deserialize(
            _avroDual,
            AvroPrefixPayload,
            _avroPrefixContext,
            in _avroPrefixHeaders);
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await _avro.DisposeAsync().ConfigureAwait(false);
        await _protobuf.DisposeAsync().ConfigureAwait(false);
        await _avroDual.DisposeAsync().ConfigureAwait(false);
        await _protobufDual.DisposeAsync().ConfigureAwait(false);
        await _avroValidation.DisposeAsync().ConfigureAwait(false);
        await _avroPocoValidation.DisposeAsync().ConfigureAwait(false);
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

    [Benchmark(Description = "Avro Dual prefix prepared deserialize")]
    public GenericRecord DeserializeAvroDualPrefixRouted()
    {
        if (!_avroDualPreparer.TryDeserialize(
            AvroPrefixPayload,
            _avroPrefixContext,
            in _avroPrefixHeaders,
            out var value))
        {
            throw new InvalidOperationException("Avro deserializer was not prepared.");
        }

        return value;
    }

    [Benchmark(Description = "Avro Dual prefix parent double-read control")]
    public GenericRecord DeserializeAvroDualPrefixDoubleRead()
    {
        _ = _avroDoubleReadPreparer.TryDeserialize(
            AvroPrefixPayload,
            _avroPrefixContext,
            in _avroPrefixHeaders,
            out var value);
        return value;
    }

    [Benchmark(Description = "Avro GUID header inline-validation cache")]
    public GenericRecord DeserializeAvroGuidHeaderWithInlineValidation()
    {
        if (!_avroValidationPreparer.TryDeserialize(
                AvroPayload,
                _avroContext,
                in _avroValidationHeaders,
                out var value))
        {
            throw new InvalidOperationException("Avro validation deserializer was not prepared.");
        }

        return value;
    }

    [Benchmark(Description = "Avro GUID inline-validator decision cache")]
    public object? ResolveAvroGuidInlineValidationDecision() =>
        _avroValidation.GetInlineValidator(-1, _avroValidationSchema);

    [Benchmark(Description = "Avro POCO GUID header inline-validation cache")]
    public PocoWriterUnionBenchmarkRecord DeserializeAvroPocoGuidHeaderWithInlineValidation()
    {
        if (!_avroPocoValidationPreparer.TryDeserialize(
                AvroPocoPayload,
                _avroContext,
                in _avroPocoValidationHeaders,
                out var value))
        {
            throw new InvalidOperationException("Avro POCO validation deserializer was not prepared.");
        }

        return value;
    }

    [Benchmark(Description = "Protobuf Dual prefix prepared deserialize")]
    public StringValue DeserializeProtobufDualPrefixRouted()
    {
        if (!_protobufDualPreparer.TryDeserialize(
            ProtobufPrefixPayload,
            _protobufPrefixContext,
            in _protobufPrefixHeaders,
            out var value))
        {
            throw new InvalidOperationException("Protobuf deserializer was not prepared.");
        }

        return value;
    }

    [Benchmark(Description = "Protobuf Dual prefix parent double-read control")]
    public StringValue DeserializeProtobufDualPrefixDoubleRead()
    {
        _ = _protobufDoubleReadPreparer.TryDeserialize(
            ProtobufPrefixPayload,
            _protobufPrefixContext,
            in _protobufPrefixHeaders,
            out var value);
        return value;
    }

    private static SerializationContext CreateContext(Header? identityHeader = null) => new()
    {
        Topic = "benchmark-orders",
        Component = SerializationComponent.Value,
        Headers = identityHeader is null ? null : new Headers(1).Add(identityHeader.Value)
    };

    private static RecordHeaderRoutingLookup CreateLookup<T>(IDeserializer<T> deserializer, Header? identityHeader)
    {
        Header[]? headers = identityHeader is null ? null : [identityHeader.Value];
        var plan = RecordHeaderRoutingPlan.Create<string, T>(null, deserializer)!;
        return new RecordHeaderRoutingLookup(
            plan,
            headers,
            headers?.Length ?? 0,
            firstIndex: 0,
            secondIndex: identityHeader is null ? 0 : 1,
            routedHeaderTailOffset: RecordHeaderRoutingPlan.FullyIndexedWithoutTail);
    }

    private sealed class DoubleReadPrefixPreparer<T>(IDeserializer<T> deserializer)
        : IRecordHeaderAsyncDeserializerPreparer<T>
    {
        public bool TryDeserialize(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            in RecordHeaderRoutingLookup headers,
            out T value)
        {
            var headerName = context.Component == SerializationComponent.Key
                ? SchemaIdentityHeaderNames.Key
                : SchemaIdentityHeaderNames.Value;
            var hasIdentityHeader = headers.TryGetLast(headerName, out var identityHeader);
            _ = SchemaIdentityFraming.Read(
                data.Span,
                hasIdentityHeader ? identityHeader : null,
                SchemaIdDeserializerStrategy.Dual,
                out _,
                out _);
            value = deserializer.Deserialize(data, context);
            return true;
        }

        public ValueTask PrepareAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            RecordHeaderRoutingLookup headers,
            CancellationToken cancellationToken) => default;
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
