using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using Dekaf.Serialization;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// Base serializer that integrates with Schema Registry.
/// Handles the wire format: [magic byte (0)] [schema ID (4 bytes)] [payload].
/// </summary>
/// <remarks>
/// <para>
/// This serializer caches schema IDs in a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// The first time a schema is needed for a particular subject, a synchronous blocking call to the
/// Schema Registry is made. After the first fetch, subsequent serialization calls for the same subject
/// use the cached schema ID without any blocking or allocation. Multiple subjects are cached concurrently.
/// </para>
/// <para>
/// The blocking call includes a timeout to prevent indefinite hangs. If the timeout is exceeded,
/// a <see cref="TimeoutException"/> is thrown.
/// </para>
/// </remarks>
/// <typeparam name="T">The type to serialize.</typeparam>
public sealed class SchemaRegistrySerializer<T> : ISerializer<T>, IAsyncDisposable
{
    private const byte MagicByte = 0x00;

    /// <summary>
    /// Default timeout for Schema Registry operations (30 seconds).
    /// </summary>
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly Action<T, IBufferWriter<byte>> _serialize;
    private readonly Func<string, Schema> _getSchema;
    private readonly SubjectNameStrategy _subjectNameStrategy;
    private readonly ISubjectNameStrategy? _customSubjectNameStrategy;
    private readonly bool _autoRegisterSchemas;
    private readonly bool _ownsClient;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;

    private readonly ConcurrentDictionary<string, int> _schemaIdCache = new();
    private readonly SubjectSchemaIdCache _subjectSchemaIdCache = new();

    /// <summary>
    /// Creates a new Schema Registry serializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="serialize">Action to serialize the value by writing to the provided buffer (without wire format).</param>
    /// <param name="getSchema">Function to get the schema for a type.</param>
    /// <param name="subjectNameStrategy">Strategy for determining subject names.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <param name="ownsClient">Whether this serializer owns the client and should dispose it.</param>
    public SchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        Action<T, IBufferWriter<byte>> serialize,
        Func<string, Schema> getSchema,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _serialize = serialize ?? throw new ArgumentNullException(nameof(serialize));
        _getSchema = getSchema ?? throw new ArgumentNullException(nameof(getSchema));
        _subjectNameStrategy = subjectNameStrategy;
        _autoRegisterSchemas = autoRegisterSchemas;
        _ownsClient = ownsClient;
        _ruleExecutor = ruleExecutor;
    }

    /// <summary>
    /// Creates a new Schema Registry serializer with a custom subject name strategy.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="serialize">Action to serialize the value by writing to the provided buffer (without wire format).</param>
    /// <param name="getSchema">Function to get the schema for a type.</param>
    /// <param name="customSubjectNameStrategy">Custom strategy for determining subject names.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <param name="ownsClient">Whether this serializer owns the client and should dispose it.</param>
    public SchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        Action<T, IBufferWriter<byte>> serialize,
        Func<string, Schema> getSchema,
        ISubjectNameStrategy customSubjectNameStrategy,
        bool autoRegisterSchemas = true,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _serialize = serialize ?? throw new ArgumentNullException(nameof(serialize));
        _getSchema = getSchema ?? throw new ArgumentNullException(nameof(getSchema));
        _customSubjectNameStrategy = customSubjectNameStrategy ?? throw new ArgumentNullException(nameof(customSubjectNameStrategy));
        _autoRegisterSchemas = autoRegisterSchemas;
        _ownsClient = ownsClient;
        _ruleExecutor = ruleExecutor;
    }

    public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var schemaId = GetSchemaIdForContext(context.Topic, context.Component == SerializationComponent.Key);

        var payloadBuffer = SchemaRegistryBuffers.PayloadBuffer ??= new ArrayBufferWriter<byte>(initialCapacity: 4096);
        payloadBuffer.ResetWrittenCount();
        _serialize(value, payloadBuffer);
        // Drop an oversized buffer so a single large message doesn't permanently hold capacity on this thread.
        if (payloadBuffer.Capacity > 1024 * 1024)
            SchemaRegistryBuffers.PayloadBuffer = null;

        var payload = payloadBuffer.WrittenMemory;
        if (_ruleExecutor is not null)
        {
            var isKey = context.Component == SerializationComponent.Key;
            var subject = GetSubjectName(context.Topic, isKey);
            payload = _ruleExecutor.TransformSerializedPayload(
                payload,
                new SchemaRegistryRuleContext
                {
                    Topic = context.Topic,
                    Component = context.Component,
                    SchemaId = schemaId,
                    Subject = subject,
                    Schema = _getSchema(subject),
                    PayloadFormat = SchemaRegistryPayloadFormat.Custom
                });
        }

        // Write wire format: [0x00] [schema ID] [payload]
        var totalSize = 1 + 4 + payload.Length;
        var span = destination.GetSpan(totalSize);

        span[0] = MagicByte;
        BinaryPrimitives.WriteInt32BigEndian(span.Slice(1, 4), schemaId);
        payload.Span.CopyTo(span.Slice(5));

        destination.Advance(totalSize);
    }

    private int GetSchemaIdForContext(string topic, bool isKey)
        => _subjectSchemaIdCache.GetOrAdd(
            topic,
            isKey,
            this,
            static (serializer, topic, isKey) => serializer.GetSubjectName(topic, isKey),
            static (serializer, subject) =>
            {
                var schema = serializer._getSchema(subject);
                return serializer.GetSchemaIdSync(subject, schema);
            });

    private int GetSchemaIdSync(string subject, Schema schema)
    {
        if (_schemaIdCache.TryGetValue(subject, out var id))
            return id;

        // Cache miss — fetch from registry. May race under contention for a new subject;
        // this is safe because schema registration is idempotent (same subject always returns same ID).
        var task = _autoRegisterSchemas
            ? _schemaRegistry.GetOrRegisterSchemaAsync(subject, schema)
            : _schemaRegistry.GetSchemaBySubjectAsync(subject).ContinueWith(
                static t => t.GetAwaiter().GetResult().Id, TaskScheduler.Default);

        var fetchedId = task.WaitAsync(SchemaRegistryTimeout).ConfigureAwait(false).GetAwaiter().GetResult();
        return _schemaIdCache.GetOrAdd(subject, fetchedId);
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
}

/// Non-generic holder for the thread-local serialization buffer.
/// Kept outside SchemaRegistrySerializer&lt;T&gt; so all generic instantiations
/// share one buffer per thread rather than one per (type × thread).
internal static class SchemaRegistryBuffers
{
    [ThreadStatic]
    internal static ArrayBufferWriter<byte>? PayloadBuffer;
}

/// <summary>
/// Base deserializer that integrates with Schema Registry.
/// Handles the wire format: [magic byte (0)] [schema ID (4 bytes)] [payload].
/// </summary>
/// <remarks>
/// <para>
/// This deserializer fetches the schema from Schema Registry on first access for each schema ID.
/// Schemas are cached internally by the Schema Registry client after first fetch.
/// </para>
/// <para>
/// The blocking call includes a timeout to prevent indefinite hangs. If the timeout is exceeded,
/// a <see cref="TimeoutException"/> is thrown.
/// </para>
/// </remarks>
/// <typeparam name="T">The type to deserialize.</typeparam>
public sealed class SchemaRegistryDeserializer<T> : IDeserializer<T>, IAsyncDisposable
{
    private const byte MagicByte = 0x00;

    /// <summary>
    /// Default timeout for Schema Registry operations (30 seconds).
    /// </summary>
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly Func<ReadOnlyMemory<byte>, Schema, T> _deserialize;
    private readonly bool _ownsClient;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;

    /// <summary>
    /// Creates a new Schema Registry deserializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="deserialize">Function to deserialize bytes to value using the schema.</param>
    /// <param name="ownsClient">Whether this deserializer owns the client and should dispose it.</param>
    public SchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        Func<byte[], Schema, T> deserialize,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
        : this(
            schemaRegistry,
            (ReadOnlyMemory<byte> payload, Schema schema) => deserialize(payload.ToArray(), schema),
            ownsClient,
            ruleExecutor)
    {
    }

    /// <summary>
    /// Creates a new Schema Registry deserializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="deserialize">Function to deserialize bytes to value using the schema.</param>
    /// <param name="ownsClient">Whether this deserializer owns the client and should dispose it.</param>
    internal SchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        Func<ReadOnlyMemory<byte>, Schema, T> deserialize,
        bool ownsClient,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _deserialize = deserialize ?? throw new ArgumentNullException(nameof(deserialize));
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

        var schema = _schemaRegistry.GetSchemaSync(schemaId, SchemaRegistryTimeout);
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
                    PayloadFormat = SchemaRegistryPayloadFormat.Custom
                });
        }

        return _deserialize(payload, schema);
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Factory methods for Schema Registry deserializers.
/// </summary>
public static class SchemaRegistryDeserializer
{
    /// <summary>
    /// Creates a Schema Registry deserializer that receives the payload as ReadOnlyMemory without copying it.
    /// </summary>
    /// <typeparam name="T">The type to deserialize.</typeparam>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="deserialize">Function to deserialize bytes to value using the schema.</param>
    /// <param name="ownsClient">Whether this deserializer owns the client and should dispose it.</param>
    /// <returns>The deserializer.</returns>
    public static SchemaRegistryDeserializer<T> Create<T>(
        ISchemaRegistryClient schemaRegistry,
        Func<ReadOnlyMemory<byte>, Schema, T> deserialize,
        bool ownsClient = false,
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
        => new(schemaRegistry, deserialize, ownsClient, ruleExecutor);
}

/// <summary>
/// Strategy for determining the subject name.
/// </summary>
public enum SubjectNameStrategy
{
    /// <summary>
    /// Subject name is the topic name with -key or -value suffix.
    /// </summary>
    TopicName,

    /// <summary>
    /// Subject name is the fully qualified record name with -key or -value suffix.
    /// </summary>
    RecordName,

    /// <summary>
    /// Subject name is topic-recordname with -key or -value suffix.
    /// </summary>
    TopicRecordName
}
