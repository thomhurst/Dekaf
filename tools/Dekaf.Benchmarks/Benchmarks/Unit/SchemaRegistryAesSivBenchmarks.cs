using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures warmed AES-SIV transforms, including short and multi-block payloads.</summary>
[MemoryDiagnoser]
public class SchemaRegistryAesSivBenchmarks
{
    private SchemaRegistryCsfleRuleHandler _handler = null!;
    private SchemaRegistryRuleHandlerContext _context = null!;
    private byte[] _plaintext = null!;
    private byte[] _ciphertext = null!;

    [Params(0, 15, 16, 17, 256, 4096)]
    public int PayloadSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _plaintext = new byte[PayloadSize];
        new Random(42).NextBytes(_plaintext);
        var rule = new SchemaRule
        {
            Name = "encrypt", Kind = SchemaRuleKind.Transform, Mode = SchemaRuleMode.WriteRead,
            Type = SchemaRegistryCsfleRuleHandler.EncryptRuleType,
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["encrypt.kek.name"] = "benchmark-kek", ["encrypt.dek.algorithm"] = "AES256_SIV"
            }
        };
        _handler = new SchemaRegistryCsfleRuleHandler(new KeyRegistry(), []);
        _context = new SchemaRegistryRuleHandlerContext
        {
            Rule = rule, Direction = SchemaRegistryRuleDirection.Write,
            PayloadContext = new SchemaRegistryRuleContext
            {
                Topic = "benchmark", Subject = "benchmark-value", SchemaId = 1, Component = SerializationComponent.Value,
                PayloadFormat = SchemaRegistryPayloadFormat.Custom
            }
        };
        _ciphertext = _handler.TransformSerializedPayload(_plaintext, _context).ToArray();
        _ = _handler.TransformDeserializedPayload(_ciphertext, _context);
    }

    [Benchmark]
    public ReadOnlyMemory<byte> Encrypt() => _handler.TransformSerializedPayload(_plaintext, _context);

    [Benchmark]
    public ReadOnlyMemory<byte> Decrypt() => _handler.TransformDeserializedPayload(_ciphertext, _context);

    private sealed class KeyRegistry : ISchemaRegistryClient
    {
        private static readonly Task<Kek> KeyEncryptionKey = Task.FromResult(new Kek
        {
            Name = "benchmark-kek", KmsType = "local-kms", KmsKeyId = "benchmark-key"
        });
        private static readonly Task<Dek> DataEncryptionKey = Task.FromResult(new Dek
        {
            KekName = "benchmark-kek", Subject = "benchmark-value", Version = 1,
            Algorithm = DekAlgorithm.Aes256Siv, KeyMaterial = Convert.ToBase64String(new byte[64])
        });

        public Task<Kek> GetKekAsync(string name, bool deleted = false, CancellationToken cancellationToken = default) => KeyEncryptionKey;
        public Task<Dek> GetDekAsync(string kekName, string subject, DekAlgorithm? algorithm = null,
            bool deleted = false, CancellationToken cancellationToken = default) => DataEncryptionKey;
        public Task<int> RegisterSchemaAsync(string subject, Schema schema, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RegisteredSchema> GetSchemaBySubjectAsync(string subject, string version = "latest", CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> GetOrRegisterSchemaAsync(string subject, Schema schema, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> GetVersionsAsync(string subject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsCompatibleAsync(string subject, Schema schema, string version = "latest", CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> DeleteSubjectAsync(string subject, bool permanent = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Dispose() { }
    }
}
