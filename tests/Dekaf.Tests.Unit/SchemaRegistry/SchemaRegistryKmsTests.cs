using System.Text;
using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaRegistryKmsTests
{
    private static readonly byte[] KekMaterial =
    [
        0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
        0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
        0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17,
        0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F
    ];

    [Test]
    public async Task LocalKmsProvider_WrapIsDeterministicAndUnwrapRestoresPlaintext()
    {
        var provider = CreateLocalProvider();
        var keyReference = CreateKeyReference();
        var plaintext = Encoding.UTF8.GetBytes("data-encryption-key-material");

        var wrapped1 = await provider.WrapKeyAsync(plaintext, keyReference);
        var wrapped2 = await provider.WrapKeyAsync(plaintext, keyReference);
        var unwrapped = await provider.UnwrapKeyAsync(wrapped1, keyReference);

        await Assert.That(wrapped1).IsEquivalentTo(wrapped2);
        await Assert.That(wrapped1).IsNotEquivalentTo(plaintext);
        await Assert.That(unwrapped).IsEquivalentTo(plaintext);
    }

    [Test]
    public async Task ProviderRegistry_WrapUnwrap_RoutesByKmsType()
    {
        var provider = CreateLocalProvider(type: "test-local");
        var registry = new SchemaRegistryKmsProviderRegistry([provider]);
        var keyReference = CreateKeyReference(kmsType: "test-local");
        var plaintext = "schema-registry-dek"u8.ToArray();

        var wrapped = await registry.WrapKeyAsync(plaintext, keyReference);
        var unwrapped = await registry.UnwrapKeyAsync(wrapped, keyReference);

        await Assert.That(registry.TryGetProvider("TEST-LOCAL", out var resolved)).IsTrue();
        await Assert.That(resolved).IsSameReferenceAs(provider);
        await Assert.That(unwrapped).IsEquivalentTo(plaintext);
    }

    [Test]
    public async Task ProviderRegistry_DuplicateProviderType_Throws()
    {
        var provider1 = CreateLocalProvider(type: "local-kms");
        var provider2 = CreateLocalProvider(type: "LOCAL-KMS");

        await Assert.That(() => new SchemaRegistryKmsProviderRegistry([provider1, provider2]))
            .Throws<ArgumentException>()
            .WithMessageContaining("LOCAL-KMS");
    }

    [Test]
    public async Task Options_CreateProviderRegistry_UsesConfiguredProviders()
    {
        var options = new SchemaRegistryKmsOptions();
        var provider = CreateLocalProvider();
        options.Providers.Add(provider);
        var registry = options.CreateProviderRegistry();

        var wrapped = await registry.WrapKeyAsync(
            "offline-dek"u8.ToArray(),
            CreateKeyReference());

        await Assert.That(wrapped).IsNotEmpty();
    }

    [Test]
    public async Task LocalKmsProvider_CopiesConfiguredKeyMaterial()
    {
        var originalKek = KekMaterial.ToArray();
        var provider = new LocalKmsProvider(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["local://payments"] = originalKek
        });
        originalKek.AsSpan().Clear();

        var plaintext = "copied-key-material"u8.ToArray();
        var wrapped = await provider.WrapKeyAsync(plaintext, CreateKeyReference());
        var unwrapped = await provider.UnwrapKeyAsync(wrapped, CreateKeyReference());

        await Assert.That(unwrapped).IsEquivalentTo(plaintext);
    }

    [Test]
    public async Task LocalKmsProvider_InvalidKeyMaterialLength_Throws()
    {
        await Assert.That(() =>
            new LocalKmsProvider(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["local://bad"] = [1, 2, 3]
            }))
            .Throws<ArgumentException>()
            .WithMessageContaining("128, 192, or 256 bits");
    }

    [Test]
    public async Task Registry_UnknownProvider_DoesNotLeakKeyMaterial()
    {
        var registry = new SchemaRegistryKmsProviderRegistry([]);
        var secret = Encoding.UTF8.GetBytes("super-secret-dek-material");

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            async () => await registry.WrapKeyAsync(
                secret,
                CreateKeyReference(kmsType: "missing-kms")));

        await Assert.That(exception!.Message).DoesNotContain("super-secret");
        await Assert.That(exception.Message).DoesNotContain(Convert.ToBase64String(secret));
    }

    [Test]
    public async Task LocalKmsProvider_UnwrapFailure_DoesNotLeakEncryptedMaterial()
    {
        var provider = CreateLocalProvider();
        var encrypted = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            async () => await provider.UnwrapKeyAsync(encrypted, CreateKeyReference()));

        await Assert.That(exception!.Message).DoesNotContain(Convert.ToHexString(encrypted));
        await Assert.That(exception.Message).Contains("invalid");
    }

    private static LocalKmsProvider CreateLocalProvider(string type = LocalKmsProvider.DefaultType) =>
        new(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["local://payments"] = KekMaterial
        }, type);

    private static SchemaRegistryKmsKeyReference CreateKeyReference(
        string kmsType = LocalKmsProvider.DefaultType) =>
        new()
        {
            KmsType = kmsType,
            KmsKeyId = "local://payments",
            KmsProps = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["environment"] = "test"
            }
        };
}
