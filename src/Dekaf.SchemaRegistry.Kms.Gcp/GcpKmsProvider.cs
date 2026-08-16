using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Grpc.Core;

namespace Dekaf.SchemaRegistry.Kms.Gcp;

/// <summary>
/// Wraps and unwraps Schema Registry data encryption keys with Google Cloud KMS.
/// </summary>
public sealed class GcpKmsProvider : ISchemaRegistryKmsProvider
{
    /// <summary>
    /// Schema Registry KMS provider type.
    /// </summary>
    public const string DefaultType = "gcp-kms";

    /// <summary>
    /// Optional URI prefix for Google Cloud KMS key resource names.
    /// </summary>
    public const string KeyUriPrefix = "gcp-kms://";

    private readonly KeyManagementServiceClient _client;

    /// <summary>
    /// Creates a provider using Google Application Default Credentials and the default endpoint.
    /// </summary>
    public GcpKmsProvider()
        : this(KeyManagementServiceClient.Create())
    {
    }

    /// <summary>
    /// Creates a provider using a caller-supplied Google Cloud KMS client.
    /// </summary>
    /// <param name="client">Shared Google Cloud KMS client.</param>
    /// <param name="type">Schema Registry KMS provider type.</param>
    public GcpKmsProvider(
        KeyManagementServiceClient client,
        string type = DefaultType)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("KMS provider type cannot be null or whitespace.", nameof(type));

        _client = client;
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
            throw new SchemaRegistryKmsException("Google Cloud KMS wrap failed. Key material cannot be empty.");

        var keyName = ResolveKeyName(keyReference);
        try
        {
            var response = await _client
                .EncryptAsync(keyName, UnsafeByteOperations.UnsafeWrap(keyMaterial), cancellationToken)
                .ConfigureAwait(false);
            return CopyAndClearMaterial(response.Ciphertext, "wrap");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RpcException ex) when (
            cancellationToken.IsCancellationRequested && ex.StatusCode == StatusCode.Cancelled)
        {
            throw new OperationCanceledException("Google Cloud KMS wrap was canceled.", ex, cancellationToken);
        }
        catch (RpcException ex)
        {
            throw new SchemaRegistryKmsException("Google Cloud KMS wrap failed.", ex);
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
                "Google Cloud KMS unwrap failed. Encrypted key material cannot be empty.");
        }

        var keyName = ResolveKeyName(keyReference);
        try
        {
            var response = await _client
                .DecryptAsync(keyName, UnsafeByteOperations.UnsafeWrap(encryptedKeyMaterial), cancellationToken)
                .ConfigureAwait(false);
            return CopyAndClearMaterial(response.Plaintext, "unwrap");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RpcException ex) when (
            cancellationToken.IsCancellationRequested && ex.StatusCode == StatusCode.Cancelled)
        {
            throw new OperationCanceledException("Google Cloud KMS unwrap was canceled.", ex, cancellationToken);
        }
        catch (RpcException ex)
        {
            throw new SchemaRegistryKmsException("Google Cloud KMS unwrap failed.", ex);
        }
    }

    private CryptoKeyName ResolveKeyName(SchemaRegistryKmsKeyReference keyReference)
    {
        ArgumentNullException.ThrowIfNull(keyReference);
        if (!string.Equals(Type, keyReference.KmsType, StringComparison.OrdinalIgnoreCase))
        {
            throw new SchemaRegistryKmsException(
                $"Google Cloud KMS provider '{Type}' cannot resolve KMS type '{keyReference.KmsType}'.");
        }

        var keyId = keyReference.KmsKeyId;
        if (string.IsNullOrWhiteSpace(keyId))
            throw new SchemaRegistryKmsException("Google Cloud KMS key identifier cannot be null or whitespace.");

        if (keyId.StartsWith(KeyUriPrefix, StringComparison.OrdinalIgnoreCase))
            keyId = keyId[KeyUriPrefix.Length..];

        if (!CryptoKeyName.TryParse(keyId, out var keyName))
        {
            throw new SchemaRegistryKmsException(
                "Google Cloud KMS key identifier must use "
                + "'projects/<project>/locations/<location>/keyRings/<key-ring>/cryptoKeys/<key>'.");
        }

        return keyName;
    }

    private static byte[] CopyAndClearMaterial(ByteString material, string operation)
    {
        if (material.IsEmpty)
        {
            throw new SchemaRegistryKmsException(
                $"Google Cloud KMS {operation} failed. The service returned no key material.");
        }

        try
        {
            return material.ToByteArray();
        }
        finally
        {
            if (MemoryMarshal.TryGetArray(material.Memory, out var segment)
                && segment.Array is not null)
            {
                CryptographicOperations.ZeroMemory(segment.Array.AsSpan(segment.Offset, segment.Count));
            }
        }
    }
}
