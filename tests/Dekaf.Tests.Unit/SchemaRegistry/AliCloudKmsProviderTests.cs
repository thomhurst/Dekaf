using System.Collections.Concurrent;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Kms.AliCloud;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class AliCloudKmsProviderTests
{
    private const string KeyUri = "alicloud-kms://cn-chengdu/alias%2Fcsfle";

    [Test]
    public async Task WrapAndUnwrap_UseDecodedKeyAndUriRegion()
    {
        var client = new XorClient();
        var factory = new RecordingFactory(_ => client);
        var provider = CreateProvider(factory);
        var reference = CreateReference();

        var wrapped = await provider.WrapKeyAsync(new byte[] { 1, 2, 3 }, reference);
        var unwrapped = await provider.UnwrapKeyAsync(wrapped, reference);

        await Assert.That(unwrapped).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(client.LastKeyId).IsEqualTo("alias/csfle");
        await Assert.That(factory.Configurations.Single().RegionId).IsEqualTo("cn-chengdu");
        await Assert.That(client.EncryptCount).IsEqualTo(1);
        await Assert.That(client.DecryptCount).IsEqualTo(1);
    }

    [Test]
    public async Task Provider_RegistersByConfluentType()
    {
        var provider = CreateProvider(new RecordingFactory(_ => new XorClient()));
        var registry = new SchemaRegistryKmsProviderRegistry([provider]);

        var resolved = registry.GetProvider(AliCloudKmsProvider.DefaultType);

        await Assert.That(resolved).IsSameReferenceAs(provider);
        await Assert.That(provider.Type).IsEqualTo("alicloud-kms");
    }

    [Test]
    [Arguments("")]
    [Arguments("key")]
    [Arguments("aws-kms://cn-chengdu/key")]
    [Arguments("alicloud-kms://cn-chengdu")]
    [Arguments("alicloud-kms:///key")]
    [Arguments("alicloud-kms://cn-chengdu/%20")]
    public async Task InvalidKeyUri_IsRejectedBeforeClientCreation(string keyUri)
    {
        var factory = new RecordingFactory(_ => new XorClient());
        var provider = CreateProvider(factory);

        await Assert.That(async () => await provider.WrapKeyAsync(
                new byte[] { 1 },
                CreateReference(keyUri)))
            .Throws<SchemaRegistryKmsException>();
        await Assert.That(factory.CreateCount).IsEqualTo(0);
    }

    [Test]
    public async Task WrongType_IsRejectedBeforeClientCreation()
    {
        var factory = new RecordingFactory(_ => new XorClient());
        var provider = CreateProvider(factory);
        var reference = new SchemaRegistryKmsKeyReference
        {
            KmsType = "aws-kms",
            KmsKeyId = KeyUri
        };

        await Assert.That(async () => await provider.WrapKeyAsync(new byte[] { 1 }, reference))
            .Throws<SchemaRegistryKmsException>();
        await Assert.That(factory.CreateCount).IsEqualTo(0);
    }

    [Test]
    public async Task CanonicalPropertiesOverrideOptionsAndEnvironment()
    {
        var factory = new RecordingFactory(_ => new XorClient());
        var options = new AliCloudKmsProviderOptions
        {
            Endpoint = "options-endpoint",
            AccessKeyId = "options-id",
            AccessKeySecret = "options-secret"
        };
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ALICLOUD_KMS_ENDPOINT"] = "environment-endpoint",
            ["ALIBABA_CLOUD_ACCESS_KEY_ID"] = "environment-id",
            ["ALIBABA_CLOUD_ACCESS_KEY_SECRET"] = "environment-secret"
        };
        var provider = CreateProvider(factory, options, environment);
        var reference = CreateReference(properties: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AliCloudKmsProvider.EndpointProperty] = "property-endpoint",
            [AliCloudKmsProvider.AccessKeyIdProperty] = "property-id",
            [AliCloudKmsProvider.AccessKeySecretProperty] = "property-secret"
        });

        await provider.WrapKeyAsync(new byte[] { 1 }, reference);

        var configuration = factory.Configurations.Single();
        await Assert.That(configuration.Endpoint).IsEqualTo("property-endpoint");
        await Assert.That(configuration.AccessKeyId).IsEqualTo("property-id");
        await Assert.That(configuration.AccessKeySecret).IsEqualTo("property-secret");
        await Assert.That(configuration.CredentialType).IsEqualTo("access_key");
    }

    [Test]
    public async Task OptionsOverrideEnvironment()
    {
        var factory = new RecordingFactory(_ => new XorClient());
        var provider = CreateProvider(
            factory,
            new AliCloudKmsProviderOptions
            {
                Endpoint = "options-endpoint",
                AccessKeyId = "options-id",
                AccessKeySecret = "options-secret"
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ALICLOUD_KMS_ENDPOINT"] = "environment-endpoint",
                ["ALIBABA_CLOUD_ACCESS_KEY_ID"] = "environment-id",
                ["ALIBABA_CLOUD_ACCESS_KEY_SECRET"] = "environment-secret"
            });

        await provider.WrapKeyAsync(new byte[] { 1 }, CreateReference());

        var configuration = factory.Configurations.Single();
        await Assert.That(configuration.Endpoint).IsEqualTo("options-endpoint");
        await Assert.That(configuration.AccessKeyId).IsEqualTo("options-id");
    }

    [Test]
    public async Task RoleSessionExpirationOption_OverridesEnvironment()
    {
        var factory = new RecordingFactory(_ => new XorClient());
        var provider = CreateProvider(
            factory,
            new AliCloudKmsProviderOptions
            {
                CredentialType = "ram_role_arn",
                RoleArn = "acs:ram::123:role/csfle",
                RoleSessionExpiration = 901
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ALICLOUD_KMS_ROLE_SESSION_EXPIRATION"] = "902"
            });

        await provider.WrapKeyAsync(new byte[] { 1 }, CreateReference());

        await Assert.That(factory.Configurations.Single().RoleSessionExpiration).IsEqualTo(901);
    }

    [Test]
    public async Task EnvironmentSuppliesEndpointAndTemporaryCredentials()
    {
        var factory = new RecordingFactory(_ => new XorClient());
        var provider = CreateProvider(
            factory,
            environment: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ALICLOUD_KMS_ENDPOINT"] = "kms.example",
                ["ALIBABA_CLOUD_ACCESS_KEY_ID"] = "temporary-id",
                ["ALIBABA_CLOUD_ACCESS_KEY_SECRET"] = "temporary-secret",
                ["ALIBABA_CLOUD_SECURITY_TOKEN"] = "temporary-token"
            });

        await provider.WrapKeyAsync(new byte[] { 1 }, CreateReference());

        var configuration = factory.Configurations.Single();
        await Assert.That(configuration.Endpoint).IsEqualTo("kms.example");
        await Assert.That(configuration.CredentialType).IsEqualTo("sts");
        await Assert.That(configuration.SecurityToken).IsEqualTo("temporary-token");
    }

    [Test]
    public async Task ExplicitDefaultCredentialType_IgnoresEnvironmentKeys()
    {
        var factory = new RecordingFactory(_ => new XorClient());
        var provider = CreateProvider(
            factory,
            environment: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ALIBABA_CLOUD_ACCESS_KEY_ID"] = "environment-id",
                ["ALIBABA_CLOUD_ACCESS_KEY_SECRET"] = "environment-secret"
            });
        var reference = CreateReference(properties: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AliCloudKmsProvider.CredentialTypeProperty] = "default"
        });

        await provider.WrapKeyAsync(new byte[] { 1 }, reference);

        var configuration = factory.Configurations.Single();
        await Assert.That(configuration.CredentialType).IsEqualTo("default");
        await Assert.That(configuration.AccessKeyId).IsNull();
        await Assert.That(configuration.AccessKeySecret).IsNull();
    }

    [Test]
    public async Task RamRoleProperties_AreResolvedWithDefaultSessionName()
    {
        var factory = new RecordingFactory(_ => new XorClient());
        var provider = CreateProvider(factory);
        var reference = CreateReference(properties: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AliCloudKmsProvider.CredentialTypeProperty] = "ram-role-arn",
            [AliCloudKmsProvider.RoleArnProperty] = "acs:ram::123:role/csfle",
            [AliCloudKmsProvider.RoleSessionExpirationProperty] = "900",
            [AliCloudKmsProvider.PolicyProperty] = "policy",
            [AliCloudKmsProvider.StsEndpointProperty] = "sts.example",
            [AliCloudKmsProvider.RoleExternalIdProperty] = "external"
        });

        await provider.WrapKeyAsync(new byte[] { 1 }, reference);

        var configuration = factory.Configurations.Single();
        await Assert.That(configuration.CredentialType).IsEqualTo("ram_role_arn");
        await Assert.That(configuration.RoleArn).IsEqualTo("acs:ram::123:role/csfle");
        await Assert.That(configuration.RoleSessionName).IsEqualTo("alicloud-kms-csfle");
        await Assert.That(configuration.RoleSessionExpiration).IsEqualTo(900);
        await Assert.That(configuration.Policy).IsEqualTo("policy");
        await Assert.That(configuration.StsEndpoint).IsEqualTo("sts.example");
        await Assert.That(configuration.RoleExternalId).IsEqualTo("external");
    }

    [Test]
    public async Task LegacyDotNetProperties_AreAccepted()
    {
        var factory = new RecordingFactory(_ => new XorClient());
        var provider = CreateProvider(factory);
        var reference = CreateReference(properties: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["endpoint"] = "legacy-endpoint",
            ["access.key.id"] = "legacy-id",
            ["access.key.secret"] = "legacy-secret",
            ["security.token"] = "legacy-token"
        });

        await provider.WrapKeyAsync(new byte[] { 1 }, reference);

        var configuration = factory.Configurations.Single();
        await Assert.That(configuration.Endpoint).IsEqualTo("legacy-endpoint");
        await Assert.That(configuration.CredentialType).IsEqualTo("sts");
        await Assert.That(configuration.SecurityToken).IsEqualTo("legacy-token");
    }

    [Test]
    public async Task DefaultRuleParameterPrefix_IsAccepted()
    {
        var factory = new RecordingFactory(_ => new XorClient());
        var provider = CreateProvider(factory);
        var reference = CreateReference(properties: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rule.executors._default_.param." + AliCloudKmsProvider.EndpointProperty] = "default-rule-endpoint"
        });

        await provider.WrapKeyAsync(new byte[] { 1 }, reference);

        await Assert.That(factory.Configurations.Single().Endpoint).IsEqualTo("default-rule-endpoint");
    }

    [Test]
    [Arguments("access_key", false, false)]
    [Arguments("sts", true, false)]
    [Arguments("ram_role_arn", false, true)]
    [Arguments("unsupported", false, false)]
    public async Task InvalidCredentialConfiguration_IsRejected(
        string credentialType,
        bool includeKeys,
        bool includeRole)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AliCloudKmsProvider.CredentialTypeProperty] = credentialType
        };
        if (includeKeys)
        {
            properties[AliCloudKmsProvider.AccessKeyIdProperty] = "id";
            properties[AliCloudKmsProvider.AccessKeySecretProperty] = "secret";
        }
        if (includeRole)
            properties[AliCloudKmsProvider.RoleSessionExpirationProperty] = "899";
        var factory = new RecordingFactory(_ => new XorClient());
        var provider = CreateProvider(factory);

        await Assert.That(async () => await provider.WrapKeyAsync(
                new byte[] { 1 },
                CreateReference(properties: properties)))
            .Throws<SchemaRegistryKmsException>();
        await Assert.That(factory.CreateCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(AliCloudKmsProvider.AccessKeyIdProperty, false)]
    [Arguments(AliCloudKmsProvider.AccessKeySecretProperty, false)]
    [Arguments(AliCloudKmsProvider.AccessKeyIdProperty, true)]
    [Arguments(AliCloudKmsProvider.AccessKeySecretProperty, true)]
    public async Task IncompleteInferredOrRamRoleAccessKeyPair_IsRejected(
        string configuredProperty,
        bool useRamRole)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [configuredProperty] = "configured-half"
        };
        if (useRamRole)
        {
            properties[AliCloudKmsProvider.RoleArnProperty] = "acs:ram::123:role/csfle";
            properties[AliCloudKmsProvider.RoleSessionExpirationProperty] = "900";
        }

        var factory = new RecordingFactory(_ => new XorClient());
        var provider = CreateProvider(factory);

        await Assert.That(async () => await provider.WrapKeyAsync(
                new byte[] { 1 },
                CreateReference(properties: properties)))
            .Throws<SchemaRegistryKmsException>()
            .WithMessageContaining("must be configured together");
        await Assert.That(factory.CreateCount).IsEqualTo(0);
    }

    [Test]
    public async Task StsWithoutSecurityToken_ReportsMissingTokenProperty()
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AliCloudKmsProvider.CredentialTypeProperty] = "sts",
            [AliCloudKmsProvider.AccessKeyIdProperty] = "id",
            [AliCloudKmsProvider.AccessKeySecretProperty] = "secret"
        };
        var provider = CreateProvider(new RecordingFactory(_ => new XorClient()));

        await Assert.That(async () => await provider.WrapKeyAsync(
                new byte[] { 1 },
                CreateReference(properties: properties)))
            .Throws<SchemaRegistryKmsException>()
            .WithMessageContaining(AliCloudKmsProvider.SecurityTokenProperty);
    }

    [Test]
    public async Task RamRoleSecurityTokenWithoutSourceAccessKeys_IsRejected()
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AliCloudKmsProvider.RoleArnProperty] = "acs:ram::123:role/csfle",
            [AliCloudKmsProvider.SecurityTokenProperty] = "token"
        };
        var factory = new RecordingFactory(_ => new XorClient());
        var provider = CreateProvider(factory);

        await Assert.That(async () => await provider.WrapKeyAsync(
                new byte[] { 1 },
                CreateReference(properties: properties)))
            .Throws<SchemaRegistryKmsException>()
            .WithMessageContaining("must be configured together");
        await Assert.That(factory.CreateCount).IsEqualTo(0);
    }

    [Test]
    public async Task RamRoleExpirationBelowMinimum_IsRejected()
    {
        var factory = new RecordingFactory(_ => new XorClient());
        var provider = CreateProvider(factory);
        var properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AliCloudKmsProvider.CredentialTypeProperty] = "ram_role_arn",
            [AliCloudKmsProvider.RoleArnProperty] = "acs:ram::123:role/csfle",
            [AliCloudKmsProvider.RoleSessionExpirationProperty] = "899"
        };

        await Assert.That(async () => await provider.WrapKeyAsync(
                new byte[] { 1 },
                CreateReference(properties: properties)))
            .Throws<SchemaRegistryKmsException>();
        await Assert.That(factory.CreateCount).IsEqualTo(0);
    }

    [Test]
    public async Task CaFileContent_IsPassedToFactory()
    {
        var caFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(caFile, "-----BEGIN CERTIFICATE-----\nfixture\n-----END CERTIFICATE-----");
            var factory = new RecordingFactory(_ => new XorClient());
            var provider = CreateProvider(factory, new AliCloudKmsProviderOptions { CaFile = caFile });

            await provider.WrapKeyAsync(new byte[] { 1 }, CreateReference());

            await Assert.That(factory.Configurations.Single().CertificateAuthority)
                .Contains("fixture");
        }
        finally
        {
            File.Delete(caFile);
        }
    }

    [Test]
    public async Task CaFileContent_IsReadOncePerCachedClient()
    {
        var caFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(caFile, "-----BEGIN CERTIFICATE-----\nfixture\n-----END CERTIFICATE-----");
            var factory = new RecordingFactory(_ => new XorClient());
            var provider = CreateProvider(factory, new AliCloudKmsProviderOptions { CaFile = caFile });

            await provider.WrapKeyAsync(new byte[] { 1 }, CreateReference());
            File.Delete(caFile);
            await provider.WrapKeyAsync(new byte[] { 2 }, CreateReference());

            await Assert.That(factory.CreateCount).IsEqualTo(1);
        }
        finally
        {
            if (File.Exists(caFile))
                File.Delete(caFile);
        }
    }

    [Test]
    public async Task CaFileReadFailure_IsWrappedAndRetryRecovers()
    {
        var caFile = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pem");
        try
        {
            var factory = new RecordingFactory(_ => new XorClient());
            var provider = CreateProvider(factory, new AliCloudKmsProviderOptions { CaFile = caFile });

            var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
                () => provider.WrapKeyAsync(new byte[] { 1 }, CreateReference()).AsTask());

            await Assert.That(exception!.Message).IsEqualTo("Alibaba Cloud KMS CA file could not be read.");
            await Assert.That(exception.InnerException).IsTypeOf<FileNotFoundException>();
            await Assert.That(factory.CreateCount).IsEqualTo(0);

            await File.WriteAllTextAsync(
                caFile,
                "-----BEGIN CERTIFICATE-----\nfixture\n-----END CERTIFICATE-----");
            await provider.WrapKeyAsync(new byte[] { 1 }, CreateReference());

            await Assert.That(factory.CreateCount).IsEqualTo(1);
        }
        finally
        {
            if (File.Exists(caFile))
                File.Delete(caFile);
        }
    }

    [Test]
    public async Task ClientConfiguration_IsCachedAndSharedConcurrently()
    {
        var client = new XorClient();
        var factory = new RecordingFactory(_ => client);
        var provider = CreateProvider(factory);

        var operations = Enumerable.Range(0, 32)
            .Select(value => provider.WrapKeyAsync(new byte[] { (byte)value }, CreateReference()).AsTask())
            .ToArray();
        await Task.WhenAll(operations);

        await Assert.That(factory.CreateCount).IsEqualTo(1);
        await Assert.That(client.EncryptCount).IsEqualTo(32);
    }

    [Test]
    public async Task ClientCache_IsBounded()
    {
        var maximumCount = 0;
        var factory = new RecordingFactory(_ => new XorClient());
        var provider = new AliCloudKmsProvider(
            factory,
            options: null,
            AliCloudKmsProvider.DefaultType,
            static _ => null,
            count => maximumCount = Math.Max(maximumCount, count));

        for (var index = 0; index <= AliCloudKmsProvider.ClientCacheCapacity; index++)
        {
            await provider.WrapKeyAsync(
                new byte[] { 1 },
                CreateReference($"alicloud-kms://region-{index}/key"));
        }

        await Assert.That(maximumCount).IsEqualTo(AliCloudKmsProvider.ClientCacheCapacity);
        await Assert.That(factory.CreateCount).IsEqualTo(AliCloudKmsProvider.ClientCacheCapacity + 1);
    }

    [Test]
    public async Task FactoryFailure_IsEvictedAndRetried()
    {
        var attempt = 0;
        var factory = new RecordingFactory(_ =>
        {
            if (Interlocked.Increment(ref attempt) == 1)
                throw new HttpRequestException("temporary failure");
            return new XorClient();
        });
        var provider = CreateProvider(factory);

        await Assert.That(async () => await provider.WrapKeyAsync(new byte[] { 1 }, CreateReference()))
            .Throws<SchemaRegistryKmsException>();
        var wrapped = await provider.WrapKeyAsync(new byte[] { 1 }, CreateReference());

        await Assert.That(wrapped).IsNotEmpty();
        await Assert.That(factory.CreateCount).IsEqualTo(2);
    }

    [Test]
    public async Task Cancellation_IsForwardedToClient()
    {
        var client = new CancelingClient();
        var provider = CreateProvider(new RecordingFactory(_ => client));
        using var cancellation = new CancellationTokenSource();
        var operation = provider.WrapKeyAsync(new byte[] { 1 }, CreateReference(), cancellation.Token).AsTask();

        cancellation.Cancel();

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => operation);
        await Assert.That(exception!.CancellationToken).IsEqualTo(cancellation.Token);
        await Assert.That(client.CapturedToken).IsEqualTo(cancellation.Token);
    }

    [Test]
    public async Task TransportFailure_PreservesContextAndInnerException()
    {
        var failure = new HttpRequestException("service detail");
        var provider = CreateProvider(new RecordingFactory(_ => new FailingClient(failure)));

        var exception = await Assert.ThrowsAsync<SchemaRegistryKmsException>(
            () => provider.WrapKeyAsync(new byte[] { 1 }, CreateReference()).AsTask());

        await Assert.That(exception!.Message).IsEqualTo("Alibaba Cloud KMS wrap failed.");
        await Assert.That(exception.InnerException).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task EmptyInputAndEmptyServiceOutput_AreRejected()
    {
        var emptyProvider = CreateProvider(new RecordingFactory(_ => new EmptyClient()));

        await Assert.That(async () => await emptyProvider.WrapKeyAsync(Array.Empty<byte>(), CreateReference()))
            .Throws<SchemaRegistryKmsException>();
        await Assert.That(async () => await emptyProvider.UnwrapKeyAsync(Array.Empty<byte>(), CreateReference()))
            .Throws<SchemaRegistryKmsException>();
        await Assert.That(async () => await emptyProvider.WrapKeyAsync(new byte[] { 1 }, CreateReference()))
            .Throws<SchemaRegistryKmsException>();
    }

    [Test]
    public async Task ConfluentWireEncoding_FixturesRoundTrip()
    {
        var plaintext = new byte[] { 0, 1, 255 };
        const string ciphertextBlob = "CiQAn-alicloud-ciphertext==";

        var encodedPlaintext = AliCloudKmsWireEncoding.EncodePlaintext(plaintext);
        var decodedPlaintext = AliCloudKmsWireEncoding.DecodePlaintext("AAH/");
        var encodedCiphertext = AliCloudKmsWireEncoding.EncodeCiphertext(ciphertextBlob);
        var decodedCiphertext = AliCloudKmsWireEncoding.DecodeCiphertext(encodedCiphertext);

        await Assert.That(encodedPlaintext).IsEqualTo("AAH/");
        await Assert.That(decodedPlaintext).IsEquivalentTo(plaintext);
        await Assert.That(decodedCiphertext).IsEqualTo(ciphertextBlob);
    }

    [Test]
    [Category("Live")]
    public async Task LiveKms_WhenExplicitlyConfigured_WrapsAndUnwraps()
    {
        var keyUri = Environment.GetEnvironmentVariable("DEKAF_ALICLOUD_KMS_KEY_URI");
        if (string.IsNullOrWhiteSpace(keyUri))
            return;

        var provider = new AliCloudKmsProvider();
        var reference = CreateReference(keyUri);
        var plaintext = new byte[] { 1, 3, 3, 7 };

        var wrapped = await provider.WrapKeyAsync(plaintext, reference);
        var unwrapped = await provider.UnwrapKeyAsync(wrapped, reference);

        await Assert.That(unwrapped).IsEquivalentTo(plaintext);
    }

    private static AliCloudKmsProvider CreateProvider(
        IAliCloudKmsClientFactory factory,
        AliCloudKmsProviderOptions? options = null,
        IReadOnlyDictionary<string, string>? environment = null) => new(
            factory,
            options,
            AliCloudKmsProvider.DefaultType,
            name => environment is not null && environment.TryGetValue(name, out var value) ? value : null,
            clientCacheCountChangedForTesting: null);

    private static SchemaRegistryKmsKeyReference CreateReference(
        string keyUri = KeyUri,
        IReadOnlyDictionary<string, string>? properties = null) => new()
        {
            KmsType = AliCloudKmsProvider.DefaultType,
            KmsKeyId = keyUri,
            KmsProps = properties
        };

    private sealed class RecordingFactory(Func<AliCloudKmsClientConfiguration, IAliCloudKmsClient> createClient)
        : IAliCloudKmsClientFactory
    {
        private readonly ConcurrentQueue<AliCloudKmsClientConfiguration> _configurations = [];

        internal int CreateCount => _configurations.Count;
        internal IReadOnlyCollection<AliCloudKmsClientConfiguration> Configurations => _configurations.ToArray();

        public IAliCloudKmsClient CreateClient(AliCloudKmsClientConfiguration configuration)
        {
            _configurations.Enqueue(configuration);
            return createClient(configuration);
        }
    }

    private sealed class XorClient : IAliCloudKmsClient
    {
        private int _encryptCount;
        private int _decryptCount;

        internal int EncryptCount => Volatile.Read(ref _encryptCount);
        internal int DecryptCount => Volatile.Read(ref _decryptCount);
        internal string? LastKeyId { get; private set; }

        public ValueTask<byte[]> EncryptAsync(
            string keyId,
            ReadOnlyMemory<byte> plaintext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastKeyId = keyId;
            Interlocked.Increment(ref _encryptCount);
            return ValueTask.FromResult(Transform(plaintext.Span));
        }

        public ValueTask<byte[]> DecryptAsync(
            ReadOnlyMemory<byte> ciphertext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _decryptCount);
            return ValueTask.FromResult(Transform(ciphertext.Span));
        }

        private static byte[] Transform(ReadOnlySpan<byte> input)
        {
            var output = input.ToArray();
            for (var index = 0; index < output.Length; index++)
                output[index] ^= 0xa5;
            return output;
        }
    }

    private sealed class CancelingClient : IAliCloudKmsClient
    {
        internal CancellationToken CapturedToken { get; private set; }

        public async ValueTask<byte[]> EncryptAsync(
            string keyId,
            ReadOnlyMemory<byte> plaintext,
            CancellationToken cancellationToken = default)
        {
            CapturedToken = cancellationToken;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return [];
        }

        public ValueTask<byte[]> DecryptAsync(
            ReadOnlyMemory<byte> ciphertext,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FailingClient(Exception failure) : IAliCloudKmsClient
    {
        public ValueTask<byte[]> EncryptAsync(
            string keyId,
            ReadOnlyMemory<byte> plaintext,
            CancellationToken cancellationToken = default) => ValueTask.FromException<byte[]>(failure);

        public ValueTask<byte[]> DecryptAsync(
            ReadOnlyMemory<byte> ciphertext,
            CancellationToken cancellationToken = default) => ValueTask.FromException<byte[]>(failure);
    }

    private sealed class EmptyClient : IAliCloudKmsClient
    {
        public ValueTask<byte[]> EncryptAsync(
            string keyId,
            ReadOnlyMemory<byte> plaintext,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(Array.Empty<byte>());

        public ValueTask<byte[]> DecryptAsync(
            ReadOnlyMemory<byte> ciphertext,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(Array.Empty<byte>());
    }
}
