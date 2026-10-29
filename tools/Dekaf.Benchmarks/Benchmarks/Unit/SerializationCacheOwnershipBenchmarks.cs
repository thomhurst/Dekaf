using System.Buffers;
using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures warmed, non-reentrant cache ownership per message. Preparation is outside measurement.
/// </summary>
[MemoryDiagnoser]
public class SerializationCacheOwnershipBenchmarks
{
    private readonly ArrayBufferWriter<byte> _destination = new(256);
    private readonly Payload _value = new(42);
    private readonly SerializationContext _context = new() { Topic = "cache-ownership", Component = SerializationComponent.Value };
    private readonly Dekaf.Serialization.Json.JsonSerializer<Payload> _plain = new();
    private SchemaRegistrySerializer<int> _custom = null!;
    private SchemaRegistrySerializer<Payload> _customReference = null!;
    private JsonSchemaRegistrySerializer<Payload> _json = null!;
    private IAsyncSerializerPreparationAdmission<int> _preparedCustom = null!;
    private IAsyncSerializerPreparationAdmission<Payload> _preparedCustomReference = null!;
    private IAsyncSerializerPreparationAdmission<Payload> _preparedJson = null!;
    private SerializerPreparationAdmission _customAdmission;
    private SerializerPreparationAdmission _customReferenceAdmission;
    private SerializerPreparationAdmission _jsonAdmission;

    [GlobalSetup]
    public async Task Setup()
    {
        var registry = new AvroSchemaRegistrySerializerBenchmarks.BenchmarkSchemaRegistryClient();
        _custom = new SchemaRegistrySerializer<int>(registry, static (value, writer) =>
        {
            BinaryPrimitives.WriteInt32BigEndian(writer.GetSpan(4), value);
            writer.Advance(4);
        }, static () => new Schema { SchemaType = SchemaType.Json, SchemaString = "{}" });
        _customReference = new SchemaRegistrySerializer<Payload>(registry, static (value, writer) =>
        {
            BinaryPrimitives.WriteInt32BigEndian(writer.GetSpan(4), value.Id);
            writer.Advance(4);
        }, static () => new Schema { SchemaType = SchemaType.Json, SchemaString = "{}" });
        _json = new JsonSchemaRegistrySerializer<Payload>(registry, "{}");
        _preparedCustom = (IAsyncSerializerPreparationAdmission<int>)_custom;
        _preparedCustomReference = (IAsyncSerializerPreparationAdmission<Payload>)_customReference;
        _preparedJson = (IAsyncSerializerPreparationAdmission<Payload>)_json;
        _customAdmission = await _preparedCustom.PrepareForSerializationAsync(42, _context);
        _customReferenceAdmission = await _preparedCustomReference.PrepareForSerializationAsync(_value, _context);
        _jsonAdmission = await _preparedJson.PrepareForSerializationAsync(_value, _context);
    }

    [Benchmark]
    public int PlainJson()
    {
        _destination.ResetWrittenCount();
        var destination = _destination;
        _plain.Serialize(_value, ref destination, _context);
        return _destination.WrittenCount;
    }

    [Benchmark]
    public int CustomRegistry()
    {
        _destination.ResetWrittenCount();
        var destination = _destination;
        _custom.Serialize(42, ref destination, _context);
        return destination.WrittenCount;
    }

    [Benchmark]
    public int CustomRegistryPrepared()
    {
        _destination.ResetWrittenCount();
        var destination = _destination;
        _preparedCustom.SerializePrepared(42, ref destination, _context, in _customAdmission);
        return destination.WrittenCount;
    }

    // Reference types share generated generic code; retain the value-type case above as well.
    [Benchmark]
    public int CustomRegistryPreparedReference()
    {
        _destination.ResetWrittenCount();
        var destination = _destination;
        _preparedCustomReference.SerializePrepared(_value, ref destination, _context, in _customReferenceAdmission);
        return destination.WrittenCount;
    }

    [Benchmark]
    public int JsonRegistry()
    {
        _destination.ResetWrittenCount();
        var destination = _destination;
        _json.Serialize(_value, ref destination, _context);
        return destination.WrittenCount;
    }

    [Benchmark]
    public int JsonRegistryPrepared()
    {
        _destination.ResetWrittenCount();
        var destination = _destination;
        _preparedJson.SerializePrepared(_value, ref destination, _context, in _jsonAdmission);
        return destination.WrittenCount;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _custom.DisposeAsync();
        await _customReference.DisposeAsync();
        await _json.DisposeAsync();
    }

    public sealed record Payload(int Id);
}
