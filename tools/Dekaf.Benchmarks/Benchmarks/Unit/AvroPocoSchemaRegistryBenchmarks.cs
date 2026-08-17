using System.Buffers;
using System.Buffers.Binary;
using Avro.Generic;
using Avro.IO;
using Avro.Specific;
using BenchmarkDotNet.Attributes;
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
    private AvroPocoSchemaRegistrySerializer<PocoBenchmarkRecord, PocoBenchmarkRecord.AvroCodec> _poco = null!;
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
        _specific.Serialize(_specificValue, ref _buffer, _context);
        _buffer.Clear();
        _generic.Serialize(_genericValue, ref _buffer, _context);
        _buffer.Clear();
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await _poco.DisposeAsync().ConfigureAwait(false);
        await _specific.DisposeAsync().ConfigureAwait(false);
        await _generic.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(Baseline = true, Description = "Serialize generated POCO")]
    public void SerializePoco()
    {
        _buffer.Clear();
        _poco.Serialize(_pocoValue, ref _buffer, _context);
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
    private AvroSchemaRegistryDeserializer<PocoBenchmarkSpecificRecord> _specific = null!;
    private AvroSchemaRegistryDeserializer<GenericRecord> _generic = null!;
    private AvroPocoSchemaRegistryDeserializer<PocoBenchmarkDecimalRecord, PocoBenchmarkDecimalRecord.AvroCodec>
        _decimalPoco = null!;
    private byte[] _wireData = null!;
    private byte[] _decimalWireData = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var registrySchema = new global::Dekaf.SchemaRegistry.Schema
        {
            SchemaType = global::Dekaf.SchemaRegistry.SchemaType.Avro,
            SchemaString = SchemaJson
        };
        _poco = PocoBenchmarkRecord.CreateAvroDeserializer(new BenchmarkSchemaRegistryClient(registrySchema));
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
        _wireData = CreateWireData();
        _decimalWireData = [0, 0, 0, 0, SchemaId, 0x04, 0x30, 0x39];

        await _poco.WarmupAsync(SchemaId).ConfigureAwait(false);
        await _specific.WarmupAsync(SchemaId).ConfigureAwait(false);
        await _generic.WarmupAsync(SchemaId).ConfigureAwait(false);
        await _decimalPoco.WarmupAsync(SchemaId).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await _poco.DisposeAsync().ConfigureAwait(false);
        await _specific.DisposeAsync().ConfigureAwait(false);
        await _generic.DisposeAsync().ConfigureAwait(false);
        await _decimalPoco.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(Baseline = true, Description = "Deserialize generated POCO")]
    public PocoBenchmarkRecord DeserializePoco() => _poco.Deserialize(_wireData, _context);

    [Benchmark(Description = "Deserialize SpecificRecord")]
    public PocoBenchmarkSpecificRecord DeserializeSpecificRecord() => _specific.Deserialize(_wireData, _context);

    [Benchmark(Description = "Deserialize GenericRecord")]
    public GenericRecord DeserializeGenericRecord() => _generic.Deserialize(_wireData, _context);

    [Benchmark(Description = "Deserialize generated decimal POCO")]
    public PocoBenchmarkDecimalRecord DeserializeDecimalPoco() =>
        _decimalPoco.Deserialize(_decimalWireData, _context);

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

    private sealed class BenchmarkSchemaRegistryClient(global::Dekaf.SchemaRegistry.Schema schema)
        : global::Dekaf.SchemaRegistry.ISchemaRegistryClient
    {
        public Task<global::Dekaf.SchemaRegistry.Schema> GetSchemaAsync(
            int id,
            CancellationToken cancellationToken = default) => Task.FromResult(schema);

        public Task<int> RegisterSchemaAsync(
            string subject,
            global::Dekaf.SchemaRegistry.Schema candidate,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<global::Dekaf.SchemaRegistry.RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
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
}

[AvroRecord(Name = "PocoBenchmarkSpecificRecord", Namespace = "Dekaf.Benchmarks.Benchmarks.Unit")]
public sealed partial class PocoBenchmarkRecord
{
    [AvroField(Name = "id", Order = 0)]
    public int Id { get; init; }

    [AvroField(Name = "name", Order = 1)]
    public required string Name { get; init; }
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
    private PocoLargeBenchmarkRecord _value = null!;
    private ArrayBufferWriter<byte> _buffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _serializer = PocoLargeBenchmarkRecord.CreateAvroSerializer(
            new AvroSchemaRegistrySerializerBenchmarks.BenchmarkSchemaRegistryClient());
        _value = new PocoLargeBenchmarkRecord { Values = new int[PayloadLength] };
        _buffer = new ArrayBufferWriter<byte>(PayloadLength + 16);
        _serializer.Serialize(_value, ref _buffer, _context);
        _buffer.Clear();
    }

    [GlobalCleanup]
    public async ValueTask Cleanup() => await _serializer.DisposeAsync().ConfigureAwait(false);

    [Benchmark]
    public void SerializeLargePayload()
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

public enum RepresentativePocoStatus
{
    Pending,
    Ready,
    Complete
}
