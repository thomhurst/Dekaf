using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Avro.Generic;
using Avro.IO;
using Avro.Specific;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;

namespace Dekaf.SchemaRegistry.Avro;

/// <summary>
/// Avro deserializer that integrates with Confluent Schema Registry.
/// Handles the wire format: [magic byte (0x00)] [4-byte schema ID] [Avro binary payload].
/// </summary>
/// <remarks>
/// <para>
/// This deserializer uses lazy caching for writer schemas. The first time a schema ID is
/// encountered, an async call to the Schema Registry is made. This call is wrapped in
/// <see cref="Lazy{T}"/> to ensure thread-safety: only one thread performs the fetch while
/// others wait for the result.
/// </para>
/// <para>
/// After the first fetch, subsequent deserialization calls for the same schema ID use the
/// cached schema without any blocking or async overhead.
/// </para>
/// <para>
/// Kafka value tombstones return <see langword="default"/> without reading Confluent framing
/// or contacting Schema Registry. This is <see langword="null"/> for reference and nullable
/// types; non-nullable value types receive their normal default value.
/// </para>
/// <para>
/// For high-throughput scenarios where you know the schema IDs in advance, use
/// <see cref="WarmupAsync"/> to pre-warm the cache before starting consumption. This ensures
/// the synchronous <see cref="Deserialize"/> method never blocks on Schema Registry calls.
/// </para>
/// </remarks>
/// <typeparam name="T">The type to deserialize. Must be either an Avro ISpecificRecord or GenericRecord.</typeparam>
public sealed class AvroSchemaRegistryDeserializer<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicFields |
        DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>
    : IDeserializer<T>,
      IAsyncDeserializerPreparer<T>,
      IAsyncDeserializerPreparationRequirement,
      IAsyncDisposable
{
    private const byte MagicByte = 0x00;
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);
    private static readonly string FallbackRecordName = typeof(T).FullName ?? typeof(T).Name;

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly AvroDeserializerConfig _config;
    private readonly bool _ownsClient;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;
    private readonly ConcurrentDictionary<int, Lazy<Task<AvroSchema>>> _schemaCache = new();
    private readonly ConcurrentDictionary<AvroSchemaPair, GenericDatumReader<GenericRecord>> _genericReaders =
        new(AvroSchemaPairReferenceComparer.Instance);
    private readonly ConcurrentDictionary<AvroSchemaPair, SpecificDatumReader<T>> _specificReaders =
        new(AvroSchemaPairReferenceComparer.Instance);
    private readonly ConcurrentDictionary<AvroSchema, byte> _validatedSpecificMigrationSchemas =
        new(AvroSchemaReferenceComparer.Instance);
    private readonly AvroSchema? _readerSchema;
    private readonly DeserializerSubjectNameCache? _subjectNames;
    private readonly SchemaRegistryMigrationRunner? _migrationRunner;
    private readonly AvroTaggedFieldTransformerProvider _taggedFieldTransformers = new();

    /// <summary>
    /// Creates a new Avro Schema Registry deserializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="config">Optional deserializer configuration.</param>
    /// <param name="ownsClient">Whether this deserializer owns the client and should dispose it.</param>
    public AvroSchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        AvroDeserializerConfig? config = null,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _config = config ?? new AvroDeserializerConfig();
        _ruleExecutor = _config.RuleExecutor;
        _ownsClient = ownsClient;
        if (_config.UseLatestVersion && !string.IsNullOrEmpty(_config.ReaderSchema))
        {
            throw new ArgumentException(
                $"{nameof(AvroDeserializerConfig.UseLatestVersion)} and {nameof(AvroDeserializerConfig.ReaderSchema)} cannot both be configured.",
                nameof(config));
        }

        _subjectNames = DeserializerSubjectNameCache.Create(
            schemaRegistry,
            _config.SubjectNameStrategy,
            _config.CustomSubjectNameStrategy,
            _config.AsyncSubjectNameStrategy,
            _config.UseLegacySubjectNames);
        if (_config.UseLatestVersion)
        {
            (_migrationRunner, _ruleExecutor) = SchemaRegistryMigrationRunner.Create(
                schemaRegistry,
                _config.RuleExecutor,
                SchemaRegistryTimeout);
        }

        // Parse custom reader schema if provided, otherwise derive from type
        _readerSchema = GetReaderSchema();
    }

    internal int CachedGenericReaderCount => _genericReaders.Count;
    internal int CachedSpecificReaderCount => _specificReaders.Count;

    bool IAsyncDeserializerPreparationRequirement.RequiresPreparation =>
        _ruleExecutor is not null && _subjectNames is { RequiresPreparation: true };

    ValueTask IAsyncDeserializerPreparer<T>.PrepareAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        CancellationToken cancellationToken)
    {
        if (_ruleExecutor is null
            || _subjectNames is not { RequiresPreparation: true }
            || !DeserializerSubjectNameCache.TryReadSchemaId(data, out var schemaId))
        {
            return default;
        }

        return _subjectNames.PrepareAsync(
            _schemaRegistry,
            schemaId,
            context.Topic,
            context.Component == SerializationComponent.Key,
            FallbackRecordName,
            cancellationToken);
    }

    bool IAsyncDeserializerPreparer<T>.TryDeserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        out T value)
    {
        if (_ruleExecutor is not null
            && _subjectNames is { RequiresPreparation: true }
            && DeserializerSubjectNameCache.TryReadSchemaId(data, out var schemaId)
            && !_subjectNames.IsPrepared(
                schemaId,
                context.Topic,
                context.Component == SerializationComponent.Key))
        {
            value = default!;
            return false;
        }

        value = Deserialize(data, context);
        return true;
    }

    /// <summary>
    /// Pre-warms the schema cache for a specific schema ID.
    /// </summary>
    /// <remarks>
    /// Call this method before starting consumption if you know the schema IDs in advance.
    /// This ensures that the synchronous <see cref="Deserialize"/> method never blocks on
    /// Schema Registry calls for the specified schema ID.
    /// </remarks>
    /// <param name="schemaId">The schema ID to warm up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed Avro schema.</returns>
    public async Task<AvroSchema> WarmupAsync(int schemaId, CancellationToken cancellationToken = default)
    {
        return await GetOrFetchWriterSchemaAsync(schemaId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deserializes data from the input buffer using Avro binary decoding
    /// with Schema Registry wire format.
    /// </summary>
    /// <remarks>
    /// This method uses cached schemas when available. If the schema is not yet cached,
    /// the first call will block while fetching from the Schema Registry. Subsequent calls
    /// for the same schema ID will use the cached value without blocking.
    /// For best performance, use <see cref="WarmupAsync"/> before starting consumption.
    /// </remarks>
    public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        var span = data.Span;

        if (span.Length < 5)
        {
            if (context is { IsNull: true, Component: SerializationComponent.Value })
                return default!;

            throw new InvalidOperationException("Message too short to contain Schema Registry wire format");
        }

        if (span[0] != MagicByte)
            throw new InvalidOperationException($"Unknown magic byte: {span[0]}. Expected Schema Registry format (0x00).");

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(span.Slice(1, 4));

        // Get writer schema from registry (cached with lazy initialization)
        var writerSchema = GetWriterSchemaCached(schemaId);

        // Extract Avro payload. Array-backed payloads decode in place; other memory falls back to a pooled copy.
        var payloadMemory = data.Slice(5);
        AvroSchema? migrationReaderSchema = null;
        if (_ruleExecutor is null)
        {
            var directCodecState = AvroCodecThreadStateCache.Deserialization ??= new AvroDeserializationThreadState();
            return ReadAvroPayload(
                payloadMemory,
                writerSchema,
                migrationReaderSchema: null,
                codecState: directCodecState);
        }

        var taggedWorkspaceOperation = AvroTaggedFieldTransformerProvider.BeginOperation();
        try
        {
            string subject;
            if (_subjectNames is null)
            {
                subject = SubjectNameResolver.GetTopicSubjectName(
                    context.Topic,
                    context.Component == SerializationComponent.Key);
            }
            else
            {
                var unscopedSchema = _schemaRegistry.GetSchemaSync(schemaId, SchemaRegistryTimeout);
                subject = GetSubjectName(schemaId, unscopedSchema, context);
            }

            var schema = _schemaRegistry.GetSchemaSync(schemaId, subject, SchemaRegistryTimeout);
            if (_migrationRunner is null)
            {
                var ruleContext = SchemaRegistryRuleContext.RentWithTaggedFieldTransformer(
                    context.Topic,
                    context.Component,
                    schemaId,
                    subject,
                    schema,
                    SchemaRegistryPayloadFormat.Avro,
                    _taggedFieldTransformers.Get(schema, writerSchema));
                try
                {
                    payloadMemory = _ruleExecutor.TransformDeserializedPayload(payloadMemory, ruleContext);
                }
                finally
                {
                    ruleContext.Return();
                }
            }
            else
            {
                var migration = _migrationRunner.Transform(
                    payloadMemory,
                    schemaId,
                    subject,
                    schema,
                    context,
                    SchemaRegistryPayloadFormat.Avro,
                    _taggedFieldTransformers);
                payloadMemory = migration.Payload;
                writerSchema = GetWriterSchemaCached(migration.PayloadSchemaId);
                migrationReaderSchema = GetWriterSchemaCached(migration.ReaderSchema.Id);
            }
            var codecState = AvroCodecThreadStateCache.Deserialization ??= new AvroDeserializationThreadState();
            return ReadAvroPayload(payloadMemory, writerSchema, migrationReaderSchema, codecState);
        }
        finally
        {
            taggedWorkspaceOperation.Dispose();
        }
    }

    private string GetSubjectName(int schemaId, Schema schema, SerializationContext context)
    {
        var isKey = context.Component == SerializationComponent.Key;
        return _subjectNames?.GetSubjectName(
                schemaId,
                schema,
                context.Topic,
                isKey,
                FallbackRecordName)
            ?? SubjectNameResolver.GetTopicSubjectName(context.Topic, isKey);
    }

    private T ReadAvroPayload(
        ReadOnlyMemory<byte> payloadMemory,
        AvroSchema writerSchema,
        AvroSchema? migrationReaderSchema,
        AvroDeserializationThreadState codecState)
    {
        var memoryStream = codecState.Stream;

        if (MemoryMarshal.TryGetArray(payloadMemory, out var segment) && segment.Array is not null)
        {
            memoryStream.Reset(segment.Array, segment.Offset, segment.Count);
            try
            {
                return ReadAvroValue(writerSchema, migrationReaderSchema, codecState.Decoder);
            }
            finally
            {
                memoryStream.DetachBuffer();
            }
        }

        var payload = payloadMemory.Span;
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(payload.Length);
        try
        {
            payload.CopyTo(rentedBuffer);
            memoryStream.Reset(rentedBuffer, payload.Length);

            return ReadAvroValue(writerSchema, migrationReaderSchema, codecState.Decoder);
        }
        finally
        {
            memoryStream.DetachBuffer();
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    private T ReadAvroValue(
        AvroSchema writerSchema,
        AvroSchema? migrationReaderSchema,
        BinaryDecoder decoder)
    {
        if (typeof(T) == typeof(GenericRecord))
        {
            var readerSchema = migrationReaderSchema ?? _readerSchema ?? writerSchema;
            var reader = _genericReaders.GetOrAdd(
                new AvroSchemaPair(writerSchema, readerSchema),
                static key => new GenericDatumReader<GenericRecord>(key.WriterSchema, key.ReaderSchema));
            var result = reader.Read(default!, decoder);
            return (T)(object)result;
        }

        if (typeof(ISpecificRecord).IsAssignableFrom(typeof(T)))
        {
            var readerSchema = _readerSchema ?? throw new InvalidOperationException(
                $"Specific Avro type {typeof(T)} does not expose a static _SCHEMA field.");
            if (migrationReaderSchema is not null &&
                !_validatedSpecificMigrationSchemas.TryGetValue(migrationReaderSchema, out _))
            {
                if (!readerSchema.CanRead(migrationReaderSchema))
                {
                    throw new InvalidOperationException(
                        $"The latest Schema Registry schema is incompatible with specific Avro type {typeof(T)}.");
                }

                _validatedSpecificMigrationSchemas.TryAdd(migrationReaderSchema, 0);
            }

            var reader = _specificReaders.GetOrAdd(
                new AvroSchemaPair(writerSchema, readerSchema),
                static key => new SpecificDatumReader<T>(key.WriterSchema, key.ReaderSchema));
            return reader.Read(default!, decoder);
        }

        throw new InvalidOperationException(
            $"Type {typeof(T)} is not supported. Must be ISpecificRecord or GenericRecord.");
    }

    private AvroSchema GetWriterSchemaCached(int schemaId)
    {
        var lazyTask = GetOrAddWriterSchemaLazy(schemaId);

        // If the task is already completed, this returns immediately without blocking.
        // If this is the first access, it will block waiting for the schema fetch.
        // The Lazy ensures that only ONE thread ever blocks for a given schema ID.
        var task = lazyTask.Value;

        if (task.IsCompletedSuccessfully)
        {
            // Fast path: schema already cached, no blocking
            return task.Result;
        }

        // Slow path: first fetch or concurrent access during first fetch.
        // This blocks the calling thread, but only happens once per schema ID.
        // Add timeout to prevent indefinite hanging.
        return task.WaitAsync(SchemaRegistryTimeout).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private async Task<AvroSchema> GetOrFetchWriterSchemaAsync(int schemaId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lazyTask = GetOrAddWriterSchemaLazy(schemaId);

        return await lazyTask.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private Lazy<Task<AvroSchema>> GetOrAddWriterSchemaLazy(int schemaId)
    {
        if (_schemaCache.TryGetValue(schemaId, out var cached))
            return cached;

        return _schemaCache.GetOrAdd(
            schemaId,
            static (id, deserializer) => deserializer.CreateWriterSchemaLazy(id),
            this);
    }

    private Lazy<Task<AvroSchema>> CreateWriterSchemaLazy(int schemaId) =>
        new(() => FetchWriterSchemaAsync(schemaId));

    private async Task<AvroSchema> FetchWriterSchemaAsync(int schemaId)
    {
        try
        {
            var registrySchema = await _schemaRegistry.GetSchemaAsync(schemaId, CancellationToken.None)
                .ConfigureAwait(false);

            if (registrySchema.SchemaType != SchemaType.Avro)
                throw new InvalidOperationException(
                    $"Schema with ID {schemaId} is not an Avro schema. Type: {registrySchema.SchemaType}");

            return AvroSchema.Parse(registrySchema.SchemaString);
        }
        catch
        {
            _schemaCache.TryRemove(schemaId, out _);
            throw;
        }
    }

    private AvroSchema? GetReaderSchema()
    {
        // If custom reader schema is provided, parse and use it
        if (!string.IsNullOrEmpty(_config.ReaderSchema))
            return AvroSchema.Parse(_config.ReaderSchema);

        // For specific records, try to get schema from type
        if (!typeof(ISpecificRecord).IsAssignableFrom(typeof(T)))
            return null;

        // Avro generated classes have a static _SCHEMA field (cached lookup)
        var schemaField = AvroSchemaFieldCache.GetSchemaField(typeof(T));

        if (schemaField?.GetValue(null) is AvroSchema schema)
            return schema;

        return null;
    }

    /// <summary>
    /// Disposes the deserializer and optionally the underlying Schema Registry client.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}
