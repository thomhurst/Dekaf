using System.Text;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaRegistryRuleExecutorTests
{
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
        new()
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            RuleSet = new SchemaRuleSet
            {
                EncodingRules = rules
            }
        };

    private static SchemaRule CreateRule(
        string name,
        string type,
        SchemaRuleMode mode,
        bool disabled = false,
        SchemaRuleKind kind = SchemaRuleKind.Transform) =>
        new()
        {
            Name = name,
            Kind = kind,
            Mode = mode,
            Type = type,
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
}
