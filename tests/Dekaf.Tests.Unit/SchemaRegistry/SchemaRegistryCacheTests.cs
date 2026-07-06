using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Avro.Generic;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using NSubstitute;
using AvroSchema = Avro.Schema;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaRegistryCacheTests
{
    private static readonly JsonSerializerOptions CamelCaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// A mock registry client that counts how many times GetOrRegisterSchemaAsync is called per subject.
    /// </summary>
    private sealed class CountingSchemaRegistryClient : ISchemaRegistryClient
    {
        private readonly ConcurrentDictionary<string, int> _callCounts = new();
        private readonly ConcurrentDictionary<string, int> _idsBySubject = new();
        private int _nextId;

        /// <summary>
        /// Returns the number of times GetOrRegisterSchemaAsync was called for a given subject.
        /// </summary>
        public int GetCallCount(string subject) =>
            _callCounts.GetValueOrDefault(subject, 0);

        /// <summary>
        /// Returns the total number of GetOrRegisterSchemaAsync calls across all subjects.
        /// </summary>
        public int TotalCallCount => _callCounts.Values.Sum();

        public int GetSchemaId(string subject) =>
            _idsBySubject[subject];

        public Task<int> GetOrRegisterSchemaAsync(string subject, Schema schema, CancellationToken cancellationToken = default)
        {
            _callCounts.AddOrUpdate(subject, 1, static (_, count) => count + 1);
            var id = _idsBySubject.GetOrAdd(subject, static (_, state) => Interlocked.Increment(ref state._nextId), this);
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

    private sealed class NonCachingSchemaRegistryClient(Schema schema) : ISchemaRegistryClient
    {
        public int GetSchemaCallCount { get; private set; }

        public Task<int> GetOrRegisterSchemaAsync(string subject, Schema schema, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> RegisterSchemaAsync(string subject, Schema schema, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default)
        {
            GetSchemaCallCount++;
            return Task.FromResult(schema);
        }

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(string subject, string version = "latest", CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<int>> GetVersionsAsync(string subject, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> IsCompatibleAsync(string subject, Schema schema, string version = "latest", CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(string subject, bool permanent = false, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public void Dispose() { }
    }

    private sealed class CountingSubjectNameStrategy : ISubjectNameStrategy
    {
        private int _callCount;

        public int CallCount => _callCount;

        public string GetSubjectName(string topic, string? recordType, bool isKey)
        {
            Interlocked.Increment(ref _callCount);
            return $"{topic}-{(isKey ? "key" : "value")}";
        }
    }

    private sealed class ReplacingRuleExecutor(
        byte[]? serializedPayload = null,
        byte[]? deserializedPayload = null) : ISchemaRegistryRuleExecutor
    {
        public SchemaRegistryRuleContext? SerializeContext { get; private set; }
        public SchemaRegistryRuleContext? DeserializeContext { get; private set; }

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
        {
            SerializeContext = context;
            return serializedPayload ?? payload;
        }

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
        {
            DeserializeContext = context;
            return deserializedPayload ?? payload;
        }
    }

    private static SerializationContext CreateContext(string topic = "topic", bool isKey = false) =>
        new()
        {
            Topic = topic,
            Component = isKey ? SerializationComponent.Key : SerializationComponent.Value
        };

    [Test]
    public async Task SubjectSchemaIdCache_StopsAddingEntriesAtMaxCachedEntries()
    {
        var cache = new SubjectSchemaIdCache();

        for (var i = 0; i < SubjectSchemaIdCache.MaxCachedEntries + 10; i++)
        {
            _ = cache.GetOrAdd(
                $"topic-{i}",
                isKey: false,
                state: 0,
                static (_, topic, isKey) => topic + (isKey ? "-key" : "-value"),
                static (_, subject) => new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(subject.Length, null));
        }

        await Assert.That(cache.CachedEntryCount).IsEqualTo(SubjectSchemaIdCache.MaxCachedEntries);
    }

    private sealed record JsonPayload(int Id, string Name);

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
    public async Task Serializer_CachesSubjectAndSchemaId_ForSameContext()
    {
        var registry = new CountingSchemaRegistryClient();
        var strategy = new CountingSubjectNameStrategy();
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = """{ "type": "string" }""" };
        var schemaFactoryCalls = 0;

        await using var serializer = new SchemaRegistrySerializer<string>(
            registry,
            serialize: static (value, writer) =>
            {
                var byteCount = Encoding.UTF8.GetByteCount(value);
                Encoding.UTF8.GetBytes(value, writer.GetSpan(byteCount));
                writer.Advance(byteCount);
            },
            getSchema: _ =>
            {
                schemaFactoryCalls++;
                return schema;
            },
            customSubjectNameStrategy: strategy);

        var context = CreateContext();

        var buffer1 = new ArrayBufferWriter<byte>();
        serializer.Serialize("msg-1", ref buffer1, context);

        var buffer2 = new ArrayBufferWriter<byte>();
        serializer.Serialize("msg-2", ref buffer2, context);

        await Assert.That(strategy.CallCount).IsEqualTo(1);
        await Assert.That(schemaFactoryCalls).IsEqualTo(1);
        await Assert.That(registry.GetCallCount("topic-value")).IsEqualTo(1);
    }

    [Test]
    public async Task Serializer_RuleExecutor_TransformsSerializedPayload()
    {
        var registry = new CountingSchemaRegistryClient();
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = """{ "type": "string" }""" };
        var executor = new ReplacingRuleExecutor(serializedPayload: "encrypted"u8.ToArray());
        var schemaFactoryCalls = 0;

        await using var serializer = new SchemaRegistrySerializer<string>(
            registry,
            serialize: static (value, writer) =>
            {
                var byteCount = Encoding.UTF8.GetByteCount(value);
                Encoding.UTF8.GetBytes(value, writer.GetSpan(byteCount));
                writer.Advance(byteCount);
            },
            getSchema: _ =>
            {
                schemaFactoryCalls++;
                return schema;
            },
            ruleExecutor: executor);

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize("plain", ref buffer, CreateContext());

        var buffer2 = new ArrayBufferWriter<byte>();
        serializer.Serialize("plain-again", ref buffer2, CreateContext());

        await Assert.That(Encoding.UTF8.GetString(buffer.WrittenSpan.Slice(5))).IsEqualTo("encrypted");
        await Assert.That(Encoding.UTF8.GetString(buffer2.WrittenSpan.Slice(5))).IsEqualTo("encrypted");
        await Assert.That(executor.SerializeContext).IsNotNull();
        await Assert.That(executor.SerializeContext!.Subject).IsEqualTo("topic-value");
        await Assert.That(executor.SerializeContext.PayloadFormat).IsEqualTo(SchemaRegistryPayloadFormat.Custom);
        await Assert.That(executor.SerializeContext.SchemaId).IsEqualTo(registry.GetSchemaId("topic-value"));
        await Assert.That(executor.SerializeContext.Schema).IsSameReferenceAs(schema);
        await Assert.That(schemaFactoryCalls).IsEqualTo(1);
        await Assert.That(registry.GetCallCount("topic-value")).IsEqualTo(1);
    }

    [Test]
    public async Task Serializer_NormalizeSchemas_PassesNormalizeToRegistry()
    {
        var registry = Substitute.For<ISchemaRegistryClient>();
        registry.GetOrRegisterSchemaAsync(
                Arg.Any<string>(),
                Arg.Any<Schema>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(123));
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = """{ "type": "string" }""" };

        await using var serializer = new SchemaRegistrySerializer<string>(
            registry,
            serialize: static (value, writer) =>
            {
                var byteCount = Encoding.UTF8.GetByteCount(value);
                Encoding.UTF8.GetBytes(value, writer.GetSpan(byteCount));
                writer.Advance(byteCount);
            },
            getSchema: _ => schema,
            normalizeSchemas: true);

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize("msg", ref buffer, CreateContext());

        await registry.Received(1).GetOrRegisterSchemaAsync(
            Arg.Any<string>(),
            Arg.Any<Schema>(),
            true,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Deserializer_RuleExecutor_TransformsDeserializedPayload()
    {
        var registry = new MockSchemaRegistryClient();
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = """{ "type": "string" }""" };
        var schemaId = await registry.RegisterSchemaAsync("topic-value", schema);
        var cipherText = "encrypted"u8.ToArray();
        var wireBytes = new byte[5 + cipherText.Length];
        wireBytes[0] = 0;
        BinaryPrimitives.WriteInt32BigEndian(wireBytes.AsSpan(1, 4), schemaId);
        cipherText.CopyTo(wireBytes.AsSpan(5));

        var executor = new ReplacingRuleExecutor(deserializedPayload: "plain"u8.ToArray());
        await using var deserializer = SchemaRegistryDeserializer.Create(
            registry,
            static (ReadOnlyMemory<byte> payload, Schema _) => Encoding.UTF8.GetString(payload.Span),
            ruleExecutor: executor);

        var result = deserializer.Deserialize(wireBytes, CreateContext());

        await Assert.That(result).IsEqualTo("plain");
        await Assert.That(executor.DeserializeContext).IsNotNull();
        await Assert.That(executor.DeserializeContext!.PayloadFormat).IsEqualTo(SchemaRegistryPayloadFormat.Custom);
        await Assert.That(executor.DeserializeContext.SchemaId).IsEqualTo(schemaId);
        await Assert.That(executor.DeserializeContext.Schema).IsSameReferenceAs(schema);
    }

    [Test]
    public async Task JsonSerializer_CachesSubjectAndSchemaId_ForSameContext()
    {
        var registry = new CountingSchemaRegistryClient();
        var strategy = new CountingSubjectNameStrategy();

        await using var serializer = new JsonSchemaRegistrySerializer<string>(
            registry,
            """{ "type": "string" }""",
            strategy);

        var context = CreateContext();

        var buffer1 = new ArrayBufferWriter<byte>();
        serializer.Serialize("msg-1", ref buffer1, context);

        var buffer2 = new ArrayBufferWriter<byte>();
        serializer.Serialize("msg-2", ref buffer2, context);

        await Assert.That(strategy.CallCount).IsEqualTo(1);
        await Assert.That(registry.GetCallCount("topic-value")).IsEqualTo(1);
    }

    [Test]
    public async Task JsonSerializer_NormalizeSchemas_PassesNormalizeToRegistry()
    {
        var registry = Substitute.For<ISchemaRegistryClient>();
        registry.GetOrRegisterSchemaAsync(
                Arg.Any<string>(),
                Arg.Any<Schema>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(123));

        await using var serializer = new JsonSchemaRegistrySerializer<string>(
            registry,
            """{ "type": "string" }""",
            normalizeSchemas: true);

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize("msg", ref buffer, CreateContext());

        await registry.Received(1).GetOrRegisterSchemaAsync(
            Arg.Any<string>(),
            Arg.Any<Schema>(),
            true,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task JsonSerializer_UsesCorrectSchemaId_ConcurrentlyAcrossTopics()
    {
        var registry = new CountingSchemaRegistryClient();

        await using var serializer = new JsonSchemaRegistrySerializer<string>(
            registry,
            """{ "type": "string" }""");

        var topics = Enumerable.Range(0, 32)
            .Select(static i => $"topic-{i}")
            .ToArray();
        var results = new ConcurrentBag<(string Topic, int SchemaId)>();

        Parallel.For(0, 4096, i =>
        {
            var topic = topics[i % topics.Length];
            var buffer = new ArrayBufferWriter<byte>();

            serializer.Serialize($"msg-{i}", ref buffer, CreateContext(topic));

            var schemaId = BinaryPrimitives.ReadInt32BigEndian(buffer.WrittenSpan.Slice(1, 4));
            results.Add((topic, schemaId));
        });

        foreach (var result in results)
        {
            var expectedSchemaId = registry.GetSchemaId(result.Topic + "-value");
            await Assert.That(result.SchemaId).IsEqualTo(expectedSchemaId);
        }
    }

    [Test]
    public async Task AvroSerializer_CachesSubjectAndSchemaId_ForSameContext()
    {
        var registry = new CountingSchemaRegistryClient();
        var strategy = new CountingSubjectNameStrategy();
        var config = new AvroSerializerConfig { CustomSubjectNameStrategy = strategy };
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(registry, config);
        var record = CreateAvroRecord();
        var context = CreateContext();

        var buffer1 = new ArrayBufferWriter<byte>();
        serializer.Serialize(record, ref buffer1, context);

        var buffer2 = new ArrayBufferWriter<byte>();
        serializer.Serialize(record, ref buffer2, context);

        await Assert.That(strategy.CallCount).IsEqualTo(1);
        await Assert.That(registry.GetCallCount("topic-value")).IsEqualTo(1);
    }

    [Test]
    public async Task ProtobufSerializer_CachesSubjectAndSchemaId_ForSameContext()
    {
        var registry = new CountingSchemaRegistryClient();
        var strategy = new CountingSubjectNameStrategy();
        var config = new ProtobufSerializerConfig { CustomSubjectNameStrategy = strategy };
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(registry, config);
        var message = new TestMessage { Id = 1, Name = "Test", Value = 3.14 };
        var context = CreateContext();

        var buffer1 = new ArrayBufferWriter<byte>();
        serializer.Serialize(message, ref buffer1, context);

        var buffer2 = new ArrayBufferWriter<byte>();
        serializer.Serialize(message, ref buffer2, context);

        await Assert.That(strategy.CallCount).IsEqualTo(1);
        await Assert.That(registry.GetCallCount("topic-value")).IsEqualTo(1);
    }

    [Test]
    public async Task JsonSerializer_WritesCorrectWireFormat()
    {
        var registry = new CountingSchemaRegistryClient();
        await using var serializer = new JsonSchemaRegistrySerializer<JsonPayload>(
            registry,
            """
            {
                "type": "object",
                "properties": {
                    "id": { "type": "integer" },
                    "name": { "type": "string" }
                }
            }
            """);
        var payload = new JsonPayload(7, "Test");
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(
            payload,
            ref buffer,
            new SerializationContext { Topic = "topic", Component = SerializationComponent.Value });

        var written = buffer.WrittenMemory.ToArray();
        var schemaId = BinaryPrimitives.ReadInt32BigEndian(written.AsSpan(1, 4));
        var actualPayload = written.AsSpan(5).ToArray();

        await Assert.That(written[0]).IsEqualTo((byte)0);
        await Assert.That(schemaId).IsEqualTo(registry.GetSchemaId("topic-value"));

        var expectedPayload = JsonSerializer.SerializeToUtf8Bytes(payload, CamelCaseJsonOptions);
        await Assert.That(actualPayload).IsEquivalentTo(expectedPayload);
        await Assert.That(registry.GetCallCount("topic-value")).IsEqualTo(1);
    }

    [Test]
    public async Task JsonSerializer_RuleExecutor_TransformsPayload()
    {
        var registry = new CountingSchemaRegistryClient();
        var strategy = new CountingSubjectNameStrategy();
        var executor = new ReplacingRuleExecutor(serializedPayload: "encrypted-json"u8.ToArray());
        await using var serializer = new JsonSchemaRegistrySerializer<JsonPayload>(
            registry,
            """
            {
                "type": "object",
                "properties": {
                    "id": { "type": "integer" },
                    "name": { "type": "string" }
                }
            }
            """,
            strategy,
            ruleExecutor: executor);

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(new JsonPayload(7, "Test"), ref buffer, CreateContext());

        var buffer2 = new ArrayBufferWriter<byte>();
        serializer.Serialize(new JsonPayload(8, "Test"), ref buffer2, CreateContext());

        await Assert.That(Encoding.UTF8.GetString(buffer.WrittenSpan.Slice(5))).IsEqualTo("encrypted-json");
        await Assert.That(Encoding.UTF8.GetString(buffer2.WrittenSpan.Slice(5))).IsEqualTo("encrypted-json");
        await Assert.That(executor.SerializeContext).IsNotNull();
        await Assert.That(executor.SerializeContext!.PayloadFormat).IsEqualTo(SchemaRegistryPayloadFormat.Json);
        await Assert.That(executor.SerializeContext.Subject).IsEqualTo("topic-value");
        await Assert.That(strategy.CallCount).IsEqualTo(1);
        await Assert.That(registry.GetCallCount("topic-value")).IsEqualTo(1);
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

    [Test]
    public async Task Client_FailsOverToNextUrl_OnRetriableStatus()
    {
        using var handler = new QueueingSchemaRegistryHandler()
            .Enqueue(HttpStatusCode.ServiceUnavailable, "{}")
            .Enqueue(HttpStatusCode.OK, "[]");
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "http://primary:8081, http://secondary:8081"
        }, handler);

        var subjects = await client.GetAllSubjectsAsync();

        await Assert.That(subjects).IsEmpty();
        await Assert.That(handler.RequestUris).Count().IsEqualTo(2);
        await Assert.That(handler.RequestUris[0].Host).IsEqualTo("primary");
        await Assert.That(handler.RequestUris[1].Host).IsEqualTo("secondary");
    }

    [Test]
    public async Task Client_DoesNotFailOver_OnSchemaRegistryClientError()
    {
        using var handler = new QueueingSchemaRegistryHandler()
            .Enqueue(HttpStatusCode.NotFound, """{ "error_code": 40401, "message": "not found" }""")
            .Enqueue(HttpStatusCode.OK, "[]");
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Urls = ["http://primary:8081", "http://secondary:8081"],
            Url = "http://ignored:8081"
        }, handler);

        var exception = await Assert.ThrowsAsync<SchemaRegistryException>(() => client.GetAllSubjectsAsync());

        await Assert.That(exception!.ErrorCode).IsEqualTo(40401);
        await Assert.That(handler.RequestUris).Count().IsEqualTo(1);
        await Assert.That(handler.RequestUris[0].Host).IsEqualTo("primary");
    }

    [Test]
    public async Task Client_AddsNormalizeQuery_WhenConfigured()
    {
        using var handler = new QueueingSchemaRegistryHandler()
            .Enqueue(HttpStatusCode.OK, """{ "id": 42 }""");
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "http://registry:8081",
            NormalizeSchemas = true
        }, handler);

        var schemaId = await client.RegisterSchemaAsync("topic-value", NewSchema(1));

        await Assert.That(schemaId).IsEqualTo(42);
        await Assert.That(handler.RequestUris[0].Query).IsEqualTo("?normalize=true");
    }

    [Test]
    public async Task Client_RegisterSchema_CacheKeyIncludesNormalizeFlag()
    {
        using var handler = new QueueingSchemaRegistryHandler()
            .Enqueue(HttpStatusCode.OK, """{ "id": 41 }""")
            .Enqueue(HttpStatusCode.OK, """{ "id": 42 }""");
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "http://registry:8081"
        }, handler);
        var schema = NewSchema(1);

        var firstId = await client.RegisterSchemaAsync("topic-value", schema, normalize: false);
        var secondId = await client.RegisterSchemaAsync("topic-value", schema, normalize: true);

        await Assert.That(firstId).IsEqualTo(41);
        await Assert.That(secondId).IsEqualTo(42);
        await Assert.That(handler.RequestUris).Count().IsEqualTo(2);
        await Assert.That(handler.RequestUris[0].Query).IsEqualTo("");
        await Assert.That(handler.RequestUris[1].Query).IsEqualTo("?normalize=true");
    }

    [Test]
    public async Task Client_GetOrRegisterSchema_CacheKeyIncludesNormalizeFlag()
    {
        using var handler = new QueueingSchemaRegistryHandler()
            .Enqueue(HttpStatusCode.OK, RegisteredSchemaJson(41))
            .Enqueue(HttpStatusCode.OK, RegisteredSchemaJson(42));
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "http://registry:8081"
        }, handler);
        var schema = NewSchema(1);

        var firstId = await client.GetOrRegisterSchemaAsync("topic-value", schema, normalize: false);
        var secondId = await client.GetOrRegisterSchemaAsync("topic-value", schema, normalize: true);

        await Assert.That(firstId).IsEqualTo(41);
        await Assert.That(secondId).IsEqualTo(42);
        await Assert.That(handler.RequestUris).Count().IsEqualTo(2);
        await Assert.That(handler.RequestUris[0].Query).IsEqualTo("");
        await Assert.That(handler.RequestUris[1].Query).IsEqualTo("?normalize=true");
    }

    [Test]
    public async Task Deserializer_UsesCachedSchema_AndPassesPayloadWithoutCopy()
    {
        var registry = new MockSchemaRegistryClient();
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = """{ "type": "string" }""" };
        var schemaId = await registry.RegisterSchemaAsync("topic-value", schema);
        var payloadBytes = "hello"u8.ToArray();
        var wireBytes = new byte[5 + payloadBytes.Length];
        wireBytes[0] = 0;
        BinaryPrimitives.WriteInt32BigEndian(wireBytes.AsSpan(1, 4), schemaId);
        payloadBytes.CopyTo(wireBytes.AsSpan(5));

        ArraySegment<byte> payloadSegment = default;
        await using var deserializer = SchemaRegistryDeserializer.Create(
            registry,
            (ReadOnlyMemory<byte> payload, Schema _) =>
            {
                MemoryMarshal.TryGetArray(payload, out var segment);
                payloadSegment = segment;
                return Encoding.UTF8.GetString(payload.Span);
            });

        var result = deserializer.Deserialize(
            wireBytes,
            new SerializationContext { Topic = "topic", Component = SerializationComponent.Value });

        await Assert.That(result).IsEqualTo("hello");
        await Assert.That(payloadSegment.Array).IsSameReferenceAs(wireBytes);
        await Assert.That(payloadSegment.Offset).IsEqualTo(5);
        await Assert.That(payloadSegment.Count).IsEqualTo(payloadBytes.Length);
        await Assert.That(registry.TryGetCachedSchemaCallCount).IsEqualTo(1);
        await Assert.That(registry.GetSchemaCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task Deserializer_FallsBackToGetSchema_WhenClientDoesNotExposeCache()
    {
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = """{ "type": "string" }""" };
        var registry = new NonCachingSchemaRegistryClient(schema);
        var payloadBytes = "hello"u8.ToArray();
        var wireBytes = new byte[5 + payloadBytes.Length];
        wireBytes[0] = 0;
        BinaryPrimitives.WriteInt32BigEndian(wireBytes.AsSpan(1, 4), 123);
        payloadBytes.CopyTo(wireBytes.AsSpan(5));

        await using var deserializer = SchemaRegistryDeserializer.Create(
            registry,
            static (ReadOnlyMemory<byte> payload, Schema _) => Encoding.UTF8.GetString(payload.Span));

        var result = deserializer.Deserialize(
            wireBytes,
            new SerializationContext { Topic = "topic", Component = SerializationComponent.Value });

        await Assert.That(result).IsEqualTo("hello");
        await Assert.That(registry.GetSchemaCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task JsonDeserializer_UsesCachedSchema()
    {
        var registry = new MockSchemaRegistryClient();
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = """{ "type": "string" }""" };
        var schemaId = await registry.RegisterSchemaAsync("topic-value", schema);
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes("hello");
        var wireBytes = new byte[5 + payloadBytes.Length];
        wireBytes[0] = 0;
        BinaryPrimitives.WriteInt32BigEndian(wireBytes.AsSpan(1, 4), schemaId);
        payloadBytes.CopyTo(wireBytes.AsSpan(5));

        await using var deserializer = new JsonSchemaRegistryDeserializer<string>(registry);

        var result = deserializer.Deserialize(
            wireBytes,
            new SerializationContext { Topic = "topic", Component = SerializationComponent.Value });

        await Assert.That(result).IsEqualTo("hello");
        await Assert.That(registry.TryGetCachedSchemaCallCount).IsEqualTo(1);
        await Assert.That(registry.GetSchemaCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task JsonDeserializer_RuleExecutor_TransformsPayload()
    {
        var registry = new MockSchemaRegistryClient();
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = """{ "type": "string" }""" };
        var schemaId = await registry.RegisterSchemaAsync("topic-value", schema);
        var cipherText = "encrypted-json"u8.ToArray();
        var wireBytes = new byte[5 + cipherText.Length];
        wireBytes[0] = 0;
        BinaryPrimitives.WriteInt32BigEndian(wireBytes.AsSpan(1, 4), schemaId);
        cipherText.CopyTo(wireBytes.AsSpan(5));

        var plainJson = JsonSerializer.SerializeToUtf8Bytes("plain");
        var executor = new ReplacingRuleExecutor(deserializedPayload: plainJson);
        await using var deserializer = new JsonSchemaRegistryDeserializer<string>(
            registry,
            ruleExecutor: executor);

        var result = deserializer.Deserialize(wireBytes, CreateContext());

        await Assert.That(result).IsEqualTo("plain");
        await Assert.That(executor.DeserializeContext).IsNotNull();
        await Assert.That(executor.DeserializeContext!.PayloadFormat).IsEqualTo(SchemaRegistryPayloadFormat.Json);
        await Assert.That(executor.DeserializeContext.SchemaId).IsEqualTo(schemaId);
        await Assert.That(executor.DeserializeContext.Schema).IsSameReferenceAs(schema);
    }

    private static Schema NewSchema(int id) => new()
    {
        SchemaType = SchemaType.Json,
        SchemaString = $$"""{ "type": "string", "title": "schema-{{id}}" }"""
    };

    private static string RegisteredSchemaJson(int id) =>
        $$"""
        {
          "subject": "topic-value",
          "version": 1,
          "id": {{id}},
          "schema": "{ \"type\": \"string\" }",
          "schemaType": "JSON"
        }
        """;

    private sealed class QueueingSchemaRegistryHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode StatusCode, string Content)> _responses = new();

        public List<Uri> RequestUris { get; } = [];

        public QueueingSchemaRegistryHandler Enqueue(HttpStatusCode statusCode, string content)
        {
            _responses.Enqueue((statusCode, content));
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            (HttpStatusCode StatusCode, string Content) response = _responses.Count > 0
                ? _responses.Dequeue()
                : (HttpStatusCode.OK, "[]");

            return Task.FromResult(new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(response.Content, Encoding.UTF8)
            });
        }
    }

    private static GenericRecord CreateAvroRecord()
    {
        var schema = (Avro.RecordSchema)AvroSchema.Parse("""
            {
                "type": "record",
                "name": "SimpleRecord",
                "namespace": "test",
                "fields": [
                    { "name": "id", "type": "int" },
                    { "name": "name", "type": "string" }
                ]
            }
            """);
        var record = new GenericRecord(schema);
        record.Add("id", 1);
        record.Add("name", "test");
        return record;
    }
}
