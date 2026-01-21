using System.Buffers;
using System.Buffers.Binary;
using System.Text.Json;
using Dekaf.Serialization;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// JSON serializer that integrates with Schema Registry.
/// Uses the Schema Registry wire format: [magic byte (0)] [schema ID (4 bytes)] [JSON payload].
/// </summary>
/// <typeparam name="T">The type to serialize.</typeparam>
public sealed class JsonSchemaRegistrySerializer<T> : ISerializer<T>, IAsyncDisposable
{
    private const byte MagicByte = 0x00;

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SubjectNameStrategy _subjectNameStrategy;
    private readonly bool _autoRegisterSchemas;
    private readonly Schema _schema;
    private readonly bool _ownsClient;

    private int _cachedSchemaId = -1;
    private string? _cachedSubject;

    /// <summary>
    /// Creates a new JSON Schema Registry serializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="jsonSchema">The JSON schema string for type T.</param>
    /// <param name="jsonOptions">JSON serializer options.</param>
    /// <param name="subjectNameStrategy">Strategy for determining subject names.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <param name="ownsClient">Whether this serializer owns the client and should dispose it.</param>
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonSerializerOptions? jsonOptions = null,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _subjectNameStrategy = subjectNameStrategy;
        _autoRegisterSchemas = autoRegisterSchemas;
        _ownsClient = ownsClient;
        _schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = jsonSchema
        };
    }

    public void Serialize(T value, IBufferWriter<byte> destination, SerializationContext context)
    {
        var subject = GetSubjectName(context.Topic, context.Component == SerializationComponent.Key);

        // Get or register schema ID (cached after first call per subject)
        var schemaId = GetSchemaIdSync(subject);

        // Serialize to JSON
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(value, _jsonOptions);

        // Write wire format: [0x00] [schema ID] [JSON payload]
        var totalSize = 1 + 4 + jsonBytes.Length;
        var span = destination.GetSpan(totalSize);

        span[0] = MagicByte;
        BinaryPrimitives.WriteInt32BigEndian(span.Slice(1, 4), schemaId);
        jsonBytes.AsSpan().CopyTo(span.Slice(5));

        destination.Advance(totalSize);
    }

    private int GetSchemaIdSync(string subject)
    {
        if (_cachedSchemaId >= 0 && _cachedSubject == subject)
            return _cachedSchemaId;

        var task = _autoRegisterSchemas
            ? _schemaRegistry.GetOrRegisterSchemaAsync(subject, _schema)
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
/// JSON deserializer that integrates with Schema Registry.
/// Handles the wire format: [magic byte (0)] [schema ID (4 bytes)] [JSON payload].
/// </summary>
/// <typeparam name="T">The type to deserialize.</typeparam>
public sealed class JsonSchemaRegistryDeserializer<T> : IDeserializer<T>, IAsyncDisposable
{
    private const byte MagicByte = 0x00;

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly bool _ownsClient;

    /// <summary>
    /// Creates a new JSON Schema Registry deserializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="jsonOptions">JSON serializer options.</param>
    /// <param name="ownsClient">Whether this deserializer owns the client and should dispose it.</param>
    public JsonSchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        JsonSerializerOptions? jsonOptions = null,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
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

        // Optionally fetch schema for validation (schema is cached)
        // For JSON, we typically just deserialize without validation
        // but we still verify the schema exists
        _ = _schemaRegistry.GetSchemaAsync(schemaId).GetAwaiter().GetResult();

        // Extract JSON payload and deserialize
        var payload = data.Slice(5);

        if (payload.IsSingleSegment)
        {
            return JsonSerializer.Deserialize<T>(payload.FirstSpan, _jsonOptions)!;
        }

        // Multi-segment: need to copy to contiguous array
        var jsonBytes = payload.ToArray();
        return JsonSerializer.Deserialize<T>(jsonBytes, _jsonOptions)!;
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}
