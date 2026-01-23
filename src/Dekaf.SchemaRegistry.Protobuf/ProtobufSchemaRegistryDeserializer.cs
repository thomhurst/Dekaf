using System.Buffers;
using System.Buffers.Binary;
using Dekaf.Serialization;
using Google.Protobuf;

namespace Dekaf.SchemaRegistry.Protobuf;

/// <summary>
/// Protobuf deserializer that integrates with Confluent Schema Registry.
/// Wire format: [magic byte (0x00)] [schema ID (4 bytes)] [varint array indexes] [protobuf binary]
/// </summary>
/// <typeparam name="T">The Protobuf message type to deserialize.</typeparam>
public sealed class ProtobufSchemaRegistryDeserializer<T> : IDeserializer<T>, IAsyncDisposable
    where T : IMessage<T>, new()
{
    private const byte MagicByte = 0x00;

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly ProtobufDeserializerConfig _config;
    private readonly bool _ownsClient;
    private readonly MessageParser<T> _parser;

    /// <summary>
    /// Creates a new Protobuf Schema Registry deserializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="config">Optional deserializer configuration.</param>
    /// <param name="ownsClient">Whether this deserializer owns the client and should dispose it.</param>
    public ProtobufSchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        ProtobufDeserializerConfig? config = null,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _config = config ?? new ProtobufDeserializerConfig();
        _ownsClient = ownsClient;

        // Create the parser for the message type
        _parser = new MessageParser<T>(() => new T());
    }

    /// <inheritdoc />
    public T Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        if (data.Length < 5)
            throw new InvalidOperationException("Message too short to contain Schema Registry wire format");

        // Read the header into a contiguous span
        Span<byte> header = stackalloc byte[5];
        data.Slice(0, 5).CopyTo(header);

        // Verify magic byte
        if (header[0] != MagicByte)
            throw new InvalidOperationException($"Unknown magic byte: {header[0]}. Expected Schema Registry format (0x00).");

        // Read schema ID (big-endian)
        var schemaId = BinaryPrimitives.ReadInt32BigEndian(header.Slice(1, 4));

        // Optionally validate the schema exists
        if (!_config.SkipSchemaValidation)
        {
            var schema = _schemaRegistry.GetSchemaAsync(schemaId).GetAwaiter().GetResult();
            if (schema.SchemaType != SchemaType.Protobuf)
                throw new InvalidOperationException($"Schema {schemaId} is not a Protobuf schema (type: {schema.SchemaType})");
        }

        // Read the message indexes (varints)
        var payload = data.Slice(5);
        var (indexCount, bytesRead) = ReadVarint(payload);

        // Skip past the index array
        for (var i = 0; i < indexCount; i++)
        {
            var (_, indexBytesRead) = ReadVarint(payload.Slice(bytesRead));
            bytesRead += indexBytesRead;
        }

        // The rest is the protobuf message
        var protobufData = payload.Slice(bytesRead);

        // Parse the message using byte array for maximum compatibility
        // with both generated and hand-coded messages
        return _parser.ParseFrom(protobufData.ToArray());
    }

    private static (int value, int bytesRead) ReadVarint(ReadOnlySequence<byte> data)
    {
        var value = 0;
        var shift = 0;
        var bytesRead = 0;

        var reader = new SequenceReader<byte>(data);

        while (reader.TryRead(out var b))
        {
            bytesRead++;
            value |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                break;
            shift += 7;

            if (shift >= 35)
                throw new InvalidOperationException("Varint is too long");
        }

        return (value, bytesRead);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}
