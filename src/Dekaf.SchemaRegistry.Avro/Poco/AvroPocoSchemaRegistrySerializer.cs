using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Avro;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;
using RegistrySchema = Dekaf.SchemaRegistry.Schema;

namespace Dekaf.SchemaRegistry.Avro.Poco;

/// <summary>Schema Registry serializer backed by a generated POCO Avro codec.</summary>
public sealed class AvroPocoSchemaRegistrySerializer<T, TCodec>
    : ISerializer<T>, IAsyncSerializerPreparer<T>, IAsyncSerializerPreparationAdmission<T>,
      IRecordHeaderSerializer, IAsyncDisposable
    where TCodec : struct, IAvroPocoCodec<T>
{
    private const byte MagicByte = 0;
    private const int WireHeaderSize = 5;
    private const int MaxAssociatedNameInvalidationRetries = 4;
    private const int InitialPayloadSize = 256;
    private const int MaxRetainedPayloadSize = 1024 * 1024;
    private const byte StableRetainedPayloadPattern = 0;
    private const byte StableOversizedPayloadPattern = 2;
    private const byte OversizedThenRetainedPayloadPattern = 3;
    private const byte RetainedThenOversizedPayloadPattern = 4;
    private static readonly TimeSpan RegistryTimeout = TimeSpan.FromSeconds(30);

    [ThreadStatic]
    private static int t_retainedPayloadSizeHint;

    [ThreadStatic]
    private static int t_oversizedPayloadSizeHint;

    [ThreadStatic]
    private static byte t_payloadPattern;

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly AvroSerializerConfig _config;
    private readonly SchemaIdSerializerStrategy _schemaIdStrategy;
    private readonly SchemaSelectionMode _schemaSelectionMode;
    private readonly IAsyncSubjectNameStrategy? _asyncSubjectNameStrategy;
    private readonly bool _ownsClient;
    private readonly RegistrySchema _schema;
    private readonly SubjectSchemaIdCache _subjectCache = new();
    private readonly SchemaResolutionCache<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> _resolutionCache = new();
    private readonly AvroTaggedFieldTransformerProvider _taggedFieldTransformers;
    private readonly AvroInlineRuleValidatorProvider? _inlineRuleValidators;
    private long _lastInlineValidationDecision = -1;
    private AvroPocoSerializerBufferState? _primaryRuleBuffer;
    private ConditionalWeakTable<Thread, AvroPocoSerializerBufferState>? _additionalRuleBuffers;
    private SubjectSchemaIdCache? _associatedSubjectCache;

    bool IRecordHeaderSerializer.ProducesRecordHeaders =>
        _schemaIdStrategy == SchemaIdSerializerStrategy.Header;

    /// <summary>Creates a generated POCO Avro serializer.</summary>
    public AvroPocoSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        AvroSerializerConfig? config = null,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _taggedFieldTransformers = new AvroTaggedFieldTransformerProvider(schemaRegistry);
        _config = config ?? new AvroSerializerConfig();
        _schemaIdStrategy = _config.SchemaIdStrategy;
        _schemaSelectionMode = SchemaRegistrySerializerConfigValidator.ValidateAndResolve(
            _config.UseSchemaId,
            _config.UseLatestVersion,
            _config.AutoRegisterSchemas);
        if (_schemaIdStrategy is not (SchemaIdSerializerStrategy.Prefix or SchemaIdSerializerStrategy.Header))
            throw new ArgumentOutOfRangeException(nameof(config), _schemaIdStrategy, "Unknown schema identity strategy.");
        _inlineRuleValidators = AvroValidationConfiguration.Create(
            schemaRegistry,
            _config.ValidationRulesExecution,
            _config.RuleExecutor);
        if (_config.CustomSubjectNameStrategy is null)
        {
            _asyncSubjectNameStrategy = _config.AsyncSubjectNameStrategy
                ?? (_config.SubjectNameStrategy == SubjectNameStrategy.AssociatedName
                    ? new AssociatedNameStrategy(schemaRegistry)
                    : null);
        }
        _ownsClient = ownsClient;
        _schema = new RegistrySchema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = TCodec.SchemaJson
        };
        if (_asyncSubjectNameStrategy is not null)
            _associatedSubjectCache = new SubjectSchemaIdCache();

        if (_asyncSubjectNameStrategy is AssociatedNameStrategy associatedNameStrategy)
        {
            AssociatedNameCacheInvalidationTargetRegistration.Register(
                this,
                associatedNameStrategy,
                InvalidateAssociatedSubjectSchemaCache);
        }
    }

    /// <summary>Prepares one topic/component and returns its Schema Registry ID.</summary>
    public async Task<int> WarmupAsync(
        string topic,
        bool isKey = false,
        CancellationToken cancellationToken = default)
    {
        var resolved = await PrepareAsync(topic, isKey, cancellationToken).ConfigureAwait(false);
        return resolved.SchemaId;
    }

    /// <summary>Resolves the subject and schema ID asynchronously.</summary>
    public ValueTask<ResolvedSchemaContext> PrepareAsync(
        string topic,
        bool isKey = false,
        CancellationToken cancellationToken = default)
    {
        var cache = _subjectCache;
        ValueTask<ResolvedSchemaContext> preparation;
        if (cache.TryGet(topic, isKey, out var cached))
        {
            preparation = new ValueTask<ResolvedSchemaContext>(ToResolvedContext(cached));
            return PrepareInlineValidationAsync(preparation, cancellationToken);
        }
        if (_asyncSubjectNameStrategy is not null)
        {
            cache = Volatile.Read(ref _associatedSubjectCache)!;
            if (cache.TryGet(topic, isKey, out cached))
            {
                preparation = new ValueTask<ResolvedSchemaContext>(ToResolvedContext(cached));
            }
            else
            {
                preparation = PrepareAssociatedCoreAsync(
                    topic,
                    isKey,
                    cache,
                    cancellationToken);
            }
            return PrepareInlineValidationAsync(preparation, cancellationToken);
        }

        var subject = GetSubjectName(topic, isKey);
        var resolution = ResolveSchemaAsync(subject, cancellationToken);
        if (resolution.IsCompletedSuccessfully)
        {
            var value = resolution.Result;
            preparation = new ValueTask<ResolvedSchemaContext>(ToResolvedContext(
                cache.CacheEntry(
                    topic,
                    isKey,
                    subject,
                    in value)));
        }
        else
        {
            preparation = AwaitResolutionAsync(topic, isKey, subject, cache, resolution);
        }
        return PrepareInlineValidationAsync(preparation, cancellationToken);
    }

    private ValueTask<ResolvedSchemaContext> PrepareInlineValidationAsync(
        ValueTask<ResolvedSchemaContext> preparation,
        CancellationToken cancellationToken)
    {
        if (_inlineRuleValidators is null)
            return preparation;
        if (preparation.IsCompletedSuccessfully)
        {
            var resolved = preparation.Result;
            var cached = Volatile.Read(ref _lastInlineValidationDecision);
            if (cached >= 0 && (int)(cached >> 1) == resolved.SchemaId)
                return new ValueTask<ResolvedSchemaContext>(resolved);
            var validationPreparation = _inlineRuleValidators.PrepareSerializerSchemaAsync(
                resolved.Schema,
                GeneratedSchema.Value,
                cancellationToken);
            if (validationPreparation.IsCompletedSuccessfully)
            {
                CacheInlineValidationDecision(resolved.SchemaId, validationPreparation.Result);
                return new ValueTask<ResolvedSchemaContext>(resolved);
            }
            return AwaitValidationCompletionAsync(this, resolved, validationPreparation);
        }

        return AwaitPreparationAndValidationAsync(this, preparation, cancellationToken);

        static async ValueTask<ResolvedSchemaContext> AwaitValidationCompletionAsync(
            AvroPocoSchemaRegistrySerializer<T, TCodec> serializer,
            ResolvedSchemaContext resolved,
            ValueTask<AvroInlineRuleValidator> pending)
        {
            var validator = await pending.ConfigureAwait(false);
            serializer.CacheInlineValidationDecision(resolved.SchemaId, validator);
            return resolved;
        }

        static async ValueTask<ResolvedSchemaContext> AwaitPreparationAndValidationAsync(
            AvroPocoSchemaRegistrySerializer<T, TCodec> serializer,
            ValueTask<ResolvedSchemaContext> pending,
            CancellationToken cancellationToken)
        {
            var resolved = await pending.ConfigureAwait(false);
            var cached = Volatile.Read(ref serializer._lastInlineValidationDecision);
            if (cached >= 0 && (int)(cached >> 1) == resolved.SchemaId)
                return resolved;
            var validator = await serializer._inlineRuleValidators!.PrepareSerializerSchemaAsync(
                    resolved.Schema,
                    GeneratedSchema.Value,
                    cancellationToken)
                .ConfigureAwait(false);
            serializer.CacheInlineValidationDecision(resolved.SchemaId, validator);
            return resolved;
        }
    }

    private async ValueTask<ResolvedSchemaContext> PrepareAssociatedCoreAsync(
        string topic,
        bool isKey,
        SubjectSchemaIdCache cache,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAssociatedNameInvalidationRetries; attempt++)
        {
            var subject = await _asyncSubjectNameStrategy!.GetSubjectNameAsync(
                    topic,
                    TCodec.FullName,
                    isKey,
                    cancellationToken)
                .ConfigureAwait(false);
            var value = await ResolveSchemaAsync(subject, cancellationToken).ConfigureAwait(false);
            if (ReferenceEquals(cache, Volatile.Read(ref _associatedSubjectCache)))
            {
                var cached = cache.CacheEntry(
                    topic,
                    isKey,
                    subject,
                    in value);
                if (ReferenceEquals(cache, Volatile.Read(ref _associatedSubjectCache)))
                    return ToResolvedContext(cached);
            }

            cache = Volatile.Read(ref _associatedSubjectCache)!;
            if (cache.TryGet(topic, isKey, out var current))
                return ToResolvedContext(current);
        }

        throw new InvalidOperationException(
            "Associated-name cache changed repeatedly while preparing the generated Avro serializer.");
    }

    /// <inheritdoc />
    public ValueTask PrepareAsync(
        T value,
        SerializationContext context,
        CancellationToken cancellationToken = default)
    {
        var preparation = PrepareAsync(
            context.Topic,
            context.Component == SerializationComponent.Key,
            cancellationToken);
        if (preparation.IsCompletedSuccessfully)
        {
            _ = preparation.Result;
            return ValueTask.CompletedTask;
        }

        return AwaitPreparationAsync(preparation);
    }

    ValueTask<SerializerPreparationAdmission>
        IAsyncSerializerPreparationAdmission<T>.PrepareForSerializationAsync(
            T value,
            SerializationContext context,
            CancellationToken cancellationToken)
    {
        var preparation = PrepareAsync(
            context.Topic,
            context.Component == SerializationComponent.Key,
            cancellationToken);
        return preparation.IsCompletedSuccessfully
            ? new ValueTask<SerializerPreparationAdmission>(
                ToAdmission(preparation.Result))
            : AwaitAdmissionAsync(this, preparation);

        static async ValueTask<SerializerPreparationAdmission> AwaitAdmissionAsync(
            AvroPocoSchemaRegistrySerializer<T, TCodec> serializer,
            ValueTask<ResolvedSchemaContext> pending) =>
            serializer.ToAdmission(await pending.ConfigureAwait(false));
    }

    /// <inheritdoc />
    public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        if (default(T) is null && value is null)
            throw new ArgumentNullException(nameof(value));

        var entry = GetSchemaForContext(
            context.Topic,
            context.Component == SerializationComponent.Key);
        AvroInlineRuleValidator? inlineValidator = null;
        if (_config.RuleExecutor is null &&
            (_inlineRuleValidators is null ||
             (inlineValidator = GetInlineValidator(entry.SchemaId, entry.Schema!)) is null))
        {
            if (_schemaIdStrategy == SchemaIdSerializerStrategy.Prefix)
                SerializeDirect(value, ref destination, entry.SchemaId);
            else
                SerializeDirectWithHeader(value, ref destination, context, entry);
            return;
        }

        if (_config.RuleExecutor is null || entry.Schema!.RuleSet is null)
        {
            SerializeWithRules(value, ref destination, context, entry, _config.RuleExecutor, inlineValidator);
            return;
        }

        SerializeWithTaggedRules(
            value,
            ref destination,
            context,
            entry,
            _config.RuleExecutor!,
            _taggedFieldTransformers);
    }

    void IAsyncSerializerPreparationAdmission<T>.SerializePrepared<TWriter>(
        T value,
        ref TWriter destination,
        SerializationContext context,
        in SerializerPreparationAdmission admission)
    {
        if (default(T) is null && value is null)
            throw new ArgumentNullException(nameof(value));

        var entry = SubjectSchemaIdCache.FromAdmission(
            context.Topic,
            context.Component == SerializationComponent.Key,
            admission);
        SerializeCore(value, ref destination, context, entry);
    }

    // Keep the public Serialize body inline; routing it through this helper measured 5.5% slower.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SerializeCore<TWriter>(
        T value,
        ref TWriter destination,
        SerializationContext context,
        SubjectSchemaIdCache.SubjectSchemaIdCacheEntry entry)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        AvroInlineRuleValidator? inlineValidator = null;
        if (_config.RuleExecutor is null &&
            (_inlineRuleValidators is null ||
             (inlineValidator = GetInlineValidator(entry.SchemaId, entry.Schema!)) is null))
        {
            if (_schemaIdStrategy == SchemaIdSerializerStrategy.Prefix)
                SerializeDirect(value, ref destination, entry.SchemaId);
            else
                SerializeDirectWithHeader(value, ref destination, context, entry);
            return;
        }

        if (_config.RuleExecutor is null || entry.Schema!.RuleSet is null)
        {
            SerializeWithRules(value, ref destination, context, entry, _config.RuleExecutor, inlineValidator);
            return;
        }

        SerializeWithTaggedRules(
            value,
            ref destination,
            context,
            entry,
            _config.RuleExecutor!,
            _taggedFieldTransformers);
    }

    private SerializerPreparationAdmission ToAdmission(in ResolvedSchemaContext context)
    {
        var schemaGuidFrame = _schemaIdStrategy == SchemaIdSerializerStrategy.Header
            ? context.SchemaGuidFrame
            : null;
        return new(context.Subject, context.SchemaId, context.Schema, schemaGuidFrame);
    }

    private static async ValueTask AwaitPreparationAsync(ValueTask<ResolvedSchemaContext> preparation) =>
        _ = await preparation.ConfigureAwait(false);

    private static async ValueTask<ResolvedSchemaContext> AwaitResolutionAsync(
        string topic,
        bool isKey,
        string subject,
        SubjectSchemaIdCache cache,
        ValueTask<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> resolution)
    {
        var value = await resolution.ConfigureAwait(false);
        return ToResolvedContext(
            cache.CacheEntry(
                topic,
                isKey,
                subject,
                in value));
    }

    private static ResolvedSchemaContext ToResolvedContext(SubjectSchemaIdCache.SubjectSchemaIdCacheEntry entry) =>
        new(entry.Subject!, entry.SchemaId, entry.Schema!)
        {
            SchemaGuidFrame = entry.SchemaGuidFrame
        };

    private static void SerializeDirect<TWriter>(T value, ref TWriter destination, int schemaId)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        var payloadSize = GetPredictedDirectPayloadSize();
        while (true)
        {
            var memory = destination.GetMemory(WireHeaderSize + payloadSize);
            var writer = new AvroValueWriter(memory.Span.Slice(WireHeaderSize));
            TCodec.Write(ref writer, value);
            if (!writer.IsComplete)
            {
                payloadSize = Grow(payloadSize);
                continue;
            }

            var span = memory.Span;
            span[0] = MagicByte;
            BinaryPrimitives.WriteInt32BigEndian(span.Slice(1, 4), schemaId);
            destination.Advance(WireHeaderSize + writer.WrittenCount);
            RecordDirectPayloadLength(writer.WrittenCount);
            return;
        }
    }

    private static void SerializeDirectWithHeader<TWriter>(
        T value,
        ref TWriter destination,
        SerializationContext context,
        SubjectSchemaIdCache.SubjectSchemaIdCacheEntry entry)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        var payloadSize = GetPredictedDirectPayloadSize();
        while (true)
        {
            var memory = destination.GetMemory(payloadSize);
            var writer = new AvroValueWriter(memory.Span);
            TCodec.Write(ref writer, value);
            if (!writer.IsComplete)
            {
                payloadSize = Grow(payloadSize);
                continue;
            }

            SchemaIdentitySerialization.WriteIdentity(
                memory.Span,
                context,
                in entry,
                SchemaIdSerializerStrategy.Header);
            destination.Advance(writer.WrittenCount);
            RecordDirectPayloadLength(writer.WrittenCount);
            return;
        }
    }

    private static int GetPredictedDirectPayloadSize()
    {
        var pattern = t_payloadPattern;
        var sizeHint = pattern is StableOversizedPayloadPattern or OversizedThenRetainedPayloadPattern
            ? t_oversizedPayloadSizeHint
            : t_retainedPayloadSizeHint;
        return sizeHint is > 0 ? sizeHint : InitialPayloadSize;
    }

    private static void RecordDirectPayloadLength(int length)
    {
        var pattern = t_payloadPattern;
        if (length > MaxRetainedPayloadSize)
        {
            t_oversizedPayloadSizeHint = length;
            if (pattern != StableOversizedPayloadPattern)
            {
                t_payloadPattern =
                    pattern is OversizedThenRetainedPayloadPattern or StableRetainedPayloadPattern
                        ? RetainedThenOversizedPayloadPattern
                        : StableOversizedPayloadPattern;
            }
            return;
        }

        t_retainedPayloadSizeHint = Math.Max(InitialPayloadSize, length);
        if (pattern != StableRetainedPayloadPattern)
        {
            t_payloadPattern =
                pattern is StableOversizedPayloadPattern or RetainedThenOversizedPayloadPattern
                    ? OversizedThenRetainedPayloadPattern
                    : StableRetainedPayloadPattern;
        }
    }

    private void SerializeWithRules<TWriter>(
        T value,
        ref TWriter destination,
        SerializationContext context,
        SubjectSchemaIdCache.SubjectSchemaIdCacheEntry entry,
        ISchemaRegistryRuleExecutor? ruleExecutor,
        AvroInlineRuleValidator? inlineValidator)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        var bufferState = GetRuleBufferState();
        var buffer = GetRuleBuffer(bufferState, out var bufferIsPooled, out var ownsBufferLease);
        try
        {
            int length;
            while (true)
            {
                var writer = new AvroValueWriter(buffer);
                TCodec.Write(ref writer, value);
                if (writer.IsComplete)
                {
                    length = writer.WrittenCount;
                    RecordRulePayloadLength(bufferState, length);
                    break;
                }

                var nextLength = Grow(buffer.Length);
                var nextBufferIsPooled = bufferIsPooled || nextLength > MaxRetainedPayloadSize;
                var nextBuffer = nextBufferIsPooled
                    ? ArrayPool<byte>.Shared.Rent(nextLength)
                    : GC.AllocateUninitializedArray<byte>(nextLength);
                if (bufferIsPooled)
                    ArrayPool<byte>.Shared.Return(buffer);
                else if (!nextBufferIsPooled)
                    RetainRuleBuffer(bufferState, nextBuffer);
                buffer = nextBuffer;
                bufferIsPooled = nextBufferIsPooled;
            }

            var payload = new ReadOnlyMemory<byte>(buffer, 0, length);
            if (ruleExecutor is null)
            {
                inlineValidator!.Validate(
                    payload,
                    entry.SchemaId,
                    _config.ValidationRulesFailFast);
            }
            else
            {
                var ruleContext = SchemaRegistryRuleContext.Rent(
                    context.Topic,
                    context.Component,
                    entry.SchemaId,
                    entry.Subject!,
                    entry.Schema!,
                    SchemaRegistryPayloadFormat.Avro);
                try
                {
                    payload = TransformSerializedPayload(payload, ruleContext, entry);
                }
                finally
                {
                    ruleContext.Return();
                }
            }

            var payloadOffset = SchemaIdentitySerialization.GetPayloadOffset(_schemaIdStrategy);
            var output = destination.GetSpan(payloadOffset + payload.Length);
            SchemaIdentitySerialization.WriteIdentity(
                output,
                context,
                in entry,
                _schemaIdStrategy);
            payload.Span.CopyTo(output[payloadOffset..]);
            destination.Advance(payloadOffset + payload.Length);
        }
        finally
        {
            if (ownsBufferLease)
                bufferState.RuleBufferInUse = false;
            if (bufferIsPooled)
                ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private AvroInlineRuleValidator? GetInlineValidator(int schemaId, RegistrySchema registrySchema)
    {
        var cached = Volatile.Read(ref _lastInlineValidationDecision);
        if (cached >= 0 && (int)(cached >> 1) == schemaId && (cached & 1) == 0)
            return null;

        var validator = _inlineRuleValidators!.RegisterSerializerSchema(registrySchema, GeneratedSchema.Value);
        var hasRules = validator.HasAnyRules;
        CacheInlineValidationDecision(schemaId, validator);
        return hasRules ? validator : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CacheInlineValidationDecision(
        int schemaId,
        AvroInlineRuleValidator validator) =>
        Volatile.Write(
            ref _lastInlineValidationDecision,
            ((long)schemaId << 1) | (validator.HasAnyRules ? 1L : 0L));

    private void SerializeWithTaggedRules<TWriter>(
        T value,
        ref TWriter destination,
        SerializationContext context,
        SubjectSchemaIdCache.SubjectSchemaIdCacheEntry entry,
        ISchemaRegistryRuleExecutor ruleExecutor,
        AvroTaggedFieldTransformerProvider taggedFieldTransformers)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        var bufferState = GetRuleBufferState();
        var buffer = GetRuleBuffer(bufferState, out var bufferIsPooled, out var ownsBufferLease);
        try
        {
            int length;
            while (true)
            {
                var writer = new AvroValueWriter(buffer);
                TCodec.Write(ref writer, value);
                if (writer.IsComplete)
                {
                    length = writer.WrittenCount;
                    RecordRulePayloadLength(bufferState, length);
                    break;
                }

                var nextLength = Grow(buffer.Length);
                var nextBufferIsPooled = bufferIsPooled || nextLength > MaxRetainedPayloadSize;
                var nextBuffer = nextBufferIsPooled
                    ? ArrayPool<byte>.Shared.Rent(nextLength)
                    : GC.AllocateUninitializedArray<byte>(nextLength);
                if (bufferIsPooled)
                    ArrayPool<byte>.Shared.Return(buffer);
                else if (!nextBufferIsPooled)
                    RetainRuleBuffer(bufferState, nextBuffer);
                buffer = nextBuffer;
                bufferIsPooled = nextBufferIsPooled;
            }

            var payload = new ReadOnlyMemory<byte>(buffer, 0, length);
            payload = TransformWithTaggedFields(
                payload,
                context,
                entry,
                ruleExecutor,
                taggedFieldTransformers);

            var payloadOffset = SchemaIdentitySerialization.GetPayloadOffset(_schemaIdStrategy);
            var output = destination.GetSpan(payloadOffset + payload.Length);
            SchemaIdentitySerialization.WriteIdentity(
                output,
                context,
                in entry,
                _schemaIdStrategy);
            payload.Span.CopyTo(output[payloadOffset..]);
            destination.Advance(payloadOffset + payload.Length);
        }
        finally
        {
            if (ownsBufferLease)
                bufferState.RuleBufferInUse = false;
            if (bufferIsPooled)
                ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private ReadOnlyMemory<byte> TransformWithTaggedFields(
        ReadOnlyMemory<byte> payload,
        SerializationContext context,
        SubjectSchemaIdCache.SubjectSchemaIdCacheEntry entry,
        ISchemaRegistryRuleExecutor ruleExecutor,
        AvroTaggedFieldTransformerProvider taggedFieldTransformers)
    {
        var taggedWorkspaceOperation = AvroTaggedFieldTransformerProvider.BeginOperation();
        try
        {
            var ruleContext = SchemaRegistryRuleContext.RentWithTaggedFieldTransformer(
                context.Topic,
                context.Component,
                entry.SchemaId,
                entry.Subject!,
                entry.Schema!,
                SchemaRegistryPayloadFormat.Avro,
                taggedFieldTransformers.Get(entry.Schema!, GeneratedSchema.Value));
            try
            {
                return TransformSerializedPayload(payload, ruleContext, entry);
            }
            finally
            {
                ruleContext.Return();
            }
        }
        finally
        {
            taggedWorkspaceOperation.Dispose();
        }
    }

    private ReadOnlyMemory<byte> TransformSerializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        SubjectSchemaIdCache.SubjectSchemaIdCacheEntry entry)
    {
        if (_inlineRuleValidators is null ||
            _config.RuleExecutor is not SchemaRegistryRuleExecutor ruleExecutor)
        {
            return _config.RuleExecutor!.TransformSerializedPayload(payload, context);
        }

        var validator = _inlineRuleValidators.RegisterSerializerSchema(entry.Schema!, GeneratedSchema.Value);
        if (_config.ValidationRulesExecution == ValidationRulesExecution.BeforeDomainRules)
            validator.Validate(payload, entry.SchemaId, _config.ValidationRulesFailFast);
        payload = ruleExecutor.TransformSerializedDomainPayload(payload, context);
        if (_config.ValidationRulesExecution == ValidationRulesExecution.AfterDomainRules)
            validator.Validate(payload, entry.SchemaId, _config.ValidationRulesFailFast);
        return ruleExecutor.TransformSerializedEncodingPayload(payload, context);
    }

    private SubjectSchemaIdCache.SubjectSchemaIdCacheEntry GetSchemaForContext(string topic, bool isKey)
    {
        if (_subjectCache.TryGet(topic, isKey, out var cached))
            return cached;

        return GetSchemaForContextSlow(topic, isKey);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private SubjectSchemaIdCache.SubjectSchemaIdCacheEntry GetSchemaForContextSlow(string topic, bool isKey)
    {
        var cache = _asyncSubjectNameStrategy is null
            ? _subjectCache
            : Volatile.Read(ref _associatedSubjectCache)!;
        if (cache.TryGet(topic, isKey, out var cached))
            return cached;

        if (_asyncSubjectNameStrategy is not null)
        {
            throw new InvalidOperationException(
                "The asynchronous subject-name strategy requires PrepareAsync before serialization.");
        }

        return cache.GetOrAdd(
            topic,
            isKey,
            this,
            static (serializer, currentTopic, currentIsKey) =>
                serializer.ResolveSchemaCached(currentTopic, currentIsKey));
    }

    private SubjectSchemaIdCache.SubjectSchemaIdCacheEntry ResolveSchemaCached(string topic, bool isKey)
    {
        var subject = GetSubjectName(topic, isKey);
        var value = _resolutionCache.Resolve(
            subject,
            _schema,
            this,
            static (serializer, resolvedSubject, schema) =>
                serializer.FetchSchemaWithTimeoutAsync(resolvedSubject, schema),
            RegistryTimeout);
        return new SubjectSchemaIdCache.SubjectSchemaIdCacheEntry(
            new SubjectSchemaIdCache.SubjectSchemaIdCacheKey(topic, isKey),
            subject,
            value.SchemaId,
            value.Schema,
            value.SchemaGuidFrame);
    }

    private ValueTask<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> ResolveSchemaAsync(
        string subject,
        CancellationToken cancellationToken) =>
        _resolutionCache.ResolveAsync(
            subject,
            _schema,
            this,
            static (serializer, resolvedSubject, schema) =>
                serializer.FetchSchemaWithTimeoutAsync(resolvedSubject, schema),
            cancellationToken);

    private Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> FetchSchemaWithTimeoutAsync(
        string subject,
        RegistrySchema schema) =>
        SchemaRegistryOperationTimeout.ExecuteAsync(
            cancellationToken => FetchSchemaAsync(subject, schema, cancellationToken),
            RegistryTimeout,
            "Schema Registry resolution timed out.");

    private async Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> FetchSchemaAsync(
        string subject,
        RegistrySchema schema,
        CancellationToken cancellationToken)
    {
        if (_schemaSelectionMode == SchemaSelectionMode.ExplicitId)
        {
            var schemaId = _config.UseSchemaId!.Value;
            var explicitSchema = await _schemaRegistry.GetSchemaAsync(
                    schemaId,
                    subject,
                    cancellationToken)
                .ConfigureAwait(false);
            await ValidateSelectedSchemaAsync(explicitSchema, $"Schema ID {schemaId}", cancellationToken).ConfigureAwait(false);
            return await CreateResolvedValueAsync(
                    subject,
                    schemaId,
                    explicitSchema,
                    registeredSchema: null,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (_schemaSelectionMode == SchemaSelectionMode.Latest)
        {
            var registered = await _schemaRegistry.GetSchemaBySubjectAsync(
                    subject,
                    "latest",
                    cancellationToken)
                .ConfigureAwait(false);
            return await ValidateLatestSchemaAsync(registered, cancellationToken).ConfigureAwait(false);
        }

        if (_schemaSelectionMode == SchemaSelectionMode.AutoRegister)
        {
            var schemaId = _config.NormalizeSchemas
                ? await _schemaRegistry.GetOrRegisterSchemaAsync(
                    subject,
                    schema,
                    normalize: true,
                    cancellationToken).ConfigureAwait(false)
                : await _schemaRegistry.GetOrRegisterSchemaAsync(
                    subject,
                    schema,
                    cancellationToken).ConfigureAwait(false);
            var registeredSchema = _config.RuleExecutor is SchemaRegistryRuleExecutor ||
                                   _config.ValidationRulesExecution != ValidationRulesExecution.Disabled
                ? await _schemaRegistry.GetSchemaAsync(schemaId, subject, cancellationToken).ConfigureAwait(false)
                : schema;
            return await CreateResolvedValueAsync(
                    subject,
                    schemaId,
                    registeredSchema,
                    registeredSchema: null,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var existing = await _schemaRegistry.LookupSchemaAsync(
                subject,
                schema,
                ignoreDeletedSchemas: true,
                normalize: _config.NormalizeSchemas,
                cancellationToken)
            .ConfigureAwait(false);
        return await CreateResolvedValueAsync(
                subject,
                existing.Id,
                existing.Schema,
                existing,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> ValidateLatestSchemaAsync(
        RegisteredSchema registered,
        CancellationToken cancellationToken)
    {
        await ValidateSelectedSchemaAsync(
                registered.Schema,
                $"Latest schema version {registered.Version} for subject '{registered.Subject}'",
                cancellationToken)
            .ConfigureAwait(false);

        return await CreateResolvedValueAsync(
                registered.Subject,
                registered.Id,
                registered.Schema,
                registered,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> CreateResolvedValueAsync(
        string subject,
        int schemaId,
        RegistrySchema schema,
        RegisteredSchema? registeredSchema,
        CancellationToken cancellationToken) =>
        SchemaIdentityResolution.CreateSerializerValueAsync(
            _schemaRegistry,
            subject,
            schemaId,
            schema,
            _schemaIdStrategy,
            _config.NormalizeSchemas,
            registeredSchema,
            cancellationToken);

    private async Task ValidateSelectedSchemaAsync(
        RegistrySchema registrySchema,
        string selection,
        CancellationToken cancellationToken)
    {
        AvroSchema? selectedSchema = null;
        if (registrySchema.SchemaType == SchemaType.Avro)
        {
            var names = registrySchema.References is { Count: > 0 }
                ? await AvroSchemaReferenceResolver.ResolveAsync(
                        _schemaRegistry,
                        registrySchema,
                        cancellationToken)
                    .ConfigureAwait(false)
                : null;
            selectedSchema = names is null
                ? AvroSchema.Parse(registrySchema.SchemaString)
                : AvroSchema.Parse(registrySchema.SchemaString, names);
        }

        if (!GeneratedSchema.Value.Equals(selectedSchema))
        {
            throw new InvalidOperationException(
                $"{selection} does not match generated POCO schema '{TCodec.FullName}'.");
        }
    }

    private static class GeneratedSchema
    {
        internal static readonly AvroSchema Value = AvroSchema.Parse(TCodec.SchemaJson);
    }

    private string GetSubjectName(string topic, bool isKey) =>
        _config.CustomSubjectNameStrategy?.GetSubjectName(topic, TCodec.FullName, isKey)
        ?? SubjectNameResolver.GetSubjectName(
            _config.SubjectNameStrategy,
            topic,
            TCodec.FullName,
            isKey,
            _config.UseLegacySubjectNames);

    private static int Grow(int current)
    {
        var maximum = Array.MaxLength - WireHeaderSize;
        if (current >= maximum)
            throw new NotSupportedException($"Avro payloads larger than {maximum} bytes are not supported.");
        return (int)Math.Min((long)current * 2, maximum);
    }

    private AvroPocoSerializerBufferState GetRuleBufferState()
    {
        var threadId = Environment.CurrentManagedThreadId;
        var primary = Volatile.Read(ref _primaryRuleBuffer);
        if (primary is not null && primary.ThreadId == threadId)
            return primary;

        return GetRuleBufferStateSlow(threadId, primary);
    }

    private AvroPocoSerializerBufferState GetRuleBufferStateSlow(
        int threadId,
        AvroPocoSerializerBufferState? primary)
    {
        if (primary is null)
        {
            var created = new AvroPocoSerializerBufferState(threadId);
            primary = Interlocked.CompareExchange(ref _primaryRuleBuffer, created, null);
            if (primary is null)
                return created;
            if (primary.ThreadId == threadId)
                return primary;
        }

        var additional = Volatile.Read(ref _additionalRuleBuffers);
        if (additional is null)
        {
            var created = new ConditionalWeakTable<Thread, AvroPocoSerializerBufferState>();
            additional = Interlocked.CompareExchange(ref _additionalRuleBuffers, created, null) ?? created;
        }

        return additional.GetValue(
            Thread.CurrentThread,
            static currentThread => new AvroPocoSerializerBufferState(currentThread.ManagedThreadId));
    }

    private static byte[] GetRuleBuffer(
        AvroPocoSerializerBufferState state,
        out bool bufferIsPooled,
        out bool ownsBufferLease)
    {
        var sizeHint = GetPredictedRulePayloadSize(state);
        if (state.RuleBufferInUse)
        {
            bufferIsPooled = true;
            ownsBufferLease = false;
            return ArrayPool<byte>.Shared.Rent(Math.Max(1024, sizeHint));
        }

        byte[] buffer;
        if (sizeHint > MaxRetainedPayloadSize)
        {
            bufferIsPooled = true;
            buffer = ArrayPool<byte>.Shared.Rent(sizeHint);
        }
        else
        {
            bufferIsPooled = false;
            buffer = state.RuleBuffer ??= GC.AllocateUninitializedArray<byte>(1024);
        }

        state.RuleBufferInUse = true;
        ownsBufferLease = true;
        return buffer;
    }

    private static int GetPredictedRulePayloadSize(AvroPocoSerializerBufferState state)
    {
        var pattern = state.RulePayloadPattern;
        return pattern is StableOversizedPayloadPattern or OversizedThenRetainedPayloadPattern
            ? state.OversizedRulePayloadSizeHint
            : state.RetainedRulePayloadSizeHint;
    }

    private static void RecordRulePayloadLength(AvroPocoSerializerBufferState state, int length)
    {
        var pattern = state.RulePayloadPattern;
        var oversized = length > MaxRetainedPayloadSize;
        if (oversized)
        {
            state.OversizedRulePayloadSizeHint = length;
            if (pattern != StableOversizedPayloadPattern)
            {
                state.RulePayloadPattern =
                    pattern is OversizedThenRetainedPayloadPattern or StableRetainedPayloadPattern
                        ? RetainedThenOversizedPayloadPattern
                        : StableOversizedPayloadPattern;
            }
            return;
        }

        state.RetainedRulePayloadSizeHint = Math.Max(InitialPayloadSize, length);
        if (pattern != StableRetainedPayloadPattern)
        {
            state.RulePayloadPattern =
                pattern is StableOversizedPayloadPattern or RetainedThenOversizedPayloadPattern
                    ? OversizedThenRetainedPayloadPattern
                    : StableRetainedPayloadPattern;
        }
    }

    private static void RetainRuleBuffer(AvroPocoSerializerBufferState state, byte[] buffer) =>
        state.RuleBuffer = buffer;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }

    private void InvalidateAssociatedSubjectSchemaCache()
    {
        Volatile.Write(ref _associatedSubjectCache, new SubjectSchemaIdCache());
    }

}

/// <summary>Rules buffer and sizing state for one serializer on one thread.</summary>
internal sealed class AvroPocoSerializerBufferState
{
    internal AvroPocoSerializerBufferState(int threadId) => ThreadId = threadId;

    internal int ThreadId { get; }

    internal byte[]? RuleBuffer;

    internal int RetainedRulePayloadSizeHint;

    internal int OversizedRulePayloadSizeHint;

    // Predicts from the prior two retained/oversized outcomes, covering stable and alternating traffic.
    internal byte RulePayloadPattern;

    internal bool RuleBufferInUse;
}
