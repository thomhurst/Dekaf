using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Guards warmed CSFLE domain-rule transforms against per-message allocations.
/// </summary>
[MemoryDiagnoser]
public class SchemaRegistryCsfleRuleBenchmarks
{
    private static readonly byte[] Payload = "benchmark-payload"u8.ToArray();
    private static readonly byte[] JsonPayload = "{\"name\":\"Ada\",\"ssn\":\"123-45-6789\"}"u8.ToArray();

    private SchemaRegistryRuleExecutor _executor = null!;
    private SchemaRegistryRuleContext _wholePayloadContext = null!;
    private SchemaRegistryRuleContext _mutableWholePayloadContext = null!;
    private SchemaRegistryRuleContext _deterministicContext = null!;
    private SchemaRegistryRuleContext _taggedJsonContext = null!;
    private SchemaRegistryRuleContext _mutableTaggedJsonContext = null!;
    private SchemaRegistryRuleContext _mutableSortedTaggedJsonContext = null!;
    private byte[] _encryptedPayload = null!;
    private byte[] _mutableEncryptedPayload = null!;
    private byte[] _deterministicEncryptedPayload = null!;
    private byte[] _encryptedJsonPayload = null!;
    private byte[] _mutableEncryptedJsonPayload = null!;
    private byte[] _mutableSortedEncryptedJsonPayload = null!;

    [GlobalSetup]
    public void Setup()
    {
        var client = new BenchmarkSchemaRegistryClient();
        _executor = new SchemaRegistryRuleExecutor([new SchemaRegistryCsfleRuleHandler(client, [])]);
        _wholePayloadContext = CreateContext(CreateSchema(CreateRule()));
        _mutableWholePayloadContext = CreateContext(CreateSchema(CreateRule(), fixedCollections: false));
        _deterministicContext = CreateContext(CreateSchema(CreateRule(algorithm: "AES256_SIV")));

        var taggedRule = CreateRule(new HashSet<string>(StringComparer.Ordinal) { "PII" });
        _taggedJsonContext = CreateContext(
            CreateSchema(
                taggedRule,
                new SchemaMetadata
                {
                    Tags = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
                    {
                        ["$.ssn"] = new HashSet<string>(StringComparer.Ordinal) { "PII" }
                    }
                }),
            SchemaRegistryPayloadFormat.Json);
        _mutableTaggedJsonContext = CreateContext(
            CreateSchema(
                taggedRule,
                new SchemaMetadata
                {
                    Tags = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
                    {
                        ["$.ssn"] = new HashSet<string>(StringComparer.Ordinal) { "PII" }
                    }
                },
                fixedCollections: false),
            SchemaRegistryPayloadFormat.Json);
        var sortedTaggedRule = CreateRule(new SortedSet<string>(StringComparer.Ordinal) { "PII" });
        _mutableSortedTaggedJsonContext = CreateContext(
            CreateSchema(
                sortedTaggedRule,
                new SchemaMetadata
                {
                    Tags = new SortedDictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
                    {
                        ["$.ssn"] = new SortedSet<string>(StringComparer.Ordinal) { "PII" }
                    }
                },
                fixedCollections: false),
            SchemaRegistryPayloadFormat.Json);
        _encryptedPayload = _executor.TransformSerializedPayload(Payload, _wholePayloadContext).ToArray();
        _mutableEncryptedPayload = _executor.TransformSerializedPayload(Payload, _mutableWholePayloadContext).ToArray();
        _deterministicEncryptedPayload = _executor.TransformSerializedPayload(Payload, _deterministicContext).ToArray();
        _encryptedJsonPayload = _executor.TransformSerializedPayload(JsonPayload, _taggedJsonContext).ToArray();
        _mutableEncryptedJsonPayload = _executor.TransformSerializedPayload(JsonPayload, _mutableTaggedJsonContext).ToArray();
        _mutableSortedEncryptedJsonPayload = _executor
            .TransformSerializedPayload(JsonPayload, _mutableSortedTaggedJsonContext)
            .ToArray();

        Warm();
        Warm();
    }

    [Benchmark]
    public ReadOnlyMemory<byte> EncryptWholePayload() =>
        _executor.TransformSerializedPayload(Payload, _wholePayloadContext);

    [Benchmark]
    public ReadOnlyMemory<byte> DecryptWholePayload() =>
        _executor.TransformDeserializedPayload(_encryptedPayload, _wholePayloadContext);

    [Benchmark]
    public ReadOnlyMemory<byte> EncryptMutableWholePayload() =>
        _executor.TransformSerializedPayload(Payload, _mutableWholePayloadContext);

    [Benchmark]
    public ReadOnlyMemory<byte> DecryptMutableWholePayload() =>
        _executor.TransformDeserializedPayload(_mutableEncryptedPayload, _mutableWholePayloadContext);

    [Benchmark]
    public ReadOnlyMemory<byte> EncryptDeterministicWholePayload() =>
        _executor.TransformSerializedPayload(Payload, _deterministicContext);

    [Benchmark]
    public ReadOnlyMemory<byte> DecryptDeterministicWholePayload() =>
        _executor.TransformDeserializedPayload(_deterministicEncryptedPayload, _deterministicContext);

    [Benchmark]
    public ReadOnlyMemory<byte> EncryptTaggedJsonField() =>
        _executor.TransformSerializedPayload(JsonPayload, _taggedJsonContext);

    [Benchmark]
    public ReadOnlyMemory<byte> DecryptTaggedJsonField() =>
        _executor.TransformDeserializedPayload(_encryptedJsonPayload, _taggedJsonContext);

    [Benchmark]
    public ReadOnlyMemory<byte> EncryptMutableTaggedJsonField() =>
        _executor.TransformSerializedPayload(JsonPayload, _mutableTaggedJsonContext);

    [Benchmark]
    public ReadOnlyMemory<byte> DecryptMutableTaggedJsonField() =>
        _executor.TransformDeserializedPayload(_mutableEncryptedJsonPayload, _mutableTaggedJsonContext);

    [Benchmark]
    public ReadOnlyMemory<byte> EncryptMutableSortedTaggedJsonField() =>
        _executor.TransformSerializedPayload(JsonPayload, _mutableSortedTaggedJsonContext);

    [Benchmark]
    public ReadOnlyMemory<byte> DecryptMutableSortedTaggedJsonField() =>
        _executor.TransformDeserializedPayload(_mutableSortedEncryptedJsonPayload, _mutableSortedTaggedJsonContext);

    private void Warm()
    {
        EncryptWholePayload();
        DecryptWholePayload();
        EncryptMutableWholePayload();
        DecryptMutableWholePayload();
        EncryptDeterministicWholePayload();
        DecryptDeterministicWholePayload();
        EncryptTaggedJsonField();
        DecryptTaggedJsonField();
        EncryptMutableTaggedJsonField();
        DecryptMutableTaggedJsonField();
        EncryptMutableSortedTaggedJsonField();
        DecryptMutableSortedTaggedJsonField();
    }

    private static SchemaRule CreateRule(IReadOnlySet<string>? tags = null, string? algorithm = null) =>
        new()
        {
            Name = "benchmark-encrypt",
            Kind = SchemaRuleKind.Transform,
            Mode = SchemaRuleMode.WriteRead,
            Type = SchemaRegistryCsfleRuleHandler.EncryptRuleType,
            Tags = tags,
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["encrypt.kek.name"] = "benchmark-kek",
                ["encrypt.dek.algorithm"] = algorithm ?? "AES256_GCM"
            }
        };

    private static Schema CreateSchema(
        SchemaRule rule,
        SchemaMetadata? metadata = null,
        bool fixedCollections = true) =>
        new()
        {
            SchemaType = metadata is null ? SchemaType.Avro : SchemaType.Json,
            SchemaString = "{}",
            Metadata = metadata,
            RuleSet = new SchemaRuleSet
            {
                DomainRules = [rule],
                HasFixedRuleCollections = fixedCollections
            }
        };

    private static SchemaRegistryRuleContext CreateContext(
        Schema schema,
        SchemaRegistryPayloadFormat format = SchemaRegistryPayloadFormat.Custom) =>
        new()
        {
            Topic = "benchmark-topic",
            Component = SerializationComponent.Value,
            SchemaId = 1,
            Subject = "benchmark-topic-value",
            Schema = schema,
            PayloadFormat = format
        };

    private sealed class BenchmarkSchemaRegistryClient : ISchemaRegistryClient
    {
        private static readonly Kek BenchmarkKek = new()
        {
            Name = "benchmark-kek",
            KmsType = "local-kms",
            KmsKeyId = "benchmark-key"
        };

        private static readonly Dek BenchmarkGcmDek = new()
        {
            KekName = "benchmark-kek",
            Subject = "benchmark-topic-value",
            Version = 1,
            Algorithm = DekAlgorithm.Aes256Gcm,
            KeyMaterial = Convert.ToBase64String(new byte[32]),
            Timestamp = 0
        };

        private static readonly Dek BenchmarkSivDek = new()
        {
            KekName = "benchmark-kek",
            Subject = "benchmark-topic-value",
            Version = 2,
            Algorithm = DekAlgorithm.Aes256Siv,
            KeyMaterial = Convert.ToBase64String(new byte[64]),
            Timestamp = 0
        };

        public Task<Kek> GetKekAsync(
            string name,
            bool deleted = false,
            CancellationToken cancellationToken = default) => Task.FromResult(BenchmarkKek);

        public Task<Dek> GetDekAsync(
            string kekName,
            string subject,
            DekAlgorithm? algorithm = null,
            bool deleted = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(algorithm == DekAlgorithm.Aes256Siv ? BenchmarkSivDek : BenchmarkGcmDek);

        public Task<Dek> GetDekAsync(
            string kekName,
            string subject,
            int version,
            bool deleted = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(version == BenchmarkSivDek.Version ? BenchmarkSivDek : BenchmarkGcmDek);

        public Task<int> RegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<int>> GetVersionsAsync(
            string subject,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> IsCompatibleAsync(
            string subject,
            Schema schema,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(
            string subject,
            bool permanent = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
