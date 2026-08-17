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
    : ISerializer<T>, IAsyncSerializerPreparer<T>, IAsyncDisposable
{
    private const byte MagicByte = 0x00;
    private const int WireHeaderSize = 5;
    private const int InitialAvroPayloadBufferSize = 1024;
    private const int MaxRetainedAvroPayloadBufferSize = 1024 * 1024;
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly AvroSerializerConfig _config;
    private readonly bool _ownsClient;
    private readonly SchemaResolutionCache<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> _schemaResolutionCache = new();
    private readonly SubjectSchemaIdCache _subjectSchemaIdCache = new();
    private readonly ConcurrentDictionary<AvroSchema, DynamicSchemaCache> _dynamicSchemaCaches =
        new(AvroSchemaLogicalComparer.Instance);
    private readonly ConcurrentDictionary<AvroSchema, SpecificDefaultWriter> _specificWriters =
        new(AvroSchemaReferenceComparer.Instance);
    private readonly AvroSchema? _writerSchema;
    private DynamicSchemaCache? _lastDynamicSchemaCache;

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

    internal int CachedGenericWriterCount => _dynamicSchemaCaches.Count;
    internal int CachedSpecificWriterCount => _specificWriters.Count;
    internal int CachedDynamicSubjectSchemaCount => _dynamicSchemaCaches.Count;

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
        var resolved = await PrepareAsync(topic, value, isKey, cancellationToken).ConfigureAwait(false);
        return resolved.SchemaId;
    }

    /// <summary>
    /// Resolves and caches the subject, schema ID, and schema for a serialization context.
    /// </summary>
    public ValueTask<ResolvedSchemaContext> PrepareAsync(
        string topic,
        T value,
        bool isKey = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        var avroSchema = GetSchemaForValue(value);
        var cache = GetSubjectSchemaIdCache(avroSchema);
        if (cache.TryGet(topic, isKey, out var cached))
            return new ValueTask<ResolvedSchemaContext>(ToResolvedContext(cached));

        return PrepareCoreAsync(
            topic,
            isKey,
            avroSchema,
            cache,
            cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Delegates to <see cref="WarmupAsync"/>. Returns a synchronously completed <see cref="ValueTask"/>
    /// with no allocation once the subject's schema ID is cached, so the producer's steady-state hot path
    /// stays fully synchronous and only the first value per subject pays the async schema fetch.
    /// </remarks>
    public ValueTask PrepareAsync(T value, SerializationContext context, CancellationToken cancellationToken = default)
    {
        var isKey = context.Component == SerializationComponent.Key;
        var preparation = PrepareAsync(context.Topic, value, isKey, cancellationToken);
        if (preparation.IsCompletedSuccessfully)
        {
            _ = preparation.Result;
            return ValueTask.CompletedTask;
        }

        return AwaitPreparationAsync(preparation);

        static async ValueTask AwaitPreparationAsync(ValueTask<ResolvedSchemaContext> preparation) =>
            _ = await preparation.ConfigureAwait(false);
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
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        ArgumentNullException.ThrowIfNull(value);

        var schemaEntry = GetSchemaForContext(context.Topic, context.Component == SerializationComponent.Key, value);
        var schemaId = schemaEntry.SchemaId;

        var codecState = AvroCodecThreadStateCache.Serialization ??= new AvroSerializationThreadState();
        if (_config.RuleExecutor is null)
        {
            SerializeDirect(value, ref destination, schemaId, codecState);
            return;
        }

        SerializeWithRuleExecutor(value, ref destination, context, schemaEntry, schemaId, codecState);
    }

    private void SerializeDirect<TWriter>(
        T value,
        ref TWriter destination,
        int schemaId,
        AvroSerializationThreadState codecState)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        var payloadSizeHint = codecState.PayloadSizeHint;

        while (true)
        {
            var memory = destination.GetMemory(WireHeaderSize + payloadSizeHint);
            var stream = codecState.DirectStream;
            stream.Reset(memory.Slice(WireHeaderSize));

            try
            {
                WriteAvroValue(value, codecState.DirectEncoder);
                codecState.DirectEncoder.Flush();

                var payloadLength = stream.WrittenCount;
                var span = memory.Span;
                span[0] = MagicByte;
                BinaryPrimitives.WriteInt32BigEndian(span.Slice(1, 4), schemaId);

                destination.Advance(WireHeaderSize + payloadLength);
                codecState.PayloadSizeHint = payloadLength > MaxRetainedAvroPayloadBufferSize
                    ? InitialAvroPayloadBufferSize
                    : Math.Max(InitialAvroPayloadBufferSize, payloadLength);
                stream.Reset(default);
                return;
            }
            catch (FixedMemoryStreamOverflowException ex)
            {
                stream.Reset(default);
                payloadSizeHint = GrowPayloadSizeHint(payloadSizeHint, ex.RequiredCapacity);
            }
            catch
            {
                stream.Reset(default);
                throw;
            }
        }
    }

    private void SerializeWithRuleExecutor<TWriter>(
        T value,
        ref TWriter destination,
        SerializationContext context,
        SubjectSchemaIdCache.SubjectSchemaIdCacheEntry schemaEntry,
        int schemaId,
        AvroSerializationThreadState codecState)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        var memoryStream = codecState.BufferedStream;
        memoryStream.ResetForWriting(InitialAvroPayloadBufferSize);

        try
        {
            var encoder = codecState.BufferedEncoder;

            WriteAvroValue(value, encoder);
            encoder.Flush();

            var avroPayloadLength = (int)memoryStream.Position;
            var payload = new ReadOnlyMemory<byte>(memoryStream.GetBuffer(), 0, avroPayloadLength);
            payload = _config.RuleExecutor!.TransformSerializedPayload(
                payload,
                new SchemaRegistryRuleContext
                {
                    Topic = context.Topic,
                    Component = context.Component,
                    SchemaId = schemaId,
                    Subject = schemaEntry.Subject,
                    Schema = schemaEntry.Schema,
                    PayloadFormat = SchemaRegistryPayloadFormat.Avro
                });

            // Write wire format: [0x00] [schema ID] [Avro payload]
            var totalSize = WireHeaderSize + payload.Length;
            var span = destination.GetSpan(totalSize);

            span[0] = MagicByte;
            BinaryPrimitives.WriteInt32BigEndian(span.Slice(1, 4), schemaId);
            payload.Span.CopyTo(span.Slice(5));

            destination.Advance(totalSize);
        }
        finally
        {
            if (memoryStream.Capacity > MaxRetainedAvroPayloadBufferSize)
                memoryStream.DetachBuffer();
        }
    }

    private static int GrowPayloadSizeHint(int currentHint, int requiredCapacity)
    {
        var maxPayloadSize = Array.MaxLength - WireHeaderSize;
        if (requiredCapacity > maxPayloadSize)
            throw new NotSupportedException($"Avro payloads larger than {maxPayloadSize} bytes are not supported.");

        var nextHint = Math.Max((long)currentHint * 2, requiredCapacity);
        return (int)Math.Min(nextHint, maxPayloadSize);
    }

    private SubjectSchemaIdCache.SubjectSchemaIdCacheEntry GetSchemaForContext(string topic, bool isKey, T value)
    {
        var avroSchema = GetSchemaForValue(value);
        var state = new SubjectSchemaIdState(this, avroSchema);
        return GetSubjectSchemaIdCache(avroSchema).GetOrAdd(
            topic,
            isKey,
            state,
            static (state, topic, isKey) => state.Serializer.GetSubjectName(topic, isKey, state.Schema),
            static (state, subject) => state.Serializer.GetSchemaIdCacheValue(subject, state.Schema));
    }

    private ValueTask<ResolvedSchemaContext> PrepareCoreAsync(
        string topic,
        bool isKey,
        AvroSchema avroSchema,
        SubjectSchemaIdCache cache,
        CancellationToken cancellationToken)
    {
        var subject = GetSubjectName(topic, isKey, avroSchema);
        var schema = CreateRegistrySchema(avroSchema);
        var resolved = ResolveSchemaAsync(subject, schema, cancellationToken);
        if (resolved.IsCompletedSuccessfully)
        {
            var value = resolved.Result;
            return new ValueTask<ResolvedSchemaContext>(ToResolvedContext(
                cache.CacheEntry(topic, isKey, subject, value.SchemaId, value.Schema!)));
        }

        return AwaitSchemaAsync(topic, isKey, subject, cache, resolved);

        static async ValueTask<ResolvedSchemaContext> AwaitSchemaAsync(
            string topic,
            bool isKey,
            string subject,
            SubjectSchemaIdCache cache,
            ValueTask<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> resolved)
        {
            var value = await resolved.ConfigureAwait(false);
            return ToResolvedContext(
                cache.CacheEntry(topic, isKey, subject, value.SchemaId, value.Schema!));
        }
    }

    private static ResolvedSchemaContext ToResolvedContext(
        SubjectSchemaIdCache.SubjectSchemaIdCacheEntry entry) =>
        new(entry.Subject!, entry.SchemaId, entry.Schema!);

    private readonly record struct SubjectSchemaIdState(
        AvroSchemaRegistrySerializer<T> Serializer,
        AvroSchema Schema);

    private SubjectSchemaIdCache.SubjectSchemaIdCacheValue GetSchemaIdCacheValue(
        string subject,
        AvroSchema avroSchema) => ResolveSchemaCached(subject, CreateRegistrySchema(avroSchema));

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
                var genericWriter = GetGenericWriter(genericRecord.Schema);
                genericWriter.Write(genericRecord, encoder);
                break;

            default:
                throw new InvalidOperationException(
                    $"Type {typeof(T)} is not supported. Must be ISpecificRecord or GenericRecord.");
        }
    }

    private SubjectSchemaIdCache.SubjectSchemaIdCacheValue ResolveSchemaCached(
        string subject,
        RegistrySchema schema) =>
        _schemaResolutionCache.Resolve(
            subject,
            schema,
            this,
            static (serializer, resolvedSubject, resolvedSchema) =>
                serializer.FetchSchemaWithTimeoutAsync(resolvedSubject, resolvedSchema),
            SchemaRegistryTimeout);

    private ValueTask<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> ResolveSchemaAsync(
        string subject,
        RegistrySchema schema,
        CancellationToken cancellationToken = default) =>
        _schemaResolutionCache.ResolveAsync(
            subject,
            schema,
            this,
            static (serializer, resolvedSubject, resolvedSchema) =>
                serializer.FetchSchemaWithTimeoutAsync(resolvedSubject, resolvedSchema),
            cancellationToken);

    private Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> FetchSchemaWithTimeoutAsync(
        string subject,
        RegistrySchema registrySchema) =>
        SchemaRegistryOperationTimeout.ExecuteAsync(
            cancellationToken => FetchSchemaAsync(subject, registrySchema, cancellationToken),
            SchemaRegistryTimeout,
            "Schema Registry resolution timed out.");

    private async Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> FetchSchemaAsync(
        string subject,
        RegistrySchema registrySchema,
        CancellationToken cancellationToken)
    {
        if (_config.UseLatestVersion)
        {
            var registered = await _schemaRegistry.GetSchemaBySubjectAsync(
                    subject,
                    "latest",
                    cancellationToken)
                .ConfigureAwait(false);
            return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(
                registered.Id,
                registered.Schema);
        }

        if (_config.AutoRegisterSchemas)
        {
            var schemaId = _config.NormalizeSchemas
                ? await _schemaRegistry.GetOrRegisterSchemaAsync(
                    subject,
                    registrySchema,
                    normalize: true,
                    cancellationToken).ConfigureAwait(false)
                : await _schemaRegistry.GetOrRegisterSchemaAsync(
                    subject,
                    registrySchema,
                    cancellationToken).ConfigureAwait(false);
            return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(schemaId, registrySchema);
        }

        var existing = await _schemaRegistry.GetSchemaBySubjectAsync(
                subject,
                "latest",
                cancellationToken)
            .ConfigureAwait(false);
        return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(existing.Id, existing.Schema);
    }

    private static RegistrySchema CreateRegistrySchema(AvroSchema avroSchema)
    {
        return new RegistrySchema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = avroSchema.ToString()
        };
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

    private AvroSchema GetSchemaForValue(T value) =>
        _writerSchema ?? GetSchemaFromValue(value);

    private SubjectSchemaIdCache GetSubjectSchemaIdCache(AvroSchema schema)
    {
        if (_writerSchema is not null)
            return _subjectSchemaIdCache;

        return GetDynamicSchemaCache(schema).SubjectSchemaIdCache;
    }

    private GenericDatumWriter<GenericRecord> GetGenericWriter(AvroSchema schema) =>
        GetDynamicSchemaCache(schema).Writer;

    private DynamicSchemaCache GetDynamicSchemaCache(AvroSchema schema)
    {
        var last = Volatile.Read(ref _lastDynamicSchemaCache);
        if (last is not null && ReferenceEquals(Volatile.Read(ref last.LastSeenSchema), schema))
            return last;

        var entry = _dynamicSchemaCaches.GetOrAdd(
            schema,
            static schema => new DynamicSchemaCache(schema));
        Volatile.Write(ref entry.LastSeenSchema, schema);
        Volatile.Write(ref _lastDynamicSchemaCache, entry);
        return entry;
    }

    private sealed class DynamicSchemaCache
    {
        internal DynamicSchemaCache(AvroSchema schema)
        {
            LastSeenSchema = schema;
            SubjectSchemaIdCache = new SubjectSchemaIdCache();
            Writer = new GenericDatumWriter<GenericRecord>(schema);
        }

        internal AvroSchema LastSeenSchema;
        internal SubjectSchemaIdCache SubjectSchemaIdCache { get; }
        internal GenericDatumWriter<GenericRecord> Writer { get; }
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

    private string GetSubjectName(string topic, bool isKey, AvroSchema schema)
    {
        var recordName = GetRecordName(schema);
        if (_config.CustomSubjectNameStrategy is not null)
        {
            return _config.CustomSubjectNameStrategy.GetSubjectName(topic, recordName, isKey);
        }

        return SubjectNameResolver.GetSubjectName(
            _config.SubjectNameStrategy,
            topic,
            recordName,
            isKey,
            _config.UseLegacySubjectNames);
    }

    private static string GetRecordName(AvroSchema schema)
    {
        return schema is global::Avro.RecordSchema recordSchema
            ? recordSchema.Fullname
            : typeof(T).FullName ?? typeof(T).Name;
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
