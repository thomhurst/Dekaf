using System.Runtime.CompilerServices;
using Avro.IO;
using AvroSchema = Avro.Schema;

namespace Dekaf.SchemaRegistry.Avro;

internal readonly record struct AvroSchemaPair(AvroSchema WriterSchema, AvroSchema ReaderSchema);

internal sealed class AvroSchemaReferenceComparer : IEqualityComparer<AvroSchema>
{
    internal static readonly AvroSchemaReferenceComparer Instance = new();

    private AvroSchemaReferenceComparer() { }

    public bool Equals(AvroSchema? x, AvroSchema? y) => ReferenceEquals(x, y);

    public int GetHashCode(AvroSchema obj) => RuntimeHelpers.GetHashCode(obj);
}

internal sealed class AvroSchemaPairReferenceComparer : IEqualityComparer<AvroSchemaPair>
{
    internal static readonly AvroSchemaPairReferenceComparer Instance = new();

    private AvroSchemaPairReferenceComparer() { }

    public bool Equals(AvroSchemaPair x, AvroSchemaPair y) =>
        ReferenceEquals(x.WriterSchema, y.WriterSchema) &&
        ReferenceEquals(x.ReaderSchema, y.ReaderSchema);

    public int GetHashCode(AvroSchemaPair obj) =>
        HashCode.Combine(
            RuntimeHelpers.GetHashCode(obj.WriterSchema),
            RuntimeHelpers.GetHashCode(obj.ReaderSchema));
}

internal sealed class AvroSerializationThreadState
{
    internal AvroSerializationThreadState()
    {
        BufferedStream = new PooledMemoryStream([]);
        BufferedEncoder = new BinaryEncoder(BufferedStream);
        DirectStream = new FixedMemoryStream();
        DirectEncoder = new BinaryEncoder(DirectStream);
    }

    internal PooledMemoryStream BufferedStream { get; }
    internal BinaryEncoder BufferedEncoder { get; }
    internal FixedMemoryStream DirectStream { get; }
    internal BinaryEncoder DirectEncoder { get; }
    internal int PayloadSizeHint { get; set; } = 1024;
}

internal sealed class AvroDeserializationThreadState
{
    internal AvroDeserializationThreadState()
    {
        Stream = new PooledMemoryStream([]);
        Decoder = new BinaryDecoder(Stream);
    }

    internal PooledMemoryStream Stream { get; }
    internal BinaryDecoder Decoder { get; }
}

internal static class AvroCodecThreadStateCache
{
    [ThreadStatic]
    internal static AvroSerializationThreadState? Serialization;

    [ThreadStatic]
    internal static AvroDeserializationThreadState? Deserialization;
}
