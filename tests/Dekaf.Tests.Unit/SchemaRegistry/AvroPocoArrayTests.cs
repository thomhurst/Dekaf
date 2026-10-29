using System.Buffers;
using System.Text.Json;
using Dekaf.SchemaRegistry.Avro.Poco;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class AvroPocoArrayTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task GeneratedCodec_RoundTripsJaggedArrays(bool empty)
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoJaggedArrays.CreateAvroSerializer(registry);
        await using var deserializer = PocoJaggedArrays.CreateAvroDeserializer(registry);
        var expected = empty ? new PocoJaggedArrays() : new PocoJaggedArrays
        {
            Bytes = [[1, 2], [], [255]],
            Values = [[1, -2], [], [3]],
            Deep = [[[4, 5], []], [], [[6]]],
            Lists = [[[7, 8], []], []],
            Map = new Dictionary<string, byte[][]> { ["data"] = [[9, 10], []], ["empty"] = [] },
            ArrayOfLists = [[new[] { 11, 12 }, Array.Empty<int>()], []],
            NullableItems = [null, [], [13]]
        };
        var context = new SerializationContext { Topic = "poco-jagged", Component = SerializationComponent.Value };
        var destination = new ArrayBufferWriter<byte>();
        await serializer.WarmupAsync(context.Topic);

        serializer.Serialize(expected, ref destination, context);
        var actual = deserializer.Deserialize(destination.WrittenMemory, context);

        await Assert.That(JsonSerializer.Serialize(actual)).IsEqualTo(JsonSerializer.Serialize(expected));
    }
}

[AvroRecord(Namespace = "Dekaf.Tests")]
internal sealed partial class PocoJaggedArrays
{
    public byte[][] Bytes { get; set; } = [];
    public int[][] Values { get; set; } = [];
    public int[][][] Deep { get; set; } = [];
    public List<int[][]> Lists { get; set; } = [];
    public Dictionary<string, byte[][]> Map { get; set; } = [];
    public List<int[]>[] ArrayOfLists { get; set; } = [];
    public byte[]?[] NullableItems { get; set; } = [];
}
