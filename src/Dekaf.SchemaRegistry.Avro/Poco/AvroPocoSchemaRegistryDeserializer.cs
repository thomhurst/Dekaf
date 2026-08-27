using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.Serialization;
using AvroRecordSchema = global::Avro.RecordSchema;
using AvroSchema = global::Avro.Schema;
using AvroSchemaNames = global::Avro.SchemaNames;

namespace Dekaf.SchemaRegistry.Avro.Poco;

/// <summary>Schema Registry deserializer backed by a generated POCO Avro codec.</summary>
public sealed class AvroPocoSchemaRegistryDeserializer<T, TCodec>
    : IDeserializer<T>, IAsyncDeserializerPreparer<T>, IRecordHeaderAsyncDeserializerPreparer<T>,
      IRecordHeaderDeserializer<T>, ICallerOwnedHeaderDeserializer<T>, IRecordHeaderRoutingProvider,
      IAsyncDisposable
    where TCodec : struct, IAvroPocoCodec<T>
{
    private const int MaxAssociatedNameInvalidationRetries = 4;
    private const int GeneratedSubjectCacheSchemaId = 0;
    private const int MaxCachedPreparedRuleStates = 1024;
    private const int MaxCachedGuidSchemas = 1024;
    internal const int MaxCachedPlans = 256;
    private static readonly TimeSpan RegistryTimeout = TimeSpan.FromSeconds(30);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly AvroDeserializerConfig _config;
    private readonly bool _ownsClient;
    private readonly ConcurrentDictionary<int, AvroPocoReaderPlan> _plans = new();
    private readonly ConcurrentDictionary<int, ResolvedAvroSchema> _resolvedSchemas = new();
    private readonly ConcurrentDictionary<GuidTopicKey, Lazy<Task<GuidResolvedSchema>>> _guidSchemaCache = new();
    private readonly ConcurrentQueue<KeyValuePair<GuidTopicKey, Lazy<Task<GuidResolvedSchema>>>>
        _guidSchemaEvictionQueue = new();
    private int _cachedGuidSchemaCount;
    private readonly ConcurrentDictionary<int, PlanEntry> _inFlightPlans = new();
    private readonly ConcurrentQueue<KeyValuePair<int, AvroPocoReaderPlan>> _planEvictionQueue = new();
    private readonly ConcurrentDictionary<PreparedRuleKey, PreparedRuleState> _preparedRuleStates = new();
    private readonly ConcurrentQueue<PreparedRuleKey> _preparedRuleStateEvictionQueue = new();
    private readonly AvroTaggedFieldTransformerProvider _taggedFieldTransformers;
    private readonly SchemaRegistryMigrationRunner? _migrationRunner;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;
    private readonly DeserializerSubjectNameCache? _subjectNames;
    private readonly bool _canUseSynchronousRuleCache;
    private readonly AvroInlineRuleValidatorProvider? _inlineRuleValidators;
    private PreparedRuleState? _lastPreparedRuleState;
    private long _lastInlineValidationDecision = -1;
    private int _cachedPlanCount;
    private int _cachedPreparedRuleStateCount;
    private int _nextGuidPlanId;

    internal int CachedPlanCount => Volatile.Read(ref _cachedPlanCount);
    internal long LastInlineValidationDecision => Volatile.Read(ref _lastInlineValidationDecision);

    /// <summary>Creates a generated POCO Avro deserializer.</summary>
    public AvroPocoSchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        AvroDeserializerConfig? config = null,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _taggedFieldTransformers = new AvroTaggedFieldTransformerProvider();
        _config = config ?? new AvroDeserializerConfig();
        if (_config.SchemaIdStrategy is not (
            SchemaIdDeserializerStrategy.Dual
            or SchemaIdDeserializerStrategy.Prefix
            or SchemaIdDeserializerStrategy.Header))
        {
            throw new ArgumentOutOfRangeException(
                nameof(config),
                _config.SchemaIdStrategy,
                "Unknown schema identity strategy.");
        }
        _ownsClient = ownsClient;
        _ruleExecutor = _config.RuleExecutor;
        _inlineRuleValidators = AvroValidationConfiguration.Create(
            schemaRegistry,
            _config.ValidationRulesExecution,
            _config.RuleExecutor);
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

        if (_ruleExecutor is not null
            || _config.SchemaIdStrategy is not SchemaIdDeserializerStrategy.Prefix)
        {
            _subjectNames = DeserializerSubjectNameCache.Create(
                schemaRegistry,
                _config.SubjectNameStrategy,
                _config.CustomSubjectNameStrategy,
                _config.AsyncSubjectNameStrategy,
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
        if (_subjectNames is { RequiresPreparation: true })
        {
            await PrepareSubjectAndRulesAsync(schemaId, context, isKey, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var preparedSubject = new DeserializerSubjectNameCache.PreparedSubject(
            GetSubjectName(context.Topic, isKey),
            Generation: 0,
            State: null);
        _ = await PrepareRulesAsync(
                schemaId,
                preparedSubject,
                new PreparedRuleKey(schemaId, context.Topic, isKey),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Prepares a GUID-framed writer schema, its topic/component subject, and its evolution plan.
    /// Call this before <c>ConsumeBatchAsync</c>, whose records are deserialized synchronously.
    /// </summary>
    public async Task WarmupAsync(
        Guid schemaGuid,
        SerializationContext context,
        CancellationToken cancellationToken = default) =>
        await PrepareGuidAsync(
                new GuidTopicKey(
                    schemaGuid,
                    context.Topic,
                    context.Component == SerializationComponent.Key,
                    _subjectNames?.Generation ?? 0),
                context,
                cancellationToken)
            .ConfigureAwait(false);

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

        var identity = ReadIdentity(data, identityHeader, out _);
        if (identity.SchemaGuid is { } schemaGuid)
        {
            return PrepareGuidAsync(
                new GuidTopicKey(
                    schemaGuid,
                    context.Topic,
                    context.Component == SerializationComponent.Key,
                    _subjectNames?.Generation ?? 0),
                context,
                cancellationToken);
        }

        return PrepareSchemaIdAsync(identity.SchemaId!.Value, context, cancellationToken);
    }

    private ValueTask PrepareSchemaIdAsync(
        int schemaId,
        SerializationContext context,
        CancellationToken cancellationToken)
    {
        if (_ruleExecutor is null)
        {
            return TryGetCachedPlan(schemaId, out _)
                ? default
                : PreparePlanAsync(schemaId, cancellationToken);
        }

        var isKey = context.Component == SerializationComponent.Key;
        DeserializerSubjectNameCache.PreparedSubject preparedSubject;
        if (_subjectNames is { RequiresPreparation: true } subjectNames)
        {
            if (!subjectNames.TryGetPreparedSubject(
                    GeneratedSubjectCacheSchemaId,
                    context.Topic,
                    isKey,
                    out preparedSubject))
            {
                return PrepareSubjectAndRulesAsync(schemaId, context, isKey, cancellationToken);
            }
        }
        else
        {
            preparedSubject = new DeserializerSubjectNameCache.PreparedSubject(
                GetSubjectName(context.Topic, isKey),
                Generation: 0,
                State: null);
        }

        var preparedKey = new PreparedRuleKey(schemaId, context.Topic, isKey);
        if (TryGetCachedPlan(schemaId, out _) &&
            TryGetPreparedRuleState(preparedKey, preparedSubject.Generation, out var preparedState))
        {
            return _migrationRunner is null
                ? default
                : RefreshMigrationTargetsAsync(
                    schemaId,
                    preparedState,
                    cancellationToken);
        }

        return PrepareRulesWithRefreshAsync(
            schemaId,
            context,
            isKey,
            preparedKey,
            preparedSubject,
            cancellationToken);
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

        var identity = ReadIdentity(data, identityHeader, out var payloadOffset);
        int schemaId;
        if (identity.SchemaGuid is { } schemaGuid)
        {
            var key = new GuidTopicKey(
                schemaGuid,
                context.Topic,
                context.Component == SerializationComponent.Key,
                _subjectNames?.Generation ?? 0);
            if (!_guidSchemaCache.TryGetValue(key, out var guidResolution)
                || !guidResolution.IsValueCreated
                || !guidResolution.Value.IsCompletedSuccessfully)
            {
                value = default!;
                return false;
            }

            schemaId = guidResolution.Value.Result.SchemaId;
        }
        else
        {
            schemaId = identity.SchemaId!.Value;
        }

        if (!TryGetCachedPlan(schemaId, out var plan))
        {
            value = default!;
            return false;
        }

        if (_ruleExecutor is not null)
        {
            var isKey = context.Component == SerializationComponent.Key;
            var subjectGeneration = 0;
            if (_subjectNames is { RequiresPreparation: true } subjectNames)
            {
                if (!subjectNames.TryGetPreparedSubject(
                        GeneratedSubjectCacheSchemaId,
                        context.Topic,
                        isKey,
                        out var preparedSubject))
                {
                    value = default!;
                    return false;
                }

                subjectGeneration = preparedSubject.Generation;
            }

            var preparedState = Volatile.Read(ref _lastPreparedRuleState);
            if (preparedState is null ||
                !preparedState.Matches(schemaId, context.Topic, isKey, subjectGeneration))
            {
                if (!_preparedRuleStates.TryGetValue(
                        new PreparedRuleKey(schemaId, context.Topic, isKey),
                        out preparedState) ||
                    preparedState.Generation != subjectGeneration)
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
                data[payloadOffset..],
                schemaId,
                context,
                preparedState.Subject,
                preparedState.Schema,
                plan);
            return true;
        }

        var payload = data[payloadOffset..];
        GetInlineValidator(schemaId, plan)?.Validate(
            payload, schemaId, _config.ValidationRulesFailFast);
        var reader = new AvroValueReader(payload.Span);
        value = TCodec.Read(ref reader, plan);
        return true;
    }

    private async ValueTask PrepareSubjectAndRulesAsync(
        int schemaId,
        SerializationContext context,
        bool isKey,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAssociatedNameInvalidationRetries; attempt++)
        {
            await _subjectNames!.PrepareAsync(
                    _schemaRegistry,
                    schemaId,
                    context.Topic,
                    isKey,
                    TCodec.FullName,
                    cancellationToken,
                    cacheSchemaId: GeneratedSubjectCacheSchemaId)
                .ConfigureAwait(false);
            if (!_subjectNames.TryGetPreparedSubject(
                    GeneratedSubjectCacheSchemaId,
                    context.Topic,
                    isKey,
                    out var preparedSubject))
            {
                continue;
            }

            if (await PrepareRulesAsync(
                    schemaId,
                    preparedSubject,
                    new PreparedRuleKey(schemaId, context.Topic, isKey),
                    cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "Associated-name cache changed repeatedly while preparing generated Avro rules.");
    }

    private async ValueTask PrepareRulesWithRefreshAsync(
        int schemaId,
        SerializationContext context,
        bool isKey,
        PreparedRuleKey preparedKey,
        DeserializerSubjectNameCache.PreparedSubject preparedSubject,
        CancellationToken cancellationToken)
    {
        if (await PrepareRulesAsync(
                schemaId,
                preparedSubject,
                preparedKey,
                cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await PrepareSubjectAndRulesAsync(schemaId, context, isKey, cancellationToken)
            .ConfigureAwait(false);
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
    public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
        DeserializeCore(data, context, FindCallerIdentityHeader(context));

    T ICallerOwnedHeaderDeserializer<T>.DeserializeCallerOwned(
        ReadOnlyMemory<byte> data,
        SerializationContext context) => Deserialize(data, context);

    T IRecordHeaderDeserializer<T>.Deserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers) =>
        DeserializeCore(data, context, FindRoutedIdentityHeader(context, in headers));

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
        Header? identityHeader)
    {
        if (context is { IsNull: true, Component: SerializationComponent.Value })
            return default!;

        var identity = ReadIdentity(data, identityHeader, out var payloadOffset);
        var schemaId = identity.SchemaId ?? GetResolvedGuidSchemaId(
            new GuidTopicKey(
                identity.SchemaGuid!.Value,
                context.Topic,
                context.Component == SerializationComponent.Key,
                _subjectNames?.Generation ?? 0));
        var payload = data[payloadOffset..];
        if (_ruleExecutor is null && _inlineRuleValidators is null)
        {
            var directReader = new AvroValueReader(payload.Span);
            return TCodec.Read(ref directReader, GetPlanCached(schemaId));
        }

        if (_ruleExecutor is null)
        {
            var directPlan = GetPlanCached(schemaId);
            GetInlineValidator(schemaId, directPlan)?.Validate(
                payload, schemaId, _config.ValidationRulesFailFast);
            var directReader = new AvroValueReader(payload.Span);
            return TCodec.Read(ref directReader, directPlan);
        }

        if (_canUseSynchronousRuleCache)
            return DeserializeWithRules(payload, schemaId, context);

        return DeserializeWithPreparedRulesOrFallback(payload, schemaId, context);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private AvroInlineRuleValidator? GetInlineValidator(int schemaId, AvroPocoReaderPlan plan)
    {
        if (_inlineRuleValidators is null)
            return null;

        var cached = Volatile.Read(ref _lastInlineValidationDecision);
        if (cached >= 0 && (int)(uint)(cached >> 1) == schemaId && (cached & 1) == 0)
            return null;

        var validator = _inlineRuleValidators.Get(GetValidationSchema(schemaId, plan));
        var hasRules = validator.HasAnyRules;
        Volatile.Write(
            ref _lastInlineValidationDecision,
            ((long)(uint)schemaId << 1) | (hasRules ? 1L : 0L));
        return hasRules ? validator : null;
    }

    private T DeserializeWithPreparedRulesOrFallback(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        SerializationContext context)
    {
        var isKey = context.Component == SerializationComponent.Key;
        if (TryGetSubjectGeneration(context.Topic, isKey, out var subjectGeneration) &&
            TryGetPreparedRuleState(
                new PreparedRuleKey(schemaId, context.Topic, isKey),
                subjectGeneration,
                out var preparedState) &&
            (_migrationRunner is null ||
             _migrationRunner.TryUsePreparedPlan(schemaId, preparedState.Subject, preparedState.Schema)))
        {
            return DeserializePreparedWithRules(
                payload,
                schemaId,
                context,
                preparedState.Subject,
                preparedState.Schema,
                preparedState.Plan);
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

        var plan = GetOrBuildPlanCached(schemaId, scopedSchema);
        if (scopedSchema.RuleSet is not null)
            return DeserializeWithTaggedRules(payload, schemaId, subject, scopedSchema, context, plan);

        var ruleContext = SchemaRegistryRuleContext.Rent(
            context.Topic,
            context.Component,
            schemaId,
            subject,
            scopedSchema,
            SchemaRegistryPayloadFormat.Avro);
        try
        {
            payload = TransformDeserializedPayload(
                payload,
                schemaId,
                scopedSchema,
                plan,
                ruleContext);
        }
        finally
        {
            ruleContext.Return();
        }

        var reader = new AvroValueReader(payload.Span);
        return TCodec.Read(ref reader, plan);
    }

    // Keep the direct and prepared bodies separate: a shared forwarding helper regresses this hot path.
    private T DeserializePreparedWithRules(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        SerializationContext context,
        string subject,
        Schema scopedSchema,
        AvroPocoReaderPlan plan)
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
            return DeserializeWithTaggedRules(payload, schemaId, subject, scopedSchema, context, plan);

        var ruleContext = SchemaRegistryRuleContext.Rent(
            context.Topic,
            context.Component,
            schemaId,
            subject,
            scopedSchema,
            SchemaRegistryPayloadFormat.Avro);
        try
        {
            payload = TransformDeserializedPayload(
                payload,
                schemaId,
                scopedSchema,
                plan,
                ruleContext);
        }
        finally
        {
            ruleContext.Return();
        }

        var reader = new AvroValueReader(payload.Span);
        return TCodec.Read(ref reader, plan);
    }

    private T DeserializeWithTaggedRules(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        string subject,
        Schema scopedSchema,
        SerializationContext context,
        AvroPocoReaderPlan plan)
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
                payload = TransformDeserializedPayload(
                    payload,
                    schemaId,
                    scopedSchema,
                    plan,
                    ruleContext);
            }
            finally
            {
                ruleContext.Return();
            }

            var reader = new AvroValueReader(payload.Span);
            return TCodec.Read(ref reader, plan);
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
            if (_inlineRuleValidators is not null)
            {
                var writerPlan = GetOrBuildPlanCached(schemaId, scopedSchema);
                _inlineRuleValidators.Register(
                    scopedSchema,
                    GetValidationSchema(schemaId, scopedSchema, writerPlan));
            }
            var migration = _config.ValidationRulesExecution == ValidationRulesExecution.BeforeDomainRules
                ? _migrationRunner!.TransformWithBeforeDomainValidation(
                    payload,
                    schemaId,
                    subject,
                    scopedSchema,
                    context,
                    SchemaRegistryPayloadFormat.Avro,
                    _inlineRuleValidators!,
                    _config.ValidationRulesFailFast,
                    _taggedFieldTransformers,
                    skipLatestRefresh)
                : _migrationRunner!.Transform(
                    payload,
                    schemaId,
                    subject,
                    scopedSchema,
                    context,
                    SchemaRegistryPayloadFormat.Avro,
                    _taggedFieldTransformers,
                    skipLatestRefresh);
            var preparedKey = new PreparedRuleKey(
                schemaId,
                context.Topic,
                context.Component == SerializationComponent.Key);
            var plan = skipLatestRefresh &&
                       TryGetSubjectGeneration(
                           context.Topic,
                           context.Component == SerializationComponent.Key,
                           out var subjectGeneration) &&
                       TryGetPreparedRuleState(preparedKey, subjectGeneration, out var preparedState) &&
                       preparedState.TryGetPlan(migration.PayloadSchemaId, out var preparedPlan)
                ? preparedPlan
                : GetOrBuildPlanCached(migration.PayloadSchemaId, migration.PayloadSchema);
            if (_inlineRuleValidators is not null)
            {
                var validationSchema = GetValidationSchema(
                    migration.PayloadSchemaId,
                    migration.PayloadSchema,
                    plan);
                _inlineRuleValidators.Register(migration.PayloadSchema, validationSchema);
                if (_config.ValidationRulesExecution == ValidationRulesExecution.AfterDomainRules)
                {
                    ((IInlineValidationRuleExecutor)_inlineRuleValidators).Validate(
                        migration.Payload,
                        migration.PayloadSchemaId,
                        subject,
                        migration.PayloadSchema,
                        _config.ValidationRulesFailFast);
                }
            }
            var reader = new AvroValueReader(migration.Payload.Span);
            return TCodec.Read(ref reader, plan);
        }
        finally
        {
            taggedWorkspaceOperation.Dispose();
        }
    }

    private ReadOnlyMemory<byte> TransformDeserializedPayload(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        Schema scopedSchema,
        AvroPocoReaderPlan plan,
        SchemaRegistryRuleContext context)
    {
        if (_inlineRuleValidators is null ||
            _ruleExecutor is not SchemaRegistryRuleExecutor ruleExecutor)
        {
            return _ruleExecutor!.TransformDeserializedPayload(payload, context);
        }

        var validator = _inlineRuleValidators.Register(
            scopedSchema,
            GetValidationSchema(schemaId, scopedSchema, plan));
        payload = ruleExecutor.TransformDeserializedEncodingPayload(payload, context);
        if (_config.ValidationRulesExecution == ValidationRulesExecution.BeforeDomainRules)
            validator.Validate(payload, schemaId, _config.ValidationRulesFailFast);
        payload = ruleExecutor.TransformDeserializedDomainPayload(payload, context);
        if (_config.ValidationRulesExecution == ValidationRulesExecution.AfterDomainRules)
            validator.Validate(payload, schemaId, _config.ValidationRulesFailFast);
        return payload;
    }

    private AvroSchema GetValidationSchema(int schemaId, AvroPocoReaderPlan plan)
    {
        if (plan.ResolvedWriterSchema is { } resolved)
            return resolved;
        var schema = _schemaRegistry.GetSchemaSync(schemaId, RegistryTimeout);
        return GetValidationSchema(schemaId, schema, plan);
    }

    private AvroSchema GetValidationSchema(
        int schemaId,
        Schema schema,
        AvroPocoReaderPlan plan)
    {
        if (plan.ResolvedWriterSchema is { } resolved)
            return resolved;
        if (_resolvedSchemas.TryGetValue(schemaId, out var cached))
            return cached.Schema;
        var parsed = AvroSchema.Parse(schema.SchemaString);
        plan.ResolvedWriterSchema = parsed;
        return parsed;
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

    private async ValueTask<bool> PrepareRulesAsync(
        int schemaId,
        DeserializerSubjectNameCache.PreparedSubject preparedSubject,
        PreparedRuleKey preparedKey,
        CancellationToken cancellationToken)
    {
        var plan = await GetPlanAsync(schemaId, cancellationToken).ConfigureAwait(false);
        var scopedSchema = await _schemaRegistry.GetSchemaAsync(
                schemaId,
                preparedSubject.Subject,
                cancellationToken)
            .ConfigureAwait(false);
        if (scopedSchema.RuleSet is not null || _migrationRunner is not null)
            PrepareTaggedTransformer(schemaId, scopedSchema, plan);

        Dictionary<int, AvroPocoReaderPlan>? migrationPlans = null;
        if (_migrationRunner is not null)
        {
            migrationPlans = await PrepareMigrationTargetPlansAsync(
                    schemaId,
                    preparedSubject.Subject,
                    scopedSchema,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return CachePreparedRuleState(
            preparedKey,
            preparedSubject,
            scopedSchema,
            plan,
            migrationPlans);
    }

    private async ValueTask RefreshMigrationTargetsAsync(
        int schemaId,
        PreparedRuleState preparedState,
        CancellationToken cancellationToken)
    {
        var plans = await PrepareMigrationTargetPlansAsync(
                schemaId,
                preparedState.Subject,
                preparedState.Schema,
                cancellationToken)
            .ConfigureAwait(false);
        preparedState.SetMigrationPlans(plans);
    }

    private async ValueTask<Dictionary<int, AvroPocoReaderPlan>?> PrepareMigrationTargetPlansAsync(
        int schemaId,
        string subject,
        Schema writerSchema,
        CancellationToken cancellationToken)
    {
        var targets = await _migrationRunner!.PrepareAsync(schemaId, subject, writerSchema, cancellationToken)
            .ConfigureAwait(false);
        Dictionary<int, AvroPocoReaderPlan>? plans = null;
        while (targets.MoveNext(out var target))
        {
            var plan = await GetPlanAsync(target.Id, cancellationToken).ConfigureAwait(false);
            PrepareTaggedTransformer(target.Id, target.Schema, plan);
            if (_inlineRuleValidators is not null)
            {
                _inlineRuleValidators.Register(
                    target.Schema,
                    GetValidationSchema(target.Id, target.Schema, plan));
            }
            (plans ??= []).Add(target.Id, plan);
        }
        return plans;
    }

    private void PrepareTaggedTransformer(int schemaId, Schema schema, AvroPocoReaderPlan plan)
    {
        var parsed = plan.ResolvedWriterSchema ??
                     (_resolvedSchemas.TryGetValue(schemaId, out var resolved) &&
                      ReferenceEquals(resolved.Plan, plan)
            ? resolved.Schema
            : AvroSchema.Parse(schema.SchemaString));
        _ = _taggedFieldTransformers.GetResolved(schema, parsed);
    }

    private bool TryGetPreparedRuleState(
        PreparedRuleKey key,
        int subjectGeneration,
        out PreparedRuleState state)
    {
        state = Volatile.Read(ref _lastPreparedRuleState)!;
        if (state is not null && state.Matches(key.SchemaId, key.Topic, key.IsKey, subjectGeneration))
            return true;

        if (!_preparedRuleStates.TryGetValue(key, out state!) || state.Generation != subjectGeneration)
            return false;

        Volatile.Write(ref _lastPreparedRuleState, state);
        return true;
    }

    private bool CachePreparedRuleState(
        PreparedRuleKey key,
        DeserializerSubjectNameCache.PreparedSubject preparedSubject,
        Schema schema,
        AvroPocoReaderPlan plan,
        Dictionary<int, AvroPocoReaderPlan>? migrationPlans)
    {
        if (_subjectNames is not null && !_subjectNames.IsCurrent(in preparedSubject))
            return false;

        var state = new PreparedRuleState(
            key,
            preparedSubject.Subject,
            schema,
            plan,
            migrationPlans,
            preparedSubject.Generation);
        if (!_preparedRuleStates.TryAdd(key, state))
        {
            if (_preparedRuleStates.TryGetValue(key, out var cached))
            {
                if (cached.Generation == preparedSubject.Generation)
                {
                    cached.SetMigrationPlans(migrationPlans);
                    Volatile.Write(ref _lastPreparedRuleState, cached);
                    return true;
                }

                if (_preparedRuleStates.TryUpdate(key, state, cached))
                {
                    Volatile.Write(ref _lastPreparedRuleState, state);
                    return true;
                }
            }
            return false;
        }

        Volatile.Write(ref _lastPreparedRuleState, state);
        Interlocked.Increment(ref _cachedPreparedRuleStateCount);
        _preparedRuleStateEvictionQueue.Enqueue(key);
        TrimPreparedRuleStateCache();
        return true;
    }

    private bool TryGetSubjectGeneration(string topic, bool isKey, out int generation)
    {
        if (_subjectNames is { RequiresPreparation: true } subjectNames)
        {
            if (subjectNames.TryGetPreparedSubject(
                    GeneratedSubjectCacheSchemaId,
                    topic,
                    isKey,
                    out var preparedSubject))
            {
                generation = preparedSubject.Generation;
                return true;
            }

            generation = 0;
            return false;
        }

        generation = 0;
        return true;
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
                if (_preparedRuleStates.TryRemove(oldest, out _))
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

        var plan = schema.References is { Count: > 0 } && _inlineRuleValidators is not null
            ? BuildResolvedValidationPlan(schemaId, schema)
            : BuildPlan(schemaId, schema);
        CacheSuccessfulPlan(schemaId, plan);
        return _plans.TryGetValue(schemaId, out cached) ? cached : plan;
    }

    private AvroPocoReaderPlan BuildResolvedValidationPlan(int schemaId, Schema schema)
    {
        var parsed = _inlineRuleValidators!.GetResolvedSchema(schema) as AvroRecordSchema
            ?? throw new InvalidOperationException("POCO Avro writer schema must be a record.");
        var plan = AvroPocoReaderPlanBuilder.Build<T, TCodec>(parsed);
        plan.ResolvedWriterSchema = parsed;
        _resolvedSchemas[schemaId] = new ResolvedAvroSchema(plan, parsed);
        return plan;
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
        plan.ResolvedWriterSchema = parsed;
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

    private async ValueTask PrepareGuidAsync(
        GuidTopicKey key,
        SerializationContext context,
        CancellationToken cancellationToken)
    {
        var resolved = await GetGuidSchemaAsync(key, cancellationToken).ConfigureAwait(false);
        if (resolved.DirectPlan is { } directPlan)
        {
            CacheSuccessfulPlan(resolved.SchemaId, directPlan);
            return;
        }

        await PrepareSchemaIdAsync(resolved.SchemaId, context, cancellationToken).ConfigureAwait(false);
    }

    private int GetResolvedGuidSchemaId(GuidTopicKey key)
    {
        if (_guidSchemaCache.TryGetValue(key, out var lazy)
            && lazy.IsValueCreated
            && lazy.Value.IsCompletedSuccessfully)
        {
            return lazy.Value.Result.SchemaId;
        }

        throw new InvalidOperationException(
            $"Schema GUID {key.SchemaGuid:D} is not cached. Consume through an asynchronous consumer API or call WarmupAsync first.");
    }

    private async Task<GuidResolvedSchema> GetGuidSchemaAsync(
        GuidTopicKey key,
        CancellationToken cancellationToken) =>
        await GetOrAddGuidSchemaLazy(key).Value.WaitAsync(cancellationToken).ConfigureAwait(false);

    private Lazy<Task<GuidResolvedSchema>> GetOrAddGuidSchemaLazy(GuidTopicKey key)
    {
        if (_guidSchemaCache.TryGetValue(key, out var cached))
            return cached;

        return _guidSchemaCache.GetOrAdd(
            key,
            static (cacheKey, deserializer) => new Lazy<Task<GuidResolvedSchema>>(
                () => deserializer.FetchGuidSchemaAsync(cacheKey)),
            this);
    }

    private async Task<GuidResolvedSchema> FetchGuidSchemaAsync(GuidTopicKey key)
    {
        try
        {
            var resolved = await SchemaRegistryOperationTimeout.ExecuteAsync(
                    cancellationToken => FetchGuidSchemaCoreAsync(key, cancellationToken),
                    RegistryTimeout,
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
                $"Schema GUID {key.SchemaGuid:D} is {unscopedSchema.SchemaType}, not Avro.");
        }
        if (_ruleExecutor is null && _migrationRunner is null)
        {
            var names = unscopedSchema.References is { Count: > 0 }
                ? await AvroSchemaReferenceResolver.ResolveAsync(
                        _schemaRegistry,
                        unscopedSchema,
                        cancellationToken)
                    .ConfigureAwait(false)
                : null;
            var schemaId = Interlocked.Decrement(ref _nextGuidPlanId);
            AvroPocoReaderPlan plan;
            if (_inlineRuleValidators is null)
            {
                plan = BuildPlan(schemaId, unscopedSchema, names);
            }
            else
            {
                ValidateAvroSchema(schemaId, unscopedSchema);
                var parsed = (names is null
                        ? AvroSchema.Parse(unscopedSchema.SchemaString)
                        : AvroSchema.Parse(unscopedSchema.SchemaString, names)) as AvroRecordSchema
                    ?? throw new InvalidOperationException("POCO Avro writer schema must be a record.");
                plan = AvroPocoReaderPlanBuilder.Build<T, TCodec>(parsed);
                plan.ResolvedWriterSchema = parsed;
            }
            CacheSuccessfulPlan(schemaId, plan);
            return new GuidResolvedSchema(schemaId, plan);
        }

        var subject = _subjectNames is null
            ? SubjectNameResolver.GetTopicSubjectName(key.Topic, key.IsKey)
            : await _subjectNames.ResolveSubjectNameAsync(
                    unscopedSchema,
                    key.Topic,
                    key.IsKey,
                    TCodec.FullName,
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

        var resolved = new GuidResolvedSchema(registered.Id, null);
        return resolved;
    }

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

    private Header? FindCallerIdentityHeader(SerializationContext context)
    {
        if (_config.SchemaIdStrategy == SchemaIdDeserializerStrategy.Prefix
            || context.Headers is not { } headers)
        {
            return null;
        }

        return headers.TryGetLastSchemaIdentity(context.Component, out var header)
            ? header
            : null;
    }

    private Header? FindRoutedIdentityHeader(
        SerializationContext context,
        in RecordHeaderRoutingLookup headers) =>
        _config.SchemaIdStrategy != SchemaIdDeserializerStrategy.Prefix
        && headers.TryGetLast(GetIdentityHeaderName(context.Component), out var header)
            ? header
            : null;

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

    private readonly record struct GuidTopicKey(
        Guid SchemaGuid,
        string Topic,
        bool IsKey,
        int SubjectGeneration);

    private sealed record GuidResolvedSchema(int SchemaId, AvroPocoReaderPlan? DirectPlan);

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

    private sealed class PreparedRuleState(
        PreparedRuleKey key,
        string subject,
        Schema schema,
        AvroPocoReaderPlan plan,
        Dictionary<int, AvroPocoReaderPlan>? migrationPlans,
        int generation)
    {
        internal PreparedRuleKey Key { get; } = key;
        internal string Subject { get; } = subject;
        internal Schema Schema { get; } = schema;
        internal AvroPocoReaderPlan Plan { get; } = plan;
        internal int Generation { get; } = generation;

        private Dictionary<int, AvroPocoReaderPlan>? _migrationPlans = migrationPlans;

        internal void SetMigrationPlans(Dictionary<int, AvroPocoReaderPlan>? plans) =>
            Volatile.Write(ref _migrationPlans, plans);

        internal bool TryGetPlan(int schemaId, out AvroPocoReaderPlan plan)
        {
            if (schemaId == Key.SchemaId)
            {
                plan = Plan;
                return true;
            }

            var plans = Volatile.Read(ref _migrationPlans);
            if (plans is not null && plans.TryGetValue(schemaId, out plan!))
                return true;

            plan = null!;
            return false;
        }

        internal bool Matches(int schemaId, string topic, bool isKey, int generation) =>
            Key.SchemaId == schemaId &&
            Key.IsKey == isKey &&
            Generation == generation &&
            (ReferenceEquals(Key.Topic, topic) || string.Equals(Key.Topic, topic, StringComparison.Ordinal));
    }
}
