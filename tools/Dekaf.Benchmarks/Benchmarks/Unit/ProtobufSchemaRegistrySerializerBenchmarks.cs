using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Google.Protobuf.WellKnownTypes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Protects cached Protobuf Schema Registry serialization from steady-state allocations.
/// Descriptor traversal and registration are deliberately completed during setup.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class ProtobufSchemaRegistrySerializerBenchmarks
{
    private readonly ArrayBufferWriter<byte> _destination = new(256);
    private ProtobufSchemaRegistrySerializer<StringValue> _serializer = null!;
    private StringValue _value = null!;
    private SerializationContext _context;

    [GlobalSetup]
    public void Setup()
    {
        _serializer = new ProtobufSchemaRegistrySerializer<StringValue>(new BenchmarkSchemaRegistryClient());
        _value = new StringValue { Value = "protobuf-benchmark" };
        _context = new SerializationContext
        {
            Topic = "protobuf-benchmark",
            Component = SerializationComponent.Value
        };

        var destination = _destination;
        _serializer.Serialize(_value, ref destination, _context);
    }

    [GlobalCleanup]
    public ValueTask Cleanup() => _serializer.DisposeAsync();

    [Benchmark]
    public void SerializeCached()
    {
        _destination.Clear();
        var destination = _destination;
        _serializer.Serialize(_value, ref destination, _context);
    }

    [Benchmark]
    public ValueTask PrepareCached() => _serializer.PrepareAsync(_value, _context);

    private sealed class BenchmarkSchemaRegistryClient : ISchemaRegistryClient
    {
        public Task<int> RegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<int>> GetVersionsAsync(
            string subject,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> IsCompatibleAsync(
            string subject,
            Schema schema,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(
            string subject,
            bool permanent = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Dispose() { }
    }
}
