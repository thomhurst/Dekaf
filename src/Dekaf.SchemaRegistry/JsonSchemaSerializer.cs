using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    IRecordHeaderSerializer,
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
    private readonly SchemaSelectionMode _schemaSelectionMode;
    private readonly int? _useSchemaId;
    private readonly SchemaIdSerializerStrategy _schemaIdStrategy;
    private readonly bool _normalizeSchemas;
    private readonly bool _useLegacySubjectNames;
    private readonly Schema _schema;
    private readonly string _recordName;
    private readonly bool _ownsClient;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;
    private readonly IJsonSchemaValidatorFactory? _validatorFactory;

    private readonly SchemaResolutionCache<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> _schemaCache = new();
    private readonly SubjectSchemaIdCache _subjectSchemaIdCache = new();

    bool IRecordHeaderSerializer.ProducesRecordHeaders =>
        _schemaIdStrategy == SchemaIdSerializerStrategy.Header;

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

    /// <summary>Creates a JSON Schema Registry serializer with identity and schema-selection configuration.</summary>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON serialization uses reflection. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON serialization may require runtime code generation. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonSchemaSerializerConfig config,
        JsonSerializerOptions? jsonOptions = null,
        bool ownsClient = false)
    {
        ArgumentNullException.ThrowIfNull(config);
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _jsonOptions = CreateJsonOptions(jsonOptions);
        _serializePayload = SerializeWithOptions;
        _subjectNameStrategy = config.SubjectNameStrategy;
        _customSubjectNameStrategy = config.CustomSubjectNameStrategy;
        _autoRegisterSchemas = config.AutoRegisterSchemas;
        _normalizeSchemas = config.NormalizeSchemas;
        _useLegacySubjectNames = config.UseLegacySubjectNames;
        _ownsClient = ownsClient;
        _ruleExecutor = config.RuleExecutor;
        _useSchemaId = config.UseSchemaId;
        _schemaSelectionMode = SchemaRegistrySerializerConfigValidator.ValidateAndResolve(
            config.UseSchemaId,
            config.UseLatestVersion,
            config.AutoRegisterSchemas);
        _schemaIdStrategy = config.SchemaIdStrategy;
        if (_schemaIdStrategy is not (SchemaIdSerializerStrategy.Prefix or SchemaIdSerializerStrategy.Header))
            throw new ArgumentOutOfRangeException(nameof(config), _schemaIdStrategy, "Unknown schema identity strategy.");
        _schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = jsonSchema
        };
        _recordName = SubjectNameResolver.GetRecordName(_schema, typeof(T).FullName ?? typeof(T).Name);
    }

    /// <summary>Creates a JSON Schema Registry serializer with identity configuration and payload validation.</summary>
    [RequiresUnreferencedCode("JsonSerializerOptions-based JSON serialization uses reflection. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    [RequiresDynamicCode("JsonSerializerOptions-based JSON serialization may require runtime code generation. Use the JsonTypeInfo<T> constructor for NativeAOT.")]
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonSchemaSerializerConfig config,
        JsonSchemaValidationOptions validationOptions,
        JsonSerializerOptions? jsonOptions = null,
        bool ownsClient = false)
        : this(schemaRegistry, jsonSchema, config, jsonOptions, ownsClient)
    {
        ArgumentNullException.ThrowIfNull(validationOptions);
        _validatorFactory = validationOptions.GetSerializerFactory();
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
        _schemaSelectionMode = SchemaRegistrySerializerConfigValidator.ValidateAndResolve(
            useSchemaId: null,
            useLatestVersion: false,
            autoRegisterSchemas);
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

    /// <summary>Creates a NativeAOT-safe JSON Schema Registry serializer with identity configuration.</summary>
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonTypeInfo<T> jsonTypeInfo,
        JsonSchemaSerializerConfig config,
        bool ownsClient = false)
    {
        ArgumentNullException.ThrowIfNull(config);
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _jsonTypeInfo = jsonTypeInfo ?? throw new ArgumentNullException(nameof(jsonTypeInfo));
        _serializePayload = SerializeWithTypeInfo;
        _subjectNameStrategy = config.SubjectNameStrategy;
        _customSubjectNameStrategy = config.CustomSubjectNameStrategy;
        _autoRegisterSchemas = config.AutoRegisterSchemas;
        _normalizeSchemas = config.NormalizeSchemas;
        _useLegacySubjectNames = config.UseLegacySubjectNames;
        _ownsClient = ownsClient;
        _ruleExecutor = config.RuleExecutor;
        _useSchemaId = config.UseSchemaId;
        _schemaSelectionMode = SchemaRegistrySerializerConfigValidator.ValidateAndResolve(
            config.UseSchemaId,
            config.UseLatestVersion,
            config.AutoRegisterSchemas);
        _schemaIdStrategy = config.SchemaIdStrategy;
        if (_schemaIdStrategy is not (SchemaIdSerializerStrategy.Prefix or SchemaIdSerializerStrategy.Header))
            throw new ArgumentOutOfRangeException(nameof(config), _schemaIdStrategy, "Unknown schema identity strategy.");
        _schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = jsonSchema
        };
        _recordName = SubjectNameResolver.GetRecordName(_schema, typeof(T).FullName ?? typeof(T).Name);
    }

    /// <summary>Creates a NativeAOT-safe JSON Schema Registry serializer with identity configuration and payload validation.</summary>
    public JsonSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        string jsonSchema,
        JsonTypeInfo<T> jsonTypeInfo,
        JsonSchemaSerializerConfig config,
        JsonSchemaValidationOptions validationOptions,
        bool ownsClient = false)
        : this(schemaRegistry, jsonSchema, jsonTypeInfo, config, ownsClient)
    {
        ArgumentNullException.ThrowIfNull(validationOptions);
        _validatorFactory = validationOptions.GetSerializerFactory();
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
        _schemaSelectionMode = SchemaRegistrySerializerConfigValidator.ValidateAndResolve(
            useSchemaId: null,
            useLatestVersion: false,
            autoRegisterSchemas);
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
        _schemaSelectionMode = SchemaRegistrySerializerConfigValidator.ValidateAndResolve(
            useSchemaId: null,
            useLatestVersion: false,
            autoRegisterSchemas);
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
        _schemaSelectionMode = SchemaRegistrySerializerConfigValidator.ValidateAndResolve(
            useSchemaId: null,
            useLatestVersion: false,
            autoRegisterSchemas);
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

        int totalSize;
        if (_schemaIdStrategy == SchemaIdSerializerStrategy.Prefix)
        {
            totalSize = SchemaIdentityFraming.SchemaIdFrameSize + payload.Length;
            var span = destination.GetSpan(totalSize);
            span[0] = MagicByte;
            BinaryPrimitives.WriteInt32BigEndian(span.Slice(1, 4), schemaId);
            payload.Span.CopyTo(span.Slice(SchemaIdentityFraming.SchemaIdFrameSize));
        }
        else
        {
            totalSize = payload.Length;
            var span = destination.GetSpan(totalSize);
            SchemaIdentitySerialization.WriteIdentity(
                span,
                context,
                in schemaEntry,
                SchemaIdSerializerStrategy.Header);
            payload.Span.CopyTo(span);
        }

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
                _subjectSchemaIdCache.CacheEntry(
                    topic,
                    isKey,
                    subject,
                    value.SchemaId,
                    value.Schema!)));
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
        if (_schemaSelectionMode == SchemaSelectionMode.ExplicitId)
        {
            var schemaId = _useSchemaId!.Value;
            var explicitSchema = await _schemaRegistry.GetSchemaAsync(
                    schemaId,
                    subject,
                    cancellationToken)
                .ConfigureAwait(false);
            ValidateSchemaFormat(schemaId, explicitSchema);
            ValidateSelectedSchema(schemaId, explicitSchema, schema);
            return await CreateResolvedValueAsync(
                    subject,
                    schemaId,
                    explicitSchema,
                    registeredSchema: null,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (_schemaSelectionMode == SchemaSelectionMode.Latest)
        {
            var latest = await _schemaRegistry.GetSchemaBySubjectAsync(
                    subject,
                    "latest",
                    cancellationToken)
                .ConfigureAwait(false);
            ValidateSchemaFormat(latest.Id, latest.Schema);
            return await CreateResolvedValueAsync(
                    subject,
                    latest.Id,
                    latest.Schema,
                    latest,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (_schemaSelectionMode == SchemaSelectionMode.AutoRegister)
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
            var registeredSchema = _ruleExecutor is SchemaRegistryRuleExecutor
                ? await _schemaRegistry.GetSchemaAsync(schemaId, subject, cancellationToken).ConfigureAwait(false)
                : _validatorFactory is not null
                    ? await _schemaRegistry.GetSchemaAsync(schemaId, cancellationToken).ConfigureAwait(false)
                    : schema;
            return await CreateResolvedValueAsync(
                    subject,
                    schemaId,
                    registeredSchema,
                    registeredSchema: null,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var registered = await _schemaRegistry.GetSchemaBySubjectAsync(
                subject,
                "latest",
                cancellationToken)
            .ConfigureAwait(false);
        ValidateSchemaFormat(registered.Id, registered.Schema);
        return await CreateResolvedValueAsync(
                subject,
                registered.Id,
                registered.Schema,
                registered,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> CreateResolvedValueAsync(
        string subject,
        int schemaId,
        Schema schema,
        RegisteredSchema? registeredSchema,
        CancellationToken cancellationToken) =>
        SchemaIdentityResolution.CreateSerializerValueAsync(
            _schemaRegistry,
            subject,
            schemaId,
            schema,
            _schemaIdStrategy,
            _normalizeSchemas,
            registeredSchema,
            cancellationToken);

    private static void ValidateSchemaFormat(int schemaId, Schema schema)
    {
        if (schema.SchemaType != SchemaType.Json)
        {
            throw new InvalidOperationException(
                $"Schema ID {schemaId} has format {schema.SchemaType}; expected {SchemaType.Json}.");
        }
    }

    private static void ValidateSelectedSchema(
        int schemaId,
        Schema selectedSchema,
        Schema configuredSchema)
    {
        if (!JsonNode.DeepEquals(
                JsonNode.Parse(selectedSchema.SchemaString),
                JsonNode.Parse(configuredSchema.SchemaString)))
        {
            throw new InvalidOperationException(
                $"Schema ID {schemaId} does not match the configured JSON schema.");
        }
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
/// <para>
/// Kafka value tombstones return <see langword="default"/> without reading Confluent framing
/// or contacting Schema Registry. This is <see langword="null"/> for reference and nullable
/// types; non-nullable value types receive their normal default value.
/// </para>
/// </remarks>
/// <typeparam name="T">The type to deserialize.</typeparam>
public sealed class JsonSchemaRegistryDeserializer<T> :
    IDeserializer<T>,
    IRecordHeaderDeserializer<T>,
    ICallerOwnedHeaderDeserializer<T>,
    IRecordHeaderRoutingProvider,
    IAsyncDisposable
{
    private const int MaxCachedGuidSchemas = 1024;
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
    private readonly SchemaRegistryMigrationRunner? _migrationRunner;
    private readonly ConcurrentDictionary<GuidTopicKey, Lazy<Task<GuidResolvedSchema>>> _guidSchemaCache = new();
    private readonly ConcurrentQueue<KeyValuePair<GuidTopicKey, Lazy<Task<GuidResolvedSchema>>>>
        _guidSchemaEvictionQueue = new();
    private int _cachedGuidSchemaCount;
    private readonly SchemaIdDeserializerStrategy _schemaIdStrategy = SchemaIdDeserializerStrategy.Dual;

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
        ValidateSchemaIdStrategy(config.SchemaIdStrategy);
        _schemaIdStrategy = config.SchemaIdStrategy;
        _subjectNames = DeserializerSubjectNameCache.Create(config);
        if (config.UseLatestVersion)
        {
            (_migrationRunner, _ruleExecutor) = SchemaRegistryMigrationRunner.Create(
                schemaRegistry,
                ruleExecutor,
                SchemaRegistryTimeout);
        }
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
        ValidateSchemaIdStrategy(config.SchemaIdStrategy);
        _schemaIdStrategy = config.SchemaIdStrategy;
        _subjectNames = DeserializerSubjectNameCache.Create(config);
        if (config.UseLatestVersion)
        {
            (_migrationRunner, _ruleExecutor) = SchemaRegistryMigrationRunner.Create(
                schemaRegistry,
                ruleExecutor,
                SchemaRegistryTimeout);
        }
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
        Header? identityHeader = null;
        if (_schemaIdStrategy != SchemaIdDeserializerStrategy.Prefix
            && context.Headers is { } callerHeaders)
        {
            var headerName = GetIdentityHeaderName(context.Component);
            for (var index = callerHeaders.Count - 1; index >= 0; index--)
            {
                if (string.Equals(callerHeaders[index].Key, headerName, StringComparison.Ordinal))
                {
                    identityHeader = callerHeaders[index];
                    break;
                }
            }
        }

        return DeserializeCore(data, context, identityHeader);
    }

    T ICallerOwnedHeaderDeserializer<T>.DeserializeCallerOwned(
        ReadOnlyMemory<byte> data,
        SerializationContext context) => Deserialize(data, context);

    T IRecordHeaderDeserializer<T>.Deserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers)
    {
        Header? identityHeader = headers.TryGetLast(GetIdentityHeaderName(context.Component), out var header)
            ? header
            : null;
        return DeserializeCore(data, context, identityHeader);
    }

    void IRecordHeaderRoutingProvider.CollectHeaderNames(List<string> names)
    {
        AddHeaderName(names, SchemaIdentityHeaderNames.Key);
        AddHeaderName(names, SchemaIdentityHeaderNames.Value);
    }

    private T DeserializeCore(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        Header? identityHeader)
    {
        if (context is { IsNull: true, Component: SerializationComponent.Value })
            return default!;

        var identity = SchemaIdentityFraming.Read(
            data.Span,
            identityHeader,
            _schemaIdStrategy,
            out var payloadOffset,
            out var trailingHeaderData);
        if (!trailingHeaderData.IsEmpty)
            throw new InvalidDataException("JSON schema identity headers cannot contain trailing data.");
        var schemaId = identity.SchemaId ?? -1;
        var guidSchema = identity.SchemaGuid is { } schemaGuid
            ? GetGuidSchemaCached(schemaGuid, context)
            : null;

        // Extract JSON payload and deserialize
        var payload = data[payloadOffset..];
        Schema schema;
        if (_ruleExecutor is not null)
        {
            string subject;
            if (guidSchema is not null)
            {
                schemaId = guidSchema.SchemaId;
                subject = guidSchema.Subject;
                schema = guidSchema.Schema;
            }
            else if (_subjectNames is null)
            {
                subject = SubjectNameResolver.GetTopicSubjectName(
                    context.Topic,
                    context.Component == SerializationComponent.Key);
                schema = _schemaRegistry.GetSchemaSync(schemaId, subject, SchemaRegistryTimeout);
            }
            else
            {
                schema = _schemaRegistry.GetSchemaSync(schemaId, SchemaRegistryTimeout);
                subject = GetSubjectName(schemaId, schema, context);
                schema = _schemaRegistry.GetSchemaSync(schemaId, subject, SchemaRegistryTimeout);
            }
            if (_migrationRunner is null)
            {
                var ruleContext = SchemaRegistryRuleContext.Rent(
                    context.Topic,
                    context.Component,
                    schemaId,
                    subject,
                    schema,
                    SchemaRegistryPayloadFormat.Json);
                try
                {
                    payload = _ruleExecutor!.TransformDeserializedPayload(payload, ruleContext);
                }
                finally
                {
                    ruleContext.Return();
                }
            }
            else
            {
                var migration = _migrationRunner.Transform(
                    payload,
                    schemaId,
                    subject,
                    schema,
                    context,
                    SchemaRegistryPayloadFormat.Json);
                payload = migration.Payload;
                schema = migration.ReaderSchema.Schema;
            }
        }
        else
        {
            // Verify the schema exists. Cache hits avoid Task allocation and sync-over-async.
            schema = guidSchema?.Schema ?? _schemaRegistry.GetSchemaSync(schemaId, SchemaRegistryTimeout);
            if (guidSchema is not null)
                schemaId = guidSchema.SchemaId;
        }

        if (_validatorFactory is not null)
            _validatorFactory.GetOrCreate(schema).Validate(payload.Span, schemaId);

        return _deserializePayload(payload.Span);
    }

    private GuidResolvedSchema GetGuidSchemaCached(Guid schemaGuid, SerializationContext context)
    {
        var key = new GuidTopicKey(
            schemaGuid,
            context.Topic,
            context.Component == SerializationComponent.Key);
        if (!_guidSchemaCache.TryGetValue(key, out var lazy))
        {
            lazy = _guidSchemaCache.GetOrAdd(
                key,
                static (cacheKey, deserializer) => deserializer.CreateGuidSchemaLazy(cacheKey),
                this);
        }

        var task = lazy.Value;
        return task.IsCompletedSuccessfully
            ? task.Result
            : task.WaitAsync(SchemaRegistryTimeout).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private Lazy<Task<GuidResolvedSchema>> CreateGuidSchemaLazy(GuidTopicKey key) =>
        new(() => FetchGuidSchemaAsync(key));

    private async Task<GuidResolvedSchema> FetchGuidSchemaAsync(GuidTopicKey key)
    {
        try
        {
            var resolved = await SchemaRegistryOperationTimeout.ExecuteAsync(
                    cancellationToken => FetchGuidSchemaCoreAsync(key, cancellationToken),
                    SchemaRegistryTimeout,
                    $"Schema GUID {key.SchemaGuid:D} resolution timed out.")
                .ConfigureAwait(false);
            BoundedSchemaIdentityCache.RecordSuccessfulResolution(
                _guidSchemaCache,
                _guidSchemaEvictionQueue,
                key,
                ref _cachedGuidSchemaCount,
                MaxCachedGuidSchemas);
            return resolved;
        }
        catch
        {
            _guidSchemaCache.TryRemove(key, out _);
            throw;
        }
    }

    private async Task<GuidResolvedSchema> FetchGuidSchemaCoreAsync(
        GuidTopicKey key,
        CancellationToken cancellationToken)
    {
        var unscopedSchema = await _schemaRegistry.GetSchemaByGuidAsync(
                key.SchemaGuid.ToString("D"),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (unscopedSchema.SchemaType != SchemaType.Json)
        {
            throw new InvalidOperationException(
                $"Schema with GUID {key.SchemaGuid:D} is not a JSON schema. Type: {unscopedSchema.SchemaType}");
        }
        var context = new SerializationContext
        {
            Topic = key.Topic,
            Component = key.IsKey ? SerializationComponent.Key : SerializationComponent.Value
        };
        var subject = GetUncachedSubjectName(unscopedSchema, context);
        var registered = await _schemaRegistry.LookupSchemaAsync(
                subject,
                unscopedSchema,
                ignoreDeletedSchemas: true,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!Guid.TryParse(registered.Guid, out var registeredGuid) || registeredGuid != key.SchemaGuid)
        {
            throw new InvalidDataException(
                $"Schema Registry returned a conflicting GUID for subject '{subject}'.");
        }

        var resolved = new GuidResolvedSchema(registered.Id, subject, registered.Schema);
        return resolved;
    }

    private static void ValidateSchemaIdStrategy(SchemaIdDeserializerStrategy strategy)
    {
        if (strategy is not (
            SchemaIdDeserializerStrategy.Dual
            or SchemaIdDeserializerStrategy.Prefix
            or SchemaIdDeserializerStrategy.Header))
        {
            throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown schema identity strategy.");
        }
    }

    private static void AddHeaderName(List<string> names, string name)
    {
        if (!names.Contains(name))
            names.Add(name);
    }

    private static string GetIdentityHeaderName(SerializationComponent component) => component switch
    {
        SerializationComponent.Key => SchemaIdentityHeaderNames.Key,
        SerializationComponent.Value => SchemaIdentityHeaderNames.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Unknown serialization component.")
    };

    private readonly record struct GuidTopicKey(Guid SchemaGuid, string Topic, bool IsKey);

    private sealed record GuidResolvedSchema(int SchemaId, string Subject, Schema Schema);

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

    private string GetUncachedSubjectName(Schema schema, SerializationContext context)
    {
        var isKey = context.Component == SerializationComponent.Key;
        return _subjectNames?.ResolveSubjectName(schema, context.Topic, isKey, FallbackRecordName)
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
