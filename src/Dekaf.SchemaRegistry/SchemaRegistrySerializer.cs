using System.Buffers;
using System.Buffers.Binary;
using Dekaf.Serialization;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// Base serializer that integrates with Schema Registry.
/// Handles the wire format: [magic byte (0)] [schema ID (4 bytes)] [payload].
/// </summary>
/// <typeparam name="T">The type to serialize.</typeparam>
public sealed class SchemaRegistrySerializer<T> : ISerializer<T>, IAsyncDisposable
{
    private const byte MagicByte = 0x00;

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly Func<T, byte[]> _serialize;
    private readonly Func<string, Schema> _getSchema;
    private readonly SubjectNameStrategy _subjectNameStrategy;
    private readonly bool _autoRegisterSchemas;
    private readonly bool _ownsClient;

    private int _cachedSchemaId = -1;
    private string? _cachedSubject;

    /// <summary>
    /// Creates a new Schema Registry serializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="serialize">Function to serialize the value to bytes (without wire format).</param>
    /// <param name="getSchema">Function to get the schema for a type.</param>
    /// <param name="subjectNameStrategy">Strategy for determining subject names.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <param name="ownsClient">Whether this serializer owns the client and should dispose it.</param>
    public SchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        Func<T, byte[]> serialize,
        Func<string, Schema> getSchema,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _serialize = serialize ?? throw new ArgumentNullException(nameof(serialize));
        _getSchema = getSchema ?? throw new ArgumentNullException(nameof(getSchema));
        _subjectNameStrategy = subjectNameStrategy;
        _autoRegisterSchemas = autoRegisterSchemas;
        _ownsClient = ownsClient;
    }

    public void Serialize<TWriter>(T value, TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var subject = GetSubjectName(context.Topic, context.Component == SerializationComponent.Key);
        var schema = _getSchema(subject);

        // Get or register schema ID (cached after first call per subject)
        var schemaId = GetSchemaIdSync(subject, schema);

        // Serialize the payload
        var payload = _serialize(value);

        // Write wire format: [0x00] [schema ID] [payload]
        var totalSize = 1 + 4 + payload.Length;
        var span = destination.GetSpan(totalSize);

        span[0] = MagicByte;
        BinaryPrimitives.WriteInt32BigEndian(span.Slice(1, 4), schemaId);
        payload.AsSpan().CopyTo(span.Slice(5));

        destination.Advance(totalSize);
    }

    private int GetSchemaIdSync(string subject, Schema schema)
    {
        // Use cached ID if subject matches
        if (_cachedSchemaId >= 0 && _cachedSubject == subject)
            return _cachedSchemaId;

        // Synchronously get/register schema (blocking)
        var task = _autoRegisterSchemas
            ? _schemaRegistry.GetOrRegisterSchemaAsync(subject, schema)
            : _schemaRegistry.GetSchemaBySubjectAsync(subject).ContinueWith(t => t.Result.Id);

        var id = task.GetAwaiter().GetResult();

        _cachedSchemaId = id;
        _cachedSubject = subject;

        return id;
    }

    private string GetSubjectName(string topic, bool isKey)
    {
        var suffix = isKey ? "-key" : "-value";
        return _subjectNameStrategy switch
        {
            SubjectNameStrategy.TopicName => topic + suffix,
            SubjectNameStrategy.RecordName => typeof(T).FullName + suffix,
            SubjectNameStrategy.TopicRecordName => $"{topic}-{typeof(T).FullName}{suffix}",
            _ => topic + suffix
        };
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Base deserializer that integrates with Schema Registry.
/// Handles the wire format: [magic byte (0)] [schema ID (4 bytes)] [payload].
/// </summary>
/// <typeparam name="T">The type to deserialize.</typeparam>
public sealed class SchemaRegistryDeserializer<T> : IDeserializer<T>, IAsyncDisposable
{
    private const byte MagicByte = 0x00;

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly Func<byte[], Schema, T> _deserialize;
    private readonly bool _ownsClient;

    /// <summary>
    /// Creates a new Schema Registry deserializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="deserialize">Function to deserialize bytes to value using the schema.</param>
    /// <param name="ownsClient">Whether this deserializer owns the client and should dispose it.</param>
    public SchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        Func<byte[], Schema, T> deserialize,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _deserialize = deserialize ?? throw new ArgumentNullException(nameof(deserialize));
        _ownsClient = ownsClient;
    }

    public T Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        if (data.Length < 5)
            throw new InvalidOperationException("Message too short to contain Schema Registry wire format");

        // Read wire format header
        Span<byte> header = stackalloc byte[5];
        data.Slice(0, 5).CopyTo(header);

        if (header[0] != MagicByte)
            throw new InvalidOperationException($"Unknown magic byte: {header[0]}. Expected Schema Registry format.");

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(header.Slice(1, 4));

        // Get schema from registry (cached)
        var schema = _schemaRegistry.GetSchemaAsync(schemaId).GetAwaiter().GetResult();

        // Extract payload
        var payload = data.Slice(5).ToArray();

        return _deserialize(payload, schema);
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Strategy for determining the subject name.
/// </summary>
public enum SubjectNameStrategy
{
    /// <summary>
    /// Subject name is the topic name with -key or -value suffix.
    /// </summary>
    TopicName,

    /// <summary>
    /// Subject name is the fully qualified record name with -key or -value suffix.
    /// </summary>
    RecordName,

    /// <summary>
    /// Subject name is topic-recordname with -key or -value suffix.
    /// </summary>
    TopicRecordName
}
