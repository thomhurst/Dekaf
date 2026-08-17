using System.Text;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaRegistryRuleExecutorTests
{
    [Test]
    public async Task RuleContextPool_ReusesPrimaryContext_AfterNestedRental()
    {
        var schema = new Schema { SchemaString = "{}" };
        var primary = SchemaRegistryRuleContext.Rent(
            "primary",
            SerializationComponent.Key,
            1,
            "primary-key",
            schema,
            SchemaRegistryPayloadFormat.Custom);
        var overflow = SchemaRegistryRuleContext.Rent(
            "overflow",
            SerializationComponent.Value,
            2,
            "overflow-value",
            schema,
            SchemaRegistryPayloadFormat.Json);

        overflow.Return();
        primary.Return();
        var primaryReferencesCleared = primary.Topic is null && primary.Subject is null && primary.Schema is null;
        var overflowReferencesCleared = overflow.Topic is null && overflow.Subject is null && overflow.Schema is null;

        var reused = SchemaRegistryRuleContext.Rent(
            "reused",
            SerializationComponent.Value,
            3,
            "reused-value",
            null,
            SchemaRegistryPayloadFormat.Avro);
        var reusedPrimary = ReferenceEquals(primary, reused);
        var topic = reused.Topic;
        var component = reused.Component;
        var schemaId = reused.SchemaId;
        var subject = reused.Subject;
        var payloadFormat = reused.PayloadFormat;
        reused.Return();

        await Assert.That(reusedPrimary).IsTrue();
        await Assert.That(primaryReferencesCleared).IsTrue();
        await Assert.That(overflowReferencesCleared).IsTrue();
        await Assert.That(topic).IsEqualTo("reused");
        await Assert.That(component).IsEqualTo(SerializationComponent.Value);
        await Assert.That(schemaId).IsEqualTo(3);
        await Assert.That(subject).IsEqualTo("reused-value");
        await Assert.That(payloadFormat).IsEqualTo(SchemaRegistryPayloadFormat.Avro);
    }

    [Test]
    public async Task TransformSerializedPayload_AppliesDomainBeforeEncodingRules()
    {
        var calls = new List<string>();
        var executor = new SchemaRegistryRuleExecutor(
        [
            new AppendingRuleHandler("DOMAIN", calls),
            new AppendingRuleHandler("ENCODING", calls)
        ]);
        var schema = CreateSchema(
            domainRules:
            [
                CreateRule("domain-a", "DOMAIN", SchemaRuleMode.WriteRead),
                CreateRule("disabled", "missing", SchemaRuleMode.WriteRead, disabled: true)
            ],
            encodingRules: [CreateRule("encoding-a", "ENCODING", SchemaRuleMode.WriteRead)]);

        var result = executor.TransformSerializedPayload("payload"u8.ToArray(), CreateContext(schema));

        await Assert.That(Encoding.UTF8.GetString(result.Span))
            .IsEqualTo("payload|DOMAINW|ENCODINGW");
        await Assert.That(calls).IsEquivalentTo([
            "Write:domain-a:Json",
            "Write:encoding-a:Json"
        ]);
    }

    [Test]
    public async Task TransformDeserializedPayload_AppliesEncodingBeforeReversedDomainRules()
    {
        var calls = new List<string>();
        var executor = new SchemaRegistryRuleExecutor(
        [
            new AppendingRuleHandler("DOMAIN-A", calls),
            new AppendingRuleHandler("DOMAIN-B", calls),
            new AppendingRuleHandler("ENCODING", calls)
        ]);
        var schema = CreateSchema(
            domainRules:
            [
                CreateRule("domain-a", "DOMAIN-A", SchemaRuleMode.WriteRead),
                CreateRule("domain-b", "DOMAIN-B", SchemaRuleMode.Read)
            ],
            encodingRules: [CreateRule("encoding-a", "ENCODING", SchemaRuleMode.WriteRead)]);

        var result = executor.TransformDeserializedPayload("payload"u8.ToArray(), CreateContext(schema));

        await Assert.That(Encoding.UTF8.GetString(result.Span))
            .IsEqualTo("payload|ENCODINGR|DOMAIN-BR|DOMAIN-AR");
        await Assert.That(calls).IsEquivalentTo([
            "Read:encoding-a:Json",
            "Read:domain-b:Json",
            "Read:domain-a:Json"
        ]);
    }

    [Test]
    public async Task TransformSerializedPayload_SourceArrayMutationChangesCallerOwnedRuleSet()
    {
        var calls = new List<string>();
        var originalRule = CreateRule("original", "ORIGINAL", SchemaRuleMode.Write);
        var replacementRule = CreateRule("replacement", "REPLACEMENT", SchemaRuleMode.Write);
        var rules = new[] { originalRule };
        var schema = CreateSchema(domainRules: rules);
        var executor = new SchemaRegistryRuleExecutor(
        [
            new AppendingRuleHandler("ORIGINAL", calls),
            new AppendingRuleHandler("REPLACEMENT", calls)
        ]);

        executor.TransformSerializedPayload("payload"u8.ToArray(), CreateContext(schema));
        rules[0] = replacementRule;
        executor.TransformSerializedPayload("payload"u8.ToArray(), CreateContext(schema));

        await Assert.That(calls).IsEquivalentTo([
            "Write:original:Json",
            "Write:replacement:Json"
        ]);
        await Assert.That(schema.RuleSet!.DomainRules![0]).IsSameReferenceAs(replacementRule);
    }

    [Test]
    public async Task TransformSerializedPayload_AppliesActiveEncodingRulesInOrder()
    {
        var calls = new List<string>();
        var executor = new SchemaRegistryRuleExecutor(
        [
            new AppendingRuleHandler("A", calls),
            new AppendingRuleHandler("B", calls)
        ]);

        var schema = CreateSchema(
            CreateRule("encrypt-a", "A", SchemaRuleMode.WriteRead),
            CreateRule("write-b", "B", SchemaRuleMode.Write),
            CreateRule("disabled", "missing", SchemaRuleMode.WriteRead, disabled: true),
            CreateRule("read-only", "missing", SchemaRuleMode.Read));

        var result = executor.TransformSerializedPayload(
            "payload"u8.ToArray(),
            CreateContext(schema));

        await Assert.That(Encoding.UTF8.GetString(result.Span)).IsEqualTo("payload|AW|BW");
        await Assert.That(calls).IsEquivalentTo([
            "Write:encrypt-a:Json",
            "Write:write-b:Json"
        ]);
    }

    [Test]
    public async Task TransformDeserializedPayload_AppliesActiveEncodingRulesInReverseOrder()
    {
        var calls = new List<string>();
        var executor = new SchemaRegistryRuleExecutor(
        [
            new AppendingRuleHandler("A", calls),
            new AppendingRuleHandler("B", calls)
        ]);

        var schema = CreateSchema(
            CreateRule("encrypt-a", "A", SchemaRuleMode.WriteRead),
            CreateRule("read-b", "B", SchemaRuleMode.Read),
            CreateRule("write-only", "missing", SchemaRuleMode.Write));

        var result = executor.TransformDeserializedPayload(
            "payload"u8.ToArray(),
            CreateContext(schema));

        await Assert.That(Encoding.UTF8.GetString(result.Span)).IsEqualTo("payload|BR|AR");
        await Assert.That(calls).IsEquivalentTo([
            "Read:read-b:Json",
            "Read:encrypt-a:Json"
        ]);
    }

    [Test]
    public async Task TransformSerializedPayload_MissingHandlerForActiveRule_Throws()
    {
        var executor = new SchemaRegistryRuleExecutor([]);
        var schema = CreateSchema(CreateRule("encrypt", "ENCRYPT", SchemaRuleMode.Write));

        await Assert.That(() => executor.TransformSerializedPayload("payload"u8.ToArray(), CreateContext(schema)))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("ENCRYPT")
            .And
            .WithMessageContaining("encrypt");
    }

    [Test]
    public async Task TransformSerializedPayload_NullSchema_ReturnsOriginalPayload()
    {
        var executor = new SchemaRegistryRuleExecutor([]);
        var payload = "payload"u8.ToArray();

        var result = executor.TransformSerializedPayload(payload, CreateContext(schema: null));

        await Assert.That(result.ToArray()).IsEquivalentTo(payload);
    }

    [Test]
    public async Task TransformSerializedPayload_ConditionKindEncodingRule_UsesHandler()
    {
        var calls = new List<string>();
        var executor = new SchemaRegistryRuleExecutor([new AppendingRuleHandler("A", calls)]);
        var payload = "payload"u8.ToArray();
        var schema = CreateSchema(
            CreateRule("condition", "A", SchemaRuleMode.WriteRead, kind: SchemaRuleKind.Condition));

        var result = executor.TransformSerializedPayload(payload, CreateContext(schema));

        await Assert.That(Encoding.UTF8.GetString(result.Span)).IsEqualTo("payload|AW");
        await Assert.That(calls).IsEquivalentTo(["Write:condition:Json"]);
    }

    [Test]
    public async Task Constructor_DuplicateHandlerType_Throws()
    {
        var calls = new List<string>();

        await Assert.That(() =>
            _ = new SchemaRegistryRuleExecutor(
            [
                new AppendingRuleHandler("ENCRYPT", calls),
                new AppendingRuleHandler("encrypt", calls)
            ]))
            .Throws<ArgumentException>()
            .WithMessageContaining("encrypt");
    }

    [Test]
    public async Task TransformSerializedPayload_FailureActionNone_SuppressesRuleFailure()
    {
        var executor = new SchemaRegistryRuleExecutor([]);
        var rule = CreateRule("optional", "missing", SchemaRuleMode.Write, onFailure: "NONE");
        var schema = CreateSchema(domainRules: [rule]);
        var payload = "payload"u8.ToArray();

        var result = executor.TransformSerializedPayload(payload, CreateContext(schema));

        await Assert.That(result.ToArray()).IsEquivalentTo(payload);
    }

    [Test]
    public async Task TransformSerializedPayload_EmptyEnableAt_AppliesRules()
    {
        var calls = new List<string>();
        var executor = new SchemaRegistryRuleExecutor([new AppendingRuleHandler("A", calls)]);
        var schema = CreateSchema(
            domainRules: [CreateRule("client-rule", "A", SchemaRuleMode.Write)],
            enableAt: string.Empty);

        _ = executor.TransformSerializedPayload("payload"u8.ToArray(), CreateContext(schema));

        await Assert.That(calls).IsEquivalentTo(["Write:client-rule:Json"]);
    }

    [Test]
    [Arguments("GATEWAY")]
    [Arguments("SERVER")]
    [Arguments("NONE")]
    public async Task TransformSerializedPayload_NonClientEnableAt_SkipsRules(string enableAt)
    {
        var executor = new SchemaRegistryRuleExecutor([]);
        var schema = CreateSchema(
            domainRules: [CreateRule("server-only", "missing", SchemaRuleMode.Write)],
            enableAt: enableAt);
        var payload = "payload"u8.ToArray();

        var result = executor.TransformSerializedPayload(payload, CreateContext(schema));

        await Assert.That(result.ToArray()).IsEquivalentTo(payload);
    }

    [Test]
    public async Task TransformSerializedPayload_UnknownEnableAt_Throws()
    {
        var executor = new SchemaRegistryRuleExecutor([]);
        var schema = CreateSchema(
            domainRules: [CreateRule("protected", "missing", SchemaRuleMode.Write)],
            enableAt: "client");

        await Assert.That(() => executor.TransformSerializedPayload("payload"u8.ToArray(), CreateContext(schema)))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("client");
    }

    [Test]
    public async Task TransformSerializedPayload_MutableRuleListReflectsAddedRules()
    {
        var calls = new List<string>();
        var rules = new List<SchemaRule>();
        var executor = new SchemaRegistryRuleExecutor([new AppendingRuleHandler("A", calls)]);
        var schema = CreateSchema(domainRules: rules);

        var before = executor.TransformSerializedPayload("payload"u8.ToArray(), CreateContext(schema));
        rules.Add(CreateRule("added", "A", SchemaRuleMode.Write));
        var after = executor.TransformSerializedPayload("payload"u8.ToArray(), CreateContext(schema));

        await Assert.That(Encoding.UTF8.GetString(before.Span)).IsEqualTo("payload");
        await Assert.That(Encoding.UTF8.GetString(after.Span)).IsEqualTo("payload|AW");
    }

    [Test]
    public async Task TransformDeserializedPayload_WriteReadActions_SelectReadAction()
    {
        var calls = new List<string>();
        var action = new CapturingRuleAction("CAPTURE", calls);
        var executor = new SchemaRegistryRuleExecutor(
            [new AppendingRuleHandler("A", calls)],
            [action]);
        var rule = CreateRule(
            "transform",
            "A",
            SchemaRuleMode.WriteRead,
            onSuccess: "NONE,CAPTURE,UNKNOWN");
        var schema = CreateSchema(domainRules: [rule]);

        _ = executor.TransformSerializedPayload("payload"u8.ToArray(), CreateContext(schema));
        _ = executor.TransformDeserializedPayload("payload"u8.ToArray(), CreateContext(schema));

        await Assert.That(calls).IsEquivalentTo([
            "Write:transform:Json",
            "Read:transform:Json",
            "Action:CAPTURE:Read:transform:success"
        ]);
    }

    [Test]
    public async Task TransformSerializedPayload_CustomFailureAction_ReceivesFailureAndContinues()
    {
        var calls = new List<string>();
        var executor = new SchemaRegistryRuleExecutor([], [new CapturingRuleAction("CAPTURE", calls)]);
        var rule = CreateRule(
            "missing-rule",
            "missing",
            SchemaRuleMode.Write,
            onFailure: "CAPTURE");
        var schema = CreateSchema(domainRules: [rule]);

        var result = executor.TransformSerializedPayload("payload"u8.ToArray(), CreateContext(schema));

        await Assert.That(Encoding.UTF8.GetString(result.Span)).IsEqualTo("payload");
        await Assert.That(calls).IsEquivalentTo([
            "Action:CAPTURE:Write:missing-rule:failure"
        ]);
    }

    [Test]
    public async Task TransformSerializedPayload_UnknownFailureAction_Throws()
    {
        var executor = new SchemaRegistryRuleExecutor([]);
        var rule = CreateRule(
            "missing-rule",
            "missing",
            SchemaRuleMode.Write,
            onFailure: "UNKNOWN");
        var schema = CreateSchema(domainRules: [rule]);

        await Assert.That(() => executor.TransformSerializedPayload("payload"u8.ToArray(), CreateContext(schema)))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("UNKNOWN");
    }

    [Test]
    public async Task TransformSerializedPayload_HandlerException_UsesConfiguredFailureAction()
    {
        var executor = new SchemaRegistryRuleExecutor([new ThrowingRuleHandler()]);
        var rule = CreateRule(
            "failing-rule",
            ThrowingRuleHandler.RuleType,
            SchemaRuleMode.Write,
            onFailure: "NONE");
        var schema = CreateSchema(domainRules: [rule]);
        var payload = "payload"u8.ToArray();

        var result = executor.TransformSerializedPayload(payload, CreateContext(schema));

        await Assert.That(result.ToArray()).IsEquivalentTo(payload);
    }

    private static SchemaRegistryRuleContext CreateContext(Schema? schema) =>
        new()
        {
            Topic = "test-topic",
            Component = SerializationComponent.Value,
            SchemaId = 42,
            Subject = "test-topic-value",
            Schema = schema,
            PayloadFormat = SchemaRegistryPayloadFormat.Json
        };

    private static Schema CreateSchema(params SchemaRule[] rules) =>
        CreateSchema(encodingRules: rules);

    private static Schema CreateSchema(
        IReadOnlyList<SchemaRule>? domainRules = null,
        IReadOnlyList<SchemaRule>? encodingRules = null,
        string? enableAt = null) =>
        new()
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            RuleSet = new SchemaRuleSet
            {
                DomainRules = domainRules,
                EncodingRules = encodingRules,
                EnableAt = enableAt
            }
        };

    private static SchemaRule CreateRule(
        string name,
        string type,
        SchemaRuleMode mode,
        bool disabled = false,
        SchemaRuleKind kind = SchemaRuleKind.Transform,
        string? onSuccess = null,
        string? onFailure = null) =>
        new()
        {
            Name = name,
            Kind = kind,
            Mode = mode,
            Type = type,
            OnSuccess = onSuccess,
            OnFailure = onFailure,
            Disabled = disabled
        };

    private sealed class AppendingRuleHandler(
        string type,
        List<string> calls) : ISchemaRegistryRuleHandler
    {
        public string Type => type;

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context)
        {
            calls.Add($"{context.Direction}:{context.Rule.Name}:{context.PayloadContext.PayloadFormat}");
            return Append(payload, $"|{Type}W");
        }

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context)
        {
            calls.Add($"{context.Direction}:{context.Rule.Name}:{context.PayloadContext.PayloadFormat}");
            return Append(payload, $"|{Type}R");
        }

        private static ReadOnlyMemory<byte> Append(ReadOnlyMemory<byte> payload, string suffix)
        {
            var suffixBytes = Encoding.UTF8.GetBytes(suffix);
            var result = new byte[payload.Length + suffixBytes.Length];
            payload.Span.CopyTo(result);
            suffixBytes.CopyTo(result.AsSpan(payload.Length));
            return result;
        }
    }

    private sealed class CapturingRuleAction(
        string type,
        List<string> calls) : ISchemaRegistryRuleAction
    {
        public string Type => type;

        public void Run(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context,
            SchemaRegistryRuleException? exception)
        {
            calls.Add(
                $"Action:{Type}:{context.Direction}:{context.Rule.Name}:{(exception is null ? "success" : "failure")}");
        }
    }

    private sealed class ThrowingRuleHandler : ISchemaRegistryRuleHandler
    {
        public const string RuleType = "THROW";

        public string Type => RuleType;

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) =>
            throw new InvalidOperationException("Handler failed.");

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) =>
            throw new InvalidOperationException("Handler failed.");
    }
}
