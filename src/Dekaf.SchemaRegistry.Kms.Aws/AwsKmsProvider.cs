using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
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
    private const int DisposeDrainTimeoutMilliseconds = 100;
    private const int DisposedMask = int.MinValue;
    private const int ActiveOperationMask = int.MaxValue;

    private readonly IAmazonKeyManagementService _client;
    private readonly bool _ownsClient;
    private readonly CancellationTokenSource _disposeCancellation = new();
    private int _operationState;
    private int _cancellationFinished;
    private int _clientDisposed;
    private int _cancellationDisposed;

    /// <summary>
    /// Creates a provider using the AWS SDK default credential and region provider chains.
    /// </summary>
    /// <param name="type">Schema Registry KMS provider type.</param>
    public AwsKmsProvider(string type = DefaultType)
        : this(CreateOwnedClient(type), ownsClient: true, type: type)
    {
    }

    /// <summary>
    /// Creates a provider using the AWS SDK default credential provider chain in <paramref name="region" />.
    /// </summary>
    /// <param name="region">AWS region containing the KMS key.</param>
    /// <param name="type">Schema Registry KMS provider type.</param>
    public AwsKmsProvider(RegionEndpoint region, string type = DefaultType)
        : this(
            CreateOwnedClient(region, type),
            ownsClient: true,
            type: type)
    {
    }

    /// <summary>
    /// Creates a provider using the AWS SDK default credential provider chain and client configuration.
    /// </summary>
    /// <param name="config">AWS KMS client configuration, including region or custom service endpoint.</param>
    /// <param name="type">Schema Registry KMS provider type.</param>
    public AwsKmsProvider(AmazonKeyManagementServiceConfig config, string type = DefaultType)
        : this(
            CreateOwnedClient(config, type),
            ownsClient: true,
            type: type)
    {
    }

    /// <summary>
    /// Creates a provider using an existing AWS KMS client.
    /// </summary>
    /// <param name="client">Thread-safe AWS KMS client.</param>
    /// <param name="ownsClient">Whether disposing this provider also disposes <paramref name="client" />.</param>
    /// <param name="type">Schema Registry KMS provider type.</param>
    public AwsKmsProvider(
        IAmazonKeyManagementService client,
        bool ownsClient = false,
        string type = DefaultType)
    {
        ArgumentNullException.ThrowIfNull(client);
        ValidateType(type);

        _client = client;
        _ownsClient = ownsClient;
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
        using var operation = EnterOperation();
        using var linkedCancellation = CreateLinkedCancellation(cancellationToken);
        var operationToken = linkedCancellation?.Token ?? _disposeCancellation.Token;
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
            }, operationToken).ConfigureAwait(false);

            return CopyResponse(response.CiphertextBlob, "wrap", clearSource: false);
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsAwsFailure(ex))
        {
            throw new SchemaRegistryKmsException("AWS KMS wrap failed.");
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
        using var operation = EnterOperation();
        using var linkedCancellation = CreateLinkedCancellation(cancellationToken);
        var operationToken = linkedCancellation?.Token ?? _disposeCancellation.Token;
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
            }, operationToken).ConfigureAwait(false);

            return CopyResponse(response.Plaintext, "unwrap", clearSource: true);
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsAwsFailure(ex))
        {
            throw new SchemaRegistryKmsException("AWS KMS unwrap failed.");
        }
        finally
        {
            ClearTemporaryBuffer(temporaryBuffer);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        var previousState = Interlocked.Or(ref _operationState, DisposedMask);
        if ((previousState & DisposedMask) != 0)
            return;

        _ = CancelOperationsAsync();

        if ((previousState & ActiveOperationMask) != 0)
            WaitForOperationsToDrain();

        DisposeOwnedClient();
    }

    private OperationLease EnterOperation()
    {
        while (true)
        {
            var state = Volatile.Read(ref _operationState);
            if ((state & DisposedMask) != 0)
                throw new ObjectDisposedException(nameof(AwsKmsProvider));

            if ((state & ActiveOperationMask) == ActiveOperationMask)
                throw new InvalidOperationException("AWS KMS provider has too many active operations.");

            if (Interlocked.CompareExchange(ref _operationState, state + 1, state) == state)
                return new OperationLease(this);
        }
    }

    private void ExitOperation()
    {
        if (Interlocked.Decrement(ref _operationState) == DisposedMask)
            TryDisposeCancellation();
    }

    private CancellationTokenSource? CreateLinkedCancellation(CancellationToken cancellationToken) =>
        cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellation.Token)
            : null;

    private async Task CancelOperationsAsync()
    {
        try
        {
            await _disposeCancellation.CancelAsync().ConfigureAwait(false);
        }
        catch (AggregateException)
        {
            // Client cancellation callbacks are external; disposal remains best-effort and bounded.
        }
        finally
        {
            Volatile.Write(ref _cancellationFinished, 1);
            TryDisposeCancellation();
        }
    }

    private void WaitForOperationsToDrain()
    {
        var startedAt = Stopwatch.GetTimestamp();
        var spinner = new SpinWait();
        while ((Volatile.Read(ref _operationState) & ActiveOperationMask) != 0 &&
               Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds < DisposeDrainTimeoutMilliseconds)
        {
            spinner.SpinOnce();
        }
    }

    private void DisposeOwnedClient()
    {
        if (_ownsClient && Interlocked.Exchange(ref _clientDisposed, 1) == 0)
            _client.Dispose();
    }

    private void TryDisposeCancellation()
    {
        if ((Volatile.Read(ref _operationState) & ActiveOperationMask) != 0 ||
            Volatile.Read(ref _cancellationFinished) == 0 ||
            Interlocked.Exchange(ref _cancellationDisposed, 1) != 0)
        {
            return;
        }

        _disposeCancellation.Dispose();
    }

    private string ResolveKeyId(SchemaRegistryKmsKeyReference keyReference)
    {
        ArgumentNullException.ThrowIfNull(keyReference);
        if (!string.Equals(keyReference.KmsType, Type, StringComparison.OrdinalIgnoreCase))
        {
            throw new SchemaRegistryKmsException(
                $"AWS KMS provider '{Type}' cannot resolve KMS type '{keyReference.KmsType}'.");
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
                if (clearSource)
                    ClearResponseStream(stream);
            }
        }
    }

    private static void ClearResponseStream(MemoryStream stream)
    {
        if (stream.TryGetBuffer(out var buffer))
        {
            CryptographicOperations.ZeroMemory(buffer.AsSpan());
            return;
        }

        if (!stream.CanWrite)
            return;

        stream.Position = 0;
        Span<byte> zeros = stackalloc byte[256];
        zeros.Clear();
        for (var remaining = stream.Length; remaining > 0;)
        {
            var count = (int)Math.Min(remaining, zeros.Length);
            stream.Write(zeros[..count]);
            remaining -= count;
        }
    }

    private static void ClearTemporaryBuffer(byte[]? temporaryBuffer)
    {
        if (temporaryBuffer is not null)
            CryptographicOperations.ZeroMemory(temporaryBuffer);
    }

    private static bool IsAwsFailure(Exception exception) => exception is
        AmazonServiceException
        or AmazonClientException
        or HttpRequestException
        or IOException
        or WebException
        or SocketException
        or TimeoutException
        or AuthenticationException
        or ObjectDisposedException
        or OperationCanceledException;

    private static AmazonKeyManagementServiceClient CreateOwnedClient(string type)
    {
        ValidateType(type);
        return new AmazonKeyManagementServiceClient();
    }

    private static AmazonKeyManagementServiceClient CreateOwnedClient(RegionEndpoint region, string type)
    {
        ArgumentNullException.ThrowIfNull(region);
        ValidateType(type);
        return new AmazonKeyManagementServiceClient(region);
    }

    private static AmazonKeyManagementServiceClient CreateOwnedClient(
        AmazonKeyManagementServiceConfig config,
        string type)
    {
        ArgumentNullException.ThrowIfNull(config);
        ValidateType(type);
        return new AmazonKeyManagementServiceClient(config);
    }

    private static void ValidateType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("KMS provider type cannot be null or whitespace.", nameof(type));
    }

    private readonly struct OperationLease(AwsKmsProvider owner) : IDisposable
    {
        public void Dispose() => owner.ExitOperation();
    }
}
