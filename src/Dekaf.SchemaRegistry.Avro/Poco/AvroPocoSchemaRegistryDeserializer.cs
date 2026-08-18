using System.Buffers.Binary;
using System.Collections.Concurrent;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.Serialization;

namespace Dekaf.SchemaRegistry.Avro.Poco;

/// <summary>Schema Registry deserializer backed by a generated POCO Avro codec.</summary>
public sealed class AvroPocoSchemaRegistryDeserializer<T, TCodec> : IDeserializer<T>, IAsyncDisposable
    where TCodec : struct, IAvroPocoCodec<T>
{
    private const byte MagicByte = 0;
    private const int WireHeaderSize = 5;
    private const int GeneratedSubjectCacheSchemaId = 0;
    internal const int MaxCachedPlans = 256;
    private static readonly TimeSpan RegistryTimeout = TimeSpan.FromSeconds(30);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly AvroDeserializerConfig _config;
    private readonly bool _ownsClient;
    private readonly ConcurrentDictionary<int, AvroPocoReaderPlan> _plans = new();
    private readonly ConcurrentDictionary<int, PlanEntry> _inFlightPlans = new();
    private readonly ConcurrentQueue<KeyValuePair<int, AvroPocoReaderPlan>> _planEvictionQueue = new();
    private readonly SchemaRegistryMigrationRunner? _migrationRunner;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;
    private readonly DeserializerSubjectNameCache? _subjectNames;
    private int _cachedPlanCount;

    internal int CachedPlanCount => Volatile.Read(ref _cachedPlanCount);

    /// <summary>Creates a generated POCO Avro deserializer.</summary>
    public AvroPocoSchemaRegistryDeserializer(
        ISchemaRegistryClient schemaRegistry,
        AvroDeserializerConfig? config = null,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _config = config ?? new AvroDeserializerConfig();
        _ownsClient = ownsClient;
        _ruleExecutor = _config.RuleExecutor;
        if (!string.IsNullOrEmpty(_config.ReaderSchema))
        {
            throw new ArgumentException(
                "Generated POCO codecs define their reader schema at compile time; ReaderSchema cannot be overridden.",
                nameof(config));
        }

        if (_config.UseLatestVersion)
        {
            (_migrationRunner, _ruleExecutor) = SchemaRegistryMigrationRunner.Create(
                schemaRegistry,
                _config.RuleExecutor,
                RegistryTimeout);
        }

        if (_ruleExecutor is not null)
        {
            _subjectNames = DeserializerSubjectNameCache.Create(
                _config.SubjectNameStrategy,
                _config.CustomSubjectNameStrategy,
                _config.UseLegacySubjectNames);
        }
    }

    /// <summary>Prepares a writer schema ID and its evolution plan.</summary>
    public async Task WarmupAsync(int schemaId, CancellationToken cancellationToken = default) =>
        _ = await GetPlanAsync(schemaId, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        var span = data.Span;
        if (span.Length < WireHeaderSize)
        {
            if (context is { IsNull: true, Component: SerializationComponent.Value })
                return default!;
            throw new InvalidOperationException("Message too short to contain Schema Registry wire format.");
        }

        if (span[0] != MagicByte)
            throw new InvalidOperationException($"Unknown magic byte: {span[0]}. Expected 0x00.");

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(span.Slice(1, 4));
        var payload = data.Slice(WireHeaderSize);
        if (_ruleExecutor is null)
        {
            var directReader = new AvroValueReader(payload.Span);
            return TCodec.Read(ref directReader, GetPlanCached(schemaId));
        }

        return DeserializeWithRules(payload, schemaId, context);
    }

    private T DeserializeWithRules(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        SerializationContext context)
    {
        var subject = GetSubjectName(
            context.Topic,
            context.Component == SerializationComponent.Key);
        var scopedSchema = _schemaRegistry.GetSchemaSync(schemaId, subject, RegistryTimeout);
        AvroPocoReaderPlan plan;
        if (_migrationRunner is null)
        {
            var ruleContext = SchemaRegistryRuleContext.Rent(
                context.Topic,
                context.Component,
                schemaId,
                subject,
                scopedSchema,
                SchemaRegistryPayloadFormat.Avro);
            try
            {
                payload = _ruleExecutor!.TransformDeserializedPayload(payload, ruleContext);
            }
            finally
            {
                ruleContext.Return();
            }
            plan = GetPlanCached(schemaId);
        }
        else
        {
            var migration = _migrationRunner.Transform(
                payload,
                schemaId,
                subject,
                scopedSchema,
                context,
                SchemaRegistryPayloadFormat.Avro);
            payload = migration.Payload;
            plan = GetPlanCached(migration.PayloadSchemaId);
        }

        var reader = new AvroValueReader(payload.Span);
        return TCodec.Read(ref reader, plan);
    }

    private AvroPocoReaderPlan GetPlanCached(int schemaId)
    {
        if (_plans.TryGetValue(schemaId, out var cached))
            return cached;

        if (_schemaRegistry is ISchemaRegistryCache cache &&
            cache.TryGetCachedSchema(schemaId, out var schema))
        {
            var plan = BuildPlan(schemaId, schema);
            CacheSuccessfulPlan(schemaId, plan);
            return _plans.TryGetValue(schemaId, out cached) ? cached : plan;
        }

        throw new InvalidOperationException(
            $"Schema {schemaId} is not cached. Call WarmupAsync before synchronous deserialization.");
    }

    private async Task<AvroPocoReaderPlan> GetPlanAsync(int schemaId, CancellationToken cancellationToken)
    {
        if (_plans.TryGetValue(schemaId, out var cached))
            return cached;

        cancellationToken.ThrowIfCancellationRequested();
        var entry = GetOrAddInFlightPlan(schemaId);
        if (_plans.TryGetValue(schemaId, out cached))
        {
            RemoveInFlightPlan(schemaId, entry);
            return cached;
        }

        var task = entry.Plan.Value;
        return task.IsCompletedSuccessfully
            ? task.Result
            : await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private PlanEntry GetOrAddInFlightPlan(int schemaId) =>
        _inFlightPlans.GetOrAdd(
            schemaId,
            static (id, owner) => PlanEntry.Create(owner, id),
            this);

    private async Task<AvroPocoReaderPlan> FetchPlanAsync(int schemaId, PlanEntry entry)
    {
        try
        {
            var schema = await SchemaRegistryOperationTimeout.ExecuteAsync(
                    cancellationToken => _schemaRegistry.GetSchemaAsync(schemaId, cancellationToken),
                    RegistryTimeout,
                    $"Schema Registry lookup for schema {schemaId} timed out.")
                .ConfigureAwait(false);
            var plan = BuildPlan(schemaId, schema);
            CacheSuccessfulPlan(schemaId, plan);
            return plan;
        }
        finally
        {
            RemoveInFlightPlan(schemaId, entry);
        }
    }

    private static AvroPocoReaderPlan BuildPlan(int schemaId, Schema schema)
    {
        if (schema.SchemaType != SchemaType.Avro)
            throw new InvalidOperationException($"Schema {schemaId} is {schema.SchemaType}, not Avro.");
        return AvroPocoReaderPlanBuilder.Build<T, TCodec>(schema.SchemaString);
    }

    private void RemoveInFlightPlan(int schemaId, PlanEntry entry) =>
        ((ICollection<KeyValuePair<int, PlanEntry>>)_inFlightPlans)
        .Remove(new KeyValuePair<int, PlanEntry>(schemaId, entry));

    private void CacheSuccessfulPlan(int schemaId, AvroPocoReaderPlan plan)
    {
        if (!_plans.TryAdd(schemaId, plan))
            return;

        Interlocked.Increment(ref _cachedPlanCount);
        _planEvictionQueue.Enqueue(new KeyValuePair<int, AvroPocoReaderPlan>(schemaId, plan));
        TrimPlanCache();
    }

    private void TrimPlanCache()
    {
        while (true)
        {
            var count = Volatile.Read(ref _cachedPlanCount);
            if (count <= MaxCachedPlans)
                return;

            if (Interlocked.CompareExchange(ref _cachedPlanCount, count - 1, count) != count)
                continue;

            var removed = false;
            while (_planEvictionQueue.TryDequeue(out var oldest))
            {
                if (((ICollection<KeyValuePair<int, AvroPocoReaderPlan>>)_plans).Remove(oldest))
                {
                    removed = true;
                    break;
                }
            }

            if (!removed)
            {
                Interlocked.Increment(ref _cachedPlanCount);
                return;
            }
        }
    }

    private string GetSubjectName(string topic, bool isKey) =>
        _subjectNames?.GetSubjectName(GeneratedSubjectCacheSchemaId, null, topic, isKey, TCodec.FullName)
        ?? SubjectNameResolver.GetTopicSubjectName(topic, isKey);

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class PlanEntry
    {
        private PlanEntry()
        {
        }

        internal Lazy<Task<AvroPocoReaderPlan>> Plan { get; private set; } = null!;

        internal static PlanEntry Create(
            AvroPocoSchemaRegistryDeserializer<T, TCodec> owner,
            int schemaId)
        {
            var entry = new PlanEntry();
            entry.Plan = new Lazy<Task<AvroPocoReaderPlan>>(
                () => ObserveFault(owner.FetchPlanAsync(schemaId, entry)));
            return entry;
        }

        private static Task<AvroPocoReaderPlan> ObserveFault(Task<AvroPocoReaderPlan> task)
        {
            _ = task.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            return task;
        }
    }
}
