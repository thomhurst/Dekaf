using System.Buffers;
using System.Runtime.CompilerServices;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Avro.Poco;
using Dekaf.Serialization;

// Publishing this POCO-only app with TrimmerSingleWarn=false must report no trim/AOT warnings:
// generated POCO serializers must not reach Apache.Avro unless rules are configured.
await AvroPocoAotSmoke.RunAsync();

internal static class AvroPocoAotSmoke
{
    private static readonly SerializationContext ValueContext = new()
    {
        Topic = "aot-poco-topic",
        Component = SerializationComponent.Value
    };

    public static async Task RunAsync()
    {
        using var registry = new InMemorySchemaRegistry();
        await RunRoundTripAsync(registry);
        RunRuleConfigurationRejected(registry);
        Console.WriteLine("Avro POCO trim/AOT smoke passed.");
    }

    private static async Task RunRoundTripAsync(InMemorySchemaRegistry registry)
    {
        await using var serializer = AotAvroPoco.CreateAvroSerializer(registry);
        await using var deserializer = AotAvroPoco.CreateAvroDeserializer(registry);
        var poco = new AotAvroPoco { Id = 12, Name = "poco", Amount = 34.56m };
        var buffer = new ArrayBufferWriter<byte>();

        var schemaId = await serializer.WarmupAsync(ValueContext.Topic);
        await deserializer.WarmupAsync(schemaId);
        serializer.Serialize(poco, ref buffer, ValueContext);

        var roundTrip = deserializer.Deserialize(buffer.WrittenMemory, ValueContext);
        Require(roundTrip.Id == poco.Id, "POCO Avro ID mismatch.");
        Require(roundTrip.Name == poco.Name, "POCO Avro name mismatch.");
        Require(roundTrip.Amount == poco.Amount, "POCO Avro decimal mismatch.");
    }

    private static void RunRuleConfigurationRejected(InMemorySchemaRegistry registry)
    {
        // Rule processing parses schemas with Apache.Avro, so trimmed and NativeAOT publishes turn it off.
        if (RuntimeFeature.IsDynamicCodeSupported &&
            !(AppContext.TryGetSwitch("Dekaf.SchemaRegistry.Avro.Poco.RulesSupport.IsSupported", out var enabled) && !enabled))
            return;

        var ruleExecutor = new SchemaRegistryRuleExecutor([]);
        RequireNotSupported(
            () => AotAvroPoco.CreateAvroSerializer(registry, new AvroSerializerConfig { RuleExecutor = ruleExecutor }),
            "serializer rule executor");
        RequireNotSupported(
            () => AotAvroPoco.CreateAvroSerializer(
                registry,
                new AvroSerializerConfig { ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules }),
            "serializer inline validation");
        RequireNotSupported(
            () => AotAvroPoco.CreateAvroDeserializer(registry, new AvroDeserializerConfig { RuleExecutor = ruleExecutor }),
            "deserializer rule executor");
        RequireNotSupported(
            () => AotAvroPoco.CreateAvroDeserializer(
                registry,
                new AvroDeserializerConfig { ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules }),
            "deserializer inline validation");
        RequireNotSupported(
            () => AotAvroPoco.CreateAvroDeserializer(registry, new AvroDeserializerConfig { UseLatestVersion = true }),
            "deserializer migrations");
    }

    private static void RequireNotSupported(Func<object> create, string feature)
    {
        try
        {
            _ = create();
        }
        catch (NotSupportedException)
        {
            return;
        }

        throw new InvalidOperationException($"POCO Avro {feature} must be rejected under NativeAOT.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class InMemorySchemaRegistry : ISchemaRegistryClient, ISchemaRegistryCache
    {
        private readonly Dictionary<int, Schema> _schemasById = [];
        private readonly Dictionary<(Guid Guid, string? Format), Schema> _schemasByGuid = [];
        private readonly Dictionary<string, RegisteredSchema> _schemasBySubject = new(StringComparer.Ordinal);
        private int _nextId = 1;

        public Task<int> RegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_schemasBySubject.TryGetValue(subject, out var existing))
                return Task.FromResult(existing.Id);

            var id = _nextId++;
            var guid = GuidFromId(id);
            var registered = new RegisteredSchema
            {
                Id = id,
                Guid = guid.ToString(),
                Subject = subject,
                Version = 1,
                Schema = schema
            };
            _schemasBySubject[subject] = registered;
            _schemasById[id] = schema;
            _schemasByGuid[(guid, null)] = schema;

            return Task.FromResult(id);
        }

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_schemasById[id]);
        }

        public Task<Schema> GetSchemaByGuidAsync(
            string guid,
            string? format = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parsedGuid = Guid.Parse(guid);
            if (_schemasByGuid.TryGetValue((parsedGuid, format), out var cached))
                return Task.FromResult(cached);

            var schema = _schemasByGuid[(parsedGuid, null)];
            _schemasByGuid[(parsedGuid, format)] = schema;
            return Task.FromResult(schema);
        }

        public Task<Schema> GetSchemaAsync(
            int id,
            string subject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var registered = _schemasBySubject[subject];
            if (registered.Id != id)
                throw new KeyNotFoundException($"Schema {id} is not registered under subject '{subject}'.");

            return Task.FromResult(registered.Schema);
        }

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_schemasBySubject[subject]);
        }

        public Task<RegisteredSchema> LookupSchemaAsync(
            string subject,
            Schema schema,
            bool ignoreDeletedSchemas = true,
            bool normalize = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var registered = _schemasBySubject[subject];
            if (registered.Schema.SchemaType != schema.SchemaType
                || !string.Equals(registered.Schema.SchemaString, schema.SchemaString, StringComparison.Ordinal))
            {
                throw new KeyNotFoundException($"Schema is not registered under subject '{subject}'.");
            }

            return Task.FromResult(registered);
        }

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default)
            => RegisterSchemaAsync(subject, schema, cancellationToken);

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<string>>(_schemasBySubject.Keys.ToArray());
        }

        public Task<IReadOnlyList<int>> GetVersionsAsync(
            string subject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<int>>([_schemasBySubject[subject].Version]);
        }

        public Task<bool> IsCompatibleAsync(
            string subject,
            Schema schema,
            string version = "latest",
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(
            string subject,
            bool permanent = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_schemasBySubject.Remove(subject, out var registered))
                return Task.FromResult<IReadOnlyList<int>>([]);

            _schemasById.Remove(registered.Id);
            var guid = Guid.Parse(registered.Guid!);
            foreach (var key in _schemasByGuid.Keys.Where(key => key.Guid == guid).ToArray())
                _schemasByGuid.Remove(key);

            return Task.FromResult<IReadOnlyList<int>>([registered.Version]);
        }

        public bool TryGetCachedSchema(int id, out Schema schema)
        {
            if (_schemasById.TryGetValue(id, out var cached))
            {
                schema = cached;
                return true;
            }

            schema = null!;
            return false;
        }

        public bool TryGetCachedSchema(Guid guid, string? format, out Schema schema)
        {
            if (_schemasByGuid.TryGetValue((guid, format), out var cached))
            {
                schema = cached;
                return true;
            }

            schema = null!;
            return false;
        }

        public bool TryGetCachedSchema(int id, string subject, out Schema schema)
        {
            if (_schemasBySubject.TryGetValue(subject, out var registered)
                && registered.Id == id)
            {
                schema = registered.Schema;
                return true;
            }

            schema = null!;
            return false;
        }

        public void Dispose()
        {
        }

        private static Guid GuidFromId(int id) => new(id, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }
}

[AvroRecord(Name = "AotAvroPoco", Namespace = "Dekaf.Tests.Aot")]
internal sealed partial class AotAvroPoco
{
    public int Id { get; init; }
    public required string Name { get; init; }

    [AvroField(Precision = 8, Scale = 2)]
    public decimal Amount { get; init; }
}
