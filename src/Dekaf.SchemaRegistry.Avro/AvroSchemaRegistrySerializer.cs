using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
    private static readonly SpecificDefaultWriter SharedSpecificWriter = new(AvroSchema.Parse("\"null\""));

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly AvroSerializerConfig _config;
    private readonly bool _ownsClient;
    private readonly ConcurrentDictionary<SchemaIdCacheKey, int> _schemaIdCache = new();
    private readonly ConcurrentDictionary<SchemaIdCacheKey, Lazy<Task<int>>> _inFlightSchemaIds = new();
    private readonly SubjectSchemaIdCache _subjectSchemaIdCache = new();
    private readonly ConcurrentDictionary<DynamicSchemaKey, DynamicSchemaCache> _dynamicSchemaCaches =
        new(DynamicSchemaKeyComparer.Instance);
    private readonly ConcurrentDictionary<AvroSchema, DynamicSchemaCache> _dynamicSchemaCachesByReference =
        new(AvroSchemaReferenceComparer.Instance);
    private readonly ConcurrentDictionary<DynamicSchemaKey, DynamicSchemaCache> _overflowDynamicSchemaCaches =
        new(DynamicSchemaKeyComparer.Instance);
    private readonly ConditionalWeakTable<AvroSchema, DynamicSchemaCache> _weakDynamicSchemaCaches = new();
    private readonly Queue<DynamicSchemaKey> _overflowDynamicSchemaOrder = new();
    private readonly object _dynamicSchemaCacheMutationLock = new();
    private readonly int _maxCachedSchemas;
    private readonly int _maxOverflowLogicalSchemas;
    private readonly AvroSchema? _writerSchema;
    private int _dynamicSchemaCacheCount;
    private int _overflowDynamicSchemaCacheCount;
    private int _hasEvictedOverflowLogicalSchemas;
    private int _schemaIdCacheCount;
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
        ArgumentOutOfRangeException.ThrowIfLessThan(_config.MaxCachedSchemas, 1);
        _maxCachedSchemas = _config.MaxCachedSchemas;
        _maxOverflowLogicalSchemas = Math.Max(3, _maxCachedSchemas);
        _ownsClient = ownsClient;

        // Try to get schema from type T if it's a specific record
        _writerSchema = GetSchemaFromType();
    }

    internal int CachedGenericWriterCount => _dynamicSchemaCaches.Count;
    internal static int CachedSpecificWriterCount =>
        typeof(ISpecificRecord).IsAssignableFrom(typeof(T)) ? 1 : 0;
    internal int CachedDynamicSubjectSchemaCount => _dynamicSchemaCaches.Count;
    internal int CachedOverflowLogicalSchemaCount => Volatile.Read(ref _overflowDynamicSchemaCacheCount);
    internal int CachedSchemaIdCount => Volatile.Read(ref _schemaIdCacheCount);

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
        var subjectSchemaIdCache = GetSubjectSchemaIdCache(value, out var avroSchema);
        var subject = GetSubjectName(topic, isKey, avroSchema);
        var schema = CreateRegistrySchema(avroSchema);
        var schemaId = await GetOrFetchSchemaIdAsync(subject, schema, cancellationToken).ConfigureAwait(false);
        subjectSchemaIdCache.Cache(topic, isKey, subject, schemaId, schema);
        return schemaId;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Delegates to <see cref="WarmupAsync"/>. Returns a synchronously completed <see cref="ValueTask"/>
    /// with no allocation once the subject's schema ID is cached, so the producer's steady-state hot path
    /// stays fully synchronous and only the first value per subject pays the async schema fetch.
    /// </remarks>
    public ValueTask PrepareAsync(T value, SerializationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        var isKey = context.Component == SerializationComponent.Key;
        var subjectSchemaIdCache = GetSubjectSchemaIdCache(value, out _);
        if (subjectSchemaIdCache.Contains(context.Topic, isKey))
        {
            return ValueTask.CompletedTask;
        }

        return new ValueTask(WarmupAsync(context.Topic, value, isKey, cancellationToken));
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
        var subjectSchemaIdCache = GetSubjectSchemaIdCache(value, out var avroSchema);
        var state = new SubjectSchemaIdState(this, avroSchema);
        return subjectSchemaIdCache.GetOrAdd(
            topic,
            isKey,
            state,
            static (state, topic, isKey) => state.Serializer.GetSubjectName(topic, isKey, state.Schema),
            static (state, subject) => state.Serializer.GetSchemaIdCacheValue(subject, state.Schema));
    }

    private readonly record struct SubjectSchemaIdState(
        AvroSchemaRegistrySerializer<T> Serializer,
        AvroSchema Schema);

    private SubjectSchemaIdCache.SubjectSchemaIdCacheValue GetSchemaIdCacheValue(
        string subject,
        AvroSchema avroSchema)
    {
        var schema = CreateRegistrySchema(avroSchema);
        return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(GetSchemaIdCached(subject, schema), schema);
    }

    private void WriteAvroValue(T value, BinaryEncoder encoder)
    {
        switch (value)
        {
            case ISpecificRecord specificRecord:
                var specificWriter = GetSpecificWriter();
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

    private int GetSchemaIdCached(string subject, RegistrySchema schema)
    {
        var key = new SchemaIdCacheKey(subject, schema.SchemaString);
        if (_schemaIdCache.TryGetValue(key, out var cached))
            return cached;

        var lazyTask = GetOrAddInFlightSchemaId(key, schema);
        if (_schemaIdCache.TryGetValue(key, out cached))
        {
            RemoveInFlightSchemaId(key, lazyTask);
            return cached;
        }

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
        try
        {
            return task.WaitAsync(SchemaRegistryTimeout).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (TimeoutException)
        {
            ObserveFault(task);
            throw;
        }
    }

    private async Task<int> GetOrFetchSchemaIdAsync(
        string subject,
        RegistrySchema schema,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = new SchemaIdCacheKey(subject, schema.SchemaString);
        if (_schemaIdCache.TryGetValue(key, out var cached))
            return cached;

        var lazyTask = GetOrAddInFlightSchemaId(key, schema);
        if (_schemaIdCache.TryGetValue(key, out cached))
        {
            RemoveInFlightSchemaId(key, lazyTask);
            return cached;
        }

        var task = lazyTask.Value;
        try
        {
            return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ObserveFault(task);
            throw;
        }
    }

    private Lazy<Task<int>> GetOrAddInFlightSchemaId(SchemaIdCacheKey key, RegistrySchema schema) =>
        _inFlightSchemaIds.GetOrAdd(
            key,
            static (cacheKey, state) => state.Owner.CreateSchemaIdLazy(cacheKey, state.Schema),
            (Owner: this, Schema: schema));

    private Lazy<Task<int>> CreateSchemaIdLazy(SchemaIdCacheKey key, RegistrySchema schema)
    {
        Lazy<Task<int>>? entry = null;
        entry = new Lazy<Task<int>>(() => FetchSchemaIdAsync(key, schema, entry!));
        return entry;
    }

    private static void ObserveFault(Task<int> task)
    {
        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private readonly record struct SchemaIdCacheKey(string Subject, string SchemaString);

    private async Task<int> FetchSchemaIdAsync(
        SchemaIdCacheKey key,
        RegistrySchema registrySchema,
        Lazy<Task<int>> entry)
    {
        var subject = key.Subject;
        try
        {
            int schemaId;
            if (_config.UseLatestVersion)
            {
                // Use latest schema from registry
                var registered = await _schemaRegistry.GetSchemaBySubjectAsync(subject, "latest", CancellationToken.None)
                    .ConfigureAwait(false);
                schemaId = registered.Id;
            }
            else if (_config.AutoRegisterSchemas)
            {
                // Register schema if auto-register is enabled
                schemaId = _config.NormalizeSchemas
                    ? await _schemaRegistry.GetOrRegisterSchemaAsync(
                        subject,
                        registrySchema,
                        normalize: true,
                        CancellationToken.None).ConfigureAwait(false)
                    : await _schemaRegistry.GetOrRegisterSchemaAsync(
                        subject,
                        registrySchema,
                        CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                // Get existing schema ID from registry
                var existing = await _schemaRegistry.GetSchemaBySubjectAsync(subject, "latest", CancellationToken.None)
                    .ConfigureAwait(false);
                schemaId = existing.Id;
            }

            if (TryReserveCacheSlot(ref _schemaIdCacheCount))
            {
                if (!_schemaIdCache.TryAdd(key, schemaId))
                    Interlocked.Decrement(ref _schemaIdCacheCount);
            }

            return schemaId;
        }
        finally
        {
            RemoveInFlightSchemaId(key, entry);
        }
    }

    private void RemoveInFlightSchemaId(SchemaIdCacheKey key, Lazy<Task<int>> entry) =>
        ((ICollection<KeyValuePair<SchemaIdCacheKey, Lazy<Task<int>>>>)_inFlightSchemaIds)
        .Remove(new KeyValuePair<SchemaIdCacheKey, Lazy<Task<int>>>(key, entry));

    private static RegistrySchema CreateRegistrySchema(AvroSchema avroSchema)
    {
        return new RegistrySchema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = avroSchema.ToString()
        };
    }

    private SubjectSchemaIdCache GetSubjectSchemaIdCache(T value, out AvroSchema schema)
    {
        if (_writerSchema is { } writerSchema)
        {
            schema = writerSchema;
            return _subjectSchemaIdCache;
        }

        switch (value)
        {
            case ISpecificRecord specificRecord:
                schema = specificRecord.Schema;
                return GetSpecificDynamicSchemaCache(schema).SubjectSchemaIdCache;

            case GenericRecord genericRecord:
                schema = genericRecord.Schema;
                return GetGenericDynamicSchemaCache(schema).SubjectSchemaIdCache;

            default:
                throw new InvalidOperationException(
                    $"Type {typeof(T)} is not supported. Must be ISpecificRecord or GenericRecord.");
        }
    }

    private GenericDatumWriter<GenericRecord> GetGenericWriter(AvroSchema schema) =>
        GetGenericDynamicSchemaCache(schema).Writer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SpecificDefaultWriter GetSpecificWriter() => SharedSpecificWriter;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DynamicSchemaCache GetGenericDynamicSchemaCache(AvroSchema schema)
    {
        var last = Volatile.Read(ref _lastDynamicSchemaCache);
        if (last is not null)
        {
            var lastSchema = Volatile.Read(ref last.LastSeenSchema);
            if (ReferenceEquals(lastSchema, schema))
                return last;

            var previous = Volatile.Read(ref _previousDynamicSchemaCache);
            if (previous is not null && ReferenceEquals(Volatile.Read(ref previous.LastSeenSchema), schema))
                return PublishStrongDynamicSchemaCache(previous, schema);

            if (AvroSchemaLogicalComparer.Instance.Equals(lastSchema, schema))
                return PublishLastDynamicSchemaCache(last, schema);
        }

        return GetDynamicSchemaCacheSlow(schema, last);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DynamicSchemaCache GetSpecificDynamicSchemaCache(AvroSchema schema)
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

        if (_weakDynamicSchemaCaches.TryGetValue(schema, out var weakEntry))
            return PublishOverflowDynamicSchemaCache(weakEntry, schema);

        if (last is not null &&
            AvroSchemaLogicalComparer.Instance.Equals(last.Key.Schema, schema))
        {
            return PublishLastDynamicSchemaCache(last, schema);
        }
        if (previous is not null && AvroSchemaLogicalComparer.Instance.Equals(previous.Key.Schema, schema))
            return PublishOverflowDynamicSchemaCache(previous, schema);

        var key = DynamicSchemaKey.Create(schema);
        if (_dynamicSchemaCaches.TryGetValue(key, out var logicalEntry))
            return PublishStrongDynamicSchemaCache(logicalEntry, schema);

        if (_overflowDynamicSchemaCaches.TryGetValue(key, out var overflowEntry))
            return PublishOverflowDynamicSchemaCache(overflowEntry, schema);

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
                return PublishOverflowDynamicSchemaCache(existingOverflowEntry, schema);

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
                _weakDynamicSchemaCaches.TryAdd(
                    Volatile.Read(ref evicted.LastSeenSchema),
                    evicted);
                _overflowDynamicSchemaCacheCount--;
            }

            var overflow = new DynamicSchemaCache(key, isStrong: false);
            _overflowDynamicSchemaCaches.TryAdd(key, overflow);
            _overflowDynamicSchemaOrder.Enqueue(key);
            _overflowDynamicSchemaCacheCount++;
            _weakDynamicSchemaCaches.TryAdd(schema, overflow);
            return PublishOverflowDynamicSchemaCache(overflow, schema);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryReserveCacheSlot(ref int cacheCount)
    {
        while (true)
        {
            var count = Volatile.Read(ref cacheCount);
            if (count >= _maxCachedSchemas)
                return false;

            if (Interlocked.CompareExchange(ref cacheCount, count + 1, count) == count)
                return true;
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
            _weakDynamicSchemaCaches.TryAdd(
                Volatile.Read(ref previous.LastSeenSchema),
                previous);
        }
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
            Writer = new GenericDatumWriter<GenericRecord>(key.Schema);
        }

        internal DynamicSchemaKey Key { get; }
        internal AvroSchema LastSeenSchema;
        internal bool IsStrong { get; }
        internal bool IsLogicallyCached;
        internal SubjectSchemaIdCache SubjectSchemaIdCache { get; }
        internal GenericDatumWriter<GenericRecord> Writer { get; }
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
    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}
