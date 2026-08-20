using System.Buffers.Binary;
using Dekaf.Serialization;
using Google.Protobuf;

namespace Dekaf.SchemaRegistry.Protobuf;

/// <summary>
/// Protobuf deserializer that integrates with Confluent Schema Registry.
/// Wire format: [magic byte (0x00)] [schema ID (4 bytes)] [varint array indexes] [protobuf binary]
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
public sealed class ProtobufSchemaRegistryDeserializer<T> :
    IDeserializer<T>,
    IAsyncDeserializerPreparer<T>,
    IAsyncDeserializerPreparationRequirement,
    IAsyncDisposable
    where T : IMessage<T>, IBufferMessage, new()
{
    private const byte MagicByte = 0x00;
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);
    private static readonly string RecordName = new T().Descriptor.FullName;

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly ProtobufDeserializerConfig _config;
    private readonly bool _ownsClient;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;
    private readonly MessageParser<T> _parser;
    private readonly DeserializerSubjectNameCache? _subjectNames;
    private readonly SchemaRegistryMigrationRunner? _migrationRunner;

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
        CancellationToken cancellationToken)
    {
        if (_ruleExecutor is null
            || _subjectNames is not { RequiresPreparation: true }
            || !DeserializerSubjectNameCache.TryReadSchemaId(data, out var schemaId))
        {
            return default;
        }

        return _subjectNames.PrepareAsync(
            _schemaRegistry,
            schemaId,
            context.Topic,
            context.Component == SerializationComponent.Key,
            RecordName,
            cancellationToken);
    }

    bool IAsyncDeserializerPreparer<T>.TryDeserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        out T value)
    {
        string? preparedSubject = null;
        if (_ruleExecutor is not null
            && _subjectNames is { RequiresPreparation: true } subjectNames
            && DeserializerSubjectNameCache.TryReadSchemaId(data, out var schemaId))
        {
            if (!subjectNames.TryGetPreparedSubject(
                    schemaId,
                    context.Topic,
                    context.Component == SerializationComponent.Key,
                    out var prepared))
            {
                value = default!;
                return false;
            }

            preparedSubject = prepared.Subject;
        }

        value = DeserializeCore(data, context, preparedSubject);
        return true;
    }

    /// <inheritdoc />
    public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
        DeserializeCore(data, context, preparedSubject: null);

    private T DeserializeCore(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        string? preparedSubject)
    {
        var span = data.Span;

        if (span.Length < 5)
        {
            if (context is { IsNull: true, Component: SerializationComponent.Value })
                return default!;

            throw new InvalidOperationException("Message too short to contain Schema Registry wire format");
        }

        // Verify magic byte
        if (span[0] != MagicByte)
            throw new InvalidOperationException($"Unknown magic byte: {span[0]}. Expected Schema Registry format (0x00).");

        // Read schema ID (big-endian)
        var schemaId = BinaryPrimitives.ReadInt32BigEndian(span.Slice(1, 4));

        // Optionally validate the schema exists (with timeout to prevent indefinite hang)
        Schema? schema = null;
        string? ruleSubject = null;
        if (!_config.SkipSchemaValidation || _config.RuleExecutor is SchemaRegistryRuleExecutor || _migrationRunner is not null)
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
        var payloadMemory = data.Slice(5);
        var payload = payloadMemory.Span;
        var (indexCount, bytesRead) = ReadVarint(payload, _config.UseDeprecatedFormat);
        if (indexCount < 0)
            throw new InvalidOperationException("Message index array length cannot be negative");

        // Skip past the index array
        for (var i = 0; i < indexCount; i++)
        {
            var (index, indexBytesRead) = ReadVarint(payload.Slice(bytesRead), _config.UseDeprecatedFormat);
            if (index < 0)
                throw new InvalidOperationException("Message index cannot be negative");
            bytesRead += indexBytesRead;
        }

        // The rest is the protobuf message
        var protobufData = payloadMemory.Slice(bytesRead);
        if (_ruleExecutor is not null)
        {
            var subject = ruleSubject ?? GetSubjectName(schemaId, schema, context);
            if (schema is not null && ruleSubject is null)
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
