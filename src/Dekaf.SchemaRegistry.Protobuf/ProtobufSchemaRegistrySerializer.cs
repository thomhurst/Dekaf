using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using Dekaf.Serialization;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Dekaf.SchemaRegistry.Protobuf;

/// <summary>
/// Protobuf serializer that integrates with Confluent Schema Registry.
/// Wire format: [magic byte (0x00)] [schema ID (4 bytes)] [varint array indexes] [protobuf binary]
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
    : ISerializer<T>, IAsyncSerializerPreparer<T>, IAsyncDisposable
    where T : IMessage<T>
{
    private const byte MagicByte = 0x00;
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly ProtobufSerializerConfig _config;
    private readonly IAsyncSubjectNameStrategy? _asyncSubjectNameStrategy;
    private readonly bool _ownsClient;
    private readonly MessageDescriptor _descriptor;
    private readonly SubjectSchemaIdCache _subjectSchemaIdCache = new();
    private readonly string _schemaString;
    private readonly byte[] _encodedMessageIndexes;
    private readonly SchemaResolutionCache<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> _schemaResolutionCache = new();
    private readonly SchemaResolutionCache<RegisteredDependency> _referenceResolutionCache = new();
    private readonly Schema _resolutionIdentitySchema;

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
        if (_config.CustomSubjectNameStrategy is null
            && _config.SubjectNameStrategy == SubjectNameStrategy.AssociatedName)
        {
            _asyncSubjectNameStrategy = new AssociatedNameStrategy(schemaRegistry);
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

        // Total size: magic byte + schema ID + indexes + message
        var protobufPayloadLength = _config.RuleExecutor is null ? protoSize : transformedPayload.Length;
        var totalSize = 1 + 4 + _encodedMessageIndexes.Length + protobufPayloadLength;
        var span = destination.GetSpan(totalSize);

        // Write magic byte
        span[0] = MagicByte;

        // Write schema ID (big-endian)
        BinaryPrimitives.WriteInt32BigEndian(span.Slice(1, 4), schemaId);

        var offset = 5;
        _encodedMessageIndexes.CopyTo(span.Slice(offset));
        offset += _encodedMessageIndexes.Length;

        // Write the protobuf message
        if (_config.RuleExecutor is null)
            value.WriteTo(span.Slice(offset, protoSize));
        else
            transformedPayload.Span.CopyTo(span.Slice(offset, transformedPayload.Length));

        destination.Advance(totalSize);
    }

    private SubjectSchemaIdCache.SubjectSchemaIdCacheEntry GetSchemaForContext(string topic, bool isKey) =>
        _subjectSchemaIdCache.GetOrAdd(
            topic,
            isKey,
            this,
            static (serializer, resolvedTopic, resolvedIsKey) =>
                serializer.ResolveSchemaSync(resolvedTopic, resolvedIsKey));

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
            resolved.Schema);
    }

    private ValueTask<ResolvedSchemaContext> PrepareCoreAsync(
        string topic,
        bool isKey,
        CancellationToken cancellationToken)
    {
        if (_asyncSubjectNameStrategy is not null)
            return PrepareAssociatedCoreAsync(topic, isKey, cancellationToken);

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
                _subjectSchemaIdCache.CacheEntry(
                    topic,
                    isKey,
                    subject,
                    value.SchemaId,
                    value.Schema!)));
        }

        return AwaitSchemaAsync(this, topic, isKey, subject, resolved);

        static async ValueTask<ResolvedSchemaContext> AwaitSchemaAsync(
            ProtobufSchemaRegistrySerializer<T> serializer,
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

    private async ValueTask<ResolvedSchemaContext> PrepareAssociatedCoreAsync(
        string topic,
        bool isKey,
        CancellationToken cancellationToken)
    {
        var subject = await _asyncSubjectNameStrategy!.GetSubjectNameAsync(
            topic,
            _descriptor.FullName,
            isKey,
            cancellationToken).ConfigureAwait(false);
        var value = await ResolveSchemaSharedAsync(subject, topic, isKey, cancellationToken)
            .ConfigureAwait(false);
        return ToResolvedContext(_subjectSchemaIdCache.CacheEntry(
            topic,
            isKey,
            subject,
            value.SchemaId,
            value.Schema!));
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
        !_config.UseLatestVersion &&
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
        if (_config.UseLatestVersion)
        {
            var latest = await _schemaRegistry.GetSchemaBySubjectAsync(
                subject,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(latest.Id, latest.Schema);
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

        if (_config.AutoRegisterSchemas)
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
            return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(id, executionSchema);
        }

        var registered = await _schemaRegistry.LookupSchemaAsync(
            subject,
            schema,
            ignoreDeletedSchemas: true,
            normalize: _config.NormalizeSchemas,
            cancellationToken).ConfigureAwait(false);
        return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(registered.Id, registered.Schema);
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

    private sealed class ReferenceRegistrationState(string topic, bool isKey)
    {
        internal string Topic { get; } = topic;
        internal bool IsKey { get; } = isKey;
        internal Dictionary<string, RegisteredDependency> Completed { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> Visiting { get; } = new(StringComparer.Ordinal);
    }

    private readonly record struct RegisteredDependency(string Subject, int Version);

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
}
