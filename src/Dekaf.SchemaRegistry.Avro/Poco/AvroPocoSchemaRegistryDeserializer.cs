using System.Buffers.Binary;
using System.Collections.Concurrent;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.Serialization;
using AvroRecordSchema = global::Avro.RecordSchema;
using AvroSchema = global::Avro.Schema;
using AvroSchemaNames = global::Avro.SchemaNames;

namespace Dekaf.SchemaRegistry.Avro.Poco;

/// <summary>Schema Registry deserializer backed by a generated POCO Avro codec.</summary>
public sealed class AvroPocoSchemaRegistryDeserializer<T, TCodec>
    : IDeserializer<T>, IAsyncDeserializerPreparer<T>, IAsyncDisposable
    where TCodec : struct, IAvroPocoCodec<T>
{
    private const byte MagicByte = 0;
    private const int WireHeaderSize = 5;
    private const int GeneratedSubjectCacheSchemaId = 0;
    private const int MaxCachedPreparedRuleStates = 1024;
    internal const int MaxCachedPlans = 256;
    private static readonly TimeSpan RegistryTimeout = TimeSpan.FromSeconds(30);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly AvroDeserializerConfig _config;
    private readonly bool _ownsClient;
    private readonly ConcurrentDictionary<int, AvroPocoReaderPlan> _plans = new();
    private readonly ConcurrentDictionary<int, ResolvedAvroSchema> _resolvedSchemas = new();
    private readonly ConcurrentDictionary<int, PlanEntry> _inFlightPlans = new();
    private readonly ConcurrentQueue<KeyValuePair<int, AvroPocoReaderPlan>> _planEvictionQueue = new();
    private readonly ConcurrentDictionary<PreparedRuleKey, PreparedRuleState> _preparedRuleStates = new();
    private readonly ConcurrentQueue<KeyValuePair<PreparedRuleKey, PreparedRuleState>>
        _preparedRuleStateEvictionQueue = new();
    private readonly AvroTaggedFieldTransformerProvider _taggedFieldTransformers = new();
    private readonly SchemaRegistryMigrationRunner? _migrationRunner;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;
    private readonly DeserializerSubjectNameCache? _subjectNames;
    private readonly bool _canUseSynchronousRuleCache;
    private PreparedRuleState? _lastPreparedRuleState;
    private int _cachedPlanCount;
    private int _cachedPreparedRuleStateCount;

    internal int CachedPlanCount => Volatile.Read(ref _cachedPlanCount);

    /// <summary>Creates a generated POCO Avro deserializer.</summary>
    public AvroPocoSchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        AvroDeserializerConfig? config = null,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _config = config ?? new AvroDeserializerConfig();
        _ownsClient = ownsClient;
        _ruleExecutor = _config.RuleExecutor;
        if (!string.IsNullOrEmpty(_config.ReaderSchema))
        {
            throw new ArgumentException(
                "Generated POCO codecs define their reader schema at compile time; ReaderSchema cannot be overridden.",
                nameof(config));
        }

        if (_config.UseLatestVersion)
        {
            (_migrationRunner, _ruleExecutor) = SchemaRegistryMigrationRunner.Create(
                schemaRegistry,
                _config.RuleExecutor,
                RegistryTimeout);
        }

        if (_ruleExecutor is not null)
        {
            _subjectNames = DeserializerSubjectNameCache.Create(
                _config.SubjectNameStrategy,
                _config.CustomSubjectNameStrategy,
                _config.UseLegacySubjectNames);
        }

        _canUseSynchronousRuleCache = _migrationRunner is null && schemaRegistry is ISchemaRegistryCache;
    }

    /// <summary>Prepares a writer schema ID and its evolution plan.</summary>
    public async Task WarmupAsync(int schemaId, CancellationToken cancellationToken = default) =>
        _ = await GetPlanAsync(schemaId, cancellationToken).ConfigureAwait(false);

    /// <summary>Prepares a writer schema ID and subject-scoped rule state.</summary>
    public async Task WarmupAsync(
        int schemaId,
        SerializationContext context,
        CancellationToken cancellationToken = default)
    {
        if (_ruleExecutor is null)
        {
            await WarmupAsync(schemaId, cancellationToken).ConfigureAwait(false);
            return;
        }

        var isKey = context.Component == SerializationComponent.Key;
        var subject = GetSubjectName(context.Topic, isKey);
        await PrepareRulesAsync(
                schemaId,
                subject,
                new PreparedRuleKey(schemaId, context.Topic, isKey),
                cancellationToken)
            .ConfigureAwait(false);
    }

    ValueTask IAsyncDeserializerPreparer<T>.PrepareAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        CancellationToken cancellationToken)
    {
        var span = data.Span;
        if (span.Length < WireHeaderSize || span[0] != MagicByte)
            return default;

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(span.Slice(1, 4));
        if (_ruleExecutor is null)
        {
            return TryGetCachedPlan(schemaId, out _)
                ? default
                : PreparePlanAsync(schemaId, cancellationToken);
        }

        var isKey = context.Component == SerializationComponent.Key;
        var subject = GetSubjectName(context.Topic, isKey);
        var preparedKey = new PreparedRuleKey(schemaId, context.Topic, isKey);
        if (TryGetCachedPlan(schemaId, out _) &&
            TryGetPreparedRuleState(preparedKey, out var preparedState))
        {
            return _migrationRunner is null
                ? default
                : PrepareMigrationTargetsAsync(
                    schemaId,
                    preparedState.Subject,
                    preparedState.Schema,
                    cancellationToken);
        }

        return PrepareRulesAsync(schemaId, subject, preparedKey, cancellationToken);
    }

    bool IAsyncDeserializerPreparer<T>.TryDeserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        out T value)
    {
        var span = data.Span;
        if (span.Length < WireHeaderSize)
        {
            if (context is { IsNull: true, Component: SerializationComponent.Value })
            {
                value = default!;
                return true;
            }

            throw new InvalidOperationException("Message too short to contain Schema Registry wire format.");
        }

        if (span[0] != MagicByte)
            throw new InvalidOperationException($"Unknown magic byte: {span[0]}. Expected 0x00.");

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(span.Slice(1, 4));
        if (!TryGetCachedPlan(schemaId, out var plan))
        {
            value = default!;
            return false;
        }

        if (_ruleExecutor is not null)
        {
            var isKey = context.Component == SerializationComponent.Key;
            var preparedState = Volatile.Read(ref _lastPreparedRuleState);
            if (preparedState is null || !preparedState.Matches(schemaId, context.Topic, isKey))
            {
                if (!_preparedRuleStates.TryGetValue(
                        new PreparedRuleKey(schemaId, context.Topic, isKey),
                        out preparedState))
                {
                    value = default!;
                    return false;
                }

                Volatile.Write(ref _lastPreparedRuleState, preparedState);
            }

            if (_migrationRunner is not null &&
                !_migrationRunner.TryUsePreparedPlan(schemaId, preparedState.Subject, preparedState.Schema))
            {
                value = default!;
                return false;
            }

            value = DeserializePreparedWithRules(
                data.Slice(WireHeaderSize),
                schemaId,
                context,
                preparedState.Subject,
                preparedState.Schema);
            return true;
        }

        var reader = new AvroValueReader(data.Span.Slice(WireHeaderSize));
        value = TCodec.Read(ref reader, plan);
        return true;
    }

    private bool TryGetCachedPlan(
        ReadOnlyMemory<byte> data,
        out int schemaId,
        out AvroPocoReaderPlan plan)
    {
        var span = data.Span;
        if (span.Length < WireHeaderSize || span[0] != MagicByte)
        {
            schemaId = default;
            plan = null!;
            return true;
        }

        schemaId = BinaryPrimitives.ReadInt32BigEndian(span.Slice(1, 4));
        return TryGetCachedPlan(schemaId, out plan);
    }

    private bool TryGetCachedPlan(int schemaId, out AvroPocoReaderPlan plan)
    {
        if (_plans.TryGetValue(schemaId, out plan!))
            return true;

        if (_schemaRegistry is ISchemaRegistryCache cache &&
            cache.TryGetCachedSchema(schemaId, out var schema) &&
            schema.References is not { Count: > 0 })
        {
            plan = BuildPlan(schemaId, schema);
            CacheSuccessfulPlan(schemaId, plan);
            return true;
        }

        plan = null!;
        return false;
    }

    /// <inheritdoc />
    public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        var span = data.Span;
        if (span.Length < WireHeaderSize)
        {
            if (context is { IsNull: true, Component: SerializationComponent.Value })
                return default!;
            throw new InvalidOperationException("Message too short to contain Schema Registry wire format.");
        }

        if (span[0] != MagicByte)
            throw new InvalidOperationException($"Unknown magic byte: {span[0]}. Expected 0x00.");

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(span.Slice(1, 4));
        var payload = data.Slice(WireHeaderSize);
        if (_ruleExecutor is null)
        {
            var directReader = new AvroValueReader(payload.Span);
            return TCodec.Read(ref directReader, GetPlanCached(schemaId));
        }

        if (_canUseSynchronousRuleCache)
            return DeserializeWithRules(payload, schemaId, context);

        return DeserializeWithPreparedRulesOrFallback(payload, schemaId, context);
    }

    private T DeserializeWithPreparedRulesOrFallback(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        SerializationContext context)
    {
        var isKey = context.Component == SerializationComponent.Key;
        if (TryGetPreparedRuleState(
                new PreparedRuleKey(schemaId, context.Topic, isKey),
                out var preparedState) &&
            (_migrationRunner is null ||
             _migrationRunner.TryUsePreparedPlan(schemaId, preparedState.Subject, preparedState.Schema)))
        {
            return DeserializePreparedWithRules(
                payload,
                schemaId,
                context,
                preparedState.Subject,
                preparedState.Schema);
        }

        return DeserializeWithRules(payload, schemaId, context);
    }

    private T DeserializeWithRules(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        SerializationContext context)
    {
        var subject = GetSubjectName(
            context.Topic,
            context.Component == SerializationComponent.Key);
        var scopedSchema = _schemaRegistry.GetSchemaSync(schemaId, subject, RegistryTimeout);
        if (_migrationRunner is not null)
        {
            return DeserializeWithMigration(
                payload,
                schemaId,
                subject,
                scopedSchema,
                context);
        }

        if (scopedSchema.RuleSet is not null)
            return DeserializeWithTaggedRules(payload, schemaId, subject, scopedSchema, context);

        var ruleContext = SchemaRegistryRuleContext.Rent(
            context.Topic,
            context.Component,
            schemaId,
            subject,
            scopedSchema,
            SchemaRegistryPayloadFormat.Avro);
        try
        {
            payload = _ruleExecutor!.TransformDeserializedPayload(payload, ruleContext);
        }
        finally
        {
            ruleContext.Return();
        }

        var reader = new AvroValueReader(payload.Span);
        return TCodec.Read(ref reader, GetOrBuildPlanCached(schemaId, scopedSchema));
    }

    // Keep the direct and prepared bodies separate: a shared forwarding helper regresses this hot path.
    private T DeserializePreparedWithRules(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        SerializationContext context,
        string subject,
        Schema scopedSchema)
    {
        if (_migrationRunner is not null)
        {
            return DeserializeWithMigration(
                payload,
                schemaId,
                subject,
                scopedSchema,
                context,
                skipLatestRefresh: true);
        }

        if (scopedSchema.RuleSet is not null)
            return DeserializeWithTaggedRules(payload, schemaId, subject, scopedSchema, context);

        var ruleContext = SchemaRegistryRuleContext.Rent(
            context.Topic,
            context.Component,
            schemaId,
            subject,
            scopedSchema,
            SchemaRegistryPayloadFormat.Avro);
        try
        {
            payload = _ruleExecutor!.TransformDeserializedPayload(payload, ruleContext);
        }
        finally
        {
            ruleContext.Return();
        }

        var reader = new AvroValueReader(payload.Span);
        return TCodec.Read(ref reader, GetOrBuildPlanCached(schemaId, scopedSchema));
    }

    private T DeserializeWithTaggedRules(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        string subject,
        Schema scopedSchema,
        SerializationContext context)
    {
        var taggedWorkspaceOperation = AvroTaggedFieldTransformerProvider.BeginOperation();
        try
        {
            var ruleContext = SchemaRegistryRuleContext.RentWithTaggedFieldTransformer(
                context.Topic,
                context.Component,
                schemaId,
                subject,
                scopedSchema,
                SchemaRegistryPayloadFormat.Avro,
                _taggedFieldTransformers.Get(scopedSchema));
            try
            {
                payload = _ruleExecutor!.TransformDeserializedPayload(payload, ruleContext);
            }
            finally
            {
                ruleContext.Return();
            }

            var reader = new AvroValueReader(payload.Span);
            return TCodec.Read(ref reader, GetOrBuildPlanCached(schemaId, scopedSchema));
        }
        finally
        {
            taggedWorkspaceOperation.Dispose();
        }
    }

    private T DeserializeWithMigration(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        string subject,
        Schema scopedSchema,
        SerializationContext context,
        bool skipLatestRefresh = false)
    {
        var taggedWorkspaceOperation = AvroTaggedFieldTransformerProvider.BeginOperation();
        try
        {
            var migration = _migrationRunner!.Transform(
                payload,
                schemaId,
                subject,
                scopedSchema,
                context,
                SchemaRegistryPayloadFormat.Avro,
                _taggedFieldTransformers,
                skipLatestRefresh);
            var reader = new AvroValueReader(migration.Payload.Span);
            return TCodec.Read(
                ref reader,
                GetOrBuildPlanCached(migration.PayloadSchemaId, migration.PayloadSchema));
        }
        finally
        {
            taggedWorkspaceOperation.Dispose();
        }
    }

    private AvroPocoReaderPlan GetPlanCached(int schemaId)
    {
        if (TryGetCachedPlan(schemaId, out var plan))
            return plan;

        throw new InvalidOperationException(
            $"Schema {schemaId} is not cached. Consume through an asynchronous consumer API or call WarmupAsync first.");
    }

    private async ValueTask PreparePlanAsync(int schemaId, CancellationToken cancellationToken) =>
        _ = await GetPlanAsync(schemaId, cancellationToken).ConfigureAwait(false);

    private async ValueTask PrepareRulesAsync(
        int schemaId,
        string subject,
        PreparedRuleKey preparedKey,
        CancellationToken cancellationToken)
    {
        var plan = await GetPlanAsync(schemaId, cancellationToken).ConfigureAwait(false);
        var scopedSchema = await _schemaRegistry.GetSchemaAsync(schemaId, subject, cancellationToken)
            .ConfigureAwait(false);
        if (scopedSchema.RuleSet is not null || _migrationRunner is not null)
            PrepareTaggedTransformer(schemaId, scopedSchema, plan);

        if (_migrationRunner is not null)
        {
            await PrepareMigrationTargetsAsync(schemaId, subject, scopedSchema, cancellationToken)
                .ConfigureAwait(false);
        }

        CachePreparedRuleState(preparedKey, subject, scopedSchema);
    }

    private async ValueTask PrepareMigrationTargetsAsync(
        int schemaId,
        string subject,
        Schema writerSchema,
        CancellationToken cancellationToken)
    {
        var targets = await _migrationRunner!.PrepareAsync(schemaId, subject, writerSchema, cancellationToken)
            .ConfigureAwait(false);
        while (targets.MoveNext(out var target))
        {
            var plan = await GetPlanAsync(target.Id, cancellationToken).ConfigureAwait(false);
            PrepareTaggedTransformer(target.Id, target.Schema, plan);
        }
    }

    private void PrepareTaggedTransformer(int schemaId, Schema schema, AvroPocoReaderPlan plan)
    {
        var parsed = _resolvedSchemas.TryGetValue(schemaId, out var resolved) &&
                     ReferenceEquals(resolved.Plan, plan)
            ? resolved.Schema
            : AvroSchema.Parse(schema.SchemaString);
        _ = _taggedFieldTransformers.GetResolved(schema, parsed);
    }

    private bool TryGetPreparedRuleState(PreparedRuleKey key, out PreparedRuleState state)
    {
        state = Volatile.Read(ref _lastPreparedRuleState)!;
        if (state is not null && state.Matches(key.SchemaId, key.Topic, key.IsKey))
            return true;

        if (!_preparedRuleStates.TryGetValue(key, out state!))
            return false;

        Volatile.Write(ref _lastPreparedRuleState, state);
        return true;
    }

    private void CachePreparedRuleState(PreparedRuleKey key, string subject, Schema schema)
    {
        var state = new PreparedRuleState(key, subject, schema);
        if (!_preparedRuleStates.TryAdd(key, state))
        {
            if (_preparedRuleStates.TryGetValue(key, out var cached))
                Volatile.Write(ref _lastPreparedRuleState, cached);
            return;
        }

        Volatile.Write(ref _lastPreparedRuleState, state);
        Interlocked.Increment(ref _cachedPreparedRuleStateCount);
        _preparedRuleStateEvictionQueue.Enqueue(new KeyValuePair<PreparedRuleKey, PreparedRuleState>(key, state));
        TrimPreparedRuleStateCache();
    }

    private void TrimPreparedRuleStateCache()
    {
        while (true)
        {
            var count = Volatile.Read(ref _cachedPreparedRuleStateCount);
            if (count <= MaxCachedPreparedRuleStates)
                return;

            if (Interlocked.CompareExchange(ref _cachedPreparedRuleStateCount, count - 1, count) != count)
                continue;

            var removed = false;
            while (_preparedRuleStateEvictionQueue.TryDequeue(out var oldest))
            {
                if (((ICollection<KeyValuePair<PreparedRuleKey, PreparedRuleState>>)_preparedRuleStates).Remove(oldest))
                {
                    removed = true;
                    break;
                }
            }

            if (!removed)
            {
                Interlocked.Increment(ref _cachedPreparedRuleStateCount);
                return;
            }
        }
    }

    private AvroPocoReaderPlan GetOrBuildPlanCached(int schemaId, Schema schema)
    {
        if (_plans.TryGetValue(schemaId, out var cached))
            return cached;

        var plan = BuildPlan(schemaId, schema);
        CacheSuccessfulPlan(schemaId, plan);
        return _plans.TryGetValue(schemaId, out cached) ? cached : plan;
    }

    private async Task<AvroPocoReaderPlan> GetPlanAsync(int schemaId, CancellationToken cancellationToken)
    {
        if (_plans.TryGetValue(schemaId, out var cached))
            return cached;

        cancellationToken.ThrowIfCancellationRequested();
        var entry = GetOrAddInFlightPlan(schemaId);
        if (_plans.TryGetValue(schemaId, out cached))
        {
            RemoveInFlightPlan(schemaId, entry);
            return cached;
        }

        var task = entry.Plan.Value;
        return task.IsCompletedSuccessfully
            ? task.Result
            : await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private PlanEntry GetOrAddInFlightPlan(int schemaId) =>
        _inFlightPlans.GetOrAdd(
            schemaId,
            static (id, owner) => PlanEntry.Create(owner, id),
            this);

    private async Task<AvroPocoReaderPlan> FetchPlanAsync(int schemaId, PlanEntry entry)
    {
        try
        {
            var plan = await SchemaRegistryOperationTimeout.ExecuteAsync(
                    cancellationToken => FetchAndBuildPlanAsync(schemaId, cancellationToken),
                    RegistryTimeout,
                    $"Schema Registry plan resolution for schema {schemaId} timed out.")
                .ConfigureAwait(false);
            CacheSuccessfulPlan(schemaId, plan);
            return plan;
        }
        finally
        {
            RemoveInFlightPlan(schemaId, entry);
        }
    }

    private async Task<AvroPocoReaderPlan> FetchAndBuildPlanAsync(
        int schemaId,
        CancellationToken cancellationToken)
    {
        var schema = await _schemaRegistry.GetSchemaAsync(schemaId, cancellationToken)
            .ConfigureAwait(false);
        if (schema.References is not { Count: > 0 })
            return BuildPlan(schemaId, schema);

        ValidateAvroSchema(schemaId, schema);
        var names = await AvroSchemaReferenceResolver.ResolveAsync(
                _schemaRegistry,
                schema,
                cancellationToken)
            .ConfigureAwait(false);
        var parsed = AvroSchema.Parse(schema.SchemaString, names) as AvroRecordSchema
            ?? throw new InvalidOperationException("POCO Avro writer schema must be a record.");
        var plan = AvroPocoReaderPlanBuilder.Build<T, TCodec>(parsed);
        _resolvedSchemas[schemaId] = new ResolvedAvroSchema(plan, parsed);
        return plan;
    }

    private static AvroPocoReaderPlan BuildPlan(
        int schemaId,
        Schema schema,
        AvroSchemaNames? names = null)
    {
        ValidateAvroSchema(schemaId, schema);
        return AvroPocoReaderPlanBuilder.Build<T, TCodec>(schema.SchemaString, names);
    }

    private static void ValidateAvroSchema(int schemaId, Schema schema)
    {
        if (schema.SchemaType != SchemaType.Avro)
            throw new InvalidOperationException($"Schema {schemaId} is {schema.SchemaType}, not Avro.");
    }

    private void RemoveInFlightPlan(int schemaId, PlanEntry entry) =>
        ((ICollection<KeyValuePair<int, PlanEntry>>)_inFlightPlans)
        .Remove(new KeyValuePair<int, PlanEntry>(schemaId, entry));

    private void CacheSuccessfulPlan(int schemaId, AvroPocoReaderPlan plan)
    {
        if (!_plans.TryAdd(schemaId, plan))
            return;

        Interlocked.Increment(ref _cachedPlanCount);
        _planEvictionQueue.Enqueue(new KeyValuePair<int, AvroPocoReaderPlan>(schemaId, plan));
        TrimPlanCache();
    }

    private void TrimPlanCache()
    {
        while (true)
        {
            var count = Volatile.Read(ref _cachedPlanCount);
            if (count <= MaxCachedPlans)
                return;

            if (Interlocked.CompareExchange(ref _cachedPlanCount, count - 1, count) != count)
                continue;

            var removed = false;
            while (_planEvictionQueue.TryDequeue(out var oldest))
            {
                if (((ICollection<KeyValuePair<int, AvroPocoReaderPlan>>)_plans).Remove(oldest))
                {
                    if (_resolvedSchemas.TryGetValue(oldest.Key, out var resolved) &&
                        ReferenceEquals(resolved.Plan, oldest.Value))
                    {
                        _ = ((ICollection<KeyValuePair<int, ResolvedAvroSchema>>)_resolvedSchemas)
                            .Remove(new KeyValuePair<int, ResolvedAvroSchema>(oldest.Key, resolved));
                    }

                    removed = true;
                    break;
                }
            }

            if (!removed)
            {
                Interlocked.Increment(ref _cachedPlanCount);
                return;
            }
        }
    }

    private string GetSubjectName(string topic, bool isKey) =>
        _subjectNames?.GetSubjectName(GeneratedSubjectCacheSchemaId, null, topic, isKey, TCodec.FullName)
        ?? SubjectNameResolver.GetTopicSubjectName(topic, isKey);

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class PlanEntry
    {
        private PlanEntry()
        {
        }

        internal Lazy<Task<AvroPocoReaderPlan>> Plan { get; private set; } = null!;

        internal static PlanEntry Create(
            AvroPocoSchemaRegistryDeserializer<T, TCodec> owner,
            int schemaId)
        {
            var entry = new PlanEntry();
            entry.Plan = new Lazy<Task<AvroPocoReaderPlan>>(
                () => ObserveFault(owner.FetchPlanAsync(schemaId, entry)));
            return entry;
        }

        private static Task<AvroPocoReaderPlan> ObserveFault(Task<AvroPocoReaderPlan> task)
        {
            _ = task.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            return task;
        }
    }

    private readonly record struct PreparedRuleKey(int SchemaId, string Topic, bool IsKey);

    private sealed record ResolvedAvroSchema(AvroPocoReaderPlan Plan, AvroSchema Schema);

    private sealed class PreparedRuleState(PreparedRuleKey key, string subject, Schema schema)
    {
        internal PreparedRuleKey Key { get; } = key;
        internal string Subject { get; } = subject;
        internal Schema Schema { get; } = schema;

        internal bool Matches(int schemaId, string topic, bool isKey) =>
            Key.SchemaId == schemaId &&
            Key.IsKey == isKey &&
            (ReferenceEquals(Key.Topic, topic) || string.Equals(Key.Topic, topic, StringComparison.Ordinal));
    }
}
