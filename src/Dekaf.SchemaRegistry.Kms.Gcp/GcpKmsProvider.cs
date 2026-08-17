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
            var request = new EncryptRequest
            {
                Name = keyName.ToString(),
                Plaintext = UnsafeByteOperations.UnsafeWrap(keyMaterial),
                PlaintextCrc32C = GcpCrc32C.Compute(keyMaterial.Span)
            };
            var response = await _client
                .EncryptAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (!response.VerifiedPlaintextCrc32C)
            {
                ClearMaterial(response.Ciphertext);
                throw new SchemaRegistryKmsException(
                    "Google Cloud KMS wrap failed. The service could not verify request integrity.");
            }

            if (!IsVersionOfKey(response.Name, request.Name))
            {
                ClearMaterial(response.Ciphertext);
                throw new SchemaRegistryKmsException(
                    "Google Cloud KMS wrap failed. The service used an unexpected key version.");
            }

            return CopyVerifyAndClearMaterial(
                response.Ciphertext,
                response.CiphertextCrc32C,
                "wrap");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RpcException ex) when (
            cancellationToken.IsCancellationRequested && ex.StatusCode == StatusCode.Cancelled)
        {
            throw new OperationCanceledException("Google Cloud KMS wrap was canceled.", cancellationToken);
        }
        catch (RpcException)
        {
            throw new SchemaRegistryKmsException("Google Cloud KMS wrap failed.");
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
            var request = new DecryptRequest
            {
                Name = keyName.ToString(),
                Ciphertext = UnsafeByteOperations.UnsafeWrap(encryptedKeyMaterial),
                CiphertextCrc32C = GcpCrc32C.Compute(encryptedKeyMaterial.Span)
            };
            var response = await _client
                .DecryptAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return CopyVerifyAndClearMaterial(
                response.Plaintext,
                response.PlaintextCrc32C,
                "unwrap");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RpcException ex) when (
            cancellationToken.IsCancellationRequested && ex.StatusCode == StatusCode.Cancelled)
        {
            throw new OperationCanceledException("Google Cloud KMS unwrap was canceled.", cancellationToken);
        }
        catch (RpcException)
        {
            throw new SchemaRegistryKmsException("Google Cloud KMS unwrap failed.");
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

    private static byte[] CopyVerifyAndClearMaterial(
        ByteString material,
        long? expectedCrc32C,
        string operation)
    {
        try
        {
            if (material.IsEmpty)
            {
                throw new SchemaRegistryKmsException(
                    $"Google Cloud KMS {operation} failed. The service returned no key material.");
            }

            if (expectedCrc32C is null || GcpCrc32C.Compute(material.Span) != expectedCrc32C)
            {
                throw new SchemaRegistryKmsException(
                    $"Google Cloud KMS {operation} failed. Response integrity validation failed.");
            }

            return material.ToByteArray();
        }
        finally
        {
            ClearMaterial(material);
        }
    }

    private static bool IsVersionOfKey(string versionName, string keyName)
    {
        var prefixLength = keyName.Length + "/cryptoKeyVersions/".Length;
        return versionName.Length > prefixLength
            && versionName.StartsWith(keyName, StringComparison.Ordinal)
            && versionName.AsSpan(keyName.Length).StartsWith("/cryptoKeyVersions/", StringComparison.Ordinal);
    }

    private static void ClearMaterial(ByteString material)
    {
        if (MemoryMarshal.TryGetArray(material.Memory, out var segment)
            && segment.Array is not null)
        {
            CryptographicOperations.ZeroMemory(segment.Array.AsSpan(segment.Offset, segment.Count));
        }
    }

    private static class GcpCrc32C
    {
        private const uint ReversedCastagnoliPolynomial = 0x82F63B78;
        private static readonly uint[] Table = CreateTable();

        internal static long Compute(ReadOnlySpan<byte> data)
        {
            var crc = uint.MaxValue;
            foreach (var value in data)
                crc = Table[(crc ^ value) & 0xff] ^ (crc >> 8);

            return crc ^ uint.MaxValue;
        }

        private static uint[] CreateTable()
        {
            var table = new uint[256];
            for (var index = 0; index < table.Length; index++)
            {
                var crc = (uint)index;
                for (var bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 1) == 0
                        ? crc >> 1
                        : (crc >> 1) ^ ReversedCastagnoliPolynomial;
                }

                table[index] = crc;
            }

            return table;
        }
    }
}
