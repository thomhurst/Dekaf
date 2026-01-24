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
/// <remarks>
/// <para>
/// This deserializer uses lazy caching for writer schemas. The first time a schema ID is
/// encountered, an async call to the Schema Registry is made. This call is wrapped in
/// <see cref="Lazy{T}"/> to ensure thread-safety: only one thread performs the fetch while
/// others wait for the result.
/// </para>
/// <para>
/// After the first fetch, subsequent deserialization calls for the same schema ID use the
/// cached schema without any blocking or async overhead.
/// </para>
/// <para>
/// For high-throughput scenarios where you know the schema IDs in advance, use
/// <see cref="WarmupAsync"/> to pre-warm the cache before starting consumption. This ensures
/// the synchronous <see cref="Deserialize"/> method never blocks on Schema Registry calls.
/// </para>
/// </remarks>
/// <typeparam name="T">The type to deserialize. Must be either an Avro ISpecificRecord or GenericRecord.</typeparam>
public sealed class AvroSchemaRegistryDeserializer<T> : IDeserializer<T>, IAsyncDisposable
{
    private const byte MagicByte = 0x00;

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly AvroDeserializerConfig _config;
    private readonly bool _ownsClient;
    private readonly ConcurrentDictionary<int, Lazy<Task<AvroSchema>>> _schemaCache = new();
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
    /// Pre-warms the schema cache for a specific schema ID.
    /// </summary>
    /// <remarks>
    /// Call this method before starting consumption if you know the schema IDs in advance.
    /// This ensures that the synchronous <see cref="Deserialize"/> method never blocks on
    /// Schema Registry calls for the specified schema ID.
    /// </remarks>
    /// <param name="schemaId">The schema ID to warm up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed Avro schema.</returns>
    public async Task<AvroSchema> WarmupAsync(int schemaId, CancellationToken cancellationToken = default)
    {
        return await GetOrFetchWriterSchemaAsync(schemaId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deserializes data from the input buffer using Avro binary decoding
    /// with Schema Registry wire format.
    /// </summary>
    /// <remarks>
    /// This method uses cached schemas when available. If the schema is not yet cached,
    /// the first call will block while fetching from the Schema Registry. Subsequent calls
    /// for the same schema ID will use the cached value without blocking.
    /// For best performance, use <see cref="WarmupAsync"/> before starting consumption.
    /// </remarks>
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

        // Get writer schema from registry (cached with lazy initialization)
        var writerSchema = GetWriterSchemaCached(schemaId);

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

    private AvroSchema GetWriterSchemaCached(int schemaId)
    {
        // Use Lazy<Task<T>> pattern for thread-safe lazy initialization.
        // GetOrAdd ensures only one Lazy instance is created per schema ID.
        // The Lazy ensures only one thread executes the factory (fetches the schema).
        var lazyTask = _schemaCache.GetOrAdd(
            schemaId,
            id => new Lazy<Task<AvroSchema>>(() => FetchWriterSchemaAsync(id)));

        // If the task is already completed, this returns immediately without blocking.
        // If this is the first access, it will block waiting for the schema fetch.
        // The Lazy ensures that only ONE thread ever blocks for a given schema ID.
        var task = lazyTask.Value;

        if (task.IsCompletedSuccessfully)
        {
            // Fast path: schema already cached, no blocking
            return task.Result;
        }

        // Slow path: first fetch or concurrent access during first fetch.
        // This blocks the calling thread, but only happens once per schema ID.
        return task.ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private async Task<AvroSchema> GetOrFetchWriterSchemaAsync(int schemaId, CancellationToken cancellationToken = default)
    {
        var lazyTask = _schemaCache.GetOrAdd(
            schemaId,
            id => new Lazy<Task<AvroSchema>>(() => FetchWriterSchemaAsync(id, cancellationToken)));

        return await lazyTask.Value.ConfigureAwait(false);
    }

    private async Task<AvroSchema> FetchWriterSchemaAsync(int schemaId, CancellationToken cancellationToken = default)
    {
        var registrySchema = await _schemaRegistry.GetSchemaAsync(schemaId, cancellationToken)
            .ConfigureAwait(false);

        if (registrySchema.SchemaType != SchemaType.Avro)
            throw new InvalidOperationException(
                $"Schema with ID {schemaId} is not an Avro schema. Type: {registrySchema.SchemaType}");

        return AvroSchema.Parse(registrySchema.SchemaString);
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
