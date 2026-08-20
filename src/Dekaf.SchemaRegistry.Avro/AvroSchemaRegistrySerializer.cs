using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Avro.Generic;
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
/// <typeparam name="T">
/// The type to serialize. Must be either a concrete Avro ISpecificRecord type with a statically
/// discoverable schema or GenericRecord. Runtime SpecificRecord type discovery is unsupported.
/// </typeparam>
public sealed class AvroSchemaRegistrySerializer<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicFields |
        DynamicallyAccessedMemberTypes.PublicProperties)] T>
    : ISerializer<T>,
    IAsyncSerializerPreparer<T>,
    IAsyncDisposable,
    IAssociatedNameCacheInvalidationTarget
{
    private const byte MagicByte = 0x00;
    private const int WireHeaderSize = 5;
    private const int InitialAvroPayloadBufferSize = 1024;
    private const int MaxRetainedAvroPayloadBufferSize = 1024 * 1024;
    private const int MaxAssociatedNameInvalidationRetries = 4;
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);
    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly AvroSerializerConfig _config;
    private readonly IAsyncSubjectNameStrategy? _asyncSubjectNameStrategy;
    private readonly bool _ownsClient;
    private readonly SchemaResolutionCache<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> _schemaResolutionCache;
    private SubjectSchemaIdCache _subjectSchemaIdCache = new();
    private readonly ConcurrentDictionary<DynamicSchemaKey, DynamicSchemaCache> _dynamicSchemaCaches =
        new(DynamicSchemaKeyComparer.Instance);
    private readonly ConcurrentDictionary<AvroSchema, DynamicSchemaCache> _dynamicSchemaCachesByReference =
        new(AvroSchemaReferenceComparer.Instance);
    private readonly ConcurrentDictionary<DynamicSchemaKey, DynamicSchemaCache> _overflowDynamicSchemaCaches =
        new(DynamicSchemaKeyComparer.Instance);
    private ConditionalWeakTable<AvroSchema, DynamicSchemaCache> _weakDynamicSchemaCaches = new();
    private readonly Queue<DynamicSchemaKey> _overflowDynamicSchemaOrder = new();
    private readonly object _dynamicSchemaCacheMutationLock = new();
    private readonly int _maxCachedSchemas;
    private readonly int _maxOverflowLogicalSchemas;
    private readonly AllocationFreeSpecificRecordWriter<T>? _specificWriter;
    private readonly AvroSchema? _writerSchema;
    private readonly AvroTaggedFieldTransformerProvider _taggedFieldTransformers = new();
    private int _dynamicSchemaCacheCount;
    private int _overflowDynamicSchemaCacheCount;
    private int _hasEvictedOverflowLogicalSchemas;
    private DynamicSchemaCache? _lastDynamicSchemaCache;
    private DynamicSchemaCache? _previousDynamicSchemaCache;

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
        if (_config.CustomSubjectNameStrategy is null)
        {
            _asyncSubjectNameStrategy = _config.AsyncSubjectNameStrategy
                ?? (_config.SubjectNameStrategy == SubjectNameStrategy.AssociatedName
                    ? new AssociatedNameStrategy(schemaRegistry)
                    : null);
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(_config.MaxCachedSchemas, 1);
        _maxCachedSchemas = _config.MaxCachedSchemas;
        _maxOverflowLogicalSchemas = Math.Max(3, _maxCachedSchemas);
        _schemaResolutionCache = new();
        _ownsClient = ownsClient;

        // Try to get schema from type T if it's a specific record
        _writerSchema = GetSchemaFromType();
        if (_writerSchema is not null)
            _specificWriter = AllocationFreeSpecificRecordWriter<T>.Create(_writerSchema);
        else if (typeof(ISpecificRecord).IsAssignableFrom(typeof(T)))
        {
            throw new NotSupportedException(
                $"Allocation-free SpecificRecord serialization requires a concrete type with a statically discoverable schema; {typeof(T)} requires trimming-unsafe runtime type discovery.");
        }
        RegisterAssociatedNameInvalidation();
    }

    internal int CachedGenericWriterCount => _dynamicSchemaCaches.Count;
    internal int CachedSpecificWriterCount => _specificWriter is null ? 0 : 1;
    internal int CachedDynamicSubjectSchemaCount => _dynamicSchemaCaches.Count;
    internal int CachedOverflowLogicalSchemaCount => Volatile.Read(ref _overflowDynamicSchemaCacheCount);
    internal int CachedSchemaIdCount => _schemaResolutionCache.CachedEntryCount;

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

        var avroSchema = GetSchemaForValue(value);
        var schemaEntry = GetSchemaForContext(
            context.Topic,
            context.Component == SerializationComponent.Key,
            avroSchema);
        var schemaId = schemaEntry.SchemaId;

        var codecState = AvroCodecThreadStateCache.Serialization ??= new AvroSerializationThreadState();
        if (_config.RuleExecutor is null)
        {
            SerializeDirect(value, ref destination, schemaId, codecState);
            return;
        }

        SerializeWithRuleExecutor(value, ref destination, context, schemaEntry, schemaId, avroSchema, codecState);
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
                    : Math.Max(codecState.PayloadSizeHint, payloadLength);
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
        AvroSchema avroSchema,
        AvroSerializationThreadState codecState)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        var memoryStream = codecState.BufferedStream;
        memoryStream.ResetForWriting(InitialAvroPayloadBufferSize);
        var taggedWorkspaceOperation = AvroTaggedFieldTransformerProvider.BeginOperation();

        try
        {
            var encoder = codecState.BufferedEncoder;

            WriteAvroValue(value, encoder);
            encoder.Flush();

            var avroPayloadLength = (int)memoryStream.Position;
            var payload = new ReadOnlyMemory<byte>(memoryStream.GetBuffer(), 0, avroPayloadLength);
            var taggedFieldTransformer = _taggedFieldTransformers.Get(schemaEntry.Schema!, avroSchema);
            var ruleContext = SchemaRegistryRuleContext.RentWithTaggedFieldTransformer(
                context.Topic,
                context.Component,
                schemaId,
                schemaEntry.Subject,
                schemaEntry.Schema,
                SchemaRegistryPayloadFormat.Avro,
                taggedFieldTransformer);
            try
            {
                payload = _config.RuleExecutor!.TransformSerializedPayload(payload, ruleContext);
            }
            finally
            {
                ruleContext.Return();
            }

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
            taggedWorkspaceOperation.Dispose();
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

    private SubjectSchemaIdCache.SubjectSchemaIdCacheEntry GetSchemaForContext(
        string topic,
        bool isKey,
        AvroSchema avroSchema)
    {
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
        if (_asyncSubjectNameStrategy is not null)
            return PrepareAssociatedCoreAsync(topic, isKey, avroSchema, cache, cancellationToken);

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

    private async ValueTask<ResolvedSchemaContext> PrepareAssociatedCoreAsync(
        string topic,
        bool isKey,
        AvroSchema avroSchema,
        SubjectSchemaIdCache cache,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAssociatedNameInvalidationRetries; attempt++)
        {
            var subject = await _asyncSubjectNameStrategy!.GetSubjectNameAsync(
                topic,
                GetRecordName(avroSchema),
                isKey,
                cancellationToken).ConfigureAwait(false);
            var schema = CreateRegistrySchema(avroSchema);
            var value = await ResolveSchemaAsync(subject, schema, cancellationToken).ConfigureAwait(false);
            if (ReferenceEquals(cache, GetSubjectSchemaIdCache(avroSchema)))
            {
                return ToResolvedContext(cache.CacheEntry(
                    topic,
                    isKey,
                    subject,
                    value.SchemaId,
                    value.Schema!));
            }

            cache = GetSubjectSchemaIdCache(avroSchema);
            if (cache.TryGet(topic, isKey, out var cached))
                return ToResolvedContext(cached);
        }

        throw new InvalidOperationException(
            "Associated-name cache changed repeatedly while preparing the Avro serializer.");
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

    private void WriteAvroValue(T value, AllocationFreeBinaryEncoder encoder)
    {
        switch (value)
        {
            case GenericRecord genericRecord:
                var genericWriter = GetGenericWriter(genericRecord.Schema);
                genericWriter.Write(genericRecord, encoder);
                break;

            case ISpecificRecord:
                (_specificWriter ?? throw new InvalidOperationException(
                    $"SpecificRecord type {typeof(T)} does not have a prepared allocation-free writer."))
                    .Write(value, encoder);
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
            var registeredSchema = _config.RuleExecutor is SchemaRegistryRuleExecutor
                ? await _schemaRegistry.GetSchemaAsync(schemaId, subject, cancellationToken).ConfigureAwait(false)
                : registrySchema;
            return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(schemaId, registeredSchema);
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

    private static AvroSchema GetSchemaFromValue(T value) =>
        value switch
        {
            ISpecificRecord specificRecord => specificRecord.Schema,
            GenericRecord genericRecord => genericRecord.Schema,
            _ => throw new InvalidOperationException(
                $"Cannot determine Avro schema for type {typeof(T)}")
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private AvroSchema GetSchemaForValue(T value) =>
        _writerSchema ?? GetSchemaFromValue(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SubjectSchemaIdCache GetSubjectSchemaIdCache(AvroSchema schema)
    {
        if (_writerSchema is not null)
            return Volatile.Read(ref _subjectSchemaIdCache);

        var last = Volatile.Read(ref _lastDynamicSchemaCache);
        if (last is not null && ReferenceEquals(Volatile.Read(ref last.LastSeenSchema), schema))
            return Volatile.Read(ref last.SubjectSchemaIdCache);

        var dynamicCache = GetDynamicSchemaCacheSlow(schema, last);
        return Volatile.Read(ref dynamicCache.SubjectSchemaIdCache);
    }

    private AllocationFreeGenericRecordWriter GetGenericWriter(AvroSchema schema) =>
        GetGenericDynamicSchemaCache(schema).Writer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DynamicSchemaCache GetGenericDynamicSchemaCache(AvroSchema schema)
    {
        var last = Volatile.Read(ref _lastDynamicSchemaCache);
        if (last is not null && ReferenceEquals(Volatile.Read(ref last.LastSeenSchema), schema))
            return last;

        return GetDynamicSchemaCacheSlow(schema, last);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private DynamicSchemaCache GetDynamicSchemaCacheSlow(
        AvroSchema schema,
        DynamicSchemaCache? last)
    {
        var previous = Volatile.Read(ref _previousDynamicSchemaCache);
        if (previous is not null && ReferenceEquals(Volatile.Read(ref previous.LastSeenSchema), schema))
            return PublishOverflowDynamicSchemaCache(previous, schema);

        if (_dynamicSchemaCachesByReference.TryGetValue(schema, out var strongEntry))
        {
            return PublishStrongDynamicSchemaCache(strongEntry, schema);
        }

        if (Volatile.Read(ref _weakDynamicSchemaCaches).TryGetValue(schema, out var weakEntry))
            return PublishOverflowDynamicSchemaCache(weakEntry, schema);

        if (last is not null &&
            AvroSchemaLogicalComparer.Instance.Equals(last.Key.Schema, schema))
        {
            IndexOverflowSchemaIdentity(last, schema);
            return PublishLastDynamicSchemaCache(last, schema);
        }
        if (previous is not null && AvroSchemaLogicalComparer.Instance.Equals(previous.Key.Schema, schema))
        {
            IndexOverflowSchemaIdentity(previous, schema);
            return PublishOverflowDynamicSchemaCache(previous, schema);
        }

        var key = DynamicSchemaKey.Create(schema);
        if (_dynamicSchemaCaches.TryGetValue(key, out var logicalEntry))
            return PublishStrongDynamicSchemaCache(logicalEntry, schema);

        if (_overflowDynamicSchemaCaches.TryGetValue(key, out var overflowEntry))
        {
            IndexOverflowSchemaIdentity(overflowEntry, schema);
            return PublishOverflowDynamicSchemaCache(overflowEntry, schema);
        }

        return AddDynamicSchemaCache(schema, key);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private DynamicSchemaCache AddDynamicSchemaCache(
        AvroSchema schema,
        DynamicSchemaKey key)
    {
        lock (_dynamicSchemaCacheMutationLock)
        {
            if (_dynamicSchemaCaches.TryGetValue(key, out var existingLogicalEntry))
                return PublishStrongDynamicSchemaCache(existingLogicalEntry, schema);

            if (_overflowDynamicSchemaCaches.TryGetValue(key, out var existingOverflowEntry))
            {
                IndexOverflowSchemaIdentity(existingOverflowEntry, schema);
                return PublishOverflowDynamicSchemaCache(existingOverflowEntry, schema);
            }

            if (_dynamicSchemaCacheCount < _maxCachedSchemas)
            {
                var created = new DynamicSchemaCache(key, isStrong: true);
                _dynamicSchemaCaches.TryAdd(key, created);
                _dynamicSchemaCachesByReference.TryAdd(schema, created);
                _dynamicSchemaCacheCount++;
                return PublishStrongDynamicSchemaCache(created, schema);
            }

            if (_overflowDynamicSchemaCacheCount == _maxOverflowLogicalSchemas)
            {
                var oldest = _overflowDynamicSchemaOrder.Dequeue();
                Volatile.Write(ref _hasEvictedOverflowLogicalSchemas, 1);
                _overflowDynamicSchemaCaches.TryRemove(oldest, out var evicted);
                Volatile.Write(ref evicted!.IsLogicallyCached, false);
                Volatile.Read(ref _weakDynamicSchemaCaches).TryAdd(
                    Volatile.Read(ref evicted.LastSeenSchema),
                    evicted);
                _overflowDynamicSchemaCacheCount--;
            }

            var overflow = new DynamicSchemaCache(key, isStrong: false);
            _overflowDynamicSchemaCaches.TryAdd(key, overflow);
            _overflowDynamicSchemaOrder.Enqueue(key);
            _overflowDynamicSchemaCacheCount++;
            Volatile.Read(ref _weakDynamicSchemaCaches).TryAdd(schema, overflow);
            return PublishOverflowDynamicSchemaCache(overflow, schema);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DynamicSchemaCache PublishLastDynamicSchemaCache(
        DynamicSchemaCache entry,
        AvroSchema schema)
    {
        Volatile.Write(ref entry.LastSeenSchema, schema);
        return entry;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DynamicSchemaCache PublishStrongDynamicSchemaCache(DynamicSchemaCache entry, AvroSchema schema)
    {
        Volatile.Write(ref entry.LastSeenSchema, schema);
        var last = Volatile.Read(ref _lastDynamicSchemaCache);
        if (!ReferenceEquals(last, entry))
        {
            if (Volatile.Read(ref _hasEvictedOverflowLogicalSchemas) != 0)
                IndexEvictedPreviousDynamicSchemaCache(entry);
            Volatile.Write(ref _previousDynamicSchemaCache, last);
            Volatile.Write(ref _lastDynamicSchemaCache, entry);
        }
        return entry;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DynamicSchemaCache PublishOverflowDynamicSchemaCache(DynamicSchemaCache entry, AvroSchema schema)
    {
        if (entry.IsStrong)
            return PublishStrongDynamicSchemaCache(entry, schema);

        Volatile.Write(ref entry.LastSeenSchema, schema);
        var last = Volatile.Read(ref _lastDynamicSchemaCache);
        if (!ReferenceEquals(last, entry))
        {
            if (Volatile.Read(ref _hasEvictedOverflowLogicalSchemas) != 0)
                IndexEvictedPreviousDynamicSchemaCache(entry);
            Volatile.Write(ref _previousDynamicSchemaCache, last);
            Volatile.Write(ref _lastDynamicSchemaCache, entry);
        }

        return entry;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IndexEvictedPreviousDynamicSchemaCache(DynamicSchemaCache entry)
    {
        var previous = Volatile.Read(ref _previousDynamicSchemaCache);
        if (previous is { IsStrong: false } &&
            !ReferenceEquals(previous, entry) &&
            !Volatile.Read(ref previous.IsLogicallyCached))
        {
            Volatile.Read(ref _weakDynamicSchemaCaches).TryAdd(
                Volatile.Read(ref previous.LastSeenSchema),
                previous);
        }
    }

    private void IndexOverflowSchemaIdentity(DynamicSchemaCache entry, AvroSchema schema)
    {
        if (!entry.IsStrong)
            Volatile.Read(ref _weakDynamicSchemaCaches).TryAdd(schema, entry);
    }

    private sealed class DynamicSchemaCache
    {
        internal DynamicSchemaCache(DynamicSchemaKey key, bool isStrong)
        {
            Key = key;
            LastSeenSchema = key.Schema;
            IsStrong = isStrong;
            IsLogicallyCached = true;
            SubjectSchemaIdCache = new SubjectSchemaIdCache();
            Writer = key.Schema is global::Avro.RecordSchema recordSchema
                ? new AllocationFreeGenericRecordWriter(recordSchema)
                : throw new global::Avro.AvroException(
                    $"GenericRecord serialization requires a record schema but received {key.Schema.Tag}.");
        }

        internal DynamicSchemaKey Key { get; }
        internal AvroSchema LastSeenSchema;
        internal bool IsStrong { get; }
        internal bool IsLogicallyCached;
        internal SubjectSchemaIdCache SubjectSchemaIdCache;
        internal AllocationFreeGenericRecordWriter Writer { get; }
    }

    private readonly record struct DynamicSchemaKey(AvroSchema Schema, int LogicalHashCode)
    {
        internal static DynamicSchemaKey Create(AvroSchema schema) =>
            new(schema, AvroSchemaLogicalComparer.Instance.GetHashCode(schema));
    }

    private sealed class DynamicSchemaKeyComparer : IEqualityComparer<DynamicSchemaKey>
    {
        internal static readonly DynamicSchemaKeyComparer Instance = new();

        private DynamicSchemaKeyComparer() { }

        public bool Equals(DynamicSchemaKey x, DynamicSchemaKey y) =>
            x.LogicalHashCode == y.LogicalHashCode &&
            AvroSchemaLogicalComparer.Instance.Equals(x.Schema, y.Schema);

        public int GetHashCode(DynamicSchemaKey obj) => obj.LogicalHashCode;
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
    private void RegisterAssociatedNameInvalidation()
    {
        if (_asyncSubjectNameStrategy is AssociatedNameStrategy strategy)
            strategy.RegisterCacheInvalidationTarget(this);
    }

    void IAssociatedNameCacheInvalidationTarget.InvalidateAssociatedNameCache()
    {
        Volatile.Write(ref _subjectSchemaIdCache, new SubjectSchemaIdCache());
        lock (_dynamicSchemaCacheMutationLock)
        {
            foreach (var cache in _dynamicSchemaCaches.Values)
                Volatile.Write(ref cache.SubjectSchemaIdCache, new SubjectSchemaIdCache());
            foreach (var cache in _overflowDynamicSchemaCaches.Values)
                Volatile.Write(ref cache.SubjectSchemaIdCache, new SubjectSchemaIdCache());

            var last = Volatile.Read(ref _lastDynamicSchemaCache);
            if (last is not null)
                Volatile.Write(ref last.SubjectSchemaIdCache, new SubjectSchemaIdCache());
            var previous = Volatile.Read(ref _previousDynamicSchemaCache);
            if (previous is not null)
                Volatile.Write(ref previous.SubjectSchemaIdCache, new SubjectSchemaIdCache());

            Volatile.Write(
                ref _weakDynamicSchemaCaches,
                new ConditionalWeakTable<AvroSchema, DynamicSchemaCache>());
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}
