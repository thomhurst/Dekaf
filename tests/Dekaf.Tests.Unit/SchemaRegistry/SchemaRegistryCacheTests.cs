using System.Buffers;
using System.Collections.Concurrent;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaRegistryCacheTests
{
    /// <summary>
    /// A mock registry client that counts how many times GetOrRegisterSchemaAsync is called per subject.
    /// </summary>
    private sealed class CountingSchemaRegistryClient : ISchemaRegistryClient
    {
        private readonly ConcurrentDictionary<string, int> _callCounts = new();
        private int _nextId = 1;

        /// <summary>
        /// Returns the number of times GetOrRegisterSchemaAsync was called for a given subject.
        /// </summary>
        public int GetCallCount(string subject) =>
            _callCounts.GetValueOrDefault(subject, 0);

        /// <summary>
        /// Returns the total number of GetOrRegisterSchemaAsync calls across all subjects.
        /// </summary>
        public int TotalCallCount => _callCounts.Values.Sum();

        public Task<int> GetOrRegisterSchemaAsync(string subject, Schema schema, CancellationToken cancellationToken = default)
        {
            _callCounts.AddOrUpdate(subject, 1, static (_, count) => count + 1);
            var id = Interlocked.Increment(ref _nextId);
            return Task.FromResult(id);
        }

        public Task<int> RegisterSchemaAsync(string subject, Schema schema, CancellationToken cancellationToken = default)
            => Task.FromResult(Interlocked.Increment(ref _nextId));

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(string subject, string version = "latest", CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<int>> GetVersionsAsync(string subject, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> IsCompatibleAsync(string subject, Schema schema, string version = "latest", CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(string subject, bool permanent = false, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public void Dispose() { }
    }

    [Test]
    public async Task Serializer_CachesSchemaId_AcrossMultipleSubjects()
    {
        // Arrange
        var registry = new CountingSchemaRegistryClient();
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = """{ "type": "string" }""" };

        await using var serializer = new SchemaRegistrySerializer<string>(
            registry,
            serialize: static (value, writer) =>
            {
                var byteCount = System.Text.Encoding.UTF8.GetByteCount(value);
                System.Text.Encoding.UTF8.GetBytes(value, writer.GetSpan(byteCount));
                writer.Advance(byteCount);
            },
            getSchema: _ => schema);

        var contextA = new SerializationContext { Topic = "topic-a", Component = SerializationComponent.Value };
        var contextB = new SerializationContext { Topic = "topic-b", Component = SerializationComponent.Value };

        // Act — serialize to topic-a twice, topic-b twice
        var buffer1 = new ArrayBufferWriter<byte>();
        serializer.Serialize("msg-1", ref buffer1, contextA);

        var buffer2 = new ArrayBufferWriter<byte>();
        serializer.Serialize("msg-2", ref buffer2, contextA);

        var buffer3 = new ArrayBufferWriter<byte>();
        serializer.Serialize("msg-3", ref buffer3, contextB);

        var buffer4 = new ArrayBufferWriter<byte>();
        serializer.Serialize("msg-4", ref buffer4, contextB);

        // Assert — exactly one registry call per subject (2 total, not 4)
        await Assert.That(registry.GetCallCount("topic-a-value")).IsEqualTo(1);
        await Assert.That(registry.GetCallCount("topic-b-value")).IsEqualTo(1);
        await Assert.That(registry.TotalCallCount).IsEqualTo(2);
    }

    [Test]
    public async Task Client_CacheSchema_ClearsWhenMaxCachedSchemasReached()
    {
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "http://localhost:8081",
            MaxCachedSchemas = 2
        });

        client.CacheSchema(1, "subject-1", NewSchema(1));
        client.CacheSchema(2, "subject-2", NewSchema(2));

        await Assert.That(client.CachedSchemaByIdCount).IsEqualTo(2);
        await Assert.That(client.CachedSchemaIdCount).IsEqualTo(2);

        client.CacheSchema(3, "subject-3", NewSchema(3));

        await Assert.That(client.CachedSchemaByIdCount).IsEqualTo(1);
        await Assert.That(client.CachedSchemaIdCount).IsEqualTo(1);
    }

    [Test]
    public async Task Client_CacheSchema_MaxCachedSchemasZero_DisablesCaching()
    {
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "http://localhost:8081",
            MaxCachedSchemas = 0
        });

        client.CacheSchema(1, "subject-1", NewSchema(1));

        await Assert.That(client.CachedSchemaByIdCount).IsEqualTo(0);
        await Assert.That(client.CachedSchemaIdCount).IsEqualTo(0);
    }

    [Test]
    public async Task Client_CreateHttpHandler_SetsPooledConnectionLifetime()
    {
        using var handler = SchemaRegistryClient.CreateHttpHandler();

        await Assert.That(handler.PooledConnectionLifetime).IsEqualTo(TimeSpan.FromMinutes(2));
    }

    private static Schema NewSchema(int id) => new()
    {
        SchemaType = SchemaType.Json,
        SchemaString = $$"""{ "type": "string", "title": "schema-{{id}}" }"""
    };
}
