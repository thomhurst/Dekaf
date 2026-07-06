using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Dekaf.Serialization;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// JSON serializer that integrates with Schema Registry.
/// Uses the Schema Registry wire format: [magic byte (0)] [schema ID (4 bytes)] [JSON payload].
/// </summary>
/// <remarks>
/// <para>
/// This serializer uses lazy caching for schema IDs. The first time a schema is needed for a
/// particular subject, a synchronous blocking call to the Schema Registry is made.
/// After the first fetch, subsequent serialization calls use the cached schema ID without blocking.
/// </para>
/// <para>
/// The blocking call includes a timeout to prevent indefinite hangs.
/// </para>
/// </remarks>
/// <typeparam name="T">The type to serialize.</typeparam>
public sealed class JsonSchemaRegistrySerializer<T> : ISerializer<T>, IAsyncDisposable
{
    private const byte MagicByte = 0x00;
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);

    [ThreadStatic]
    private static Utf8JsonWriter? t_jsonWriter;

    private delegate void JsonPayloadSerializer(Utf8JsonWriter writer, T value);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly JsonPayloadSerializer _serializePayload;
    private readonly JsonSerializerOptions? _jsonOptions;
    private readonly JsonTypeInfo<T>? _jsonTypeInfo;
    private readonly SubjectNameStrategy _subjectNameStrategy;
    private readonly ISubjectNameStrategy? _customSubjectNameStrategy;
    private readonly bool _autoRegisterSchemas;
    private readonly Schema _schema;
    private readonly bool _ownsClient;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;

    private readonly ConcurrentDictionary<string, int> _schemaIdCache = new();
    private readonly SubjectSchemaIdCache _subjectSchemaIdCache = new();

    /// <summary>
    /// Creates a new JSON Schema Registry serializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="jsonSchema">The JSON schema string for type T.</param>
    /// <param name="jsonOptions">JSON serializer options.</param>
    /// <param name="subjectNameStrategy">Strategy for determining subject names.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <param name="ownsClient">Whether this serializer owns the client and should dispose it.</param>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON serialization uses reflection. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON serialization may require runtime code generation. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonSerializerOptions? jsonOptions = null,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _jsonOptions = CreateJsonOptions(jsonOptions);
        _serializePayload = SerializeWithOptions;
        _subjectNameStrategy = subjectNameStrategy;
        _autoRegisterSchemas = autoRegisterSchemas;
        _ownsClient = ownsClient;
        _ruleExecutor = ruleExecutor;
        _schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = jsonSchema
        };
    }

    /// <summary>
    /// Creates a new NativeAOT-safe JSON Schema Registry serializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="jsonSchema">The JSON schema string for type T.</param>
    /// <param name="jsonTypeInfo">Source-generated metadata for type T.</param>
    /// <param name="subjectNameStrategy">Strategy for determining subject names.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <param name="ownsClient">Whether this serializer owns the client and should dispose it.</param>
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonTypeInfo<T> jsonTypeInfo,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _jsonTypeInfo = jsonTypeInfo ?? throw new ArgumentNullException(nameof(jsonTypeInfo));
        _serializePayload = SerializeWithTypeInfo;
        _subjectNameStrategy = subjectNameStrategy;
        _autoRegisterSchemas = autoRegisterSchemas;
        _ownsClient = ownsClient;
        _ruleExecutor = ruleExecutor;
        _schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = jsonSchema
        };
    }

    /// <summary>
    /// Creates a new JSON Schema Registry serializer with a custom subject name strategy.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="jsonSchema">The JSON schema string for type T.</param>
    /// <param name="customSubjectNameStrategy">Custom strategy for determining subject names.</param>
    /// <param name="jsonOptions">JSON serializer options.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <param name="ownsClient">Whether this serializer owns the client and should dispose it.</param>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON serialization uses reflection. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON serialization may require runtime code generation. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        ISubjectNameStrategy customSubjectNameStrategy,
        JsonSerializerOptions? jsonOptions = null,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _customSubjectNameStrategy = customSubjectNameStrategy ?? throw new ArgumentNullException(nameof(customSubjectNameStrategy));
        _jsonOptions = CreateJsonOptions(jsonOptions);
        _serializePayload = SerializeWithOptions;
        _autoRegisterSchemas = autoRegisterSchemas;
        _ownsClient = ownsClient;
        _ruleExecutor = ruleExecutor;
        _schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = jsonSchema
        };
    }

    /// <summary>
    /// Creates a new NativeAOT-safe JSON Schema Registry serializer with a custom subject name strategy.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="jsonSchema">The JSON schema string for type T.</param>
    /// <param name="customSubjectNameStrategy">Custom strategy for determining subject names.</param>
    /// <param name="jsonTypeInfo">Source-generated metadata for type T.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <param name="ownsClient">Whether this serializer owns the client and should dispose it.</param>
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        ISubjectNameStrategy customSubjectNameStrategy,
        JsonTypeInfo<T> jsonTypeInfo,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _customSubjectNameStrategy = customSubjectNameStrategy ?? throw new ArgumentNullException(nameof(customSubjectNameStrategy));
        _jsonTypeInfo = jsonTypeInfo ?? throw new ArgumentNullException(nameof(jsonTypeInfo));
        _serializePayload = SerializeWithTypeInfo;
        _autoRegisterSchemas = autoRegisterSchemas;
        _ownsClient = ownsClient;
        _ruleExecutor = ruleExecutor;
        _schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = jsonSchema
        };
    }

    public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var schemaEntry = GetSchemaForContext(context.Topic, context.Component == SerializationComponent.Key);
        var schemaId = schemaEntry.SchemaId;

        var payloadBuffer = SchemaRegistryBuffers.PayloadBuffer ??= new ArrayBufferWriter<byte>(initialCapacity: 4096);
        payloadBuffer.ResetWrittenCount();

        var jsonWriter = t_jsonWriter;
        if (jsonWriter is null)
        {
            jsonWriter = new Utf8JsonWriter(payloadBuffer);
            t_jsonWriter = jsonWriter;
        }
        else
        {
            jsonWriter.Reset(payloadBuffer);
        }

        try
        {
            _serializePayload(jsonWriter, value);
            jsonWriter.Flush();
        }
        catch
        {
            t_jsonWriter = null;
            throw;
        }

        var payload = payloadBuffer.WrittenMemory;
        if (_ruleExecutor is not null)
        {
            payload = _ruleExecutor.TransformSerializedPayload(
                payload,
                new SchemaRegistryRuleContext
                {
                    Topic = context.Topic,
                    Component = context.Component,
                    SchemaId = schemaId,
                    Subject = schemaEntry.Subject,
                    Schema = schemaEntry.Schema,
                    PayloadFormat = SchemaRegistryPayloadFormat.Json
                });
        }

        // Write wire format: [0x00] [schema ID] [JSON payload]
        var totalSize = 1 + 4 + payload.Length;
        var span = destination.GetSpan(totalSize);

        span[0] = MagicByte;
        BinaryPrimitives.WriteInt32BigEndian(span.Slice(1, 4), schemaId);
        payload.Span.CopyTo(span.Slice(5));

        destination.Advance(totalSize);

        if (payloadBuffer.Capacity > 1024 * 1024)
        {
            SchemaRegistryBuffers.PayloadBuffer = null;
            t_jsonWriter = null;
        }
    }

    private SubjectSchemaIdCache.SubjectSchemaIdCacheEntry GetSchemaForContext(string topic, bool isKey)
        => _subjectSchemaIdCache.GetOrAdd(
            topic,
            isKey,
            this,
            static (serializer, topic, isKey) => serializer.GetSubjectName(topic, isKey),
            static (serializer, subject) => new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(
                serializer.GetSchemaIdSync(subject),
                serializer._schema));

    private int GetSchemaIdSync(string subject)
    {
        if (_schemaIdCache.TryGetValue(subject, out var cachedId))
            return cachedId;

        var task = _autoRegisterSchemas
            ? _schemaRegistry.GetOrRegisterSchemaAsync(subject, _schema)
            : _schemaRegistry.GetSchemaBySubjectAsync(subject).ContinueWith(
                static t => t.GetAwaiter().GetResult().Id, TaskScheduler.Default);

        // Add timeout to prevent indefinite blocking
        var id = task.WaitAsync(SchemaRegistryTimeout).ConfigureAwait(false).GetAwaiter().GetResult();
        return _schemaIdCache.GetOrAdd(subject, id);
    }

    private string GetSubjectName(string topic, bool isKey)
    {
        if (_customSubjectNameStrategy is not null)
        {
            return _customSubjectNameStrategy.GetSubjectName(topic, typeof(T).FullName, isKey);
        }

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

    private static JsonSerializerOptions CreateJsonOptions(JsonSerializerOptions? jsonOptions)
    {
        return jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON serialization uses reflection. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON serialization may require runtime code generation. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    private void SerializeWithOptions(Utf8JsonWriter writer, T value)
    {
        JsonSerializer.Serialize(writer, value, _jsonOptions);
    }

    private void SerializeWithTypeInfo(Utf8JsonWriter writer, T value)
    {
        JsonSerializer.Serialize(writer, value, _jsonTypeInfo!);
    }
}

/// <summary>
/// JSON deserializer that integrates with Schema Registry.
/// Handles the wire format: [magic byte (0)] [schema ID (4 bytes)] [JSON payload].
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
/// <typeparam name="T">The type to deserialize.</typeparam>
public sealed class JsonSchemaRegistryDeserializer<T> : IDeserializer<T>, IAsyncDisposable
{
    private const byte MagicByte = 0x00;
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);

    private delegate T JsonPayloadDeserializer(ReadOnlySpan<byte> payload);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly JsonPayloadDeserializer _deserializePayload;
    private readonly JsonSerializerOptions? _jsonOptions;
    private readonly JsonTypeInfo<T>? _jsonTypeInfo;
    private readonly bool _ownsClient;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;

    /// <summary>
    /// Creates a new JSON Schema Registry deserializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="jsonOptions">JSON serializer options.</param>
    /// <param name="ownsClient">Whether this deserializer owns the client and should dispose it.</param>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON deserialization uses reflection. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON deserialization may require runtime code generation. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    public JsonSchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        JsonSerializerOptions? jsonOptions = null,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _jsonOptions = CreateJsonOptions(jsonOptions);
        _deserializePayload = DeserializeWithOptions;
        _ownsClient = ownsClient;
        _ruleExecutor = ruleExecutor;
    }

    /// <summary>
    /// Creates a new NativeAOT-safe JSON Schema Registry deserializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="jsonTypeInfo">Source-generated metadata for type T.</param>
    /// <param name="ownsClient">Whether this deserializer owns the client and should dispose it.</param>
    public JsonSchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        JsonTypeInfo<T> jsonTypeInfo,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _jsonTypeInfo = jsonTypeInfo ?? throw new ArgumentNullException(nameof(jsonTypeInfo));
        _deserializePayload = DeserializeWithTypeInfo;
        _ownsClient = ownsClient;
        _ruleExecutor = ruleExecutor;
    }

    public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        var span = data.Span;

        if (span.Length < 5)
            throw new InvalidOperationException("Message too short to contain Schema Registry wire format");

        if (span[0] != MagicByte)
            throw new InvalidOperationException($"Unknown magic byte: {span[0]}. Expected Schema Registry format.");

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(span.Slice(1, 4));

        // Verify the schema exists. Cache hits avoid Task allocation and sync-over-async.
        var schema = _schemaRegistry.GetSchemaSync(schemaId, SchemaRegistryTimeout);

        // Extract JSON payload and deserialize
        var payload = data.Slice(5);
        if (_ruleExecutor is not null)
        {
            payload = _ruleExecutor.TransformDeserializedPayload(
                payload,
                new SchemaRegistryRuleContext
                {
                    Topic = context.Topic,
                    Component = context.Component,
                    SchemaId = schemaId,
                    Schema = schema,
                    PayloadFormat = SchemaRegistryPayloadFormat.Json
                });
        }

        return _deserializePayload(payload.Span);
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }

    private static JsonSerializerOptions CreateJsonOptions(JsonSerializerOptions? jsonOptions)
    {
        return jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON deserialization uses reflection. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON deserialization may require runtime code generation. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    private T DeserializeWithOptions(ReadOnlySpan<byte> payload)
    {
        return JsonSerializer.Deserialize<T>(payload, _jsonOptions)!;
    }

    private T DeserializeWithTypeInfo(ReadOnlySpan<byte> payload)
    {
        return JsonSerializer.Deserialize(payload, _jsonTypeInfo!)!;
    }
}
