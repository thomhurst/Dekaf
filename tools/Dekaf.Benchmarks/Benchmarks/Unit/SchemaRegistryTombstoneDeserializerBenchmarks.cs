using System.Buffers.Binary;
using Avro.Generic;
using Avro.IO;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using AvroSchema = Avro.Schema;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser(displayGenColumns: false)]
public class SchemaRegistryTombstoneDeserializerBenchmarks
{
    private const int SchemaId = 1;
    private const string RecordSchema =
        """
        {
          "type": "record",
          "name": "BenchmarkRecord",
          "fields": [{ "name": "id", "type": "int" }]
        }
        """;

    private AvroSchemaRegistryDeserializer<GenericRecord> _avro = null!;
    private ProtobufSchemaRegistryDeserializer<StringValue> _protobuf = null!;
    private JsonSchemaRegistryDeserializer<string> _json = null!;
    private byte[] _avroWireData = null!;
    private byte[] _protobufWireData = null!;
    private byte[] _jsonWireData = null!;
    private SerializationContext _valueContext;
    private SerializationContext _tombstoneContext;

    [GlobalSetup]
    public async Task Setup()
    {
        var avroRegistry = new BenchmarkSchemaRegistryClient(new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = RecordSchema
        });
        var protobufRegistry = new BenchmarkSchemaRegistryClient(new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = "syntax = \"proto3\";"
        });
        var jsonRegistry = new BenchmarkSchemaRegistryClient(new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}"
        });

        _avro = new AvroSchemaRegistryDeserializer<GenericRecord>(avroRegistry, ownsClient: true);
        _protobuf = new ProtobufSchemaRegistryDeserializer<StringValue>(protobufRegistry, ownsClient: true);
        _json = new JsonSchemaRegistryDeserializer<string>(jsonRegistry, ownsClient: true);

        await _avro.WarmupAsync(SchemaId).ConfigureAwait(false);
        _avroWireData = CreateAvroWireData();
        _protobufWireData = CreateProtobufWireData();
        _jsonWireData = CreateWireData("\"benchmark\""u8);
        _valueContext = new SerializationContext
        {
            Topic = "benchmark-topic",
            Component = SerializationComponent.Value
        };
        _tombstoneContext = new SerializationContext
        {
            Topic = "benchmark-topic",
            Component = SerializationComponent.Value,
            IsNull = true
        };
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _avro.DisposeAsync().ConfigureAwait(false);
        await _protobuf.DisposeAsync().ConfigureAwait(false);
        await _json.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark]
    public GenericRecord AvroNonNull() => _avro.Deserialize(_avroWireData, _valueContext);

    [Benchmark]
    public StringValue ProtobufNonNull() => _protobuf.Deserialize(_protobufWireData, _valueContext);

    [Benchmark]
    public string JsonNonNull() => _json.Deserialize(_jsonWireData, _valueContext);

    [Benchmark]
    public GenericRecord? AvroTombstone() => _avro.Deserialize(ReadOnlyMemory<byte>.Empty, _tombstoneContext);

    [Benchmark]
    public StringValue? ProtobufTombstone() => _protobuf.Deserialize(ReadOnlyMemory<byte>.Empty, _tombstoneContext);

    [Benchmark]
    public string? JsonTombstone() => _json.Deserialize(ReadOnlyMemory<byte>.Empty, _tombstoneContext);

    private static byte[] CreateAvroWireData()
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse(RecordSchema);
        var record = new GenericRecord(schema);
        record.Add("id", 42);
        using var payload = new MemoryStream();
        var writer = new GenericDatumWriter<GenericRecord>(schema);
        var encoder = new BinaryEncoder(payload);
        writer.Write(record, encoder);
        encoder.Flush();
        return CreateWireData(payload.ToArray());
    }

    private static byte[] CreateProtobufWireData()
    {
        var payload = new StringValue { Value = "benchmark" }.ToByteArray();
        var wireData = new byte[6 + payload.Length];
        wireData[0] = 0;
        BinaryPrimitives.WriteInt32BigEndian(wireData.AsSpan(1, 4), SchemaId);
        wireData[5] = 0;
        payload.CopyTo(wireData.AsSpan(6));
        return wireData;
    }

    private static byte[] CreateWireData(ReadOnlySpan<byte> payload)
    {
        var wireData = new byte[5 + payload.Length];
        wireData[0] = 0;
        BinaryPrimitives.WriteInt32BigEndian(wireData.AsSpan(1, 4), SchemaId);
        payload.CopyTo(wireData.AsSpan(5));
        return wireData;
    }

    private sealed class BenchmarkSchemaRegistryClient(Schema schema)
        : ISchemaRegistryClient, ISchemaRegistryCache
    {
        public bool TryGetCachedSchema(int id, out Schema cachedSchema)
        {
            cachedSchema = schema;
            return true;
        }

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(schema);

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

        public void Dispose() { }
    }
}
