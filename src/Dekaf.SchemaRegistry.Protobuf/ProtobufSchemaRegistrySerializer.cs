using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Dekaf.Serialization;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Dekaf.SchemaRegistry.Protobuf;

/// <summary>
/// Protobuf serializer that integrates with Confluent Schema Registry.
/// Prefix framing writes [magic byte (0x00)] [schema ID (4 bytes)] [varint array indexes]
/// [protobuf binary]. Header framing writes the GUID and message indexes to the schema-identity
/// record header and leaves the payload as raw Protobuf binary.
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
/// <typeparam name="T">The Protobuf message type to serialize.</typeparam>
public sealed class ProtobufSchemaRegistrySerializer<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>
    : ISerializer<T>, IAsyncSerializerPreparer<T>, IAsyncSerializerPreparationAdmission<T>,
      IRecordHeaderSerializer, IAsyncDisposable
    where T : IMessage<T>
{
    private const string RegistrySerializedRootName = "default";
    private const string SerializedSchemaFormat = "serialized";
    private const int MaxAssociatedNameInvalidationRetries = 4;
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly ProtobufSerializerConfig _config;
    private readonly SchemaIdSerializerStrategy _schemaIdStrategy;
    private readonly SchemaSelectionMode _schemaSelectionMode;
    private readonly IAsyncSubjectNameStrategy? _asyncSubjectNameStrategy;
    private readonly bool _ownsClient;
    private readonly MessageDescriptor _descriptor;
    private readonly SubjectSchemaIdCache _subjectSchemaIdCache = new();
    private readonly string _schemaString;
    private readonly byte[] _encodedMessageIndexes;
    private readonly SchemaResolutionCache<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> _schemaResolutionCache = new();
    private readonly SchemaResolutionCache<RegisteredDependency> _referenceResolutionCache = new();
    private readonly Schema _resolutionIdentitySchema;
    private readonly ConditionalWeakTable<Schema, IdentityHeaderFrame> _identityHeaderFrames = new();
    private SubjectSchemaIdCache? _associatedSubjectSchemaIdCache;

    bool IRecordHeaderSerializer.ProducesRecordHeaders =>
        _schemaIdStrategy == SchemaIdSerializerStrategy.Header;

    /// <summary>
    /// Creates a new Protobuf Schema Registry serializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="config">Optional serializer configuration.</param>
    /// <param name="ownsClient">Whether this serializer owns the client and should dispose it.</param>
    public ProtobufSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        ProtobufSerializerConfig? config = null,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _config = config ?? new ProtobufSerializerConfig();
        _schemaIdStrategy = _config.SchemaIdStrategy;
        _schemaSelectionMode = SchemaRegistrySerializerConfigValidator.ValidateAndResolve(
            _config.UseSchemaId,
            _config.UseLatestVersion,
            _config.AutoRegisterSchemas);
        if (_schemaIdStrategy is not (SchemaIdSerializerStrategy.Prefix or SchemaIdSerializerStrategy.Header))
            throw new ArgumentOutOfRangeException(nameof(config), _schemaIdStrategy, "Unknown schema identity strategy.");
        if (_config.CustomSubjectNameStrategy is null)
        {
            _asyncSubjectNameStrategy = _config.AsyncSubjectNameStrategy
                ?? (_config.SubjectNameStrategy == SubjectNameStrategy.AssociatedName
                    ? new AssociatedNameStrategy(schemaRegistry)
                    : null);
        }
        _ownsClient = ownsClient;

        // Get the message descriptor from the type
        _descriptor = GetMessageDescriptor();

        // Schema Registry's canonical Protobuf representation is the serialized FileDescriptorProto.
        _schemaString = _descriptor.File.SerializedData.ToBase64();
        _resolutionIdentitySchema = new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = _schemaString
        };

        // Pre-encode the immutable message index path once per serializer.
        _encodedMessageIndexes = VarintEncoder.EncodeMessageIndexes(
            CalculateMessageIndexes(_descriptor),
            _config.UseDeprecatedFormat);
        if (_asyncSubjectNameStrategy is not null)
            _associatedSubjectSchemaIdCache = new SubjectSchemaIdCache();

        if (_asyncSubjectNameStrategy is AssociatedNameStrategy associatedNameStrategy)
        {
            AssociatedNameCacheInvalidationTargetRegistration.Register(
                this,
                associatedNameStrategy,
                InvalidateAssociatedSubjectSchemaCache);
        }
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
        ArgumentNullException.ThrowIfNull(value);

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

    ValueTask<SerializerPreparationAdmission>
        IAsyncSerializerPreparationAdmission<T>.PrepareForSerializationAsync(
            T value,
            SerializationContext context,
            CancellationToken cancellationToken)
    {
        var preparation = PrepareAsync(
            context.Topic,
            value,
            context.Component == SerializationComponent.Key,
            cancellationToken);
        return preparation.IsCompletedSuccessfully
            ? new ValueTask<SerializerPreparationAdmission>(ToAdmission(preparation.Result))
            : AwaitAdmissionAsync(preparation);

        static async ValueTask<SerializerPreparationAdmission> AwaitAdmissionAsync(
            ValueTask<ResolvedSchemaContext> pending) =>
            ToAdmission(await pending.ConfigureAwait(false));
    }

    /// <inheritdoc />
    public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        ArgumentNullException.ThrowIfNull(value);

        var schemaEntry = GetSchemaForContext(context.Topic, context.Component == SerializationComponent.Key);
        var schemaId = schemaEntry.SchemaId;

        var protoSize = value.CalculateSize();
        ReadOnlyMemory<byte> transformedPayload = default;
        if (_config.RuleExecutor is not null)
        {
            var ruleContext = SchemaRegistryRuleContext.Rent(
                context.Topic,
                context.Component,
                schemaId,
                schemaEntry.Subject,
                schemaEntry.Schema,
                SchemaRegistryPayloadFormat.Protobuf);
            try
            {
                transformedPayload = _config.RuleExecutor.TransformSerializedPayload(value.ToByteArray(), ruleContext);
            }
            finally
            {
                ruleContext.Return();
            }
        }

        var protobufPayloadLength = _config.RuleExecutor is null ? protoSize : transformedPayload.Length;
        var payloadOffset = _schemaIdStrategy == SchemaIdSerializerStrategy.Prefix
            ? SchemaIdentityFraming.SchemaIdFrameSize + _encodedMessageIndexes.Length
            : 0;
        var totalSize = payloadOffset + protobufPayloadLength;
        var span = destination.GetSpan(totalSize);

        if (_schemaIdStrategy == SchemaIdSerializerStrategy.Prefix)
        {
            SchemaIdentityFraming.WriteSchemaId(span, schemaId);
            _encodedMessageIndexes.CopyTo(span[SchemaIdentityFraming.SchemaIdFrameSize..]);
        }

        if (_config.RuleExecutor is null)
            value.WriteTo(span.Slice(payloadOffset, protoSize));
        else
            transformedPayload.Span.CopyTo(span.Slice(payloadOffset, transformedPayload.Length));

        if (_schemaIdStrategy == SchemaIdSerializerStrategy.Header)
            WriteIdentityHeader(context, in schemaEntry);

        destination.Advance(totalSize);
    }

    void IAsyncSerializerPreparationAdmission<T>.SerializePrepared<TWriter>(
        T value,
        ref TWriter destination,
        SerializationContext context,
        in SerializerPreparationAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(value);
        var schemaEntry = SubjectSchemaIdCache.FromAdmission(
            context.Topic,
            context.Component == SerializationComponent.Key,
            admission);
        SerializeCore(value, ref destination, context, schemaEntry);
    }

    // Keep the public Serialize body inline; routing it through this helper measured 5.5% slower.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SerializeCore<TWriter>(
        T value,
        ref TWriter destination,
        SerializationContext context,
        SubjectSchemaIdCache.SubjectSchemaIdCacheEntry schemaEntry)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        var schemaId = schemaEntry.SchemaId;

        var protoSize = value.CalculateSize();
        ReadOnlyMemory<byte> transformedPayload = default;
        if (_config.RuleExecutor is not null)
        {
            var ruleContext = SchemaRegistryRuleContext.Rent(
                context.Topic,
                context.Component,
                schemaId,
                schemaEntry.Subject,
                schemaEntry.Schema,
                SchemaRegistryPayloadFormat.Protobuf);
            try
            {
                transformedPayload = _config.RuleExecutor.TransformSerializedPayload(value.ToByteArray(), ruleContext);
            }
            finally
            {
                ruleContext.Return();
            }
        }
        var protobufPayloadLength = _config.RuleExecutor is null ? protoSize : transformedPayload.Length;
        var payloadOffset = _schemaIdStrategy == SchemaIdSerializerStrategy.Prefix
            ? SchemaIdentityFraming.SchemaIdFrameSize + _encodedMessageIndexes.Length
            : 0;
        var totalSize = payloadOffset + protobufPayloadLength;
        var span = destination.GetSpan(totalSize);

        if (_schemaIdStrategy == SchemaIdSerializerStrategy.Prefix)
        {
            SchemaIdentityFraming.WriteSchemaId(span, schemaId);
            _encodedMessageIndexes.CopyTo(span[SchemaIdentityFraming.SchemaIdFrameSize..]);
        }

        // Write the protobuf message
        if (_config.RuleExecutor is null)
            value.WriteTo(span.Slice(payloadOffset, protoSize));
        else
            transformedPayload.Span.CopyTo(span.Slice(payloadOffset, transformedPayload.Length));

        if (_schemaIdStrategy == SchemaIdSerializerStrategy.Header)
            WriteIdentityHeader(context, in schemaEntry);

        destination.Advance(totalSize);
    }

    private static SerializerPreparationAdmission ToAdmission(
        in ResolvedSchemaContext context) =>
        new(context.Subject, context.SchemaId, context.Schema);

    private SubjectSchemaIdCache.SubjectSchemaIdCacheEntry GetSchemaForContext(string topic, bool isKey)
    {
        if (_subjectSchemaIdCache.TryGet(topic, isKey, out var cached))
            return cached;

        return GetSchemaForContextSlow(topic, isKey);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private SubjectSchemaIdCache.SubjectSchemaIdCacheEntry GetSchemaForContextSlow(string topic, bool isKey)
    {
        var cache = _asyncSubjectNameStrategy is null
            ? _subjectSchemaIdCache
            : Volatile.Read(ref _associatedSubjectSchemaIdCache)!;
        if (cache.TryGet(topic, isKey, out var cached))
            return cached;

        if (_asyncSubjectNameStrategy is not null)
        {
            throw new InvalidOperationException(
                "The asynchronous subject-name strategy requires PrepareAsync before serialization.");
        }

        return cache.GetOrAdd(
            topic,
            isKey,
            this,
            static (serializer, resolvedTopic, resolvedIsKey) =>
                serializer.ResolveSchemaSync(resolvedTopic, resolvedIsKey));
    }

    private SubjectSchemaIdCache.SubjectSchemaIdCacheEntry ResolveSchemaSync(string topic, bool isKey)
    {
        var subject = GetSubjectName(topic, isKey);
        var resolved = ResolveSchemaShared(
            subject,
            topic,
            isKey,
            SchemaRegistryTimeout);

        return new SubjectSchemaIdCache.SubjectSchemaIdCacheEntry(
            new SubjectSchemaIdCache.SubjectSchemaIdCacheKey(topic, isKey),
            subject,
            resolved.SchemaId,
            resolved.Schema,
            resolved.SchemaGuidFrame);
    }

    private ValueTask<ResolvedSchemaContext> PrepareCoreAsync(
        string topic,
        bool isKey,
        CancellationToken cancellationToken)
    {
        if (_asyncSubjectNameStrategy is not null)
        {
            var cache = Volatile.Read(ref _associatedSubjectSchemaIdCache)!;
            if (cache.TryGet(topic, isKey, out var cached))
                return new ValueTask<ResolvedSchemaContext>(ToResolvedContext(cached));

            return PrepareAssociatedCoreAsync(
                topic,
                isKey,
                cache,
                cancellationToken);
        }

        var standardCache = _subjectSchemaIdCache;
        var subject = GetSubjectName(topic, isKey);
        var resolved = ResolveSchemaSharedAsync(
            subject,
            topic,
            isKey,
            cancellationToken);
        if (resolved.IsCompletedSuccessfully)
        {
            var value = resolved.Result;
            return new ValueTask<ResolvedSchemaContext>(ToResolvedContext(
                standardCache.CacheEntry(
                    topic,
                    isKey,
                    subject,
                    in value)));
        }

        return AwaitSchemaAsync(topic, isKey, subject, standardCache, resolved);

        static async ValueTask<ResolvedSchemaContext> AwaitSchemaAsync(
            string topic,
            bool isKey,
            string subject,
            SubjectSchemaIdCache cache,
            ValueTask<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> resolved)
        {
            var value = await resolved.ConfigureAwait(false);
            return ToResolvedContext(cache.CacheEntry(
                topic,
                isKey,
                subject,
                in value));
        }
    }

    private async ValueTask<ResolvedSchemaContext> PrepareAssociatedCoreAsync(
        string topic,
        bool isKey,
        SubjectSchemaIdCache cache,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAssociatedNameInvalidationRetries; attempt++)
        {
            var subject = await _asyncSubjectNameStrategy!.GetSubjectNameAsync(
                topic,
                _descriptor.FullName,
                isKey,
                cancellationToken).ConfigureAwait(false);
            var value = await ResolveSchemaSharedAsync(subject, topic, isKey, cancellationToken)
                .ConfigureAwait(false);
            if (ReferenceEquals(cache, Volatile.Read(ref _associatedSubjectSchemaIdCache)))
            {
                var cached = cache.CacheEntry(
                    topic,
                    isKey,
                    subject,
                    in value);
                if (ReferenceEquals(cache, Volatile.Read(ref _associatedSubjectSchemaIdCache)))
                    return ToResolvedContext(cached);
            }

            cache = Volatile.Read(ref _associatedSubjectSchemaIdCache)!;
            if (cache.TryGet(topic, isKey, out var current))
                return ToResolvedContext(current);
        }

        throw new InvalidOperationException(
            "Associated-name cache changed repeatedly while preparing the Protobuf serializer.");
    }

    private ValueTask<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> ResolveSchemaSharedAsync(
        string subject,
        string topic,
        bool isKey,
        CancellationToken cancellationToken) =>
        _schemaResolutionCache.ResolveAsync(
            subject,
            _resolutionIdentitySchema,
            GetSchemaResolutionScope(topic, isKey),
            new SchemaResolutionState(this, topic, isKey),
            static (state, resolvedSubject, _) => state.Serializer.ResolveSchemaWithTimeoutAsync(
                resolvedSubject,
                state.Topic,
                state.IsKey),
            cancellationToken);

    private SubjectSchemaIdCache.SubjectSchemaIdCacheValue ResolveSchemaShared(
        string subject,
        string topic,
        bool isKey,
        TimeSpan timeout) =>
        _schemaResolutionCache.Resolve(
            subject,
            _resolutionIdentitySchema,
            GetSchemaResolutionScope(topic, isKey),
            new SchemaResolutionState(this, topic, isKey),
            static (state, resolvedSubject, _) => state.Serializer.ResolveSchemaWithTimeoutAsync(
                resolvedSubject,
                state.Topic,
                state.IsKey),
            timeout);

    private SchemaResolutionScope GetSchemaResolutionScope(string topic, bool isKey) =>
        _config.UseSchemaReferences &&
        _schemaSelectionMode is SchemaSelectionMode.AutoRegister or SchemaSelectionMode.Lookup &&
        _config.CustomReferenceSubjectNameStrategy is not null
            ? new SchemaResolutionScope(topic, isKey)
            : default;

    private Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> ResolveSchemaWithTimeoutAsync(
        string subject,
        string topic,
        bool isKey) =>
        SchemaRegistryOperationTimeout.ExecuteAsync(
            cancellationToken => ResolveSchemaAsync(subject, topic, isKey, cancellationToken),
            SchemaRegistryTimeout,
            "Schema Registry resolution timed out.");

    private async Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> ResolveSchemaAsync(
        string subject,
        string topic,
        bool isKey,
        CancellationToken cancellationToken)
    {
        if (_schemaSelectionMode == SchemaSelectionMode.ExplicitId)
        {
            var schemaId = _config.UseSchemaId!.Value;
            var explicitSchema = await GetSerializedSchemaAsync(
                    schemaId,
                    subject,
                    cancellationToken)
                .ConfigureAwait(false);
            await ValidateExplicitSchemaAsync(
                    schemaId,
                    explicitSchema,
                    cancellationToken)
                .ConfigureAwait(false);

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
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (latest.Schema.SchemaType != SchemaType.Protobuf)
            {
                throw new InvalidOperationException(
                    $"Schema ID {latest.Id} has format {latest.Schema.SchemaType}; expected {SchemaType.Protobuf}.");
            }

            return await CreateResolvedValueAsync(
                    subject,
                    latest.Id,
                    latest.Schema,
                    latest,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        IReadOnlyList<SchemaReference>? references = null;
        if (_config.UseSchemaReferences)
        {
            references = await RegisterOrLookupReferencesAsync(
                _descriptor.File,
                topic,
                isKey,
                cancellationToken).ConfigureAwait(false);
        }

        var schema = new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = _schemaString,
            References = references
        };

        if (_schemaSelectionMode == SchemaSelectionMode.AutoRegister)
        {
            var id = _config.NormalizeSchemas
                ? await _schemaRegistry.GetOrRegisterSchemaAsync(
                    subject,
                    schema,
                    normalize: true,
                    cancellationToken).ConfigureAwait(false)
                : await _schemaRegistry.GetOrRegisterSchemaAsync(
                    subject,
                    schema,
                    cancellationToken).ConfigureAwait(false);
            var executionSchema = _config.RuleExecutor is SchemaRegistryRuleExecutor
                ? await _schemaRegistry.GetSchemaAsync(id, subject, cancellationToken).ConfigureAwait(false)
                : schema;
            return await CreateResolvedValueAsync(
                    subject,
                    id,
                    executionSchema,
                    registeredSchema: null,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var registered = await _schemaRegistry.LookupSchemaAsync(
            subject,
            schema,
            ignoreDeletedSchemas: true,
            normalize: _config.NormalizeSchemas,
            cancellationToken).ConfigureAwait(false);
        return await CreateResolvedValueAsync(
                subject,
                registered.Id,
                registered.Schema,
                registered,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ValidateExplicitSchemaAsync(
        int schemaId,
        Schema schema,
        CancellationToken cancellationToken)
    {
        ValidateDescriptor(schemaId, _descriptor.File, schema);
        if (!_config.UseSchemaReferences)
            return;

        var resolvedReferences = new Dictionary<SchemaReferenceKey, Schema>();
        await ValidateReferencesAsync(
                schemaId,
                _descriptor.File,
                schema,
                resolvedReferences,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ValidateReferencesAsync(
        int schemaId,
        FileDescriptor descriptor,
        Schema schema,
        Dictionary<SchemaReferenceKey, Schema> resolvedReferences,
        CancellationToken cancellationToken)
    {
        var references = schema.References;
        var referenceCount = references?.Count ?? 0;
        var matchedReferenceCount = 0;

        for (var dependencyIndex = 0; dependencyIndex < descriptor.Dependencies.Count; dependencyIndex++)
        {
            var dependency = descriptor.Dependencies[dependencyIndex];
            SchemaReference? matchingReference = null;
            for (var referenceIndex = 0; referenceIndex < referenceCount; referenceIndex++)
            {
                var candidate = references![referenceIndex];
                if (!string.Equals(candidate.Name, dependency.Name, StringComparison.Ordinal))
                    continue;
                if (matchingReference is not null)
                {
                    throw new InvalidOperationException(
                        $"Schema ID {schemaId} contains duplicate Protobuf reference '{dependency.Name}'.");
                }

                matchingReference = candidate;
            }

            if (matchingReference is null)
            {
                if (IsKnownType(dependency.Name))
                    continue;

                throw new InvalidOperationException(
                    $"Schema ID {schemaId} is missing Protobuf reference '{dependency.Name}'.");
            }

            matchedReferenceCount++;
            var key = new SchemaReferenceKey(matchingReference.Subject, matchingReference.Version);
            if (!resolvedReferences.TryGetValue(key, out var resolvedSchema))
            {
                var registered = await GetSerializedSchemaBySubjectAsync(
                        matchingReference.Subject,
                        matchingReference.Version.ToString(CultureInfo.InvariantCulture),
                        cancellationToken)
                    .ConfigureAwait(false);
                resolvedSchema = registered.Schema;
                resolvedReferences.Add(key, resolvedSchema);
            }

            ValidateDescriptor(schemaId, dependency, resolvedSchema);
            await ValidateReferencesAsync(
                    schemaId,
                    dependency,
                    resolvedSchema,
                    resolvedReferences,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (matchedReferenceCount != referenceCount)
        {
            throw new InvalidOperationException(
                $"Schema ID {schemaId} does not match the Protobuf reference graph for '{_descriptor.FullName}'.");
        }
    }

    private void ValidateDescriptor(int schemaId, FileDescriptor descriptor, Schema schema)
    {
        if (schema.SchemaType != SchemaType.Protobuf)
        {
            throw new InvalidOperationException(
                $"Schema ID {schemaId} has format {schema.SchemaType}; expected {SchemaType.Protobuf}.");
        }

        if (!string.Equals(schema.SchemaString, descriptor.SerializedData.ToBase64(), StringComparison.Ordinal)
            && !MatchesDescriptorIgnoringFileName(schema.SchemaString, descriptor))
        {
            throw new InvalidOperationException(
                $"Schema ID {schemaId} does not match Protobuf message type '{_descriptor.FullName}'.");
        }
    }

    private static bool MatchesDescriptorIgnoringFileName(string schemaString, FileDescriptor descriptor)
    {
        try
        {
            var selectedDescriptor = FileDescriptorProto.Parser.ParseFrom(
                Convert.FromBase64String(schemaString));
            if (!string.Equals(selectedDescriptor.Name, RegistrySerializedRootName, StringComparison.Ordinal))
                return false;

            var expectedDescriptor = descriptor.ToProto();
            selectedDescriptor.Name = expectedDescriptor.Name;
            return selectedDescriptor.Equals(expectedDescriptor);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (InvalidProtocolBufferException)
        {
            return false;
        }
    }

    private Task<Schema> GetSerializedSchemaAsync(
        int schemaId,
        string subject,
        CancellationToken cancellationToken) =>
        GetFormattedSchemaRegistryClient().GetSchemaWithFormatAsync(
            schemaId,
            subject,
            SerializedSchemaFormat,
            cancellationToken);

    private Task<RegisteredSchema> GetSerializedSchemaBySubjectAsync(
        string subject,
        string version,
        CancellationToken cancellationToken) =>
        GetFormattedSchemaRegistryClient().GetSchemaBySubjectWithFormatAsync(
            subject,
            version,
            ignoreDeletedSchemas: true,
            SerializedSchemaFormat,
            cancellationToken);

    private IFormattedSchemaRegistryClient GetFormattedSchemaRegistryClient() =>
        _schemaRegistry as IFormattedSchemaRegistryClient
        ?? throw new NotSupportedException(
            $"{_schemaRegistry.GetType().Name} does not support formatted schema lookup. " +
            $"Implement {nameof(IFormattedSchemaRegistryClient)} to use explicit Protobuf schema IDs.");

    private async Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> CreateResolvedValueAsync(
        string subject,
        int schemaId,
        Schema schema,
        RegisteredSchema? registeredSchema,
        CancellationToken cancellationToken)
    {
        var resolved = await SchemaIdentityResolution.CreateSerializerValueAsync(
                _schemaRegistry,
                subject,
                schemaId,
                schema,
                _schemaIdStrategy,
                _config.NormalizeSchemas,
                registeredSchema,
                cancellationToken)
            .ConfigureAwait(false);
        if (_schemaIdStrategy == SchemaIdSerializerStrategy.Header)
            CacheIdentityHeaderFrame(resolved.Schema!, resolved.SchemaId, resolved.SchemaGuidFrame);

        return resolved;
    }

    private void CacheIdentityHeaderFrame(Schema schema, int schemaId, byte[]? encodedGuid)
    {
        if (encodedGuid is null)
        {
            throw new InvalidDataException(
                $"Schema Registry GUID framing is unavailable for schema ID {schemaId}.");
        }

        var frame = GC.AllocateUninitializedArray<byte>(encodedGuid.Length + _encodedMessageIndexes.Length);
        encodedGuid.AsSpan().CopyTo(frame);
        _encodedMessageIndexes.CopyTo(frame.AsSpan(encodedGuid.Length));
        _identityHeaderFrames.AddOrUpdate(schema, new IdentityHeaderFrame(frame));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteIdentityHeader(
        SerializationContext context,
        in SubjectSchemaIdCache.SubjectSchemaIdCacheEntry schemaEntry)
    {
        if (context.Headers is not { } headers)
        {
            throw new InvalidOperationException(
                "Header schema identity framing requires a record Headers collection.");
        }

        if (!_identityHeaderFrames.TryGetValue(schemaEntry.Schema!, out var frame))
        {
            throw new InvalidDataException(
                $"Schema Registry GUID framing is unavailable for schema ID {schemaEntry.SchemaId}.");
        }

        headers.Add(SchemaIdentityFraming.CreateSchemaGuidHeader(context.Component, frame.Value));
    }

    private string GetSubjectName(string topic, bool isKey)
    {
        if (_config.CustomSubjectNameStrategy is not null)
        {
            return _config.CustomSubjectNameStrategy.GetSubjectName(topic, _descriptor.FullName, isKey);
        }

        return SubjectNameResolver.GetSubjectName(
            _config.SubjectNameStrategy,
            topic,
            _descriptor.FullName,
            isKey,
            _config.UseLegacySubjectNames);
    }

    private static MessageDescriptor GetMessageDescriptor()
    {
        // Get the Descriptor property from the message type
        var descriptorProperty = typeof(T).GetProperty("Descriptor",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        if (descriptorProperty == null)
            throw new InvalidOperationException($"Type {typeof(T).Name} does not have a static Descriptor property");

        var descriptor = descriptorProperty.GetValue(null) as MessageDescriptor;

        if (descriptor == null)
            throw new InvalidOperationException($"Could not get MessageDescriptor for type {typeof(T).Name}");

        return descriptor;
    }

    private async Task<IReadOnlyList<SchemaReference>?> RegisterOrLookupReferencesAsync(
        FileDescriptor root,
        string topic,
        bool isKey,
        CancellationToken cancellationToken)
    {
        var state = new ReferenceRegistrationState(topic, isKey);
        return await ResolveDirectReferencesAsync(root, state, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<SchemaReference>?> ResolveDirectReferencesAsync(
        FileDescriptor file,
        ReferenceRegistrationState state,
        CancellationToken cancellationToken)
    {
        List<SchemaReference>? references = null;
        for (var index = 0; index < file.Dependencies.Count; index++)
        {
            var dependency = file.Dependencies[index];
            if (_config.SkipKnownTypes && IsKnownType(dependency.Name))
                continue;

            var registered = await RegisterOrLookupDependencyAsync(
                dependency,
                state,
                cancellationToken).ConfigureAwait(false);
            (references ??= []).Add(new SchemaReference
            {
                Name = dependency.Name,
                Subject = registered.Subject,
                Version = registered.Version
            });
        }

        return references;
    }

    private async Task<RegisteredDependency> RegisterOrLookupDependencyAsync(
        FileDescriptor dependency,
        ReferenceRegistrationState state,
        CancellationToken cancellationToken)
    {
        if (state.Completed.TryGetValue(dependency.Name, out var completed))
            return completed;

        if (!state.Visiting.Add(dependency.Name))
            throw new InvalidOperationException($"Cyclic Protobuf schema reference detected at '{dependency.Name}'.");

        try
        {
            var references = await ResolveDirectReferencesAsync(
                dependency,
                state,
                cancellationToken).ConfigureAwait(false);
            var schema = new Schema
            {
                SchemaType = SchemaType.Protobuf,
                SchemaString = dependency.SerializedData.ToBase64(),
                References = references
            };
            var subject = GetReferenceSubjectName(state.Topic, dependency.Name, state.IsKey);
            var result = await _referenceResolutionCache.ResolveAsync(
                subject,
                schema,
                this,
                static (serializer, resolvedSubject, resolvedSchema) =>
                    serializer.ResolveDependencyWithTimeoutAsync(resolvedSubject, resolvedSchema),
                cancellationToken).ConfigureAwait(false);
            state.Completed.Add(dependency.Name, result);
            return result;
        }
        finally
        {
            state.Visiting.Remove(dependency.Name);
        }
    }

    private Task<RegisteredDependency> ResolveDependencyWithTimeoutAsync(
        string subject,
        Schema schema) =>
        SchemaRegistryOperationTimeout.ExecuteAsync(
            cancellationToken => ResolveDependencyAsync(subject, schema, cancellationToken),
            SchemaRegistryTimeout,
            "Schema Registry reference resolution timed out.");

    private async Task<RegisteredDependency> ResolveDependencyAsync(
        string subject,
        Schema schema,
        CancellationToken cancellationToken)
    {
        if (_config.AutoRegisterSchemas)
        {
            if (_config.NormalizeSchemas)
            {
                await _schemaRegistry.RegisterSchemaAsync(
                    subject,
                    schema,
                    normalize: true,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _schemaRegistry.RegisterSchemaAsync(
                    subject,
                    schema,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        var registered = await _schemaRegistry.LookupSchemaAsync(
            subject,
            schema,
            ignoreDeletedSchemas: true,
            normalize: _config.NormalizeSchemas,
            cancellationToken).ConfigureAwait(false);
        return new RegisteredDependency(subject, registered.Version);
    }

    private string GetReferenceSubjectName(string topic, string referenceName, bool isKey)
    {
        if (_config.CustomReferenceSubjectNameStrategy is not null)
        {
            return _config.CustomReferenceSubjectNameStrategy.GetSubjectName(
                topic,
                referenceName,
                isKey);
        }

        return _config.ReferenceSubjectNameStrategy switch
        {
            ReferenceSubjectNameStrategy.ReferenceName => referenceName,
            ReferenceSubjectNameStrategy.Qualified => (referenceName.EndsWith(".proto", StringComparison.Ordinal)
                    ? referenceName[..^".proto".Length]
                    : referenceName)
                .Replace('/', '.'),
            _ => throw new InvalidOperationException(
                $"Unknown Protobuf reference subject name strategy: {_config.ReferenceSubjectNameStrategy}.")
        };
    }

    // Keep this exact set aligned with Schema Registry's ProtobufSchema.KNOWN_DEPENDENCIES.
    private static bool IsKnownType(string referenceName) =>
        referenceName is
            "confluent/meta.proto" or
            "confluent/type/decimal.proto" or
            "confluent/type/variant.proto" or
            "google/type/calendar_period.proto" or
            "google/type/color.proto" or
            "google/type/date.proto" or
            "google/type/datetime.proto" or
            "google/type/dayofweek.proto" or
            "google/type/decimal.proto" or
            "google/type/expr.proto" or
            "google/type/fraction.proto" or
            "google/type/interval.proto" or
            "google/type/latlng.proto" or
            "google/type/money.proto" or
            "google/type/month.proto" or
            "google/type/phone_number.proto" or
            "google/type/postal_address.proto" or
            "google/type/quaternion.proto" or
            "google/type/timeofday.proto" or
            "google/protobuf/any.proto" or
            "google/protobuf/api.proto" or
            "google/protobuf/descriptor.proto" or
            "google/protobuf/duration.proto" or
            "google/protobuf/empty.proto" or
            "google/protobuf/field_mask.proto" or
            "google/protobuf/source_context.proto" or
            "google/protobuf/struct.proto" or
            "google/protobuf/timestamp.proto" or
            "google/protobuf/type.proto" or
            "google/protobuf/wrappers.proto";

    private static ResolvedSchemaContext ToResolvedContext(
        SubjectSchemaIdCache.SubjectSchemaIdCacheEntry entry) =>
        new(entry.Subject!, entry.SchemaId, entry.Schema!);

    private readonly record struct SchemaResolutionState(
        ProtobufSchemaRegistrySerializer<T> Serializer,
        string Topic,
        bool IsKey);

    private readonly record struct SchemaReferenceKey(string Subject, int Version);

    private sealed class ReferenceRegistrationState(string topic, bool isKey)
    {
        internal string Topic { get; } = topic;
        internal bool IsKey { get; } = isKey;
        internal Dictionary<string, RegisteredDependency> Completed { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> Visiting { get; } = new(StringComparer.Ordinal);
    }

    private readonly record struct RegisteredDependency(string Subject, int Version);

    private sealed class IdentityHeaderFrame(ReadOnlyMemory<byte> value)
    {
        internal ReadOnlyMemory<byte> Value { get; } = value;
    }

    private static int[] CalculateMessageIndexes(MessageDescriptor descriptor)
    {
        var indexes = new List<int>();
        CalculateMessageIndexesRecursive(descriptor, indexes);
        return [.. indexes];
    }

    private static void CalculateMessageIndexesRecursive(MessageDescriptor descriptor, List<int> indexes)
    {
        // Check if this is a nested message
        if (descriptor.ContainingType != null)
        {
            CalculateMessageIndexesRecursive(descriptor.ContainingType, indexes);
            var index = 0;
            foreach (var nested in descriptor.ContainingType.NestedTypes)
            {
                if (nested == descriptor)
                {
                    indexes.Add(index);
                    return;
                }
                index++;
            }
        }
        else
        {
            // Top-level message - find index in file
            var index = 0;
            foreach (var message in descriptor.File.MessageTypes)
            {
                if (message == descriptor)
                {
                    indexes.Add(index);
                    return;
                }
                index++;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }

    private void InvalidateAssociatedSubjectSchemaCache()
    {
        Volatile.Write(ref _associatedSubjectSchemaIdCache, new SubjectSchemaIdCache());
    }
}
