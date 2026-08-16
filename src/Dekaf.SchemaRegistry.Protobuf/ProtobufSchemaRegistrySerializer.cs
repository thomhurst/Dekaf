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
    : ISerializer<T>, IAsyncDisposable
    where T : IMessage<T>
{
    private const byte MagicByte = 0x00;
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly ProtobufSerializerConfig _config;
    private readonly bool _ownsClient;
    private readonly MessageDescriptor _descriptor;
    private readonly SubjectSchemaIdCache _subjectSchemaIdCache = new();
    private readonly string _schemaString;
    private readonly byte[] _encodedMessageIndexes;

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
        _ownsClient = ownsClient;

        // Get the message descriptor from the type
        _descriptor = GetMessageDescriptor();

        // Schema Registry's canonical Protobuf representation is the serialized FileDescriptorProto.
        _schemaString = _descriptor.File.SerializedData.ToBase64();

        // Pre-encode the immutable message index path once per serializer.
        _encodedMessageIndexes = VarintEncoder.EncodeMessageIndexes(
            CalculateMessageIndexes(_descriptor),
            _config.UseDeprecatedFormat);
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
            transformedPayload = _config.RuleExecutor.TransformSerializedPayload(
                value.ToByteArray(),
                new SchemaRegistryRuleContext
                {
                    Topic = context.Topic,
                    Component = context.Component,
                    SchemaId = schemaId,
                    Subject = schemaEntry.Subject,
                    Schema = schemaEntry.Schema,
                    PayloadFormat = SchemaRegistryPayloadFormat.Protobuf
                });
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
        var resolved = ResolveSchemaAsync(subject, topic, isKey)
            .WaitAsync(SchemaRegistryTimeout)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        return new SubjectSchemaIdCache.SubjectSchemaIdCacheEntry(
            new SubjectSchemaIdCache.SubjectSchemaIdCacheKey(topic, isKey),
            subject,
            resolved.SchemaId,
            resolved.Schema);
    }

    private async Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> ResolveSchemaAsync(
        string subject,
        string topic,
        bool isKey)
    {
        if (_config.UseLatestVersion)
        {
            var latest = await _schemaRegistry.GetSchemaBySubjectAsync(subject).ConfigureAwait(false);
            return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(latest.Id, latest.Schema);
        }

        IReadOnlyList<SchemaReference>? references = null;
        if (_config.UseSchemaReferences)
        {
            references = await RegisterOrLookupReferencesAsync(
                _descriptor.File,
                topic,
                isKey).ConfigureAwait(false);
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
                ? await _schemaRegistry.GetOrRegisterSchemaAsync(subject, schema, normalize: true).ConfigureAwait(false)
                : await _schemaRegistry.GetOrRegisterSchemaAsync(subject, schema).ConfigureAwait(false);
            return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(id, schema);
        }

        var registered = await _schemaRegistry.LookupSchemaAsync(
            subject,
            schema,
            ignoreDeletedSchemas: true,
            normalize: _config.NormalizeSchemas).ConfigureAwait(false);
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
        bool isKey)
    {
        var state = new ReferenceRegistrationState(topic, isKey);
        return await ResolveDirectReferencesAsync(root, state).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<SchemaReference>?> ResolveDirectReferencesAsync(
        FileDescriptor file,
        ReferenceRegistrationState state)
    {
        List<SchemaReference>? references = null;
        for (var index = 0; index < file.Dependencies.Count; index++)
        {
            var dependency = file.Dependencies[index];
            if (_config.SkipKnownTypes && IsKnownType(dependency.Name))
                continue;

            var registered = await RegisterOrLookupDependencyAsync(dependency, state).ConfigureAwait(false);
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
        ReferenceRegistrationState state)
    {
        if (state.Completed.TryGetValue(dependency.Name, out var completed))
            return completed;

        if (!state.Visiting.Add(dependency.Name))
            throw new InvalidOperationException($"Cyclic Protobuf schema reference detected at '{dependency.Name}'.");

        try
        {
            var references = await ResolveDirectReferencesAsync(dependency, state).ConfigureAwait(false);
            var schema = new Schema
            {
                SchemaType = SchemaType.Protobuf,
                SchemaString = dependency.SerializedData.ToBase64(),
                References = references
            };
            var subject = GetReferenceSubjectName(state.Topic, dependency.Name, state.IsKey);

            if (_config.AutoRegisterSchemas)
            {
                if (_config.NormalizeSchemas)
                {
                    await _schemaRegistry.RegisterSchemaAsync(
                        subject,
                        schema,
                        normalize: true).ConfigureAwait(false);
                }
                else
                {
                    await _schemaRegistry.RegisterSchemaAsync(subject, schema).ConfigureAwait(false);
                }
            }

            var registered = await _schemaRegistry.LookupSchemaAsync(
                subject,
                schema,
                ignoreDeletedSchemas: true,
                normalize: _config.NormalizeSchemas).ConfigureAwait(false);
            var result = new RegisteredDependency(subject, registered.Version);
            state.Completed.Add(dependency.Name, result);
            return result;
        }
        finally
        {
            state.Visiting.Remove(dependency.Name);
        }
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
            ReferenceSubjectNameStrategy.Qualified => referenceName
                .Replace(".proto", string.Empty)
                .Replace('/', '.'),
            _ => throw new InvalidOperationException(
                $"Unknown Protobuf reference subject name strategy: {_config.ReferenceSubjectNameStrategy}.")
        };
    }

    private static bool IsKnownType(string referenceName) =>
        referenceName.StartsWith("confluent/", StringComparison.Ordinal) ||
        referenceName.StartsWith("google/protobuf/", StringComparison.Ordinal) ||
        referenceName.StartsWith("google/type/", StringComparison.Ordinal);

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
