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
    private ProtobufSchemaRegistrySerializer<StringValue> _headerSerializer = null!;
    private ProtobufSchemaRegistrySerializer<StringValue> _validatedSerializer = null!;
    private StringValue _value = null!;
    private StringValue _largeValue = null!;
    private SerializationContext _context;
    private SerializationContext _headerContext;

    [GlobalSetup]
    public void Setup()
    {
        _serializer = new ProtobufSchemaRegistrySerializer<StringValue>(new BenchmarkSchemaRegistryClient());
        _headerSerializer = new ProtobufSchemaRegistrySerializer<StringValue>(
            new BenchmarkSchemaRegistryClient(),
            new ProtobufSerializerConfig { SchemaIdStrategy = SchemaIdSerializerStrategy.Header });
        _validatedSerializer = new ProtobufSchemaRegistrySerializer<StringValue>(
            new BenchmarkSchemaRegistryClient(),
            new ProtobufSerializerConfig
            {
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            });
        _value = new StringValue { Value = "protobuf-benchmark" };
        _largeValue = new StringValue { Value = new string('x', 16 * 1024) };
        _context = new SerializationContext
        {
            Topic = "protobuf-benchmark",
            Component = SerializationComponent.Value
        };
        _headerContext = new SerializationContext
        {
            Topic = "protobuf-benchmark",
            Component = SerializationComponent.Value,
            Headers = new Headers(1)
        };

        var destination = _destination;
        _serializer.Serialize(_value, ref destination, _context);
        destination.Clear();
        _headerSerializer.Serialize(_value, ref destination, _headerContext);
        _headerContext.Headers!.Clear();
        destination.Clear();
        _validatedSerializer.Serialize(_largeValue, ref destination, _context);
        destination.Clear();
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await _serializer.DisposeAsync().ConfigureAwait(false);
        await _headerSerializer.DisposeAsync().ConfigureAwait(false);
        await _validatedSerializer.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark]
    public void SerializeCached()
    {
        _destination.Clear();
        var destination = _destination;
        _serializer.Serialize(_value, ref destination, _context);
    }

    [Benchmark]
    public void SerializeCachedHeader()
    {
        _destination.Clear();
        _headerContext.Headers!.Clear();
        var destination = _destination;
        _headerSerializer.Serialize(_value, ref destination, _headerContext);
    }

    [Benchmark]
    public void SerializeCachedWithInlineValidation()
    {
        _destination.Clear();
        var destination = _destination;
        _validatedSerializer.Serialize(_largeValue, ref destination, _context);
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

        public Task<RegisteredSchema> LookupSchemaAsync(
            string subject,
            Schema schema,
            bool ignoreDeletedSchemas = true,
            bool normalize = false,
            CancellationToken cancellationToken = default) => Task.FromResult(new RegisteredSchema
            {
                Id = 1,
                Guid = "89791762-2336-4186-9674-299b90a802e2",
                Subject = subject,
                Version = 1,
                Schema = schema
            });

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
