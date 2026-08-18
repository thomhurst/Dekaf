using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaRegistryMigrationTests
{
    private static readonly SerializationContext SerializationContext = new()
    {
        Component = SerializationComponent.Value,
        Topic = "orders"
    };

    [Test]
    public async Task Transform_UpgradePath_UsesHigherSchemasAndForwardRuleOrder()
    {
        var registry = new MigrationRegistryClient();
        var v1 = CreateSchema("v1");
        var v2 = CreateSchema(
            "v2",
            CreateRule("v2-up", SchemaRuleMode.Upgrade),
            CreateRule("v2-both", SchemaRuleMode.UpDown),
            CreateRule("v2-disabled", SchemaRuleMode.Upgrade, disabled: true));
        var v3 = CreateSchema("v3", CreateRule("v3-up", SchemaRuleMode.Upgrade));
        var v1Id = registry.Register("orders-value", v1);
        registry.Register("orders-value", v2);
        var v3Id = registry.Register("orders-value", v3);
        var calls = new List<string>();
        var executor = new SchemaRegistryRuleExecutor([new CapturingMigrationHandler(calls)]);
        var runner = new SchemaRegistryMigrationRunner(registry, executor, TimeSpan.FromSeconds(1));

        var result = runner.Transform(
            "payload"u8.ToArray(),
            v1Id,
            "orders-value",
            v1,
            SerializationContext,
            SchemaRegistryPayloadFormat.Json);

        await Assert.That(Encoding.UTF8.GetString(result.Payload.Span))
            .IsEqualTo("payload|v2-up|v2-both|v3-up");
        await Assert.That(result.ReaderSchema.Id).IsEqualTo(v3Id);
        await Assert.That(result.PayloadSchemaId).IsEqualTo(v3Id);
        await Assert.That(string.Join('|', calls)).IsEqualTo(
            "v2-up:Upgrade:Write:v1->v2|v2-both:Upgrade:Write:v1->v2|v3-up:Upgrade:Write:v2->v3");
    }

    [Test]
    public async Task Transform_UpgradeTaggedFields_UsesPayloadLayoutAndTargetRuleOwner()
    {
        var registry = new MigrationRegistryClient();
        var writer = CreateSchema("v1");
        var intermediate = CreateSchema("v2", CreateRule("v2-up", SchemaRuleMode.Upgrade));
        var target = CreateSchema("v3", CreateRule("v3-up", SchemaRuleMode.Upgrade));
        var writerId = registry.Register("orders-value", writer);
        registry.Register("orders-value", intermediate);
        registry.Register("orders-value", target);
        var provider = new CapturingTaggedFieldTransformerProvider();
        var runner = new SchemaRegistryMigrationRunner(
            registry,
            new SchemaRegistryRuleExecutor([new CapturingMigrationHandler([])]),
            TimeSpan.FromSeconds(1));

        _ = runner.Transform(
            "payload"u8.ToArray(),
            writerId,
            "orders-value",
            writer,
            SerializationContext,
            SchemaRegistryPayloadFormat.Avro,
            provider);

        await Assert.That(provider.Calls.Count).IsEqualTo(4);
        await Assert.That(provider.Calls[0].PayloadSchema).IsSameReferenceAs(writer);
        await Assert.That(provider.Calls[1].PayloadSchema).IsSameReferenceAs(writer);
        await Assert.That(provider.Calls[1].RuleOwnerSchema).IsSameReferenceAs(intermediate);
        await Assert.That(provider.Calls[2].PayloadSchema).IsSameReferenceAs(intermediate);
        await Assert.That(provider.Calls[2].RuleOwnerSchema).IsSameReferenceAs(target);
        await Assert.That(provider.Calls[3].PayloadSchema).IsSameReferenceAs(target);
        await Assert.That(provider.Calls[3].RuleOwnerSchema).IsSameReferenceAs(target);
    }

    [Test]
    public async Task Transform_DowngradePath_ReversesVersionsAndRulesAndSelectsSecondAction()
    {
        var registry = new MigrationRegistryClient { LatestVersion = 1 };
        var v1 = CreateSchema("v1");
        var v2 = CreateSchema("v2", CreateRule("v2-down", SchemaRuleMode.Downgrade));
        var v3 = CreateSchema(
            "v3",
            CreateRule("v3-down", SchemaRuleMode.Downgrade),
            CreateRule("v3-both", SchemaRuleMode.UpDown, onSuccess: "NONE,CAPTURE"));
        var v1Id = registry.Register("orders-value", v1);
        registry.Register("orders-value", v2);
        var v3Id = registry.Register("orders-value", v3);
        var calls = new List<string>();
        var executor = new SchemaRegistryRuleExecutor(
            [new CapturingMigrationHandler(calls)],
            [new CapturingAction(calls)]);
        var runner = new SchemaRegistryMigrationRunner(registry, executor, TimeSpan.FromSeconds(1));

        var result = runner.Transform(
            "payload"u8.ToArray(),
            v3Id,
            "orders-value",
            v3,
            SerializationContext,
            SchemaRegistryPayloadFormat.Json);

        await Assert.That(Encoding.UTF8.GetString(result.Payload.Span))
            .IsEqualTo("payload|v3-both|v3-down|v2-down");
        await Assert.That(result.ReaderSchema.Id).IsEqualTo(v1Id);
        await Assert.That(result.PayloadSchemaId).IsEqualTo(v1Id);
        await Assert.That(string.Join('|', calls)).IsEqualTo(
            "v3-both:Downgrade:Read:v3->v2|action:v3-both:Downgrade|" +
            "v3-down:Downgrade:Read:v3->v2|v2-down:Downgrade:Read:v2->v1");
    }

    [Test]
    public async Task Transform_UpgradeWithUntransformedTail_ReturnsLastTransformedSchema()
    {
        var registry = new MigrationRegistryClient();
        var v1 = CreateSchema("v1");
        var v2 = CreateSchema("v2", CreateRule("v2-up", SchemaRuleMode.Upgrade));
        var v3 = CreateSchema("v3");
        var v1Id = registry.Register("orders-value", v1);
        var v2Id = registry.Register("orders-value", v2);
        var v3Id = registry.Register("orders-value", v3);
        var runner = new SchemaRegistryMigrationRunner(
            registry,
            new SchemaRegistryRuleExecutor([new CapturingMigrationHandler([])]),
            TimeSpan.FromSeconds(1));

        var result = runner.Transform(
            "payload"u8.ToArray(),
            v1Id,
            "orders-value",
            v1,
            SerializationContext,
            SchemaRegistryPayloadFormat.Json);

        await Assert.That(result.ReaderSchema.Id).IsEqualTo(v3Id);
        await Assert.That(result.PayloadSchemaId).IsEqualTo(v2Id);
    }

    [Test]
    public async Task Transform_MissingHandlerWithSuppressedFailure_KeepsSourcePayloadSchema()
    {
        var registry = new MigrationRegistryClient();
        var v1 = CreateSchema("v1");
        var v2 = CreateSchema(
            "v2",
            CreateRule("v2-up", SchemaRuleMode.Upgrade, "MISSING", onFailure: "NONE"));
        var v1Id = registry.Register("orders-value", v1);
        var v2Id = registry.Register("orders-value", v2);
        var runner = new SchemaRegistryMigrationRunner(
            registry,
            new SchemaRegistryRuleExecutor([]),
            TimeSpan.FromSeconds(1));

        var result = runner.Transform(
            "payload"u8.ToArray(),
            v1Id,
            "orders-value",
            v1,
            SerializationContext,
            SchemaRegistryPayloadFormat.Json);

        await Assert.That(result.ReaderSchema.Id).IsEqualTo(v2Id);
        await Assert.That(result.PayloadSchemaId).IsEqualTo(v1Id);
        await Assert.That(Encoding.UTF8.GetString(result.Payload.Span)).IsEqualTo("payload");
    }

    [Test]
    public async Task Transform_ThrowingHandlerWithSuppressedFailure_KeepsSourcePayloadSchema()
    {
        var registry = new MigrationRegistryClient();
        var v1 = CreateSchema("v1");
        var v2 = CreateSchema(
            "v2",
            CreateRule(
                "v2-up",
                SchemaRuleMode.Upgrade,
                ThrowingMigrationHandler.RuleType,
                onFailure: "NONE"));
        var v1Id = registry.Register("orders-value", v1);
        var v2Id = registry.Register("orders-value", v2);
        var runner = new SchemaRegistryMigrationRunner(
            registry,
            new SchemaRegistryRuleExecutor([new ThrowingMigrationHandler()]),
            TimeSpan.FromSeconds(1));

        var result = runner.Transform(
            "payload"u8.ToArray(),
            v1Id,
            "orders-value",
            v1,
            SerializationContext,
            SchemaRegistryPayloadFormat.Json);

        await Assert.That(result.ReaderSchema.Id).IsEqualTo(v2Id);
        await Assert.That(result.PayloadSchemaId).IsEqualTo(v1Id);
        await Assert.That(Encoding.UTF8.GetString(result.Payload.Span)).IsEqualTo("payload");
    }

    [Test]
    public async Task Transform_SuppressedFailedStep_StopsDependentMigrationChain()
    {
        var registry = new MigrationRegistryClient();
        var v1 = CreateSchema("v1");
        var v2 = CreateSchema(
            "v2",
            CreateRule("v2-up", SchemaRuleMode.Upgrade, "MISSING", onFailure: "NONE"));
        var v3 = CreateSchema("v3", CreateRule("v3-up", SchemaRuleMode.Upgrade));
        var v1Id = registry.Register("orders-value", v1);
        registry.Register("orders-value", v2);
        var v3Id = registry.Register("orders-value", v3);
        var calls = new List<string>();
        var runner = new SchemaRegistryMigrationRunner(
            registry,
            new SchemaRegistryRuleExecutor([new CapturingMigrationHandler(calls)]),
            TimeSpan.FromSeconds(1));

        var result = runner.Transform(
            "payload"u8.ToArray(),
            v1Id,
            "orders-value",
            v1,
            SerializationContext,
            SchemaRegistryPayloadFormat.Json);

        await Assert.That(result.ReaderSchema.Id).IsEqualTo(v3Id);
        await Assert.That(result.PayloadSchemaId).IsEqualTo(v1Id);
        await Assert.That(Encoding.UTF8.GetString(result.Payload.Span)).IsEqualTo("payload");
        await Assert.That(calls).IsEmpty();
    }

    [Test]
    public async Task Transform_ConditionOnlyStep_StopsDependentMigrationChain()
    {
        var registry = new MigrationRegistryClient();
        var v1 = CreateSchema("v1");
        var conditionHandler = new PassThroughConditionHandler();
        var v2 = CreateSchema(
            "v2",
            new SchemaRule
            {
                Name = "v2-condition",
                Kind = SchemaRuleKind.Condition,
                Mode = SchemaRuleMode.Upgrade,
                Type = PassThroughConditionHandler.RuleType
            });
        var v3 = CreateSchema("v3", CreateRule("v3-up", SchemaRuleMode.Upgrade));
        var v1Id = registry.Register("orders-value", v1);
        registry.Register("orders-value", v2);
        var v3Id = registry.Register("orders-value", v3);
        var migrationCalls = new List<string>();
        var runner = new SchemaRegistryMigrationRunner(
            registry,
            new SchemaRegistryRuleExecutor(
                [conditionHandler, new CapturingMigrationHandler(migrationCalls)]),
            TimeSpan.FromSeconds(1));

        var result = runner.Transform(
            "payload"u8.ToArray(),
            v1Id,
            "orders-value",
            v1,
            SerializationContext,
            SchemaRegistryPayloadFormat.Json);

        await Assert.That(result.ReaderSchema.Id).IsEqualTo(v3Id);
        await Assert.That(result.PayloadSchemaId).IsEqualTo(v1Id);
        await Assert.That(result.PayloadSchema).IsSameReferenceAs(v1);
        await Assert.That(Encoding.UTF8.GetString(result.Payload.Span)).IsEqualTo("payload");
        await Assert.That(conditionHandler.CallCount).IsEqualTo(1);
        await Assert.That(migrationCalls).IsEmpty();
    }

    [Test]
    public async Task Transform_SuppressedFailedStep_SkipsTargetDomainRules()
    {
        var registry = new MigrationRegistryClient();
        var v1 = CreateSchema("v1");
        var v2 = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "v2",
            RuleSet = new SchemaRuleSet
            {
                MigrationRules =
                [
                    CreateRule("v2-up", SchemaRuleMode.Upgrade, "MISSING", onFailure: "NONE")
                ],
                DomainRules =
                [
                    CreateRule("domain", SchemaRuleMode.Read, ReplacingRuleHandler.RuleType)
                ]
            }
        };
        var v1Id = registry.Register("orders-value", v1);
        var v2Id = registry.Register("orders-value", v2);
        var calls = new List<string>();
        var runner = new SchemaRegistryMigrationRunner(
            registry,
            new SchemaRegistryRuleExecutor([new ReplacingRuleHandler(calls)]),
            TimeSpan.FromSeconds(1));

        var result = runner.Transform(
            "payload"u8.ToArray(),
            v1Id,
            "orders-value",
            v1,
            SerializationContext,
            SchemaRegistryPayloadFormat.Json);

        await Assert.That(result.ReaderSchema.Id).IsEqualTo(v2Id);
        await Assert.That(result.PayloadSchemaId).IsEqualTo(v1Id);
        await Assert.That(Encoding.UTF8.GetString(result.Payload.Span)).IsEqualTo("payload");
        await Assert.That(calls).IsEmpty();
    }

    [Test]
    public async Task Transform_PartiallySuccessfulStepWithSuppressedFailure_FailsClosed()
    {
        var registry = new MigrationRegistryClient();
        var v1 = CreateSchema("v1");
        var v2 = CreateSchema(
            "v2",
            CreateRule("successful", SchemaRuleMode.Upgrade),
            CreateRule(
                "failing",
                SchemaRuleMode.Upgrade,
                ThrowingMigrationHandler.RuleType,
                onFailure: "NONE"));
        var v1Id = registry.Register("orders-value", v1);
        registry.Register("orders-value", v2);
        var runner = new SchemaRegistryMigrationRunner(
            registry,
            new SchemaRegistryRuleExecutor(
                [new CapturingMigrationHandler([]), new ThrowingMigrationHandler()]),
            TimeSpan.FromSeconds(1));

        await Assert.That(() => runner.Transform(
                "payload"u8.ToArray(),
                v1Id,
                "orders-value",
                v1,
                SerializationContext,
                SchemaRegistryPayloadFormat.Json))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("partially transformed");
    }

    [Test]
    public async Task Transform_MissingIntermediateVersion_ThrowsAndDoesNotCacheFailure()
    {
        var registry = new MigrationRegistryClient();
        var v1 = CreateSchema("v1");
        var v2 = CreateSchema("v2", CreateRule("v2-up", SchemaRuleMode.Upgrade));
        var v3 = CreateSchema("v3", CreateRule("v3-up", SchemaRuleMode.Upgrade));
        var v1Id = registry.Register("orders-value", v1);
        registry.Register("orders-value", v2);
        registry.Register("orders-value", v3);
        registry.RemoveVersion(2);
        var runner = new SchemaRegistryMigrationRunner(
            registry,
            new SchemaRegistryRuleExecutor([new CapturingMigrationHandler([])]),
            TimeSpan.FromSeconds(1));

        await Assert.That(() => runner.Transform(
                "payload"u8.ToArray(),
                v1Id,
                "orders-value",
                v1,
                SerializationContext,
                SchemaRegistryPayloadFormat.Json))
            .Throws<SchemaRegistryException>();

        await Assert.That(() => runner.Transform(
                "payload"u8.ToArray(),
                v1Id,
                "orders-value",
                v1,
                SerializationContext,
                SchemaRegistryPayloadFormat.Json))
            .Throws<SchemaRegistryException>();
        await Assert.That(registry.LookupCount).IsEqualTo(2);
    }

    [Test]
    public async Task PrepareAsync_RegistryTimeoutCancelsResolutionAndAllowsRetry()
    {
        var registry = new MigrationRegistryClient();
        var schema = CreateSchema("v1");
        var schemaId = registry.Register("orders-value", schema);
        registry.BlockNextLookup();
        var runner = new SchemaRegistryMigrationRunner(
            registry,
            ruleExecutor: null,
            TimeSpan.FromMilliseconds(100));

        var preparation = runner.PrepareAsync(
            schemaId,
            "orders-value",
            schema,
            CancellationToken.None).AsTask();
        await registry.WaitForBlockedLookupAsync(TimeSpan.FromSeconds(2));

        await Assert.That(registry.LastLookupCancellationToken.CanBeCanceled).IsTrue();
        await Assert.That(async () => await preparation.WaitAsync(TimeSpan.FromSeconds(2)))
            .Throws<TimeoutException>()
            .WithMessageContaining("migration plan resolution timed out");

        await runner.PrepareAsync(schemaId, "orders-value", schema, CancellationToken.None);

        await Assert.That(registry.LookupCount).IsEqualTo(2);
        await Assert.That(runner.TryUsePreparedPlan(schemaId, "orders-value")).IsTrue();
    }

    [Test]
    public async Task Transform_NoMigration_CachesNeutralPlanAndReturnsOriginalPayload()
    {
        var registry = new MigrationRegistryClient();
        var schema = CreateSchema("v1");
        var schemaId = registry.Register("orders-value", schema);
        var calls = new List<string>();
        var runner = new SchemaRegistryMigrationRunner(
            registry,
            new SchemaRegistryRuleExecutor([new CapturingMigrationHandler(calls)]),
            TimeSpan.FromSeconds(1));
        var payload = "payload"u8.ToArray();

        var first = runner.Transform(
            payload,
            schemaId,
            "orders-value",
            schema,
            SerializationContext,
            SchemaRegistryPayloadFormat.Json);
        var second = runner.Transform(
            payload,
            schemaId,
            "orders-value",
            schema,
            SerializationContext,
            SchemaRegistryPayloadFormat.Json);

        await Assert.That(first.Payload.ToArray()).IsEquivalentTo(payload);
        await Assert.That(second.Payload.ToArray()).IsEquivalentTo(payload);
        await Assert.That(first.PayloadSchemaId).IsEqualTo(schemaId);
        await Assert.That(second.PayloadSchemaId).IsEqualTo(schemaId);
        await Assert.That(calls).IsEmpty();
        await Assert.That(registry.LookupCount).IsEqualTo(1);
        await Assert.That(registry.LatestCount).IsEqualTo(1);
    }

    [Test]
    public async Task Transform_ExpiredLatestPlan_RefreshesReaderSchema()
    {
        var registry = new MigrationRegistryClient { LatestCacheTtlSecs = 0 };
        var v1 = CreateSchema("v1");
        var v2 = CreateSchema("v2");
        var v1Id = registry.Register("orders-value", v1);
        var v2Id = registry.Register("orders-value", v2);
        var runner = new SchemaRegistryMigrationRunner(registry, ruleExecutor: null, TimeSpan.FromSeconds(1));

        var first = runner.Transform(
            "payload"u8.ToArray(),
            v1Id,
            "orders-value",
            v1,
            SerializationContext,
            SchemaRegistryPayloadFormat.Json);
        var v3 = CreateSchema("v3");
        var v3Id = registry.Register("orders-value", v3);
        var second = runner.Transform(
            "payload"u8.ToArray(),
            v1Id,
            "orders-value",
            v1,
            SerializationContext,
            SchemaRegistryPayloadFormat.Json);

        await Assert.That(first.ReaderSchema.Id).IsEqualTo(v2Id);
        await Assert.That(second.ReaderSchema.Id).IsEqualTo(v3Id);
        await Assert.That(registry.LatestCount).IsEqualTo(2);
    }

    [Test]
    public async Task DeletedVersionLookup_DefaultImplementation_FailsClosed()
    {
        ISchemaRegistryClient registry = new MockSchemaRegistryClient();
        async Task LookupDeletedVersionAsync() =>
            _ = await registry.GetSchemaBySubjectAsync(
                "orders-value",
                "2",
                ignoreDeletedSchemas: false);

        await Assert.That(LookupDeletedVersionAsync)
            .Throws<NotSupportedException>()
            .WithMessageContaining("deleted schema versions");
    }

    [Test]
    public async Task JsonDeserializer_UseLatestVersion_ExecutesEncodingMigrationDomainThenReadsTarget()
    {
        var registry = new MigrationRegistryClient();
        var v1 = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "v1",
            RuleSet = new SchemaRuleSet
            {
                EncodingRules = [CreateRule("decode", SchemaRuleMode.Read, ReplacingRuleHandler.RuleType)]
            }
        };
        var v2 = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "v2",
            RuleSet = new SchemaRuleSet
            {
                MigrationRules = [CreateRule("migrate", SchemaRuleMode.Upgrade, ReplacingRuleHandler.RuleType)],
                DomainRules = [CreateRule("domain", SchemaRuleMode.Read, ReplacingRuleHandler.RuleType)]
            }
        };
        var writerId = registry.Register("orders-value", v1);
        registry.Register("orders-value", v2);
        var calls = new List<string>();
        var executor = new SchemaRegistryRuleExecutor([new ReplacingRuleHandler(calls)]);
        await using var deserializer = new JsonSchemaRegistryDeserializer<MigrationPayload>(
            registry,
            jsonOptions: null,
            new SchemaRegistryDeserializerConfig { UseLatestVersion = true },
            ruleExecutor: executor);
        var json = "{\"name\":\"encoded\"}"u8;
        var wire = new byte[5 + json.Length];
        BinaryPrimitives.WriteInt32BigEndian(wire.AsSpan(1, 4), writerId);
        json.CopyTo(wire.AsSpan(5));

        var result = deserializer.Deserialize(wire, SerializationContext);

        await Assert.That(result.Name).IsEqualTo("final");
        await Assert.That(string.Join('|', calls)).IsEqualTo(
            "decode:Read:null|migrate:Write:Upgrade|domain:Read:null");
    }

    [Test]
    public async Task GenericDeserializer_UseLatestWithoutRules_SelectsReaderSchemaWithoutExecutor()
    {
        var registry = new MigrationRegistryClient();
        var v1 = CreateSchema("v1");
        var v2 = CreateSchema("v2");
        var writerId = registry.Register("orders-value", v1);
        registry.Register("orders-value", v2);
        Schema? observedSchema = null;
        await using var deserializer = SchemaRegistryDeserializer.Create(
            registry,
            (ReadOnlyMemory<byte> payload, Schema schema) =>
            {
                observedSchema = schema;
                return Encoding.UTF8.GetString(payload.Span);
            },
            new SchemaRegistryDeserializerConfig { UseLatestVersion = true });
        var payload = "payload"u8;
        var wire = new byte[5 + payload.Length];
        BinaryPrimitives.WriteInt32BigEndian(wire.AsSpan(1, 4), writerId);
        payload.CopyTo(wire.AsSpan(5));

        var result = deserializer.Deserialize(wire, SerializationContext);

        await Assert.That(result).IsEqualTo("payload");
        await Assert.That(observedSchema).IsSameReferenceAs(v2);
    }

    [Test]
    public async Task GenericDeserializer_ActiveMigrationWithoutBuiltInExecutor_FailsClosed()
    {
        var registry = new MigrationRegistryClient();
        var v1 = CreateSchema("v1");
        var v2 = CreateSchema("v2", CreateRule("upgrade", SchemaRuleMode.Upgrade));
        var writerId = registry.Register("orders-value", v1);
        registry.Register("orders-value", v2);
        await using var deserializer = SchemaRegistryDeserializer.Create(
            registry,
            static (ReadOnlyMemory<byte> payload, Schema _) => payload,
            new SchemaRegistryDeserializerConfig { UseLatestVersion = true });
        var wire = new byte[6];
        BinaryPrimitives.WriteInt32BigEndian(wire.AsSpan(1, 4), writerId);

        await Assert.That(() => deserializer.Deserialize(wire, SerializationContext))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining(nameof(SchemaRegistryRuleExecutor));
    }

    [Test]
    public async Task GenericDeserializer_ServerMigrationWithoutExecutor_SelectsLatest()
    {
        var registry = new MigrationRegistryClient();
        var v1 = CreateSchema("v1");
        var v2 = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "v2",
            RuleSet = new SchemaRuleSet
            {
                EnableAt = "SERVER",
                MigrationRules = [CreateRule("upgrade", SchemaRuleMode.Upgrade)]
            }
        };
        var writerId = registry.Register("orders-value", v1);
        registry.Register("orders-value", v2);
        Schema? observedSchema = null;
        await using var deserializer = SchemaRegistryDeserializer.Create(
            registry,
            (ReadOnlyMemory<byte> payload, Schema schema) =>
            {
                observedSchema = schema;
                return payload;
            },
            new SchemaRegistryDeserializerConfig { UseLatestVersion = true });
        var wire = new byte[6];
        BinaryPrimitives.WriteInt32BigEndian(wire.AsSpan(1, 4), writerId);

        _ = deserializer.Deserialize(wire, SerializationContext);

        await Assert.That(observedSchema).IsSameReferenceAs(v2);
    }

    private static Schema CreateSchema(string name, params SchemaRule[] migrationRules) =>
        new()
        {
            SchemaType = SchemaType.Json,
            SchemaString = name,
            RuleSet = new SchemaRuleSet { MigrationRules = migrationRules }
        };

    private static SchemaRule CreateRule(
        string name,
        SchemaRuleMode mode,
        bool disabled = false,
        string? onSuccess = null) =>
        CreateRule(name, mode, CapturingMigrationHandler.RuleType, disabled, onSuccess);

    private static SchemaRule CreateRule(
        string name,
        SchemaRuleMode mode,
        string type,
        bool disabled = false,
        string? onSuccess = null,
        string? onFailure = null) =>
        new()
        {
            Name = name,
            Kind = SchemaRuleKind.Transform,
            Mode = mode,
            Type = type,
            Disabled = disabled,
            OnSuccess = onSuccess,
            OnFailure = onFailure
        };

    private sealed class CapturingMigrationHandler(List<string> calls) : ISchemaRegistryRuleHandler
    {
        internal const string RuleType = "MIGRATE";

        public string Type => RuleType;

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => Transform(payload, context);

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => Transform(payload, context);

        private ReadOnlyMemory<byte> Transform(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context)
        {
            var migration = context.PayloadContext;
            calls.Add(
                $"{context.Rule.Name}:{migration.RuleMode}:{context.Direction}:{migration.SourceSchema!.SchemaString}->{migration.TargetSchema!.SchemaString}");
            var suffix = Encoding.UTF8.GetBytes($"|{context.Rule.Name}");
            var result = new byte[payload.Length + suffix.Length];
            payload.Span.CopyTo(result);
            suffix.CopyTo(result.AsSpan(payload.Length));
            return result;
        }
    }

    private sealed class PassThroughConditionHandler : ISchemaRegistryRuleHandler
    {
        internal const string RuleType = "CONDITION";

        internal int CallCount { get; private set; }

        public string Type => RuleType;

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context)
        {
            CallCount++;
            return payload;
        }

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context)
        {
            CallCount++;
            return payload;
        }
    }

    private sealed class CapturingTaggedFieldTransformerProvider : ISchemaRegistryTaggedFieldTransformerProvider
    {
        internal List<(Schema PayloadSchema, Schema? RuleOwnerSchema)> Calls { get; } = [];

        public ISchemaRegistryTaggedFieldTransformer Get(
            Schema payloadSchema,
            Schema? ruleOwnerSchema = null)
        {
            Calls.Add((payloadSchema, ruleOwnerSchema));
            return PassthroughTaggedFieldTransformer.Instance;
        }
    }

    private sealed class PassthroughTaggedFieldTransformer : ISchemaRegistryTaggedFieldTransformer
    {
        internal static PassthroughTaggedFieldTransformer Instance { get; } = new();

        public ReadOnlyMemory<byte> Transform<TState>(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context,
            TState state,
            SchemaRegistryFieldTransform<TState> transform) => payload;
    }

    private sealed class CapturingAction(List<string> calls) : ISchemaRegistryRuleAction
    {
        public string Type => "CAPTURE";

        public void Run(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context,
            SchemaRegistryRuleException? exception) =>
            calls.Add($"action:{context.Rule.Name}:{context.PayloadContext.RuleMode}");
    }

    private sealed class ThrowingMigrationHandler : ISchemaRegistryRuleHandler
    {
        internal const string RuleType = "THROW";

        public string Type => RuleType;

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => throw new InvalidOperationException("failure");

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => throw new InvalidOperationException("failure");
    }

    private sealed class ReplacingRuleHandler(List<string> calls) : ISchemaRegistryRuleHandler
    {
        internal const string RuleType = "REPLACE";

        public string Type => RuleType;

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => Transform(payload, context);

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => Transform(payload, context);

        private ReadOnlyMemory<byte> Transform(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context)
        {
            calls.Add($"{context.Rule.Name}:{context.Direction}:{context.PayloadContext.RuleMode?.ToString() ?? "null"}");
            var json = Encoding.UTF8.GetString(payload.Span);
            var transformed = context.Rule.Name switch
            {
                "decode" => json.Replace("encoded", "writer", StringComparison.Ordinal),
                "migrate" => json.Replace("writer", "migrated", StringComparison.Ordinal),
                "domain" => json.Replace("migrated", "final", StringComparison.Ordinal),
                _ => json
            };
            return Encoding.UTF8.GetBytes(transformed);
        }
    }

    private sealed class MigrationPayload
    {
        public string Name { get; init; } = "";
    }

    private sealed class MigrationRegistryClient : ISchemaRegistryClient
    {
        private readonly Dictionary<int, RegisteredSchema> _byId = [];
        private readonly Dictionary<int, RegisteredSchema> _byVersion = [];
        private TaskCompletionSource? _lookupEntered;
        private TaskCompletionSource? _lookupRelease;
        private int _nextId = 1;

        public int LatestCacheTtlSecs { get; init; } = -1;
        internal int LatestVersion { get; init; }
        internal int LookupCount { get; private set; }
        internal int LatestCount { get; private set; }
        internal CancellationToken LastLookupCancellationToken { get; private set; }

        internal void BlockNextLookup()
        {
            _lookupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _lookupRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        internal async Task WaitForBlockedLookupAsync(TimeSpan timeout)
        {
            var entered = _lookupEntered
                ?? throw new InvalidOperationException("No blocked schema lookup was configured.");
            await entered.Task.WaitAsync(timeout).ConfigureAwait(false);
        }

        internal int Register(string subject, Schema schema)
        {
            var id = _nextId++;
            var registered = new RegisteredSchema
            {
                Id = id,
                Subject = subject,
                Version = id,
                Schema = schema
            };
            _byId.Add(id, registered);
            _byVersion.Add(id, registered);
            return id;
        }

        internal void RemoveVersion(int version) => _byVersion.Remove(version);

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byId[id].Schema);

        public Task<Schema> GetSchemaAsync(
            int id,
            string subject,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_byId[id].Schema);

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default)
        {
            if (version == "latest")
                LatestCount++;

            var requestedVersion = version == "latest"
                ? LatestVersion == 0 ? _byVersion.Keys.Max() : LatestVersion
                : int.Parse(version, System.Globalization.CultureInfo.InvariantCulture);
            if (!_byVersion.TryGetValue(requestedVersion, out var schema))
                throw new SchemaRegistryException(40402, $"Version {requestedVersion} not found.");

            return Task.FromResult(schema);
        }

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version,
            bool ignoreDeletedSchemas,
            CancellationToken cancellationToken = default) =>
            GetSchemaBySubjectAsync(subject, version, cancellationToken);

        public async Task<RegisteredSchema> LookupSchemaAsync(
            string subject,
            Schema schema,
            bool ignoreDeletedSchemas = true,
            bool normalize = false,
            CancellationToken cancellationToken = default)
        {
            LookupCount++;
            LastLookupCancellationToken = cancellationToken;
            var release = Interlocked.Exchange(ref _lookupRelease, null);
            if (release is not null)
            {
                _lookupEntered!.TrySetResult();
                await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            var registered = _byId.Values.SingleOrDefault(candidate => ReferenceEquals(candidate.Schema, schema))
                ?? throw new SchemaRegistryException(40403, "Schema not found.");
            return registered;
        }

        public Task<int> RegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<int>> GetVersionsAsync(
            string subject,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsCompatibleAsync(
            string subject,
            Schema schema,
            string version = "latest",
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(
            string subject,
            bool permanent = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
