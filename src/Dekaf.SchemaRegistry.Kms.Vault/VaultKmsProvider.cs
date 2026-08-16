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

    /// <summary>
    /// URI prefix for Vault key references.
    /// </summary>
    public const string KeyUriPrefix = "hcvault://";

    /// <summary>
    /// Default Vault Transit secrets-engine mount point.
    /// </summary>
    public const string DefaultMountPoint = "transit";

    private readonly IVaultTransitClient _client;
    private readonly string _mountPoint;
    private readonly string? _vaultNamespace;

    /// <summary>
    /// Creates a provider using an injected Vault Transit client.
    /// </summary>
    /// <param name="client">Thread-safe Vault Transit client.</param>
    /// <param name="mountPoint">Transit secrets-engine mount point.</param>
    /// <param name="vaultNamespace">Optional Vault Enterprise namespace.</param>
    /// <param name="type">Schema Registry KMS provider type.</param>
    public VaultKmsProvider(
        IVaultTransitClient client,
        string mountPoint = DefaultMountPoint,
        string? vaultNamespace = null,
        string type = DefaultType)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("KMS provider type cannot be null or whitespace.", nameof(type));

        _client = client;
        _mountPoint = VaultTransitHttpClient.NormalizeMountPoint(mountPoint);
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
                    key.VaultAddress,
                    _mountPoint,
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
                    key.VaultAddress,
                    _mountPoint,
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

        var keyId = keyReference.KmsKeyId;
        if (string.IsNullOrWhiteSpace(keyId)
            || !keyId.StartsWith(KeyUriPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new SchemaRegistryKmsException(
                $"Vault key identifier must start with '{KeyUriPrefix}'.");
        }

        var absoluteKeyUrl = keyId[KeyUriPrefix.Length..];
        if (!Uri.TryCreate(absoluteKeyUrl, UriKind.Absolute, out var keyUri)
            || keyUri.Scheme is not ("http" or "https")
            || keyUri.UserInfo.Length != 0
            || keyUri.Query.Length != 0
            || keyUri.Fragment.Length != 0)
        {
            throw new SchemaRegistryKmsException(
                "Vault key identifier must contain an absolute HTTP or HTTPS Vault key URL.");
        }

        var segments = keyUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 1)
        {
            throw new SchemaRegistryKmsException(
                "Vault key URL must contain exactly one path segment naming the Transit key.");
        }

        var keyName = Uri.UnescapeDataString(segments[0]);
        if (string.IsNullOrWhiteSpace(keyName) || keyName.Contains('/'))
            throw new SchemaRegistryKmsException("Vault Transit key name is invalid.");

        var vaultAddress = new Uri(keyUri.GetLeftPart(UriPartial.Authority) + "/", UriKind.Absolute);
        return new VaultKeyReference(vaultAddress, keyName);
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

    private readonly record struct VaultKeyReference(Uri VaultAddress, string KeyName);
}
