using System.Buffers;
using System.Buffers.Binary;
using Dekaf.Serialization;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// Base serializer that integrates with Schema Registry.
/// Handles the wire format: [magic byte (0)] [schema ID (4 bytes)] [payload].
/// </summary>
/// <remarks>
/// <para>
/// This serializer uses lazy caching for schema IDs. The first time a schema is needed for a
/// particular subject, a synchronous blocking call to the Schema Registry is made.
/// After the first fetch, subsequent serialization calls for the same subject use the cached
/// schema ID without any blocking.
/// </para>
/// <para>
/// The blocking call includes a timeout of 30 seconds to prevent indefinite hangs. If the timeout
/// is exceeded, a <see cref="TimeoutException"/> is thrown.
/// </para>
/// <para>
/// For high-throughput scenarios, use <see cref="WarmupAsync"/> to pre-warm the cache before
/// starting production. This ensures the synchronous <see cref="Serialize"/> method never
/// blocks on Schema Registry calls.
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
    private readonly Func<T, byte[]> _serialize;
    private readonly Func<string, Schema> _getSchema;
    private readonly SubjectNameStrategy _subjectNameStrategy;
    private readonly ISubjectNameStrategy? _customSubjectNameStrategy;
    private readonly bool _autoRegisterSchemas;
    private readonly bool _ownsClient;

    private int _cachedSchemaId = -1;
    private string? _cachedSubject;

    /// <summary>
    /// Creates a new Schema Registry serializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="serialize">Function to serialize the value to bytes (without wire format).</param>
    /// <param name="getSchema">Function to get the schema for a type.</param>
    /// <param name="subjectNameStrategy">Strategy for determining subject names.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <param name="ownsClient">Whether this serializer owns the client and should dispose it.</param>
    public SchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        Func<T, byte[]> serialize,
        Func<string, Schema> getSchema,
        SubjectNameStrategy subjectNameStrategy = SubjectNameStrategy.TopicName,
        bool autoRegisterSchemas = true,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _serialize = serialize ?? throw new ArgumentNullException(nameof(serialize));
        _getSchema = getSchema ?? throw new ArgumentNullException(nameof(getSchema));
        _subjectNameStrategy = subjectNameStrategy;
        _autoRegisterSchemas = autoRegisterSchemas;
        _ownsClient = ownsClient;
    }

    /// <summary>
    /// Creates a new Schema Registry serializer with a custom subject name strategy.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="serialize">Function to serialize the value to bytes (without wire format).</param>
    /// <param name="getSchema">Function to get the schema for a type.</param>
    /// <param name="customSubjectNameStrategy">Custom strategy for determining subject names.</param>
    /// <param name="autoRegisterSchemas">Whether to auto-register schemas.</param>
    /// <param name="ownsClient">Whether this serializer owns the client and should dispose it.</param>
    public SchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        Func<T, byte[]> serialize,
        Func<string, Schema> getSchema,
        ISubjectNameStrategy customSubjectNameStrategy,
        bool autoRegisterSchemas = true,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _serialize = serialize ?? throw new ArgumentNullException(nameof(serialize));
        _getSchema = getSchema ?? throw new ArgumentNullException(nameof(getSchema));
        _customSubjectNameStrategy = customSubjectNameStrategy ?? throw new ArgumentNullException(nameof(customSubjectNameStrategy));
        _autoRegisterSchemas = autoRegisterSchemas;
        _ownsClient = ownsClient;
    }

    /// <summary>
    /// Pre-warms the schema cache for a specific topic and component.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call this method after construction and before producing any messages to avoid synchronous
    /// blocking on the first <see cref="Serialize"/> call. Without warmup, the first serialization
    /// call for each subject will block the calling thread while fetching the schema ID from the
    /// Schema Registry.
    /// </para>
    /// <para>
    /// This method is safe to call multiple times and from multiple threads. Subsequent calls for
    /// the same subject return the cached schema ID without contacting the registry.
    /// </para>
    /// </remarks>
    /// <param name="topic">The topic name to warm up the cache for.</param>
    /// <param name="isKey">Whether this is for the key (true) or value (false) component.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The schema ID that will be used for serialization.</returns>
    /// <example>
    /// <code>
    /// var serializer = new SchemaRegistrySerializer&lt;MyType&gt;(client, serialize, getSchema);
    /// await serializer.WarmupAsync("my-topic");
    /// // Serialize calls will now use the cached schema ID without blocking.
    /// </code>
    /// </example>
    public async Task<int> WarmupAsync(string topic, bool isKey = false, CancellationToken cancellationToken = default)
    {
        var subject = GetSubjectName(topic, isKey);
        var schema = _getSchema(subject);

        var id = _autoRegisterSchemas
            ? await _schemaRegistry.GetOrRegisterSchemaAsync(subject, schema, cancellationToken).ConfigureAwait(false)
            : (await _schemaRegistry.GetSchemaBySubjectAsync(subject, "latest", cancellationToken).ConfigureAwait(false)).Id;

        _cachedSchemaId = id;
        _cachedSubject = subject;

        return id;
    }

    /// <summary>
    /// Serializes the value to the output buffer using the Schema Registry wire format.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>WARNING: First-call blocking.</strong> If <see cref="WarmupAsync"/> has not been called
    /// for the topic/component being serialized, the first invocation of this method will block the
    /// calling thread synchronously while it fetches (or registers) the schema ID from the Schema
    /// Registry. This can take up to 30 seconds (the default timeout) and may cause deadlocks in
    /// environments with a synchronization context (e.g., UI applications, ASP.NET with legacy sync
    /// pipelines).
    /// </para>
    /// <para>
    /// To avoid this, call <see cref="WarmupAsync"/> for each topic you intend to produce to before
    /// calling this method. After the schema ID is cached (either via warmup or after the first
    /// successful serialization), subsequent calls return immediately without blocking.
    /// </para>
    /// </remarks>
    public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var subject = GetSubjectName(context.Topic, context.Component == SerializationComponent.Key);
        var schema = _getSchema(subject);

        // Get or register schema ID (cached after first call per subject)
        var schemaId = GetSchemaIdSync(subject, schema);

        // Serialize the payload
        var payload = _serialize(value);

        // Write wire format: [0x00] [schema ID] [payload]
        var totalSize = 1 + 4 + payload.Length;
        var span = destination.GetSpan(totalSize);

        span[0] = MagicByte;
        BinaryPrimitives.WriteInt32BigEndian(span.Slice(1, 4), schemaId);
        payload.AsSpan().CopyTo(span.Slice(5));

        destination.Advance(totalSize);
    }

    private int GetSchemaIdSync(string subject, Schema schema)
    {
        // Use cached ID if subject matches
        if (_cachedSchemaId >= 0 && _cachedSubject == subject)
            return _cachedSchemaId;

        // Synchronously get/register schema (blocking with timeout to prevent indefinite hang)
        var task = _autoRegisterSchemas
            ? _schemaRegistry.GetOrRegisterSchemaAsync(subject, schema)
            : _schemaRegistry.GetSchemaBySubjectAsync(subject).ContinueWith(t => t.Result.Id, TaskScheduler.Default);

        // Add timeout to prevent indefinite blocking in UI/sync context scenarios
        var id = task.WaitAsync(SchemaRegistryTimeout).ConfigureAwait(false).GetAwaiter().GetResult();

        _cachedSchemaId = id;
        _cachedSubject = subject;

        return id;
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
    private readonly Func<byte[], Schema, T> _deserialize;
    private readonly bool _ownsClient;

    /// <summary>
    /// Creates a new Schema Registry deserializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="deserialize">Function to deserialize bytes to value using the schema.</param>
    /// <param name="ownsClient">Whether this deserializer owns the client and should dispose it.</param>
    public SchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        Func<byte[], Schema, T> deserialize,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _deserialize = deserialize ?? throw new ArgumentNullException(nameof(deserialize));
        _ownsClient = ownsClient;
    }

    public T Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
    {
        if (data.Length < 5)
            throw new InvalidOperationException("Message too short to contain Schema Registry wire format");

        // Read wire format header
        Span<byte> header = stackalloc byte[5];
        data.Slice(0, 5).CopyTo(header);

        if (header[0] != MagicByte)
            throw new InvalidOperationException($"Unknown magic byte: {header[0]}. Expected Schema Registry format.");

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(header.Slice(1, 4));

        // Get schema from registry (cached, with timeout to prevent indefinite hang)
        var schema = _schemaRegistry.GetSchemaAsync(schemaId)
            .WaitAsync(SchemaRegistryTimeout)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        // Extract payload
        var payload = data.Slice(5).ToArray();

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
