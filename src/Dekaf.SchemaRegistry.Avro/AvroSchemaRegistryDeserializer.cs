using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Avro.Generic;
using Avro.IO;
using Avro.Specific;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;

namespace Dekaf.SchemaRegistry.Avro;

/// <summary>
/// Avro deserializer that integrates with Confluent Schema Registry.
/// Handles the wire format: [magic byte (0x00)] [4-byte schema ID] [Avro binary payload].
/// </summary>
/// <typeparam name="T">The type to deserialize. Must be either an Avro ISpecificRecord or GenericRecord.</typeparam>
public sealed class AvroSchemaRegistryDeserializer<T> : IDeserializer<T>, IAsyncDisposable
{
    private const byte MagicByte = 0x00;

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly AvroDeserializerConfig _config;
    private readonly bool _ownsClient;
    private readonly ConcurrentDictionary<int, AvroSchema> _schemaCache = new();
    private readonly AvroSchema? _readerSchema;

    /// <summary>
    /// Creates a new Avro Schema Registry deserializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="config">Optional deserializer configuration.</param>
    /// <param name="ownsClient">Whether this deserializer owns the client and should dispose it.</param>
    public AvroSchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        AvroDeserializerConfig? config = null,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _config = config ?? new AvroDeserializerConfig();
        _ownsClient = ownsClient;

        // Parse custom reader schema if provided, otherwise derive from type
        _readerSchema = GetReaderSchema();
    }

    /// <summary>
    /// Deserializes data from the input buffer using Avro binary decoding
    /// with Schema Registry wire format.
    /// </summary>
    public T Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        if (data.Length < 5)
            throw new InvalidOperationException("Message too short to contain Schema Registry wire format");

        // Read wire format header
        Span<byte> header = stackalloc byte[5];
        data.Slice(0, 5).CopyTo(header);

        if (header[0] != MagicByte)
            throw new InvalidOperationException($"Unknown magic byte: {header[0]}. Expected Schema Registry format (0x00).");

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(header.Slice(1, 4));

        // Get writer schema from registry (cached)
        var writerSchema = GetWriterSchema(schemaId);

        // Extract Avro payload using pooled buffer to avoid allocation
        var payloadLength = (int)(data.Length - 5);
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(payloadLength);
        try
        {
            data.Slice(5).CopyTo(rentedBuffer);

            using var memoryStream = new PooledMemoryStream(rentedBuffer, payloadLength);
            var decoder = new BinaryDecoder(memoryStream);

            return ReadAvroValue(writerSchema, decoder);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    private T ReadAvroValue(AvroSchema writerSchema, BinaryDecoder decoder)
    {
        var readerSchema = _readerSchema ?? writerSchema;

        if (typeof(T) == typeof(GenericRecord))
        {
            var reader = new GenericDatumReader<GenericRecord>(writerSchema, readerSchema);
            var result = reader.Read(default!, decoder);
            return (T)(object)result;
        }

        if (typeof(ISpecificRecord).IsAssignableFrom(typeof(T)))
        {
            var reader = new SpecificDatumReader<T>(writerSchema, readerSchema);
            return reader.Read(default!, decoder);
        }

        throw new InvalidOperationException(
            $"Type {typeof(T)} is not supported. Must be ISpecificRecord or GenericRecord.");
    }

    private AvroSchema GetWriterSchema(int schemaId)
    {
        if (_schemaCache.TryGetValue(schemaId, out var cachedSchema))
            return cachedSchema;

        var registrySchema = _schemaRegistry.GetSchemaAsync(schemaId)
            .ConfigureAwait(false).GetAwaiter().GetResult();

        if (registrySchema.SchemaType != SchemaType.Avro)
            throw new InvalidOperationException(
                $"Schema with ID {schemaId} is not an Avro schema. Type: {registrySchema.SchemaType}");

        var avroSchema = AvroSchema.Parse(registrySchema.SchemaString);
        _schemaCache.TryAdd(schemaId, avroSchema);

        return avroSchema;
    }

    private AvroSchema? GetReaderSchema()
    {
        // If custom reader schema is provided, parse and use it
        if (!string.IsNullOrEmpty(_config.ReaderSchema))
            return AvroSchema.Parse(_config.ReaderSchema);

        // For specific records, try to get schema from type
        if (!typeof(ISpecificRecord).IsAssignableFrom(typeof(T)))
            return null;

        // Avro generated classes have a static _SCHEMA field
        var schemaField = typeof(T).GetField("_SCHEMA",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        if (schemaField?.GetValue(null) is AvroSchema schema)
            return schema;

        return null;
    }

    /// <summary>
    /// Disposes the deserializer and optionally the underlying Schema Registry client.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}
