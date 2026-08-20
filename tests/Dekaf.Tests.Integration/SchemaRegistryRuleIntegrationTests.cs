using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Serialization;
using Avro.Generic;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Json;
using Dekaf.SchemaRegistry.Jsonata;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Dekaf.Tests.Integration.Protos;
using Google.Protobuf;
using AvroSchema = Avro.Schema;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class SchemaRegistryRuleIntegrationTests(KafkaWithSchemaRegistryContainer testInfra)
{
    [Test]
    public async Task RegisteredAvroCsfleRule_ProduceConsume_RoundTripsTaggedField()
    {
        const string schemaText = """
            {
              "type": "record",
              "name": "EncryptedPayment",
              "namespace": "Dekaf.Tests.Integration",
              "fields": [
                { "name": "id", "type": "string" },
                { "name": "secret", "type": "string", "confluent:tags": ["PII"] }
              ]
            }
            """;
        var topic = await testInfra.CreateTestTopicAsync();
        using var registryClient = new CsfleTestRegistryClient(
            new SchemaRegistryClient(new SchemaRegistryConfig
            {
                Url = testInfra.RegistryUrl
            }));
        var rule = new SchemaRule
        {
            Name = "encrypt-pii",
            Kind = SchemaRuleKind.Transform,
            Mode = SchemaRuleMode.WriteRead,
            Type = SchemaRegistryCsfleRuleHandler.EncryptRuleType,
            Tags = new HashSet<string>(StringComparer.Ordinal) { "PII" },
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["encrypt.kek.name"] = $"integration-kek-{Guid.NewGuid():N}",
                ["encrypt.kms.type"] = LocalKmsProvider.DefaultType,
                ["encrypt.kms.key.id"] = "local://integration"
            }
        };
        await registryClient.RegisterSchemaAsync($"{topic}-value", new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = schemaText,
            RuleSet = new SchemaRuleSet { DomainRules = [rule] }
        });
        var kms = new LocalKmsProvider(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["local://integration"] = Enumerable.Range(1, 32).Select(static value => (byte)value).ToArray()
        });
        var executor = new SchemaRegistryRuleExecutor(
        [
            new SchemaRegistryCsfleRuleHandler(registryClient, [kms])
        ]);
        var avroSchema = (Avro.RecordSchema)AvroSchema.Parse(schemaText);
        var record = new GenericRecord(avroSchema);
        record.Add("id", "payment-1");
        record.Add("secret", "account-secret");
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            registryClient,
            new AvroSerializerConfig { AutoRegisterSchemas = false, RuleExecutor = executor });
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(
            registryClient,
            new AvroDeserializerConfig { RuleExecutor = executor });
        var serializationContext = CreateContext(topic);
        var encrypted = new ArrayBufferWriter<byte>();
        serializer.Serialize(record, ref encrypted, serializationContext);

        await Assert.That(encrypted.WrittenSpan.IndexOf("account-secret"u8)).IsEqualTo(-1);
        await Assert.That(encrypted.WrittenSpan.IndexOf("payment-1"u8)).IsGreaterThanOrEqualTo(0);

        await using var producer = await Kafka.CreateProducer<string, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithValueSerializer(serializer)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        await producer.ProduceAsync(new ProducerMessage<string, GenericRecord>
        {
            Topic = topic,
            Key = "key",
            Value = record
        });
        await using var consumer = await Kafka.CreateConsumer<string, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"avro-csfle-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(deserializer)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        consumer.Subscribe(topic);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        GenericRecord? consumed = null;
        await foreach (var message in consumer.ConsumeAsync(cts.Token))
        {
            consumed = message.Value;
            break;
        }

        await Assert.That((string)consumed!["id"]!).IsEqualTo("payment-1");
        await Assert.That((string)consumed["secret"]!).IsEqualTo("account-secret");
    }

    [Test]
    public async Task RegisteredJsonataMigrationRule_UseLatestVersion_TransformsPayload()
    {
        var topic = await testInfra.CreateTestTopicAsync();
        var subject = $"{topic}-value";
        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });
        var v1 = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = """{ "type": "object", "properties": { "first": { "type": "string" }, "last": { "type": "string" } }, "additionalProperties": false }"""
        };
        var v2 = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = """{ "type": "object", "properties": { "first": { "type": "string" }, "last": { "type": "string" }, "fullName": { "type": "string" } }, "additionalProperties": false }""",
            RuleSet = new SchemaRuleSet
            {
                MigrationRules =
                [
                    new SchemaRule
                    {
                        Name = "add-full-name",
                        Kind = SchemaRuleKind.Transform,
                        Mode = SchemaRuleMode.Upgrade,
                        Type = JsonataSchemaRegistryRuleHandler.RuleType,
                        Expr = "$merge([$, {'fullName': first & ' ' & last}])"
                    }
                ]
            }
        };
        var writerId = await registryClient.RegisterSchemaAsync(subject, v1);
        await registryClient.RegisterSchemaAsync(subject, v2);
        var executor = new SchemaRegistryRuleExecutor([new JsonataSchemaRegistryRuleHandler()]);
        await using var deserializer = new JsonSchemaRegistryDeserializer<System.Text.Json.JsonElement>(
            registryClient,
            SchemaRegistryRuleJsonContext.Default.JsonElement,
            new SchemaRegistryDeserializerConfig { UseLatestVersion = true },
            ruleExecutor: executor);
        var payload = """{"first":"Ada","last":"Lovelace"}"""u8;
        var wire = new byte[5 + payload.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(wire.AsSpan(1, 4), writerId);
        payload.CopyTo(wire.AsSpan(5));

        var result = deserializer.Deserialize(wire, CreateContext(topic));

        await Assert.That(result.GetProperty("fullName").GetString()).IsEqualTo("Ada Lovelace");
    }

    [Test]
    public async Task RegisteredMigrationRules_UseLatestVersion_ExecutesUpgradePath()
    {
        var topic = await testInfra.CreateTestTopicAsync();
        var subject = $"{topic}-value";
        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });
        var v1 = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = """{ "type": "string", "title": "MigrationV1" }"""
        };
        var v2 = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = """{ "type": "string", "title": "MigrationV2" }""",
            RuleSet = new SchemaRuleSet
            {
                MigrationRules =
                [
                    new SchemaRule
                    {
                        Name = "append-version",
                        Kind = SchemaRuleKind.Transform,
                        Mode = SchemaRuleMode.Upgrade,
                        Type = AppendMigrationRuleHandler.RuleType
                    }
                ]
            }
        };
        var writerId = await registryClient.RegisterSchemaAsync(subject, v1);
        await registryClient.RegisterSchemaAsync(subject, v2);
        var calls = new ConcurrentQueue<string>();
        var executor = new SchemaRegistryRuleExecutor([new AppendMigrationRuleHandler(calls)]);
        await using var deserializer = SchemaRegistryDeserializer.Create(
            registryClient,
            static (ReadOnlyMemory<byte> payload, Schema _) => Encoding.UTF8.GetString(payload.Span),
            new SchemaRegistryDeserializerConfig { UseLatestVersion = true },
            ruleExecutor: executor);
        var payload = "payload"u8;
        var wire = new byte[5 + payload.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(wire.AsSpan(1, 4), writerId);
        payload.CopyTo(wire.AsSpan(5));

        var result = deserializer.Deserialize(wire, CreateContext(topic));

        await Assert.That(result).IsEqualTo("payload|v2");
        await Assert.That(calls).IsEquivalentTo(["Upgrade:MigrationV1->MigrationV2"]);
    }

    [Test]
    public async Task RegisteredDomainRules_ProduceAndConsume_RoundTrip()
    {
        var topic = await testInfra.CreateTestTopicAsync();
        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });
        var calls = new ConcurrentQueue<string>();
        var ruleExecutor = new SchemaRegistryRuleExecutor(
        [
            new CelSchemaRegistryRuleHandler(),
            new XorRuleHandler("DOMAIN-XOR", 0x25, calls)
        ]);
        var schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = """{ "type": "string", "title": "DomainRulePayload" }""",
            RuleSet = new SchemaRuleSet
            {
                DomainRules =
                [
                    new SchemaRule
                    {
                        Name = "subject-condition",
                        Kind = SchemaRuleKind.Condition,
                        Mode = SchemaRuleMode.WriteRead,
                        Type = "CEL",
                        Expr = "subject == \"DomainRulePayload\""
                    },
                    CreateRule("domain-xor", "DOMAIN-XOR")
                ]
            }
        };

        await using var serializer = new SchemaRegistrySerializer<string>(
            registryClient,
            static (value, writer) =>
            {
                var byteCount = Encoding.UTF8.GetByteCount(value);
                Encoding.UTF8.GetBytes(value, writer.GetSpan(byteCount));
                writer.Advance(byteCount);
            },
            () => schema,
            subjectNameStrategy: SubjectNameStrategy.RecordName,
            ruleExecutor: ruleExecutor);
        await using var deserializer = SchemaRegistryDeserializer.Create(
            registryClient,
            static (ReadOnlyMemory<byte> payload, Schema _) => Encoding.UTF8.GetString(payload.Span),
            new SchemaRegistryDeserializerConfig
            {
                SubjectNameStrategy = SubjectNameStrategy.RecordName
            },
            ruleExecutor: ruleExecutor);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithValueSerializer(serializer)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "domain-rule-payload"
        });

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"schema-rule-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(deserializer)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        string? consumed = null;
        await foreach (var message in consumer.ConsumeAsync(cts.Token))
        {
            consumed = message.Value;
            break;
        }

        var registered = await registryClient.GetSchemaBySubjectAsync("DomainRulePayload");
        var registeredById = await registryClient.GetSchemaAsync(registered.Id);
        await Assert.That(consumed).IsEqualTo("domain-rule-payload");
        await Assert.That(registeredById.RuleSet!.DomainRules).Count().IsEqualTo(2);
        await Assert.That(calls).IsEquivalentTo([
            "Write:domain-xor",
            "Read:domain-xor"
        ]);
    }

    [Test]
    public async Task RegisteredWriteRules_RegistryResolutionModes_JsonAvroAndProtobufExecute()
    {
        var jsonTopic = await testInfra.CreateTestTopicAsync();
        var avroTopic = await testInfra.CreateTestTopicAsync();
        var protobufTopic = await testInfra.CreateTestTopicAsync();
        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });
        var calls = new ConcurrentQueue<string>();
        var ruleExecutor = new SchemaRegistryRuleExecutor(
        [
            new XorRuleHandler("DOMAIN-XOR", 0x25, calls)
        ]);

        const string jsonSchemaText = """{ "type": "string", "title": "JsonWriteRule" }""";
        await registryClient.RegisterSchemaAsync(
            $"{jsonTopic}-value",
            CreateSchema(SchemaType.Json, jsonSchemaText));
        await using var jsonSerializer = new JsonSchemaRegistrySerializer<string>(
            registryClient,
            jsonSchemaText,
            SchemaRegistryRuleJsonContext.Default.String,
            autoRegisterSchemas: false,
            ruleExecutor: ruleExecutor);

        var jsonOutput = new ArrayBufferWriter<byte>();
        jsonSerializer.Serialize("json-payload", ref jsonOutput, CreateContext(jsonTopic));

        const string avroSchemaText = """
            {
              "type": "record",
              "name": "AvroWriteRule",
              "namespace": "Dekaf.Tests.Integration",
              "fields": [{ "name": "value", "type": "string" }]
            }
            """;
        var avroSchema = (Avro.RecordSchema)AvroSchema.Parse(avroSchemaText);
        await registryClient.RegisterSchemaAsync(
            $"{avroTopic}-value",
            CreateSchema(SchemaType.Avro, avroSchema.ToString()));
        await using var avroSerializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            registryClient,
            new AvroSerializerConfig
            {
                AutoRegisterSchemas = false,
                RuleExecutor = ruleExecutor
            });
        var record = new GenericRecord(avroSchema);
        record.Add("value", "avro-payload");

        await using var avroProducer = await Kafka.CreateProducer<string, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithValueSerializer(avroSerializer)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        await avroProducer.ProduceAsync(new ProducerMessage<string, GenericRecord>
        {
            Topic = avroTopic,
            Key = "key",
            Value = record
        });

        await using var latestAvroSerializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            registryClient,
            new AvroSerializerConfig
            {
                UseLatestVersion = true,
                RuleExecutor = ruleExecutor
            });
        var latestAvroOutput = new ArrayBufferWriter<byte>();
        latestAvroSerializer.Serialize(record, ref latestAvroOutput, CreateContext(avroTopic));

        await registryClient.RegisterSchemaAsync(
            $"{protobufTopic}-value",
            CreateSchema(
                SchemaType.Protobuf,
                SchemaRuleMessage.Descriptor.File.SerializedData.ToBase64()));
        await using var protobufSerializer = new ProtobufSchemaRegistrySerializer<SchemaRuleMessage>(
            registryClient,
            new ProtobufSerializerConfig
            {
                RuleExecutor = ruleExecutor
            });
        var protobufOutput = new ArrayBufferWriter<byte>();
        protobufSerializer.Serialize(
            new SchemaRuleMessage { Value = "protobuf-payload" },
            ref protobufOutput,
            CreateContext(protobufTopic));

        await Assert.That(calls).IsEquivalentTo([
            "Write:domain-xor",
            "Write:domain-xor",
            "Write:domain-xor",
            "Write:domain-xor"
        ]);
    }

    [Test]
    public async Task RegisteredJsonInlineRules_ProduceConsumeAndRejectInvalidPayload()
    {
        const string schemaText = """
            {
              "type": "object",
              "properties": {
                "name": {
                  "type": "string",
                  "confluent:rules": [{ "name": "nameRequired", "expr": "size(this) > 0" }]
                }
              }
            }
            """;
        var topic = await testInfra.CreateTestTopicAsync();
        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });
        await registryClient.RegisterSchemaAsync($"{topic}-value", new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = schemaText
        });
        var validationOptions = new JsonSchemaValidationOptions
        {
            ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registryClient),
            Mode = JsonSchemaValidationMode.None,
            ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
        };
        await using var serializer = new JsonSchemaRegistrySerializer<InlineValidationPayload>(
            registryClient,
            schemaText,
            jsonOptions: null,
            validationOptions,
            autoRegisterSchemas: false);
        await using var deserializer = new JsonSchemaRegistryDeserializer<InlineValidationPayload>(
            registryClient,
            jsonOptions: null,
            validationOptions);
        var context = CreateContext(topic);
        var invalid = new ArrayBufferWriter<byte>();
        Assert.Throws<ValidationRulesFailedException>(
            () => serializer.Serialize(new InlineValidationPayload(string.Empty), ref invalid, context));

        await using var producer = await Kafka.CreateProducer<string, InlineValidationPayload>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithValueSerializer(serializer)
            .BuildAsync();
        await producer.ProduceAsync(new ProducerMessage<string, InlineValidationPayload>
        {
            Topic = topic,
            Key = "key",
            Value = new InlineValidationPayload("valid")
        });

        await using var consumer = await Kafka.CreateConsumer<string, InlineValidationPayload>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"inline-validation-{Guid.NewGuid():N}")
            .WithValueDeserializer(deserializer)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();
        consumer.Subscribe(topic);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        InlineValidationPayload? consumed = null;
        await foreach (var message in consumer.ConsumeAsync(timeout.Token))
        {
            consumed = message.Value;
            break;
        }

        await Assert.That(consumed).IsEqualTo(new InlineValidationPayload("valid"));
    }

    private static Schema CreateSchema(SchemaType schemaType, string schemaString) =>
        new()
        {
            SchemaType = schemaType,
            SchemaString = schemaString,
            RuleSet = new SchemaRuleSet
            {
                DomainRules = [CreateRule("domain-xor", "DOMAIN-XOR")]
            }
        };

    private sealed record InlineValidationPayload(string Name);

    private static SerializationContext CreateContext(string topic) =>
        new()
        {
            Topic = topic,
            Component = SerializationComponent.Value
        };

    private static SchemaRule CreateRule(string name, string type) =>
        new()
        {
            Name = name,
            Kind = SchemaRuleKind.Transform,
            Mode = SchemaRuleMode.WriteRead,
            Type = type
        };

    private sealed class XorRuleHandler(
        string type,
        byte mask,
        ConcurrentQueue<string> calls) : ISchemaRegistryRuleHandler
    {
        public string Type => type;

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => Transform(payload, context);

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => Transform(payload, context);

        private ReadOnlyMemory<byte> Transform(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context)
        {
            calls.Enqueue($"{context.Direction}:{context.Rule.Name}");
            var result = payload.ToArray();
            for (var i = 0; i < result.Length; i++)
                result[i] ^= mask;

            return result;
        }
    }

    private sealed class AppendMigrationRuleHandler(ConcurrentQueue<string> calls)
        : ISchemaRegistryRuleHandler
    {
        internal const string RuleType = "APPEND-MIGRATION";

        public string Type => RuleType;

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => Transform(payload, context);

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => Transform(payload, context);

        private ReadOnlyMemory<byte> Transform(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context)
        {
            calls.Enqueue(
                $"{context.PayloadContext.RuleMode}:{GetTitle(context.PayloadContext.SourceSchema!)}->{GetTitle(context.PayloadContext.TargetSchema!)}");
            var suffix = "|v2"u8;
            var result = new byte[payload.Length + suffix.Length];
            payload.Span.CopyTo(result);
            suffix.CopyTo(result.AsSpan(payload.Length));
            return result;
        }

        private static string GetTitle(Schema schema)
        {
            using var document = System.Text.Json.JsonDocument.Parse(schema.SchemaString);
            return document.RootElement.GetProperty("title").GetString()!;
        }
    }

    private sealed class CsfleTestRegistryClient(SchemaRegistryClient inner) : ISchemaRegistryClient
    {
        private readonly ConcurrentDictionary<string, Kek> _keks = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<(string Kek, string Subject, DekAlgorithm Algorithm), Dek> _deks = new();

        public int LatestCacheTtlSecs => inner.LatestCacheTtlSecs;

        public Task<int> RegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) =>
            inner.RegisterSchemaAsync(subject, schema, cancellationToken);

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default) =>
            inner.GetSchemaAsync(id, cancellationToken);

        public Task<Schema> GetSchemaAsync(
            int id,
            string subject,
            CancellationToken cancellationToken = default) =>
            inner.GetSchemaAsync(id, subject, cancellationToken);

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default) =>
            inner.GetSchemaBySubjectAsync(subject, version, cancellationToken);

        public Task<RegisteredSchema> LookupSchemaAsync(
            string subject,
            Schema schema,
            bool ignoreDeletedSchemas = true,
            bool normalize = false,
            CancellationToken cancellationToken = default) =>
            inner.LookupSchemaAsync(subject, schema, ignoreDeletedSchemas, normalize, cancellationToken);

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) =>
            inner.GetOrRegisterSchemaAsync(subject, schema, cancellationToken);

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default) =>
            inner.GetAllSubjectsAsync(cancellationToken);

        public Task<IReadOnlyList<int>> GetVersionsAsync(
            string subject,
            CancellationToken cancellationToken = default) =>
            inner.GetVersionsAsync(subject, cancellationToken);

        public Task<bool> IsCompatibleAsync(
            string subject,
            Schema schema,
            string version = "latest",
            CancellationToken cancellationToken = default) =>
            inner.IsCompatibleAsync(subject, schema, version, cancellationToken);

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(
            string subject,
            bool permanent = false,
            CancellationToken cancellationToken = default) =>
            inner.DeleteSubjectAsync(subject, permanent, cancellationToken);

        public Task<Kek> RegisterKekAsync(
            RegisterKekRequest request,
            bool testSharing = false,
            CancellationToken cancellationToken = default)
        {
            var kek = new Kek
            {
                Name = request.Name,
                KmsType = request.KmsType,
                KmsKeyId = request.KmsKeyId,
                KmsProps = request.KmsProps
            };
            _keks[kek.Name] = kek;
            return Task.FromResult(kek);
        }

        public Task<Kek> GetKekAsync(
            string name,
            bool deleted = false,
            CancellationToken cancellationToken = default) =>
            _keks.TryGetValue(name, out var kek)
                ? Task.FromResult(kek)
                : Task.FromException<Kek>(new SchemaRegistryException(40470, $"KEK '{name}' not found"));

        public Task<Dek> RegisterDekAsync(
            string kekName,
            RegisterDekRequest request,
            CancellationToken cancellationToken = default)
        {
            var algorithm = request.Algorithm ?? DekAlgorithm.Aes256Gcm;
            var dek = new Dek
            {
                KekName = kekName,
                Subject = request.Subject,
                Version = request.Version ?? 1,
                Algorithm = algorithm,
                EncryptedKeyMaterial = request.EncryptedKeyMaterial,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            _deks[(kekName, request.Subject, algorithm)] = dek;
            return Task.FromResult(dek);
        }

        public Task<Dek> GetDekAsync(
            string kekName,
            string subject,
            DekAlgorithm? algorithm = null,
            bool deleted = false,
            CancellationToken cancellationToken = default)
        {
            var resolvedAlgorithm = algorithm ?? DekAlgorithm.Aes256Gcm;
            return _deks.TryGetValue((kekName, subject, resolvedAlgorithm), out var dek)
                ? Task.FromResult(dek)
                : Task.FromException<Dek>(new SchemaRegistryException(40471, $"DEK for '{subject}' not found"));
        }

        public Task<Dek> GetDekAsync(
            string kekName,
            string subject,
            int version,
            bool deleted = false,
            CancellationToken cancellationToken = default) =>
            GetDekAsync(kekName, subject, DekAlgorithm.Aes256Gcm, deleted, cancellationToken);

        public Task<Dek> GetDekAsync(
            string kekName,
            string subject,
            int version,
            DekAlgorithm algorithm,
            bool deleted = false,
            CancellationToken cancellationToken = default) =>
            GetDekAsync(kekName, subject, algorithm, deleted, cancellationToken);

        public void Dispose() => inner.Dispose();
    }
}

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(System.Text.Json.JsonElement))]
internal sealed partial class SchemaRegistryRuleJsonContext : JsonSerializerContext;
