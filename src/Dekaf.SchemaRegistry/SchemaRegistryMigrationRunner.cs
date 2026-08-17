using System.Runtime.CompilerServices;
using Dekaf.Serialization;

namespace Dekaf.SchemaRegistry;

internal sealed class SchemaRegistryMigrationRunner
{
    internal static ISchemaRegistryRuleExecutor MarkerRuleExecutor { get; } = new MigrationMarkerRuleExecutor();

    private static readonly Func<SchemaRegistryMigrationRunner, string, Schema, Task<MigrationPlan>> s_createPlan =
        static (runner, subject, writerSchema) => runner.CreatePlanAsync(subject, writerSchema);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;
    private readonly SchemaRegistryRuleExecutor? _schemaRuleExecutor;
    private readonly SchemaResolutionCache<MigrationPlan> _plans = new();
    private readonly TimeSpan _timeout;
    private CachedPlan? _lastPlan;

    internal SchemaRegistryMigrationRunner(
        ISchemaRegistryClient schemaRegistry,
        ISchemaRegistryRuleExecutor? ruleExecutor,
        TimeSpan timeout)
    {
        _schemaRegistry = schemaRegistry;
        _ruleExecutor = ruleExecutor;
        _schemaRuleExecutor = ruleExecutor as SchemaRegistryRuleExecutor;
        _timeout = timeout;
    }

    internal MigrationResult Transform(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        string subject,
        Schema writerSchema,
        SerializationContext serializationContext,
        SchemaRegistryPayloadFormat payloadFormat)
    {
        var cached = Volatile.Read(ref _lastPlan);
        MigrationPlan plan;
        if (cached is not null &&
            cached.SchemaId == schemaId &&
            string.Equals(cached.Subject, subject, StringComparison.Ordinal))
        {
            plan = cached.Plan;
        }
        else
        {
            plan = _plans.Resolve(subject, writerSchema, this, s_createPlan, _timeout);
            Volatile.Write(ref _lastPlan, new CachedPlan(schemaId, subject, plan));
        }
        if (_schemaRuleExecutor is null)
        {
            if (plan.Steps.Length != 0)
            {
                throw new SchemaRegistryRuleException(
                    $"Migration rules require {nameof(SchemaRegistryRuleExecutor)}.");
            }

            if (_ruleExecutor is null)
                return new MigrationResult(payload, plan.ReaderSchema);

            var readerContext = SchemaRegistryRuleContext.Rent(
                serializationContext.Topic,
                serializationContext.Component,
                plan.ReaderSchema.Id,
                subject,
                plan.ReaderSchema.Schema,
                payloadFormat);
            try
            {
                payload = _ruleExecutor.TransformDeserializedPayload(payload, readerContext);
            }
            finally
            {
                readerContext.Return();
            }

            return new MigrationResult(payload, plan.ReaderSchema);
        }

        var context = SchemaRegistryRuleContext.Rent(
            serializationContext.Topic,
            serializationContext.Component,
            schemaId,
            subject,
            writerSchema,
            payloadFormat);
        try
        {
            payload = _schemaRuleExecutor.TransformDeserializedEncodingPayload(payload, context);
        }
        finally
        {
            context.Return();
        }

        var steps = plan.Steps;
        for (var i = 0; i < steps.Length; i++)
        {
            ref readonly var step = ref steps[i];
            var owner = step.Mode == SchemaRuleMode.Upgrade ? step.Target.Schema : step.Source.Schema;
            context = SchemaRegistryRuleContext.Rent(
                serializationContext.Topic,
                serializationContext.Component,
                schemaId,
                subject,
                owner,
                payloadFormat,
                step.Source.Schema,
                step.Target.Schema,
                step.Mode);
            try
            {
                payload = _schemaRuleExecutor.TransformMigrationPayload(payload, context, step.Mode);
            }
            finally
            {
                context.Return();
            }
        }

        context = SchemaRegistryRuleContext.Rent(
            serializationContext.Topic,
            serializationContext.Component,
            plan.ReaderSchema.Id,
            subject,
            plan.ReaderSchema.Schema,
            payloadFormat);
        try
        {
            payload = _schemaRuleExecutor.TransformDeserializedDomainPayload(payload, context);
        }
        finally
        {
            context.Return();
        }

        return new MigrationResult(payload, plan.ReaderSchema);
    }

    private async Task<MigrationPlan> CreatePlanAsync(string subject, Schema writerSchema)
    {
        var writer = await _schemaRegistry.LookupSchemaAsync(
                subject,
                writerSchema,
                ignoreDeletedSchemas: false,
                normalize: false,
                CancellationToken.None)
            .ConfigureAwait(false);
        var reader = await _schemaRegistry.GetSchemaBySubjectAsync(subject, "latest", CancellationToken.None)
            .ConfigureAwait(false);

        if (writer.Version == reader.Version)
            return new MigrationPlan(reader, []);

        var steps = new List<MigrationStep>();
        if (writer.Version < reader.Version)
        {
            var previous = writer;
            for (var version = writer.Version + 1; version <= reader.Version; version++)
            {
                var current = version == reader.Version
                    ? reader
                    : await GetVersionAsync(subject, version).ConfigureAwait(false);
                if (SchemaRegistryRuleExecutor.HasActiveMigrationRule(
                        current.Schema.RuleSet,
                        SchemaRuleMode.Upgrade))
                {
                    steps.Add(new MigrationStep(SchemaRuleMode.Upgrade, previous, current));
                }

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
                    : await GetVersionAsync(subject, version).ConfigureAwait(false);
                if (SchemaRegistryRuleExecutor.HasActiveMigrationRule(
                        current.Schema.RuleSet,
                        SchemaRuleMode.Downgrade))
                {
                    steps.Add(new MigrationStep(SchemaRuleMode.Downgrade, current, previous));
                }

                current = previous;
            }
        }

        return new MigrationPlan(reader, [.. steps]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task<RegisteredSchema> GetVersionAsync(string subject, int version) =>
        _schemaRegistry.GetSchemaBySubjectAsync(
            subject,
            version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ignoreDeletedSchemas: false,
            CancellationToken.None);

    internal readonly record struct MigrationResult(
        ReadOnlyMemory<byte> Payload,
        RegisteredSchema ReaderSchema);

    private sealed record MigrationPlan(RegisteredSchema ReaderSchema, MigrationStep[] Steps);

    private sealed record CachedPlan(int SchemaId, string Subject, MigrationPlan Plan);

    private readonly record struct MigrationStep(
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
