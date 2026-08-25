using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Avro.Generic;
using Avro.IO;
using Avro.Specific;
using Dekaf.SchemaRegistry.Avro.Poco;
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
    : IDeserializer<T>, IRecordHeaderDeserializer<T>, ICallerOwnedHeaderDeserializer<T>,
      IRecordHeaderRoutingProvider,
      IAsyncDeserializerPreparer<T>,
      IRecordHeaderAsyncDeserializerPreparer<T>,
      IAsyncDeserializerPreparationRequirement,
      IAsyncDisposable
{
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);
    private const int MaxCachedGuidSchemas = 1024;
    private static readonly string FallbackRecordName = typeof(T).FullName ?? typeof(T).Name;

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly AvroDeserializerConfig _config;
    private readonly bool _ownsClient;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;
    private readonly ConcurrentDictionary<int, Lazy<Task<AvroSchema>>> _schemaCache = new();
    private readonly ConcurrentDictionary<GuidTopicKey, Lazy<Task<GuidResolvedSchema>>> _guidSchemaCache = new();
    private readonly ConcurrentQueue<KeyValuePair<GuidTopicKey, Lazy<Task<GuidResolvedSchema>>>>
        _guidSchemaEvictionQueue = new();
    private int _cachedGuidSchemaCount;
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
        if (_config.SchemaIdStrategy is not (SchemaIdDeserializerStrategy.Dual or SchemaIdDeserializerStrategy.Prefix or SchemaIdDeserializerStrategy.Header))
            throw new ArgumentOutOfRangeException(nameof(config), _config.SchemaIdStrategy, "Unknown schema identity strategy.");
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
        _config.SchemaIdStrategy != SchemaIdDeserializerStrategy.Prefix
        || _ruleExecutor is not null && _subjectNames is { RequiresPreparation: true };

    ValueTask IAsyncDeserializerPreparer<T>.PrepareAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        CancellationToken cancellationToken) =>
        PrepareCoreAsync(data, context, FindCallerIdentityHeader(context), cancellationToken);

    ValueTask IRecordHeaderAsyncDeserializerPreparer<T>.PrepareAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        RecordHeaderRoutingLookup headers,
        CancellationToken cancellationToken) =>
        PrepareCoreAsync(data, context, FindRoutedIdentityHeader(context, in headers), cancellationToken);

    private ValueTask PrepareCoreAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        Header? identityHeader,
        CancellationToken cancellationToken)
    {
        if (context is { IsNull: true, Component: SerializationComponent.Value })
            return default;

        if (_config.SchemaIdStrategy == SchemaIdDeserializerStrategy.Prefix)
        {
            return _ruleExecutor is not null
                && _subjectNames is { RequiresPreparation: true } prefixSubjectNames
                && DeserializerSubjectNameCache.TryReadSchemaId(data, out var prefixSchemaId)
                ? prefixSubjectNames.PrepareAsync(
                    _schemaRegistry,
                    prefixSchemaId,
                    context.Topic,
                    context.Component == SerializationComponent.Key,
                    FallbackRecordName,
                    cancellationToken)
                : default;
        }

        var identity = ReadIdentity(data, identityHeader, out _);
        if (identity.SchemaGuid is { } schemaGuid)
        {
            return new ValueTask(GetGuidSchemaAsync(
                new GuidTopicKey(
                    schemaGuid,
                    context.Topic,
                    context.Component == SerializationComponent.Key,
                    _subjectNames?.Generation ?? 0),
                cancellationToken));
        }

        return _ruleExecutor is not null
            && _subjectNames is { RequiresPreparation: true } subjectNames
            ? subjectNames.PrepareAsync(
                _schemaRegistry,
                identity.SchemaId!.Value,
                context.Topic,
                context.Component == SerializationComponent.Key,
                FallbackRecordName,
                cancellationToken)
            : default;
    }

    bool IAsyncDeserializerPreparer<T>.TryDeserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        out T value) =>
        TryDeserializeCore(data, context, FindCallerIdentityHeader(context), out value);

    bool IRecordHeaderAsyncDeserializerPreparer<T>.TryDeserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers,
        out T value) =>
        TryDeserializeCore(data, context, FindRoutedIdentityHeader(context, in headers), out value);

    private bool TryDeserializeCore(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        Header? identityHeader,
        out T value)
    {
        if (context is { IsNull: true, Component: SerializationComponent.Value })
        {
            value = default!;
            return true;
        }

        string? preparedSubject = null;
        SchemaIdentity identity = default;
        var payloadOffset = 0;
        if (_config.SchemaIdStrategy != SchemaIdDeserializerStrategy.Prefix)
        {
            identity = ReadIdentity(data, identityHeader, out payloadOffset);
            if (identity.SchemaGuid is { } schemaGuid)
            {
                var key = new GuidTopicKey(
                    schemaGuid,
                    context.Topic,
                    context.Component == SerializationComponent.Key,
                    _subjectNames?.Generation ?? 0);
                if (!HasResolvedGuidSchema(key))
                {
                    value = default!;
                    return false;
                }
            }
            else if (_ruleExecutor is not null
                     && _subjectNames is { RequiresPreparation: true } subjectNames)
            {
                if (!subjectNames.TryGetPreparedSubject(
                        identity.SchemaId!.Value,
                        context.Topic,
                        context.Component == SerializationComponent.Key,
                        out var prepared))
                {
                    value = default!;
                    return false;
                }

                preparedSubject = prepared.Subject;
            }
        }
        else if (_ruleExecutor is not null
                 && _subjectNames is { RequiresPreparation: true } prefixSubjectNames
                 && DeserializerSubjectNameCache.TryReadSchemaId(data, out var prefixSchemaId))
        {
            if (!prefixSubjectNames.TryGetPreparedSubject(
                    prefixSchemaId,
                    context.Topic,
                    context.Component == SerializationComponent.Key,
                    out var prepared))
            {
                value = default!;
                return false;
            }

            preparedSubject = prepared.Subject;
        }

        value = _config.SchemaIdStrategy == SchemaIdDeserializerStrategy.Prefix
            ? DeserializeCore(data, context, identityHeader, preparedSubject)
            : DeserializeCore(data, context, identity, payloadOffset, preparedSubject);
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
    public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
        DeserializeCore(data, context, FindCallerIdentityHeader(context), preparedSubject: null);

    T ICallerOwnedHeaderDeserializer<T>.DeserializeCallerOwned(
        ReadOnlyMemory<byte> data,
        SerializationContext context) => Deserialize(data, context);

    T IRecordHeaderDeserializer<T>.Deserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers) =>
        DeserializeCore(
            data,
            context,
            FindRoutedIdentityHeader(context, in headers),
            preparedSubject: null);

    void IRecordHeaderRoutingProvider.CollectHeaderNames(List<string> names)
    {
        if (_config.SchemaIdStrategy == SchemaIdDeserializerStrategy.Prefix)
            return;

        AddHeaderName(names, SchemaIdentityHeaderNames.Key);
        AddHeaderName(names, SchemaIdentityHeaderNames.Value);
    }

    private T DeserializeCore(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        Header? identityHeader,
        string? preparedSubject)
    {
        if (context is { IsNull: true, Component: SerializationComponent.Value })
            return default!;

        var identity = ReadIdentity(data, identityHeader, out var payloadOffset);
        return DeserializeCore(data, context, identity, payloadOffset, preparedSubject);
    }

    private T DeserializeCore(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        SchemaIdentity identity,
        int payloadOffset,
        string? preparedSubject)
    {
        var schemaId = identity.SchemaId ?? -1;
        var guidSchema = identity.SchemaGuid is { } schemaGuid
            ? GetGuidSchemaCached(schemaGuid, context)
            : null;

        // Get writer schema from registry (cached with lazy initialization)
        var writerSchema = guidSchema?.WriterSchema ?? GetWriterSchemaCached(schemaId);

        // Extract Avro payload. Array-backed payloads decode in place; other memory falls back to a pooled copy.
        var payloadMemory = data[payloadOffset..];
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
            Schema schema;
            if (guidSchema is not null)
            {
                schemaId = guidSchema.SchemaId;
                subject = guidSchema.Subject;
                schema = guidSchema.Schema;
            }
            else if (preparedSubject is not null)
            {
                subject = preparedSubject;
                schema = _schemaRegistry.GetSchemaSync(schemaId, subject, SchemaRegistryTimeout);
            }
            else if (_subjectNames is null)
            {
                subject = SubjectNameResolver.GetTopicSubjectName(
                    context.Topic,
                    context.Component == SerializationComponent.Key);
                schema = _schemaRegistry.GetSchemaSync(schemaId, subject, SchemaRegistryTimeout);
            }
            else
            {
                var unscopedSchema = _schemaRegistry.GetSchemaSync(schemaId, SchemaRegistryTimeout);
                subject = GetSubjectName(schemaId, unscopedSchema, context);
                schema = _schemaRegistry.GetSchemaSync(schemaId, subject, SchemaRegistryTimeout);
            }
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

    private GuidResolvedSchema GetGuidSchemaCached(Guid schemaGuid, SerializationContext context)
    {
        var key = new GuidTopicKey(
            schemaGuid,
            context.Topic,
            context.Component == SerializationComponent.Key,
            _subjectNames?.Generation ?? 0);
        if (!_guidSchemaCache.TryGetValue(key, out var lazy))
        {
            lazy = _guidSchemaCache.GetOrAdd(
                key,
                static (cacheKey, deserializer) => deserializer.CreateGuidSchemaLazy(cacheKey),
                this);
        }

        var task = lazy.Value;
        return task.IsCompletedSuccessfully
            ? task.Result
            : task.WaitAsync(SchemaRegistryTimeout).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private bool HasResolvedGuidSchema(GuidTopicKey key) =>
        _guidSchemaCache.TryGetValue(key, out var lazy)
        && lazy.IsValueCreated
        && lazy.Value.IsCompletedSuccessfully;

    private Task<GuidResolvedSchema> GetGuidSchemaAsync(
        GuidTopicKey key,
        CancellationToken cancellationToken)
    {
        var lazy = _guidSchemaCache.GetOrAdd(
            key,
            static (cacheKey, deserializer) => deserializer.CreateGuidSchemaLazy(cacheKey),
            this);
        return lazy.Value.WaitAsync(cancellationToken);
    }

    private Lazy<Task<GuidResolvedSchema>> CreateGuidSchemaLazy(GuidTopicKey key) =>
        new(() => FetchGuidSchemaAsync(key));

    private async Task<GuidResolvedSchema> FetchGuidSchemaAsync(GuidTopicKey key)
    {
        try
        {
            var resolved = await SchemaRegistryOperationTimeout.ExecuteAsync(
                    cancellationToken => FetchGuidSchemaCoreAsync(key, cancellationToken),
                    SchemaRegistryTimeout,
                    $"Schema GUID {key.SchemaGuid:D} resolution timed out.")
                .ConfigureAwait(false);
            BoundedSchemaIdentityCache.RecordSuccessfulResolution(
                _guidSchemaCache,
                _guidSchemaEvictionQueue,
                key,
                ref _cachedGuidSchemaCount,
                MaxCachedGuidSchemas);
            return resolved;
        }
        catch
        {
            _guidSchemaCache.TryRemove(key, out _);
            throw;
        }
    }

    private async Task<GuidResolvedSchema> FetchGuidSchemaCoreAsync(
        GuidTopicKey key,
        CancellationToken cancellationToken)
    {
        var unscopedSchema = await _schemaRegistry.GetSchemaByGuidAsync(
                key.SchemaGuid.ToString("D"),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (unscopedSchema.SchemaType != SchemaType.Avro)
        {
            throw new InvalidOperationException(
                $"Schema with GUID {key.SchemaGuid:D} is not an Avro schema. Type: {unscopedSchema.SchemaType}");
        }

        var subject = _subjectNames is null
            ? SubjectNameResolver.GetTopicSubjectName(key.Topic, key.IsKey)
            : await _subjectNames.ResolveSubjectNameAsync(
                    unscopedSchema,
                    key.Topic,
                    key.IsKey,
                    FallbackRecordName,
                    cancellationToken)
                .ConfigureAwait(false);
        var registered = await _schemaRegistry.LookupSchemaAsync(
                subject,
                unscopedSchema,
                ignoreDeletedSchemas: true,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!Guid.TryParse(registered.Guid, out var registeredGuid) || registeredGuid != key.SchemaGuid)
        {
            throw new InvalidDataException(
                $"Schema Registry returned a conflicting GUID for subject '{subject}'.");
        }

        var names = registered.Schema.References is { Count: > 0 }
            ? await AvroSchemaReferenceResolver.ResolveAsync(
                    _schemaRegistry,
                    registered.Schema,
                    cancellationToken)
                .ConfigureAwait(false)
            : null;
        var writerSchema = names is null
            ? AvroSchema.Parse(registered.Schema.SchemaString)
            : AvroSchema.Parse(registered.Schema.SchemaString, names);
        var resolved = new GuidResolvedSchema(
            registered.Id,
            subject,
            registered.Schema,
            writerSchema);
        return resolved;
    }

    private static void AddHeaderName(List<string> names, string name)
    {
        if (!names.Contains(name))
            names.Add(name);
    }

    private static string GetIdentityHeaderName(SerializationComponent component) => component switch
    {
        SerializationComponent.Key => SchemaIdentityHeaderNames.Key,
        SerializationComponent.Value => SchemaIdentityHeaderNames.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Unknown serialization component.")
    };

    private Header? FindCallerIdentityHeader(SerializationContext context)
    {
        if (_config.SchemaIdStrategy == SchemaIdDeserializerStrategy.Prefix
            || context.Headers is not { } headers)
        {
            return null;
        }

        var headerName = GetIdentityHeaderName(context.Component);
        for (var index = headers.Count - 1; index >= 0; index--)
        {
            if (string.Equals(headers[index].Key, headerName, StringComparison.Ordinal))
                return headers[index];
        }

        return null;
    }

    private Header? FindRoutedIdentityHeader(
        SerializationContext context,
        in RecordHeaderRoutingLookup headers) =>
        _config.SchemaIdStrategy != SchemaIdDeserializerStrategy.Prefix
        && headers.TryGetLast(GetIdentityHeaderName(context.Component), out var header)
            ? header
            : null;

    private SchemaIdentity ReadIdentity(
        ReadOnlyMemory<byte> data,
        Header? identityHeader,
        out int payloadOffset)
    {
        var identity = SchemaIdentityFraming.Read(
            data.Span,
            identityHeader,
            _config.SchemaIdStrategy,
            out payloadOffset,
            out var trailingHeaderData);
        if (!trailingHeaderData.IsEmpty)
            throw new InvalidDataException("Avro schema identity headers cannot contain trailing data.");
        return identity;
    }

    private readonly record struct GuidTopicKey(
        Guid SchemaGuid,
        string Topic,
        bool IsKey,
        int SubjectGeneration);

    private sealed record GuidResolvedSchema(
        int SchemaId,
        string Subject,
        Schema Schema,
        AvroSchema WriterSchema);

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
