using System.Net;
using System.Text.Json;
using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaRegistryDataContractTests
{
    [Test]
    public async Task RegisterSchemaAsync_SendsMetadataAndRuleSet()
    {
        var handler = new CapturingSchemaRegistryHandler("""{ "id": 7 }""");
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "http://schema-registry.local"
        }, handler);

        var schema = CreateDataContractSchema();

        var id = await client.RegisterSchemaAsync("orders-value", schema);

        await Assert.That(id).IsEqualTo(7);
        using var document = JsonDocument.Parse(handler.LastRequestBody!);
        var root = document.RootElement;
        var metadata = root.GetProperty("metadata");
        var rule = root.GetProperty("ruleSet").GetProperty("encodingRules")[0];

        await Assert.That(metadata.GetProperty("tags").GetProperty("$.name")[0].GetString()).IsEqualTo("PII");
        await Assert.That(metadata.GetProperty("properties").GetProperty("owner").GetString()).IsEqualTo("payments");
        await Assert.That(rule.GetProperty("kind").GetString()).IsEqualTo("TRANSFORM");
        await Assert.That(rule.GetProperty("mode").GetString()).IsEqualTo("WRITEREAD");
        await Assert.That(rule.GetProperty("params").GetProperty("encrypt.kek.name").GetString())
            .IsEqualTo("payments-kek");
    }

    [Test]
    public async Task GetSchemaBySubjectAsync_MapsMetadataAndRuleSet()
    {
        var handler = new CapturingSchemaRegistryHandler("""
            {
              "subject": "orders-value",
              "version": 3,
              "id": 42,
              "schema": "{\"type\":\"object\"}",
              "schemaType": "JSON",
              "metadata": {
                "tags": { "$.name": [ "PII" ] },
                "properties": { "owner": "payments" },
                "sensitive": [ "owner" ]
              },
              "ruleSet": {
                "encodingRules": [
                  {
                    "name": "encryptPii",
                    "doc": "Encrypt PII fields",
                    "kind": "TRANSFORM",
                    "mode": "WRITEREAD",
                    "type": "ENCRYPT",
                    "tags": [ "PII" ],
                    "params": { "encrypt.kek.name": "payments-kek" },
                    "expr": "message.name",
                    "onSuccess": "NONE",
                    "onFailure": "ERROR",
                    "disabled": false
                  }
                ]
              }
            }
            """);
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "http://schema-registry.local"
        }, handler);

        var registered = await client.GetSchemaBySubjectAsync("orders-value");
        var metadata = registered.Schema.Metadata!;
        var ruleSet = registered.Schema.RuleSet!;
        var rule = ruleSet.EncodingRules![0];

        await Assert.That(registered.Id).IsEqualTo(42);
        await Assert.That(metadata.Tags!["$.name"]).Contains("PII");
        await Assert.That(metadata.Properties!["owner"]).IsEqualTo("payments");
        await Assert.That(metadata.Sensitive!).Contains("owner");

        await Assert.That(rule.Name).IsEqualTo("encryptPii");
        await Assert.That(rule.Kind).IsEqualTo(SchemaRuleKind.Transform);
        await Assert.That(rule.Mode).IsEqualTo(SchemaRuleMode.WriteRead);
        await Assert.That(rule.Type).IsEqualTo("ENCRYPT");
        await Assert.That(rule.Tags!).Contains("PII");
        await Assert.That(rule.Parameters!["encrypt.kek.name"]).IsEqualTo("payments-kek");
        await Assert.That(rule.Expr).IsEqualTo("message.name");
    }

    private static Schema CreateDataContractSchema() => new()
    {
        SchemaType = SchemaType.Json,
        SchemaString = """{ "type": "object" }""",
        Metadata = new SchemaMetadata
        {
            Tags = new Dictionary<string, IReadOnlySet<string>>
            {
                ["$.name"] = new HashSet<string>(StringComparer.Ordinal) { "PII" }
            },
            Properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["owner"] = "payments"
            },
            Sensitive = new HashSet<string>(StringComparer.Ordinal) { "owner" }
        },
        RuleSet = new SchemaRuleSet
        {
            EncodingRules =
            [
                new SchemaRule
                {
                    Name = "encryptPii",
                    Doc = "Encrypt PII fields",
                    Kind = SchemaRuleKind.Transform,
                    Mode = SchemaRuleMode.WriteRead,
                    Type = "ENCRYPT",
                    Tags = new HashSet<string>(StringComparer.Ordinal) { "PII" },
                    Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["encrypt.kek.name"] = "payments-kek"
                    }
                }
            ]
        }
    };

    private sealed class CapturingSchemaRegistryHandler(string responseContent) : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent)
            };
        }
    }
}
