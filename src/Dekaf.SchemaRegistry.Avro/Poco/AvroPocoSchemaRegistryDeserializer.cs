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
    private static readonly TimeSpan RegistryTimeout = TimeSpan.FromSeconds(30);

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly AvroDeserializerConfig _config;
    private readonly bool _ownsClient;
    private readonly ConcurrentDictionary<int, Lazy<Task<AvroPocoReaderPlan>>> _plans = new();
    private readonly SchemaRegistryMigrationRunner? _migrationRunner;
    private readonly ISchemaRegistryRuleExecutor? _ruleExecutor;

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
        var planSchemaId = schemaId;
        var payload = data.Slice(WireHeaderSize);

        if (_ruleExecutor is not null)
        {
            var subject = GetSubjectName(context.Topic, context.Component == SerializationComponent.Key);
            var scopedSchema = _schemaRegistry.GetSchemaSync(schemaId, subject, RegistryTimeout);
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
                    payload = _ruleExecutor.TransformDeserializedPayload(payload, ruleContext);
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
                    scopedSchema,
                    context,
                    SchemaRegistryPayloadFormat.Avro);
                payload = migration.Payload;
                planSchemaId = migration.ReaderSchema.Id;
            }
        }

        var plan = GetPlanCached(planSchemaId);
        var reader = new AvroValueReader(payload.Span);
        return TCodec.Read(ref reader, plan);
    }

    private AvroPocoReaderPlan GetPlanCached(int schemaId)
    {
        var task = GetOrAddPlan(schemaId).Value;
        return task.IsCompletedSuccessfully
            ? task.Result
            : task.WaitAsync(RegistryTimeout).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private async Task<AvroPocoReaderPlan> GetPlanAsync(int schemaId, CancellationToken cancellationToken) =>
        await GetOrAddPlan(schemaId).Value.WaitAsync(cancellationToken).ConfigureAwait(false);

    private Lazy<Task<AvroPocoReaderPlan>> GetOrAddPlan(int schemaId)
    {
        if (_plans.TryGetValue(schemaId, out var cached))
            return cached;
        return _plans.GetOrAdd(
            schemaId,
            static (id, owner) => new Lazy<Task<AvroPocoReaderPlan>>(() => owner.FetchPlanAsync(id)),
            this);
    }

    private async Task<AvroPocoReaderPlan> FetchPlanAsync(int schemaId)
    {
        try
        {
            var schema = await SchemaRegistryOperationTimeout.ExecuteAsync(
                    cancellationToken => _schemaRegistry.GetSchemaAsync(schemaId, cancellationToken),
                    RegistryTimeout,
                    $"Schema Registry lookup for schema {schemaId} timed out.")
                .ConfigureAwait(false);
            if (schema.SchemaType != SchemaType.Avro)
                throw new InvalidOperationException($"Schema {schemaId} is {schema.SchemaType}, not Avro.");
            return AvroPocoReaderPlanBuilder.Build<T, TCodec>(schema.SchemaString);
        }
        catch
        {
            _plans.TryRemove(schemaId, out _);
            throw;
        }
    }

    private string GetSubjectName(string topic, bool isKey) =>
        _config.CustomSubjectNameStrategy?.GetSubjectName(topic, TCodec.FullName, isKey)
        ?? SubjectNameResolver.GetSubjectName(
            _config.SubjectNameStrategy,
            topic,
            TCodec.FullName,
            isKey,
            _config.UseLegacySubjectNames);

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}
