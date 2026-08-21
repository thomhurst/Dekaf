using System.Collections.Concurrent;
using Dekaf.Serialization;
using Google.Protobuf;

namespace Dekaf.SchemaRegistry.Protobuf;

/// <summary>
/// Protobuf deserializer that integrates with Confluent Schema Registry.
/// Supports Confluent schema-ID prefix framing, GUID record-header framing, or dual detection.
/// Protobuf message indexes remain adjacent to their selected identity frame.
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
/// <typeparam name="T">The Protobuf message type to deserialize.</typeparam>
public sealed class ProtobufSchemaRegistryDeserializer<T>
    : IDeserializer<T>, IRecordHeaderDeserializer<T>, ICallerOwnedHeaderDeserializer<T>,
      IRecordHeaderRoutingProvider, IAsyncDeserializerPreparer<T>,
      IRecordHeaderAsyncDeserializerPreparer<T>,
      IAsyncDeserializerPreparationRequirement, IAsyncDisposable
    where T : IMessage<T>, IBufferMessage, new()
{
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);
    private const int MaxCachedGuidSchemas = 1024;
    private static readonly string RecordName = new T().Descriptor.FullName;

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly ProtobufDeserializerConfig _config;
    private readonly bool _ownsClient;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;
    private readonly MessageParser<T> _parser;
    private readonly DeserializerSubjectNameCache? _subjectNames;
    private readonly SchemaRegistryMigrationRunner? _migrationRunner;
    private readonly ConcurrentDictionary<GuidTopicKey, Lazy<Task<GuidResolvedSchema>>> _guidSchemaCache = new();
    private readonly ConcurrentQueue<KeyValuePair<GuidTopicKey, Lazy<Task<GuidResolvedSchema>>>>
        _guidSchemaEvictionQueue = new();
    private int _cachedGuidSchemaCount;

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
        ValidateSchemaIdStrategy(_config.SchemaIdStrategy);
        _ruleExecutor = _config.RuleExecutor;
        _ownsClient = ownsClient;
        _parser = new MessageParser<T>(() => new T());
        _subjectNames = DeserializerSubjectNameCache.Create(
            schemaRegistry,
            _config.SubjectNameStrategy,
            _config.CustomSubjectNameStrategy,
            _config.AsyncSubjectNameStrategy,
            _config.UseLegacySubjectNames);
        if (_config.UseLatestVersion)
        {
            (_migrationRunner, _ruleExecutor) = SchemaRegistryMigrationRunner.Create(
                schemaRegistry,
                _config.RuleExecutor,
                SchemaRegistryTimeout);
        }
    }

    bool IAsyncDeserializerPreparationRequirement.RequiresPreparation =>
        _ruleExecutor is not null && _subjectNames is { RequiresPreparation: true };

    ValueTask IAsyncDeserializerPreparer<T>.PrepareAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        CancellationToken cancellationToken) =>
        PrepareCoreAsync(data, context, FindCallerIdentityHeader(context), cancellationToken);

    ValueTask IRecordHeaderAsyncDeserializerPreparer<T>.PrepareAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        RecordHeaderRoutingLookup headers,
        CancellationToken cancellationToken) =>
        PrepareCoreAsync(data, context, FindRoutedIdentityHeader(context, in headers), cancellationToken);

    private ValueTask PrepareCoreAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        Header? identityHeader,
        CancellationToken cancellationToken)
    {
        if (context is { IsNull: true, Component: SerializationComponent.Value }
            || _ruleExecutor is null
            || _subjectNames is not { RequiresPreparation: true } subjectNames)
        {
            return default;
        }

        if (_config.SchemaIdStrategy == SchemaIdDeserializerStrategy.Prefix)
        {
            return DeserializerSubjectNameCache.TryReadSchemaId(data, out var prefixSchemaId)
                ? subjectNames.PrepareAsync(
                    _schemaRegistry,
                    prefixSchemaId,
                    context.Topic,
                    context.Component == SerializationComponent.Key,
                    RecordName,
                    cancellationToken)
                : default;
        }

        var identity = SchemaIdentityFraming.Read(
            data.Span,
            identityHeader,
            _config.SchemaIdStrategy,
            out _,
            out _);
        if (identity.SchemaGuid is { } schemaGuid)
        {
            return new ValueTask(GetGuidSchemaAsync(
                new GuidTopicKey(
                    schemaGuid,
                    context.Topic,
                    context.Component == SerializationComponent.Key,
                    subjectNames.Generation),
                cancellationToken));
        }

        return subjectNames.PrepareAsync(
            _schemaRegistry,
            identity.SchemaId!.Value,
            context.Topic,
            context.Component == SerializationComponent.Key,
            RecordName,
            cancellationToken);
    }

    bool IAsyncDeserializerPreparer<T>.TryDeserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        out T value) =>
        TryDeserializeCore(data, context, FindCallerIdentityHeader(context), out value);

    bool IRecordHeaderAsyncDeserializerPreparer<T>.TryDeserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers,
        out T value) =>
        TryDeserializeCore(data, context, FindRoutedIdentityHeader(context, in headers), out value);

    private bool TryDeserializeCore(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        Header? identityHeader,
        out T value)
    {
        if (context is { IsNull: true, Component: SerializationComponent.Value })
        {
            value = default!;
            return true;
        }

        string? preparedSubject = null;
        if (_ruleExecutor is not null
            && _subjectNames is { RequiresPreparation: true } subjectNames)
        {
            if (_config.SchemaIdStrategy == SchemaIdDeserializerStrategy.Prefix)
            {
                if (DeserializerSubjectNameCache.TryReadSchemaId(data, out var prefixSchemaId))
                {
                    if (!subjectNames.TryGetPreparedSubject(
                            prefixSchemaId,
                            context.Topic,
                            context.Component == SerializationComponent.Key,
                            out var prepared))
                    {
                        value = default!;
                        return false;
                    }

                    preparedSubject = prepared.Subject;
                }
            }
            else
            {
                var identity = SchemaIdentityFraming.Read(
                    data.Span,
                    identityHeader,
                    _config.SchemaIdStrategy,
                    out _,
                    out _);
                if (identity.SchemaGuid is { } schemaGuid)
                {
                    var key = new GuidTopicKey(
                        schemaGuid,
                        context.Topic,
                        context.Component == SerializationComponent.Key,
                        subjectNames.Generation);
                    if (!HasResolvedGuidSchema(key))
                    {
                        value = default!;
                        return false;
                    }
                }
                else if (!subjectNames.TryGetPreparedSubject(
                             identity.SchemaId!.Value,
                             context.Topic,
                             context.Component == SerializationComponent.Key,
                             out var prepared))
                {
                    value = default!;
                    return false;
                }
                else
                {
                    preparedSubject = prepared.Subject;
                }
            }
        }

        value = DeserializeCore(data, context, identityHeader, preparedSubject);
        return true;
    }

    /// <inheritdoc />
    public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
        DeserializeCore(data, context, FindCallerIdentityHeader(context), preparedSubject: null);

    T ICallerOwnedHeaderDeserializer<T>.DeserializeCallerOwned(
        ReadOnlyMemory<byte> data,
        SerializationContext context) => Deserialize(data, context);

    T IRecordHeaderDeserializer<T>.Deserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers) =>
        DeserializeCore(
            data,
            context,
            FindRoutedIdentityHeader(context, in headers),
            preparedSubject: null);

    void IRecordHeaderRoutingProvider.CollectHeaderNames(List<string> names)
    {
        AddHeaderName(names, SchemaIdentityHeaderNames.Key);
        AddHeaderName(names, SchemaIdentityHeaderNames.Value);
    }

    private T DeserializeCore(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        Header? identityHeader,
        string? preparedSubject)
    {
        if (context is { IsNull: true, Component: SerializationComponent.Value })
            return default!;

        var identity = SchemaIdentityFraming.Read(
            data.Span,
            identityHeader,
            _config.SchemaIdStrategy,
            out var payloadOffset,
            out var headerMessageIndexes);
        var schemaId = identity.SchemaId ?? -1;
        var needsSchema = !_config.SkipSchemaValidation
                          || _config.RuleExecutor is SchemaRegistryRuleExecutor
                          || _migrationRunner is not null;
        var guidSchema = identity.SchemaGuid is { } schemaGuid
                         && (needsSchema || _ruleExecutor is not null)
            ? GetGuidSchemaCached(schemaGuid, context)
            : null;
        if (guidSchema is not null)
            schemaId = guidSchema.SchemaId;

        // Optionally validate the schema exists (with timeout to prevent indefinite hang)
        Schema? schema = guidSchema?.Schema;
        string? ruleSubject = null;
        if (needsSchema && guidSchema is null)
        {
            if (preparedSubject is not null)
            {
                ruleSubject = preparedSubject;
                schema = _schemaRegistry.GetSchemaSync(schemaId, ruleSubject, SchemaRegistryTimeout);
            }
            else if (_config.RuleExecutor is not null && _subjectNames is null)
            {
                ruleSubject = SubjectNameResolver.GetTopicSubjectName(
                    context.Topic,
                    context.Component == SerializationComponent.Key);
                schema = _schemaRegistry.GetSchemaSync(schemaId, ruleSubject, SchemaRegistryTimeout);
            }
            else
            {
                schema = _schemaRegistry.GetSchemaSync(schemaId, SchemaRegistryTimeout);
            }

            if (!_config.SkipSchemaValidation && schema.SchemaType != SchemaType.Protobuf)
                throw new InvalidOperationException($"Schema {schemaId} is not a Protobuf schema (type: {schema.SchemaType})");
        }

        // Read the message indexes (varints)
        var indexesMemory = identity.SchemaGuid.HasValue
            ? headerMessageIndexes
            : data[payloadOffset..];
        var indexes = indexesMemory.Span;
        var (indexCount, bytesRead) = ReadVarint(indexes, _config.UseDeprecatedFormat);
        if (indexCount < 0)
            throw new InvalidOperationException("Message index array length cannot be negative");

        // Skip past the index array
        for (var i = 0; i < indexCount; i++)
        {
            var (index, indexBytesRead) = ReadVarint(indexes[bytesRead..], _config.UseDeprecatedFormat);
            if (index < 0)
                throw new InvalidOperationException("Message index cannot be negative");
            bytesRead += indexBytesRead;
        }

        // The rest is the protobuf message
        ReadOnlyMemory<byte> protobufData;
        if (identity.SchemaGuid.HasValue)
        {
            if (bytesRead != headerMessageIndexes.Length)
            {
                throw new InvalidDataException(
                    "The Protobuf schema identity header contains trailing message-index data.");
            }

            protobufData = data[payloadOffset..];
        }
        else
        {
            protobufData = data[(payloadOffset + bytesRead)..];
        }

        if (_ruleExecutor is not null)
        {
            var subject = guidSchema?.Subject ?? ruleSubject ?? GetSubjectName(schemaId, schema, context);
            if (guidSchema is null && schema is not null && ruleSubject is null)
                schema = _schemaRegistry.GetSchemaSync(schemaId, subject, SchemaRegistryTimeout);
            if (_migrationRunner is null)
            {
                var ruleContext = SchemaRegistryRuleContext.Rent(
                    context.Topic,
                    context.Component,
                    schemaId,
                    subject,
                    schema,
                    SchemaRegistryPayloadFormat.Protobuf);
                try
                {
                    protobufData = _ruleExecutor.TransformDeserializedPayload(protobufData, ruleContext);
                }
                finally
                {
                    ruleContext.Return();
                }
            }
            else
            {
                var migration = _migrationRunner.Transform(
                    protobufData,
                    schemaId,
                    subject,
                    schema!,
                    context,
                    SchemaRegistryPayloadFormat.Protobuf);
                protobufData = migration.Payload;
            }
        }

        // Parse directly from span — zero allocation (Google.Protobuf 3.21+).
        // IBufferMessage constraint is enforced at compile time.
        return _parser.ParseFrom(protobufData.Span);
    }

    private GuidResolvedSchema GetGuidSchemaCached(Guid schemaGuid, SerializationContext context)
    {
        var key = new GuidTopicKey(
            schemaGuid,
            context.Topic,
            context.Component == SerializationComponent.Key,
            _subjectNames?.Generation ?? 0);
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

    private bool HasResolvedGuidSchema(GuidTopicKey key) =>
        _guidSchemaCache.TryGetValue(key, out var lazy)
        && lazy.IsValueCreated
        && lazy.Value.IsCompletedSuccessfully;

    private Task<GuidResolvedSchema> GetGuidSchemaAsync(
        GuidTopicKey key,
        CancellationToken cancellationToken)
    {
        var lazy = _guidSchemaCache.GetOrAdd(
            key,
            static (cacheKey, deserializer) => deserializer.CreateGuidSchemaLazy(cacheKey),
            this);
        return lazy.Value.WaitAsync(cancellationToken);
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
        if (unscopedSchema.SchemaType != SchemaType.Protobuf)
        {
            throw new InvalidOperationException(
                $"Schema with GUID {key.SchemaGuid:D} is not a Protobuf schema. Type: {unscopedSchema.SchemaType}");
        }

        if (_ruleExecutor is null && _migrationRunner is null)
            return new GuidResolvedSchema(-1, null, unscopedSchema);

        var subject = _subjectNames is null
            ? SubjectNameResolver.GetTopicSubjectName(key.Topic, key.IsKey)
            : await _subjectNames.ResolveSubjectNameAsync(
                    unscopedSchema,
                    key.Topic,
                    key.IsKey,
                    RecordName,
                    cancellationToken)
                .ConfigureAwait(false);
        var registered = await _schemaRegistry.LookupSchemaAsync(
                subject,
                unscopedSchema,
                ignoreDeletedSchemas: true,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (registered.Schema.SchemaType != SchemaType.Protobuf)
        {
            throw new InvalidOperationException(
                $"Schema with GUID {key.SchemaGuid:D} resolved to type {registered.Schema.SchemaType}; expected {SchemaType.Protobuf}.");
        }
        if (!Guid.TryParse(registered.Guid, out var registeredGuid) || registeredGuid != key.SchemaGuid)
        {
            throw new InvalidDataException(
                $"Schema Registry returned a conflicting GUID for subject '{subject}'.");
        }

        return new GuidResolvedSchema(
            registered.Id,
            subject,
            registered.Schema);
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

    private Header? FindCallerIdentityHeader(SerializationContext context)
    {
        if (_config.SchemaIdStrategy == SchemaIdDeserializerStrategy.Prefix
            || context.Headers is not { } headers
            || !headers.TryGetLastSchemaIdentity(context.Component, out var header))
        {
            return null;
        }

        return header;
    }

    private Header? FindRoutedIdentityHeader(
        SerializationContext context,
        in RecordHeaderRoutingLookup headers) =>
        _config.SchemaIdStrategy != SchemaIdDeserializerStrategy.Prefix
        && headers.TryGetLast(GetIdentityHeaderName(context.Component), out var header)
            ? header
            : null;

    private readonly record struct GuidTopicKey(
        Guid SchemaGuid,
        string Topic,
        bool IsKey,
        int SubjectGeneration);

    private sealed record GuidResolvedSchema(int SchemaId, string? Subject, Schema Schema);

    private string GetSubjectName(int schemaId, Schema? schema, SerializationContext context)
    {
        var isKey = context.Component == SerializationComponent.Key;
        return _subjectNames?.GetSubjectName(
                schemaId,
                schema,
                context.Topic,
                isKey,
                RecordName)
            ?? SubjectNameResolver.GetTopicSubjectName(context.Topic, isKey);
    }

    private static (int value, int bytesRead) ReadVarint(ReadOnlySpan<byte> data, bool useDeprecatedFormat)
    {
        ulong rawValue = 0;
        var shift = 0;
        var bytesRead = 0;

        foreach (var b in data)
        {
            bytesRead++;
            rawValue |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return (DecodeVarint(rawValue, useDeprecatedFormat), bytesRead);
            shift += 7;

            if (shift >= 35)
                throw new InvalidOperationException("Varint is too long");
        }

        throw new InvalidOperationException("Varint is truncated");
    }

    private static int DecodeVarint(ulong rawValue, bool useDeprecatedFormat)
    {
        if (useDeprecatedFormat)
        {
            if (rawValue > int.MaxValue)
                throw new InvalidOperationException("Varint value is too large");

            return (int)rawValue;
        }

        var decoded = (long)(rawValue >> 1);
        if ((rawValue & 1) != 0)
            decoded = ~decoded;

        if (decoded is < int.MinValue or > int.MaxValue)
            throw new InvalidOperationException("Varint value is too large");

        return (int)decoded;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}
