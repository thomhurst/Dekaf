using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Avro.Generic;
using Avro.IO;
using Avro.Specific;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;
using RegistrySchema = Dekaf.SchemaRegistry.Schema;

namespace Dekaf.SchemaRegistry.Avro;

/// <summary>
/// Avro serializer that integrates with Confluent Schema Registry.
/// Handles the wire format: [magic byte (0x00)] [4-byte schema ID] [Avro binary payload].
/// </summary>
/// <remarks>
/// <para>
/// This serializer uses lazy caching for schema IDs. The first time a schema is needed for a
/// particular subject, an async call to the Schema Registry is made. This call is wrapped in
/// <see cref="Lazy{T}"/> to ensure thread-safety: only one thread performs the fetch while
/// others wait for the result.
/// </para>
/// <para>
/// After the first fetch, subsequent serialization calls for the same subject use the cached
/// schema ID without any blocking or async overhead.
/// </para>
/// <para>
/// For high-throughput scenarios, use <see cref="WarmupAsync"/> to pre-warm the cache before
/// starting production. This ensures the synchronous <see cref="Serialize"/> method never
/// blocks on Schema Registry calls.
/// </para>
/// </remarks>
/// <typeparam name="T">The type to serialize. Must be either an Avro ISpecificRecord or GenericRecord.</typeparam>
public sealed class AvroSchemaRegistrySerializer<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>
    : ISerializer<T>, IAsyncDisposable
{
    private const byte MagicByte = 0x00;
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly AvroSerializerConfig _config;
    private readonly bool _ownsClient;
    private readonly ConcurrentDictionary<string, Lazy<Task<int>>> _schemaIdCache = new();
    private readonly SubjectSchemaIdCache _subjectSchemaIdCache = new();
    private readonly ConcurrentDictionary<AvroSchema, GenericDatumWriter<GenericRecord>> _genericWriters =
        new(AvroSchemaReferenceComparer.Instance);
    private readonly ConcurrentDictionary<AvroSchema, SpecificDefaultWriter> _specificWriters =
        new(AvroSchemaReferenceComparer.Instance);
    private readonly AvroSchema? _writerSchema;

    /// <summary>
    /// Creates a new Avro Schema Registry serializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="config">Optional serializer configuration.</param>
    /// <param name="ownsClient">Whether this serializer owns the client and should dispose it.</param>
    public AvroSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        AvroSerializerConfig? config = null,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _config = config ?? new AvroSerializerConfig();
        _ownsClient = ownsClient;

        // Try to get schema from type T if it's a specific record
        _writerSchema = GetSchemaFromType();
    }

    internal int CachedGenericWriterCount => _genericWriters.Count;
    internal int CachedSpecificWriterCount => _specificWriters.Count;

    /// <summary>
    /// Pre-warms the schema cache for a specific topic.
    /// </summary>
    /// <remarks>
    /// Call this method before starting production to ensure that the synchronous
    /// <see cref="Serialize"/> method never blocks on Schema Registry calls.
    /// After warmup, all serialization calls for the specified topic will use cached schema IDs.
    /// </remarks>
    /// <param name="topic">The topic name to warm up the cache for.</param>
    /// <param name="value">A sample value to extract the schema from. Required for GenericRecord types.</param>
    /// <param name="isKey">Whether this is for the key (true) or value (false) component.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The schema ID that will be used for serialization.</returns>
    public async Task<int> WarmupAsync(string topic, T value, bool isKey = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        var subject = GetSubjectName(topic, isKey);
        var schemaId = await GetOrFetchSchemaIdAsync(subject, value, cancellationToken).ConfigureAwait(false);
        CacheSubjectSchemaId(topic, isKey, schemaId);
        return schemaId;
    }

    /// <summary>
    /// Serializes the value to the output buffer using Avro binary encoding
    /// with Schema Registry wire format.
    /// </summary>
    /// <remarks>
    /// This method uses cached schema IDs when available. If the schema is not yet cached,
    /// the first call will block while fetching from the Schema Registry. Subsequent calls
    /// for the same subject will use the cached value without blocking.
    /// For best performance, use <see cref="WarmupAsync"/> before starting production.
    /// </remarks>
    public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        ArgumentNullException.ThrowIfNull(value);

        var schemaId = GetSchemaIdForContext(context.Topic, context.Component == SerializationComponent.Key, value);

        var codecState = AvroCodecThreadStateCache.Serialization ??= new AvroSerializationThreadState();
        var memoryStream = codecState.Stream;
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(1024);

        try
        {
            // Initial estimate: 1KB should handle most messages; the stream grows if needed.
            memoryStream.Reset(rentedBuffer);
            var encoder = codecState.Encoder;

            WriteAvroValue(value, encoder);
            encoder.Flush();

            var avroPayloadLength = (int)memoryStream.Position;
            var payload = new ReadOnlyMemory<byte>(memoryStream.GetBuffer(), 0, avroPayloadLength);
            if (_config.RuleExecutor is not null)
            {
                var isKey = context.Component == SerializationComponent.Key;
                var subject = GetSubjectName(context.Topic, isKey);
                payload = _config.RuleExecutor.TransformSerializedPayload(
                    payload,
                    new SchemaRegistryRuleContext
                    {
                        Topic = context.Topic,
                        Component = context.Component,
                        SchemaId = schemaId,
                        Subject = subject,
                        Schema = new RegistrySchema
                        {
                            SchemaType = SchemaType.Avro,
                            SchemaString = GetSchemaFromValue(value).ToString()
                        },
                        PayloadFormat = SchemaRegistryPayloadFormat.Avro
                    });
            }

            // Write wire format: [0x00] [schema ID] [Avro payload]
            var totalSize = 1 + 4 + payload.Length;
            var span = destination.GetSpan(totalSize);

            span[0] = MagicByte;
            BinaryPrimitives.WriteInt32BigEndian(span.Slice(1, 4), schemaId);
            payload.Span.CopyTo(span.Slice(5));

            destination.Advance(totalSize);
        }
        finally
        {
            memoryStream.DetachBuffer();
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    private int GetSchemaIdForContext(string topic, bool isKey, T value)
        => _subjectSchemaIdCache.GetOrAdd(
            topic,
            isKey,
            new SubjectSchemaIdState(this, value),
            static (state, topic, isKey) => state.Serializer.GetSubjectName(topic, isKey),
            static (state, subject) => state.Serializer.GetSchemaIdCached(subject, state.Value));

    private int CacheSubjectSchemaId(string topic, bool isKey, int schemaId)
        => _subjectSchemaIdCache.Cache(topic, isKey, schemaId);

    private readonly record struct SubjectSchemaIdState(AvroSchemaRegistrySerializer<T> Serializer, T Value);

    private void WriteAvroValue(T value, BinaryEncoder encoder)
    {
        switch (value)
        {
            case ISpecificRecord specificRecord:
                var specificWriter = _specificWriters.GetOrAdd(
                    specificRecord.Schema,
                    static schema => new SpecificDefaultWriter(schema));
                specificWriter.Write(specificRecord.Schema, specificRecord, encoder);
                break;

            case GenericRecord genericRecord:
                var genericWriter = _genericWriters.GetOrAdd(
                    genericRecord.Schema,
                    static schema => new GenericDatumWriter<GenericRecord>(schema));
                genericWriter.Write(genericRecord, encoder);
                break;

            default:
                throw new InvalidOperationException(
                    $"Type {typeof(T)} is not supported. Must be ISpecificRecord or GenericRecord.");
        }
    }

    private int GetSchemaIdCached(string subject, T value)
    {
        var lazyTask = GetOrAddSchemaIdLazy(subject, value, cancellationToken: default);

        // If the task is already completed, this returns immediately without blocking.
        // If this is the first access, it will block waiting for the schema fetch.
        // The Lazy ensures that only ONE thread ever blocks for a given subject.
        var task = lazyTask.Value;

        if (task.IsCompletedSuccessfully)
        {
            // Fast path: schema already cached, no blocking
            return task.Result;
        }

        // Slow path: first fetch or concurrent access during first fetch.
        // This blocks the calling thread, but only happens once per subject.
        // Add timeout to prevent indefinite hanging.
        return task.WaitAsync(SchemaRegistryTimeout).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private async Task<int> GetOrFetchSchemaIdAsync(string subject, T value, CancellationToken cancellationToken = default)
    {
        var lazyTask = GetOrAddSchemaIdLazy(subject, value, cancellationToken);

        return await lazyTask.Value.ConfigureAwait(false);
    }

    private Lazy<Task<int>> GetOrAddSchemaIdLazy(string subject, T value, CancellationToken cancellationToken)
    {
        if (_schemaIdCache.TryGetValue(subject, out var cached))
            return cached;

        return _schemaIdCache.GetOrAdd(
            subject,
            static (key, state) => state.Serializer.CreateSchemaIdLazy(key, state.Value, state.CancellationToken),
            new SchemaIdFetchState(this, value, cancellationToken));
    }

    private Lazy<Task<int>> CreateSchemaIdLazy(string subject, T value, CancellationToken cancellationToken) =>
        new(() => FetchSchemaIdAsync(subject, value, cancellationToken));

    private readonly record struct SchemaIdFetchState(
        AvroSchemaRegistrySerializer<T> Serializer,
        T Value,
        CancellationToken CancellationToken);

    private async Task<int> FetchSchemaIdAsync(string subject, T value, CancellationToken cancellationToken = default)
    {
        // Get schema from value or type
        var avroSchema = GetSchemaFromValue(value);
        var schemaString = avroSchema.ToString();

        var registrySchema = new RegistrySchema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = schemaString
        };

        if (_config.UseLatestVersion)
        {
            // Use latest schema from registry
            var registered = await _schemaRegistry.GetSchemaBySubjectAsync(subject, "latest", cancellationToken)
                .ConfigureAwait(false);
            return registered.Id;
        }

        if (_config.AutoRegisterSchemas)
        {
            // Register schema if auto-register is enabled
            return await _schemaRegistry.GetOrRegisterSchemaAsync(subject, registrySchema, cancellationToken)
                .ConfigureAwait(false);
        }

        // Get existing schema ID from registry
        var existing = await _schemaRegistry.GetSchemaBySubjectAsync(subject, "latest", cancellationToken)
            .ConfigureAwait(false);
        return existing.Id;
    }

    private AvroSchema GetSchemaFromValue(T value)
    {
        return value switch
        {
            ISpecificRecord specificRecord => specificRecord.Schema,
            GenericRecord genericRecord => genericRecord.Schema,
            _ => _writerSchema ?? throw new InvalidOperationException(
                $"Cannot determine Avro schema for type {typeof(T)}")
        };
    }

    private static AvroSchema? GetSchemaFromType()
    {
        // Check if T implements ISpecificRecord and has a static Schema property
        if (!typeof(ISpecificRecord).IsAssignableFrom(typeof(T)))
            return null;

        // Avro generated classes have a static _SCHEMA field (cached lookup)
        var schemaField = AvroSchemaFieldCache.GetSchemaField(typeof(T));

        if (schemaField?.GetValue(null) is AvroSchema schema)
            return schema;

        return null;
    }

    private string GetSubjectName(string topic, bool isKey)
    {
        if (_config.CustomSubjectNameStrategy is not null)
        {
            return _config.CustomSubjectNameStrategy.GetSubjectName(topic, GetRecordName(), isKey);
        }

        var suffix = isKey ? "-key" : "-value";
        return _config.SubjectNameStrategy switch
        {
            SubjectNameStrategy.TopicName => topic + suffix,
            SubjectNameStrategy.RecordName => GetRecordName() + suffix,
            SubjectNameStrategy.TopicRecordName => $"{topic}-{GetRecordName()}{suffix}",
            _ => topic + suffix
        };
    }

    private static string GetRecordName()
    {
        // For Avro specific records, try to get the full name from schema
        if (typeof(ISpecificRecord).IsAssignableFrom(typeof(T)))
        {
            // Avro generated classes have a static _SCHEMA field (cached lookup)
            var schemaField = AvroSchemaFieldCache.GetSchemaField(typeof(T));

            if (schemaField?.GetValue(null) is global::Avro.RecordSchema recordSchema)
                return recordSchema.Fullname;
        }

        return typeof(T).FullName ?? typeof(T).Name;
    }

    /// <summary>
    /// Disposes the serializer and optionally the underlying Schema Registry client.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}
