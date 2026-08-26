using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Dekaf.Serialization;

namespace Dekaf.SchemaRegistry;

internal delegate ReadOnlyMemory<byte> SchemaRegistryFieldTransform<TState>(
    ReadOnlyMemory<byte> value,
    SchemaRegistryRuleHandlerContext context,
    TState state);

internal interface ISchemaRegistryTaggedFieldTransformer
{
    ReadOnlyMemory<byte> Transform<TState>(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context,
        TState state,
        SchemaRegistryFieldTransform<TState> transform);
}

internal interface ISchemaRegistryTaggedFieldTransformerProvider
{
    ISchemaRegistryTaggedFieldTransformer Get(Schema payloadSchema, Schema? ruleOwnerSchema = null);
}

/// <summary>
/// Schema Registry payload format exposed to rule executors.
/// </summary>
public enum SchemaRegistryPayloadFormat
{
    /// <summary>
    /// Custom Schema Registry serializer payload.
    /// </summary>
    Custom,

    /// <summary>
    /// JSON Schema payload.
    /// </summary>
    Json,

    /// <summary>
    /// Avro binary payload.
    /// </summary>
    Avro,

    /// <summary>
    /// Protobuf message payload.
    /// </summary>
    Protobuf
}

/// <summary>
/// Context passed to Schema Registry payload rule executors.
/// </summary>
/// <remarks>
/// Context instances supplied to an executor are borrowed and valid only for the synchronous
/// duration of the transform call. Implementations must not retain them.
/// </remarks>
public sealed class SchemaRegistryRuleContext
{
    [ThreadStatic]
    private static SchemaRegistryRuleContext? t_primary;

    [ThreadStatic]
    private static SchemaRegistryRuleContext? t_overflow;

    private string _topic = null!;
    private SerializationComponent _component;
    private int _schemaId;
    private string? _subject;
    private Schema? _schema;
    private Schema? _sourceSchema;
    private Schema? _targetSchema;
    private SchemaRuleMode? _ruleMode;
    private SchemaRegistryPayloadFormat _payloadFormat;
    private ISchemaRegistryTaggedFieldTransformer? _taggedFieldTransformer;
    private bool _inUse;
    private SchemaRegistryRuleContext? _next;

    /// <summary>
    /// Gets the Kafka topic.
    /// </summary>
    // Preserve the CompilerGenerated accessor metadata emitted by the shipped auto-properties.
    public required string Topic
    {
        [CompilerGenerated]
        get => _topic;

        [CompilerGenerated]
        init => _topic = value;
    }

    /// <summary>
    /// Gets whether the payload is for the key or value component.
    /// </summary>
    public required SerializationComponent Component
    {
        [CompilerGenerated]
        get => _component;

        [CompilerGenerated]
        init => _component = value;
    }

    /// <summary>
    /// Gets the Schema Registry schema ID from the wire envelope.
    /// </summary>
    public required int SchemaId
    {
        [CompilerGenerated]
        get => _schemaId;

        [CompilerGenerated]
        init => _schemaId = value;
    }

    /// <summary>
    /// Gets the Schema Registry subject when known.
    /// </summary>
    public string? Subject
    {
        [CompilerGenerated]
        get => _subject;

        [CompilerGenerated]
        init => _subject = value;
    }

    /// <summary>
    /// Gets the Schema Registry schema when available. This can be <see langword="null" />
    /// when a deserializer skips schema validation or when the subject is unknown.
    /// </summary>
    public Schema? Schema
    {
        [CompilerGenerated]
        get => _schema;

        [CompilerGenerated]
        init => _schema = value;
    }

    /// <summary>
    /// Gets the source schema for a migration rule, or <see langword="null" /> outside migration execution.
    /// </summary>
    public Schema? SourceSchema => _sourceSchema;

    /// <summary>
    /// Gets the target schema for a migration rule, or <see langword="null" /> outside migration execution.
    /// </summary>
    public Schema? TargetSchema => _targetSchema;

    /// <summary>
    /// Gets the active migration mode, or <see langword="null" /> outside migration execution.
    /// </summary>
    public SchemaRuleMode? RuleMode => _ruleMode;

    /// <summary>
    /// Gets the codec payload format.
    /// </summary>
    public required SchemaRegistryPayloadFormat PayloadFormat
    {
        [CompilerGenerated]
        get => _payloadFormat;

        [CompilerGenerated]
        init => _payloadFormat = value;
    }

    internal ISchemaRegistryTaggedFieldTransformer? TaggedFieldTransformer
    {
        get => _taggedFieldTransformer;
        init => _taggedFieldTransformer = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SchemaRegistryRuleContext Rent(
        string topic,
        SerializationComponent component,
        int schemaId,
        string? subject,
        Schema? schema,
        SchemaRegistryPayloadFormat payloadFormat,
        Schema? sourceSchema = null,
        Schema? targetSchema = null,
        SchemaRuleMode? ruleMode = null)
    {
        var context = t_primary;
        if (context is null)
        {
            context = new SchemaRegistryRuleContext
            {
                Topic = topic,
                Component = component,
                SchemaId = schemaId,
                Subject = subject,
                Schema = schema,
                PayloadFormat = payloadFormat
            };
            context.SetMigration(sourceSchema, targetSchema, ruleMode);
            context._inUse = true;
            t_primary = context;
        }
        else if (!context._inUse)
        {
            context._inUse = true;
            context.Reset(topic, component, schemaId, subject, schema, payloadFormat, sourceSchema, targetSchema, ruleMode);
        }
        else
        {
            context = t_overflow;
            if (context is null)
            {
                context = new SchemaRegistryRuleContext
                {
                    Topic = topic,
                    Component = component,
                    SchemaId = schemaId,
                    Subject = subject,
                    Schema = schema,
                    PayloadFormat = payloadFormat
                };
                context.SetMigration(sourceSchema, targetSchema, ruleMode);
            }
            else
            {
                t_overflow = context._next;
                context._next = null;
                context.Reset(topic, component, schemaId, subject, schema, payloadFormat, sourceSchema, targetSchema, ruleMode);
            }
        }

        return context;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SchemaRegistryRuleContext RentWithTaggedFieldTransformer(
        string topic,
        SerializationComponent component,
        int schemaId,
        string? subject,
        Schema? schema,
        SchemaRegistryPayloadFormat payloadFormat,
        ISchemaRegistryTaggedFieldTransformer taggedFieldTransformer,
        Schema? sourceSchema = null,
        Schema? targetSchema = null,
        SchemaRuleMode? ruleMode = null)
    {
        var context = Rent(
            topic,
            component,
            schemaId,
            subject,
            schema,
            payloadFormat,
            sourceSchema,
            targetSchema,
            ruleMode);
        context._taggedFieldTransformer = taggedFieldTransformer;
        return context;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Return()
    {
        _topic = null!;
        _subject = null;
        _schema = null;
        _sourceSchema = null;
        _targetSchema = null;
        _ruleMode = null;
        _taggedFieldTransformer = null;

        if (ReferenceEquals(this, t_primary))
        {
            _inUse = false;
            return;
        }

        ReturnOverflow(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReturnOverflow(SchemaRegistryRuleContext context)
    {
        context._next = t_overflow;
        t_overflow = context;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Reset(
        string topic,
        SerializationComponent component,
        int schemaId,
        string? subject,
        Schema? schema,
        SchemaRegistryPayloadFormat payloadFormat,
        Schema? sourceSchema,
        Schema? targetSchema,
        SchemaRuleMode? ruleMode)
    {
        _topic = topic;
        _component = component;
        _schemaId = schemaId;
        _subject = subject;
        _schema = schema;
        _payloadFormat = payloadFormat;
        _taggedFieldTransformer = null;
        SetMigration(sourceSchema, targetSchema, ruleMode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetMigration(Schema? sourceSchema, Schema? targetSchema, SchemaRuleMode? ruleMode)
    {
        _sourceSchema = sourceSchema;
        _targetSchema = targetSchema;
        _ruleMode = ruleMode;
    }
}

/// <summary>
/// Executes Schema Registry payload rules such as encryption or other data-contract transforms.
/// </summary>
/// <remarks>
/// Implementations receive only the codec payload bytes. The Schema Registry magic byte,
/// schema ID, and Protobuf message-index prefix remain owned by the serializer.
/// </remarks>
public interface ISchemaRegistryRuleExecutor
{
    /// <summary>
    /// Transforms the codec payload immediately before it is written to the Schema Registry wire envelope.
    /// </summary>
    /// <remarks>
    /// The <paramref name="payload" /> memory is valid only for the synchronous duration of this call.
    /// Implementations that retain the bytes or use them after returning must copy the payload.
    /// Returned memory may use reusable storage and must be consumed before the next transform call
    /// on the same thread; callers that retain it must copy it.
    /// </remarks>
    ReadOnlyMemory<byte> TransformSerializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context);

    /// <summary>
    /// Transforms the codec payload immediately after it is read from the Schema Registry wire envelope.
    /// </summary>
    /// <remarks>
    /// The <paramref name="payload" /> memory is valid only for the synchronous duration of this call.
    /// Implementations that retain the bytes or use them after returning must copy the payload.
    /// Returned memory may use reusable storage and must be consumed before the next transform call
    /// on the same thread; callers that retain it must copy it.
    /// The <see cref="SchemaRegistryRuleContext.Schema" /> property can be <see langword="null" />
    /// when deserializer schema validation is skipped.
    /// </remarks>
    ReadOnlyMemory<byte> TransformDeserializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context);
}

/// <summary>
/// Direction for a Schema Registry payload rule transform.
/// </summary>
public enum SchemaRegistryRuleDirection
{
    /// <summary>
    /// Rule is running before bytes are written to Kafka.
    /// </summary>
    Write,

    /// <summary>
    /// Rule is running after bytes are read from Kafka.
    /// </summary>
    Read
}

/// <summary>
/// Context passed to a typed Schema Registry rule handler.
/// </summary>
/// <remarks>
/// Contexts supplied by <see cref="SchemaRegistryRuleExecutor" /> are borrowed and valid only for
/// the synchronous duration of a handler or action invocation. Implementations must not retain them.
/// </remarks>
public sealed class SchemaRegistryRuleHandlerContext
{
    private SchemaRegistryRuleContext? _payloadContext;
    private SchemaRule? _rule;
    private SchemaRegistryRuleDirection _direction;

    /// <summary>
    /// Gets the payload context shared by all rule executors.
    /// </summary>
    // Preserve the CompilerGenerated accessor metadata emitted by the shipped auto-properties.
    public required SchemaRegistryRuleContext PayloadContext
    {
        [CompilerGenerated]
        get => _payloadContext!;

        [CompilerGenerated]
        init => _payloadContext = value;
    }

    /// <summary>
    /// Gets the Schema Registry rule being executed.
    /// </summary>
    public required SchemaRule Rule
    {
        [CompilerGenerated]
        get => _rule!;

        [CompilerGenerated]
        init => _rule = value;
    }

    /// <summary>
    /// Gets the transform direction.
    /// </summary>
    public required SchemaRegistryRuleDirection Direction
    {
        [CompilerGenerated]
        get => _direction;

        [CompilerGenerated]
        init => _direction = value;
    }

    internal SchemaRegistryRuleHandlerContext? PoolNext { get; set; }

    internal void Initialize(
        SchemaRegistryRuleContext payloadContext,
        SchemaRule rule,
        SchemaRegistryRuleDirection direction)
    {
        _payloadContext = payloadContext;
        _rule = rule;
        _direction = direction;
    }

    internal void Clear()
    {
        _payloadContext = null;
        _rule = null;
        _direction = default;
    }
}

/// <summary>
/// Handles one Schema Registry rule type, such as ENCRYPT or CEL.
/// </summary>
public interface ISchemaRegistryRuleHandler
{
    /// <summary>
    /// Gets the Schema Registry rule type this handler supports.
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Applies a write-side transform for the rule.
    /// </summary>
    /// <remarks>
    /// Returned memory may use reusable storage and must be consumed before the next transform call
    /// on the same thread; callers that retain it must copy it.
    /// </remarks>
    ReadOnlyMemory<byte> TransformSerializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context);

    /// <summary>
    /// Applies a read-side transform for the rule.
    /// </summary>
    /// <remarks>
    /// Returned memory may use reusable storage and must be consumed before the next transform call
    /// on the same thread; callers that retain it must copy it.
    /// </remarks>
    ReadOnlyMemory<byte> TransformDeserializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context);
}

/// <summary>
/// Reports whether a read-side rule transform changed the payload representation.
/// </summary>
/// <remarks>
/// Implement this optional interface when the handler can determine the result while transforming.
/// It avoids a second full-payload comparison on deserialization hot paths. Implementations set
/// <c>payloadChanged</c> to <see langword="false" /> only when they can certify that the transform
/// preserved the input representation. A handler may report <see langword="true" /> when copied
/// output happens to contain equal bytes but proving equality would require another payload scan.
/// </remarks>
public interface ISchemaRegistryRuleTransformResultHandler : ISchemaRegistryRuleHandler
{
    /// <summary>
    /// Applies a read-side transform and reports whether the payload representation changed.
    /// </summary>
    ReadOnlyMemory<byte> TransformDeserializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context,
        out bool payloadChanged);
}

/// <summary>
/// Runs a configured Schema Registry rule success or failure action.
/// </summary>
public interface ISchemaRegistryRuleAction
{
    /// <summary>
    /// Gets the Schema Registry action type this implementation supports.
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Runs the action for a completed rule.
    /// </summary>
    /// <param name="payload">The current codec payload.</param>
    /// <param name="context">The completed rule context.</param>
    /// <param name="exception">The rule failure, or <see langword="null" /> after success.</param>
    void Run(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context,
        SchemaRegistryRuleException? exception);
}

/// <summary>
/// Exception thrown when Schema Registry rule execution fails.
/// </summary>
public sealed class SchemaRegistryRuleException : Exception
{
    /// <summary>
    /// Creates a rule exception.
    /// </summary>
    public SchemaRegistryRuleException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a rule exception with an inner exception.
    /// </summary>
    public SchemaRegistryRuleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Executes Schema Registry domain and encoding rules by dispatching each active rule to a typed handler.
/// </summary>
/// <remarks>
/// This executor operates on codec payload bytes. It intentionally fails closed when a schema
/// contains an active rule with no registered handler, so protected data is not
/// accidentally produced or consumed without the configured transform.
/// </remarks>
public sealed class SchemaRegistryRuleExecutor : ISchemaRegistryRuleExecutor
{
    [ThreadStatic]
    private static SchemaRegistryRuleHandlerContext? t_handlerContextPool;

    private readonly FrozenDictionary<string, ISchemaRegistryRuleHandler> _handlers;
    private readonly Dictionary<RuleActionName, ISchemaRegistryRuleAction> _actions;
    private readonly ConditionalWeakTable<SchemaRuleSet, RuleExecutionPlan> _executionPlans = new();
    private readonly ConditionalWeakTable<SchemaRuleSet, RuleExecutionPlan>.CreateValueCallback _createExecutionPlan;

    /// <summary>
    /// Creates a Schema Registry rule executor.
    /// </summary>
    /// <param name="handlers">Rule handlers keyed by <see cref="ISchemaRegistryRuleHandler.Type" />.</param>
    public SchemaRegistryRuleExecutor(IEnumerable<ISchemaRegistryRuleHandler> handlers)
        : this(handlers, [])
    {
    }

    /// <summary>
    /// Creates a Schema Registry rule executor with custom success and failure actions.
    /// </summary>
    /// <param name="handlers">Rule handlers keyed by <see cref="ISchemaRegistryRuleHandler.Type" />.</param>
    /// <param name="actions">Rule actions keyed by <see cref="ISchemaRegistryRuleAction.Type" />.</param>
    public SchemaRegistryRuleExecutor(
        IEnumerable<ISchemaRegistryRuleHandler> handlers,
        IEnumerable<ISchemaRegistryRuleAction> actions)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(actions);

        var dictionary = new Dictionary<string, ISchemaRegistryRuleHandler>(StringComparer.OrdinalIgnoreCase);
        foreach (var handler in handlers)
        {
            ArgumentNullException.ThrowIfNull(handler);
            if (string.IsNullOrWhiteSpace(handler.Type))
                throw new ArgumentException("Rule handler type cannot be null or whitespace.", nameof(handlers));

            if (!dictionary.TryAdd(handler.Type, handler))
                throw new ArgumentException($"A rule handler for type '{handler.Type}' is already registered.", nameof(handlers));
        }

        _handlers = dictionary.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        var actionDictionary = new Dictionary<RuleActionName, ISchemaRegistryRuleAction>();
        foreach (var action in actions)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (string.IsNullOrWhiteSpace(action.Type))
                throw new ArgumentException("Rule action type cannot be null or whitespace.", nameof(actions));

            if (!actionDictionary.TryAdd(new RuleActionName(action.Type), action))
                throw new ArgumentException($"A rule action for type '{action.Type}' is already registered.", nameof(actions));
        }

        _actions = actionDictionary;
        _createExecutionPlan = CreateExecutionPlan;
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> TransformSerializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context)
        => ApplyRules(payload, context, SchemaRegistryRuleDirection.Write);

    internal ReadOnlyMemory<byte> TransformSerializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        IJsonSchemaValidator validator,
        int schemaId)
        => TransformSerializedPayload(
            payload,
            context,
            validator,
            schemaId,
            validationRules: null,
            ValidationRulesExecution.Disabled,
            validationRulesFailFast: false);

    internal ReadOnlyMemory<byte> TransformSerializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        IJsonSchemaValidator? validator,
        int schemaId,
        IJsonSchemaValidator? validationRules,
        ValidationRulesExecution validationRulesExecution,
        bool validationRulesFailFast)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (validationRulesExecution == ValidationRulesExecution.BeforeDomainRules)
            validationRules!.ValidateRules(payload, schemaId, validationRulesFailFast);

        var ruleSet = context.Schema?.RuleSet;
        if (ruleSet is null || !ruleSet.HasDomainOrEncodingRules)
        {
            validator?.Validate(payload.Span, schemaId);
            if (validationRulesExecution == ValidationRulesExecution.AfterDomainRules)
                validationRules!.ValidateRules(payload, schemaId, validationRulesFailFast);
            return payload;
        }

        switch (ruleSet.EnableAt)
        {
            case null or "" or "ALL" or "CLIENT":
                break;
            case "GATEWAY" or "SERVER" or "NONE":
                validator?.Validate(payload.Span, schemaId);
                if (validationRulesExecution == ValidationRulesExecution.AfterDomainRules)
                    validationRules!.ValidateRules(payload, schemaId, validationRulesFailFast);
                return payload;
            case { } enabledEnvironment:
                throw new SchemaRegistryRuleException(
                    $"Unknown Schema Registry rule execution environment '{enabledEnvironment}'.");
        }

        if (ruleSet.HasFixedRuleCollections)
        {
            var plan = _executionPlans.GetValue(ruleSet, _createExecutionPlan);
            payload = ApplyRules(
                payload,
                context,
                plan.WriteSteps,
                start: 0,
                count: plan.WriteDomainStepCount,
                SchemaRegistryRuleDirection.Write);
            validator?.Validate(payload.Span, schemaId);
            if (validationRulesExecution == ValidationRulesExecution.AfterDomainRules)
                validationRules!.ValidateRules(payload, schemaId, validationRulesFailFast);
            return ApplyRules(
                payload,
                context,
                plan.WriteSteps,
                start: plan.WriteDomainStepCount,
                count: plan.WriteSteps.Length - plan.WriteDomainStepCount,
                SchemaRegistryRuleDirection.Write);
        }

        payload = ApplyRules(payload, context, ruleSet.DomainRules, SchemaRegistryRuleDirection.Write);
        validator?.Validate(payload.Span, schemaId);
        if (validationRulesExecution == ValidationRulesExecution.AfterDomainRules)
            validationRules!.ValidateRules(payload, schemaId, validationRulesFailFast);
        return ApplyRules(payload, context, ruleSet.EncodingRules, SchemaRegistryRuleDirection.Write);
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> TransformDeserializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context)
        => ApplyRules(payload, context, SchemaRegistryRuleDirection.Read);

    internal ReadOnlyMemory<byte> TransformDeserializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        IJsonSchemaValidator validationRules,
        int schemaId,
        ValidationRulesExecution validationRulesExecution,
        bool validationRulesFailFast)
    {
        payload = TransformDeserializedEncodingPayload(payload, context);
        if (validationRulesExecution == ValidationRulesExecution.BeforeDomainRules)
            validationRules.ValidateRules(payload, schemaId, validationRulesFailFast);
        payload = TransformDeserializedDomainPayload(payload, context);
        if (validationRulesExecution == ValidationRulesExecution.AfterDomainRules)
            validationRules.ValidateRules(payload, schemaId, validationRulesFailFast);
        return payload;
    }

    internal ReadOnlyMemory<byte> TransformDeserializedEncodingPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context)
        => ApplyReadRuleCollection(payload, context, useEncodingRules: true);

    internal ReadOnlyMemory<byte> TransformDeserializedDomainPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context)
        => ApplyReadRuleCollection(payload, context, useEncodingRules: false);

    internal ReadOnlyMemory<byte> TransformDeserializedDomainPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        out bool payloadWasTransformed)
        => ApplyReadDomainRuleCollectionWithTransformResult(
            payload,
            context,
            out payloadWasTransformed);

    internal SchemaRegistryMigrationTransformResult TransformMigrationPayload(
        ref ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        SchemaRuleMode mode)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ruleSet = context.Schema?.RuleSet;
        var rules = ruleSet?.MigrationRules;
        if (rules is null || rules.Count == 0 || !ShouldExecute(ruleSet!))
            return SchemaRegistryMigrationTransformResult.None;

        var isUpgrade = mode == SchemaRuleMode.Upgrade;
        var index = isUpgrade ? 0 : rules.Count - 1;
        var end = isUpgrade ? rules.Count : -1;
        var step = isUpgrade ? 1 : -1;
        var direction = isUpgrade ? SchemaRegistryRuleDirection.Write : SchemaRegistryRuleDirection.Read;
        var payloadWasTransformed = false;
        var payloadTransformFailed = false;
        for (; index != end; index += step)
        {
            var rule = rules[index];
            if (!IsActiveMigrationRule(rule, mode))
                continue;

            _handlers.TryGetValue(rule.Type, out var handler);
            var ruleSucceeded = ApplyRule(ref payload, context, rule, handler, direction);
            if (rule.Kind == SchemaRuleKind.Transform)
            {
                payloadWasTransformed |= ruleSucceeded;
                payloadTransformFailed |= !ruleSucceeded;
            }
        }

        if (payloadWasTransformed && payloadTransformFailed)
        {
            throw new SchemaRegistryRuleException(
                "A migration step was only partially transformed and cannot be decoded safely.");
        }

        if (payloadTransformFailed)
            return SchemaRegistryMigrationTransformResult.Failed;

        return payloadWasTransformed
            ? SchemaRegistryMigrationTransformResult.Transformed
            : SchemaRegistryMigrationTransformResult.None;
    }

    internal static bool HasActiveMigrationRule(SchemaRuleSet? ruleSet, SchemaRuleMode mode)
    {
        if (ruleSet is null || !ShouldExecute(ruleSet))
            return false;

        var rules = ruleSet.MigrationRules;
        if (rules is null)
            return false;

        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (IsActiveMigrationRule(rule, mode))
                return true;
        }

        return false;
    }

    private ReadOnlyMemory<byte> ApplyReadRuleCollection(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        bool useEncodingRules)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ruleSet = context.Schema?.RuleSet;
        if (ruleSet is null || !ruleSet.HasDomainOrEncodingRules || !ShouldExecute(ruleSet))
            return payload;

        if (ruleSet.HasFixedRuleCollections)
        {
            var plan = _executionPlans.GetValue(ruleSet, _createExecutionPlan);
            var start = useEncodingRules ? 0 : plan.ReadEncodingStepCount;
            var count = useEncodingRules
                ? plan.ReadEncodingStepCount
                : plan.ReadSteps.Length - plan.ReadEncodingStepCount;
            return ApplyRules(
                payload,
                context,
                plan.ReadSteps,
                start,
                count,
                SchemaRegistryRuleDirection.Read);
        }

        return ApplyRules(
            payload,
            context,
            useEncodingRules ? ruleSet.EncodingRules : ruleSet.DomainRules,
            SchemaRegistryRuleDirection.Read);
    }

    private ReadOnlyMemory<byte> ApplyReadDomainRuleCollectionWithTransformResult(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        out bool payloadWasTransformed)
    {
        ArgumentNullException.ThrowIfNull(context);

        payloadWasTransformed = false;
        var ruleSet = context.Schema?.RuleSet;
        if (ruleSet is null || !ruleSet.HasDomainOrEncodingRules || !ShouldExecute(ruleSet))
            return payload;

        if (ruleSet.HasFixedRuleCollections)
        {
            var plan = _executionPlans.GetValue(ruleSet, _createExecutionPlan);
            return ApplyRulesWithTransformResult(
                payload,
                context,
                plan.ReadSteps,
                plan.ReadEncodingStepCount,
                plan.ReadSteps.Length - plan.ReadEncodingStepCount,
                SchemaRegistryRuleDirection.Read,
                out payloadWasTransformed);
        }

        return ApplyRulesWithTransformResult(
            payload,
            context,
            ruleSet.DomainRules,
            SchemaRegistryRuleDirection.Read,
            out payloadWasTransformed);
    }

    private ReadOnlyMemory<byte> ApplyRules(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        SchemaRegistryRuleDirection direction)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ruleSet = context.Schema?.RuleSet;
        if (ruleSet is null || !ruleSet.HasDomainOrEncodingRules)
            return payload;

        if (!ShouldExecute(ruleSet))
            return payload;

        if (ruleSet.HasFixedRuleCollections)
        {
            var plan = _executionPlans.GetValue(ruleSet, _createExecutionPlan);
            return ApplyRules(
                payload,
                context,
                direction == SchemaRegistryRuleDirection.Write ? plan.WriteSteps : plan.ReadSteps,
                direction);
        }

        var domainRules = ruleSet.DomainRules;
        var encodingRules = ruleSet.EncodingRules;
        if (direction == SchemaRegistryRuleDirection.Write)
        {
            payload = ApplyRules(payload, context, domainRules, direction);
            return ApplyRules(payload, context, encodingRules, direction);
        }

        payload = ApplyRules(payload, context, encodingRules, direction);
        return ApplyRules(payload, context, domainRules, direction);
    }

    private ReadOnlyMemory<byte> ApplyRules(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        IReadOnlyList<SchemaRule>? rules,
        SchemaRegistryRuleDirection direction)
    {
        if (rules is null || rules.Count == 0)
            return payload;

        var isWrite = direction == SchemaRegistryRuleDirection.Write;
        var index = isWrite ? 0 : rules.Count - 1;
        var end = isWrite ? rules.Count : -1;
        var step = isWrite ? 1 : -1;

        for (; index != end; index += step)
        {
            var rule = rules[index];
            if (!IsActiveRule(rule, direction))
                continue;

            var handlerContext = RentHandlerContext(context, rule, direction);
            try
            {
                SchemaRegistryRuleException? failure = null;
                var transformedPayload = payload;
                if (!_handlers.TryGetValue(rule.Type, out var handler))
                {
                    failure = new SchemaRegistryRuleException(
                        $"No Schema Registry rule handler is registered for rule type '{rule.Type}' (rule '{rule.Name}').");
                }
                else
                {
                    try
                    {
                        transformedPayload = direction == SchemaRegistryRuleDirection.Write
                            ? handler.TransformSerializedPayload(payload, handlerContext)
                            : handler.TransformDeserializedPayload(payload, handlerContext);
                    }
                    catch (SchemaRegistryRuleException ex)
                    {
                        failure = ex;
                    }
                    catch (Exception ex) when (ex is not SchemaRegistryRuleException)
                    {
                        failure = new SchemaRegistryRuleException(
                            $"Schema Registry rule '{rule.Name}' of type '{rule.Type}' failed during {direction.ToString().ToLowerInvariant()} transform.",
                            ex);
                    }
                }

                if (failure is null)
                {
                    payload = transformedPayload;
                    RunAction(rule.OnSuccess, defaultAction: null, payload, handlerContext, exception: null);
                }
                else
                {
                    RunAction(rule.OnFailure, defaultAction: "ERROR", payload, handlerContext, failure);
                }
            }
            finally
            {
                ReturnHandlerContext(handlerContext);
            }
        }

        return payload;
    }

    private ReadOnlyMemory<byte> ApplyRulesWithTransformResult(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        IReadOnlyList<SchemaRule>? rules,
        SchemaRegistryRuleDirection direction,
        out bool payloadWasTransformed)
    {
        payloadWasTransformed = false;
        if (rules is null || rules.Count == 0)
            return payload;

        var input = payload;
        var requiresContentComparison = false;
        var isWrite = direction == SchemaRegistryRuleDirection.Write;
        var index = isWrite ? 0 : rules.Count - 1;
        var end = isWrite ? rules.Count : -1;
        var step = isWrite ? 1 : -1;
        for (; index != end; index += step)
        {
            var rule = rules[index];
            if (!IsActiveRule(rule, direction))
                continue;

            _handlers.TryGetValue(rule.Type, out var handler);
            if (ApplyRule(
                    ref payload,
                    context,
                    rule,
                    handler,
                    direction,
                    trackPayloadChange: true,
                    out var payloadChanged) &&
                rule.Kind == SchemaRuleKind.Transform)
            {
                if (payloadChanged is { } changed)
                    payloadWasTransformed |= changed;
                else
                    requiresContentComparison = true;
            }
        }

        if (requiresContentComparison)
            payloadWasTransformed = PayloadContentChanged(input, payload);

        return payload;
    }

    private ReadOnlyMemory<byte> ApplyRules(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        RuleExecutionStep[] steps,
        SchemaRegistryRuleDirection direction)
        => ApplyRules(payload, context, steps, start: 0, steps.Length, direction);

    private ReadOnlyMemory<byte> ApplyRules(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        RuleExecutionStep[] steps,
        int start,
        int count,
        SchemaRegistryRuleDirection direction)
    {
        var end = start + count;
        for (var i = start; i < end; i++)
        {
            ref readonly var step = ref steps[i];
            payload = ApplyRule(payload, context, step.Rule, step.Handler, direction);
        }

        return payload;
    }

    private ReadOnlyMemory<byte> ApplyRulesWithTransformResult(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        RuleExecutionStep[] steps,
        int start,
        int count,
        SchemaRegistryRuleDirection direction,
        out bool payloadWasTransformed)
    {
        payloadWasTransformed = false;
        var input = payload;
        var requiresContentComparison = false;
        var end = start + count;
        for (var i = start; i < end; i++)
        {
            ref readonly var step = ref steps[i];
            if (ApplyRule(
                    ref payload,
                    context,
                    step.Rule,
                    step.Handler,
                    direction,
                    trackPayloadChange: true,
                    out var payloadChanged) &&
                step.Rule.Kind == SchemaRuleKind.Transform)
            {
                if (payloadChanged is { } changed)
                    payloadWasTransformed |= changed;
                else
                    requiresContentComparison = true;
            }
        }

        if (requiresContentComparison)
            payloadWasTransformed = PayloadContentChanged(input, payload);

        return payload;
    }

    private ReadOnlyMemory<byte> ApplyRule(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        SchemaRule rule,
        ISchemaRegistryRuleHandler? handler,
        SchemaRegistryRuleDirection direction)
    {
        _ = ApplyRule(ref payload, context, rule, handler, direction);
        return payload;
    }

    private bool ApplyRule(
        ref ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        SchemaRule rule,
        ISchemaRegistryRuleHandler? handler,
        SchemaRegistryRuleDirection direction) =>
        ApplyRule(
            ref payload,
            context,
            rule,
            handler,
            direction,
            trackPayloadChange: false,
            out _);

    private bool ApplyRule(
        ref ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        SchemaRule rule,
        ISchemaRegistryRuleHandler? handler,
        SchemaRegistryRuleDirection direction,
        bool trackPayloadChange,
        out bool? payloadChanged)
    {
        payloadChanged = null;
        var handlerContext = RentHandlerContext(context, rule, direction);
        try
        {
            SchemaRegistryRuleException? failure = null;
            var transformedPayload = payload;
            if (handler is null)
            {
                failure = new SchemaRegistryRuleException(
                    $"No Schema Registry rule handler is registered for rule type '{rule.Type}' (rule '{rule.Name}').");
            }
            else
            {
                try
                {
                    if (trackPayloadChange &&
                        direction == SchemaRegistryRuleDirection.Read &&
                        rule.Kind == SchemaRuleKind.Transform)
                    {
                        if (handler is ISchemaRegistryRuleTransformResultHandler resultHandler)
                        {
                            transformedPayload = resultHandler.TransformDeserializedPayload(
                                payload,
                                handlerContext,
                                out var changed);
                            payloadChanged = changed;
                        }
                        else
                        {
                            transformedPayload = handler.TransformDeserializedPayload(payload, handlerContext);
                            payloadChanged = null;
                        }
                    }
                    else
                    {
                        transformedPayload = direction == SchemaRegistryRuleDirection.Write
                            ? handler.TransformSerializedPayload(payload, handlerContext)
                            : handler.TransformDeserializedPayload(payload, handlerContext);
                    }
                }
                catch (SchemaRegistryRuleException ex)
                {
                    failure = ex;
                }
                catch (Exception ex) when (ex is not SchemaRegistryRuleException)
                {
                    failure = new SchemaRegistryRuleException(
                        $"Schema Registry rule '{rule.Name}' of type '{rule.Type}' failed during {direction.ToString().ToLowerInvariant()} transform.",
                        ex);
                }
            }

            if (failure is null)
            {
                payload = transformedPayload;
                RunAction(rule.OnSuccess, defaultAction: null, payload, handlerContext, exception: null);
            }
            else
            {
                RunAction(rule.OnFailure, defaultAction: "ERROR", payload, handlerContext, failure);
            }

            return failure is null;
        }
        finally
        {
            ReturnHandlerContext(handlerContext);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PayloadContentChanged(
        ReadOnlyMemory<byte> input,
        ReadOnlyMemory<byte> output) =>
        !input.Equals(output) && !input.Span.SequenceEqual(output.Span);

    private RuleExecutionPlan CreateExecutionPlan(SchemaRuleSet ruleSet)
    {
        var writeSteps = new List<RuleExecutionStep>();
        AddExecutionSteps(writeSteps, ruleSet.DomainRules, SchemaRegistryRuleDirection.Write, reverse: false);
        var writeDomainStepCount = writeSteps.Count;
        AddExecutionSteps(writeSteps, ruleSet.EncodingRules, SchemaRegistryRuleDirection.Write, reverse: false);

        var readSteps = new List<RuleExecutionStep>();
        AddExecutionSteps(readSteps, ruleSet.EncodingRules, SchemaRegistryRuleDirection.Read, reverse: true);
        var readEncodingStepCount = readSteps.Count;
        AddExecutionSteps(readSteps, ruleSet.DomainRules, SchemaRegistryRuleDirection.Read, reverse: true);
        return new RuleExecutionPlan(
            [.. writeSteps],
            writeDomainStepCount,
            [.. readSteps],
            readEncodingStepCount);
    }

    private void AddExecutionSteps(
        List<RuleExecutionStep> destination,
        IReadOnlyList<SchemaRule>? rules,
        SchemaRegistryRuleDirection direction,
        bool reverse)
    {
        if (rules is null)
            return;

        var index = reverse ? rules.Count - 1 : 0;
        var end = reverse ? -1 : rules.Count;
        var step = reverse ? -1 : 1;
        for (; index != end; index += step)
        {
            var rule = rules[index];
            if (!IsActiveRule(rule, direction))
                continue;

            _handlers.TryGetValue(rule.Type, out var handler);
            destination.Add(new RuleExecutionStep(rule, handler));
        }
    }

    private static SchemaRegistryRuleHandlerContext RentHandlerContext(
        SchemaRegistryRuleContext payloadContext,
        SchemaRule rule,
        SchemaRegistryRuleDirection direction)
    {
        var context = t_handlerContextPool;
        if (context is null)
        {
            return new SchemaRegistryRuleHandlerContext
            {
                PayloadContext = payloadContext,
                Rule = rule,
                Direction = direction
            };
        }

        t_handlerContextPool = context.PoolNext;
        context.PoolNext = null;
        context.Initialize(payloadContext, rule, direction);
        return context;
    }

    private static void ReturnHandlerContext(SchemaRegistryRuleHandlerContext context)
    {
        context.Clear();
        context.PoolNext = t_handlerContextPool;
        t_handlerContextPool = context;
    }

    private void RunAction(
        string? configuredAction,
        string? defaultAction,
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context,
        SchemaRegistryRuleException? exception)
    {
        var actionName = GetActionName(configuredAction, context.Rule.Mode, context.Direction);
        if (actionName is null && defaultAction is not null)
            actionName = new RuleActionName(defaultAction);

        if (actionName is not { } resolvedActionName)
            return;

        if (resolvedActionName.Equals("NONE"))
            return;

        if (resolvedActionName.Equals("ERROR"))
        {
            throw exception ?? new SchemaRegistryRuleException(
                $"Schema Registry rule '{context.Rule.Name}' configured the ERROR action after success.");
        }

        if (!_actions.TryGetValue(resolvedActionName, out var action))
        {
            throw new SchemaRegistryRuleException(
                $"No Schema Registry rule action is registered for action type '{resolvedActionName}' (rule '{context.Rule.Name}').");
        }

        try
        {
            action.Run(payload, context, exception);
        }
        catch (Exception ex) when (ex is not SchemaRegistryRuleException)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule action '{resolvedActionName}' failed for rule '{context.Rule.Name}'.",
                ex);
        }
    }

    private static RuleActionName? GetActionName(
        string? configuredAction,
        SchemaRuleMode ruleMode,
        SchemaRegistryRuleDirection direction)
    {
        if (configuredAction is null)
            return null;

        if (ruleMode is not (SchemaRuleMode.WriteRead or SchemaRuleMode.UpDown))
            return new RuleActionName(configuredAction);

        var separator = configuredAction.IndexOf(',');
        if (separator < 0)
            return new RuleActionName(configuredAction);

        if (direction == SchemaRegistryRuleDirection.Write)
            return new RuleActionName(configuredAction, 0, separator);

        var start = separator + 1;
        var nextSeparator = configuredAction.IndexOf(',', start);
        var length = nextSeparator < 0 ? configuredAction.Length - start : nextSeparator - start;
        return new RuleActionName(configuredAction, start, length);
    }

    private static bool IsActiveRule(SchemaRule rule, SchemaRegistryRuleDirection direction)
    {
        if (rule.Disabled || string.IsNullOrWhiteSpace(rule.Type))
            return false;

        if (rule.Kind is not (SchemaRuleKind.Transform or SchemaRuleKind.Condition))
            return false;

        return direction == SchemaRegistryRuleDirection.Write
            ? rule.Mode is SchemaRuleMode.Write or SchemaRuleMode.WriteRead
            : rule.Mode is SchemaRuleMode.Read or SchemaRuleMode.WriteRead;
    }

    private static bool IsActiveMigrationRule(SchemaRule rule, SchemaRuleMode mode)
    {
        if (rule.Disabled || string.IsNullOrWhiteSpace(rule.Type))
            return false;

        if (rule.Kind is not (SchemaRuleKind.Transform or SchemaRuleKind.Condition))
            return false;

        return rule.Mode == mode || rule.Mode == SchemaRuleMode.UpDown;
    }

    private static bool ShouldExecute(SchemaRuleSet ruleSet)
    {
        switch (ruleSet.EnableAt)
        {
            case null or "" or "ALL" or "CLIENT":
                return true;
            case "GATEWAY" or "SERVER" or "NONE":
                return false;
            case { } enabledEnvironment:
                throw new SchemaRegistryRuleException(
                    $"Unknown Schema Registry rule execution environment '{enabledEnvironment}'.");
        }
    }

    private sealed record RuleExecutionPlan(
        RuleExecutionStep[] WriteSteps,
        int WriteDomainStepCount,
        RuleExecutionStep[] ReadSteps,
        int ReadEncodingStepCount);

    private readonly record struct RuleExecutionStep(
        SchemaRule Rule,
        ISchemaRegistryRuleHandler? Handler);

    private readonly struct RuleActionName : IEquatable<RuleActionName>
    {
        private readonly string _value;
        private readonly int _start;
        private readonly int _length;

        public RuleActionName(string value)
            : this(value, 0, value.Length)
        {
        }

        public RuleActionName(string value, int start, int length)
        {
            _value = value;
            _start = start;
            _length = length;
        }

        public bool Equals(RuleActionName other) =>
            _value.AsSpan(_start, _length).Equals(
                other._value.AsSpan(other._start, other._length),
                StringComparison.OrdinalIgnoreCase);

        public bool Equals(string other) =>
            _value.AsSpan(_start, _length).Equals(other, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => obj is RuleActionName other && Equals(other);

        public override int GetHashCode() =>
            string.GetHashCode(_value.AsSpan(_start, _length), StringComparison.OrdinalIgnoreCase);

        public override string ToString() =>
            _start == 0 && _length == _value.Length
                ? _value
                : _value.Substring(_start, _length);
    }
}
