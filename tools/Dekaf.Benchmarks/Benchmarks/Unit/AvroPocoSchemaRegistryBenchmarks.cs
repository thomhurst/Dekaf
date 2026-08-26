using System.Buffers;
using System.Buffers.Binary;
using Avro.Generic;
using Avro.IO;
using Avro.Specific;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Records;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Avro.Poco;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Compares prepared generated POCO, SpecificRecord, and GenericRecord Avro serialization.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class AvroPocoSchemaRegistryBenchmarks
{
    private const string SchemaJson =
        """
        {"type":"record","name":"PocoBenchmarkSpecificRecord","namespace":"Dekaf.Benchmarks.Benchmarks.Unit","fields":[{"name":"id","type":"int"},{"name":"name","type":"string"}]}
        """;

    private readonly SerializationContext _context = new()
    {
        Topic = "avro-poco-benchmark",
        Component = SerializationComponent.Value
    };
    private readonly SerializationContext _headerContext = new()
    {
        Topic = "avro-poco-header-benchmark",
        Component = SerializationComponent.Value,
        Headers = new Headers(1)
    };
    private AvroPocoSchemaRegistrySerializer<PocoBenchmarkRecord, PocoBenchmarkRecord.AvroCodec> _poco = null!;
    private AvroPocoSchemaRegistrySerializer<PocoBenchmarkRecord, PocoBenchmarkRecord.AvroCodec> _headerPoco = null!;
    private AvroSchemaRegistrySerializer<SpecificPocoBenchmarkRecord> _specific = null!;
    private AvroSchemaRegistrySerializer<GenericRecord> _generic = null!;
    private PocoBenchmarkRecord _pocoValue = null!;
    private SpecificPocoBenchmarkRecord _specificValue = null!;
    private GenericRecord _genericValue = null!;
    private ArrayBufferWriter<byte> _buffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _poco = PocoBenchmarkRecord.CreateAvroSerializer(
            new AvroSchemaRegistrySerializerBenchmarks.BenchmarkSchemaRegistryClient());
        _headerPoco = PocoBenchmarkRecord.CreateAvroSerializer(
            new AvroSchemaRegistrySerializerBenchmarks.BenchmarkSchemaRegistryClient(),
            new AvroSerializerConfig { SchemaIdStrategy = SchemaIdSerializerStrategy.Header });
        _specific = new AvroSchemaRegistrySerializer<SpecificPocoBenchmarkRecord>(
            new AvroSchemaRegistrySerializerBenchmarks.BenchmarkSchemaRegistryClient());
        _generic = new AvroSchemaRegistrySerializer<GenericRecord>(
            new AvroSchemaRegistrySerializerBenchmarks.BenchmarkSchemaRegistryClient());
        _pocoValue = new PocoBenchmarkRecord { Id = 42, Name = "benchmark" };
        _specificValue = new SpecificPocoBenchmarkRecord { id = 42, name = "benchmark" };
        var schema = (Avro.RecordSchema)AvroSchema.Parse(SchemaJson);
        _genericValue = new GenericRecord(schema);
        _genericValue.Add("id", 42);
        _genericValue.Add("name", "benchmark");
        _buffer = new ArrayBufferWriter<byte>(1024);

        _poco.Serialize(_pocoValue, ref _buffer, _context);
        _buffer.Clear();
        _headerPoco.Serialize(_pocoValue, ref _buffer, _headerContext);
        _buffer.Clear();
        _headerContext.Headers!.Clear();
        _specific.Serialize(_specificValue, ref _buffer, _context);
        _buffer.Clear();
        _generic.Serialize(_genericValue, ref _buffer, _context);
        _buffer.Clear();
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await _poco.DisposeAsync().ConfigureAwait(false);
        await _headerPoco.DisposeAsync().ConfigureAwait(false);
        await _specific.DisposeAsync().ConfigureAwait(false);
        await _generic.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(Baseline = true, Description = "Serialize generated POCO")]
    public void SerializePoco()
    {
        _buffer.Clear();
        _poco.Serialize(_pocoValue, ref _buffer, _context);
    }

    [Benchmark(Description = "Serialize generated POCO with GUID header")]
    public void SerializePocoWithGuidHeader()
    {
        _buffer.Clear();
        _headerContext.Headers!.Clear();
        _headerPoco.Serialize(_pocoValue, ref _buffer, _headerContext);
    }

    [Benchmark(Description = "Serialize SpecificRecord")]
    public void SerializeSpecificRecord()
    {
        _buffer.Clear();
        _specific.Serialize(_specificValue, ref _buffer, _context);
    }

    [Benchmark(Description = "Serialize GenericRecord")]
    public void SerializeGenericRecord()
    {
        _buffer.Clear();
        _generic.Serialize(_genericValue, ref _buffer, _context);
    }

    private sealed class SpecificPocoBenchmarkRecord : ISpecificRecord
    {
        public static readonly AvroSchema _SCHEMA = AvroSchema.Parse(SchemaJson);

        public int id { get; init; }
        public string name { get; init; } = string.Empty;
        public AvroSchema Schema => _SCHEMA;

        public object Get(int fieldPos) => fieldPos switch
        {
            0 => id,
            1 => name,
            _ => throw new ArgumentOutOfRangeException(nameof(fieldPos))
        };

        public void Put(int fieldPos, object fieldValue) => throw new NotSupportedException();
    }
}

/// <summary>Verifies steady-state generated serialization across representative supported shapes.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class AvroPocoRepresentativeSerializationBenchmarks
{
    private readonly SerializationContext _context = new()
    {
        Topic = "avro-poco-representative",
        Component = SerializationComponent.Value
    };
    private AvroPocoSchemaRegistrySerializer<RepresentativePocoRecord, RepresentativePocoRecord.AvroCodec>
        _serializer = null!;
    private RepresentativePocoRecord _value = null!;
    private ArrayBufferWriter<byte> _buffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _serializer = RepresentativePocoRecord.CreateAvroSerializer(
            new AvroSchemaRegistrySerializerBenchmarks.BenchmarkSchemaRegistryClient());
        _value = new RepresentativePocoRecord
        {
            Id = 42,
            Name = "benchmark",
            Note = null,
            Status = RepresentativePocoStatus.Ready,
            Scores = [1, 2, 3],
            Tags = ["fast", "native"],
            Totals = new Dictionary<string, long> { ["net"] = 40, ["tax"] = 2 },
            Address = new RepresentativePocoAddress { City = "London" },
            Created = DateTime.UnixEpoch,
            CorrelationId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
            Amount = 12345.67m
        };
        _buffer = new ArrayBufferWriter<byte>(1024);
        _serializer.Serialize(_value, ref _buffer, _context);
        _buffer.Clear();
    }

    [GlobalCleanup]
    public async ValueTask Cleanup() => await _serializer.DisposeAsync().ConfigureAwait(false);

    [Benchmark(Description = "Serialize representative generated POCO")]
    public void Serialize()
    {
        _buffer.Clear();
        _serializer.Serialize(_value, ref _buffer, _context);
    }
}

/// <summary>Compares prepared generated POCO, SpecificRecord, and GenericRecord Avro deserialization.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class AvroPocoSchemaRegistryDeserializationBenchmarks
{
    private const int SchemaId = 1;
    private const string DecimalSchemaJson =
        """
        {"type":"record","name":"PocoBenchmarkDecimalRecord","namespace":"Dekaf.Benchmarks.Benchmarks.Unit","fields":[{"name":"amount","type":{"type":"bytes","logicalType":"decimal","precision":9,"scale":2}}]}
        """;
    private const string WriterUnionSchemaJson =
        """
        {"type":"record","name":"PocoWriterUnionBenchmarkRecord","namespace":"Dekaf.Benchmarks.Benchmarks.Unit","fields":[{"name":"value","type":["int","long"]}]}
        """;
    private const string CollectionSchemaJson =
        """
        {"type":"record","name":"PocoCollectionBenchmarkRecord","namespace":"Dekaf.Benchmarks.Benchmarks.Unit","fields":[{"name":"Values","type":{"type":"array","items":"int"}}]}
        """;
    private const string ReferenceCollectionSchemaJson =
        """
        {"type":"record","name":"PocoReferenceCollectionBenchmarkRecord","namespace":"Dekaf.Benchmarks.Benchmarks.Unit","fields":[{"name":"Values","type":{"type":"array","items":"string"}}]}
        """;
    private const string SkipSchemaJson =
        """
        {"type":"record","name":"PocoSkipBenchmarkRecord","namespace":"Dekaf.Benchmarks.Benchmarks.Unit","fields":[{"name":"Id","type":"int"},{"name":"ignored","type":{"type":"record","name":"PocoSkippedRecord","fields":[{"name":"value","type":"int"}]}}]}
        """;
    private const string SkipCollectionSchemaJson =
        """
        {"type":"record","name":"PocoSkipBenchmarkRecord","namespace":"Dekaf.Benchmarks.Benchmarks.Unit","fields":[{"name":"Id","type":"int"},{"name":"ignored","type":{"type":"array","items":"int"}}]}
        """;
    private const string SkipEnumSchemaJson =
        """
        {"type":"record","name":"PocoSkipBenchmarkRecord","namespace":"Dekaf.Benchmarks.Benchmarks.Unit","fields":[{"name":"Id","type":"int"},{"name":"ignored","type":{"type":"enum","name":"PocoSkippedStatus","symbols":["A","B"]}}]}
        """;
    private const string TimeSpanSchemaJson =
        """
        {"type":"record","name":"PocoTimeSpanBenchmarkRecord","namespace":"Dekaf.Benchmarks","fields":[{"name":"Value","type":{"type":"long","logicalType":"time-micros"}}]}
        """;
    internal const string SchemaJson =
        """
        {"type":"record","name":"PocoBenchmarkSpecificRecord","namespace":"Dekaf.Benchmarks.Benchmarks.Unit","fields":[{"name":"id","type":"int"},{"name":"name","type":"string"}]}
        """;

    private readonly SerializationContext _context = new()
    {
        Topic = "avro-poco-benchmark",
        Component = SerializationComponent.Value
    };
    private AvroPocoSchemaRegistryDeserializer<PocoBenchmarkRecord, PocoBenchmarkRecord.AvroCodec> _poco = null!;
    private AvroPocoSchemaRegistryDeserializer<
        PocoWriterUnionBenchmarkRecord,
        PocoWriterUnionBenchmarkRecord.AvroCodec> _rulesValuePoco = null!;
    private AvroPocoSchemaRegistryDeserializer<
        PocoWriterUnionBenchmarkRecord,
        PocoWriterUnionBenchmarkRecord.AvroCodec> _cachedRulesValuePoco = null!;
    private IAsyncDeserializerPreparer<PocoBenchmarkRecord> _pocoPreparer = null!;
    private AvroSchemaRegistryDeserializer<PocoBenchmarkSpecificRecord> _specific = null!;
    private AvroSchemaRegistryDeserializer<GenericRecord> _generic = null!;
    private AvroPocoSchemaRegistryDeserializer<PocoBenchmarkDecimalRecord, PocoBenchmarkDecimalRecord.AvroCodec>
        _decimalPoco = null!;
    private AvroPocoSchemaRegistryDeserializer<PocoWriterUnionBenchmarkRecord, PocoWriterUnionBenchmarkRecord.AvroCodec>
        _writerUnionPoco = null!;
    private AvroPocoSchemaRegistryDeserializer<PocoWriterUnionBenchmarkRecord, PocoWriterUnionBenchmarkRecord.AvroCodec>
        _latestWriterUnionPoco = null!;
    private AvroPocoSchemaRegistryDeserializer<PocoCollectionBenchmarkRecord, PocoCollectionBenchmarkRecord.AvroCodec>
        _collectionPoco = null!;
    private AvroPocoSchemaRegistryDeserializer<
        PocoReferenceCollectionBenchmarkRecord,
        PocoReferenceCollectionBenchmarkRecord.AvroCodec> _referenceCollectionPoco = null!;
    private AvroPocoSchemaRegistryDeserializer<PocoSkipBenchmarkRecord, PocoSkipBenchmarkRecord.AvroCodec>
        _skipPoco = null!;
    private AvroPocoSchemaRegistryDeserializer<PocoSkipBenchmarkRecord, PocoSkipBenchmarkRecord.AvroCodec>
        _skipCollectionPoco = null!;
    private AvroPocoSchemaRegistryDeserializer<PocoSkipBenchmarkRecord, PocoSkipBenchmarkRecord.AvroCodec>
        _skipEnumPoco = null!;
    private AvroPocoSchemaRegistryDeserializer<PocoTimeSpanBenchmarkRecord, PocoTimeSpanBenchmarkRecord.AvroCodec>
        _timeSpanPoco = null!;
    private byte[] _wireData = null!;
    private byte[] _decimalWireData = null!;
    private byte[] _writerUnionWireData = null!;
    private byte[] _collectionWireData = null!;
    private byte[] _referenceCollectionWireData = null!;
    private byte[] _skipWireData = null!;
    private byte[] _skipCollectionWireData = null!;
    private byte[] _skipEnumWireData = null!;
    private byte[] _timeSpanWireData = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var registrySchema = new global::Dekaf.SchemaRegistry.Schema
        {
            SchemaType = global::Dekaf.SchemaRegistry.SchemaType.Avro,
            SchemaString = SchemaJson
        };
        _poco = PocoBenchmarkRecord.CreateAvroDeserializer(new BenchmarkSchemaRegistryClient(registrySchema));
        _pocoPreparer = _poco;
        _rulesValuePoco = PocoWriterUnionBenchmarkRecord.CreateAvroDeserializer(
            new NonCachingBenchmarkSchemaRegistryClient(new global::Dekaf.SchemaRegistry.Schema
            {
                SchemaType = global::Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = WriterUnionSchemaJson
            }),
            new AvroDeserializerConfig { RuleExecutor = PassThroughRuleExecutor.Instance });
        _cachedRulesValuePoco = PocoWriterUnionBenchmarkRecord.CreateAvroDeserializer(
            new BenchmarkSchemaRegistryClient(new global::Dekaf.SchemaRegistry.Schema
            {
                SchemaType = global::Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = WriterUnionSchemaJson
            }),
            new AvroDeserializerConfig { RuleExecutor = PassThroughRuleExecutor.Instance });
        _specific = new AvroSchemaRegistryDeserializer<PocoBenchmarkSpecificRecord>(
            new BenchmarkSchemaRegistryClient(registrySchema));
        _generic = new AvroSchemaRegistryDeserializer<GenericRecord>(
            new BenchmarkSchemaRegistryClient(registrySchema));
        _decimalPoco = PocoBenchmarkDecimalRecord.CreateAvroDeserializer(
            new BenchmarkSchemaRegistryClient(new global::Dekaf.SchemaRegistry.Schema
            {
                SchemaType = global::Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = DecimalSchemaJson
            }));
        _writerUnionPoco = PocoWriterUnionBenchmarkRecord.CreateAvroDeserializer(
            new BenchmarkSchemaRegistryClient(new global::Dekaf.SchemaRegistry.Schema
            {
                SchemaType = global::Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = WriterUnionSchemaJson
            }));
        _latestWriterUnionPoco = PocoWriterUnionBenchmarkRecord.CreateAvroDeserializer(
            new BenchmarkSchemaRegistryClient(new global::Dekaf.SchemaRegistry.Schema
            {
                SchemaType = global::Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = WriterUnionSchemaJson
            }),
            new AvroDeserializerConfig { UseLatestVersion = true });
        _collectionPoco = PocoCollectionBenchmarkRecord.CreateAvroDeserializer(
            new BenchmarkSchemaRegistryClient(new global::Dekaf.SchemaRegistry.Schema
            {
                SchemaType = global::Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = CollectionSchemaJson
            }));
        _referenceCollectionPoco = PocoReferenceCollectionBenchmarkRecord.CreateAvroDeserializer(
            new BenchmarkSchemaRegistryClient(new global::Dekaf.SchemaRegistry.Schema
            {
                SchemaType = global::Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = ReferenceCollectionSchemaJson
            }));
        _skipPoco = PocoSkipBenchmarkRecord.CreateAvroDeserializer(
            new BenchmarkSchemaRegistryClient(new global::Dekaf.SchemaRegistry.Schema
            {
                SchemaType = global::Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = SkipSchemaJson
            }));
        _skipCollectionPoco = PocoSkipBenchmarkRecord.CreateAvroDeserializer(
            new BenchmarkSchemaRegistryClient(new global::Dekaf.SchemaRegistry.Schema
            {
                SchemaType = global::Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = SkipCollectionSchemaJson
            }));
        _skipEnumPoco = PocoSkipBenchmarkRecord.CreateAvroDeserializer(
            new BenchmarkSchemaRegistryClient(new global::Dekaf.SchemaRegistry.Schema
            {
                SchemaType = global::Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = SkipEnumSchemaJson
            }));
        _timeSpanPoco = PocoTimeSpanBenchmarkRecord.CreateAvroDeserializer(
            new BenchmarkSchemaRegistryClient(new global::Dekaf.SchemaRegistry.Schema
            {
                SchemaType = global::Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = TimeSpanSchemaJson
            }));
        _wireData = CreateWireData();
        _decimalWireData = [0, 0, 0, 0, SchemaId, 0x04, 0x30, 0x39];
        _writerUnionWireData = [0, 0, 0, 0, SchemaId, 0x00, 0x54];
        _collectionWireData = [0, 0, 0, 0, SchemaId, 0x06, 0x02, 0x04, 0x06, 0x00];
        _referenceCollectionWireData =
        [
            0, 0, 0, 0, SchemaId, 0x06,
            0x06, (byte)'o', (byte)'n', (byte)'e',
            0x06, (byte)'t', (byte)'w', (byte)'o',
            0x0A, (byte)'t', (byte)'h', (byte)'r', (byte)'e', (byte)'e',
            0x00
        ];
        _skipWireData = [0, 0, 0, 0, SchemaId, 0x54, 0x0E];
        _skipCollectionWireData = [0, 0, 0, 0, SchemaId, 0x54, 0x06, 0x02, 0x04, 0x06, 0x00];
        _skipEnumWireData = [0, 0, 0, 0, SchemaId, 0x54, 0x02];
        _timeSpanWireData = CreateTimeSpanWireData();

        await _poco.WarmupAsync(SchemaId).ConfigureAwait(false);
        await ((IAsyncDeserializerPreparer<PocoWriterUnionBenchmarkRecord>)_rulesValuePoco)
            .PrepareAsync(_writerUnionWireData, _context)
            .ConfigureAwait(false);
        await ((IAsyncDeserializerPreparer<PocoWriterUnionBenchmarkRecord>)_cachedRulesValuePoco)
            .PrepareAsync(_writerUnionWireData, _context)
            .ConfigureAwait(false);
        await _specific.WarmupAsync(SchemaId).ConfigureAwait(false);
        await _generic.WarmupAsync(SchemaId).ConfigureAwait(false);
        await _decimalPoco.WarmupAsync(SchemaId).ConfigureAwait(false);
        await _writerUnionPoco.WarmupAsync(SchemaId).ConfigureAwait(false);
        await _collectionPoco.WarmupAsync(SchemaId).ConfigureAwait(false);
        await _referenceCollectionPoco.WarmupAsync(SchemaId).ConfigureAwait(false);
        await _skipPoco.WarmupAsync(SchemaId).ConfigureAwait(false);
        await _skipCollectionPoco.WarmupAsync(SchemaId).ConfigureAwait(false);
        await _skipEnumPoco.WarmupAsync(SchemaId).ConfigureAwait(false);
        await _timeSpanPoco.WarmupAsync(SchemaId).ConfigureAwait(false);
        _ = _latestWriterUnionPoco.Deserialize(_writerUnionWireData, _context);
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await _poco.DisposeAsync().ConfigureAwait(false);
        await _rulesValuePoco.DisposeAsync().ConfigureAwait(false);
        await _cachedRulesValuePoco.DisposeAsync().ConfigureAwait(false);
        await _specific.DisposeAsync().ConfigureAwait(false);
        await _generic.DisposeAsync().ConfigureAwait(false);
        await _decimalPoco.DisposeAsync().ConfigureAwait(false);
        await _writerUnionPoco.DisposeAsync().ConfigureAwait(false);
        await _latestWriterUnionPoco.DisposeAsync().ConfigureAwait(false);
        await _collectionPoco.DisposeAsync().ConfigureAwait(false);
        await _referenceCollectionPoco.DisposeAsync().ConfigureAwait(false);
        await _skipPoco.DisposeAsync().ConfigureAwait(false);
        await _skipCollectionPoco.DisposeAsync().ConfigureAwait(false);
        await _skipEnumPoco.DisposeAsync().ConfigureAwait(false);
        await _timeSpanPoco.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(Baseline = true, Description = "Deserialize generated POCO")]
    public PocoBenchmarkRecord DeserializePoco() => _poco.Deserialize(_wireData, _context);

    [Benchmark(Description = "Check prepared then deserialize generated POCO")]
    public PocoBenchmarkRecord PrepareThenDeserializePoco()
    {
        if (!_pocoPreparer.TryDeserialize(_wireData, _context, out var value))
            throw new InvalidOperationException("Benchmark deserializer was not prepared.");

        return value;
    }

    [Benchmark(Description = "Deserialize generated value POCO with prepared rules")]
    public PocoWriterUnionBenchmarkRecord DeserializeValuePocoWithPreparedRules() =>
        _rulesValuePoco.Deserialize(_writerUnionWireData, _context);

    [Benchmark(Description = "Deserialize generated value POCO with cached rules")]
    public PocoWriterUnionBenchmarkRecord DeserializeValuePocoWithCachedRules() =>
        _cachedRulesValuePoco.Deserialize(_writerUnionWireData, _context);

    [Benchmark(Description = "Deserialize SpecificRecord")]
    public PocoBenchmarkSpecificRecord DeserializeSpecificRecord() => _specific.Deserialize(_wireData, _context);

    [Benchmark(Description = "Deserialize GenericRecord")]
    public GenericRecord DeserializeGenericRecord() => _generic.Deserialize(_wireData, _context);

    [Benchmark(Description = "Deserialize generated decimal POCO")]
    public PocoBenchmarkDecimalRecord DeserializeDecimalPoco() =>
        _decimalPoco.Deserialize(_decimalWireData, _context);

    [Benchmark(Description = "Deserialize generated POCO writer union")]
    public PocoWriterUnionBenchmarkRecord DeserializeWriterUnionPoco() =>
        _writerUnionPoco.Deserialize(_writerUnionWireData, _context);

    [Benchmark(Description = "Deserialize generated value-type POCO with latest schema")]
    public PocoWriterUnionBenchmarkRecord DeserializeWriterUnionPocoUseLatest() =>
        _latestWriterUnionPoco.Deserialize(_writerUnionWireData, _context);

    [Benchmark(Description = "Deserialize generated collection POCO")]
    public PocoCollectionBenchmarkRecord DeserializeCollectionPoco() =>
        _collectionPoco.Deserialize(_collectionWireData, _context);

    [Benchmark(Description = "Deserialize generated reference collection POCO")]
    public PocoReferenceCollectionBenchmarkRecord DeserializeReferenceCollectionPoco() =>
        _referenceCollectionPoco.Deserialize(_referenceCollectionWireData, _context);

    [Benchmark(Description = "Deserialize generated POCO with skipped record")]
    public PocoSkipBenchmarkRecord DeserializeSkippedRecordPoco() =>
        _skipPoco.Deserialize(_skipWireData, _context);

    [Benchmark(Description = "Deserialize generated POCO with skipped collection")]
    public PocoSkipBenchmarkRecord DeserializeSkippedCollectionPoco() =>
        _skipCollectionPoco.Deserialize(_skipCollectionWireData, _context);

    [Benchmark(Description = "Deserialize generated POCO with skipped enum")]
    public PocoSkipBenchmarkRecord DeserializeSkippedEnumPoco() =>
        _skipEnumPoco.Deserialize(_skipEnumWireData, _context);

    [Benchmark(Description = "Deserialize generated time-micros TimeSpan POCO")]
    public PocoTimeSpanBenchmarkRecord DeserializeTimeSpanPoco() =>
        _timeSpanPoco.Deserialize(_timeSpanWireData, _context);

    private static byte[] CreateWireData()
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(SchemaJson);
        var record = new GenericRecord(schema);
        record.Add("id", 42);
        record.Add("name", "benchmark");
        using var payload = new MemoryStream();
        var writer = new GenericDatumWriter<GenericRecord>(schema);
        var encoder = new BinaryEncoder(payload);
        writer.Write(record, encoder);
        encoder.Flush();

        var wireData = new byte[5 + payload.Length];
        wireData[0] = 0;
        BinaryPrimitives.WriteInt32BigEndian(wireData.AsSpan(1, 4), SchemaId);
        payload.GetBuffer().AsSpan(0, (int)payload.Length).CopyTo(wireData.AsSpan(5));
        return wireData;
    }

    private static byte[] CreateTimeSpanWireData()
    {
        const long microseconds = 43_200_000_000L;
        var encoded = (ulong)(microseconds << 1);
        var wireData = new byte[15];
        BinaryPrimitives.WriteInt32BigEndian(wireData.AsSpan(1, 4), SchemaId);
        var position = 5;
        while ((encoded & ~0x7FUL) != 0)
        {
            wireData[position++] = (byte)((encoded & 0x7F) | 0x80);
            encoded >>= 7;
        }
        wireData[position++] = (byte)encoded;
        Array.Resize(ref wireData, position);
        return wireData;
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

    private sealed class NonCachingBenchmarkSchemaRegistryClient(global::Dekaf.SchemaRegistry.Schema schema)
        : global::Dekaf.SchemaRegistry.ISchemaRegistryClient
    {
        public Task<global::Dekaf.SchemaRegistry.Schema> GetSchemaAsync(
            int id,
            CancellationToken cancellationToken = default) => Task.FromResult(schema);

        public Task<global::Dekaf.SchemaRegistry.Schema> GetSchemaAsync(
            int id,
            string subject,
            CancellationToken cancellationToken = default) => Task.FromResult(schema);

        public Task<int> RegisterSchemaAsync(
            string subject,
            global::Dekaf.SchemaRegistry.Schema candidate,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<global::Dekaf.SchemaRegistry.RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<global::Dekaf.SchemaRegistry.RegisteredSchema> LookupSchemaAsync(
            string subject,
            global::Dekaf.SchemaRegistry.Schema candidate,
            bool ignoreDeletedSchemas = true,
            bool normalize = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            global::Dekaf.SchemaRegistry.Schema candidate,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<int>> GetVersionsAsync(
            string subject,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> IsCompatibleAsync(
            string subject,
            global::Dekaf.SchemaRegistry.Schema candidate,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(
            string subject,
            bool permanent = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Dispose() { }
    }

    internal sealed class BenchmarkSchemaRegistryClient(global::Dekaf.SchemaRegistry.Schema schema)
        : global::Dekaf.SchemaRegistry.ISchemaRegistryClient, global::Dekaf.SchemaRegistry.ISchemaRegistryCache
    {
        public Task<global::Dekaf.SchemaRegistry.Schema> GetSchemaAsync(
            int id,
            CancellationToken cancellationToken = default) => Task.FromResult(schema);

        public Task<global::Dekaf.SchemaRegistry.Schema> GetSchemaAsync(
            int id,
            string subject,
            CancellationToken cancellationToken = default) => Task.FromResult(schema);

        public bool TryGetCachedSchema(int id, out global::Dekaf.SchemaRegistry.Schema cachedSchema)
        {
            cachedSchema = schema;
            return true;
        }

        public bool TryGetCachedSchema(
            int id,
            string subject,
            out global::Dekaf.SchemaRegistry.Schema cachedSchema) => TryGetCachedSchema(id, out cachedSchema);

        public Task<int> RegisterSchemaAsync(
            string subject,
            global::Dekaf.SchemaRegistry.Schema candidate,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<global::Dekaf.SchemaRegistry.RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default) => Task.FromResult(CreateRegisteredSchema(subject));

        public Task<global::Dekaf.SchemaRegistry.RegisteredSchema> LookupSchemaAsync(
            string subject,
            global::Dekaf.SchemaRegistry.Schema candidate,
            bool ignoreDeletedSchemas = true,
            bool normalize = false,
            CancellationToken cancellationToken = default) => Task.FromResult(CreateRegisteredSchema(subject));

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            global::Dekaf.SchemaRegistry.Schema candidate,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<int>> GetVersionsAsync(
            string subject,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> IsCompatibleAsync(
            string subject,
            global::Dekaf.SchemaRegistry.Schema candidate,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(
            string subject,
            bool permanent = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Dispose() { }

        private global::Dekaf.SchemaRegistry.RegisteredSchema CreateRegisteredSchema(string subject) => new()
        {
            Id = SchemaId,
            Subject = subject,
            Version = 1,
            Schema = schema
        };
    }
}

/// <summary>Measures the warmed hybrid Avro decoder through the buffered consumer path.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
[Config(typeof(AvroPocoPreparedConsumerConfig))]
public class AvroPocoPreparedConsumerBenchmarks
{
    private const int RecordsPerBatch = 1_000;
    private const int BatchCount = 800;
    private const int PollsPerIteration = RecordsPerBatch * BatchCount;
    private const string Topic = "avro-poco-prepared-consumer";
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(10);

    private sealed class AvroPocoPreparedConsumerConfig : ManualConfig
    {
        public AvroPocoPreparedConsumerConfig()
        {
            AddJob(Job.Default
                .WithStrategy(RunStrategy.Throughput)
                .WithLaunchCount(1)
                .WithWarmupCount(10)
                .WithIterationCount(10)
                .WithInvocationCount(PollsPerIteration)
                .WithUnrollFactor(1));
        }
    }

    private Record[][] _recordArrays = null!;
    private KafkaConsumer<ReadOnlyMemory<byte>, PocoBenchmarkRecord> _plainConsumer = null!;
    private KafkaConsumer<ReadOnlyMemory<byte>, PocoBenchmarkRecord> _preparedConsumer = null!;
    private AvroPocoSchemaRegistryDeserializer<PocoBenchmarkRecord, PocoBenchmarkRecord.AvroCodec>
        _plainDeserializer = null!;
    private AvroPocoSchemaRegistryDeserializer<PocoBenchmarkRecord, PocoBenchmarkRecord.AvroCodec>
        _preparedDeserializer = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var schema = new global::Dekaf.SchemaRegistry.Schema
        {
            SchemaType = global::Dekaf.SchemaRegistry.SchemaType.Avro,
            SchemaString = AvroPocoSchemaRegistryDeserializationBenchmarks.SchemaJson
        };
        _plainDeserializer = PocoBenchmarkRecord.CreateAvroDeserializer(
            new AvroPocoSchemaRegistryDeserializationBenchmarks.BenchmarkSchemaRegistryClient(schema));
        _preparedDeserializer = PocoBenchmarkRecord.CreateAvroDeserializer(
            new AvroPocoSchemaRegistryDeserializationBenchmarks.BenchmarkSchemaRegistryClient(schema));
        await _plainDeserializer.WarmupAsync(1).ConfigureAwait(false);
        await _preparedDeserializer.WarmupAsync(1).ConfigureAwait(false);

        _plainConsumer = CreateConsumer(new PlainDeserializer(_plainDeserializer));
        _preparedConsumer = CreateConsumer(_preparedDeserializer);

        var wireData = new byte[] { 0, 0, 0, 0, 1, 84, 6, (byte)'A', (byte)'d', (byte)'a' };
        _recordArrays = new Record[10][];
        for (var batchIndex = 0; batchIndex < _recordArrays.Length; batchIndex++)
        {
            var records = new Record[RecordsPerBatch];
            for (var recordIndex = 0; recordIndex < records.Length; recordIndex++)
            {
                records[recordIndex] = new Record
                {
                    OffsetDelta = recordIndex,
                    TimestampDelta = recordIndex,
                    Key = ReadOnlyMemory<byte>.Empty,
                    Value = wireData
                };
            }

            _recordArrays[batchIndex] = records;
        }
    }

    [IterationSetup(Target = nameof(PlainSynchronous))]
    public void PlainSetup() => Reseed(_plainConsumer);

    [IterationSetup(Target = nameof(PreparedSynchronous))]
    public void PreparedSetup() => Reseed(_preparedConsumer);

    [Benchmark(Baseline = true)]
    public ValueTask<ConsumeResult<ReadOnlyMemory<byte>, PocoBenchmarkRecord>?> PlainSynchronous() =>
        _plainConsumer.ConsumeOneAsync(PollTimeout);

    [Benchmark]
    public ValueTask<ConsumeResult<ReadOnlyMemory<byte>, PocoBenchmarkRecord>?> PreparedSynchronous() =>
        _preparedConsumer.ConsumeOneAsync(PollTimeout);

    [GlobalCleanup]
    public void Cleanup()
    {
        BufferedConsumerHarness.DrainPendingFetches(_plainConsumer);
        BufferedConsumerHarness.DrainPendingFetches(_preparedConsumer);
        _plainConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _preparedConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _plainDeserializer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _preparedDeserializer.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private static KafkaConsumer<ReadOnlyMemory<byte>, PocoBenchmarkRecord> CreateConsumer(
        IDeserializer<PocoBenchmarkRecord> valueDeserializer)
    {
        var consumer = new KafkaConsumer<ReadOnlyMemory<byte>, PocoBenchmarkRecord>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1
            },
            Serializers.RawBytes,
            valueDeserializer);
        BufferedConsumerHarness.InitializeForBufferedFastPath(consumer, Topic, partition: 0);
        return consumer;
    }

    private void Reseed(KafkaConsumer<ReadOnlyMemory<byte>, PocoBenchmarkRecord> consumer) =>
        BufferedConsumerHarness.ReseedPendingFetches(
            consumer,
            Topic,
            partition: 0,
            _recordArrays,
            BatchCount,
            RecordsPerBatch);

    private sealed class PlainDeserializer(
        AvroPocoSchemaRegistryDeserializer<PocoBenchmarkRecord, PocoBenchmarkRecord.AvroCodec> inner)
        : IDeserializer<PocoBenchmarkRecord>
    {
        public PocoBenchmarkRecord Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            inner.Deserialize(data, context);
    }
}

[AvroRecord(Name = "PocoBenchmarkSpecificRecord", Namespace = "Dekaf.Benchmarks.Benchmarks.Unit")]
public sealed partial class PocoBenchmarkRecord
{
    [AvroField(Name = "id", Order = 0)]
    public int Id { get; init; }

    [AvroField(Name = "name", Order = 1)]
    public required string Name { get; init; }
}

/// <summary>Guards generated POCO rule serialization against per-message overhead and allocations.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class AvroPocoRuleSerializationBenchmarks
{
    private readonly SerializationContext _context = new()
    {
        Topic = "avro-poco-rules",
        Component = SerializationComponent.Value
    };
    private readonly PocoBenchmarkRecord _value = new() { Id = 42, Name = "benchmark" };
    private AvroPocoSchemaRegistrySerializer<PocoBenchmarkRecord, PocoBenchmarkRecord.AvroCodec>
        _serializer = null!;
    private ArrayBufferWriter<byte> _buffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _serializer = PocoBenchmarkRecord.CreateAvroSerializer(
            new AvroSchemaRegistrySerializerBenchmarks.BenchmarkSchemaRegistryClient(),
            new AvroSerializerConfig { RuleExecutor = PassThroughRuleExecutor.Instance });
        _buffer = new ArrayBufferWriter<byte>(64);
        _serializer.Serialize(_value, ref _buffer, _context);
        _buffer.Clear();
    }

    [GlobalCleanup]
    public async ValueTask Cleanup() => await _serializer.DisposeAsync().ConfigureAwait(false);

    [Benchmark]
    public void SerializeWithRules()
    {
        _buffer.Clear();
        _serializer.Serialize(_value, ref _buffer, _context);
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
}

/// <summary>Guards nested generated POCO rule-buffer growth against per-message allocations.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class AvroPocoReentrantRuleSerializationBenchmarks
{
    private readonly SerializationContext _context = new()
    {
        Topic = "avro-poco-reentrant-rules",
        Component = SerializationComponent.Value
    };
    private readonly PocoBenchmarkRecord _outerValue = new() { Id = 42, Name = "small" };
    private readonly PocoBenchmarkRecord _nestedValue = new() { Id = 7, Name = new string('x', 64 * 1024) };
    private AvroPocoSchemaRegistrySerializer<PocoBenchmarkRecord, PocoBenchmarkRecord.AvroCodec>
        _serializer = null!;
    private ArrayBufferWriter<byte> _outerBuffer = null!;
    private ArrayBufferWriter<byte> _nestedBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        var executor = new ReentrantRuleExecutor();
        _serializer = PocoBenchmarkRecord.CreateAvroSerializer(
            new AvroSchemaRegistrySerializerBenchmarks.BenchmarkSchemaRegistryClient(),
            new AvroSerializerConfig { RuleExecutor = executor });
        _outerBuffer = new ArrayBufferWriter<byte>(64);
        _nestedBuffer = new ArrayBufferWriter<byte>(128 * 1024);
        executor.Reenter = SerializeNested;
        _serializer.Serialize(_outerValue, ref _outerBuffer, _context);
        _outerBuffer.Clear();
    }

    [GlobalCleanup]
    public async ValueTask Cleanup() => await _serializer.DisposeAsync().ConfigureAwait(false);

    [Benchmark]
    public void SerializeWithReentrantRules()
    {
        _outerBuffer.Clear();
        _serializer.Serialize(_outerValue, ref _outerBuffer, _context);
    }

    private void SerializeNested()
    {
        _nestedBuffer.Clear();
        _serializer.Serialize(_nestedValue, ref _nestedBuffer, _context);
    }

    private sealed class ReentrantRuleExecutor : ISchemaRegistryRuleExecutor
    {
        private bool _isReentrant;

        internal Action? Reenter { get; set; }

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
        {
            if (_isReentrant)
                return payload;

            _isReentrant = true;
            try
            {
                Reenter!();
            }
            finally
            {
                _isReentrant = false;
            }

            return payload;
        }

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;
    }
}

/// <summary>Guards stable large generated POCO payloads against repeated sizing traversals.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class AvroPocoLargePayloadSerializationBenchmarks
{
    private const int PayloadLength = 1024 * 1024 + 1;
    private readonly SerializationContext _context = new()
    {
        Topic = "avro-poco-large",
        Component = SerializationComponent.Value
    };
    private AvroPocoSchemaRegistrySerializer<PocoLargeBenchmarkRecord, PocoLargeBenchmarkRecord.AvroCodec>
        _serializer = null!;
    private AvroPocoSchemaRegistrySerializer<PocoLargeBenchmarkRecord, PocoLargeBenchmarkRecord.AvroCodec>
        _rulesSerializer = null!;
    private PocoLargeBenchmarkRecord _value = null!;
    private ArrayBufferWriter<byte> _buffer = null!;
    private ArrayBufferWriter<byte> _rulesBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _serializer = PocoLargeBenchmarkRecord.CreateAvroSerializer(
            new AvroSchemaRegistrySerializerBenchmarks.BenchmarkSchemaRegistryClient());
        _rulesSerializer = PocoLargeBenchmarkRecord.CreateAvroSerializer(
            new AvroSchemaRegistrySerializerBenchmarks.BenchmarkSchemaRegistryClient(),
            new AvroSerializerConfig { RuleExecutor = PassThroughRuleExecutor.Instance });
        _value = new PocoLargeBenchmarkRecord { Values = new int[PayloadLength] };
        _buffer = new ArrayBufferWriter<byte>(PayloadLength + 16);
        _rulesBuffer = new ArrayBufferWriter<byte>(PayloadLength + 16);
        _serializer.Serialize(_value, ref _buffer, _context);
        _buffer.Clear();
        _rulesSerializer.Serialize(_value, ref _rulesBuffer, _context);
        _rulesBuffer.Clear();
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await _serializer.DisposeAsync().ConfigureAwait(false);
        await _rulesSerializer.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark]
    public void SerializeLargePayload()
    {
        _buffer.Clear();
        _serializer.Serialize(_value, ref _buffer, _context);
    }

    [Benchmark]
    public void SerializeLargePayloadWithRules()
    {
        _rulesBuffer.Clear();
        _rulesSerializer.Serialize(_value, ref _rulesBuffer, _context);
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
}

/// <summary>Guards mixed small and oversized generated POCO payload sizing.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class AvroPocoMixedPayloadSerializationBenchmarks
{
    private const int LargePayloadLength = 1024 * 1024 + 1;
    private readonly SerializationContext _context = new()
    {
        Topic = "avro-poco-mixed-payload",
        Component = SerializationComponent.Value
    };
    private readonly PocoBenchmarkRecord _smallValue = new() { Id = 42, Name = "small" };
    private PocoBenchmarkRecord _largeValue = null!;
    private AvroPocoSchemaRegistrySerializer<PocoBenchmarkRecord, PocoBenchmarkRecord.AvroCodec>
        _serializer = null!;
    private AvroPocoSchemaRegistrySerializer<PocoBenchmarkRecord, PocoBenchmarkRecord.AvroCodec>
        _directSerializer = null!;
    private ArrayBufferWriter<byte> _buffer = null!;
    private byte[]? _directBuffer;

    [GlobalSetup]
    public void Setup()
    {
        _serializer = PocoBenchmarkRecord.CreateAvroSerializer(
            new AvroSchemaRegistrySerializerBenchmarks.BenchmarkSchemaRegistryClient(),
            new AvroSerializerConfig { RuleExecutor = PassThroughRuleExecutor.Instance });
        _directSerializer = PocoBenchmarkRecord.CreateAvroSerializer(
            new AvroSchemaRegistrySerializerBenchmarks.BenchmarkSchemaRegistryClient());
        _largeValue = new PocoBenchmarkRecord { Id = 42, Name = new string('x', LargePayloadLength) };
        _buffer = new ArrayBufferWriter<byte>(2 * LargePayloadLength);
        Serialize(_largeValue);
        Serialize(_smallValue);
        SerializeDirect(_largeValue);
        SerializeDirect(_smallValue);
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await _serializer.DisposeAsync().ConfigureAwait(false);
        await _directSerializer.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(OperationsPerInvoke = 2)]
    public void SerializeAlternatingPayloadsDirect()
    {
        SerializeDirect(_largeValue);
        SerializeDirect(_smallValue);
    }

    [Benchmark]
    public void SerializeSmallPayloadDirect() => SerializeDirect(_smallValue);

    [Benchmark(OperationsPerInvoke = 2)]
    public void SerializeAlternatingPayloadsWithRules()
    {
        Serialize(_largeValue);
        Serialize(_smallValue);
    }

    [Benchmark]
    public void SerializeSmallPayloadWithRules() => Serialize(_smallValue);

    [Benchmark]
    public void SerializeLargePayloadWithRules() => Serialize(_largeValue);

    private void Serialize(PocoBenchmarkRecord value)
    {
        _buffer.Clear();
        _serializer.Serialize(value, ref _buffer, _context);
    }

    private void SerializeDirect(PocoBenchmarkRecord value)
    {
        var writer = new ReusableBufferWriter(ref _directBuffer, 256);
        _directSerializer.Serialize(value, ref writer, _context);
        writer.UpdateBufferRef(ref _directBuffer);
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
}

/// <summary>Measures the generated TimeSpan-to-time-micros validation path.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class AvroPocoTimeSpanSerializationBenchmarks
{
    private readonly SerializationContext _context = new()
    {
        Topic = "avro-poco-time-span",
        Component = SerializationComponent.Value
    };
    private AvroPocoSchemaRegistrySerializer<PocoTimeSpanBenchmarkRecord, PocoTimeSpanBenchmarkRecord.AvroCodec>
        _serializer = null!;
    private PocoTimeSpanBenchmarkRecord _value;
    private ArrayBufferWriter<byte> _buffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _serializer = PocoTimeSpanBenchmarkRecord.CreateAvroSerializer(
            new AvroSchemaRegistrySerializerBenchmarks.BenchmarkSchemaRegistryClient());
        _value = new PocoTimeSpanBenchmarkRecord { Value = TimeSpan.FromHours(12) };
        _buffer = new ArrayBufferWriter<byte>(32);
        SerializeTimeSpan();
    }

    [GlobalCleanup]
    public async ValueTask Cleanup() => await _serializer.DisposeAsync().ConfigureAwait(false);

    [Benchmark]
    public void SerializeTimeSpan()
    {
        _buffer.Clear();
        _serializer.Serialize(_value, ref _buffer, _context);
    }
}

[AvroRecord(Name = "PocoBenchmarkDecimalRecord", Namespace = "Dekaf.Benchmarks.Benchmarks.Unit")]
public sealed partial class PocoBenchmarkDecimalRecord
{
    [AvroField(Name = "amount", Precision = 9, Scale = 2)]
    public decimal Amount { get; init; }
}

[AvroRecord(Name = "PocoWriterUnionBenchmarkRecord", Namespace = "Dekaf.Benchmarks.Benchmarks.Unit")]
public readonly partial record struct PocoWriterUnionBenchmarkRecord
{
    [AvroField(Name = "value")]
    public long Value { get; init; }
}

[AvroRecord(Name = "PocoCollectionBenchmarkRecord", Namespace = "Dekaf.Benchmarks.Benchmarks.Unit")]
public sealed partial class PocoCollectionBenchmarkRecord
{
    public required int[] Values { get; init; }
}

[AvroRecord(Name = "PocoReferenceCollectionBenchmarkRecord", Namespace = "Dekaf.Benchmarks.Benchmarks.Unit")]
public sealed partial class PocoReferenceCollectionBenchmarkRecord
{
    public required string[] Values { get; init; }
}

[AvroRecord(Name = "PocoSkipBenchmarkRecord", Namespace = "Dekaf.Benchmarks.Benchmarks.Unit")]
public sealed partial class PocoSkipBenchmarkRecord
{
    public int Id { get; init; }
}

public sealed class PocoBenchmarkSpecificRecord : ISpecificRecord
{
    public static readonly AvroSchema _SCHEMA =
        AvroSchema.Parse(AvroPocoSchemaRegistryDeserializationBenchmarks.SchemaJson);

    public int id { get; private set; }
    public string name { get; private set; } = string.Empty;
    public AvroSchema Schema => _SCHEMA;

    public object Get(int fieldPos) => fieldPos switch
    {
        0 => id,
        1 => name,
        _ => throw new ArgumentOutOfRangeException(nameof(fieldPos))
    };

    public void Put(int fieldPos, object fieldValue)
    {
        switch (fieldPos)
        {
            case 0:
                id = (int)fieldValue;
                break;
            case 1:
                name = (string)fieldValue;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fieldPos));
        }
    }
}

[AvroRecord(Name = "RepresentativePocoRecord", Namespace = "Dekaf.Benchmarks")]
public sealed partial class RepresentativePocoRecord
{
    public int Id { get; init; }
    public required string Name { get; init; }

    [AvroField(DefaultJson = "null")]
    public string? Note { get; init; }

    public RepresentativePocoStatus Status { get; init; }
    public required int[] Scores { get; init; }
    public required List<string> Tags { get; init; }
    public required Dictionary<string, long> Totals { get; init; }
    public required RepresentativePocoAddress Address { get; init; }
    public DateTime Created { get; init; }
    public Guid CorrelationId { get; init; }

    [AvroField(Precision = 10, Scale = 2)]
    public decimal Amount { get; init; }
}

[AvroRecord(Name = "RepresentativePocoAddress", Namespace = "Dekaf.Benchmarks")]
public sealed partial class RepresentativePocoAddress
{
    public required string City { get; init; }
}

[AvroRecord(Name = "PocoLargeBenchmarkRecord", Namespace = "Dekaf.Benchmarks")]
public sealed partial class PocoLargeBenchmarkRecord
{
    public required int[] Values { get; init; }
}

[AvroRecord(Name = "PocoTimeSpanBenchmarkRecord", Namespace = "Dekaf.Benchmarks")]
public readonly partial record struct PocoTimeSpanBenchmarkRecord
{
    public TimeSpan Value { get; init; }
}

/// <summary>Guards generated collection writers against traversing after buffer overflow.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class AvroPocoCollectionOverflowBenchmarks
{
    private const int ItemCount = 100_000;
    private readonly byte[] _buffer = new byte[256];
    private readonly byte[] _largeBuffer = new byte[2 * 1024 * 1024];
    private readonly PocoOverflowArrayRecord _array = new()
    {
        Values = Enumerable.Range(0, ItemCount).ToArray()
    };
    private readonly PocoOverflowMapRecord _map = new()
    {
        Values = Enumerable.Range(0, ItemCount)
            .ToDictionary(static value => value.ToString(), static value => value)
    };

    [Benchmark]
    public int WriteUndersizedArray()
    {
        var writer = new AvroValueWriter(_buffer);
        PocoOverflowArrayRecord.AvroCodec.Write(ref writer, _array);
        return writer.WrittenCount;
    }

    [Benchmark]
    public int WriteUndersizedMap()
    {
        var writer = new AvroValueWriter(_buffer);
        PocoOverflowMapRecord.AvroCodec.Write(ref writer, _map);
        return writer.WrittenCount;
    }

    [Benchmark]
    public int WriteSizedArray()
    {
        var writer = new AvroValueWriter(_largeBuffer);
        PocoOverflowArrayRecord.AvroCodec.Write(ref writer, _array);
        return writer.WrittenCount;
    }

    [Benchmark]
    public int WriteSizedMap()
    {
        var writer = new AvroValueWriter(_largeBuffer);
        PocoOverflowMapRecord.AvroCodec.Write(ref writer, _map);
        return writer.WrittenCount;
    }
}

[AvroRecord(Name = "PocoOverflowArrayRecord", Namespace = "Dekaf.Benchmarks.Benchmarks.Unit")]
public sealed partial class PocoOverflowArrayRecord
{
    public required int[] Values { get; init; }
}

[AvroRecord(Name = "PocoOverflowMapRecord", Namespace = "Dekaf.Benchmarks.Benchmarks.Unit")]
public sealed partial class PocoOverflowMapRecord
{
    public required Dictionary<string, int> Values { get; init; }
}

/// <summary>Guards skipped zero-width Avro collections against per-item scans.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class AvroPocoZeroWidthSkipBenchmarks
{
    private const string ZeroWidthSchemaJson =
        """
        {"type":"record","name":"PocoZeroWidthSkipBenchmarkRecord","namespace":"Dekaf.Benchmarks.Benchmarks.Unit","fields":[{"name":"Id","type":"int"},{"name":"ignored","type":{"type":"array","items":"null"}}]}
        """;
    private readonly byte[] _zeroWidthPayload = CreateZeroWidthPayload();
    private AvroPocoReadNode _zeroWidthNode = null!;

    [GlobalSetup]
    public void Setup()
    {
        _zeroWidthNode = AvroPocoReaderPlanBuilder
            .Build<PocoZeroWidthSkipBenchmarkRecord, PocoZeroWidthSkipBenchmarkRecord.AvroCodec>(ZeroWidthSchemaJson)
            .GetOperation(1)
            .WriterType;
    }

    [Benchmark]
    public int SkipMillionZeroWidthItems()
    {
        var reader = new AvroValueReader(_zeroWidthPayload);
        reader.Skip(_zeroWidthNode);
        return reader.ReadInt32();
    }

    private static byte[] CreateZeroWidthPayload()
    {
        var payload = new byte[16];
        var writer = new AvroValueWriter(payload);
        writer.WriteBlockCount(1_048_576);
        writer.WriteBlockEnd();
        writer.WriteInt32(42);
        return payload.AsSpan(0, writer.WrittenCount).ToArray();
    }

}

[AvroRecord(Name = "PocoZeroWidthSkipBenchmarkRecord", Namespace = "Dekaf.Benchmarks.Benchmarks.Unit")]
public readonly partial record struct PocoZeroWidthSkipBenchmarkRecord
{
    public int Id { get; init; }
}

public enum RepresentativePocoStatus
{
    Pending,
    Ready,
    Complete
}
