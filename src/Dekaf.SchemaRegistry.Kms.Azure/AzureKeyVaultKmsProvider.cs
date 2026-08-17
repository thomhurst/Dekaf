using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace Dekaf.SchemaRegistry.Kms.Azure;

/// <summary>
/// Creates Azure Key Vault cryptography clients for KMS key identifiers.
/// </summary>
public interface IAzureKeyVaultCryptographyClientFactory
{
    /// <summary>
    /// Creates a client for an Azure Key Vault key identifier, optionally including a key version.
    /// </summary>
    /// <param name="keyId">Absolute Azure Key Vault key identifier.</param>
    CryptographyClient CreateClient(Uri keyId);
}

/// <summary>
/// Wraps and unwraps Schema Registry data encryption keys with Azure Key Vault Keys.
/// </summary>
public sealed class AzureKeyVaultKmsProvider : ISchemaRegistryKmsProvider
{
    /// <summary>
    /// Default Schema Registry KMS provider type.
    /// </summary>
    public const string DefaultType = "azure-kv";

    /// <summary>
    /// Confluent Schema Registry KMS provider type.
    /// </summary>
    public const string ConfluentType = "azure-kms";

    /// <summary>
    /// URI prefix accepted for the default provider type.
    /// </summary>
    public const string KeyUriPrefix = "azure-kv://";

    /// <summary>
    /// Confluent-compatible URI prefix.
    /// </summary>
    public const string ConfluentKeyUriPrefix = "azure-kms://";

    /// <summary>
    /// KEK property that embeds the exact Azure key version in wrapped key material.
    /// </summary>
    public const string SaveVersionProperty = "encrypt.azure.key.version.save";

    private const int AzureKeyVersionLength = 32;
    internal const int ClientCacheCapacity = 64;
    internal const int EmbeddedVersionClientCapacity = ClientCacheCapacity;
    private const byte HeaderSeparator = (byte)':';

    private static ReadOnlySpan<byte> VersionHeaderPrefix => "azure:v1:"u8;

    private readonly BoundedClientCache _clients;
    private readonly BoundedClientCache _embeddedVersionClients;

    /// <summary>
    /// Creates a provider using <see cref="DefaultAzureCredential" />.
    /// </summary>
    public AzureKeyVaultKmsProvider()
        : this(new DefaultAzureCredential())
    {
    }

    /// <summary>
    /// Creates a provider using an Azure token credential.
    /// </summary>
    /// <param name="credential">Credential used to authenticate with Azure Key Vault.</param>
    /// <param name="clientOptions">Optional Azure Key Vault cryptography client options.</param>
    /// <param name="type">Schema Registry KMS provider type.</param>
    public AzureKeyVaultKmsProvider(
        TokenCredential credential,
        CryptographyClientOptions? clientOptions = null,
        string type = DefaultType)
        : this(new TokenCredentialClientFactory(credential, clientOptions), type)
    {
    }

    /// <summary>
    /// Creates a provider using a custom Azure Key Vault client factory.
    /// </summary>
    /// <param name="clientFactory">Factory used to create clients for key identifiers.</param>
    /// <param name="type">Schema Registry KMS provider type.</param>
    public AzureKeyVaultKmsProvider(
        IAzureKeyVaultCryptographyClientFactory clientFactory,
        string type = DefaultType)
        : this(clientFactory, type, clientCacheCountChangedForTesting: null)
    {
    }

    internal AzureKeyVaultKmsProvider(
        IAzureKeyVaultCryptographyClientFactory clientFactory,
        string type,
        Action<int>? clientCacheCountChangedForTesting)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("KMS provider type cannot be null or whitespace.", nameof(type));

        _clients = new BoundedClientCache(
            clientFactory,
            ClientCacheCapacity,
            clientCacheCountChangedForTesting);
        _embeddedVersionClients = new BoundedClientCache(clientFactory, EmbeddedVersionClientCapacity);
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
            throw new SchemaRegistryKmsException("Azure Key Vault wrap failed. Key material cannot be empty.");

        var key = ResolveKey(keyReference);
        var plaintext = GetInputArray(keyMaterial, out var temporaryPlaintext);
        var client = _clients.GetOrAdd(key.KeyId);
        try
        {
            var result = await client.Value
                .WrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, plaintext, cancellationToken)
                .ConfigureAwait(false);
            var ciphertext = RequireMaterial(result.EncryptedKey, "wrap");

            if (!ShouldSaveVersion(keyReference.KmsProps))
                return ciphertext;

            var resolvedKey = ParseKeyUri(result.KeyId, "Azure Key Vault response key identifier");
            if (!IsValidVersion(resolvedKey.Version))
            {
                throw new SchemaRegistryKmsException(
                    "Azure Key Vault wrap failed. The service did not return a valid key version.");
            }

            return AddVersionHeader(ciphertext, resolvedKey.Version!);
        }
        catch (Exception ex) when (!client.IsValueCreated)
        {
            _clients.Remove(key.KeyId, client);
            if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                throw;

            if (IsAzureFailure(ex))
                throw new SchemaRegistryKmsException("Azure Key Vault wrap failed.");

            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsAzureFailure(ex))
        {
            throw new SchemaRegistryKmsException("Azure Key Vault wrap failed.");
        }
        finally
        {
            ClearTemporaryBuffer(temporaryPlaintext);
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
            throw new SchemaRegistryKmsException("Azure Key Vault unwrap failed. Encrypted key material cannot be empty.");

        var key = ResolveKey(keyReference);
        var wrappedMaterial = encryptedKeyMaterial;
        var hasEmbeddedVersion = TryReadVersionHeader(encryptedKeyMaterial.Span, out var version);
        if (hasEmbeddedVersion)
        {
            key = key.WithVersion(version);
            wrappedMaterial = encryptedKeyMaterial[VersionHeaderLength..];
            if (wrappedMaterial.IsEmpty)
                throw new SchemaRegistryKmsException("Azure Key Vault ciphertext contains no wrapped key material.");
        }

        var ciphertext = GetInputArray(wrappedMaterial, out var temporaryCiphertext);
        var clientCache = hasEmbeddedVersion ? _embeddedVersionClients : _clients;
        var client = clientCache.GetOrAdd(key.KeyId);
        var evictClient = false;
        try
        {
            var result = await client.Value
                .UnwrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, ciphertext, cancellationToken)
                .ConfigureAwait(false);
            var plaintext = RequireMaterial(result.Key, "unwrap");
            return plaintext;
        }
        catch (Exception ex) when (!client.IsValueCreated)
        {
            clientCache.Remove(key.KeyId, client);

            if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                throw;

            if (IsAzureFailure(ex))
                throw new SchemaRegistryKmsException("Azure Key Vault unwrap failed.");

            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsAzureFailure(ex))
        {
            evictClient = ShouldEvictFailedVersionClient(ex);
            throw new SchemaRegistryKmsException("Azure Key Vault unwrap failed.");
        }
        finally
        {
            ClearTemporaryBuffer(temporaryCiphertext);
            if (hasEmbeddedVersion && evictClient)
                clientCache.Remove(key.KeyId, client);
        }
    }

    private static int VersionHeaderLength => VersionHeaderPrefix.Length + AzureKeyVersionLength + 1;

    internal int EmbeddedVersionClientCount => _embeddedVersionClients.Count;
    internal int ClientCount => _clients.Count;

    private KeyReference ResolveKey(SchemaRegistryKmsKeyReference keyReference)
    {
        ArgumentNullException.ThrowIfNull(keyReference);
        if (!string.Equals(Type, keyReference.KmsType, StringComparison.OrdinalIgnoreCase))
        {
            throw new SchemaRegistryKmsException(
                $"Azure Key Vault provider '{Type}' cannot resolve KMS type '{keyReference.KmsType}'.");
        }

        var keyId = keyReference.KmsKeyId;
        if (string.IsNullOrWhiteSpace(keyId))
            throw new SchemaRegistryKmsException("Azure Key Vault key identifier cannot be null or whitespace.");

        if (keyId.StartsWith(KeyUriPrefix, StringComparison.OrdinalIgnoreCase))
            keyId = keyId[KeyUriPrefix.Length..];
        else if (keyId.StartsWith(ConfluentKeyUriPrefix, StringComparison.OrdinalIgnoreCase))
            keyId = keyId[ConfluentKeyUriPrefix.Length..];

        return ParseKeyUri(keyId, "Azure Key Vault key identifier");
    }

    private static KeyReference ParseKeyUri(string? value, string description)
    {
        if (!HasTrustedVaultAuthority(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out var keyId)
            || !string.Equals(keyId.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || keyId.UserInfo.Length != 0
            || keyId.Query.Length != 0
            || keyId.Fragment.Length != 0)
        {
            throw new SchemaRegistryKmsException($"{description} is not a valid absolute key URI.");
        }

        var segments = keyId.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length is < 2 or > 3 || !string.Equals(segments[0], "keys", StringComparison.Ordinal))
        {
            throw new SchemaRegistryKmsException(
                $"{description} must use the path '/keys/<name>[/<version>]'.");
        }

        var vaultUri = new Uri(keyId.GetLeftPart(UriPartial.Authority), UriKind.Absolute);
        var versionlessKeyId = new Uri(vaultUri, $"keys/{segments[1]}");
        return new KeyReference(keyId, versionlessKeyId, segments.Length == 3 ? segments[2] : null);
    }

    private static bool HasTrustedVaultAuthority(string? value)
    {
        const string httpsPrefix = "https://";
        const string defaultPort = ":443";
        if (value is null || !value.StartsWith(httpsPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var remainder = value.AsSpan(httpsPrefix.Length);
        var pathStart = remainder.IndexOf('/');
        if (pathStart <= 0)
            return false;

        var authority = remainder[..pathStart];
        if (authority.EndsWith(defaultPort, StringComparison.Ordinal))
            authority = authority[..^defaultPort.Length];

        return IsTrustedVaultHost(authority);
    }

    private static bool IsTrustedVaultHost(ReadOnlySpan<char> host) =>
        HasVaultSuffix(host, ".vault.azure.net")
        || HasVaultSuffix(host, ".vault.azure.cn")
        || HasVaultSuffix(host, ".vault.usgovcloudapi.net")
        || HasVaultSuffix(host, ".managedhsm.azure.net")
        || HasVaultSuffix(host, ".managedhsm.azure.cn")
        || HasVaultSuffix(host, ".managedhsm.usgovcloudapi.net");

    private static bool HasVaultSuffix(ReadOnlySpan<char> host, ReadOnlySpan<char> suffix)
    {
        if (host.Length <= suffix.Length
            || !host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var vaultName = host[..^suffix.Length];
        for (var index = 0; index < vaultName.Length; index++)
        {
            var value = vaultName[index];
            if (value is not (>= 'a' and <= 'z')
                and not (>= 'A' and <= 'Z')
                and not (>= '0' and <= '9')
                and not '-')
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] GetInputArray(ReadOnlyMemory<byte> source, out byte[]? temporaryBuffer)
    {
        if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(source, out var segment)
            && segment.Array is not null
            && segment.Offset == 0
            && segment.Count == segment.Array.Length)
        {
            temporaryBuffer = null;
            return segment.Array;
        }

        temporaryBuffer = source.ToArray();
        return temporaryBuffer;
    }

    private static byte[] RequireMaterial(byte[]? material, string operation)
    {
        if (material is null || material.Length == 0)
        {
            throw new SchemaRegistryKmsException(
                $"Azure Key Vault {operation} failed. The service returned no key material.");
        }

        return material;
    }

    private static bool ShouldSaveVersion(IReadOnlyDictionary<string, string>? properties) =>
        properties is not null
        && properties.TryGetValue(SaveVersionProperty, out var value)
        && bool.TryParse(value, out var enabled)
        && enabled;

    private static byte[] AddVersionHeader(byte[] ciphertext, string version)
    {
        var output = new byte[checked(VersionHeaderLength + ciphertext.Length)];
        VersionHeaderPrefix.CopyTo(output);
        Encoding.ASCII.GetBytes(version, output.AsSpan(VersionHeaderPrefix.Length, AzureKeyVersionLength));
        output[VersionHeaderLength - 1] = HeaderSeparator;
        ciphertext.CopyTo(output, VersionHeaderLength);
        return output;
    }

    private static bool TryReadVersionHeader(ReadOnlySpan<byte> ciphertext, out string version)
    {
        if (!ciphertext.StartsWith(VersionHeaderPrefix))
        {
            version = string.Empty;
            return false;
        }

        if (ciphertext.Length < VersionHeaderLength
            || ciphertext[VersionHeaderLength - 1] != HeaderSeparator)
        {
            throw new SchemaRegistryKmsException("Azure Key Vault ciphertext contains an invalid version header.");
        }

        var versionBytes = ciphertext.Slice(VersionHeaderPrefix.Length, AzureKeyVersionLength);
        if (!IsValidVersion(versionBytes))
            throw new SchemaRegistryKmsException("Azure Key Vault ciphertext contains an invalid embedded key version.");

        version = Encoding.ASCII.GetString(versionBytes);
        return true;
    }

    private static bool IsValidVersion(string? version)
    {
        if (version is null || version.Length != AzureKeyVersionLength)
            return false;

        foreach (var value in version)
        {
            if (value is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')
                and not (>= 'A' and <= 'F'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidVersion(ReadOnlySpan<byte> version)
    {
        if (version.Length != AzureKeyVersionLength)
            return false;

        foreach (var value in version)
        {
            if (value is not (>= (byte)'0' and <= (byte)'9')
                and not (>= (byte)'a' and <= (byte)'f')
                and not (>= (byte)'A' and <= (byte)'F'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAzureFailure(Exception exception) => exception is
        RequestFailedException
        or AuthenticationFailedException
        or CredentialUnavailableException
        or CryptographicException
        or ArgumentException
        or OperationCanceledException;

    private static bool ShouldEvictFailedVersionClient(Exception exception) => exception switch
    {
        RequestFailedException { Status: 0 or 408 or 429 or >= 500 } => false,
        AuthenticationFailedException or CredentialUnavailableException or OperationCanceledException => false,
        _ => true
    };

    private static void ClearTemporaryBuffer(byte[]? temporaryBuffer)
    {
        if (temporaryBuffer is not null)
            CryptographicOperations.ZeroMemory(temporaryBuffer);
    }

    private readonly record struct KeyReference(Uri KeyId, Uri VersionlessKeyId, string? Version)
    {
        internal KeyReference WithVersion(string version) => new(
            new Uri($"{VersionlessKeyId.AbsoluteUri.TrimEnd('/')}/{version}", UriKind.Absolute),
            VersionlessKeyId,
            version);
    }

    private sealed class BoundedClientCache(
        IAzureKeyVaultCryptographyClientFactory clientFactory,
        int capacity,
        Action<int>? countChangedForTesting = null)
    {
        private readonly ConcurrentDictionary<Uri, Lazy<CryptographyClient>> _entries = [];
        private readonly ClientCacheEntry?[] _slots = new ClientCacheEntry[capacity];
        private readonly object _missLock = new();
        private int _nextSlot;

        internal int Count => _entries.Count;

        internal Lazy<CryptographyClient> GetOrAdd(Uri keyId)
        {
            if (_entries.TryGetValue(keyId, out var client))
                return client;

            lock (_missLock)
            {
                if (_entries.TryGetValue(keyId, out client))
                    return client;

                var slot = GetInsertionSlot();
                var evicted = _slots[slot];
                if (evicted is not null)
                    _entries.TryRemove(KeyValuePair.Create(evicted.KeyId, evicted.Client));

                client = CreateClientEntry(keyId, clientFactory);
                _entries[keyId] = client;
                _slots[slot] = new ClientCacheEntry(keyId, client);
                _nextSlot = (slot + 1) % capacity;
                countChangedForTesting?.Invoke(_entries.Count);
                return client;
            }
        }

        private static Lazy<CryptographyClient> CreateClientEntry(
            Uri keyId,
            IAzureKeyVaultCryptographyClientFactory factory) => new(
                () => factory.CreateClient(keyId),
                LazyThreadSafetyMode.ExecutionAndPublication);

        internal void Remove(Uri keyId, Lazy<CryptographyClient> client)
        {
            lock (_missLock)
            {
                if (!_entries.TryRemove(KeyValuePair.Create(keyId, client)))
                    return;

                for (var slot = 0; slot < capacity; slot++)
                {
                    var current = _slots[slot];
                    if (current is not null && ReferenceEquals(current.Client, client))
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

        private sealed record ClientCacheEntry(Uri KeyId, Lazy<CryptographyClient> Client);
    }

    private sealed class TokenCredentialClientFactory(
        TokenCredential credential,
        CryptographyClientOptions? options) : IAzureKeyVaultCryptographyClientFactory
    {
        private readonly TokenCredential _credential = credential ?? throw new ArgumentNullException(nameof(credential));

        public CryptographyClient CreateClient(Uri keyId) => options is null
            ? new CryptographyClient(keyId, _credential)
            : new CryptographyClient(keyId, _credential, options);
    }
}
