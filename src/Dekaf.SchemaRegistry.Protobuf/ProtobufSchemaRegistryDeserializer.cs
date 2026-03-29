using System.Buffers.Binary;
using Dekaf.Serialization;
using Google.Protobuf;

namespace Dekaf.SchemaRegistry.Protobuf;

/// <summary>
/// Protobuf deserializer that integrates with Confluent Schema Registry.
/// Wire format: [magic byte (0x00)] [schema ID (4 bytes)] [varint array indexes] [protobuf binary]
/// </summary>
/// <remarks>
/// <para>
/// This deserializer fetches the schema from Schema Registry for validation on first access.
/// Schemas are cached internally by the Schema Registry client after first fetch.
/// </para>
/// <para>
/// The blocking call includes a timeout to prevent indefinite hangs.
/// </para>
/// </remarks>
/// <typeparam name="T">The Protobuf message type to deserialize.</typeparam>
public sealed class ProtobufSchemaRegistryDeserializer<T> : IDeserializer<T>, IAsyncDisposable
    where T : IMessage<T>, IBufferMessage, new()
{
    private const byte MagicByte = 0x00;
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);

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
        _parser = new MessageParser<T>(() => new T());
    }

    /// <inheritdoc />
    public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        var span = data.Span;

        if (span.Length < 5)
            throw new InvalidOperationException("Message too short to contain Schema Registry wire format");

        // Verify magic byte
        if (span[0] != MagicByte)
            throw new InvalidOperationException($"Unknown magic byte: {span[0]}. Expected Schema Registry format (0x00).");

        // Read schema ID (big-endian)
        var schemaId = BinaryPrimitives.ReadInt32BigEndian(span.Slice(1, 4));

        // Optionally validate the schema exists (with timeout to prevent indefinite hang)
        if (!_config.SkipSchemaValidation)
        {
            var schema = _schemaRegistry.GetSchemaAsync(schemaId)
                .WaitAsync(SchemaRegistryTimeout)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
            if (schema.SchemaType != SchemaType.Protobuf)
                throw new InvalidOperationException($"Schema {schemaId} is not a Protobuf schema (type: {schema.SchemaType})");
        }

        // Read the message indexes (varints)
        var payload = span.Slice(5);
        var (indexCount, bytesRead) = ReadVarint(payload);

        // Skip past the index array
        for (var i = 0; i < indexCount; i++)
        {
            var (_, indexBytesRead) = ReadVarint(payload.Slice(bytesRead));
            bytesRead += indexBytesRead;
        }

        // The rest is the protobuf message
        var protobufData = payload.Slice(bytesRead);

        // Parse directly from span — zero allocation (Google.Protobuf 3.21+).
        // IBufferMessage constraint is enforced at compile time.
        return _parser.ParseFrom(protobufData);
    }

    private static (int value, int bytesRead) ReadVarint(ReadOnlySpan<byte> data)
    {
        var value = 0;
        var shift = 0;
        var bytesRead = 0;

        foreach (var b in data)
        {
            bytesRead++;
            value |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return (value, bytesRead);
            shift += 7;

            if (shift >= 35)
                throw new InvalidOperationException("Varint is too long");
        }

        throw new InvalidOperationException("Varint is truncated");
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}
