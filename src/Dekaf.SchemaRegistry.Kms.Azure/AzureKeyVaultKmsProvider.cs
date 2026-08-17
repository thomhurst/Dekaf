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
    internal const int EmbeddedVersionClientCapacity = 64;
    private const byte HeaderSeparator = (byte)':';

    private static ReadOnlySpan<byte> VersionHeaderPrefix => "azure:v1:"u8;

    private readonly IAzureKeyVaultCryptographyClientFactory _clientFactory;
    private readonly ConcurrentDictionary<Uri, Lazy<CryptographyClient>> _clients = [];
    private readonly ConcurrentDictionary<Uri, Lazy<CryptographyClient>> _embeddedVersionClients = [];
    private readonly VersionClientEntry?[] _embeddedVersionClientSlots =
        new VersionClientEntry[EmbeddedVersionClientCapacity];

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
    /// <param name="clientFactory">Factory used once per distinct key identifier.</param>
    /// <param name="type">Schema Registry KMS provider type.</param>
    public AzureKeyVaultKmsProvider(
        IAzureKeyVaultCryptographyClientFactory clientFactory,
        string type = DefaultType)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("KMS provider type cannot be null or whitespace.", nameof(type));

        _clientFactory = clientFactory;
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
        var client = GetClientEntry(key.KeyId);
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
            _clients.TryRemove(KeyValuePair.Create(key.KeyId, client));
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
        var client = hasEmbeddedVersion
            ? GetEmbeddedVersionClientEntry(key.KeyId, version)
            : GetClientEntry(key.KeyId);
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
            if (hasEmbeddedVersion)
                RemoveEmbeddedVersionClient(key.KeyId, version, client);
            else
                _clients.TryRemove(KeyValuePair.Create(key.KeyId, client));

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
                RemoveEmbeddedVersionClient(key.KeyId, version, client);
        }
    }

    private static int VersionHeaderLength => VersionHeaderPrefix.Length + AzureKeyVersionLength + 1;

    private Lazy<CryptographyClient> GetClientEntry(Uri keyId) => _clients.GetOrAdd(
        keyId,
        static (uri, factory) => new Lazy<CryptographyClient>(
            () => factory.CreateClient(uri),
            LazyThreadSafetyMode.ExecutionAndPublication),
        _clientFactory);

    private Lazy<CryptographyClient> GetEmbeddedVersionClientEntry(Uri keyId, string version)
    {
        var client = _embeddedVersionClients.GetOrAdd(
            keyId,
            static (uri, factory) => new Lazy<CryptographyClient>(
                () => factory.CreateClient(uri),
                LazyThreadSafetyMode.ExecutionAndPublication),
            _clientFactory);
        TrackEmbeddedVersionClient(keyId, version, client);
        return client;
    }

    private void TrackEmbeddedVersionClient(
        Uri keyId,
        string version,
        Lazy<CryptographyClient> client)
    {
        var slot = GetEmbeddedVersionClientSlot(version);
        VersionClientEntry? candidate = null;
        while (true)
        {
            var current = Volatile.Read(ref _embeddedVersionClientSlots[slot]);
            if (current is not null && ReferenceEquals(current.Client, client))
                return;

            candidate ??= new VersionClientEntry(keyId, client);
            var published = Interlocked.CompareExchange(
                ref _embeddedVersionClientSlots[slot],
                candidate,
                current);
            if (ReferenceEquals(published, current))
            {
                if (current is not null)
                {
                    _embeddedVersionClients.TryRemove(
                        KeyValuePair.Create(current.KeyId, current.Client));
                }

                return;
            }
        }
    }

    private void RemoveEmbeddedVersionClient(
        Uri keyId,
        string version,
        Lazy<CryptographyClient> client)
    {
        _embeddedVersionClients.TryRemove(KeyValuePair.Create(keyId, client));
        var slot = GetEmbeddedVersionClientSlot(version);
        var current = Volatile.Read(ref _embeddedVersionClientSlots[slot]);
        if (current is not null && ReferenceEquals(current.Client, client))
            Interlocked.CompareExchange(ref _embeddedVersionClientSlots[slot], null, current);
    }

    private static int GetEmbeddedVersionClientSlot(string version) =>
        (int)((uint)StringComparer.Ordinal.GetHashCode(version) & (EmbeddedVersionClientCapacity - 1));

    internal int EmbeddedVersionClientCount => _embeddedVersionClients.Count;

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
        if (value is null || !value.StartsWith(httpsPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var remainder = value.AsSpan(httpsPrefix.Length);
        var pathStart = remainder.IndexOf('/');
        if (pathStart <= 0)
            return false;

        return IsTrustedVaultHost(remainder[..pathStart]);
    }

    private static bool IsTrustedVaultHost(ReadOnlySpan<char> host) =>
        HasVaultSuffix(host, ".vault.azure.net")
        || HasVaultSuffix(host, ".vault.azure.cn")
        || HasVaultSuffix(host, ".vault.usgovcloudapi.net");

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
        or ArgumentException;

    private static bool ShouldEvictFailedVersionClient(Exception exception) => exception switch
    {
        RequestFailedException { Status: 0 or 408 or 429 or >= 500 } => false,
        AuthenticationFailedException or CredentialUnavailableException => false,
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

    private sealed record VersionClientEntry(Uri KeyId, Lazy<CryptographyClient> Client);

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
