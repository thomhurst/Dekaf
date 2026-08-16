using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Amazon;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Amazon.Runtime;

namespace Dekaf.SchemaRegistry.Kms.Aws;

/// <summary>
/// Wraps and unwraps Schema Registry data encryption keys with AWS Key Management Service.
/// </summary>
/// <remarks>
/// AWS SDK clients are thread-safe. Reuse one provider for all operations that share a client configuration.
/// </remarks>
public sealed class AwsKmsProvider : ISchemaRegistryKmsProvider, IDisposable
{
    /// <summary>
    /// Schema Registry KMS provider type.
    /// </summary>
    public const string DefaultType = "aws-kms";

    /// <summary>
    /// Confluent-compatible AWS KMS key URI prefix.
    /// </summary>
    public const string KeyUriPrefix = "aws-kms://";

    private const int MaximumPlaintextLength = 4096;

    private readonly IAmazonKeyManagementService _client;
    private readonly bool _ownsClient;
    private int _disposed;

    /// <summary>
    /// Creates a provider using the AWS SDK default credential and region provider chains.
    /// </summary>
    public AwsKmsProvider()
        : this(new AmazonKeyManagementServiceClient(), ownsClient: true)
    {
    }

    /// <summary>
    /// Creates a provider using the AWS SDK default credential provider chain in <paramref name="region" />.
    /// </summary>
    /// <param name="region">AWS region containing the KMS key.</param>
    public AwsKmsProvider(RegionEndpoint region)
        : this(new AmazonKeyManagementServiceClient(region ?? throw new ArgumentNullException(nameof(region))), ownsClient: true)
    {
    }

    /// <summary>
    /// Creates a provider using the AWS SDK default credential provider chain and client configuration.
    /// </summary>
    /// <param name="config">AWS KMS client configuration, including region or custom service endpoint.</param>
    public AwsKmsProvider(AmazonKeyManagementServiceConfig config)
        : this(new AmazonKeyManagementServiceClient(config ?? throw new ArgumentNullException(nameof(config))), ownsClient: true)
    {
    }

    /// <summary>
    /// Creates a provider using an existing AWS KMS client.
    /// </summary>
    /// <param name="client">Thread-safe AWS KMS client.</param>
    /// <param name="ownsClient">Whether disposing this provider also disposes <paramref name="client" />.</param>
    public AwsKmsProvider(IAmazonKeyManagementService client, bool ownsClient = false)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _ownsClient = ownsClient;
    }

    /// <inheritdoc />
    public string Type => DefaultType;

    /// <inheritdoc />
    public async ValueTask<byte[]> WrapKeyAsync(
        ReadOnlyMemory<byte> keyMaterial,
        SchemaRegistryKmsKeyReference keyReference,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        if (keyMaterial.IsEmpty)
            throw new SchemaRegistryKmsException("AWS KMS wrap failed. Key material cannot be empty.");

        if (keyMaterial.Length > MaximumPlaintextLength)
        {
            throw new SchemaRegistryKmsException(
                $"AWS KMS wrap failed. Key material cannot exceed {MaximumPlaintextLength} bytes.");
        }

        var keyId = ResolveKeyId(keyReference);
        using var plaintext = CreateInputStream(keyMaterial, out var temporaryBuffer);
        try
        {
            var response = await _client.EncryptAsync(new EncryptRequest
            {
                KeyId = keyId,
                Plaintext = plaintext
            }, cancellationToken).ConfigureAwait(false);

            return CopyResponse(response.CiphertextBlob, "wrap", clearSource: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is AmazonServiceException or AmazonClientException)
        {
            throw new SchemaRegistryKmsException("AWS KMS wrap failed.", ex);
        }
        finally
        {
            ClearTemporaryBuffer(temporaryBuffer);
        }
    }

    /// <inheritdoc />
    public async ValueTask<byte[]> UnwrapKeyAsync(
        ReadOnlyMemory<byte> encryptedKeyMaterial,
        SchemaRegistryKmsKeyReference keyReference,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        if (encryptedKeyMaterial.IsEmpty)
            throw new SchemaRegistryKmsException("AWS KMS unwrap failed. Encrypted key material cannot be empty.");

        var keyId = ResolveKeyId(keyReference);
        using var ciphertext = CreateInputStream(encryptedKeyMaterial, out var temporaryBuffer);
        try
        {
            var response = await _client.DecryptAsync(new DecryptRequest
            {
                KeyId = keyId,
                CiphertextBlob = ciphertext
            }, cancellationToken).ConfigureAwait(false);

            return CopyResponse(response.Plaintext, "unwrap", clearSource: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is AmazonServiceException or AmazonClientException)
        {
            throw new SchemaRegistryKmsException("AWS KMS unwrap failed.", ex);
        }
        finally
        {
            ClearTemporaryBuffer(temporaryBuffer);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0 || !_ownsClient)
            return;

        _client.Dispose();
    }

    private static string ResolveKeyId(SchemaRegistryKmsKeyReference keyReference)
    {
        ArgumentNullException.ThrowIfNull(keyReference);
        if (!string.Equals(keyReference.KmsType, DefaultType, StringComparison.OrdinalIgnoreCase))
        {
            throw new SchemaRegistryKmsException(
                $"AWS KMS provider cannot resolve KMS type '{keyReference.KmsType}'.");
        }

        var keyId = keyReference.KmsKeyId;
        if (string.IsNullOrWhiteSpace(keyId))
            throw new SchemaRegistryKmsException("AWS KMS key identifier cannot be null or whitespace.");

        if (keyId.StartsWith(KeyUriPrefix, StringComparison.OrdinalIgnoreCase))
            keyId = keyId[KeyUriPrefix.Length..];

        if (string.IsNullOrWhiteSpace(keyId))
            throw new SchemaRegistryKmsException("AWS KMS key identifier cannot be null or whitespace.");

        return keyId;
    }

    private static MemoryStream CreateInputStream(ReadOnlyMemory<byte> source, out byte[]? temporaryBuffer)
    {
        if (MemoryMarshal.TryGetArray(source, out var segment) && segment.Array is not null)
        {
            temporaryBuffer = null;
            return new MemoryStream(segment.Array, segment.Offset, segment.Count, writable: false, publiclyVisible: false);
        }

        temporaryBuffer = source.ToArray();
        return new MemoryStream(temporaryBuffer, writable: false);
    }

    private static byte[] CopyResponse(MemoryStream? stream, string operation, bool clearSource)
    {
        if (stream is null)
            throw new SchemaRegistryKmsException($"AWS KMS {operation} failed. The service returned no key material.");

        using (stream)
        {
            try
            {
                if (stream.Length == 0)
                {
                    throw new SchemaRegistryKmsException(
                        $"AWS KMS {operation} failed. The service returned empty key material.");
                }

                return stream.ToArray();
            }
            finally
            {
                if (clearSource && stream.TryGetBuffer(out var buffer))
                    CryptographicOperations.ZeroMemory(buffer.AsSpan());
            }
        }
    }

    private static void ClearTemporaryBuffer(byte[]? temporaryBuffer)
    {
        if (temporaryBuffer is not null)
            CryptographicOperations.ZeroMemory(temporaryBuffer);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed != 0, this);
}
