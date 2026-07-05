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
            ]
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
              "schema": "{}",
              "schemaType": "JSON",
              "references": [
                {
                  "name": "shared",
                  "subject": "shared-value",
                  "version": 2
                }
              ]
            }
            """u8;
        var subjectResponse = JsonSerializer.Deserialize(
            subjectResponseJson,
            SchemaRegistryJsonContext.Default.GetSubjectVersionResponse);

        var compatibilityJson = JsonSerializer.SerializeToUtf8Bytes(
            new CompatibilityResponse { IsCompatible = true },
            SchemaRegistryJsonContext.Default.CompatibilityResponse);
        var errorJson = JsonSerializer.SerializeToUtf8Bytes(
            new ErrorResponse { ErrorCode = 40401, Message = "missing" },
            SchemaRegistryJsonContext.Default.ErrorResponse);
        using var compatibilityDocument = JsonDocument.Parse(compatibilityJson);
        using var errorDocument = JsonDocument.Parse(errorJson);

        await Assert.That(roundTrippedRequest!.SchemaType).IsEqualTo("JSON");
        await Assert.That(roundTrippedRequest.References!.Count).IsEqualTo(1);
        await Assert.That(subjectResponse!.Subject).IsEqualTo("orders-value");
        await Assert.That(subjectResponse.References!.Count).IsEqualTo(1);
        await Assert.That(compatibilityDocument.RootElement.TryGetProperty("is_compatible", out _)).IsTrue();
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
