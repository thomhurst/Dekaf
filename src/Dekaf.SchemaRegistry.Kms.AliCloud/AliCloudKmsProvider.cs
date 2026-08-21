using System.Collections.Concurrent;
using Tea;

namespace Dekaf.SchemaRegistry.Kms.AliCloud;

/// <summary>
/// Encrypts and decrypts key material through Alibaba Cloud KMS.
/// </summary>
public interface IAliCloudKmsClient
{
    /// <summary>
    /// Encrypts plaintext with an Alibaba Cloud KMS key.
    /// </summary>
    ValueTask<byte[]> EncryptAsync(
        string keyId,
        ReadOnlyMemory<byte> plaintext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts Alibaba Cloud KMS ciphertext.
    /// </summary>
    ValueTask<byte[]> DecryptAsync(
        ReadOnlyMemory<byte> ciphertext,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates Alibaba Cloud KMS clients for resolved regions, endpoints, and credentials.
/// </summary>
public interface IAliCloudKmsClientFactory
{
    /// <summary>
    /// Creates a thread-safe KMS client for <paramref name="configuration" />.
    /// </summary>
    IAliCloudKmsClient CreateClient(AliCloudKmsClientConfiguration configuration);
}

/// <summary>
/// Resolved Alibaba Cloud KMS client configuration.
/// </summary>
public sealed class AliCloudKmsClientConfiguration
{
    /// <summary>
    /// Alibaba Cloud region parsed from the key URI.
    /// </summary>
    public required string RegionId { get; init; }

    /// <summary>
    /// Optional KMS endpoint override.
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// Optional PEM certificate-authority content.
    /// </summary>
    public string? CertificateAuthority { get; init; }

    /// <summary>
    /// Resolved credential type: default, access_key, sts, or ram_role_arn.
    /// </summary>
    public required string CredentialType { get; init; }

    /// <summary>
    /// Optional explicit access-key identifier.
    /// </summary>
    public string? AccessKeyId { get; init; }

    /// <summary>
    /// Optional explicit access-key secret.
    /// </summary>
    public string? AccessKeySecret { get; init; }

    /// <summary>
    /// Optional temporary-credential security token.
    /// </summary>
    public string? SecurityToken { get; init; }

    /// <summary>
    /// Optional RAM role ARN to assume.
    /// </summary>
    public string? RoleArn { get; init; }

    /// <summary>
    /// Optional RAM role session name.
    /// </summary>
    public string? RoleSessionName { get; init; }

    /// <summary>
    /// Optional RAM role session duration in seconds.
    /// </summary>
    public int? RoleSessionExpiration { get; init; }

    /// <summary>
    /// Optional RAM role policy.
    /// </summary>
    public string? Policy { get; init; }

    /// <summary>
    /// Optional Security Token Service endpoint override.
    /// </summary>
    public string? StsEndpoint { get; init; }

    /// <summary>
    /// Optional external identifier for RAM role assumption.
    /// </summary>
    public string? RoleExternalId { get; init; }
}

/// <summary>
/// Default values used when a KEK does not provide an Alibaba Cloud KMS property.
/// </summary>
public sealed class AliCloudKmsProviderOptions
{
    /// <summary>Optional KMS endpoint override.</summary>
    public string? Endpoint { get; init; }

    /// <summary>Optional path to a PEM certificate-authority file.</summary>
    public string? CaFile { get; init; }

    /// <summary>Optional credential type: default, access_key, sts, or ram_role_arn.</summary>
    public string? CredentialType { get; init; }

    /// <summary>Optional explicit access-key identifier.</summary>
    public string? AccessKeyId { get; init; }

    /// <summary>Optional explicit access-key secret.</summary>
    public string? AccessKeySecret { get; init; }

    /// <summary>Optional temporary-credential security token.</summary>
    public string? SecurityToken { get; init; }

    /// <summary>Optional RAM role ARN to assume.</summary>
    public string? RoleArn { get; init; }

    /// <summary>Optional RAM role session name.</summary>
    public string? RoleSessionName { get; init; }

    /// <summary>Optional RAM role session duration in seconds.</summary>
    public int? RoleSessionExpiration { get; init; }

    /// <summary>Optional RAM role policy.</summary>
    public string? Policy { get; init; }

    /// <summary>Optional Security Token Service endpoint override.</summary>
    public string? StsEndpoint { get; init; }

    /// <summary>Optional external identifier for RAM role assumption.</summary>
    public string? RoleExternalId { get; init; }
}

/// <summary>
/// Wraps and unwraps Schema Registry data encryption keys with Alibaba Cloud KMS.
/// </summary>
public sealed class AliCloudKmsProvider : ISchemaRegistryKmsProvider
{
    /// <summary>Schema Registry KMS provider type.</summary>
    public const string DefaultType = "alicloud-kms";

    /// <summary>Alibaba Cloud KMS key URI prefix.</summary>
    public const string KeyUriPrefix = "alicloud-kms://";

    /// <summary>KEK property for an explicit access-key identifier.</summary>
    public const string AccessKeyIdProperty = "alicloud.kms.accessKeyId";

    /// <summary>KEK property for an explicit access-key secret.</summary>
    public const string AccessKeySecretProperty = "alicloud.kms.accessKeySecret";

    /// <summary>KEK property for a temporary-credential security token.</summary>
    public const string SecurityTokenProperty = "alicloud.kms.securityToken";

    /// <summary>KEK property for the credential type.</summary>
    public const string CredentialTypeProperty = "alicloud.kms.credentialType";

    /// <summary>KEK property for a RAM role ARN.</summary>
    public const string RoleArnProperty = "alicloud.kms.roleArn";

    /// <summary>KEK property for a RAM role session name.</summary>
    public const string RoleSessionNameProperty = "alicloud.kms.roleSessionName";

    /// <summary>KEK property for a RAM role session duration.</summary>
    public const string RoleSessionExpirationProperty = "alicloud.kms.roleSessionExpiration";

    /// <summary>KEK property for a RAM role policy.</summary>
    public const string PolicyProperty = "alicloud.kms.policy";

    /// <summary>KEK property for a Security Token Service endpoint.</summary>
    public const string StsEndpointProperty = "alicloud.kms.stsEndpoint";

    /// <summary>KEK property for a RAM role external identifier.</summary>
    public const string RoleExternalIdProperty = "alicloud.kms.externalId";

    /// <summary>KEK property for a KMS endpoint override.</summary>
    public const string EndpointProperty = "alicloud.kms.endpoint";

    /// <summary>KEK property for a PEM certificate-authority file path.</summary>
    public const string CaFileProperty = "alicloud.kms.caFile";

    private const string LegacyAccessKeyIdProperty = "access.key.id";
    private const string LegacyAccessKeySecretProperty = "access.key.secret";
    private const string LegacySecurityTokenProperty = "security.token";
    private const string LegacyRoleArnProperty = "role.arn";
    private const string LegacyRoleSessionNameProperty = "role.session.name";
    private const string LegacyRoleSessionExpirationProperty = "role.session.expiration";
    private const string LegacyPolicyProperty = "policy";
    private const string LegacyStsEndpointProperty = "sts.endpoint";
    private const string LegacyRoleExternalIdProperty = "role.external.id";
    private const string LegacyEndpointProperty = "endpoint";
    private const string LegacyCaFileProperty = "ca.file";
    private const string DefaultRuleParameterPrefix = "rule.executors._default_.param.";

    internal const int ClientCacheCapacity = 64;
    private const string DefaultRoleSessionName = "alicloud-kms-csfle";

    private readonly AliCloudKmsProviderOptions _options;
    private readonly Func<string, string?> _environmentVariableReader;
    private readonly BoundedClientCache _clients;

    /// <summary>
    /// Creates a provider using the Alibaba Cloud SDK default credential chain.
    /// </summary>
    public AliCloudKmsProvider(
        AliCloudKmsProviderOptions? options = null,
        string type = DefaultType)
        : this(new AliCloudSdkKmsClientFactory(), options, type)
    {
    }

    /// <summary>
    /// Creates a provider using a custom client factory.
    /// </summary>
    public AliCloudKmsProvider(
        IAliCloudKmsClientFactory clientFactory,
        AliCloudKmsProviderOptions? options = null,
        string type = DefaultType)
        : this(clientFactory, options, type, Environment.GetEnvironmentVariable, null)
    {
    }

    internal AliCloudKmsProvider(
        IAliCloudKmsClientFactory clientFactory,
        AliCloudKmsProviderOptions? options,
        string type,
        Func<string, string?> environmentVariableReader,
        Action<int>? clientCacheCountChangedForTesting)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(environmentVariableReader);
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("KMS provider type cannot be null or whitespace.", nameof(type));

        _options = options ?? new AliCloudKmsProviderOptions();
        _environmentVariableReader = environmentVariableReader;
        _clients = new BoundedClientCache(clientFactory, ClientCacheCapacity, clientCacheCountChangedForTesting);
        Type = type;
    }

    /// <inheritdoc />
    public string Type { get; }

    /// <inheritdoc />
    public async ValueTask<byte[]> WrapKeyAsync(
        ReadOnlyMemory<byte> keyMaterial,
        SchemaRegistryKmsKeyReference keyReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (keyMaterial.IsEmpty)
            throw new SchemaRegistryKmsException("Alibaba Cloud KMS wrap failed. Key material cannot be empty.");

        var operation = ResolveOperation(keyReference);
        var client = _clients.GetOrAdd(operation.ClientConfiguration);
        try
        {
            var ciphertext = await client.Value
                .EncryptAsync(operation.KeyId, keyMaterial, cancellationToken)
                .ConfigureAwait(false);
            return RequireMaterial(ciphertext, "wrap");
        }
        catch (Exception ex) when (!client.IsValueCreated)
        {
            _clients.Remove(operation.ClientConfiguration, client);
            if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                throw;
            if (IsAliCloudFailure(ex))
                throw new SchemaRegistryKmsException("Alibaba Cloud KMS wrap failed.", ex);
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SchemaRegistryKmsException)
        {
            throw;
        }
        catch (Exception ex) when (IsAliCloudFailure(ex))
        {
            throw new SchemaRegistryKmsException("Alibaba Cloud KMS wrap failed.", ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<byte[]> UnwrapKeyAsync(
        ReadOnlyMemory<byte> encryptedKeyMaterial,
        SchemaRegistryKmsKeyReference keyReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (encryptedKeyMaterial.IsEmpty)
        {
            throw new SchemaRegistryKmsException(
                "Alibaba Cloud KMS unwrap failed. Encrypted key material cannot be empty.");
        }

        var operation = ResolveOperation(keyReference);
        var client = _clients.GetOrAdd(operation.ClientConfiguration);
        try
        {
            var plaintext = await client.Value
                .DecryptAsync(encryptedKeyMaterial, cancellationToken)
                .ConfigureAwait(false);
            return RequireMaterial(plaintext, "unwrap");
        }
        catch (Exception ex) when (!client.IsValueCreated)
        {
            _clients.Remove(operation.ClientConfiguration, client);
            if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                throw;
            if (IsAliCloudFailure(ex))
                throw new SchemaRegistryKmsException("Alibaba Cloud KMS unwrap failed.", ex);
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SchemaRegistryKmsException)
        {
            throw;
        }
        catch (Exception ex) when (IsAliCloudFailure(ex))
        {
            throw new SchemaRegistryKmsException("Alibaba Cloud KMS unwrap failed.", ex);
        }
    }

    private OperationConfiguration ResolveOperation(SchemaRegistryKmsKeyReference keyReference)
    {
        ArgumentNullException.ThrowIfNull(keyReference);
        if (!string.Equals(Type, keyReference.KmsType, StringComparison.OrdinalIgnoreCase))
        {
            throw new SchemaRegistryKmsException(
                $"Alibaba Cloud KMS provider '{Type}' cannot resolve KMS type '{keyReference.KmsType}'.");
        }

        var keyUriValue = keyReference.KmsKeyId;
        if (string.IsNullOrWhiteSpace(keyUriValue)
            || !keyUriValue.StartsWith(KeyUriPrefix, StringComparison.OrdinalIgnoreCase)
            || !Uri.TryCreate(keyUriValue, UriKind.Absolute, out var keyUri)
            || !string.Equals(keyUri.Scheme, DefaultType, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(keyUri.Host)
            || keyUri.AbsolutePath.Length <= 1)
        {
            throw new SchemaRegistryKmsException(
                "Alibaba Cloud KMS key identifier must use 'alicloud-kms://<region>/<key>'.");
        }

        var regionId = keyUri.Host;
        var keyId = Uri.UnescapeDataString(keyUri.AbsolutePath[1..]);
        if (string.IsNullOrWhiteSpace(keyId) || ContainsInvalidKeyCharacter(keyId))
        {
            throw new SchemaRegistryKmsException(
                "Alibaba Cloud KMS key identifier must use 'alicloud-kms://<region>/<key>'.");
        }

        return new OperationConfiguration(
            keyId,
            ResolveClientConfiguration(regionId, keyReference.KmsProps));
    }

    private ClientCacheKey ResolveClientConfiguration(
        string regionId,
        IReadOnlyDictionary<string, string>? properties)
    {
        var accessKeyId = ResolveCredentialValue(
            properties,
            AccessKeyIdProperty,
            LegacyAccessKeyIdProperty,
            _options.AccessKeyId,
            "ALIBABA_CLOUD_ACCESS_KEY_ID");
        var accessKeySecret = ResolveCredentialValue(
            properties,
            AccessKeySecretProperty,
            LegacyAccessKeySecretProperty,
            _options.AccessKeySecret,
            "ALIBABA_CLOUD_ACCESS_KEY_SECRET");
        var securityToken = ResolveCredentialValue(
            properties,
            SecurityTokenProperty,
            LegacySecurityTokenProperty,
            _options.SecurityToken,
            "ALIBABA_CLOUD_SECURITY_TOKEN");
        var roleArn = ResolveCredentialValue(
            properties,
            RoleArnProperty,
            LegacyRoleArnProperty,
            _options.RoleArn,
            "ALICLOUD_KMS_ROLE_ARN",
            "ALIBABA_CLOUD_ROLE_ARN");
        var roleSessionName = ResolveCredentialValue(
            properties,
            RoleSessionNameProperty,
            LegacyRoleSessionNameProperty,
            _options.RoleSessionName,
            "ALICLOUD_KMS_ROLE_SESSION_NAME",
            "ALIBABA_CLOUD_ROLE_SESSION_NAME");

        var credentialType = ResolveCredentialType(properties, accessKeyId, accessKeySecret, securityToken, roleArn);

        if (credentialType != CredentialKind.Default
            && (accessKeyId is null) != (accessKeySecret is null))
        {
            throw new SchemaRegistryKmsException(
                "Alibaba Cloud KMS access key ID and secret must be configured together.");
        }

        if (credentialType == CredentialKind.RamRoleArn
            && securityToken is not null
            && accessKeyId is null)
        {
            throw new SchemaRegistryKmsException(
                "Alibaba Cloud KMS access key ID and secret must be configured together when a RAM role security token is provided.");
        }

        if (credentialType is CredentialKind.AccessKey or CredentialKind.Sts
            && (accessKeyId is null || accessKeySecret is null))
        {
            throw new SchemaRegistryKmsException(
                "Alibaba Cloud KMS access key ID and secret must be configured together.");
        }

        if (credentialType == CredentialKind.Sts && securityToken is null)
        {
            throw new SchemaRegistryKmsException(
                $"Alibaba Cloud KMS credential type 'sts' requires property '{SecurityTokenProperty}'.");
        }

        if (credentialType == CredentialKind.RamRoleArn && roleArn is null)
        {
            throw new SchemaRegistryKmsException(
                "Alibaba Cloud KMS RAM role credentials require a role ARN.");
        }

        if (credentialType == CredentialKind.Default)
        {
            accessKeyId = null;
            accessKeySecret = null;
            securityToken = null;
        }
        else if (credentialType == CredentialKind.AccessKey)
        {
            securityToken = null;
        }
        else if (credentialType == CredentialKind.RamRoleArn
            && (accessKeyId is null || accessKeySecret is null))
        {
            accessKeyId = null;
            accessKeySecret = null;
            securityToken = null;
        }

        var expiration = credentialType == CredentialKind.RamRoleArn
            ? ResolveRoleSessionExpiration(properties)
            : null;

        var caFile = ResolveValue(
            properties,
            CaFileProperty,
            LegacyCaFileProperty,
            _options.CaFile,
            "ALICLOUD_KMS_CA_FILE");
        return new ClientCacheKey(
            regionId,
            ResolveValue(
                properties,
                EndpointProperty,
                LegacyEndpointProperty,
                _options.Endpoint,
                "ALICLOUD_KMS_ENDPOINT"),
            caFile,
            CredentialTypeName(credentialType),
            accessKeyId,
            accessKeySecret,
            securityToken,
            credentialType == CredentialKind.RamRoleArn ? roleArn : null,
            credentialType == CredentialKind.RamRoleArn ? roleSessionName ?? DefaultRoleSessionName : null,
            expiration,
            credentialType == CredentialKind.RamRoleArn
                ? ResolveValue(properties, PolicyProperty, LegacyPolicyProperty, _options.Policy, "ALICLOUD_KMS_ROLE_POLICY")
                : null,
            credentialType == CredentialKind.RamRoleArn
                ? ResolveValue(properties, StsEndpointProperty, LegacyStsEndpointProperty, _options.StsEndpoint, "ALICLOUD_KMS_STS_ENDPOINT")
                : null,
            credentialType == CredentialKind.RamRoleArn
                ? ResolveValue(properties, RoleExternalIdProperty, LegacyRoleExternalIdProperty, _options.RoleExternalId, "ALICLOUD_KMS_EXTERNAL_ID")
                : null);
    }

    private int? ResolveRoleSessionExpiration(IReadOnlyDictionary<string, string>? properties)
    {
        if (TryGetNonEmptyValue(
                properties,
                RoleSessionExpirationProperty,
                LegacyRoleSessionExpirationProperty,
                out var value))
            return ParseRoleSessionExpiration(value);

        if (_options.RoleSessionExpiration is < 900)
        {
            throw new SchemaRegistryKmsException(
                $"Alibaba Cloud KMS property '{RoleSessionExpirationProperty}' must be at least 900 seconds.");
        }

        return _options.RoleSessionExpiration
            ?? (Normalize(_environmentVariableReader("ALICLOUD_KMS_ROLE_SESSION_EXPIRATION")) is { } environmentValue
                ? ParseRoleSessionExpiration(environmentValue)
                : null);
    }

    private static int ParseRoleSessionExpiration(string value)
    {
        if (!int.TryParse(
                value,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed))
        {
            throw new SchemaRegistryKmsException(
                $"Alibaba Cloud KMS property '{RoleSessionExpirationProperty}' must be an integer.");
        }

        if (parsed < 900)
        {
            throw new SchemaRegistryKmsException(
                $"Alibaba Cloud KMS property '{RoleSessionExpirationProperty}' must be at least 900 seconds.");
        }

        return parsed;
    }

    private CredentialKind ResolveCredentialType(
        IReadOnlyDictionary<string, string>? properties,
        string? accessKeyId,
        string? accessKeySecret,
        string? securityToken,
        string? roleArn)
    {
        var configured = ResolveValue(
            properties,
            CredentialTypeProperty,
            legacyPropertyName: null,
            _options.CredentialType,
            "ALICLOUD_KMS_CREDENTIAL_TYPE");
        if (configured is null)
        {
            if (roleArn is not null)
                return CredentialKind.RamRoleArn;
            if (securityToken is not null)
                return CredentialKind.Sts;
            return accessKeyId is not null || accessKeySecret is not null
                ? CredentialKind.AccessKey
                : CredentialKind.Default;
        }

        return configured.Trim().ToLowerInvariant().Replace('-', '_') switch
        {
            "default" => CredentialKind.Default,
            "access_key" => CredentialKind.AccessKey,
            "sts" => CredentialKind.Sts,
            "ram_role_arn" => CredentialKind.RamRoleArn,
            _ => throw new SchemaRegistryKmsException(
                $"Alibaba Cloud KMS property '{CredentialTypeProperty}' must be default, access_key, sts, or ram_role_arn.")
        };
    }

    private string? ResolveCredentialValue(
        IReadOnlyDictionary<string, string>? properties,
        string propertyName,
        string? legacyPropertyName,
        string? optionValue,
        string environmentVariable,
        string? alternateEnvironmentVariable = null)
    {
        var value = ResolveValue(properties, propertyName, legacyPropertyName, optionValue);
        return value
            ?? Normalize(_environmentVariableReader(environmentVariable))
            ?? (alternateEnvironmentVariable is null
                ? null
                : Normalize(_environmentVariableReader(alternateEnvironmentVariable)));
    }

    private string? ResolveValue(
        IReadOnlyDictionary<string, string>? properties,
        string propertyName,
        string? legacyPropertyName,
        string? optionValue,
        string? environmentVariable = null) =>
        TryGetNonEmptyValue(properties, propertyName, legacyPropertyName, out var value)
            ? value
            : Normalize(optionValue)
                ?? (environmentVariable is null
                    ? null
                    : Normalize(_environmentVariableReader(environmentVariable)));

    private static bool TryGetNonEmptyValue(
        IReadOnlyDictionary<string, string>? properties,
        string name,
        string? legacyName,
        out string value)
    {
        if (TryGetProperty(properties, name, out value)
            || TryGetProperty(properties, DefaultRuleParameterPrefix + name, out value)
            || legacyName is not null && TryGetProperty(properties, legacyName, out value))
        {
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetProperty(
        IReadOnlyDictionary<string, string>? properties,
        string name,
        out string value)
    {
        if (properties is not null
            && properties.TryGetValue(name, out var configured)
            && Normalize(configured) is { } normalized)
        {
            value = normalized;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? ReadCertificateAuthority(string? file)
    {
        if (file is null)
            return null;

        try
        {
            var content = File.ReadAllText(file);
            if (string.IsNullOrWhiteSpace(content))
                throw new SchemaRegistryKmsException("Alibaba Cloud KMS CA file cannot be empty.");
            return content;
        }
        catch (Exception ex) when (ex is
            IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or System.Security.SecurityException)
        {
            throw new SchemaRegistryKmsException(
                "Alibaba Cloud KMS CA file could not be read.",
                ex);
        }
    }

    private static bool ContainsInvalidKeyCharacter(string keyId)
    {
        foreach (var character in keyId)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
                return true;
        }

        return false;
    }

    private static byte[] RequireMaterial(byte[]? material, string operation)
    {
        if (material is null || material.Length == 0)
        {
            throw new SchemaRegistryKmsException(
                $"Alibaba Cloud KMS {operation} failed. The service returned no key material.");
        }

        return material;
    }

    private static bool IsAliCloudFailure(Exception exception) => exception is
        TeaException
        or TeaRetryableException
        or TeaUnretryableException
        or HttpRequestException
        or IOException
        or TimeoutException
        or OperationCanceledException
        or FormatException
        or InvalidOperationException
        or ArgumentException;

    private readonly record struct OperationConfiguration(string KeyId, ClientCacheKey ClientConfiguration);

    private static string CredentialTypeName(CredentialKind credentialKind) => credentialKind switch
    {
        CredentialKind.Default => "default",
        CredentialKind.AccessKey => "access_key",
        CredentialKind.Sts => "sts",
        CredentialKind.RamRoleArn => "ram_role_arn",
        _ => throw new ArgumentOutOfRangeException(nameof(credentialKind))
    };

    private enum CredentialKind
    {
        Default,
        AccessKey,
        Sts,
        RamRoleArn
    }

    private sealed record ClientCacheKey(
        string RegionId,
        string? Endpoint,
        string? CertificateAuthorityFile,
        string CredentialType,
        string? AccessKeyId,
        string? AccessKeySecret,
        string? SecurityToken,
        string? RoleArn,
        string? RoleSessionName,
        int? RoleSessionExpiration,
        string? Policy,
        string? StsEndpoint,
        string? RoleExternalId)
    {
        internal AliCloudKmsClientConfiguration ToConfiguration() => new()
        {
            RegionId = RegionId,
            Endpoint = Endpoint,
            CertificateAuthority = ReadCertificateAuthority(CertificateAuthorityFile),
            CredentialType = CredentialType,
            AccessKeyId = AccessKeyId,
            AccessKeySecret = AccessKeySecret,
            SecurityToken = SecurityToken,
            RoleArn = RoleArn,
            RoleSessionName = RoleSessionName,
            RoleSessionExpiration = RoleSessionExpiration,
            Policy = Policy,
            StsEndpoint = StsEndpoint,
            RoleExternalId = RoleExternalId
        };

        public override string ToString() => nameof(ClientCacheKey);
    }

    private sealed class BoundedClientCache(
        IAliCloudKmsClientFactory clientFactory,
        int capacity,
        Action<int>? countChangedForTesting)
    {
        private readonly ConcurrentDictionary<ClientCacheKey, Lazy<IAliCloudKmsClient>> _entries = [];
        private readonly ClientCacheEntry?[] _slots = new ClientCacheEntry[capacity];
        private readonly object _missLock = new();
        private int _nextSlot;

        internal Lazy<IAliCloudKmsClient> GetOrAdd(ClientCacheKey configuration)
        {
            if (_entries.TryGetValue(configuration, out var client))
                return client;

            lock (_missLock)
            {
                if (_entries.TryGetValue(configuration, out client))
                    return client;

                var slot = GetInsertionSlot();
                var evicted = _slots[slot];
                if (evicted is not null)
                    _entries.TryRemove(KeyValuePair.Create(evicted.Configuration, evicted.Client));

                client = new Lazy<IAliCloudKmsClient>(
                    () => clientFactory.CreateClient(configuration.ToConfiguration()),
                    LazyThreadSafetyMode.ExecutionAndPublication);
                _entries[configuration] = client;
                _slots[slot] = new ClientCacheEntry(configuration, client);
                _nextSlot = (slot + 1) % capacity;
                countChangedForTesting?.Invoke(_entries.Count);
                return client;
            }
        }

        internal void Remove(ClientCacheKey configuration, Lazy<IAliCloudKmsClient> client)
        {
            lock (_missLock)
            {
                if (!_entries.TryRemove(KeyValuePair.Create(configuration, client)))
                    return;

                for (var slot = 0; slot < capacity; slot++)
                {
                    if (_slots[slot] is { } current && ReferenceEquals(current.Client, client))
                    {
                        _slots[slot] = null;
                        break;
                    }
                }

                countChangedForTesting?.Invoke(_entries.Count);
            }
        }

        private int GetInsertionSlot()
        {
            for (var offset = 0; offset < capacity; offset++)
            {
                var slot = (_nextSlot + offset) % capacity;
                if (_slots[slot] is null)
                    return slot;
            }

            return _nextSlot;
        }

        private sealed record ClientCacheEntry(
            ClientCacheKey Configuration,
            Lazy<IAliCloudKmsClient> Client);
    }
}
