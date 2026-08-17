using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Dekaf.Serialization;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// Base serializer that integrates with Schema Registry.
/// Handles the wire format: [magic byte (0)] [schema ID (4 bytes)] [payload].
/// </summary>
/// <remarks>
/// <para>
/// This serializer caches schema IDs in a bounded concurrent cache.
/// The first time a schema is needed for a particular subject, a synchronous blocking call to the
/// Schema Registry is made. After the first fetch, subsequent serialization calls for the same subject
/// use the cached schema ID without any blocking or allocation. Multiple subjects are cached concurrently.
/// </para>
/// <para>
/// The blocking call includes a timeout to prevent indefinite hangs. If the timeout is exceeded,
/// a <see cref="TimeoutException"/> is thrown.
/// </para>
/// </remarks>
/// <typeparam name="T">The type to serialize.</typeparam>
public sealed class SchemaRegistrySerializer<T> :
    ISerializer<T>,
    IAsyncSerializerPreparer<T>,
    IAsyncDisposable
{
    private const byte MagicByte = 0x00;

    /// <summary>
    /// Default timeout for Schema Registry operations (30 seconds).
    /// </summary>
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly Action<T, IBufferWriter<byte>> _serialize;
    private readonly Func<string, Schema> _getSchema;
    private readonly bool _schemaFactoryIgnoresSubject;
    private readonly SubjectNameStrategy _subjectNameStrategy;
    private readonly ISubjectNameStrategy? _customSubjectNameStrategy;
    private readonly bool _autoRegisterSchemas;
    private readonly bool _normalizeSchemas;
    private readonly bool _useLegacySubjectNames;
    private readonly bool _ownsClient;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;
    private CachedFactorySchema? _subjectIndependentSchema;

    private readonly SchemaResolutionCache<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> _schemaResolutionCache = new();
    private readonly SubjectSchemaCache? _subjectSchemaCache;
    private readonly SubjectSchemaIdCache _subjectSchemaIdCache = new();

    /// <summary>
    /// Creates a new Schema Registry serializer.
    /// </summary>
    public SchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        Action<T, IBufferWriter<byte>> serialize,
        Func<string, Schema> getSchema,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null,
        bool normalizeSchemas = false)
        : this(
            schemaRegistry,
            serialize,
            getSchema,
            useLegacySubjectNames: false,
            subjectNameStrategy,
            autoRegisterSchemas,
            ownsClient,
            ruleExecutor,
            normalizeSchemas)
    {
    }

    /// <summary>
    /// Creates a new Schema Registry serializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="serialize">Action to serialize the value by writing to the provided buffer (without wire format).</param>
    /// <param name="getSchema">Function to get the schema for a type.</param>
    /// <param name="subjectNameStrategy">Strategy for determining subject names.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <param name="ownsClient">Whether this serializer owns the client and should dispose it.</param>
    /// <param name="ruleExecutor">Optional rule executor applied to payload bytes.</param>
    /// <param name="normalizeSchemas">Whether to normalize schemas during registration.</param>
    /// <param name="useLegacySubjectNames">Whether RecordName and TopicRecordName should use Dekaf's legacy -key/-value suffixes.</param>
    public SchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        Action<T, IBufferWriter<byte>> serialize,
        Func<string, Schema> getSchema,
        bool useLegacySubjectNames,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null,
        bool normalizeSchemas = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _serialize = serialize ?? throw new ArgumentNullException(nameof(serialize));
        _getSchema = getSchema ?? throw new ArgumentNullException(nameof(getSchema));
        _subjectSchemaCache = new SubjectSchemaCache();
        _subjectNameStrategy = subjectNameStrategy;
        _autoRegisterSchemas = autoRegisterSchemas;
        _normalizeSchemas = normalizeSchemas;
        _useLegacySubjectNames = useLegacySubjectNames;
        _ownsClient = ownsClient;
        _ruleExecutor = ruleExecutor;
    }

    /// <summary>
    /// Creates a new Schema Registry serializer whose schema factory is independent of the subject name.
    /// </summary>
    public SchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        Action<T, IBufferWriter<byte>> serialize,
        Func<Schema> getSchema,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null,
        bool normalizeSchemas = false)
        : this(
            schemaRegistry,
            serialize,
            getSchema,
            useLegacySubjectNames: false,
            subjectNameStrategy,
            autoRegisterSchemas,
            ownsClient,
            ruleExecutor,
            normalizeSchemas)
    {
    }

    /// <summary>
    /// Creates a new Schema Registry serializer whose schema factory is independent of the subject name.
    /// </summary>
    public SchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        Action<T, IBufferWriter<byte>> serialize,
        Func<Schema> getSchema,
        bool useLegacySubjectNames,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null,
        bool normalizeSchemas = false)
    {
        ArgumentNullException.ThrowIfNull(getSchema);
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _serialize = serialize ?? throw new ArgumentNullException(nameof(serialize));
        _getSchema = _ => getSchema();
        _schemaFactoryIgnoresSubject = true;
        _subjectNameStrategy = subjectNameStrategy;
        _autoRegisterSchemas = autoRegisterSchemas;
        _normalizeSchemas = normalizeSchemas;
        _useLegacySubjectNames = useLegacySubjectNames;
        _ownsClient = ownsClient;
        _ruleExecutor = ruleExecutor;
    }

    /// <summary>
    /// Creates a new Schema Registry serializer with a custom subject name strategy.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="serialize">Action to serialize the value by writing to the provided buffer (without wire format).</param>
    /// <param name="getSchema">Function to get the schema for a type.</param>
    /// <param name="customSubjectNameStrategy">Custom strategy for determining subject names.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <param name="ownsClient">Whether this serializer owns the client and should dispose it.</param>
    /// <param name="ruleExecutor">Optional rule executor applied to payload bytes.</param>
    /// <param name="normalizeSchemas">Whether to normalize schemas during registration.</param>
    public SchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        Action<T, IBufferWriter<byte>> serialize,
        Func<string, Schema> getSchema,
        ISubjectNameStrategy customSubjectNameStrategy,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null,
        bool normalizeSchemas = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _serialize = serialize ?? throw new ArgumentNullException(nameof(serialize));
        _getSchema = getSchema ?? throw new ArgumentNullException(nameof(getSchema));
        _subjectSchemaCache = new SubjectSchemaCache();
        _customSubjectNameStrategy = customSubjectNameStrategy ?? throw new ArgumentNullException(nameof(customSubjectNameStrategy));
        _autoRegisterSchemas = autoRegisterSchemas;
        _normalizeSchemas = normalizeSchemas;
        _ownsClient = ownsClient;
        _ruleExecutor = ruleExecutor;
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
        if (_subjectSchemaIdCache.TryGet(topic, isKey, out var cached))
            return new ValueTask<ResolvedSchemaContext>(ToResolvedContext(cached));

        return PrepareCoreAsync(topic, isKey, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask PrepareAsync(
        T value,
        SerializationContext context,
        CancellationToken cancellationToken = default)
    {
        var preparation = PrepareAsync(
            context.Topic,
            value,
            context.Component == SerializationComponent.Key,
            cancellationToken);
        if (preparation.IsCompletedSuccessfully)
        {
            _ = preparation.Result;
            return ValueTask.CompletedTask;
        }

        return AwaitPreparationAsync(preparation);

        static async ValueTask AwaitPreparationAsync(ValueTask<ResolvedSchemaContext> preparation) =>
            _ = await preparation.ConfigureAwait(false);
    }

    public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        var schemaEntry = GetSchemaForContext(context.Topic, context.Component == SerializationComponent.Key);
        var schemaId = schemaEntry.SchemaId;

        var payloadBuffer = SchemaRegistryBuffers.PayloadBuffer ??= new ArrayBufferWriter<byte>(initialCapacity: 4096);
        payloadBuffer.ResetWrittenCount();
        _serialize(value, payloadBuffer);
        // Drop an oversized buffer so a single large message doesn't permanently hold capacity on this thread.
        if (payloadBuffer.Capacity > 1024 * 1024)
            SchemaRegistryBuffers.PayloadBuffer = null;

        var payload = payloadBuffer.WrittenMemory;
        if (_ruleExecutor is not null)
        {
            var ruleContext = SchemaRegistryRuleContext.Rent(
                context.Topic,
                context.Component,
                schemaId,
                schemaEntry.Subject,
                schemaEntry.Schema,
                SchemaRegistryPayloadFormat.Custom);
            try
            {
                payload = _ruleExecutor.TransformSerializedPayload(payload, ruleContext);
            }
            finally
            {
                ruleContext.Return();
            }
        }

        // Write wire format: [0x00] [schema ID] [payload]
        var totalSize = 1 + 4 + payload.Length;
        var span = destination.GetSpan(totalSize);

        span[0] = MagicByte;
        BinaryPrimitives.WriteInt32BigEndian(span.Slice(1, 4), schemaId);
        payload.Span.CopyTo(span.Slice(5));

        destination.Advance(totalSize);
    }

    private SubjectSchemaIdCache.SubjectSchemaIdCacheEntry GetSchemaForContext(string topic, bool isKey)
        => _subjectSchemaIdCache.GetOrAdd(
            topic,
            isKey,
            this,
            static (serializer, topic, isKey) => serializer.ResolveSchema(topic, isKey));

    private SubjectSchemaIdCache.SubjectSchemaIdCacheEntry ResolveSchema(string topic, bool isKey)
    {
        var resolved = ResolveSubjectAndSchema(topic, isKey);
        var value = ResolveSchemaCached(resolved.Subject, resolved.Schema);
        return new SubjectSchemaIdCache.SubjectSchemaIdCacheEntry(
            new SubjectSchemaIdCache.SubjectSchemaIdCacheKey(topic, isKey),
            resolved.Subject,
            value.SchemaId,
            value.Schema);
    }

    private ValueTask<ResolvedSchemaContext> PrepareCoreAsync(
        string topic,
        bool isKey,
        CancellationToken cancellationToken)
    {
        var resolved = ResolveSubjectAndSchema(topic, isKey);
        var resolution = ResolveSchemaAsync(
            resolved.Subject,
            resolved.Schema,
            cancellationToken);
        if (resolution.IsCompletedSuccessfully)
        {
            var value = resolution.Result;
            return new ValueTask<ResolvedSchemaContext>(ToResolvedContext(
                _subjectSchemaIdCache.CacheEntry(
                    topic,
                    isKey,
                    resolved.Subject,
                    value.SchemaId,
                    value.Schema!)));
        }

        return AwaitSchemaAsync(this, topic, isKey, resolved.Subject, resolution);

        static async ValueTask<ResolvedSchemaContext> AwaitSchemaAsync(
            SchemaRegistrySerializer<T> serializer,
            string topic,
            bool isKey,
            string subject,
            ValueTask<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> resolution)
        {
            var value = await resolution.ConfigureAwait(false);
            return ToResolvedContext(serializer._subjectSchemaIdCache.CacheEntry(
                topic,
                isKey,
                subject,
                value.SchemaId,
                value.Schema!));
        }
    }

    private ResolvedSubjectSchema ResolveSubjectAndSchema(string topic, bool isKey)
    {
        var fallbackRecordName = typeof(T).FullName ?? typeof(T).Name;
        var subject = GetSubjectName(topic, fallbackRecordName, isKey);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var factorySchema = GetSchema(subject, fallbackRecordName);
            var schema = factorySchema.Schema;
            var recordName = factorySchema.RecordName;
            var resolvedSubject = string.Equals(recordName, fallbackRecordName, StringComparison.Ordinal)
                ? subject
                : GetSubjectName(topic, recordName, isKey);

            if (_schemaFactoryIgnoresSubject || string.Equals(resolvedSubject, subject, StringComparison.Ordinal))
                return new ResolvedSubjectSchema(resolvedSubject, schema);

            subject = resolvedSubject;
        }

        throw new InvalidOperationException("The schema callback did not resolve to a stable subject name.");
    }

    private FactorySchema GetSchema(string subject, string fallbackRecordName)
    {
        if (!_schemaFactoryIgnoresSubject)
            return _subjectSchemaCache!.GetOrAdd(subject, fallbackRecordName, _getSchema);

        var cached = Volatile.Read(ref _subjectIndependentSchema);
        if (cached is not null)
            return cached.Value;

        var schema = _getSchema(subject);
        var candidate = new CachedFactorySchema(new FactorySchema(
            schema,
            SubjectNameResolver.GetRecordName(schema, fallbackRecordName)));
        return (Interlocked.CompareExchange(ref _subjectIndependentSchema, candidate, null) ?? candidate).Value;
    }

    private sealed class SubjectSchemaCache
    {
        private const int FixedOverflowCapacity = 9;
        private const int TurnoverCapacity = 4;
        private readonly ConcurrentDictionary<string, FactorySchema> _cache = new(StringComparer.Ordinal);
        private readonly CachedSubjectSchema?[] _overflow = new CachedSubjectSchema?[FixedOverflowCapacity];
        private int _cacheCount;
        private int _turnoverCursor = -1;
        private readonly TurnoverSubjectSchema[] _turnover = CreateTurnoverSlots();
        private int _turnoverCount;

        internal FactorySchema GetOrAdd(
            string subject,
            string fallbackRecordName,
            Func<string, Schema> schemaFactory)
        {
            if (_cache.TryGetValue(subject, out var cached))
                return cached;
            if (TryGetOverflow(subject, out cached))
                return cached;

            var schema = schemaFactory(subject);
            var factorySchema = new FactorySchema(
                schema,
                SubjectNameResolver.GetRecordName(schema, fallbackRecordName));
            if (!TryReserveSlot())
                return PublishOverflow(subject, factorySchema);

            if (_cache.TryAdd(subject, factorySchema))
                return factorySchema;

            Interlocked.Decrement(ref _cacheCount);
            return _cache.TryGetValue(subject, out cached)
                ? cached
                : PublishOverflow(subject, factorySchema);
        }

        private bool TryGetOverflow(string subject, out FactorySchema factorySchema)
        {
            for (var index = 0; index < FixedOverflowCapacity; index++)
            {
                var cached = Volatile.Read(ref _overflow[index]);
                if (cached is not null && string.Equals(cached.Subject, subject, StringComparison.Ordinal))
                {
                    factorySchema = cached.Value;
                    return true;
                }
            }

            for (var index = 0; index < TurnoverCapacity; index++)
            {
                if (_turnover[index].TryGet(subject, out factorySchema))
                    return true;
            }

            return TryGetRetainedOverflow(subject, out factorySchema);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetRetainedOverflow(string subject, out FactorySchema factorySchema)
        {
            for (var index = 0; index < TurnoverCapacity; index++)
            {
                if (_turnover[index].TryGetRetained(subject, out factorySchema))
                    return true;
            }

            factorySchema = default;
            return false;
        }

        private FactorySchema PublishOverflow(string subject, FactorySchema factorySchema)
        {
            CachedSubjectSchema? candidate = null;
            for (var index = 0; index < FixedOverflowCapacity; index++)
            {
                var cached = Volatile.Read(ref _overflow[index]);
                if (cached is null)
                {
                    candidate ??= new CachedSubjectSchema(subject, factorySchema);
                    cached = Interlocked.CompareExchange(ref _overflow[index], candidate, null);
                    if (cached is null)
                        return factorySchema;
                }

                if (string.Equals(cached.Subject, subject, StringComparison.Ordinal))
                    return cached.Value;
            }

            if (Volatile.Read(ref _turnoverCount) >= TurnoverCapacity)
            {
                for (var index = 0; index < TurnoverCapacity; index++)
                {
                    if (_turnover[index].TryGet(subject, out var cached))
                        return cached;
                }

                if (TryGetRetainedOverflow(subject, out var retained))
                    return retained;

                return PublishTurnover(subject, factorySchema);
            }

            for (var index = 0; index < TurnoverCapacity; index++)
            {
                var turnover = _turnover[index];
                if (turnover.TryGet(subject, out var cached))
                    return cached;

                if (turnover.TryPublishEmpty(subject, factorySchema))
                {
                    Interlocked.Increment(ref _turnoverCount);
                    return factorySchema;
                }

                if (turnover.TryGet(subject, out cached))
                    return cached;
            }

            return PublishTurnover(subject, factorySchema);
        }

        private FactorySchema PublishTurnover(string subject, FactorySchema factorySchema)
        {
            var startIndex = Interlocked.Increment(ref _turnoverCursor);
            for (var attempt = 0; attempt < TurnoverCapacity; attempt++)
            {
                var index = (startIndex + attempt) & (TurnoverCapacity - 1);
                if (_turnover[index].TryPublish(subject, factorySchema))
                    break;
            }

            return factorySchema;
        }

        private static TurnoverSubjectSchema[] CreateTurnoverSlots()
        {
            var slots = new TurnoverSubjectSchema[TurnoverCapacity];
            for (var index = 0; index < slots.Length; index++)
                slots[index] = new TurnoverSubjectSchema();

            return slots;
        }

        private bool TryReserveSlot()
        {
            while (true)
            {
                var count = Volatile.Read(ref _cacheCount);
                if (count >= SubjectSchemaIdCache.MaxCachedEntries)
                    return false;

                if (Interlocked.CompareExchange(ref _cacheCount, count + 1, count) == count)
                    return true;
            }
        }
    }

    private readonly record struct FactorySchema(Schema Schema, string RecordName);

    private sealed class CachedFactorySchema(FactorySchema value)
    {
        internal FactorySchema Value { get; } = value;
    }

    private sealed class CachedSubjectSchema(string subject, FactorySchema value)
    {
        internal string Subject { get; } = subject;
        internal FactorySchema Value { get; } = value;
    }

    private sealed class TurnoverSubjectSchema
    {
        private string? _firstSubject;
        private string? _secondSubject;
        private FactorySchema _firstValue;
        private FactorySchema _secondValue;
        private int _activeIndex;
        private int _version;

        internal bool TryGet(string subject, out FactorySchema value)
        {
            var version = Volatile.Read(ref _version);
            if (version is 0 or 1)
            {
                value = default;
                return false;
            }

            var activeIndex = Volatile.Read(ref _activeIndex);
            var candidateSubject = activeIndex == 0 ? _firstSubject : _secondSubject;
            var candidateValue = activeIndex == 0 ? _firstValue : _secondValue;
            if (activeIndex != Volatile.Read(ref _activeIndex)
                || version != Volatile.Read(ref _version)
                || !string.Equals(candidateSubject, subject, StringComparison.Ordinal))
            {
                value = default;
                return false;
            }

            value = candidateValue;
            return true;
        }

        internal bool TryGetRetained(string subject, out FactorySchema value)
        {
            var version = Volatile.Read(ref _version);
            if (version == 0 || (version & 1) != 0)
            {
                value = default;
                return false;
            }

            var activeIndex = Volatile.Read(ref _activeIndex);
            var retainedIndex = activeIndex ^ 1;
            var candidateSubject = retainedIndex == 0 ? _firstSubject : _secondSubject;
            var candidateValue = retainedIndex == 0 ? _firstValue : _secondValue;
            if (activeIndex != Volatile.Read(ref _activeIndex)
                || version != Volatile.Read(ref _version)
                || !string.Equals(candidateSubject, subject, StringComparison.Ordinal))
            {
                value = default;
                return false;
            }

            value = candidateValue;
            return true;
        }

        internal bool TryPublish(string subject, FactorySchema value)
        {
            var version = Volatile.Read(ref _version);
            if ((version & 1) != 0
                || Interlocked.CompareExchange(ref _version, unchecked(version + 1), version) != version)
            {
                return false;
            }

            // Readers keep using the active buffer while this inactive buffer is populated.
            var nextIndex = Volatile.Read(ref _activeIndex) ^ 1;
            if (nextIndex == 0)
            {
                _firstSubject = subject;
                _firstValue = value;
            }
            else
            {
                _secondSubject = subject;
                _secondValue = value;
            }

            Volatile.Write(ref _activeIndex, nextIndex);
            var publishedVersion = unchecked(version + 2);
            Volatile.Write(ref _version, publishedVersion == 0 ? 2 : publishedVersion);
            return true;
        }

        internal bool TryPublishEmpty(string subject, FactorySchema value)
        {
            if (Interlocked.CompareExchange(ref _version, 1, 0) != 0)
                return false;

            _firstSubject = subject;
            _firstValue = value;
            Volatile.Write(ref _activeIndex, 0);
            Volatile.Write(ref _version, 2);
            return true;
        }
    }

    private SubjectSchemaIdCache.SubjectSchemaIdCacheValue ResolveSchemaCached(string subject, Schema schema)
        => _schemaResolutionCache.Resolve(
            subject,
            schema,
            this,
            static (serializer, resolvedSubject, resolvedSchema) =>
                serializer.FetchSchemaWithTimeoutAsync(resolvedSubject, resolvedSchema),
            SchemaRegistryTimeout);

    private ValueTask<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> ResolveSchemaAsync(
        string subject,
        Schema schema,
        CancellationToken cancellationToken) =>
        _schemaResolutionCache.ResolveAsync(
            subject,
            schema,
            this,
            static (serializer, resolvedSubject, resolvedSchema) =>
                serializer.FetchSchemaWithTimeoutAsync(resolvedSubject, resolvedSchema),
            cancellationToken);

    private Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> FetchSchemaWithTimeoutAsync(
        string subject,
        Schema schema) =>
        SchemaRegistryOperationTimeout.ExecuteAsync(
            cancellationToken => FetchSchemaAsync(subject, schema, cancellationToken),
            SchemaRegistryTimeout,
            "Schema Registry resolution timed out.");

    private async Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> FetchSchemaAsync(
        string subject,
        Schema schema,
        CancellationToken cancellationToken)
    {
        if (_autoRegisterSchemas)
        {
            var schemaId = _normalizeSchemas
                ? await _schemaRegistry.GetOrRegisterSchemaAsync(
                    subject,
                    schema,
                    normalize: true,
                    cancellationToken).ConfigureAwait(false)
                : await _schemaRegistry.GetOrRegisterSchemaAsync(
                    subject,
                    schema,
                    cancellationToken).ConfigureAwait(false);
            var registeredSchema = _ruleExecutor is SchemaRegistryRuleExecutor
                ? await _schemaRegistry.GetSchemaAsync(schemaId, subject, cancellationToken).ConfigureAwait(false)
                : schema;
            return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(schemaId, registeredSchema);
        }

        var registered = await _schemaRegistry.GetSchemaBySubjectAsync(
                subject,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(
            registered.Id,
            registered.Schema);
    }

    private static ResolvedSchemaContext ToResolvedContext(
        SubjectSchemaIdCache.SubjectSchemaIdCacheEntry entry) =>
        new(entry.Subject!, entry.SchemaId, entry.Schema!);

    private readonly record struct ResolvedSubjectSchema(string Subject, Schema Schema);

    private string GetSubjectName(string topic, string recordName, bool isKey)
    {
        if (_customSubjectNameStrategy is not null)
        {
            return _customSubjectNameStrategy.GetSubjectName(topic, recordName, isKey);
        }

        return SubjectNameResolver.GetSubjectName(
            _subjectNameStrategy,
            topic,
            recordName,
            isKey,
            _useLegacySubjectNames);
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// Non-generic holder for the thread-local serialization buffer.
/// Kept outside SchemaRegistrySerializer&lt;T&gt; so all generic instantiations
/// share one buffer per thread rather than one per (type × thread).
internal static class SchemaRegistryBuffers
{
    [ThreadStatic]
    internal static ArrayBufferWriter<byte>? PayloadBuffer;
}

/// <summary>
/// Base deserializer that integrates with Schema Registry.
/// Handles the wire format: [magic byte (0)] [schema ID (4 bytes)] [payload].
/// </summary>
/// <remarks>
/// <para>
/// This deserializer fetches the schema from Schema Registry on first access for each schema ID.
/// Schemas are cached internally by the Schema Registry client after first fetch.
/// </para>
/// <para>
/// The blocking call includes a timeout to prevent indefinite hangs. If the timeout is exceeded,
/// a <see cref="TimeoutException"/> is thrown.
/// </para>
/// </remarks>
/// <typeparam name="T">The type to deserialize.</typeparam>
public sealed class SchemaRegistryDeserializer<T> : IDeserializer<T>, IAsyncDisposable
{
    private const byte MagicByte = 0x00;
    private static readonly string FallbackRecordName = typeof(T).FullName ?? typeof(T).Name;

    /// <summary>
    /// Default timeout for Schema Registry operations (30 seconds).
    /// </summary>
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly Func<ReadOnlyMemory<byte>, Schema, T> _deserialize;
    private readonly bool _ownsClient;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;
    private readonly DeserializerSubjectNameCache? _subjectNames;
    private readonly SchemaRegistryMigrationRunner? _migrationRunner;

    /// <summary>
    /// Creates a new Schema Registry deserializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="deserialize">Function to deserialize bytes to value using the schema.</param>
    /// <param name="ownsClient">Whether this deserializer owns the client and should dispose it.</param>
    public SchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        Func<byte[], Schema, T> deserialize,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
        : this(
            schemaRegistry,
            (ReadOnlyMemory<byte> payload, Schema schema) => deserialize(payload.ToArray(), schema),
            ownsClient,
            ruleExecutor,
            config: null)
    {
    }

    /// <summary>
    /// Creates a new Schema Registry deserializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="deserialize">Function to deserialize bytes to value using the schema.</param>
    /// <param name="ownsClient">Whether this deserializer owns the client and should dispose it.</param>
    internal SchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        Func<ReadOnlyMemory<byte>, Schema, T> deserialize,
        bool ownsClient,
        ISchemaRegistryRuleExecutor? ruleExecutor = null,
        SchemaRegistryDeserializerConfig? config = null)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _deserialize = deserialize ?? throw new ArgumentNullException(nameof(deserialize));
        _ownsClient = ownsClient;
        _ruleExecutor = ruleExecutor;
        _subjectNames = DeserializerSubjectNameCache.Create(config);
        if (config?.UseLatestVersion == true)
        {
            (_migrationRunner, _ruleExecutor) = SchemaRegistryMigrationRunner.Create(
                schemaRegistry,
                ruleExecutor,
                SchemaRegistryTimeout);
        }
    }

    public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        var span = data.Span;

        if (span.Length < 5)
            throw new InvalidOperationException("Message too short to contain Schema Registry wire format");

        if (span[0] != MagicByte)
            throw new InvalidOperationException($"Unknown magic byte: {span[0]}. Expected Schema Registry format.");

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(span.Slice(1, 4));

        var payload = data.Slice(5);
        Schema schema;
        if (_ruleExecutor is not null)
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
                schema = _schemaRegistry.GetSchemaSync(schemaId, SchemaRegistryTimeout);
                subject = GetSubjectName(schemaId, schema, context);
            }

            schema = _schemaRegistry.GetSchemaSync(schemaId, subject, SchemaRegistryTimeout);
            if (_migrationRunner is null)
            {
                var ruleContext = SchemaRegistryRuleContext.Rent(
                    context.Topic,
                    context.Component,
                    schemaId,
                    subject,
                    schema,
                    SchemaRegistryPayloadFormat.Custom);
                try
                {
                    payload = _ruleExecutor!.TransformDeserializedPayload(payload, ruleContext);
                }
                finally
                {
                    ruleContext.Return();
                }
            }
            else
            {
                var migration = _migrationRunner.Transform(
                    payload,
                    schemaId,
                    subject,
                    schema,
                    context,
                    SchemaRegistryPayloadFormat.Custom);
                payload = migration.Payload;
                schema = migration.ReaderSchema.Schema;
            }
        }
        else
        {
            schema = _schemaRegistry.GetSchemaSync(schemaId, SchemaRegistryTimeout);
        }

        return _deserialize(payload, schema);
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

    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Factory methods for Schema Registry deserializers.
/// </summary>
public static class SchemaRegistryDeserializer
{
    /// <summary>
    /// Creates a Schema Registry deserializer that receives the payload as ReadOnlyMemory without copying it.
    /// </summary>
    /// <typeparam name="T">The type to deserialize.</typeparam>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="deserialize">Function to deserialize bytes to value using the schema.</param>
    /// <param name="ownsClient">Whether this deserializer owns the client and should dispose it.</param>
    /// <returns>The deserializer.</returns>
    public static SchemaRegistryDeserializer<T> Create<T>(
        ISchemaRegistryClient schemaRegistry,
        Func<ReadOnlyMemory<byte>, Schema, T> deserialize,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
        => new(schemaRegistry, deserialize, ownsClient, ruleExecutor);

    /// <summary>
    /// Creates a zero-copy Schema Registry deserializer with subject-name configuration for read rules.
    /// </summary>
    public static SchemaRegistryDeserializer<T> Create<T>(
        ISchemaRegistryClient schemaRegistry,
        Func<ReadOnlyMemory<byte>, Schema, T> deserialize,
        SchemaRegistryDeserializerConfig config,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new SchemaRegistryDeserializer<T>(schemaRegistry, deserialize, ownsClient, ruleExecutor, config);
    }
}

/// <summary>
/// Strategy for determining the subject name.
/// </summary>
public enum SubjectNameStrategy
{
    /// <summary>
    /// Subject name is the topic name with -key or -value suffix.
    /// </summary>
    TopicName,

    /// <summary>
    /// Subject name is the fully qualified record name.
    /// </summary>
    RecordName,

    /// <summary>
    /// Subject name is topic-recordname.
    /// </summary>
    TopicRecordName
}
