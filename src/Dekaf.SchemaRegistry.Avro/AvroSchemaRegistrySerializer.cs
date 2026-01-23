using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using Avro.Generic;
using Avro.IO;
using Avro.Specific;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;
using RegistrySchema = Dekaf.SchemaRegistry.Schema;

namespace Dekaf.SchemaRegistry.Avro;

/// <summary>
/// Avro serializer that integrates with Confluent Schema Registry.
/// Handles the wire format: [magic byte (0x00)] [4-byte schema ID] [Avro binary payload].
/// </summary>
/// <typeparam name="T">The type to serialize. Must be either an Avro ISpecificRecord or GenericRecord.</typeparam>
public sealed class AvroSchemaRegistrySerializer<T> : ISerializer<T>, IAsyncDisposable
{
    private const byte MagicByte = 0x00;

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly AvroSerializerConfig _config;
    private readonly bool _ownsClient;
    private readonly ConcurrentDictionary<string, int> _schemaIdCache = new();
    private readonly AvroSchema? _writerSchema;

    /// <summary>
    /// Creates a new Avro Schema Registry serializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="config">Optional serializer configuration.</param>
    /// <param name="ownsClient">Whether this serializer owns the client and should dispose it.</param>
    public AvroSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        AvroSerializerConfig? config = null,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _config = config ?? new AvroSerializerConfig();
        _ownsClient = ownsClient;

        // Try to get schema from type T if it's a specific record
        _writerSchema = GetSchemaFromType();
    }

    /// <summary>
    /// Serializes the value to the output buffer using Avro binary encoding
    /// with Schema Registry wire format.
    /// </summary>
    public void Serialize(T value, IBufferWriter<byte> destination, SerializationContext context)
    {
        ArgumentNullException.ThrowIfNull(value);

        var subject = GetSubjectName(context.Topic, context.Component == SerializationComponent.Key);
        var schemaId = GetSchemaIdSync(subject, value);

        // Serialize Avro payload to a temporary buffer
        using var memoryStream = new MemoryStream();
        var encoder = new BinaryEncoder(memoryStream);

        WriteAvroValue(value, encoder);
        encoder.Flush();

        var avroPayload = memoryStream.ToArray();

        // Write wire format: [0x00] [schema ID] [Avro payload]
        var totalSize = 1 + 4 + avroPayload.Length;
        var span = destination.GetSpan(totalSize);

        span[0] = MagicByte;
        BinaryPrimitives.WriteInt32BigEndian(span.Slice(1, 4), schemaId);
        avroPayload.AsSpan().CopyTo(span.Slice(5));

        destination.Advance(totalSize);
    }

    private static void WriteAvroValue(T value, BinaryEncoder encoder)
    {
        switch (value)
        {
            case ISpecificRecord specificRecord:
                var specificWriter = new SpecificDefaultWriter(specificRecord.Schema);
                specificWriter.Write(specificRecord.Schema, specificRecord, encoder);
                break;

            case GenericRecord genericRecord:
                var genericWriter = new GenericDatumWriter<GenericRecord>(genericRecord.Schema);
                genericWriter.Write(genericRecord, encoder);
                break;

            default:
                throw new InvalidOperationException(
                    $"Type {typeof(T)} is not supported. Must be ISpecificRecord or GenericRecord.");
        }
    }

    private int GetSchemaIdSync(string subject, T value)
    {
        // Check cache first
        if (_schemaIdCache.TryGetValue(subject, out var cachedId))
            return cachedId;

        // Get schema from value or type
        var avroSchema = GetSchemaFromValue(value);
        var schemaString = avroSchema.ToString();

        var registrySchema = new RegistrySchema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = schemaString
        };

        int schemaId;

        if (_config.UseLatestVersion)
        {
            // Use latest schema from registry
            var registered = _schemaRegistry.GetSchemaBySubjectAsync(subject, "latest")
                .GetAwaiter().GetResult();
            schemaId = registered.Id;
        }
        else if (_config.AutoRegisterSchemas)
        {
            // Register schema if auto-register is enabled
            schemaId = _schemaRegistry.GetOrRegisterSchemaAsync(subject, registrySchema)
                .GetAwaiter().GetResult();
        }
        else
        {
            // Get existing schema ID from registry
            var registered = _schemaRegistry.GetSchemaBySubjectAsync(subject)
                .GetAwaiter().GetResult();
            schemaId = registered.Id;
        }

        _schemaIdCache.TryAdd(subject, schemaId);
        return schemaId;
    }

    private AvroSchema GetSchemaFromValue(T value)
    {
        return value switch
        {
            ISpecificRecord specificRecord => specificRecord.Schema,
            GenericRecord genericRecord => genericRecord.Schema,
            _ => _writerSchema ?? throw new InvalidOperationException(
                $"Cannot determine Avro schema for type {typeof(T)}")
        };
    }

    private static AvroSchema? GetSchemaFromType()
    {
        // Check if T implements ISpecificRecord and has a static Schema property
        if (!typeof(ISpecificRecord).IsAssignableFrom(typeof(T)))
            return null;

        // Avro generated classes have a static _SCHEMA field
        var schemaField = typeof(T).GetField("_SCHEMA",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        if (schemaField?.GetValue(null) is AvroSchema schema)
            return schema;

        return null;
    }

    private string GetSubjectName(string topic, bool isKey)
    {
        var suffix = isKey ? "-key" : "-value";
        return _config.SubjectNameStrategy switch
        {
            SubjectNameStrategy.TopicName => topic + suffix,
            SubjectNameStrategy.RecordName => GetRecordName() + suffix,
            SubjectNameStrategy.TopicRecordName => $"{topic}-{GetRecordName()}{suffix}",
            _ => topic + suffix
        };
    }

    private static string GetRecordName()
    {
        // For Avro specific records, try to get the full name from schema
        if (typeof(ISpecificRecord).IsAssignableFrom(typeof(T)))
        {
            var schemaField = typeof(T).GetField("_SCHEMA",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            if (schemaField?.GetValue(null) is global::Avro.RecordSchema recordSchema)
                return recordSchema.Fullname;
        }

        return typeof(T).FullName ?? typeof(T).Name;
    }

    /// <summary>
    /// Disposes the serializer and optionally the underlying Schema Registry client.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}
