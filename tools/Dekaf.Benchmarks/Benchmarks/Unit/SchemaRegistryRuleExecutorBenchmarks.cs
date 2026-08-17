using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Guards the Schema Registry rule executor's no-rules and active-rule paths.
/// </summary>
[MemoryDiagnoser]
public class SchemaRegistryRuleExecutorBenchmarks
{
    private readonly byte[] _payload = "benchmark-payload"u8.ToArray();
    private readonly SchemaRegistryRuleExecutor _executor = new([PassThroughRuleHandler.Instance]);
    private readonly SchemaRegistryRuleExecutor _celExecutor = new([new CelSchemaRegistryRuleHandler()]);
    private readonly IJsonSchemaValidator _validator = NoOpJsonSchemaValidator.Instance;
    private readonly SchemaRegistryRuleExecutor _multipleHandlerExecutor = new(
    [
        new PassThroughRuleHandler("A"),
        new PassThroughRuleHandler("B"),
        new PassThroughRuleHandler("C"),
        PassThroughRuleHandler.Instance
    ]);
    private readonly SchemaRegistryRuleContext _schemaWithoutRules = CreateContext(
        new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}"
        });
    private readonly SchemaRegistryRuleContext _emptyRuleSet = CreateContext(
        new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            RuleSet = new SchemaRuleSet
            {
                DomainRules = [],
                EncodingRules = [],
                HasFixedRuleCollections = true
            }
        });
    private readonly SchemaRegistryRuleContext _activeDomainRule = CreateContext(
        new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            RuleSet = new SchemaRuleSet
            {
                HasFixedRuleCollections = true,
                DomainRules =
                [
                    new SchemaRule
                    {
                        Name = "benchmark-rule",
                        Kind = SchemaRuleKind.Transform,
                        Mode = SchemaRuleMode.Write,
                        Type = PassThroughRuleHandler.RuleType
                    }
                ]
            }
        });
    private readonly SchemaRegistryRuleContext _activeDomainRuleWithInactiveRules = CreateContext(
        CreatePassThroughSchema(inactiveRuleCount: 32));
    private readonly SchemaRegistryRuleContext _activeDomainAndEncodingRules = CreateContext(
        new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            RuleSet = new SchemaRuleSet
            {
                HasFixedRuleCollections = true,
                DomainRules = [CreatePassThroughRule("domain-rule")],
                EncodingRules = [CreatePassThroughRule("encoding-rule")]
            }
        });
    private readonly SchemaRegistryRuleContext _activeCelCondition = CreateContext(
        CreateCelSchema(
            SchemaRuleKind.Condition,
            "contains(message, \"mark\") && payload == \"benchmark-payload\" && format == \"Json\""));
    private readonly SchemaRegistryRuleContext _activeCelTransform = CreateContext(
        CreateCelSchema(SchemaRuleKind.Transform, "metadata(\"replacement\")"));

    [GlobalSetup]
    public void WarmHandlerContextPool()
    {
        _executor.TransformSerializedPayload(_payload, _activeDomainRule);
        _executor.TransformSerializedPayload(_payload, _activeDomainRuleWithInactiveRules);
        _executor.TransformSerializedPayload(_payload, _activeDomainAndEncodingRules, _validator, schemaId: 1);
        _multipleHandlerExecutor.TransformSerializedPayload(_payload, _activeDomainRule);
        _celExecutor.TransformSerializedPayload(_payload, _activeCelCondition);
        _celExecutor.TransformSerializedPayload(_payload, _activeCelTransform);
    }

    [Benchmark(Baseline = true)]
    public ReadOnlyMemory<byte> SchemaWithoutRules() =>
        _executor.TransformSerializedPayload(_payload, _schemaWithoutRules);

    [Benchmark]
    public ReadOnlyMemory<byte> EmptyRuleSet() =>
        _executor.TransformSerializedPayload(_payload, _emptyRuleSet);

    [Benchmark]
    public ReadOnlyMemory<byte> ActiveDomainRule() =>
        _executor.TransformSerializedPayload(_payload, _activeDomainRule);

    [Benchmark]
    public ReadOnlyMemory<byte> ActiveDomainRuleMultipleHandlers() =>
        _multipleHandlerExecutor.TransformSerializedPayload(_payload, _activeDomainRule);

    [Benchmark]
    public ReadOnlyMemory<byte> ActiveDomainRuleAfterInactiveRules() =>
        _executor.TransformSerializedPayload(_payload, _activeDomainRuleWithInactiveRules);

    [Benchmark]
    public ReadOnlyMemory<byte> ActiveDomainAndEncodingRulesWithValidation() =>
        _executor.TransformSerializedPayload(_payload, _activeDomainAndEncodingRules, _validator, schemaId: 1);

    [Benchmark]
    public ReadOnlyMemory<byte> ActiveCelDomainCondition() =>
        _celExecutor.TransformSerializedPayload(_payload, _activeCelCondition);

    [Benchmark]
    public ReadOnlyMemory<byte> ActiveCelDomainTransform() =>
        _celExecutor.TransformSerializedPayload(_payload, _activeCelTransform);

    private static Schema CreateCelSchema(SchemaRuleKind kind, string expression) =>
        new()
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            Metadata = new SchemaMetadata
            {
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["replacement"] = "rewritten-payload"
                }
            },
            RuleSet = new SchemaRuleSet
            {
                HasFixedRuleCollections = true,
                DomainRules =
                [
                    new SchemaRule
                    {
                        Name = "benchmark-cel-rule",
                        Kind = kind,
                        Mode = SchemaRuleMode.Write,
                        Type = "CEL",
                        Expr = expression
                    }
                ]
            }
        };

    private static Schema CreatePassThroughSchema(int inactiveRuleCount)
    {
        var rules = new SchemaRule[inactiveRuleCount + 1];
        for (var i = 0; i < inactiveRuleCount; i++)
        {
            rules[i] = new SchemaRule
            {
                Name = "inactive-rule",
                Kind = SchemaRuleKind.Transform,
                Mode = SchemaRuleMode.Write,
                Type = "MISSING",
                Disabled = true
            };
        }

        rules[^1] = new SchemaRule
        {
            Name = "benchmark-rule",
            Kind = SchemaRuleKind.Transform,
            Mode = SchemaRuleMode.Write,
            Type = PassThroughRuleHandler.RuleType
        };

        return new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            RuleSet = new SchemaRuleSet
            {
                DomainRules = rules,
                HasFixedRuleCollections = true
            }
        };
    }

    private static SchemaRule CreatePassThroughRule(string name) =>
        new()
        {
            Name = name,
            Kind = SchemaRuleKind.Transform,
            Mode = SchemaRuleMode.Write,
            Type = PassThroughRuleHandler.RuleType
        };

    private static SchemaRegistryRuleContext CreateContext(Schema schema) =>
        new()
        {
            Topic = "benchmark-topic",
            Component = SerializationComponent.Value,
            SchemaId = 1,
            Subject = "benchmark-topic-value",
            Schema = schema,
            PayloadFormat = SchemaRegistryPayloadFormat.Json
        };

    private sealed class PassThroughRuleHandler : ISchemaRegistryRuleHandler
    {
        public const string RuleType = "BENCHMARK";

        public static PassThroughRuleHandler Instance { get; } = new(RuleType);

        public PassThroughRuleHandler(string type) => Type = type;

        public string Type { get; }

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => payload;
    }

    private sealed class NoOpJsonSchemaValidator : IJsonSchemaValidator
    {
        public static NoOpJsonSchemaValidator Instance { get; } = new();

        public void Validate(ReadOnlySpan<byte> payload, int schemaId)
        {
        }
    }
}

/// <summary>
/// Guards CEL comparisons against allocating when callers supply a fresh equal context string.
/// One invocation per iteration keeps the identity-cache miss visible while excluding setup.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 10, invocationCount: 1)]
public class SchemaRegistryCelFreshContextBenchmarks
{
    private static readonly byte[] Payload = "benchmark-payload"u8.ToArray();
    private readonly SchemaRegistryRuleExecutor _executor = new([new CelSchemaRegistryRuleHandler()]);
    private readonly Schema _schema = new()
    {
        SchemaType = SchemaType.Json,
        SchemaString = "{}",
        RuleSet = new SchemaRuleSet
        {
            HasFixedRuleCollections = true,
            DomainRules =
            [
                new SchemaRule
                {
                    Name = "fresh-topic",
                    Kind = SchemaRuleKind.Condition,
                    Mode = SchemaRuleMode.Write,
                    Type = "CEL",
                    Expr = "message == topic"
                }
            ]
        }
    };
    private readonly Schema _transformSchema = new()
    {
        SchemaType = SchemaType.Json,
        SchemaString = "{}",
        RuleSet = new SchemaRuleSet
        {
            HasFixedRuleCollections = true,
            DomainRules =
            [
                new SchemaRule
                {
                    Name = "fresh-topic-transform",
                    Kind = SchemaRuleKind.Transform,
                    Mode = SchemaRuleMode.Write,
                    Type = "CEL",
                    Expr = "topic"
                }
            ]
        }
    };
    private SchemaRegistryRuleContext _context = null!;
    private SchemaRegistryRuleContext _transformContext = null!;

    [IterationSetup]
    public void CreateFreshTopicContext() =>
        (_context, _transformContext) =
        (
            new SchemaRegistryRuleContext
            {
                Topic = new string("benchmark-payload".AsSpan()),
                Component = SerializationComponent.Value,
                SchemaId = 1,
                Subject = "benchmark-topic-value",
                Schema = _schema,
                PayloadFormat = SchemaRegistryPayloadFormat.Json
            },
            new SchemaRegistryRuleContext
            {
                Topic = new string("benchmark-payload".AsSpan()),
                Component = SerializationComponent.Value,
                SchemaId = 1,
                Subject = "benchmark-topic-value",
                Schema = _transformSchema,
                PayloadFormat = SchemaRegistryPayloadFormat.Json
            }
        );

    [Benchmark]
    public ReadOnlyMemory<byte> ActiveCelConditionWithFreshEqualTopic() =>
        _executor.TransformSerializedPayload(Payload, _context);

    [Benchmark]
    public ReadOnlyMemory<byte> ActiveCelTransformWithFreshEqualTopic() =>
        _executor.TransformSerializedPayload(Payload, _transformContext);
}

/// <summary>
/// Guards the full custom serializer and deserializer rule-enabled paths against per-message context allocations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 10)]
public class SchemaRegistryRuleContextBenchmarks
{
    private static readonly byte[] Payload = "benchmark-payload"u8.ToArray();
    private static readonly Schema BenchmarkSchema = new()
    {
        SchemaType = SchemaType.Json,
        SchemaString = "{}"
    };

    private readonly ArrayBufferWriter<byte> _destination = new(256);
    private readonly SerializationContext _context = new()
    {
        Topic = "benchmark-topic",
        Component = SerializationComponent.Value
    };
    private ReadOnlyMemory<byte> _encodedPayload;
    private SchemaRegistryDeserializer<ReadOnlyMemory<byte>> _deserializer = null!;
    private SchemaRegistrySerializer<byte[]> _serializer = null!;

    [GlobalSetup]
    public void Setup()
    {
        var client = new BenchmarkSchemaRegistryClient();
        _serializer = new SchemaRegistrySerializer<byte[]>(
            client,
            static (value, writer) => writer.Write(value),
            static () => BenchmarkSchema,
            ruleExecutor: PassThroughRuleExecutor.Instance);
        var destination = _destination;
        _serializer.Serialize(Payload, ref destination, _context);
        _encodedPayload = _destination.WrittenMemory.ToArray();
        _deserializer = new SchemaRegistryDeserializer<ReadOnlyMemory<byte>>(
            client,
            static (payload, _) => payload,
            ownsClient: false,
            ruleExecutor: PassThroughRuleExecutor.Instance);
        _ = _deserializer.Deserialize(_encodedPayload, _context);
    }

    [Benchmark]
    public void SerializeWithRuleExecutor()
    {
        _destination.ResetWrittenCount();
        var destination = _destination;
        _serializer.Serialize(Payload, ref destination, _context);
    }

    [Benchmark]
    public ReadOnlyMemory<byte> DeserializeWithRuleExecutor() =>
        _deserializer.Deserialize(_encodedPayload, _context);

    private sealed class PassThroughRuleExecutor : ISchemaRegistryRuleExecutor
    {
        public static PassThroughRuleExecutor Instance { get; } = new();

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;
    }

    private sealed class BenchmarkSchemaRegistryClient : ISchemaRegistryClient, ISchemaRegistryCache
    {
        public bool TryGetCachedSchema(int id, out Schema schema)
        {
            schema = BenchmarkSchema;
            return true;
        }

        public bool TryGetCachedSchema(int id, string subject, out Schema schema)
        {
            schema = BenchmarkSchema;
            return true;
        }

        public Task<int> RegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(BenchmarkSchema);

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) => Task.FromResult(1);

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

/// <summary>
/// Guards cached migration planning and disabled/active migration execution allocations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 10)]
public class SchemaRegistryMigrationBenchmarks
{
    private static readonly byte[] Payload = "benchmark-payload"u8.ToArray();
    private static readonly SerializationContext Context = new()
    {
        Topic = "benchmark-topic",
        Component = SerializationComponent.Value
    };

    private SchemaRegistryMigrationRunner _noMigration = null!;
    private SchemaRegistryMigrationRunner _disabledMigration = null!;
    private SchemaRegistryMigrationRunner _activeMigration = null!;
    private Schema _noMigrationWriter = null!;
    private Schema _disabledWriter = null!;
    private Schema _activeWriter = null!;

    [GlobalSetup]
    public void Setup()
    {
        var executor = new SchemaRegistryRuleExecutor([PassThroughMigrationHandler.Instance]);
        (_noMigration, _noMigrationWriter) = CreateRunner(executor, targetRule: null, sameVersion: true);
        (_disabledMigration, _disabledWriter) = CreateRunner(
            executor,
            CreateMigrationRule(disabled: true),
            sameVersion: false);
        (_activeMigration, _activeWriter) = CreateRunner(
            executor,
            CreateMigrationRule(disabled: false),
            sameVersion: false);

        _ = NoMigration();
        _ = DisabledMigration();
        _ = ActiveMigration();
    }

    [Benchmark(Baseline = true)]
    public ReadOnlyMemory<byte> NoMigration() =>
        _noMigration.Transform(
            Payload,
            1,
            "benchmark-topic-value",
            _noMigrationWriter,
            Context,
            SchemaRegistryPayloadFormat.Json).Payload;

    [Benchmark]
    public ReadOnlyMemory<byte> DisabledMigration() =>
        _disabledMigration.Transform(
            Payload,
            1,
            "benchmark-topic-value",
            _disabledWriter,
            Context,
            SchemaRegistryPayloadFormat.Json).Payload;

    [Benchmark]
    public ReadOnlyMemory<byte> ActiveMigration() =>
        _activeMigration.Transform(
            Payload,
            1,
            "benchmark-topic-value",
            _activeWriter,
            Context,
            SchemaRegistryPayloadFormat.Json).Payload;

    private static (SchemaRegistryMigrationRunner Runner, Schema Writer) CreateRunner(
        SchemaRegistryRuleExecutor executor,
        SchemaRule? targetRule,
        bool sameVersion)
    {
        var writer = new Schema { SchemaType = SchemaType.Json, SchemaString = "writer" };
        var reader = sameVersion
            ? writer
            : new Schema
            {
                SchemaType = SchemaType.Json,
                SchemaString = "reader",
                RuleSet = new SchemaRuleSet { MigrationRules = targetRule is null ? [] : [targetRule] }
            };
        var client = new MigrationBenchmarkRegistryClient(writer, reader);
        return (new SchemaRegistryMigrationRunner(client, executor, TimeSpan.FromSeconds(1)), writer);
    }

    private static SchemaRule CreateMigrationRule(bool disabled) =>
        new()
        {
            Name = "benchmark-migration",
            Kind = SchemaRuleKind.Transform,
            Mode = SchemaRuleMode.Upgrade,
            Type = PassThroughMigrationHandler.RuleType,
            Disabled = disabled
        };

    private sealed class PassThroughMigrationHandler : ISchemaRegistryRuleHandler
    {
        internal const string RuleType = "MIGRATION-BENCHMARK";
        internal static PassThroughMigrationHandler Instance { get; } = new();

        public string Type => RuleType;

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => payload;
    }

    private sealed class MigrationBenchmarkRegistryClient(
        Schema writer,
        Schema reader) : ISchemaRegistryClient
    {
        private readonly RegisteredSchema _writer = new()
        {
            Id = 1,
            Subject = "benchmark-topic-value",
            Version = 1,
            Schema = writer
        };
        private readonly RegisteredSchema _reader = new()
        {
            Id = ReferenceEquals(writer, reader) ? 1 : 2,
            Subject = "benchmark-topic-value",
            Version = ReferenceEquals(writer, reader) ? 1 : 2,
            Schema = reader
        };

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == 1 ? _writer.Schema : _reader.Schema);

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default) => Task.FromResult(_reader);

        public Task<RegisteredSchema> LookupSchemaAsync(
            string subject,
            Schema schema,
            bool ignoreDeletedSchemas = true,
            bool normalize = false,
            CancellationToken cancellationToken = default) => Task.FromResult(_writer);

        public Task<int> RegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

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
