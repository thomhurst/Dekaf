using System.Text.Json;

namespace Dekaf.SchemaRegistry.Kms.Vault;

/// <summary>
/// Encrypts and decrypts data encryption keys through a Vault Transit client.
/// </summary>
public interface IVaultTransitClient
{
    /// <summary>
    /// Encrypts plaintext with a Vault Transit key.
    /// </summary>
    ValueTask<byte[]> EncryptAsync(
        Uri vaultAddress,
        string mountPoint,
        string keyName,
        string? vaultNamespace,
        ReadOnlyMemory<byte> plaintext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts Vault Transit ciphertext.
    /// </summary>
    ValueTask<byte[]> DecryptAsync(
        Uri vaultAddress,
        string mountPoint,
        string keyName,
        string? vaultNamespace,
        ReadOnlyMemory<byte> ciphertext,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Wraps and unwraps Schema Registry data encryption keys with HashiCorp Vault Transit.
/// </summary>
public sealed class VaultKmsProvider : ISchemaRegistryKmsProvider
{
    /// <summary>
    /// Schema Registry KMS provider type.
    /// </summary>
    public const string DefaultType = "hcvault";

    private readonly IVaultTransitClient _client;
    private readonly Uri _vaultAddress;
    private readonly string? _vaultNamespace;

    /// <summary>
    /// Creates a provider using an injected Vault Transit client.
    /// </summary>
    /// <param name="client">Thread-safe Vault Transit client.</param>
    /// <param name="vaultAddress">Allowed Vault server address.</param>
    /// <param name="vaultNamespace">Optional Vault Enterprise namespace.</param>
    /// <param name="type">Schema Registry KMS provider type.</param>
    public VaultKmsProvider(
        IVaultTransitClient client,
        Uri vaultAddress,
        string? vaultNamespace = null,
        string type = DefaultType)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("KMS provider type cannot be null or whitespace.", nameof(type));

        _client = client;
        _vaultAddress = VaultTransitHttpClient.NormalizeAddress(vaultAddress);
        _vaultNamespace = VaultTransitHttpClient.NormalizeNamespace(vaultNamespace);
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
            throw new SchemaRegistryKmsException("Vault Transit wrap failed. Key material cannot be empty.");

        var key = ResolveKey(keyReference);
        try
        {
            var ciphertext = await _client.EncryptAsync(
                    _vaultAddress,
                    key.MountPoint,
                    key.KeyName,
                    _vaultNamespace,
                    keyMaterial,
                    cancellationToken)
                .ConfigureAwait(false);
            return RequireMaterial(ciphertext, "wrap");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SchemaRegistryKmsException)
        {
            throw;
        }
        catch (Exception ex) when (IsVaultFailure(ex))
        {
            throw new SchemaRegistryKmsException("Vault Transit wrap failed.", ex);
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
                "Vault Transit unwrap failed. Encrypted key material cannot be empty.");
        }

        var key = ResolveKey(keyReference);
        try
        {
            var plaintext = await _client.DecryptAsync(
                    _vaultAddress,
                    key.MountPoint,
                    key.KeyName,
                    _vaultNamespace,
                    encryptedKeyMaterial,
                    cancellationToken)
                .ConfigureAwait(false);
            return RequireMaterial(plaintext, "unwrap");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SchemaRegistryKmsException)
        {
            throw;
        }
        catch (Exception ex) when (IsVaultFailure(ex))
        {
            throw new SchemaRegistryKmsException("Vault Transit unwrap failed.", ex);
        }
    }

    private VaultKeyReference ResolveKey(SchemaRegistryKmsKeyReference keyReference)
    {
        ArgumentNullException.ThrowIfNull(keyReference);
        if (!string.Equals(Type, keyReference.KmsType, StringComparison.OrdinalIgnoreCase))
        {
            throw new SchemaRegistryKmsException(
                $"Vault KMS provider '{Type}' cannot resolve KMS type '{keyReference.KmsType}'.");
        }

        if (string.IsNullOrWhiteSpace(keyReference.KmsKeyId)
            || !Uri.TryCreate(keyReference.KmsKeyId, UriKind.Absolute, out var keyUri)
            || keyUri.Scheme is not ("http" or "https")
            || keyUri.UserInfo.Length != 0
            || keyUri.Query.Length != 0
            || keyUri.Fragment.Length != 0)
        {
            throw new SchemaRegistryKmsException(
                "Vault key identifier must be an absolute HTTP or HTTPS Vault key URL.");
        }

        var keyAddress = VaultTransitHttpClient.NormalizeAddress(keyUri);
        if (keyAddress != _vaultAddress)
        {
            throw new SchemaRegistryKmsException(
                "Vault key identifier authority does not match the configured Vault address.");
        }

        var escapedSegments = keyUri.AbsolutePath.Split('/');
        if (escapedSegments.Length < 4
            || escapedSegments[0].Length != 0
            || escapedSegments.AsSpan(1).Contains(string.Empty))
        {
            throw new SchemaRegistryKmsException(
                "Vault key identifier path must use '<mount>/keys/<key-name>'.");
        }

        var keyName = DecodePathSegment(escapedSegments[^1]);
        if (string.IsNullOrWhiteSpace(keyName) || keyName.Contains('/'))
            throw new SchemaRegistryKmsException("Vault Transit key name is invalid.");

        if (!string.Equals(DecodePathSegment(escapedSegments[^2]), "keys", StringComparison.Ordinal))
        {
            throw new SchemaRegistryKmsException(
                "Vault key identifier path must use '<mount>/keys/<key-name>'.");
        }

        var mountSegmentCount = escapedSegments.Length - 3;
        var mountSegments = new string[mountSegmentCount];
        for (var index = 0; index < mountSegments.Length; index++)
            mountSegments[index] = DecodePathSegment(escapedSegments[index + 1]);

        var mountPoint = string.Join('/', mountSegments);
        try
        {
            mountPoint = VaultTransitHttpClient.NormalizeMountPoint(mountPoint);
        }
        catch (ArgumentException ex)
        {
            throw new SchemaRegistryKmsException("Vault Transit mount point is invalid.", ex);
        }

        return new VaultKeyReference(mountPoint, keyName);
    }

    private static string DecodePathSegment(string segment)
    {
        var decoded = Uri.UnescapeDataString(segment);
        if (decoded.Contains('/'))
            throw new SchemaRegistryKmsException("Vault key identifier contains an invalid path segment.");

        return decoded;
    }

    private static byte[] RequireMaterial(byte[]? material, string operation)
    {
        if (material is null || material.Length == 0)
        {
            throw new SchemaRegistryKmsException(
                $"Vault Transit {operation} failed. The service returned no key material.");
        }

        return material;
    }

    private static bool IsVaultFailure(Exception exception) => exception is
        HttpRequestException
        or JsonException
        or FormatException
        or InvalidOperationException
        or ArgumentException
        or OperationCanceledException;

    private readonly record struct VaultKeyReference(string MountPoint, string KeyName);
}
