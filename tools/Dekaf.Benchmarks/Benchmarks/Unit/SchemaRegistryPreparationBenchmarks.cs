using System.Buffers;
using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Protects generic and JSON Schema Registry cached preparation and serialization.
/// Registry resolution is completed during setup.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class SchemaRegistryPreparationBenchmarks
{
    private ArrayBufferWriter<byte> _genericDestination = new(64);
    private ArrayBufferWriter<byte> _jsonDestination = new(128);
    private SchemaRegistrySerializer<int> _genericSerializer = null!;
    private JsonSchemaRegistrySerializer<BenchmarkPayload> _jsonSerializer = null!;
    private SerializationContext _context;
    private BenchmarkPayload _jsonValue = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var registry = new BenchmarkSchemaRegistryClient();
        var genericSchema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{\"type\":\"integer\"}"
        };
        _genericSerializer = new SchemaRegistrySerializer<int>(
            registry,
            static (value, writer) =>
            {
                var span = writer.GetSpan(sizeof(int));
                BinaryPrimitives.WriteInt32BigEndian(span, value);
                writer.Advance(sizeof(int));
            },
            () => genericSchema);
        _jsonSerializer = new JsonSchemaRegistrySerializer<BenchmarkPayload>(
            registry,
            "{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"integer\"}}}");
        _jsonValue = new BenchmarkPayload { Id = 42 };
        _context = new SerializationContext
        {
            Topic = "schema-preparation-benchmark",
            Component = SerializationComponent.Value
        };

        await _genericSerializer.PrepareAsync(42, _context).ConfigureAwait(false);
        await _jsonSerializer.PrepareAsync(_jsonValue, _context).ConfigureAwait(false);
        _genericSerializer.Serialize(42, ref _genericDestination, _context);
        _jsonSerializer.Serialize(_jsonValue, ref _jsonDestination, _context);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _genericSerializer.DisposeAsync().ConfigureAwait(false);
        await _jsonSerializer.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark]
    public ValueTask PrepareGenericCached() => _genericSerializer.PrepareAsync(42, _context);

    [Benchmark]
    public void SerializeGenericPrepared()
    {
        _genericDestination.Clear();
        _genericSerializer.Serialize(42, ref _genericDestination, _context);
    }

    [Benchmark]
    public ValueTask PrepareJsonCached() => _jsonSerializer.PrepareAsync(_jsonValue, _context);

    [Benchmark]
    public void SerializeJsonPrepared()
    {
        _jsonDestination.Clear();
        _jsonSerializer.Serialize(_jsonValue, ref _jsonDestination, _context);
    }

    private sealed class BenchmarkPayload
    {
        public int Id { get; init; }
    }

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

        public void Dispose()
        {
        }
    }
}
