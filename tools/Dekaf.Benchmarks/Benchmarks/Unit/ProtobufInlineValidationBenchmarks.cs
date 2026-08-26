using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Dekaf.Tests.Unit.SchemaRegistry.ProtobufFixtures;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Protects warmed valid Protobuf inline CHECK validation from per-message allocations.
/// Descriptor traversal, rule compilation, schema registration, and pools are warmed in setup.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class ProtobufInlineValidationBenchmarks
{
    private readonly ArrayBufferWriter<byte> _destination = new(512);
    private ProtobufInlineRuleValidator _validator = null!;
    private ProtobufSchemaRegistrySerializer<ValidationEnvelope> _serializer = null!;
    private ProtobufSchemaRegistrySerializer<ValidationEnvelope> _unvalidatedSerializer = null!;
    private CompiledValidationRule _mixedNumericRule = null!;
    private ValidationEnvelope _message = null!;
    private byte[] _payload = null!;
    private SerializationContext _context;

    [GlobalSetup]
    public void Setup()
    {
        _message = CreateMessage();
        _payload = _message.ToByteArray();
        _validator = new ProtobufInlineRuleValidator(ValidationEnvelope.Descriptor);
        _serializer = new ProtobufSchemaRegistrySerializer<ValidationEnvelope>(
            new BenchmarkSchemaRegistryClient(),
            new ProtobufSerializerConfig
            {
                UseSchemaReferences = false,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            });
        _unvalidatedSerializer = new ProtobufSchemaRegistrySerializer<ValidationEnvelope>(
            new BenchmarkSchemaRegistryClient(),
            new ProtobufSerializerConfig { UseSchemaReferences = false });
        _mixedNumericRule = CompiledValidationRule.Compile(
            new ValidationRule { Name = "mixed-numeric", Expr = "this > 9007199254740992.0" },
            new Dictionary<string, int>(StringComparer.Ordinal),
            [],
            []);
        _context = new SerializationContext
        {
            Topic = "protobuf-inline-validation",
            Component = SerializationComponent.Value
        };

        _validator.Validate(_payload, schemaId: 1, failFast: false);
        var destination = _destination;
        _serializer.Serialize(_message, ref destination, _context);
        _destination.Clear();
        destination = _destination;
        _unvalidatedSerializer.Serialize(_message, ref destination, _context);
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await _serializer.DisposeAsync();
        await _unvalidatedSerializer.DisposeAsync();
    }

    [Benchmark]
    public void ValidateRules() => _validator.Validate(_payload, schemaId: 1, failFast: false);

    [Benchmark]
    public bool ValidateMixedNumericComparison() => _mixedNumericRule.Evaluate(
            ValidationCelValue.FromNumber(9007199254740993m),
            nowUnixMilliseconds: 0,
            default,
            default,
            equalityGeneration: 0)
        .Boolean;

    [Benchmark]
    public void SerializeValidated()
    {
        _destination.Clear();
        var destination = _destination;
        _serializer.Serialize(_message, ref destination, _context);
    }

    [Benchmark(Baseline = true)]
    public void SerializeWithoutValidation()
    {
        _destination.Clear();
        var destination = _destination;
        _unvalidatedSerializer.Serialize(_message, ref destination, _context);
    }

    private static ValidationEnvelope CreateMessage()
    {
        var message = new ValidationEnvelope
        {
            Age = 42,
            Name = "Dekaf",
            Email = "test@example.com",
            Token = ByteString.CopyFromUtf8("abc"),
            Status = ValidationStatus.Active,
            CreatedAt = Timestamp.FromDateTime(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            Score = 7
        };
        message.Tags.Add("fast");
        message.Tags.Add("native");
        message.Children.Add(new ValidationChild { Value = 1 });
        message.Codes.Add(1);
        message.Codes.Add(2);
        message.Codes.Add(3);
        return message;
    }

    private sealed class BenchmarkSchemaRegistryClient : ISchemaRegistryClient
    {
        public Task<int> RegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
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

        public void Dispose() { }
    }
}
