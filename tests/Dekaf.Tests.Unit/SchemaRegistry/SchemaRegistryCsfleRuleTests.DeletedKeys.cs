using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed partial class SchemaRegistryCsfleRuleTests
{
    [Test]
    [Arguments(DekAlgorithm.Aes256Gcm, false, false)]
    [Arguments(DekAlgorithm.Aes256Gcm, false, true)]
    [Arguments(DekAlgorithm.Aes256Gcm, true, false)]
    [Arguments(DekAlgorithm.Aes256Gcm, true, true)]
    [Arguments(DekAlgorithm.Aes256Siv, false, false)]
    [Arguments(DekAlgorithm.Aes256Siv, false, true)]
    [Arguments(DekAlgorithm.Aes256Siv, true, false)]
    [Arguments(DekAlgorithm.Aes256Siv, true, true)]
    public async Task FreshReader_DecryptsWithSoftDeletedKey(DekAlgorithm algorithm, bool versioned, bool deleteKek)
    {
        var client = CreateDekClient();
        var context = CreateHandlerContext(AlgorithmRule(algorithm, rotate: versioned));
        var payload = "historical ciphertext"u8.ToArray();
        var ciphertext = CreateHandler(client).TransformSerializedPayload(payload, context).ToArray();
        var originalDek = await client.GetDekAsync("payments-kek", "orders-value", 1, algorithm);
        client.AddDek(new Dek
        {
            KekName = originalDek.KekName,
            Subject = originalDek.Subject,
            Version = originalDek.Version,
            Algorithm = originalDek.Algorithm,
            EncryptedKeyMaterial = originalDek.EncryptedKeyMaterial,
            Timestamp = originalDek.Timestamp,
            Deleted = !deleteKek
        });
        client.AddKek(new Kek
        {
            Name = "payments-kek",
            KmsType = LocalKmsProvider.DefaultType,
            KmsKeyId = "local://payments",
            Deleted = deleteKek
        });
        if (versioned)
        {
            // The reader must fetch the ciphertext's version, even when a newer key exists.
            client.AddDek(new Dek
            {
                KekName = originalDek.KekName,
                Subject = originalDek.Subject,
                Version = 2,
                Algorithm = algorithm,
                KeyMaterial = Convert.ToBase64String(new byte[algorithm == DekAlgorithm.Aes256Siv ? 64 : 32])
            });
        }
        var kekRegistrations = 0;
        client.BeforeRegisterKek = () => kekRegistrations++;
        var dekRegistrations = client.RegisterDekCallCount;
        var reader = CreateHandler(client);

        var decrypted = reader.TransformDeserializedPayload(ciphertext, context);

        await Assert.That(decrypted.Span.SequenceEqual(payload)).IsTrue();
        await Assert.That(kekRegistrations).IsEqualTo(0);
        await Assert.That(client.RegisterDekCallCount).IsEqualTo(dekRegistrations);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task FreshReader_MissingKey_NeverRegistersKeys(bool versioned, bool missingKek)
    {
        var rule = CreateRule(parameters: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["encrypt.kek.name"] = "payments-kek",
            ["encrypt.kms.type"] = LocalKmsProvider.DefaultType,
            ["encrypt.kms.key.id"] = "local://payments",
            ["encrypt.dek.expiry.days"] = versioned ? "1" : "0"
        });
        var context = CreateHandlerContext(rule);
        var ciphertext = CreateHandler(CreateDekClient())
            .TransformSerializedPayload("historical ciphertext"u8.ToArray(), context).ToArray();
        var readerClient = missingKek ? new FakeDekRegistryClient() : CreateDekClient();
        var kekRegistrations = 0;
        readerClient.BeforeRegisterKek = () => kekRegistrations++;
        var reader = CreateHandler(readerClient);

        await Assert.That(() => reader.TransformDeserializedPayload(ciphertext, context))
            .Throws<SchemaRegistryException>();
        await Assert.That(kekRegistrations).IsEqualTo(0);
        await Assert.That(readerClient.RegisterDekCallCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FreshWriter_ExcludesSoftDeletedKeys(bool deleteKek)
    {
        var client = CreateDekClient();
        client.AddKek(new Kek
        {
            Name = "payments-kek",
            KmsType = LocalKmsProvider.DefaultType,
            KmsKeyId = "local://payments",
            Deleted = deleteKek
        });
        client.AddDek(new Dek
        {
            KekName = "payments-kek",
            Subject = "orders-value",
            Version = 1,
            Algorithm = DekAlgorithm.Aes256Gcm,
            KeyMaterial = Convert.ToBase64String(new byte[32]),
            Deleted = !deleteKek
        });
        var writer = CreateHandler(client);
        var context = CreateHandlerContext(CreateRule());

        if (deleteKek)
        {
            await Assert.That(() => writer.TransformSerializedPayload("payload"u8.ToArray(), context))
                .Throws<SchemaRegistryRuleException>();
        }
        else
        {
            await Assert.That(() => writer.TransformSerializedPayload("payload"u8.ToArray(), context))
                .Throws<SchemaRegistryException>();
        }
    }
}
