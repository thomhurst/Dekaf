using System.Buffers;
using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    public async Task TransformSerializedPayload_AesGcm_IsCompatibleWithPlatformImplementation()
    {
        var key = Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray();
        var client = CreateDekClient();
        client.AddDek(new Dek
        {
            KekName = "payments-kek",
            Subject = "orders-value",
            Version = 1,
            Algorithm = DekAlgorithm.Aes256Gcm,
            KeyMaterial = Convert.ToBase64String(key)
        });
        var handler = CreateHandler(client);
        var context = CreateHandlerContext(CreateRule());
        var payload = "platform-compatible payload"u8.ToArray();

        var encrypted = handler.TransformSerializedPayload(payload, context).ToArray();
        var platformPlaintext = new byte[payload.Length];
        using (var aes = new AesGcm(key, 16))
        {
            aes.Decrypt(
                encrypted.AsSpan(0, 12),
                encrypted.AsSpan(12, payload.Length),
                encrypted.AsSpan(12 + payload.Length, 16),
                platformPlaintext);
        }

        var platformCiphertext = new byte[12 + payload.Length + 16];
        RandomNumberGenerator.Fill(platformCiphertext.AsSpan(0, 12));
        using (var aes = new AesGcm(key, 16))
        {
            aes.Encrypt(
                platformCiphertext.AsSpan(0, 12),
                payload,
                platformCiphertext.AsSpan(12, payload.Length),
                platformCiphertext.AsSpan(12 + payload.Length, 16));
        }

        var handlerPlaintext = handler.TransformDeserializedPayload(platformCiphertext, context);

        await Assert.That(platformPlaintext).IsEquivalentTo(payload);
        await Assert.That(handlerPlaintext.ToArray()).IsEquivalentTo(payload);
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
    public async Task TransformSerializedPayload_CallerOwnedTaggedRule_ObservesTagMutations()
    {
        var ruleTags = new HashSet<string>(StringComparer.Ordinal) { "PII" };
        var metadataTags = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["$.ssn"] = new HashSet<string>(StringComparer.Ordinal) { "PII" }
        };
        var rule = CreateRule(tags: ruleTags);
        var schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            Metadata = new SchemaMetadata { Tags = metadataTags },
            RuleSet = new SchemaRuleSet { DomainRules = [rule] }
        };
        var handler = CreateHandler(CreateDekClient());
        var context = CreateHandlerContext(rule, schema);
        var payload = """{"name":"Ada","ssn":"123-45-6789"}"""u8.ToArray();

        _ = handler.TransformSerializedPayload(payload, context);
        metadataTags["$.ssn"] = new HashSet<string>(StringComparer.Ordinal) { "PUBLIC" };

        await Assert.That(() => handler.TransformSerializedPayload(payload, context))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("did not match");

        ruleTags.Clear();
        ruleTags.Add("PUBLIC");
        metadataTags.Remove("$.ssn");
        metadataTags["$.name"] = new HashSet<string>(StringComparer.Ordinal) { "PUBLIC" };

        var encrypted = handler.TransformSerializedPayload(payload, context).ToArray();
        using var document = JsonDocument.Parse(encrypted);
        await Assert.That(document.RootElement.GetProperty("name").GetString()).IsNotEqualTo("Ada");
        await Assert.That(document.RootElement.GetProperty("ssn").GetString()).IsEqualTo("123-45-6789");
    }

    [Test]
    public async Task TransformSerializedPayload_CallerOwnedSortedTags_ObservesTagMutations()
    {
        var ruleTags = new SortedSet<string>(StringComparer.Ordinal) { "PII" };
        var fieldTags = new SortedSet<string>(StringComparer.Ordinal) { "PII" };
        var rule = CreateRule(tags: ruleTags);
        var schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            Metadata = new SchemaMetadata
            {
                Tags = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
                {
                    ["$.ssn"] = fieldTags
                }
            },
            RuleSet = new SchemaRuleSet { DomainRules = [rule] }
        };
        var handler = CreateHandler(CreateDekClient());
        var context = CreateHandlerContext(rule, schema);
        var payload = """{"ssn":"123-45-6789"}"""u8.ToArray();

        _ = handler.TransformSerializedPayload(payload, context);
        fieldTags.Clear();
        fieldTags.Add("PUBLIC");

        await Assert.That(() => handler.TransformSerializedPayload(payload, context))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("did not match");

        ruleTags.Clear();
        ruleTags.Add("PUBLIC");
        var encrypted = handler.TransformSerializedPayload(payload, context);

        using var document = JsonDocument.Parse(encrypted);
        await Assert.That(document.RootElement.GetProperty("ssn").GetString()).IsNotEqualTo("123-45-6789");
    }

    [Test]
    public async Task TransformSerializedPayload_CallerOwnedSortedMetadata_ObservesSameCountReplacement()
    {
        var ruleTags = new HashSet<string>(StringComparer.Ordinal) { "PII" };
        var metadataTags = new SortedDictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["$.ssn"] = new HashSet<string>(StringComparer.Ordinal) { "PII" }
        };
        var rule = CreateRule(tags: ruleTags);
        var schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            Metadata = new SchemaMetadata { Tags = metadataTags },
            RuleSet = new SchemaRuleSet { DomainRules = [rule] }
        };
        var handler = CreateHandler(CreateDekClient());
        var context = CreateHandlerContext(rule, schema);
        var payload = """{"name":"Ada","ssn":"123-45-6789"}"""u8.ToArray();

        _ = handler.TransformSerializedPayload(payload, context);
        metadataTags.Remove("$.ssn");
        metadataTags["$.name"] = new HashSet<string>(StringComparer.Ordinal) { "PII" };

        var encrypted = handler.TransformSerializedPayload(payload, context);

        using var document = JsonDocument.Parse(encrypted);
        await Assert.That(document.RootElement.GetProperty("name").GetString()).IsNotEqualTo("Ada");
        await Assert.That(document.RootElement.GetProperty("ssn").GetString()).IsEqualTo("123-45-6789");
    }

    [Test]
    public async Task TransformSerializedPayload_UntrackableCallerOwnedTags_FailsClosed()
    {
        var rule = CreateRule(tags: new UntrackableTagSet("PII"));
        var handler = CreateHandler(CreateDekClient());
        var context = CreateHandlerContext(rule, CreateTaggedSchema(rule));

        await Assert.That(() => handler.TransformSerializedPayload("""{"ssn":"secret"}"""u8.ToArray(), context))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("cannot be tracked for mutation");
    }

    [Test]
    public async Task TransformSerializedPayload_UntrackableMetadataMap_FailsClosed()
    {
        var rule = CreateRule(tags: new HashSet<string>(StringComparer.Ordinal) { "PII" });
        var schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            Metadata = new SchemaMetadata
            {
                Tags = new SortedList<string, IReadOnlySet<string>>(StringComparer.Ordinal)
                {
                    ["$.ssn"] = new HashSet<string>(StringComparer.Ordinal) { "PII" }
                }
            },
            RuleSet = new SchemaRuleSet { DomainRules = [rule] }
        };
        var handler = CreateHandler(CreateDekClient());
        var context = CreateHandlerContext(rule, schema);

        await Assert.That(() => handler.TransformSerializedPayload("""{"ssn":"secret"}"""u8.ToArray(), context))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("cannot be tracked for mutation");
    }

    [Test]
    public async Task TransformSerializedPayload_OversizedWorkspaceBuffers_AreNotRetained()
    {
        const int maxRetainedBufferSize = 1024 * 1024;
        var rule = CreateRule(tags: new HashSet<string>(StringComparer.Ordinal) { "PII" });
        var handler = CreateHandler(CreateDekClient());
        var context = CreateHandlerContext(rule, CreateTaggedSchema(rule));
        var value = new string('x', maxRetainedBufferSize + 1);
        var payload = Encoding.UTF8.GetBytes($"{{\"ssn\":\"{value}\"}}");

        var encrypted = handler.TransformSerializedPayload(payload, context);
        var (outputs, temporaries) = GetWorkspaceBuffers(handler);

        await Assert.That(outputs.Where(static buffer => buffer is not null).All(
            buffer => buffer!.Length <= maxRetainedBufferSize)).IsTrue();
        await Assert.That(temporaries.Where(static buffer => buffer is not null).All(
            buffer => buffer!.Length <= maxRetainedBufferSize)).IsTrue();
        GC.KeepAlive(encrypted);
    }

    [Test]
    public async Task TransformSerializedPayload_TaggedNestedArrayField_PreservesEscapedUtf8()
    {
        var client = CreateDekClient();
        var handler = CreateHandler(client);
        var rule = CreateRule(tags: new HashSet<string>(StringComparer.Ordinal) { "PII" });
        var schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            Metadata = new SchemaMetadata
            {
                Tags = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
                {
                    ["$.items[1].secret"] = new HashSet<string>(StringComparer.Ordinal) { "PII" }
                }
            },
            RuleSet = new SchemaRuleSet { DomainRules = [rule] }
        };
        var context = CreateHandlerContext(rule, schema);
        var payload = "{\"items\":[{\"secret\":\"plain\"},{\"secret\":\"line\\n\\\"雪\"}]}"u8.ToArray();

        var encrypted = handler.TransformSerializedPayload(payload, context);
        var decrypted = handler.TransformDeserializedPayload(encrypted, context);

        using var encryptedJson = JsonDocument.Parse(encrypted);
        using var decryptedJson = JsonDocument.Parse(decrypted);
        await Assert.That(encryptedJson.RootElement.GetProperty("items")[0].GetProperty("secret").GetString())
            .IsEqualTo("plain");
        await Assert.That(encryptedJson.RootElement.GetProperty("items")[1].GetProperty("secret").GetString())
            .IsNotEqualTo("line\n\"雪");
        await Assert.That(decryptedJson.RootElement.GetProperty("items")[1].GetProperty("secret").GetString())
            .IsEqualTo("line\n\"雪");
    }

    [Test]
    public async Task TransformSerializedPayload_TaggedObject_ThrowsRuleException()
    {
        var rule = CreateRule(tags: new HashSet<string>(StringComparer.Ordinal) { "PII" });
        var schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}",
            Metadata = new SchemaMetadata
            {
                Tags = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
                {
                    ["$"] = new HashSet<string>(StringComparer.Ordinal) { "PII" }
                }
            },
            RuleSet = new SchemaRuleSet { DomainRules = [rule] }
        };
        var handler = CreateHandler(CreateDekClient());
        var context = CreateHandlerContext(rule, schema);

        await Assert.That(() => handler.TransformSerializedPayload("{}"u8.ToArray(), context))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("StartObject");
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
    public async Task TransformSerializedPayload_CallerOwnedRule_ObservesParameterMutations()
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["encrypt.kek.name"] = "payments-kek",
            ["encrypt.dek.algorithm"] = "AES256_GCM"
        };
        var handler = CreateHandler(CreateDekClient());
        var context = CreateHandlerContext(CreateRule(parameters: parameters));

        _ = handler.TransformSerializedPayload("payload"u8.ToArray(), context);
        parameters["encrypt.dek.algorithm"] = "unsupported";

        await Assert.That(() => handler.TransformSerializedPayload("payload"u8.ToArray(), context))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("unsupported");
    }

    [Test]
    public async Task TransformSerializedPayload_CallerOwnedRule_ObservesWhitespaceParameterBecomingNonBlank()
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["encrypt.kek.name"] = "payments-kek",
            ["encrypt.dek.algorithm"] = " "
        };
        var handler = CreateHandler(CreateDekClient());
        var context = CreateHandlerContext(CreateRule(parameters: parameters));

        _ = handler.TransformSerializedPayload("payload"u8.ToArray(), context);
        parameters["encrypt.dek.algorithm"] = "unsupported";

        await Assert.That(() => handler.TransformSerializedPayload("payload"u8.ToArray(), context))
            .Throws<SchemaRegistryRuleException>()
            .WithMessageContaining("unsupported");
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
    public async Task TransformPayload_RotatingSubjects_BoundsDekCachesAndClearsEvictedKeys()
    {
        const int subjectCount = 257;
        var client = CreateDekClient();
        var handler = CreateHandler(client);
        var payload = "payload"u8.ToArray();
        byte[]? firstWriteKey = null;
        byte[]? firstReadKey = null;

        for (var i = 0; i < subjectCount; i++)
        {
            var subject = $"orders-{i}-value";
            var key = new byte[32];
            BinaryPrimitives.WriteInt32BigEndian(key, i + 1);
            client.AddDek(new Dek
            {
                KekName = "payments-kek",
                Subject = subject,
                Version = 1,
                Algorithm = DekAlgorithm.Aes256Gcm,
                KeyMaterial = Convert.ToBase64String(key)
            });

            var rule = CreateRule();
            var context = CreateHandlerContext(rule, subject: subject);
            var encrypted = handler.TransformSerializedPayload(payload, context).ToArray();
            var decrypted = handler.TransformDeserializedPayload(encrypted, context);

            await Assert.That(decrypted.ToArray()).IsEquivalentTo(payload);
            if (i == 0)
            {
                firstWriteKey = GetOnlyCachedDekKeyMaterial(handler, "_writeDeks");
                firstReadKey = GetOnlyCachedDekKeyMaterial(handler, "_readDeks");
            }
        }

        await Assert.That(GetCachedDekCount(handler, "_writeDeks")).IsEqualTo(256);
        await Assert.That(GetCachedDekCount(handler, "_readDeks")).IsEqualTo(256);
        await Assert.That(GetWorkspaceCipherCount(handler, "_gcmCiphers")).IsEqualTo(256);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Assert.That(firstWriteKey).IsNotNull();
        await Assert.That(firstReadKey).IsNotNull();
        await Assert.That(firstWriteKey!.All(static value => value == 0)).IsTrue();
        await Assert.That(firstReadKey!.All(static value => value == 0)).IsTrue();
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
        Schema? schema = null,
        string subject = "orders-value") =>
        new()
        {
            PayloadContext = CreateRuleContext(schema ?? CreateRuleSchema(rule), subject),
            Rule = rule,
            Direction = SchemaRegistryRuleDirection.Write
        };

    private static SchemaRegistryRuleContext CreateRuleContext(
        Schema? schema,
        string subject = "orders-value") =>
        new()
        {
            Topic = "orders",
            Component = SerializationComponent.Value,
            SchemaId = 12,
            Subject = subject,
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

    private static (byte[]?[] Outputs, byte[]?[] Temporaries) GetWorkspaceBuffers(
        SchemaRegistryCsfleRuleHandler handler)
    {
        var workspace = GetWorkspace(handler);
        var workspaceType = workspace.GetType();
        return (
            (byte[]?[])workspaceType.GetField("_outputs", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(workspace)!,
            (byte[]?[])workspaceType.GetField("_temporaries", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(workspace)!);
    }

    private static object GetWorkspace(SchemaRegistryCsfleRuleHandler handler)
    {
        var workspaces = typeof(SchemaRegistryCsfleRuleHandler)
            .GetField("t_workspaces", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        var arguments = new object?[] { handler, null };
        var found = (bool)workspaces.GetType().GetMethod("TryGetValue")!.Invoke(workspaces, arguments)!;
        if (!found)
            throw new InvalidOperationException("CSFLE workspace was not created.");

        return arguments[1]!;
    }

    private static int GetCachedDekCount(SchemaRegistryCsfleRuleHandler handler, string fieldName)
    {
        var cache = typeof(SchemaRegistryCsfleRuleHandler)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(handler)!;
        return (int)cache.GetType().GetProperty("Count")!.GetValue(cache)!;
    }

    private static int GetWorkspaceCipherCount(
        SchemaRegistryCsfleRuleHandler handler,
        string fieldName)
    {
        var workspace = GetWorkspace(handler);
        var cache = workspace.GetType()
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(workspace)!;
        return (int)cache.GetType().GetProperty("Count")!.GetValue(cache)!;
    }

    private static byte[] GetOnlyCachedDekKeyMaterial(
        SchemaRegistryCsfleRuleHandler handler,
        string fieldName)
    {
        var cache = typeof(SchemaRegistryCsfleRuleHandler)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(handler)!;
        var values = (System.Collections.IEnumerable)cache.GetType().GetProperty("Values")!.GetValue(cache)!;
        var entry = values.Cast<object>().Single();
        var lazy = entry.GetType().GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(entry)!;
        var resolved = lazy.GetType().GetProperty("Value")!.GetValue(lazy)!;
        return (byte[])resolved.GetType().GetProperty("KeyMaterial")!.GetValue(resolved)!;
    }

    private sealed class UntrackableTagSet(params string[] tags) : IReadOnlySet<string>
    {
        private readonly HashSet<string> _tags = new(tags, StringComparer.Ordinal);

        public int Count => _tags.Count;

        public bool Contains(string item) => _tags.Contains(item);

        public IEnumerator<string> GetEnumerator() => _tags.GetEnumerator();

        public bool IsProperSubsetOf(IEnumerable<string> other) => _tags.IsProperSubsetOf(other);

        public bool IsProperSupersetOf(IEnumerable<string> other) => _tags.IsProperSupersetOf(other);

        public bool IsSubsetOf(IEnumerable<string> other) => _tags.IsSubsetOf(other);

        public bool IsSupersetOf(IEnumerable<string> other) => _tags.IsSupersetOf(other);

        public bool Overlaps(IEnumerable<string> other) => _tags.Overlaps(other);

        public bool SetEquals(IEnumerable<string> other) => _tags.SetEquals(other);

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
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

        public Task<Schema> GetSchemaAsync(
            int id,
            string subject,
            CancellationToken cancellationToken = default)
        {
            if (_schemasBySubject.TryGetValue(subject, out var schemas))
            {
                for (var i = 0; i < schemas.Count; i++)
                {
                    if (schemas[i].Id == id)
                        return Task.FromResult(schemas[i].Schema);
                }
            }

            throw new SchemaRegistryException(40403, $"Schema '{id}' not found under subject '{subject}'");
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
