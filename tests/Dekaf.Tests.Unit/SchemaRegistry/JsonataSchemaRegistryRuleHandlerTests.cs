using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Jsonata;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class JsonataSchemaRegistryRuleHandlerTests
{
    private static readonly SerializationContext SerializationContext = new()
    {
        Topic = "orders",
        Component = SerializationComponent.Value
    };

    [Test]
    [Arguments("$merge([$, {'fullName': first & ' ' & last}])", "fullName", "\"Ada Lovelace\"")]
    [Arguments("$merge([$, {'total': $sum(items.(price * quantity))}])", "total", "25")]
    [Arguments("{'ids': items[quantity > 1].id[], 'count': $count(items)}", "ids", "[2]")]
    [Arguments("missing ? missing : null", "", "null")]
    public async Task TransformSerializedPayload_ConfluentExpressions_ProduceExpectedJson(
        string expression,
        string property,
        string expectedJson)
    {
        var payload = Apply(
            expression,
            """
            {
              "first": "Ada",
              "last": "Lovelace",
              "items": [
                { "id": 1, "price": 5, "quantity": 1 },
                { "id": 2, "price": 10, "quantity": 2 }
              ]
            }
            """u8.ToArray(),
            SchemaRegistryRuleDirection.Write);

        using var actual = JsonDocument.Parse(payload);
        using var expected = JsonDocument.Parse(expectedJson);
        var actualValue = string.IsNullOrEmpty(property)
            ? actual.RootElement
            : actual.RootElement.GetProperty(property);

        await Assert.That(JsonElement.DeepEquals(actualValue, expected.RootElement)).IsTrue();
    }

    [Test]
    public async Task TransformDeserializedPayload_ReadRule_TransformsJson()
    {
        var payload = Apply(
            "$merge([$, {'read': true}])",
            """{"id":7}"""u8.ToArray(),
            SchemaRegistryRuleDirection.Read);

        using var document = JsonDocument.Parse(payload);
        await Assert.That(document.RootElement.GetProperty("id").GetInt32()).IsEqualTo(7);
        await Assert.That(document.RootElement.GetProperty("read").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task ConditionRule_RequiresTrueBoolean()
    {
        var payload = """{"total":12}"""u8.ToArray();
        var passing = Apply(
            "total >= 10",
            payload,
            SchemaRegistryRuleDirection.Write,
            SchemaRuleKind.Condition);

        await Assert.That(passing.Span.SequenceEqual(payload)).IsTrue();
        await Assert.That(() => Apply(
                "total < 10",
                payload,
                SchemaRegistryRuleDirection.Write,
                SchemaRuleKind.Condition))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("evaluated to false");
        await Assert.That(() => Apply(
                "total",
                payload,
                SchemaRegistryRuleDirection.Write,
                SchemaRuleKind.Condition))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("must evaluate to a boolean");
    }

    [Test]
    public async Task TransformRule_UndefinedResult_FailsPredictably()
    {
        await Assert.That(() => Apply(
                "missing",
                """{"present":1}"""u8.ToArray(),
                SchemaRegistryRuleDirection.Write))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("evaluated to undefined");
    }

    [Test]
    public async Task InvalidExpression_MalformedPayload_AndBinaryFormat_FailWithoutPayloadLeak()
    {
        const string secret = "do-not-leak-this-payload";
        var malformed = Encoding.UTF8.GetBytes("{\"secret\":\"" + secret + "\"");

        var invalidExpression = await Assert.That(() => Apply(
                "foo[",
                "{}"u8.ToArray(),
                SchemaRegistryRuleDirection.Write))
            .Throws<SchemaRegistryRuleException>();
        await Assert.That(invalidExpression!.Message).Contains("expression is invalid");

        var invalidPayload = await Assert.That(() => Apply(
                "$",
                malformed,
                SchemaRegistryRuleDirection.Write))
            .Throws<SchemaRegistryRuleException>();
        await Assert.That(invalidPayload!.Message).Contains("payload is not valid JSON");
        await Assert.That(invalidPayload.Message).DoesNotContain(secret);

        var unsupported = await Assert.That(() => Apply(
                "$",
                "{}"u8.ToArray(),
                SchemaRegistryRuleDirection.Write,
                payloadFormat: SchemaRegistryPayloadFormat.Avro))
            .Throws<SchemaRegistryRuleException>();
        await Assert.That(unsupported!.Message).Contains("requires a JSON payload");
    }

    [Test]
    public async Task TransformRule_OversizedOutputBuffer_IsNotRetained()
    {
        const int maxRetainedOutputBufferSize = 1024 * 1024;
        var value = new string('x', maxRetainedOutputBufferSize + 1);
        var payload = Encoding.UTF8.GetBytes($$"""{"value":"{{value}}"}""");

        var output = Apply("$", payload, SchemaRegistryRuleDirection.Write);
        var retained = (byte[]?)typeof(JsonataSchemaRegistryRuleHandler)
            .GetField("t_outputBuffer", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null);

        await Assert.That(retained is null || retained.Length <= maxRetainedOutputBufferSize).IsTrue();
        GC.KeepAlive(output);
    }

    [Test]
    public async Task Handler_ConcurrentEvaluation_ReusesCompiledQuerySafely()
    {
        var handler = new JsonataSchemaRegistryRuleHandler();
        var schema = CreateSchema("$merge([$, {'double': value * 2}])");
        var executor = new SchemaRegistryRuleExecutor([handler]);
        var failures = new System.Collections.Concurrent.ConcurrentQueue<int>();

        Parallel.For(0, 128, value =>
        {
            var context = CreateContext(schema);
            var input = Encoding.UTF8.GetBytes($"{{\"value\":{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");
            var output = executor.TransformSerializedPayload(input, context);
            using var document = JsonDocument.Parse(output);
            if (document.RootElement.GetProperty("double").GetInt32() != value * 2)
                failures.Enqueue(value);
        });

        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task MigrationRunner_JsonataUpgrade_TransformsBeforeReaderDeserialization()
    {
        var v1 = new Schema { SchemaType = SchemaType.Json, SchemaString = "v1" };
        var v2 = CreateSchema(
            "$merge([$, {'fullName': first & ' ' & last}])",
            SchemaRuleMode.Upgrade,
            migration: true);
        var registry = new MigrationRegistryClient(v1, v2);
        var executor = new SchemaRegistryRuleExecutor([new JsonataSchemaRegistryRuleHandler()]);
        var runner = new SchemaRegistryMigrationRunner(registry, executor, TimeSpan.FromSeconds(1));

        var result = runner.Transform(
            """{"first":"Ada","last":"Lovelace"}"""u8.ToArray(),
            schemaId: 1,
            "orders-value",
            v1,
            SerializationContext,
            SchemaRegistryPayloadFormat.Json);

        using var document = JsonDocument.Parse(result.Payload);
        await Assert.That(document.RootElement.GetProperty("fullName").GetString()).IsEqualTo("Ada Lovelace");
        await Assert.That(result.ReaderSchema.Schema).IsSameReferenceAs(v2);
    }

    [Test]
    public async Task MigrationRunner_LatestReaderJsonataIdentity_KeepsWriterPayloadSchemaAndMemory()
    {
        var v1 = new Schema { SchemaType = SchemaType.Json, SchemaString = "v1" };
        var v2 = CreateSchema("$", SchemaRuleMode.Read);
        var registry = new MigrationRegistryClient(v1, v2);
        var executor = new SchemaRegistryRuleExecutor([new JsonataSchemaRegistryRuleHandler()]);
        var runner = new SchemaRegistryMigrationRunner(registry, executor, TimeSpan.FromSeconds(1));
        var payload = """{"id":7}"""u8.ToArray();

        var result = runner.Transform(
            payload,
            schemaId: 1,
            "orders-value",
            v1,
            SerializationContext,
            SchemaRegistryPayloadFormat.Json);

        await Assert.That(result.PayloadSchemaId).IsEqualTo(1);
        await Assert.That(result.Payload.Equals(payload.AsMemory())).IsTrue();
        await Assert.That(result.ReaderSchema.Schema).IsSameReferenceAs(v2);
    }

    private static ReadOnlyMemory<byte> Apply(
        string expression,
        byte[] payload,
        SchemaRegistryRuleDirection direction,
        SchemaRuleKind kind = SchemaRuleKind.Transform,
        SchemaRegistryPayloadFormat payloadFormat = SchemaRegistryPayloadFormat.Json)
    {
        var schema = CreateSchema(expression, kind: kind);
        var executor = new SchemaRegistryRuleExecutor([new JsonataSchemaRegistryRuleHandler()]);
        var context = CreateContext(schema, payloadFormat);
        return direction == SchemaRegistryRuleDirection.Write
            ? executor.TransformSerializedPayload(payload, context)
            : executor.TransformDeserializedPayload(payload, context);
    }

    private static Schema CreateSchema(
        string expression,
        SchemaRuleMode mode = SchemaRuleMode.WriteRead,
        SchemaRuleKind kind = SchemaRuleKind.Transform,
        bool migration = false)
    {
        var rule = new SchemaRule
        {
            Name = "jsonata-test",
            Kind = kind,
            Mode = mode,
            Type = JsonataSchemaRegistryRuleHandler.RuleType,
            Expr = expression
        };
        return new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            RuleSet = migration
                ? new SchemaRuleSet { MigrationRules = [rule] }
                : new SchemaRuleSet { DomainRules = [rule], HasFixedRuleCollections = true }
        };
    }

    private static SchemaRegistryRuleContext CreateContext(
        Schema schema,
        SchemaRegistryPayloadFormat payloadFormat = SchemaRegistryPayloadFormat.Json) =>
        new()
        {
            Topic = "orders",
            Component = SerializationComponent.Value,
            SchemaId = 1,
            Subject = "orders-value",
            Schema = schema,
            PayloadFormat = payloadFormat
        };

    private sealed class MigrationRegistryClient(Schema writer, Schema reader) : ISchemaRegistryClient
    {
        private readonly RegisteredSchema _writer = new()
        {
            Id = 1,
            Subject = "orders-value",
            Version = 1,
            Schema = writer
        };
        private readonly RegisteredSchema _reader = new()
        {
            Id = 2,
            Subject = "orders-value",
            Version = 2,
            Schema = reader
        };

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == 1 ? _writer.Schema : _reader.Schema);

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default) => Task.FromResult(_reader);

        public Task<RegisteredSchema> LookupSchemaAsync(
            string subject,
            Schema schema,
            bool ignoreDeletedSchemas = true,
            bool normalize = false,
            CancellationToken cancellationToken = default) => Task.FromResult(_writer);

        public Task<int> RegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<int>> GetVersionsAsync(
            string subject,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> IsCompatibleAsync(
            string subject,
            Schema schema,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(
            string subject,
            bool permanent = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
