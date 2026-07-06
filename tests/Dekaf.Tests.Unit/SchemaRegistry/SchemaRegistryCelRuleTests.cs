using System.Text;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaRegistryCelRuleTests
{
    [Test]
    public async Task ConditionRule_AllowsPayload_WhenExpressionIsTrue()
    {
        var executor = new SchemaRegistryRuleExecutor([new CelSchemaRegistryRuleHandler()]);
        var schema = CreateSchema(CreateCelRule(
            "allow-payments",
            SchemaRuleKind.Condition,
            SchemaRuleMode.Write,
            """
            topic == "orders" &&
            subject == "orders-value" &&
            schemaId == 42 &&
            component == "value" &&
            metadata("owner") == "payments" &&
            hasTag("$.card", "PII")
            """));

        var payload = "approved"u8.ToArray();
        var result = executor.TransformSerializedPayload(payload, CreateContext(schema));

        await Assert.That(result.ToArray()).IsEquivalentTo(payload);
    }

    [Test]
    public async Task ConditionRule_BlocksPayload_WhenExpressionIsFalse()
    {
        var executor = new SchemaRegistryRuleExecutor([new CelSchemaRegistryRuleHandler()]);
        var schema = CreateSchema(CreateCelRule(
            "block-non-payments",
            SchemaRuleKind.Condition,
            SchemaRuleMode.Write,
            "metadata.owner == \"fraud\" || topic == \"audit\""));

        await Assert.That(() => executor.TransformSerializedPayload("payload"u8.ToArray(), CreateContext(schema)))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("block-non-payments")
            .And
            .WithMessageContaining("false");
    }

    [Test]
    public async Task TransformRule_ReturnsStringResult_WhenSupported()
    {
        var executor = new SchemaRegistryRuleExecutor([new CelSchemaRegistryRuleHandler()]);
        var schema = CreateSchema(CreateCelRule(
            "replace",
            SchemaRuleKind.Transform,
            SchemaRuleMode.Write,
            "metadata(\"replacement\")"));

        var result = executor.TransformSerializedPayload("plain"u8.ToArray(), CreateContext(schema));

        await Assert.That(Encoding.UTF8.GetString(result.Span)).IsEqualTo("rewritten");
    }

    [Test]
    public async Task TransformRules_RespectWriteAndReadDirection()
    {
        var executor = new SchemaRegistryRuleExecutor([new CelSchemaRegistryRuleHandler()]);
        var schema = CreateSchema(
            CreateCelRule("write-rule", SchemaRuleKind.Transform, SchemaRuleMode.Write, "\"write\""),
            CreateCelRule("read-rule", SchemaRuleKind.Transform, SchemaRuleMode.Read, "\"read\""));

        var writeResult = executor.TransformSerializedPayload("payload"u8.ToArray(), CreateContext(schema));
        var readResult = executor.TransformDeserializedPayload("payload"u8.ToArray(), CreateContext(schema));

        await Assert.That(Encoding.UTF8.GetString(writeResult.Span)).IsEqualTo("write");
        await Assert.That(Encoding.UTF8.GetString(readResult.Span)).IsEqualTo("read");
    }

    [Test]
    public async Task DisabledCelRule_IsIgnored()
    {
        var executor = new SchemaRegistryRuleExecutor([new CelSchemaRegistryRuleHandler()]);
        var schema = CreateSchema(CreateCelRule(
            "disabled",
            SchemaRuleKind.Condition,
            SchemaRuleMode.Write,
            "false",
            disabled: true));
        var payload = "payload"u8.ToArray();

        var result = executor.TransformSerializedPayload(payload, CreateContext(schema));

        await Assert.That(result.ToArray()).IsEquivalentTo(payload);
    }

    [Test]
    public async Task TransformRule_BooleanResult_FailsClearly()
    {
        var executor = new SchemaRegistryRuleExecutor([new CelSchemaRegistryRuleHandler()]);
        var schema = CreateSchema(CreateCelRule(
            "unsupported-transform",
            SchemaRuleKind.Transform,
            SchemaRuleMode.Write,
            "schemaId == 42"));

        await Assert.That(() => executor.TransformSerializedPayload("payload"u8.ToArray(), CreateContext(schema)))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("unsupported-transform")
            .And
            .WithMessageContaining("only string transform results are supported");
    }

    [Test]
    public async Task UnsupportedIdentifier_FailsClearly()
    {
        var executor = new SchemaRegistryRuleExecutor([new CelSchemaRegistryRuleHandler()]);
        var schema = CreateSchema(CreateCelRule(
            "unsupported-identifier",
            SchemaRuleKind.Condition,
            SchemaRuleMode.Write,
            "message.card == true"));

        await Assert.That(() => executor.TransformSerializedPayload("""{"card":true}"""u8.ToArray(), CreateContext(schema)))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("message.card");
    }

    [Test]
    public async Task MessageVariable_RequiresUtf8Payload()
    {
        var executor = new SchemaRegistryRuleExecutor([new CelSchemaRegistryRuleHandler()]);
        var schema = CreateSchema(CreateCelRule(
            "utf8",
            SchemaRuleKind.Condition,
            SchemaRuleMode.Write,
            "message == \"payload\""));

        await Assert.That(() => executor.TransformSerializedPayload(new byte[] { 0xFF }, CreateContext(schema)))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("valid UTF-8");
    }

    [Test]
    public async Task FunctionArguments_AreValidated()
    {
        var executor = new SchemaRegistryRuleExecutor([new CelSchemaRegistryRuleHandler()]);
        var schema = CreateSchema(CreateCelRule(
            "bad-function",
            SchemaRuleKind.Condition,
            SchemaRuleMode.Write,
            "metadata(\"owner\", \"extra\") == \"payments\""));

        await Assert.That(() => executor.TransformSerializedPayload("payload"u8.ToArray(), CreateContext(schema)))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("expects 1 argument");
    }

    [Test]
    public async Task LogicalOperators_ShortCircuitUnsupportedExpressions()
    {
        var executor = new SchemaRegistryRuleExecutor([new CelSchemaRegistryRuleHandler()]);
        var schema = CreateSchema(CreateCelRule(
            "short-circuit",
            SchemaRuleKind.Condition,
            SchemaRuleMode.Write,
            "true || unsupported.identifier == true"));
        var payload = "payload"u8.ToArray();

        var result = executor.TransformSerializedPayload(payload, CreateContext(schema));

        await Assert.That(result.ToArray()).IsEquivalentTo(payload);
    }

    [Test]
    public async Task StringFunctionsAndContextIdentifiers_AreEvaluated()
    {
        var executor = new SchemaRegistryRuleExecutor([new CelSchemaRegistryRuleHandler()]);
        var schema = CreateSchema(CreateCelRule(
            "context-check",
            SchemaRuleKind.Condition,
            SchemaRuleMode.Write,
            """
            contains(message, "pay") &&
            startsWith(payload, "pay") &&
            endsWith(message, "load") &&
            hasMetadata("owner") &&
            format == "Json" &&
            payloadFormat == "Json" &&
            rule.name == "context-check" &&
            rule.type == "CEL" &&
            rule.mode == "Write" &&
            rule.kind == "Condition"
            """));
        var payload = "payload"u8.ToArray();

        var result = executor.TransformSerializedPayload(payload, CreateContext(schema));

        await Assert.That(result.ToArray()).IsEquivalentTo(payload);
    }

    private static SchemaRegistryRuleContext CreateContext(Schema schema) =>
        new()
        {
            Topic = "orders",
            Component = SerializationComponent.Value,
            SchemaId = 42,
            Subject = "orders-value",
            Schema = schema,
            PayloadFormat = SchemaRegistryPayloadFormat.Json
        };

    private static Schema CreateSchema(params SchemaRule[] rules) =>
        new()
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            Metadata = new SchemaMetadata
            {
                Tags = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
                {
                    ["$.card"] = new HashSet<string>(StringComparer.Ordinal) { "PII" }
                },
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["owner"] = "payments",
                    ["replacement"] = "rewritten"
                }
            },
            RuleSet = new SchemaRuleSet
            {
                EncodingRules = rules
            }
        };

    private static SchemaRule CreateCelRule(
        string name,
        SchemaRuleKind kind,
        SchemaRuleMode mode,
        string expression,
        bool disabled = false) =>
        new()
        {
            Name = name,
            Kind = kind,
            Mode = mode,
            Type = "CEL",
            Expr = expression,
            Disabled = disabled
        };
}
