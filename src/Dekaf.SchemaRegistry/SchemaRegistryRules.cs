using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Dekaf.Serialization;

namespace Dekaf.SchemaRegistry;

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
public sealed class SchemaRegistryRuleContext
{
    /// <summary>
    /// Gets the Kafka topic.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Gets whether the payload is for the key or value component.
    /// </summary>
    public required SerializationComponent Component { get; init; }

    /// <summary>
    /// Gets the Schema Registry schema ID from the wire envelope.
    /// </summary>
    public required int SchemaId { get; init; }

    /// <summary>
    /// Gets the Schema Registry subject when known.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// Gets the Schema Registry schema when available. This can be <see langword="null" />
    /// when a deserializer skips schema validation or when the subject is unknown.
    /// </summary>
    public Schema? Schema { get; init; }

    /// <summary>
    /// Gets the codec payload format.
    /// </summary>
    public required SchemaRegistryPayloadFormat PayloadFormat { get; init; }
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
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(validator);

        var ruleSet = context.Schema?.RuleSet;
        if (ruleSet is null || !ruleSet.HasDomainOrEncodingRules)
        {
            validator.Validate(payload.Span, schemaId);
            return payload;
        }

        switch (ruleSet.EnableAt)
        {
            case null or "" or "ALL" or "CLIENT":
                break;
            case "GATEWAY" or "SERVER" or "NONE":
                validator.Validate(payload.Span, schemaId);
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
            validator.Validate(payload.Span, schemaId);
            return ApplyRules(
                payload,
                context,
                plan.WriteSteps,
                start: plan.WriteDomainStepCount,
                count: plan.WriteSteps.Length - plan.WriteDomainStepCount,
                SchemaRegistryRuleDirection.Write);
        }

        payload = ApplyRules(payload, context, ruleSet.DomainRules, SchemaRegistryRuleDirection.Write);
        validator.Validate(payload.Span, schemaId);
        return ApplyRules(payload, context, ruleSet.EncodingRules, SchemaRegistryRuleDirection.Write);
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> TransformDeserializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context)
        => ApplyRules(payload, context, SchemaRegistryRuleDirection.Read);

    private ReadOnlyMemory<byte> ApplyRules(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        SchemaRegistryRuleDirection direction)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ruleSet = context.Schema?.RuleSet;
        if (ruleSet is null || !ruleSet.HasDomainOrEncodingRules)
            return payload;

        switch (ruleSet.EnableAt)
        {
            case null or "" or "ALL" or "CLIENT":
                break;
            case "GATEWAY" or "SERVER" or "NONE":
                return payload;
            case { } enabledEnvironment:
                throw new SchemaRegistryRuleException(
                    $"Unknown Schema Registry rule execution environment '{enabledEnvironment}'.");
        }

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

    private ReadOnlyMemory<byte> ApplyRule(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        SchemaRule rule,
        ISchemaRegistryRuleHandler? handler,
        SchemaRegistryRuleDirection direction)
    {
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

            return payload;
        }
        finally
        {
            ReturnHandlerContext(handlerContext);
        }
    }

    private RuleExecutionPlan CreateExecutionPlan(SchemaRuleSet ruleSet)
    {
        var writeSteps = new List<RuleExecutionStep>();
        AddExecutionSteps(writeSteps, ruleSet.DomainRules, SchemaRegistryRuleDirection.Write, reverse: false);
        var writeDomainStepCount = writeSteps.Count;
        AddExecutionSteps(writeSteps, ruleSet.EncodingRules, SchemaRegistryRuleDirection.Write, reverse: false);

        var readSteps = new List<RuleExecutionStep>();
        AddExecutionSteps(readSteps, ruleSet.EncodingRules, SchemaRegistryRuleDirection.Read, reverse: true);
        AddExecutionSteps(readSteps, ruleSet.DomainRules, SchemaRegistryRuleDirection.Read, reverse: true);
        return new RuleExecutionPlan([.. writeSteps], writeDomainStepCount, [.. readSteps]);
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

        if (ruleMode != SchemaRuleMode.WriteRead)
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

    private sealed record RuleExecutionPlan(
        RuleExecutionStep[] WriteSteps,
        int WriteDomainStepCount,
        RuleExecutionStep[] ReadSteps);

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
