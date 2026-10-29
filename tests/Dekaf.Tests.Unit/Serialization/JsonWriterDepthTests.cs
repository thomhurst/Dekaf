using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;
using Dekaf.Tests.Unit.SchemaRegistry;

namespace Dekaf.Tests.Unit.Serialization;

public sealed class JsonWriterDepthTests
{
    [Test]
    [Arguments(0, false)]
    [Arguments(0, true)]
    [Arguments(4, false)]
    [Arguments(4, true)]
    [Arguments(96, false)]
    [Arguments(96, true)]
    public async Task Serialize_DepthBoundary_MatchesSystemTextJson(int maxDepth, bool useTypeInfo)
    {
        var options = new JsonSerializerOptions { MaxDepth = maxDepth };
        var typeInfo = new JsonWriterDepthContext(options).NestedJsonPayload;
        var ordinary = useTypeInfo
            ? new Dekaf.Serialization.Json.JsonSerializer<NestedJsonPayload>(typeInfo)
            : new Dekaf.Serialization.Json.JsonSerializer<NestedJsonPayload>(options);
        using var registry = new MockSchemaRegistryClient();
        await using var schema = useTypeInfo
            ? new JsonSchemaRegistrySerializer<NestedJsonPayload>(registry, "{\"type\":\"array\"}", typeInfo)
            : new JsonSchemaRegistrySerializer<NestedJsonPayload>(registry, "{\"type\":\"array\"}", options);
        var prepared = (IAsyncSerializerPreparationAdmission<NestedJsonPayload>)schema;
        var context = new SerializationContext { Topic = "depth", Component = SerializationComponent.Value };
        var limit = maxDepth == 0 ? 64 : maxDepth;
        var admission = await prepared.PrepareForSerializationAsync(new NestedJsonPayload(limit), context);
        var buffer = new ArrayBufferWriter<byte>();

        // Retry the boundary after a failure to cover cached writer recovery as well.
        foreach (var depth in new[] { limit, limit + 1, limit })
        {
            var payload = new NestedJsonPayload(depth);
            string? expected = null;
            Exception? expectedError = null;
            try
            {
                expected = useTypeInfo
                    ? JsonSerializer.Serialize(payload, typeInfo)
                    : JsonSerializer.Serialize(payload, options);
            }
            catch (Exception exception)
            {
                expectedError = exception;
            }

            await Assert.That(expectedError is null).IsEqualTo(depth == limit);
            for (var path = 0; path < 3; path++)
            {
                buffer.ResetWrittenCount();
                Exception? actualError = null;
                try
                {
                    switch (path)
                    {
                        case 0:
                            ordinary.Serialize(payload, ref buffer, context);
                            break;
                        case 1:
                            schema.Serialize(payload, ref buffer, context);
                            break;
                        case 2:
                            prepared.SerializePrepared(payload, ref buffer, context, in admission);
                            break;
                    }
                }
                catch (Exception exception)
                {
                    actualError = exception;
                }

                await Assert.That(actualError?.GetType()).IsEqualTo(expectedError?.GetType());
                if (expectedError is null)
                {
                    var offset = path == 0 ? 0 : 5;
                    await Assert.That(System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan[offset..]))
                        .IsEqualTo(expected);
                }
            }
        }
    }
}

[JsonConverter(typeof(NestedJsonPayloadConverter))]
internal sealed record NestedJsonPayload(int Depth);

internal sealed class NestedJsonPayloadConverter : JsonConverter<NestedJsonPayload>
{
    public override NestedJsonPayload Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException();

    public override void Write(Utf8JsonWriter writer, NestedJsonPayload value, JsonSerializerOptions options)
    {
        for (var i = 0; i < value.Depth; i++)
            writer.WriteStartArray();
        writer.WriteNumberValue(0);
        for (var i = 0; i < value.Depth; i++)
            writer.WriteEndArray();
    }
}

[JsonSerializable(typeof(NestedJsonPayload))]
internal sealed partial class JsonWriterDepthContext : JsonSerializerContext;
