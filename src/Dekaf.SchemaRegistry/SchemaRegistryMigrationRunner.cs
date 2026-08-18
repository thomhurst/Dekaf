using System.Runtime.CompilerServices;
using Dekaf.Serialization;

namespace Dekaf.SchemaRegistry;

internal sealed class SchemaRegistryMigrationRunner
{
    internal static ISchemaRegistryRuleExecutor MarkerRuleExecutor { get; } = new MigrationMarkerRuleExecutor();

    private static readonly Func<SchemaRegistryMigrationRunner, string, Schema, Task<MigrationPlan>> s_createPlan =
        static (runner, subject, writerSchema) => runner.CreatePlanWithTimeoutAsync(subject, writerSchema);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;
    private readonly SchemaRegistryRuleExecutor? _schemaRuleExecutor;
    private readonly SchemaResolutionCache<MigrationPlan> _plans = new();
    private readonly TimeSpan _timeout;
    private MigrationPlan? _lastPlan;
    private MigrationPlan? _preparedPlan;
    private readonly long _latestCacheTtlMilliseconds;

    internal SchemaRegistryMigrationRunner(
        ISchemaRegistryClient schemaRegistry,
        ISchemaRegistryRuleExecutor? ruleExecutor,
        TimeSpan timeout)
    {
        _schemaRegistry = schemaRegistry;
        _ruleExecutor = ruleExecutor;
        _schemaRuleExecutor = ruleExecutor as SchemaRegistryRuleExecutor;
        _timeout = timeout;
        var latestCacheTtlSecs = schemaRegistry.LatestCacheTtlSecs;
        if (latestCacheTtlSecs < -1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaRegistry),
                "LatestCacheTtlSecs must be non-negative or -1 for no expiry.");
        }

        _latestCacheTtlMilliseconds = (long)latestCacheTtlSecs * 1000;
    }

    internal static (
        SchemaRegistryMigrationRunner Runner,
        ISchemaRegistryRuleExecutor EffectiveRuleExecutor) Create(
        ISchemaRegistryClient schemaRegistry,
        ISchemaRegistryRuleExecutor? ruleExecutor,
        TimeSpan timeout) =>
        (new SchemaRegistryMigrationRunner(schemaRegistry, ruleExecutor, timeout),
         ruleExecutor ?? MarkerRuleExecutor);

    internal ValueTask<PreparedTargetEnumerator> PrepareAsync(
        int schemaId,
        string subject,
        Schema writerSchema,
        CancellationToken cancellationToken)
    {
        var plan = Volatile.Read(ref _lastPlan);
        if (plan is not null &&
            plan.WriterSchemaId == schemaId &&
            string.Equals(plan.Subject, subject, StringComparison.Ordinal))
        {
            if (!IsExpired(plan))
                return new ValueTask<PreparedTargetEnumerator>(
                    PreparedTargetEnumerator.Create(plan.Steps));

            _plans.TryRemove(subject, writerSchema, plan);
            return AwaitPreparedPlanAsync(schemaId, subject, writerSchema, cancellationToken);
        }

        if (_plans.TryGet(subject, writerSchema, out plan))
        {
            if (!IsExpired(plan))
            {
                Volatile.Write(ref _lastPlan, plan);
                return new ValueTask<PreparedTargetEnumerator>(
                    PreparedTargetEnumerator.Create(plan.Steps));
            }

            _plans.TryRemove(subject, writerSchema, plan);
        }

        return AwaitPreparedPlanAsync(schemaId, subject, writerSchema, cancellationToken);
    }

    private async ValueTask<PreparedTargetEnumerator> AwaitPreparedPlanAsync(
        int schemaId,
        string subject,
        Schema writerSchema,
        CancellationToken cancellationToken)
    {
        var plan = await _plans.ResolveAsync(
                subject,
                writerSchema,
                this,
                s_createPlan,
                cancellationToken)
            .ConfigureAwait(false);
        if (plan.WriterSchemaId != schemaId)
        {
            throw new InvalidOperationException(
                $"Schema Registry returned writer schema ID {plan.WriterSchemaId} for payload schema ID {schemaId}.");
        }

        Volatile.Write(ref _lastPlan, plan);
        Volatile.Write(ref _preparedPlan, plan);
        return PreparedTargetEnumerator.Create(plan.Steps);
    }

    internal bool TryUsePreparedPlan(int schemaId, string subject)
    {
        var plan = Volatile.Read(ref _lastPlan);
        if (plan is null ||
            plan.WriterSchemaId != schemaId ||
            !string.Equals(plan.Subject, subject, StringComparison.Ordinal))
        {
            return false;
        }

        if (!IsExpired(plan))
        {
            _ = Interlocked.CompareExchange(ref _preparedPlan, null, plan);
            return true;
        }

        return ReferenceEquals(Interlocked.CompareExchange(ref _preparedPlan, null, plan), plan);
    }

    internal MigrationResult Transform(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        string subject,
        Schema writerSchema,
        SerializationContext serializationContext,
        SchemaRegistryPayloadFormat payloadFormat,
        ISchemaRegistryTaggedFieldTransformerProvider? taggedFieldTransformers = null,
        bool skipLatestRefresh = false)
    {
        var isNewPlan = false;
        var plan = Volatile.Read(ref _lastPlan);
        if (plan is null ||
            plan.WriterSchemaId != schemaId ||
            !string.Equals(plan.Subject, subject, StringComparison.Ordinal))
        {
            if (!_plans.TryGet(subject, writerSchema, out plan))
            {
                plan = _plans.Resolve(subject, writerSchema, this, s_createPlan, _timeout);
                isNewPlan = true;
            }

            Volatile.Write(ref _lastPlan, plan);
        }

        if (!isNewPlan && !skipLatestRefresh && IsExpired(plan))
        {
            _plans.TryRemove(subject, writerSchema, plan);
            plan = _plans.Resolve(subject, writerSchema, this, s_createPlan, _timeout);
            Volatile.Write(ref _lastPlan, plan);
        }

        if (_schemaRuleExecutor is null)
        {
            if (plan.Steps.Length != 0)
            {
                throw new SchemaRegistryRuleException(
                    $"Migration rules require {nameof(SchemaRegistryRuleExecutor)}.");
            }

            if (_ruleExecutor is null)
                return new MigrationResult(payload, plan.ReaderSchema, schemaId, writerSchema);

            var readerContext = RentContext(
                serializationContext,
                plan.ReaderSchema.Id,
                subject,
                plan.ReaderSchema.Schema,
                payloadFormat,
                taggedFieldTransformers,
                taggedFieldSchema: writerSchema);
            try
            {
                payload = _ruleExecutor.TransformDeserializedPayload(payload, readerContext);
            }
            finally
            {
                readerContext.Return();
            }

            return new MigrationResult(payload, plan.ReaderSchema, schemaId, writerSchema);
        }

        var steps = plan.Steps;
        if (steps.Length == 0 &&
            writerSchema.RuleSet?.HasDomainOrEncodingRules != true &&
            plan.ReaderSchema.Schema.RuleSet?.HasDomainOrEncodingRules != true)
        {
            return new MigrationResult(payload, plan.ReaderSchema, schemaId, writerSchema);
        }

        var context = RentContext(
            serializationContext,
            schemaId,
            subject,
            writerSchema,
            payloadFormat,
            taggedFieldTransformers);
        try
        {
            payload = _schemaRuleExecutor.TransformDeserializedEncodingPayload(payload, context);
        }
        finally
        {
            context.Return();
        }

        var payloadSchemaId = schemaId;
        var payloadSchema = writerSchema;
        if (steps.Length != 0)
        {
            if (!TransformMigrationSteps(
                ref payload,
                ref payloadSchemaId,
                ref payloadSchema,
                schemaId,
                subject,
                serializationContext,
                payloadFormat,
                taggedFieldTransformers,
                steps))
            {
                return new MigrationResult(payload, plan.ReaderSchema, payloadSchemaId, payloadSchema);
            }
        }

        if (!plan.IsMigrationChainComplete)
            return new MigrationResult(payload, plan.ReaderSchema, payloadSchemaId, payloadSchema);

        context = RentContext(
            serializationContext,
            plan.ReaderSchema.Id,
            subject,
            plan.ReaderSchema.Schema,
            payloadFormat,
            taggedFieldTransformers,
            taggedFieldSchema: payloadSchema);
        try
        {
            payload = _schemaRuleExecutor.TransformDeserializedDomainPayload(payload, context);
        }
        finally
        {
            context.Return();
        }

        return new MigrationResult(payload, plan.ReaderSchema, payloadSchemaId, payloadSchema);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsExpired(MigrationPlan plan) =>
        _latestCacheTtlMilliseconds >= 0 &&
        Environment.TickCount64 - plan.CreatedAtMilliseconds >= _latestCacheTtlMilliseconds;

    private bool TransformMigrationSteps(
        ref ReadOnlyMemory<byte> payload,
        ref int payloadSchemaId,
        ref Schema payloadSchema,
        int schemaId,
        string subject,
        SerializationContext serializationContext,
        SchemaRegistryPayloadFormat payloadFormat,
        ISchemaRegistryTaggedFieldTransformerProvider? taggedFieldTransformers,
        MigrationStep[] steps)
    {
        SchemaRegistryRuleContext context;
        for (var i = 0; i < steps.Length; i++)
        {
            ref readonly var step = ref steps[i];
            var owner = step.Mode == SchemaRuleMode.Upgrade ? step.Target.Schema : step.Source.Schema;
            context = RentContext(
                serializationContext,
                schemaId,
                subject,
                owner,
                payloadFormat,
                taggedFieldTransformers,
                payloadSchema,
                step.Source.Schema,
                step.Target.Schema,
                step.Mode);
            try
            {
                var transformResult = _schemaRuleExecutor!.TransformMigrationPayload(
                    ref payload,
                    context,
                    step.Mode);
                if (transformResult != SchemaRegistryMigrationTransformResult.Transformed)
                    return false;

                payloadSchemaId = step.Target.Id;
                payloadSchema = step.Target.Schema;
            }
            finally
            {
                context.Return();
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SchemaRegistryRuleContext RentContext(
        SerializationContext serializationContext,
        int schemaId,
        string subject,
        Schema schema,
        SchemaRegistryPayloadFormat payloadFormat,
        ISchemaRegistryTaggedFieldTransformerProvider? taggedFieldTransformers,
        Schema? taggedFieldSchema = null,
        Schema? sourceSchema = null,
        Schema? targetSchema = null,
        SchemaRuleMode? ruleMode = null) => taggedFieldTransformers is null
        ? SchemaRegistryRuleContext.Rent(
            serializationContext.Topic,
            serializationContext.Component,
            schemaId,
            subject,
            schema,
            payloadFormat,
            sourceSchema,
            targetSchema,
            ruleMode)
        : SchemaRegistryRuleContext.RentWithTaggedFieldTransformer(
            serializationContext.Topic,
            serializationContext.Component,
            schemaId,
            subject,
            schema,
            payloadFormat,
            taggedFieldTransformers.Get(taggedFieldSchema ?? schema, schema),
            sourceSchema,
            targetSchema,
            ruleMode);

    private Task<MigrationPlan> CreatePlanWithTimeoutAsync(string subject, Schema writerSchema) =>
        SchemaRegistryOperationTimeout.ExecuteAsync(
            cancellationToken => CreatePlanAsync(subject, writerSchema, cancellationToken),
            _timeout,
            "Schema Registry migration plan resolution timed out.");

    private async Task<MigrationPlan> CreatePlanAsync(
        string subject,
        Schema writerSchema,
        CancellationToken cancellationToken)
    {
        var writer = await _schemaRegistry.LookupSchemaAsync(
                subject,
                writerSchema,
                ignoreDeletedSchemas: false,
                normalize: false,
                cancellationToken)
            .ConfigureAwait(false);
        var reader = await _schemaRegistry.GetSchemaBySubjectAsync(subject, "latest", cancellationToken)
            .ConfigureAwait(false);

        if (writer.Version == reader.Version)
            return new MigrationPlan(
                writer.Id,
                subject,
                reader,
                [],
                isMigrationChainComplete: true,
                Environment.TickCount64);

        var steps = new List<MigrationStep>();
        var hasGap = false;
        var isMigrationChainComplete = true;
        if (writer.Version < reader.Version)
        {
            var previous = writer;
            for (var version = writer.Version + 1; version <= reader.Version; version++)
            {
                var current = version == reader.Version
                    ? reader
                    : await GetVersionAsync(subject, version, cancellationToken).ConfigureAwait(false);
                var hasActiveRule = SchemaRegistryRuleExecutor.HasActiveMigrationRule(
                    current.Schema.RuleSet,
                    SchemaRuleMode.Upgrade);
                if (!hasActiveRule)
                {
                    hasGap = true;
                    previous = current;
                    continue;
                }

                if (hasGap)
                {
                    isMigrationChainComplete = false;
                    break;
                }

                steps.Add(new MigrationStep(SchemaRuleMode.Upgrade, previous, current));

                previous = current;
            }
        }
        else
        {
            var current = writer;
            for (var version = writer.Version - 1; version >= reader.Version; version--)
            {
                var previous = version == reader.Version
                    ? reader
                    : await GetVersionAsync(subject, version, cancellationToken).ConfigureAwait(false);
                var hasActiveRule = SchemaRegistryRuleExecutor.HasActiveMigrationRule(
                    current.Schema.RuleSet,
                    SchemaRuleMode.Downgrade);
                if (!hasActiveRule)
                {
                    hasGap = true;
                    current = previous;
                    continue;
                }

                if (hasGap)
                {
                    isMigrationChainComplete = false;
                    break;
                }

                steps.Add(new MigrationStep(SchemaRuleMode.Downgrade, current, previous));

                current = previous;
            }
        }

        return new MigrationPlan(
            writer.Id,
            subject,
            reader,
            [.. steps],
            isMigrationChainComplete,
            Environment.TickCount64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task<RegisteredSchema> GetVersionAsync(
        string subject,
        int version,
        CancellationToken cancellationToken) =>
        _schemaRegistry.GetSchemaBySubjectAsync(
            subject,
            version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ignoreDeletedSchemas: false,
            cancellationToken);

    internal readonly record struct MigrationResult(
        ReadOnlyMemory<byte> Payload,
        RegisteredSchema ReaderSchema,
        int PayloadSchemaId,
        Schema PayloadSchema);

    internal struct PreparedTargetEnumerator
    {
        private MigrationStep[]? _steps;
        private int _index;

        private PreparedTargetEnumerator(MigrationStep[] steps)
        {
            _steps = steps;
            _index = 0;
        }

        internal static PreparedTargetEnumerator Create(MigrationStep[] steps) => new(steps);

        internal bool MoveNext(out RegisteredSchema target)
        {
            if (_steps is not null && _index < _steps.Length)
            {
                target = _steps[_index++].Target;
                return true;
            }

            target = null!;
            return false;
        }
    }

    private sealed class MigrationPlan(
        int writerSchemaId,
        string subject,
        RegisteredSchema readerSchema,
        MigrationStep[] steps,
        bool isMigrationChainComplete,
        long createdAtMilliseconds)
    {
        internal int WriterSchemaId { get; } = writerSchemaId;
        internal string Subject { get; } = subject;
        internal RegisteredSchema ReaderSchema { get; } = readerSchema;
        internal MigrationStep[] Steps { get; } = steps;
        internal bool IsMigrationChainComplete { get; } = isMigrationChainComplete;
        internal long CreatedAtMilliseconds { get; } = createdAtMilliseconds;
    }

    internal readonly record struct MigrationStep(
        SchemaRuleMode Mode,
        RegisteredSchema Source,
        RegisteredSchema Target);

    private sealed class MigrationMarkerRuleExecutor : ISchemaRegistryRuleExecutor
    {
        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;
    }
}

internal enum SchemaRegistryMigrationTransformResult : byte
{
    None,
    Transformed,
    Failed
}
