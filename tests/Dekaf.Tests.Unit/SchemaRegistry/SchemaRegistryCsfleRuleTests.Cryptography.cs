using System.Buffers.Binary;
using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed partial class SchemaRegistryCsfleRuleTests
{
    // Independent vectors generated with Python cryptography 48.0.1 (OpenSSL AESSIV):
    // AESSIV(bytes(range(64))).encrypt(bytes(range(length)), [b""]).hex()
    // Tink/Confluent supplies one empty associated-data item, not zero items.
    // https://github.com/tink-crypto/tink-java/blob/main/src/main/java/com/google/crypto/tink/subtle/AesSiv.java
    [Test]
    [Arguments(0, "6ff5b8ef53fc365606cd3ea047374885")]
    [Arguments(1, "bbcda500ab22fb1ab45fe96068870dfe1e")]
    [Arguments(15, "0de333fe4d5bad07944c5c56dec45671a322c688be60c6c70e79fc8304c241")]
    [Arguments(16, "9ecf98ce2cb3f71206a949776d15f9be5a82761b5c0496412e99c0fe98667c0e")]
    [Arguments(17, "49f74bb21732c89f5157cb7eb8af6e28b9ee83720da541e33752bd67796ba35815")]
    [Arguments(32, "63bfa4a6e4faf322411d0d4e29ca546a304cc7c76e679bcd8744c659ea028d9e255dd44737c823c9b4be28de3268827c")]
    [Arguments(65, "e0a79cc86c46c9cbbfa6a3d9f3a958199d1dcd8fd3b7d983c1f07394857935784c756330c78fb9a06686cc0c480eec397d249543291dfda062bd54bd0e11985776856e049f50b631f7ee4a4b52e10029e9")]
    public async Task AesSiv_MatchesIndependentCiphertextAndDecryptsExternalPayload(int length, string ciphertextHex)
    {
        var key = Enumerable.Range(0, 64).Select(static value => (byte)value).ToArray();
        var payload = Enumerable.Range(0, length).Select(static value => (byte)value).ToArray();
        var client = CreateDekClient();
        client.AddDek(TestDek(DekAlgorithm.Aes256Siv, key));
        var handler = CreateHandler(client);
        var context = CreateHandlerContext(AlgorithmRule(DekAlgorithm.Aes256Siv));
        var expected = Convert.FromHexString(ciphertextHex);

        var encrypted = handler.TransformSerializedPayload(payload, context).ToArray();
        await Assert.That(encrypted.AsSpan().SequenceEqual(expected)).IsTrue();
        var decrypted = handler.TransformDeserializedPayload(expected, context).ToArray();
        await Assert.That(decrypted.AsSpan().SequenceEqual(payload)).IsTrue();
    }

    [Test]
    [Arguments(DekAlgorithm.Aes256Gcm, "tag")]
    [Arguments(DekAlgorithm.Aes256Gcm, "ciphertext")]
    [Arguments(DekAlgorithm.Aes256Gcm, "truncated")]
    [Arguments(DekAlgorithm.Aes256Gcm, "wrong-key")]
    [Arguments(DekAlgorithm.Aes256Siv, "tag")]
    [Arguments(DekAlgorithm.Aes256Siv, "ciphertext")]
    [Arguments(DekAlgorithm.Aes256Siv, "truncated")]
    [Arguments(DekAlgorithm.Aes256Siv, "wrong-key")]
    public async Task Decrypt_RejectsUnauthenticatedPayloadAndRecovers(DekAlgorithm algorithm, string corruption)
    {
        var key = new byte[algorithm == DekAlgorithm.Aes256Siv ? 64 : 32];
        var client = CreateDekClient();
        client.AddDek(TestDek(algorithm, key));
        var writer = CreateHandler(client);
        var context = CreateHandlerContext(AlgorithmRule(algorithm));
        var payload = "confidential payload crossing a block boundary"u8.ToArray();
        var valid = writer.TransformSerializedPayload(payload, context).ToArray();
        var damaged = valid.ToArray();
        var readerClient = CreateDekClient();
        var readerKey = key.ToArray();
        switch (corruption)
        {
            case "tag":
                damaged[algorithm == DekAlgorithm.Aes256Siv ? 0 : damaged.Length - 1] ^= 1;
                break;
            case "ciphertext":
                damaged[algorithm == DekAlgorithm.Aes256Siv ? 16 : 12] ^= 1;
                break;
            case "truncated":
                damaged = damaged[..(algorithm == DekAlgorithm.Aes256Siv ? 15 : 27)];
                break;
            case "wrong-key":
                readerKey[0] ^= 1;
                break;
        }
        readerClient.AddDek(TestDek(algorithm, readerKey));
        var reader = CreateHandler(readerClient);

        await Assert.That(() => reader.TransformDeserializedPayload(damaged, context))
            .Throws<SchemaRegistryRuleException>();

        // A failure must not poison the handler's workspace or its cached key.
        var recoveryWriter = CreateHandler(readerClient);
        var recoveryCiphertext = recoveryWriter.TransformSerializedPayload(payload, context).ToArray();
        await Assert.That(reader.TransformDeserializedPayload(recoveryCiphertext, context).Span.SequenceEqual(payload)).IsTrue();
    }

    [Test]
    [Arguments(DekAlgorithm.Aes256Gcm)]
    [Arguments(DekAlgorithm.Aes256Siv)]
    public async Task ExpiredDek_RotatesOnceAndFreshReaderDecryptsBothVersions(DekAlgorithm algorithm)
    {
        var client = CreateDekClient();
        var key = new byte[algorithm == DekAlgorithm.Aes256Siv ? 64 : 32];
        client.AddDek(TestDek(algorithm, key, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        var context = CreateHandlerContext(AlgorithmRule(algorithm, rotate: true));
        var payload = "message before rotation"u8.ToArray();
        var oldCiphertext = CreateHandler(client).TransformSerializedPayload(payload, context).ToArray();
        client.AddDek(TestDek(algorithm, key, DateTimeOffset.UtcNow.AddDays(-2).ToUnixTimeMilliseconds()));
        var writer = CreateHandler(client);
        using var ready = new CountdownEvent(8);
        using var releaseRegistration = new ManualResetEventSlim();
        var registrationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.BeforeRegisterDek = () =>
        {
            registrationEntered.TrySetResult();
            if (!releaseRegistration.Wait(TimeSpan.FromSeconds(15)))
                throw new TimeoutException("Concurrent DEK registration was not released.");
        };
        var operations = Enumerable.Range(0, 8).Select(_ => Task.Factory.StartNew(() =>
        {
            ready.Signal();
            return writer.TransformSerializedPayload(payload, context).ToArray();
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        try
        {
            await registrationEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(ready.Wait(TimeSpan.FromSeconds(10))).IsTrue();
        }
        finally
        {
            releaseRegistration.Set();
        }
        var ciphertexts = await Task.WhenAll(operations).WaitAsync(TimeSpan.FromSeconds(15));

        await Assert.That(client.RegisterDekCallCount).IsEqualTo(1);
        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(oldCiphertext.AsSpan(1, 4))).IsEqualTo(1);
        var reader = CreateHandler(client);
        foreach (var ciphertext in ciphertexts)
        {
            await Assert.That(ciphertext[0]).IsEqualTo((byte)0);
            await Assert.That(BinaryPrimitives.ReadInt32BigEndian(ciphertext.AsSpan(1, 4))).IsEqualTo(2);
            await Assert.That(reader.TransformDeserializedPayload(ciphertext, context).Span.SequenceEqual(payload)).IsTrue();
        }
        // Read old data after loading version 2, using versioned registry lookup rather than writer caches.
        await Assert.That(reader.TransformDeserializedPayload(oldCiphertext, context).Span.SequenceEqual(payload)).IsTrue();
    }

    // Ciphertexts from the original Dekaf S2V construction (CMAC(empty), no AD item).
    [Test]
    [Arguments(0, "64699f564e16f177875ad70d35a153b3")]
    [Arguments(15, "3628604b08d1e04dade900b3f2189a36581ce214ff544eb395e8db619bc338")]
    [Arguments(16, "8cc71f65ca46060c3def6f15900e0fa0d09f6b4a95c88ab264025ddd2cd931b7")]
    [Arguments(17, "c7aaadbb44383d477fa69e96767a7f64fb10bcb18fd7f68ba45d2e831bad648eff")]
    [Arguments(65, "6f695de39428b8d94bf4a34832c5c848f18e9de6a7edb89fb4e43831879ff18ef9a868f1b549ec00bf96626f815131457724b3678c26257a3549e403ac3270a9fd586f34a676ff6e68ba6dd9c3a0d23653")]
    public async Task AesSiv_AuthenticatesLegacyCiphertextAndRejectsTampering(int length, string ciphertextHex)
    {
        var key = Enumerable.Range(0, 64).Select(static value => (byte)value).ToArray();
        var client = CreateDekClient();
        client.AddDek(TestDek(DekAlgorithm.Aes256Siv, key));
        var handler = CreateHandler(client);
        var context = CreateHandlerContext(AlgorithmRule(DekAlgorithm.Aes256Siv));
        var ciphertext = Convert.FromHexString(ciphertextHex);
        var expected = Enumerable.Range(0, length).Select(static value => (byte)value).ToArray();

        await Assert.That(handler.TransformDeserializedPayload(ciphertext, context).Span.SequenceEqual(expected)).IsTrue();
        ciphertext[^1] ^= 1;
        await Assert.That(() => handler.TransformDeserializedPayload(ciphertext, context)).Throws<SchemaRegistryRuleException>();
    }

    private static Dek TestDek(DekAlgorithm algorithm, byte[] key, long? timestamp = null) => new()
    {
        KekName = "payments-kek", Subject = "orders-value", Version = 1,
        Algorithm = algorithm, KeyMaterial = Convert.ToBase64String(key), Timestamp = timestamp
    };

    private static SchemaRule AlgorithmRule(DekAlgorithm algorithm, bool rotate = false) => CreateRule(
        parameters: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["encrypt.kek.name"] = "payments-kek",
            ["encrypt.dek.algorithm"] = algorithm == DekAlgorithm.Aes256Siv ? "AES256_SIV" : "AES256_GCM",
            ["encrypt.dek.expiry.days"] = rotate ? "1" : "0"
        });
}
