using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Threading;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaRegistryCsfleRuleTests
{
    private static readonly byte[] KekMaterial =
    [
        0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27,
        0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F,
        0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37,
        0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F
    ];

    [Test]
    public async Task TransformSerializedPayload_AesGcm_EncryptsWithRegisteredDekAndDecrypts()
    {
        var client = CreateDekClient();
        var handler = CreateHandler(client);
        var context = CreateHandlerContext(CreateRule());
        var payload = "plain payload"u8.ToArray();

        var encrypted1 = handler.TransformSerializedPayload(payload, context);
        var encrypted2 = handler.TransformSerializedPayload(payload, context);
        var decrypted = handler.TransformDeserializedPayload(encrypted1, context);

        await Assert.That(encrypted1.ToArray()).IsNotEquivalentTo(payload);
        await Assert.That(encrypted1.ToArray()).IsNotEquivalentTo(encrypted2.ToArray());
        await Assert.That(encrypted1.Span.StartsWith("DKFLE1"u8)).IsFalse();
        await Assert.That(decrypted.ToArray()).IsEquivalentTo(payload);
        await Assert.That(client.RegisterDekCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task TransformSerializedPayload_TaggedJsonField_EncryptsOnlyTaggedString()
    {
        var client = CreateDekClient();
        var handler = CreateHandler(client);
        var rule = CreateRule(tags: new HashSet<string>(StringComparer.Ordinal) { "PII" });
        var schema = CreateTaggedSchema(rule);
        var context = CreateHandlerContext(rule, schema);
        var payload = """{"name":"Ada","ssn":"123-45-6789"}"""u8.ToArray();

        var encrypted = handler.TransformSerializedPayload(payload, context);
        var decrypted = handler.TransformDeserializedPayload(encrypted, context);

        using var encryptedJson = JsonDocument.Parse(encrypted);
        var root = encryptedJson.RootElement;
        var encryptedSsn = root.GetProperty("ssn").GetString();
        await Assert.That(root.GetProperty("name").GetString()).IsEqualTo("Ada");
        await Assert.That(encryptedSsn).DoesNotStartWith("__dekaf_csfle:");
        await Assert.That(encryptedSsn).DoesNotContain("123-45-6789");
        await Assert.That(Convert.FromBase64String(encryptedSsn!).Length).IsGreaterThan(0);
        await Assert.That(Encoding.UTF8.GetString(decrypted.Span)).IsEqualTo(Encoding.UTF8.GetString(payload));
    }

    [Test]
    public async Task TransformSerializedPayload_TaggedJsonField_PreservesNull()
    {
        var client = CreateDekClient();
        var handler = CreateHandler(client);
        var rule = CreateRule(tags: new HashSet<string>(StringComparer.Ordinal) { "PII" });
        var schema = CreateTaggedSchema(rule);
        var context = CreateHandlerContext(rule, schema);
        var payload = """{"name":"Ada","ssn":null}"""u8.ToArray();

        var encrypted = handler.TransformSerializedPayload(payload, context);
        var decrypted = handler.TransformDeserializedPayload(encrypted, context);

        using var encryptedJson = JsonDocument.Parse(encrypted);
        await Assert.That(encryptedJson.RootElement.GetProperty("ssn").ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(Encoding.UTF8.GetString(decrypted.Span)).IsEqualTo(Encoding.UTF8.GetString(payload));
    }

    [Test]
    public async Task TransformSerializedPayload_AesSiv_IsDeterministicAndDecrypts()
    {
        var client = CreateDekClient();
        var handler = CreateHandler(client);
        var context = CreateHandlerContext(
            CreateRule(parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["encrypt.kek.name"] = "payments-kek",
                ["encrypt.dek.algorithm"] = "AES256_SIV"
            }));
        var payload = "deterministic payload"u8.ToArray();

        var encrypted1 = handler.TransformSerializedPayload(payload, context);
        var encrypted2 = handler.TransformSerializedPayload(payload, context);
        var decrypted = handler.TransformDeserializedPayload(encrypted1, context);

        await Assert.That(encrypted1.ToArray()).IsEquivalentTo(encrypted2.ToArray());
        await Assert.That(decrypted.ToArray()).IsEquivalentTo(payload);
    }

    [Test]
    public async Task TransformSerializedPayload_DekExpiry_AddsConfluentVersionPrefix()
    {
        var client = CreateDekClient();
        var handler = CreateHandler(client);
        var context = CreateHandlerContext(
            CreateRule(parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["encrypt.kek.name"] = "payments-kek",
                ["encrypt.dek.expiry.days"] = "1"
            }));
        var payload = "versioned payload"u8.ToArray();

        var encrypted = handler.TransformSerializedPayload(payload, context);
        var decrypted = handler.TransformDeserializedPayload(encrypted, context);

        await Assert.That(encrypted.Span[0]).IsEqualTo((byte)0);
        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(encrypted.Span[1..5])).IsEqualTo(1);
        await Assert.That(decrypted.ToArray()).IsEquivalentTo(payload);
    }

    [Test]
    public async Task TransformSerializedPayload_ConcurrentFirstUse_RegistersDekOnce()
    {
        var client = CreateDekClient();
        client.RegisterDekDelay = TimeSpan.FromMilliseconds(50);
        var handler = CreateHandler(client);
        var context = CreateHandlerContext(CreateRule());

        var tasks = Enumerable.Range(0, 16)
            .Select(i => Task.Run(() => handler.TransformSerializedPayload(Encoding.UTF8.GetBytes("payload-" + i), context)))
            .ToArray();

        await Task.WhenAll(tasks);

        await Assert.That(client.RegisterDekCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task TransformDeserializedPayload_ConcurrentFirstRead_GetsDekOnce()
    {
        var client = CreateDekClient();
        var handler = CreateHandler(client);
        var context = CreateHandlerContext(CreateRule());
        var payload = "payload"u8.ToArray();
        var encrypted = handler.TransformSerializedPayload(payload, context);
        client.ResetGetDekCallCount();
        client.GetDekDelay = TimeSpan.FromMilliseconds(50);

        var tasks = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => handler.TransformDeserializedPayload(encrypted, context)))
            .ToArray();

        await Task.WhenAll(tasks);

        foreach (var task in tasks)
            await Assert.That(task.Result.ToArray()).IsEquivalentTo(payload);

        await Assert.That(client.GetDekCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task TransformSerializedPayload_ConfluentKekAndDekNotFoundCodes_AutoRegisters()
    {
        var client = new FakeDekRegistryClient();
        var handler = CreateHandler(client);
        var context = CreateHandlerContext(
            CreateRule(parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["encrypt.kek.name"] = "payments-kek",
                ["encrypt.kms.type"] = LocalKmsProvider.DefaultType,
                ["encrypt.kms.key.id"] = "local://payments"
            }));
        var payload = "payload"u8.ToArray();

        var encrypted = handler.TransformSerializedPayload(payload, context);
        var decrypted = handler.TransformDeserializedPayload(encrypted, context);

        await Assert.That(decrypted.ToArray()).IsEquivalentTo(payload);
        await Assert.That(client.RegisterDekCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task SchemaRegistrySerializerDeserializer_AppliesCsfleRuleHooks()
    {
        var client = CreateDekClient();
        var rule = CreateRule();
        var schema = CreateRuleSchema(rule);
        var executor = new SchemaRegistryRuleExecutor([CreateHandler(client)]);
        var serializer = new SchemaRegistrySerializer<string>(
            client,
            WriteUtf8,
            _ => schema,
            ruleExecutor: executor);
        var deserializer = SchemaRegistryDeserializer.Create<string>(
            client,
            static (payload, _) => Encoding.UTF8.GetString(payload.Span),
            ruleExecutor: executor);
        var context = new SerializationContext
        {
            Topic = "orders",
            Component = SerializationComponent.Value
        };
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize("payload", ref buffer, context);
        var roundTripped = deserializer.Deserialize(buffer.WrittenMemory, context);

        await Assert.That(roundTripped).IsEqualTo("payload");
        await Assert.That(buffer.WrittenMemory.Length).IsGreaterThan(5);
        await Assert.That(Encoding.UTF8.GetString(buffer.WrittenSpan[5..])).DoesNotContain("payload");
    }

    [Test]
    public async Task TransformSerializedPayload_MissingKekName_ThrowsRuleException()
    {
        var handler = CreateHandler(CreateDekClient());
        var context = CreateHandlerContext(
            CreateRule(parameters: new Dictionary<string, string>(StringComparer.Ordinal)));

        await Assert.That(() => handler.TransformSerializedPayload("payload"u8.ToArray(), context))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("encrypt.kek.name");
    }

    [Test]
    public async Task TransformSerializedPayload_UnknownProvider_DoesNotLeakPlaintext()
    {
        var client = new FakeDekRegistryClient();
        client.AddKek(new Kek
        {
            Name = "payments-kek",
            KmsType = "missing-kms",
            KmsKeyId = "local://payments"
        });
        var executor = new SchemaRegistryRuleExecutor(
        [
            new SchemaRegistryCsfleRuleHandler(client, [])
        ]);
        var schema = CreateRuleSchema(CreateRule());

        var exception = await Assert.ThrowsAsync<SchemaRegistryRuleException>(
            () => Task.Run(() => executor.TransformSerializedPayload(
                "super-secret-payload"u8.ToArray(),
                CreateRuleContext(schema))));

        await Assert.That(exception!.Message).Contains("failed");
        await Assert.That(exception.Message).DoesNotContain("super-secret-payload");
    }

    [Test]
    public async Task TransformDeserializedPayload_InvalidDekMaterial_ThrowsRuleException()
    {
        var goodClient = CreateDekClient();
        var handler = CreateHandler(goodClient);
        var rule = CreateRule();
        var context = CreateHandlerContext(rule);
        var encrypted = handler.TransformSerializedPayload("payload"u8.ToArray(), context);

        var badClient = CreateDekClient();
        badClient.AddDek(new Dek
        {
            KekName = "payments-kek",
            Subject = "orders-value",
            Version = 1,
            Algorithm = DekAlgorithm.Aes256Gcm,
            KeyMaterial = Convert.ToBase64String([1, 2, 3])
        });
        var badHandler = CreateHandler(badClient);

        var exception = await Assert.ThrowsAsync<SchemaRegistryRuleException>(
            () => Task.Run(() => badHandler.TransformDeserializedPayload(encrypted, context)));

        await Assert.That(exception!.Message).Contains("invalid DEK material length");
        await Assert.That(exception.Message).DoesNotContain(Convert.ToBase64String([1, 2, 3]));
    }

    [Test]
    public async Task TransformSerializedPayload_UnsupportedMode_IsSkippedByExecutor()
    {
        var client = new FakeDekRegistryClient();
        var executor = new SchemaRegistryRuleExecutor(
        [
            new SchemaRegistryCsfleRuleHandler(client, [])
        ]);
        var schema = CreateRuleSchema(CreateRule(mode: SchemaRuleMode.Upgrade));
        var payload = "payload"u8.ToArray();

        var result = executor.TransformSerializedPayload(payload, CreateRuleContext(schema));

        await Assert.That(result.ToArray()).IsEquivalentTo(payload);
        await Assert.That(client.GetKekCallCount).IsEqualTo(0);
    }

    private static SchemaRegistryCsfleRuleHandler CreateHandler(FakeDekRegistryClient client) =>
        new(client, [new LocalKmsProvider(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["local://payments"] = KekMaterial
        })]);

    private static FakeDekRegistryClient CreateDekClient()
    {
        var client = new FakeDekRegistryClient();
        client.AddKek(new Kek
        {
            Name = "payments-kek",
            KmsType = LocalKmsProvider.DefaultType,
            KmsKeyId = "local://payments"
        });
        return client;
    }

    private static SchemaRegistryRuleHandlerContext CreateHandlerContext(
        SchemaRule rule,
        Schema? schema = null) =>
        new()
        {
            PayloadContext = CreateRuleContext(schema ?? CreateRuleSchema(rule)),
            Rule = rule,
            Direction = SchemaRegistryRuleDirection.Write
        };

    private static SchemaRegistryRuleContext CreateRuleContext(Schema? schema) =>
        new()
        {
            Topic = "orders",
            Component = SerializationComponent.Value,
            SchemaId = 12,
            Subject = "orders-value",
            Schema = schema,
            PayloadFormat = schema?.SchemaType == SchemaType.Json
                ? SchemaRegistryPayloadFormat.Json
                : SchemaRegistryPayloadFormat.Custom
        };

    private static Schema CreateTaggedSchema(SchemaRule rule) =>
        new()
        {
            SchemaType = SchemaType.Json,
            SchemaString = """{"type":"object"}""",
            Metadata = new SchemaMetadata
            {
                Tags = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
                {
                    ["$.ssn"] = new HashSet<string>(StringComparer.Ordinal) { "PII" }
                }
            },
            RuleSet = new SchemaRuleSet
            {
                EncodingRules = [rule]
            }
        };

    private static Schema CreateRuleSchema(SchemaRule rule) =>
        new()
        {
            SchemaType = SchemaType.Avro,
            SchemaString = "{}",
            RuleSet = new SchemaRuleSet
            {
                EncodingRules = [rule]
            }
        };

    private static SchemaRule CreateRule(
        SchemaRuleMode mode = SchemaRuleMode.WriteRead,
        IReadOnlySet<string>? tags = null,
        IReadOnlyDictionary<string, string>? parameters = null) =>
        new()
        {
            Name = "encryptPii",
            Kind = SchemaRuleKind.Transform,
            Mode = mode,
            Type = SchemaRegistryCsfleRuleHandler.EncryptRuleType,
            Tags = tags,
            Parameters = parameters ?? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["encrypt.kek.name"] = "payments-kek"
            }
        };

    private static void WriteUtf8(string value, IBufferWriter<byte> writer)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        bytes.CopyTo(writer.GetSpan(bytes.Length));
        writer.Advance(bytes.Length);
    }

    private sealed class FakeDekRegistryClient : ISchemaRegistryClient
    {
        private readonly object _gate = new();
        private readonly Dictionary<int, Schema> _schemasById = new();
        private readonly Dictionary<string, List<(int Version, int Id, Schema Schema)>> _schemasBySubject = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Kek> _keks = new(StringComparer.Ordinal);
        private readonly Dictionary<(string KekName, string Subject, int Version), Dek> _deksByVersion = new();
        private readonly Dictionary<(string KekName, string Subject, DekAlgorithm Algorithm), Dek> _latestDeks = new();
        private int _nextSchemaId = 1;
        private int _nextDekVersion = 1;
        private int _getKekCallCount;
        private int _getDekCallCount;
        private int _registerDekCallCount;

        public TimeSpan GetDekDelay { get; set; }

        public TimeSpan RegisterDekDelay { get; set; }

        public int GetKekCallCount
        {
            get
            {
                lock (_gate)
                {
                    return _getKekCallCount;
                }
            }
        }

        public int RegisterDekCallCount
        {
            get
            {
                lock (_gate)
                {
                    return _registerDekCallCount;
                }
            }
        }

        public int GetDekCallCount
        {
            get
            {
                lock (_gate)
                {
                    return _getDekCallCount;
                }
            }
        }

        public void ResetGetDekCallCount()
        {
            lock (_gate)
            {
                _getDekCallCount = 0;
            }
        }

        public void AddKek(Kek kek)
        {
            lock (_gate)
            {
                _keks[kek.Name] = kek;
            }
        }

        public void AddDek(Dek dek)
        {
            lock (_gate)
            {
                AddDekCore(dek);
            }
        }

        private void AddDekCore(Dek dek)
        {
            _deksByVersion[(dek.KekName, dek.Subject, dek.Version)] = dek;
            _latestDeks[(dek.KekName, dek.Subject, dek.Algorithm)] = dek;
            _nextDekVersion = Math.Max(_nextDekVersion, dek.Version + 1);
        }

        public Task<Kek> GetKekAsync(
            string name,
            bool deleted = false,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _getKekCallCount++;
                if (_keks.TryGetValue(name, out var kek))
                    return Task.FromResult(kek);
            }

            throw new SchemaRegistryException(40470, $"KEK '{name}' not found");
        }

        public Task<Kek> RegisterKekAsync(
            RegisterKekRequest request,
            bool testSharing = false,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
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
        }

        public Task<Dek> GetDekAsync(
            string kekName,
            string subject,
            DekAlgorithm? algorithm = null,
            bool deleted = false,
            CancellationToken cancellationToken = default)
        {
            if (GetDekDelay > TimeSpan.Zero)
                Thread.Sleep(GetDekDelay);

            lock (_gate)
            {
                _getDekCallCount++;
                var resolvedAlgorithm = algorithm ?? DekAlgorithm.Aes256Gcm;
                if (_latestDeks.TryGetValue((kekName, subject, resolvedAlgorithm), out var dek))
                    return Task.FromResult(dek);
            }

            throw new SchemaRegistryException(40471, $"DEK for subject '{subject}' not found");
        }

        public Task<Dek> GetDekAsync(
            string kekName,
            string subject,
            int version,
            bool deleted = false,
            CancellationToken cancellationToken = default)
        {
            if (GetDekDelay > TimeSpan.Zero)
                Thread.Sleep(GetDekDelay);

            lock (_gate)
            {
                _getDekCallCount++;
                if (_deksByVersion.TryGetValue((kekName, subject, version), out var dek))
                    return Task.FromResult(dek);
            }

            throw new SchemaRegistryException(40471, $"DEK version '{version}' not found");
        }

        public Task<Dek> RegisterDekAsync(
            string kekName,
            RegisterDekRequest request,
            CancellationToken cancellationToken = default)
        {
            if (RegisterDekDelay > TimeSpan.Zero)
                Thread.Sleep(RegisterDekDelay);

            lock (_gate)
            {
                _registerDekCallCount++;
                var dek = new Dek
                {
                    KekName = kekName,
                    Subject = request.Subject,
                    Version = request.Version ?? _nextDekVersion++,
                    Algorithm = request.Algorithm ?? DekAlgorithm.Aes256Gcm,
                    EncryptedKeyMaterial = request.EncryptedKeyMaterial,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                AddDekCore(dek);
                return Task.FromResult(dek);
            }
        }

        public Task<int> RegisterSchemaAsync(string subject, Schema schema, CancellationToken cancellationToken = default)
        {
            var id = _nextSchemaId++;
            _schemasById[id] = schema;
            if (!_schemasBySubject.TryGetValue(subject, out var versions))
            {
                versions = [];
                _schemasBySubject[subject] = versions;
            }

            versions.Add((versions.Count + 1, id, schema));
            return Task.FromResult(id);
        }

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default)
        {
            if (_schemasById.TryGetValue(id, out var schema))
                return Task.FromResult(schema);

            throw new SchemaRegistryException(40403, $"Schema '{id}' not found");
        }

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default)
        {
            if (!_schemasBySubject.TryGetValue(subject, out var versions) || versions.Count == 0)
                throw new SchemaRegistryException(40401, $"Subject '{subject}' not found");

            var entry = version == "latest"
                ? versions[^1]
                : versions.First(item => item.Version == int.Parse(version, System.Globalization.CultureInfo.InvariantCulture));
            return Task.FromResult(new RegisteredSchema
            {
                Id = entry.Id,
                Subject = subject,
                Version = entry.Version,
                Schema = entry.Schema
            });
        }

        public Task<int> GetOrRegisterSchemaAsync(string subject, Schema schema, CancellationToken cancellationToken = default)
        {
            if (_schemasBySubject.TryGetValue(subject, out var versions))
            {
                var existing = versions.FirstOrDefault(item => item.Schema.SchemaString == schema.SchemaString);
                if (existing != default)
                    return Task.FromResult(existing.Id);
            }

            return RegisterSchemaAsync(subject, schema, cancellationToken);
        }

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(_schemasBySubject.Keys.ToList());

        public Task<IReadOnlyList<int>> GetVersionsAsync(string subject, CancellationToken cancellationToken = default)
        {
            if (!_schemasBySubject.TryGetValue(subject, out var versions))
                throw new SchemaRegistryException(40401, $"Subject '{subject}' not found");

            return Task.FromResult<IReadOnlyList<int>>(versions.Select(static item => item.Version).ToList());
        }

        public Task<bool> IsCompatibleAsync(
            string subject,
            Schema schema,
            string version = "latest",
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(
            string subject,
            bool permanent = false,
            CancellationToken cancellationToken = default)
        {
            if (!_schemasBySubject.Remove(subject, out var versions))
                throw new SchemaRegistryException(40401, $"Subject '{subject}' not found");

            foreach (var (_, id, _) in versions)
                _schemasById.Remove(id);

            return Task.FromResult<IReadOnlyList<int>>(versions.Select(static item => item.Version).ToList());
        }

        public void Dispose()
        {
        }
    }
}
