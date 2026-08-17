using System.Buffers;
using System.Buffers.Binary;
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
public sealed class JsonSchemaRegistrySerializer<T> :
    ISerializer<T>,
    IAsyncSerializerPreparer<T>,
    IAsyncDisposable
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
    private readonly bool _normalizeSchemas;
    private readonly bool _useLegacySubjectNames;
    private readonly Schema _schema;
    private readonly string _recordName;
    private readonly bool _ownsClient;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;
    private readonly IJsonSchemaValidatorFactory? _validatorFactory;

    private readonly SchemaResolutionCache<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> _schemaCache = new();
    private readonly SubjectSchemaIdCache _subjectSchemaIdCache = new();

    /// <summary>
    /// Creates a new JSON Schema Registry serializer.
    /// </summary>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON serialization uses reflection. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON serialization may require runtime code generation. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonSerializerOptions? jsonOptions = null,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null,
        bool normalizeSchemas = false)
        : this(
            schemaRegistry,
            jsonSchema,
            useLegacySubjectNames: false,
            jsonOptions,
            subjectNameStrategy,
            autoRegisterSchemas,
            ownsClient,
            ruleExecutor,
            normalizeSchemas)
    {
    }

    /// <summary>
    /// Creates a new JSON Schema Registry serializer with optional payload validation.
    /// </summary>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON serialization uses reflection. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON serialization may require runtime code generation. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonSerializerOptions? jsonOptions,
        JsonSchemaValidationOptions validationOptions,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null,
        bool normalizeSchemas = false)
        : this(
            schemaRegistry,
            jsonSchema,
            useLegacySubjectNames: false,
            jsonOptions,
            subjectNameStrategy,
            autoRegisterSchemas,
            ownsClient,
            ruleExecutor,
            normalizeSchemas)
    {
        ArgumentNullException.ThrowIfNull(validationOptions);
        _validatorFactory = validationOptions.GetSerializerFactory();
    }

    /// <summary>
    /// Creates a new JSON Schema Registry serializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="jsonSchema">The JSON schema string for type T.</param>
    /// <param name="jsonOptions">JSON serializer options.</param>
    /// <param name="subjectNameStrategy">Strategy for determining subject names.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <param name="ownsClient">Whether this serializer owns the client and should dispose it.</param>
    /// <param name="ruleExecutor">Optional rule executor applied to JSON payload bytes.</param>
    /// <param name="normalizeSchemas">Whether to normalize schemas during registration.</param>
    /// <param name="useLegacySubjectNames">Whether RecordName and TopicRecordName should use Dekaf's legacy -key/-value suffixes.</param>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON serialization uses reflection. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON serialization may require runtime code generation. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        bool useLegacySubjectNames,
        JsonSerializerOptions? jsonOptions = null,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null,
        bool normalizeSchemas = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _jsonOptions = CreateJsonOptions(jsonOptions);
        _serializePayload = SerializeWithOptions;
        _subjectNameStrategy = subjectNameStrategy;
        _autoRegisterSchemas = autoRegisterSchemas;
        _normalizeSchemas = normalizeSchemas;
        _useLegacySubjectNames = useLegacySubjectNames;
        _ownsClient = ownsClient;
        _ruleExecutor = ruleExecutor;
        _schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = jsonSchema
        };
        _recordName = SubjectNameResolver.GetRecordName(_schema, typeof(T).FullName ?? typeof(T).Name);
    }

    /// <summary>
    /// Creates a new NativeAOT-safe JSON Schema Registry serializer.
    /// </summary>
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonTypeInfo<T> jsonTypeInfo,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null,
        bool normalizeSchemas = false)
        : this(
            schemaRegistry,
            jsonSchema,
            jsonTypeInfo,
            useLegacySubjectNames: false,
            subjectNameStrategy,
            autoRegisterSchemas,
            ownsClient,
            ruleExecutor,
            normalizeSchemas)
    {
    }

    /// <summary>
    /// Creates a new NativeAOT-safe JSON Schema Registry serializer with optional payload validation.
    /// </summary>
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonTypeInfo<T> jsonTypeInfo,
        JsonSchemaValidationOptions validationOptions,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null,
        bool normalizeSchemas = false)
        : this(
            schemaRegistry,
            jsonSchema,
            jsonTypeInfo,
            useLegacySubjectNames: false,
            subjectNameStrategy,
            autoRegisterSchemas,
            ownsClient,
            ruleExecutor,
            normalizeSchemas)
    {
        ArgumentNullException.ThrowIfNull(validationOptions);
        _validatorFactory = validationOptions.GetSerializerFactory();
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
    /// <param name="ruleExecutor">Optional rule executor applied to JSON payload bytes.</param>
    /// <param name="normalizeSchemas">Whether to normalize schemas during registration.</param>
    /// <param name="useLegacySubjectNames">Whether RecordName and TopicRecordName should use Dekaf's legacy -key/-value suffixes.</param>
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonTypeInfo<T> jsonTypeInfo,
        bool useLegacySubjectNames,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null,
        bool normalizeSchemas = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _jsonTypeInfo = jsonTypeInfo ?? throw new ArgumentNullException(nameof(jsonTypeInfo));
        _serializePayload = SerializeWithTypeInfo;
        _subjectNameStrategy = subjectNameStrategy;
        _autoRegisterSchemas = autoRegisterSchemas;
        _normalizeSchemas = normalizeSchemas;
        _useLegacySubjectNames = useLegacySubjectNames;
        _ownsClient = ownsClient;
        _ruleExecutor = ruleExecutor;
        _schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = jsonSchema
        };
        _recordName = SubjectNameResolver.GetRecordName(_schema, typeof(T).FullName ?? typeof(T).Name);
    }

    /// <summary>
    /// Creates a new JSON Schema Registry serializer with a custom subject strategy and payload validation.
    /// </summary>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON serialization uses reflection. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON serialization may require runtime code generation. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        ISubjectNameStrategy customSubjectNameStrategy,
        JsonSerializerOptions? jsonOptions,
        JsonSchemaValidationOptions validationOptions,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null,
        bool normalizeSchemas = false)
        : this(
            schemaRegistry,
            jsonSchema,
            customSubjectNameStrategy,
            jsonOptions,
            autoRegisterSchemas,
            ownsClient,
            ruleExecutor,
            normalizeSchemas)
    {
        ArgumentNullException.ThrowIfNull(validationOptions);
        _validatorFactory = validationOptions.GetSerializerFactory();
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
    /// <param name="ruleExecutor">Optional rule executor applied to JSON payload bytes.</param>
    /// <param name="normalizeSchemas">Whether to normalize schemas during registration.</param>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON serialization uses reflection. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON serialization may require runtime code generation. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        ISubjectNameStrategy customSubjectNameStrategy,
        JsonSerializerOptions? jsonOptions = null,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null,
        bool normalizeSchemas = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _customSubjectNameStrategy = customSubjectNameStrategy ?? throw new ArgumentNullException(nameof(customSubjectNameStrategy));
        _jsonOptions = CreateJsonOptions(jsonOptions);
        _serializePayload = SerializeWithOptions;
        _autoRegisterSchemas = autoRegisterSchemas;
        _normalizeSchemas = normalizeSchemas;
        _ownsClient = ownsClient;
        _ruleExecutor = ruleExecutor;
        _schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = jsonSchema
        };
        _recordName = SubjectNameResolver.GetRecordName(_schema, typeof(T).FullName ?? typeof(T).Name);
    }

    /// <summary>
    /// Creates a new NativeAOT-safe JSON Schema Registry serializer with a custom subject strategy and payload validation.
    /// </summary>
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        ISubjectNameStrategy customSubjectNameStrategy,
        JsonTypeInfo<T> jsonTypeInfo,
        JsonSchemaValidationOptions validationOptions,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null,
        bool normalizeSchemas = false)
        : this(
            schemaRegistry,
            jsonSchema,
            customSubjectNameStrategy,
            jsonTypeInfo,
            autoRegisterSchemas,
            ownsClient,
            ruleExecutor,
            normalizeSchemas)
    {
        ArgumentNullException.ThrowIfNull(validationOptions);
        _validatorFactory = validationOptions.GetSerializerFactory();
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
    /// <param name="ruleExecutor">Optional rule executor applied to JSON payload bytes.</param>
    /// <param name="normalizeSchemas">Whether to normalize schemas during registration.</param>
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        ISubjectNameStrategy customSubjectNameStrategy,
        JsonTypeInfo<T> jsonTypeInfo,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null,
        bool normalizeSchemas = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _customSubjectNameStrategy = customSubjectNameStrategy ?? throw new ArgumentNullException(nameof(customSubjectNameStrategy));
        _jsonTypeInfo = jsonTypeInfo ?? throw new ArgumentNullException(nameof(jsonTypeInfo));
        _serializePayload = SerializeWithTypeInfo;
        _autoRegisterSchemas = autoRegisterSchemas;
        _normalizeSchemas = normalizeSchemas;
        _ownsClient = ownsClient;
        _ruleExecutor = ruleExecutor;
        _schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = jsonSchema
        };
        _recordName = SubjectNameResolver.GetRecordName(_schema, typeof(T).FullName ?? typeof(T).Name);
    }

    /// <summary>
    /// Resolves and caches the subject, schema ID, and schema for a serialization context.
    /// </summary>
    public ValueTask<ResolvedSchemaContext> PrepareAsync(
        string topic,
        T value,
        bool isKey = false,
        CancellationToken cancellationToken = default)
    {
        if (_subjectSchemaIdCache.TryGet(topic, isKey, out var cached))
            return new ValueTask<ResolvedSchemaContext>(ToResolvedContext(cached));

        return PrepareCoreAsync(topic, isKey, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask PrepareAsync(
        T value,
        SerializationContext context,
        CancellationToken cancellationToken = default)
    {
        var preparation = PrepareAsync(
            context.Topic,
            value,
            context.Component == SerializationComponent.Key,
            cancellationToken);
        if (preparation.IsCompletedSuccessfully)
        {
            _ = preparation.Result;
            return ValueTask.CompletedTask;
        }

        return AwaitPreparationAsync(preparation);

        static async ValueTask AwaitPreparationAsync(ValueTask<ResolvedSchemaContext> preparation) =>
            _ = await preparation.ConfigureAwait(false);
    }

    public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
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
        var validator = _validatorFactory?.GetOrCreate(schemaEntry.Schema!);
        if (_ruleExecutor is null)
        {
            validator?.Validate(payload.Span, schemaId);
        }
        else
        {
            var ruleContext = SchemaRegistryRuleContext.Rent(
                context.Topic,
                context.Component,
                schemaId,
                schemaEntry.Subject,
                schemaEntry.Schema,
                SchemaRegistryPayloadFormat.Json);
            try
            {
                if (_ruleExecutor is SchemaRegistryRuleExecutor builtInRuleExecutor && validator is not null)
                {
                    payload = builtInRuleExecutor.TransformSerializedPayload(
                        payload,
                        ruleContext,
                        validator,
                        schemaId);
                }
                else
                {
                    validator?.Validate(payload.Span, schemaId);
                    payload = _ruleExecutor.TransformSerializedPayload(payload, ruleContext);
                }
            }
            finally
            {
                ruleContext.Return();
            }
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
            static (serializer, subject) => serializer.ResolveSchema(subject));

    private SubjectSchemaIdCache.SubjectSchemaIdCacheValue ResolveSchema(string subject)
        => _schemaCache.Resolve(
            subject,
            _schema,
            this,
            static (serializer, resolvedSubject, schema) =>
                serializer.FetchSchemaWithTimeoutAsync(resolvedSubject, schema),
            SchemaRegistryTimeout);

    private ValueTask<ResolvedSchemaContext> PrepareCoreAsync(
        string topic,
        bool isKey,
        CancellationToken cancellationToken)
    {
        var subject = GetSubjectName(topic, isKey);
        var resolved = _schemaCache.ResolveAsync(
            subject,
            _schema,
            this,
            static (serializer, resolvedSubject, schema) =>
                serializer.FetchSchemaWithTimeoutAsync(resolvedSubject, schema),
            cancellationToken);
        if (resolved.IsCompletedSuccessfully)
        {
            var value = resolved.Result;
            return new ValueTask<ResolvedSchemaContext>(ToResolvedContext(
                _subjectSchemaIdCache.CacheEntry(topic, isKey, subject, value.SchemaId, value.Schema!)));
        }

        return AwaitSchemaAsync(this, topic, isKey, subject, resolved);

        static async ValueTask<ResolvedSchemaContext> AwaitSchemaAsync(
            JsonSchemaRegistrySerializer<T> serializer,
            string topic,
            bool isKey,
            string subject,
            ValueTask<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> resolved)
        {
            var value = await resolved.ConfigureAwait(false);
            return ToResolvedContext(serializer._subjectSchemaIdCache.CacheEntry(
                topic,
                isKey,
                subject,
                value.SchemaId,
                value.Schema!));
        }
    }

    private Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> FetchSchemaWithTimeoutAsync(
        string subject,
        Schema schema) =>
        SchemaRegistryOperationTimeout.ExecuteAsync(
            cancellationToken => FetchSchemaAsync(subject, schema, cancellationToken),
            SchemaRegistryTimeout,
            "Schema Registry resolution timed out.");

    private async Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> FetchSchemaAsync(
        string subject,
        Schema schema,
        CancellationToken cancellationToken)
    {
        if (_autoRegisterSchemas)
        {
            var schemaId = _normalizeSchemas
                ? await _schemaRegistry.GetOrRegisterSchemaAsync(
                    subject,
                    schema,
                    normalize: true,
                    cancellationToken).ConfigureAwait(false)
                : await _schemaRegistry.GetOrRegisterSchemaAsync(
                    subject,
                    schema,
                    cancellationToken).ConfigureAwait(false);
            var registeredSchema = _validatorFactory is null && _ruleExecutor is null
                ? schema
                : await _schemaRegistry.GetSchemaAsync(schemaId, subject, cancellationToken).ConfigureAwait(false);
            return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(schemaId, registeredSchema);
        }

        var registered = await _schemaRegistry.GetSchemaBySubjectAsync(
                subject,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(
            registered.Id,
            registered.Schema);
    }

    private static ResolvedSchemaContext ToResolvedContext(
        SubjectSchemaIdCache.SubjectSchemaIdCacheEntry entry) =>
        new(entry.Subject!, entry.SchemaId, entry.Schema!);

    private string GetSubjectName(string topic, bool isKey)
    {
        if (_customSubjectNameStrategy is not null)
        {
            return _customSubjectNameStrategy.GetSubjectName(topic, _recordName, isKey);
        }

        return SubjectNameResolver.GetSubjectName(
            _subjectNameStrategy,
            topic,
            _recordName,
            isKey,
            _useLegacySubjectNames);
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
    private static readonly string FallbackRecordName = typeof(T).FullName ?? typeof(T).Name;

    private delegate T JsonPayloadDeserializer(ReadOnlySpan<byte> payload);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly JsonPayloadDeserializer _deserializePayload;
    private readonly JsonSerializerOptions? _jsonOptions;
    private readonly JsonTypeInfo<T>? _jsonTypeInfo;
    private readonly bool _ownsClient;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;
    private readonly IJsonSchemaValidatorFactory? _validatorFactory;
    private readonly DeserializerSubjectNameCache? _subjectNames;

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
    /// Creates a new JSON Schema Registry deserializer with subject-name configuration for read rules.
    /// </summary>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON deserialization uses reflection. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON deserialization may require runtime code generation. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    public JsonSchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        JsonSerializerOptions? jsonOptions,
        SchemaRegistryDeserializerConfig config,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
        : this(schemaRegistry, jsonOptions, ownsClient, ruleExecutor)
    {
        ArgumentNullException.ThrowIfNull(config);
        _subjectNames = DeserializerSubjectNameCache.Create(config);
    }

    /// <summary>
    /// Creates a new JSON Schema Registry deserializer with optional payload validation.
    /// </summary>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON deserialization uses reflection. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON deserialization may require runtime code generation. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    public JsonSchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        JsonSerializerOptions? jsonOptions,
        JsonSchemaValidationOptions validationOptions,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
        : this(schemaRegistry, jsonOptions, ownsClient, ruleExecutor)
    {
        ArgumentNullException.ThrowIfNull(validationOptions);
        _validatorFactory = validationOptions.GetDeserializerFactory();
    }

    /// <summary>
    /// Creates a new JSON Schema Registry deserializer with validation and subject-name configuration.
    /// </summary>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON deserialization uses reflection. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON deserialization may require runtime code generation. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    public JsonSchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        JsonSerializerOptions? jsonOptions,
        JsonSchemaValidationOptions validationOptions,
        SchemaRegistryDeserializerConfig config,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
        : this(schemaRegistry, jsonOptions, config, ownsClient, ruleExecutor)
    {
        ArgumentNullException.ThrowIfNull(validationOptions);
        _validatorFactory = validationOptions.GetDeserializerFactory();
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

    /// <summary>
    /// Creates a new NativeAOT-safe JSON Schema Registry deserializer with subject-name configuration.
    /// </summary>
    public JsonSchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        JsonTypeInfo<T> jsonTypeInfo,
        SchemaRegistryDeserializerConfig config,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
        : this(schemaRegistry, jsonTypeInfo, ownsClient, ruleExecutor)
    {
        ArgumentNullException.ThrowIfNull(config);
        _subjectNames = DeserializerSubjectNameCache.Create(config);
    }

    /// <summary>
    /// Creates a new NativeAOT-safe JSON Schema Registry deserializer with optional payload validation.
    /// </summary>
    public JsonSchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        JsonTypeInfo<T> jsonTypeInfo,
        JsonSchemaValidationOptions validationOptions,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
        : this(schemaRegistry, jsonTypeInfo, ownsClient, ruleExecutor)
    {
        ArgumentNullException.ThrowIfNull(validationOptions);
        _validatorFactory = validationOptions.GetDeserializerFactory();
    }

    /// <summary>
    /// Creates a NativeAOT-safe JSON Schema Registry deserializer with validation and subject-name configuration.
    /// </summary>
    public JsonSchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        JsonTypeInfo<T> jsonTypeInfo,
        JsonSchemaValidationOptions validationOptions,
        SchemaRegistryDeserializerConfig config,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
        : this(schemaRegistry, jsonTypeInfo, config, ownsClient, ruleExecutor)
    {
        ArgumentNullException.ThrowIfNull(validationOptions);
        _validatorFactory = validationOptions.GetDeserializerFactory();
    }

    public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        var span = data.Span;

        if (span.Length < 5)
            throw new InvalidOperationException("Message too short to contain Schema Registry wire format");

        if (span[0] != MagicByte)
            throw new InvalidOperationException($"Unknown magic byte: {span[0]}. Expected Schema Registry format.");

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(span.Slice(1, 4));

        // Extract JSON payload and deserialize
        var payload = data.Slice(5);
        Schema schema;
        if (_ruleExecutor is not null)
        {
            string subject;
            if (_subjectNames is null)
            {
                subject = SubjectNameResolver.GetTopicSubjectName(
                    context.Topic,
                    context.Component == SerializationComponent.Key);
            }
            else
            {
                schema = _schemaRegistry.GetSchemaSync(schemaId, SchemaRegistryTimeout);
                subject = GetSubjectName(schemaId, schema, context);
            }

            schema = _schemaRegistry.GetSchemaSync(schemaId, subject, SchemaRegistryTimeout);
            var ruleContext = SchemaRegistryRuleContext.Rent(
                context.Topic,
                context.Component,
                schemaId,
                subject,
                schema,
                SchemaRegistryPayloadFormat.Json);
            try
            {
                payload = _ruleExecutor.TransformDeserializedPayload(payload, ruleContext);
            }
            finally
            {
                ruleContext.Return();
            }
        }
        else
        {
            // Verify the schema exists. Cache hits avoid Task allocation and sync-over-async.
            schema = _schemaRegistry.GetSchemaSync(schemaId, SchemaRegistryTimeout);
        }

        if (_validatorFactory is not null)
            _validatorFactory.GetOrCreate(schema).Validate(payload.Span, schemaId);

        return _deserializePayload(payload.Span);
    }

    private string GetSubjectName(int schemaId, Schema schema, SerializationContext context)
    {
        var isKey = context.Component == SerializationComponent.Key;
        return _subjectNames?.GetSubjectName(
                schemaId,
                schema,
                context.Topic,
                isKey,
                FallbackRecordName)
            ?? SubjectNameResolver.GetTopicSubjectName(context.Topic, isKey);
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
