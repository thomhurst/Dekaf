using System.Buffers;
using System.Buffers.Binary;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.Serialization;
using RegistrySchema = Dekaf.SchemaRegistry.Schema;

namespace Dekaf.SchemaRegistry.Avro.Poco;

/// <summary>Schema Registry serializer backed by a generated POCO Avro codec.</summary>
public sealed class AvroPocoSchemaRegistrySerializer<T, TCodec>
    : ISerializer<T>, IAsyncSerializerPreparer<T>, IAsyncDisposable
    where TCodec : struct, IAvroPocoCodec<T>
{
    private const byte MagicByte = 0;
    private const int WireHeaderSize = 5;
    private const int InitialPayloadSize = 256;
    private const int MaxRetainedPayloadSize = 1024 * 1024;
    private static readonly TimeSpan RegistryTimeout = TimeSpan.FromSeconds(30);

    [ThreadStatic]
    private static int t_payloadSizeHint;

    [ThreadStatic]
    private static byte[]? t_ruleBuffer;

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly AvroSerializerConfig _config;
    private readonly bool _ownsClient;
    private readonly RegistrySchema _schema;
    private readonly SubjectSchemaIdCache _subjectCache = new();
    private readonly SchemaResolutionCache<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> _resolutionCache = new();

    /// <summary>Creates a generated POCO Avro serializer.</summary>
    public AvroPocoSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        AvroSerializerConfig? config = null,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _config = config ?? new AvroSerializerConfig();
        _ownsClient = ownsClient;
        _schema = new RegistrySchema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = TCodec.SchemaJson
        };
    }

    /// <summary>Prepares one topic/component and returns its Schema Registry ID.</summary>
    public async Task<int> WarmupAsync(
        string topic,
        bool isKey = false,
        CancellationToken cancellationToken = default)
    {
        var resolved = await PrepareAsync(topic, isKey, cancellationToken).ConfigureAwait(false);
        return resolved.SchemaId;
    }

    /// <summary>Resolves the subject and schema ID asynchronously.</summary>
    public ValueTask<ResolvedSchemaContext> PrepareAsync(
        string topic,
        bool isKey = false,
        CancellationToken cancellationToken = default)
    {
        if (_subjectCache.TryGet(topic, isKey, out var cached))
            return new ValueTask<ResolvedSchemaContext>(ToResolvedContext(cached));

        var subject = GetSubjectName(topic, isKey);
        var resolution = ResolveSchemaAsync(subject, cancellationToken);
        if (resolution.IsCompletedSuccessfully)
        {
            var value = resolution.Result;
            return new ValueTask<ResolvedSchemaContext>(ToResolvedContext(
                _subjectCache.CacheEntry(topic, isKey, subject, value.SchemaId, value.Schema!)));
        }

        return AwaitResolutionAsync(topic, isKey, subject, resolution);
    }

    /// <inheritdoc />
    public ValueTask PrepareAsync(
        T value,
        SerializationContext context,
        CancellationToken cancellationToken = default)
    {
        var preparation = PrepareAsync(
            context.Topic,
            context.Component == SerializationComponent.Key,
            cancellationToken);
        if (preparation.IsCompletedSuccessfully)
        {
            _ = preparation.Result;
            return ValueTask.CompletedTask;
        }

        return AwaitPreparationAsync(preparation);
    }

    /// <inheritdoc />
    public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        if (default(T) is null && value is null)
            throw new ArgumentNullException(nameof(value));

        var entry = GetSchemaForContext(
            context.Topic,
            context.Component == SerializationComponent.Key);
        if (_config.RuleExecutor is null)
        {
            SerializeDirect(value, ref destination, entry.SchemaId);
            return;
        }

        SerializeWithRules(value, ref destination, context, entry);
    }

    private static async ValueTask AwaitPreparationAsync(ValueTask<ResolvedSchemaContext> preparation) =>
        _ = await preparation.ConfigureAwait(false);

    private async ValueTask<ResolvedSchemaContext> AwaitResolutionAsync(
        string topic,
        bool isKey,
        string subject,
        ValueTask<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> resolution)
    {
        var value = await resolution.ConfigureAwait(false);
        return ToResolvedContext(
            _subjectCache.CacheEntry(topic, isKey, subject, value.SchemaId, value.Schema!));
    }

    private static ResolvedSchemaContext ToResolvedContext(SubjectSchemaIdCache.SubjectSchemaIdCacheEntry entry) =>
        new(entry.Subject!, entry.SchemaId, entry.Schema!);

    private static void SerializeDirect<TWriter>(T value, ref TWriter destination, int schemaId)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        var payloadSize = t_payloadSizeHint is > 0 ? t_payloadSizeHint : InitialPayloadSize;
        while (true)
        {
            var memory = destination.GetMemory(WireHeaderSize + payloadSize);
            var writer = new AvroValueWriter(memory.Span.Slice(WireHeaderSize));
            TCodec.Write(ref writer, value);
            if (!writer.IsComplete)
            {
                payloadSize = Grow(payloadSize);
                continue;
            }

            var span = memory.Span;
            span[0] = MagicByte;
            BinaryPrimitives.WriteInt32BigEndian(span.Slice(1, 4), schemaId);
            destination.Advance(WireHeaderSize + writer.WrittenCount);
            t_payloadSizeHint = writer.WrittenCount > MaxRetainedPayloadSize
                ? InitialPayloadSize
                : Math.Max(payloadSize, writer.WrittenCount);
            return;
        }
    }

    private void SerializeWithRules<TWriter>(
        T value,
        ref TWriter destination,
        SerializationContext context,
        SubjectSchemaIdCache.SubjectSchemaIdCacheEntry entry)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        var buffer = t_ruleBuffer ??= GC.AllocateUninitializedArray<byte>(1024);
        int length;
        while (true)
        {
            var writer = new AvroValueWriter(buffer);
            TCodec.Write(ref writer, value);
            if (writer.IsComplete)
            {
                length = writer.WrittenCount;
                break;
            }

            var nextLength = Grow(buffer.Length);
            buffer = GC.AllocateUninitializedArray<byte>(nextLength);
            if (nextLength <= MaxRetainedPayloadSize)
                t_ruleBuffer = buffer;
        }

        var payload = new ReadOnlyMemory<byte>(buffer, 0, length);
        var ruleContext = SchemaRegistryRuleContext.Rent(
            context.Topic,
            context.Component,
            entry.SchemaId,
            entry.Subject!,
            entry.Schema!,
            SchemaRegistryPayloadFormat.Avro);
        try
        {
            payload = _config.RuleExecutor!.TransformSerializedPayload(payload, ruleContext);
        }
        finally
        {
            ruleContext.Return();
        }

        var output = destination.GetSpan(WireHeaderSize + payload.Length);
        output[0] = MagicByte;
        BinaryPrimitives.WriteInt32BigEndian(output.Slice(1, 4), entry.SchemaId);
        payload.Span.CopyTo(output.Slice(WireHeaderSize));
        destination.Advance(WireHeaderSize + payload.Length);
    }

    private SubjectSchemaIdCache.SubjectSchemaIdCacheEntry GetSchemaForContext(string topic, bool isKey) =>
        _subjectCache.GetOrAdd(
            topic,
            isKey,
            this,
            static (serializer, currentTopic, currentIsKey) =>
                serializer.ResolveSchemaCached(currentTopic, currentIsKey));

    private SubjectSchemaIdCache.SubjectSchemaIdCacheEntry ResolveSchemaCached(string topic, bool isKey)
    {
        var subject = GetSubjectName(topic, isKey);
        var value = _resolutionCache.Resolve(
            subject,
            _schema,
            this,
            static (serializer, resolvedSubject, schema) =>
                serializer.FetchSchemaWithTimeoutAsync(resolvedSubject, schema),
            RegistryTimeout);
        return new SubjectSchemaIdCache.SubjectSchemaIdCacheEntry(
            new SubjectSchemaIdCache.SubjectSchemaIdCacheKey(topic, isKey),
            subject,
            value.SchemaId,
            value.Schema);
    }

    private ValueTask<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> ResolveSchemaAsync(
        string subject,
        CancellationToken cancellationToken) =>
        _resolutionCache.ResolveAsync(
            subject,
            _schema,
            this,
            static (serializer, resolvedSubject, schema) =>
                serializer.FetchSchemaWithTimeoutAsync(resolvedSubject, schema),
            cancellationToken);

    private Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> FetchSchemaWithTimeoutAsync(
        string subject,
        RegistrySchema schema) =>
        SchemaRegistryOperationTimeout.ExecuteAsync(
            cancellationToken => FetchSchemaAsync(subject, schema, cancellationToken),
            RegistryTimeout,
            "Schema Registry resolution timed out.");

    private async Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> FetchSchemaAsync(
        string subject,
        RegistrySchema schema,
        CancellationToken cancellationToken)
    {
        if (_config.UseLatestVersion)
        {
            var registered = await _schemaRegistry.GetSchemaBySubjectAsync(
                    subject,
                    "latest",
                    cancellationToken)
                .ConfigureAwait(false);
            return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(registered.Id, registered.Schema);
        }

        if (_config.AutoRegisterSchemas)
        {
            var schemaId = _config.NormalizeSchemas
                ? await _schemaRegistry.GetOrRegisterSchemaAsync(
                    subject,
                    schema,
                    normalize: true,
                    cancellationToken).ConfigureAwait(false)
                : await _schemaRegistry.GetOrRegisterSchemaAsync(
                    subject,
                    schema,
                    cancellationToken).ConfigureAwait(false);
            var registeredSchema = _config.RuleExecutor is SchemaRegistryRuleExecutor
                ? await _schemaRegistry.GetSchemaAsync(schemaId, subject, cancellationToken).ConfigureAwait(false)
                : schema;
            return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(schemaId, registeredSchema);
        }

        var existing = await _schemaRegistry.GetSchemaBySubjectAsync(
                subject,
                "latest",
                cancellationToken)
            .ConfigureAwait(false);
        return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(existing.Id, existing.Schema);
    }

    private string GetSubjectName(string topic, bool isKey) =>
        _config.CustomSubjectNameStrategy?.GetSubjectName(topic, TCodec.FullName, isKey)
        ?? SubjectNameResolver.GetSubjectName(
            _config.SubjectNameStrategy,
            topic,
            TCodec.FullName,
            isKey,
            _config.UseLegacySubjectNames);

    private static int Grow(int current)
    {
        var maximum = Array.MaxLength - WireHeaderSize;
        if (current >= maximum)
            throw new NotSupportedException($"Avro payloads larger than {maximum} bytes are not supported.");
        return (int)Math.Min((long)current * 2, maximum);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}
