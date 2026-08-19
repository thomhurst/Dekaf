using System.Buffers;
using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaRegistryJsonAotTests
{
    private const string JsonSchema = """
        {
          "type": "object",
          "properties": {
            "id": { "type": "integer" },
            "name": { "type": "string" }
          }
        }
        """;

    [Test]
    public async Task SchemaRegistryJsonContext_SerializesAndDeserializesRegistryDtos()
    {
        var request = new RegisterSchemaRequest
        {
            Schema = JsonSchema,
            SchemaType = "JSON",
            References =
            [
                new SchemaReferenceDto
                {
                    Name = "shared",
                    Subject = "shared-value",
                    Version = 2
                }
            ],
            Metadata = new SchemaMetadataDto
            {
                Tags = new Dictionary<string, HashSet<string>>
                {
                    ["$.name"] = ["PII"]
                },
                Properties = new Dictionary<string, string>
                {
                    ["owner"] = "payments"
                },
                Sensitive = ["owner"]
            },
            RuleSet = new SchemaRuleSetDto
            {
                EncodingRules =
                [
                    new SchemaRuleDto
                    {
                        Name = "encryptPii",
                        Kind = "TRANSFORM",
                        Mode = "WRITEREAD",
                        Type = "ENCRYPT",
                        Tags = ["PII"],
                        Params = new Dictionary<string, string>
                        {
                            ["encrypt.kek.name"] = "payments-kek"
                        }
                    }
                ]
            }
        };

        var requestJson = JsonSerializer.SerializeToUtf8Bytes(
            request,
            SchemaRegistryJsonContext.Default.RegisterSchemaRequest);
        var roundTrippedRequest = JsonSerializer.Deserialize(
            requestJson,
            SchemaRegistryJsonContext.Default.RegisterSchemaRequest);

        var subjectResponseJson = """
            {
              "subject": "orders-value",
              "version": 3,
              "id": 42,
              "guid": "01234567-89ab-cdef-0123-456789abcdef",
              "schema": "{}",
              "schemaType": "JSON",
              "references": [
                {
                  "name": "shared",
                  "subject": "shared-value",
                  "version": 2
                }
              ],
              "metadata": {
                "tags": { "$.name": [ "PII" ] },
                "properties": { "owner": "payments" },
                "sensitive": [ "owner" ]
              },
              "ruleSet": {
                "encodingRules": [
                  {
                    "name": "encryptPii",
                    "kind": "TRANSFORM",
                    "mode": "WRITEREAD",
                    "type": "ENCRYPT",
                    "tags": [ "PII" ],
                    "params": { "encrypt.kek.name": "payments-kek" }
                  }
                ]
              }
            }
            """u8;
        var subjectResponse = JsonSerializer.Deserialize(
            subjectResponseJson,
            SchemaRegistryJsonContext.Default.GetSubjectVersionResponse);

        var compatibilityJson = JsonSerializer.SerializeToUtf8Bytes(
            new CompatibilityResponse { IsCompatible = true },
            SchemaRegistryJsonContext.Default.CompatibilityResponse);
        var updateCompatibilityJson = JsonSerializer.SerializeToUtf8Bytes(
            new UpdateCompatibilityRequest { Compatibility = "FULL_TRANSITIVE" },
            SchemaRegistryJsonContext.Default.UpdateCompatibilityRequest);
        var getCompatibility = JsonSerializer.Deserialize(
            """{ "compatibilityLevel": "BACKWARD" }""",
            SchemaRegistryJsonContext.Default.GetCompatibilityResponse);
        var updateCompatibility = JsonSerializer.Deserialize(
            """{ "compatibility": "FORWARD" }""",
            SchemaRegistryJsonContext.Default.UpdateCompatibilityResponse);
        var associationJson = JsonSerializer.SerializeToUtf8Bytes(
            new AssociationCreateOrUpdateRequestDto
            {
                ResourceName = "orders",
                ResourceNamespace = "lkc-123",
                ResourceId = "lkc-123:orders",
                ResourceType = "topic",
                Associations =
                [
                    new AssociationCreateOrUpdateInfoDto
                    {
                        Subject = "orders-value",
                        AssociationType = "value",
                        Lifecycle = "STRONG"
                    }
                ]
            },
            SchemaRegistryJsonContext.Default.AssociationCreateOrUpdateRequestDto);
        var associationResponse = JsonSerializer.Deserialize(
            """
            {
              "resourceName": "orders",
              "resourceNamespace": "lkc-123",
              "resourceId": "lkc-123:orders",
              "resourceType": "topic",
              "associations": []
            }
            """,
            SchemaRegistryJsonContext.Default.AssociationResponseDto);
        var errorJson = JsonSerializer.SerializeToUtf8Bytes(
            new ErrorResponse { ErrorCode = 40401, Message = "missing" },
            SchemaRegistryJsonContext.Default.ErrorResponse);
        using var compatibilityDocument = JsonDocument.Parse(compatibilityJson);
        using var updateCompatibilityDocument = JsonDocument.Parse(updateCompatibilityJson);
        using var errorDocument = JsonDocument.Parse(errorJson);

        await Assert.That(roundTrippedRequest!.SchemaType).IsEqualTo("JSON");
        await Assert.That(roundTrippedRequest.References!.Count).IsEqualTo(1);
        await Assert.That(roundTrippedRequest.Metadata!.Tags!["$.name"]).Contains("PII");
        await Assert.That(roundTrippedRequest.RuleSet!.EncodingRules![0].Params!["encrypt.kek.name"]).IsEqualTo("payments-kek");
        await Assert.That(subjectResponse!.Subject).IsEqualTo("orders-value");
        await Assert.That(subjectResponse.Guid).IsEqualTo("01234567-89ab-cdef-0123-456789abcdef");
        await Assert.That(subjectResponse.References!.Count).IsEqualTo(1);
        await Assert.That(subjectResponse.Metadata!.Properties!["owner"]).IsEqualTo("payments");
        await Assert.That(subjectResponse.RuleSet!.EncodingRules![0].Mode).IsEqualTo("WRITEREAD");
        await Assert.That(compatibilityDocument.RootElement.TryGetProperty("is_compatible", out _)).IsTrue();
        await Assert.That(updateCompatibilityDocument.RootElement.GetProperty("compatibility").GetString())
            .IsEqualTo("FULL_TRANSITIVE");
        await Assert.That(getCompatibility!.CompatibilityLevel).IsEqualTo("BACKWARD");
        await Assert.That(updateCompatibility!.Compatibility).IsEqualTo("FORWARD");
        using var associationDocument = JsonDocument.Parse(associationJson);
        await Assert.That(associationDocument.RootElement.GetProperty("associations")[0]
            .GetProperty("subject").GetString()).IsEqualTo("orders-value");
        await Assert.That(associationResponse!.ResourceId).IsEqualTo("lkc-123:orders");
        await Assert.That(errorDocument.RootElement.TryGetProperty("error_code", out _)).IsTrue();
    }

    [Test]
    public async Task JsonSchemaRegistrySerializer_RoundTripsWithJsonTypeInfo()
    {
        var registry = new MockSchemaRegistryClient();
        await using var serializer = new JsonSchemaRegistrySerializer<SchemaRegistryAotPayload>(
            registry,
            JsonSchema,
            SchemaRegistryAotJsonContext.Default.SchemaRegistryAotPayload);
        await using var deserializer = new JsonSchemaRegistryDeserializer<SchemaRegistryAotPayload>(
            registry,
            SchemaRegistryAotJsonContext.Default.SchemaRegistryAotPayload);
        var payload = new SchemaRegistryAotPayload(7, "test");
        var context = new SerializationContext
        {
            Topic = "orders",
            Component = SerializationComponent.Value
        };
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(payload, ref buffer, context);
        var result = deserializer.Deserialize(buffer.WrittenMemory, context);

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(buffer.WrittenSpan.Slice(1, 4));
        await Assert.That(buffer.WrittenSpan[0]).IsEqualTo((byte)0);
        await Assert.That(schemaId).IsGreaterThan(0);
        await Assert.That(result).IsEqualTo(payload);
    }
}

internal sealed record SchemaRegistryAotPayload(int Id, string Name);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SchemaRegistryAotPayload))]
internal sealed partial class SchemaRegistryAotJsonContext : JsonSerializerContext;
