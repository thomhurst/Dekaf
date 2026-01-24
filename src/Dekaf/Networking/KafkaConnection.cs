using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Security;
using Dekaf.Security.Sasl;
using Microsoft.Extensions.Logging;

namespace Dekaf.Networking;


/// <summary>
/// A multiplexed connection to a Kafka broker using System.IO.Pipelines.
/// </summary>
public sealed class KafkaConnection : IKafkaConnection
{
    private readonly string _host;
    private readonly int _port;
    private readonly string? _clientId;
    private readonly ILogger<KafkaConnection>? _logger;
    private readonly ConnectionOptions _options;

    private Socket? _socket;
    private Stream? _stream;
    private PipeReader? _reader;
    private PipeWriter? _writer;

    private int _correlationId;
    private readonly ConcurrentDictionary<int, PendingRequest> _pendingRequests = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private Task? _receiveTask;
    private CancellationTokenSource? _receiveCts;
    private OAuthBearerTokenProvider? _ownedTokenProvider;
    private volatile bool _disposed;

    // Certificates loaded from files that we own and must dispose
    private X509Certificate2Collection? _loadedCaCertificates;
    private X509Certificate2? _loadedClientCertificate;

    public int BrokerId { get; private set; } = -1;
    public string Host => _host;
    public int Port => _port;
    public bool IsConnected => _socket?.Connected ?? false;

    public KafkaConnection(
        string host,
        int port,
        string? clientId = null,
        ConnectionOptions? options = null,
        ILogger<KafkaConnection>? logger = null)
    {
        _host = host;
        _port = port;
        _clientId = clientId;
        _options = options ?? new ConnectionOptions();
        _logger = logger;
    }

    public KafkaConnection(
        int brokerId,
        string host,
        int port,
        string? clientId = null,
        ConnectionOptions? options = null,
        ILogger<KafkaConnection>? logger = null)
        : this(host, port, clientId, options, logger)
    {
        BrokerId = brokerId;
    }

    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaConnection));

        if (IsConnected)
            return;

        _logger?.LogDebug("Connecting to {Host}:{Port}", _host, _port);

        _socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };

        if (_options.SendBufferSize > 0)
            _socket.SendBufferSize = _options.SendBufferSize;
        if (_options.ReceiveBufferSize > 0)
            _socket.ReceiveBufferSize = _options.ReceiveBufferSize;

        var endpoint = new DnsEndPoint(_host, _port);
        await _socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);

        Stream networkStream = new NetworkStream(_socket, ownsSocket: false);

        if (_options.UseTls || _options.TlsConfig is not null)
        {
            var sslStream = new SslStream(networkStream, leaveInnerStreamOpen: false);
            var sslOptions = BuildSslClientAuthenticationOptions();
            await sslStream.AuthenticateAsClientAsync(sslOptions, cancellationToken).ConfigureAwait(false);
            networkStream = sslStream;
        }

        _stream = networkStream;

        // Perform SASL authentication if configured
        if (_options.SaslMechanism != SaslMechanism.None)
        {
            await PerformSaslAuthenticationAsync(cancellationToken).ConfigureAwait(false);
        }

        var pipe = new Pipe(new PipeOptions(
            pool: MemoryPool<byte>.Shared,
            minimumSegmentSize: _options.MinimumSegmentSize,
            useSynchronizationContext: false));

        _reader = PipeReader.Create(_stream, new StreamPipeReaderOptions(
            pool: MemoryPool<byte>.Shared,
            bufferSize: _options.ReceiveBufferSize > 0 ? _options.ReceiveBufferSize : 65536,
            minimumReadSize: _options.MinimumReadSize,
            leaveOpen: true));

        _writer = PipeWriter.Create(_stream, new StreamPipeWriterOptions(
            pool: MemoryPool<byte>.Shared,
            minimumBufferSize: _options.SendBufferSize > 0 ? _options.SendBufferSize : 65536,
            leaveOpen: true));

        _receiveCts = new CancellationTokenSource();
        _receiveTask = ReceiveLoopAsync(_receiveCts.Token);

        _logger?.LogDebug("Connected to {Host}:{Port}", _host, _port);
    }

    public async ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaConnection));

        if (!IsConnected)
            throw new InvalidOperationException("Not connected");

        var correlationId = Interlocked.Increment(ref _correlationId);
        var headerVersion = TRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = TRequest.GetResponseHeaderVersion(apiVersion);

        var pending = new PendingRequest(responseHeaderVersion, cancellationToken);
        _pendingRequests[correlationId] = pending;

        try
        {
            _logger?.LogDebug("Sending {ApiKey} request (correlation {CorrelationId}, version {Version}) to {Host}:{Port}",
                TRequest.ApiKey, correlationId, apiVersion, _host, _port);

            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await WriteRequestAsync<TRequest, TResponse>(request, correlationId, apiVersion, headerVersion, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }

            _logger?.LogDebug("Request sent, waiting for response (correlation {CorrelationId})", correlationId);

            // Apply request timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.RequestTimeout);

            PooledResponseBuffer pooledBuffer;
            try
            {
                pooledBuffer = await pending.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Request {TRequest.ApiKey} (correlation {correlationId}) timed out after {_options.RequestTimeout.TotalSeconds}s waiting for response from {_host}:{_port}");
            }

            _logger?.LogDebug("Response received for correlation {CorrelationId}", correlationId);

            try
            {
                var reader = new KafkaProtocolReader(pooledBuffer.Data);
                return (TResponse)TResponse.Read(ref reader, apiVersion);
            }
            finally
            {
                // Return buffer to pool after deserialization
                pooledBuffer.Dispose();
            }
        }
        finally
        {
            if (_pendingRequests.TryRemove(correlationId, out var removed))
            {
                removed.Dispose();
            }
        }
    }

    public async ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaConnection));

        if (!IsConnected)
            throw new InvalidOperationException("Not connected");

        var correlationId = Interlocked.Increment(ref _correlationId);
        var headerVersion = TRequest.GetRequestHeaderVersion(apiVersion);

        // Don't register a pending request - we won't receive a response

        _logger?.LogDebug("Sending fire-and-forget {ApiKey} request (correlation {CorrelationId}, version {Version}) to {Host}:{Port}",
            TRequest.ApiKey, correlationId, apiVersion, _host, _port);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteRequestAsync<TRequest, TResponse>(request, correlationId, apiVersion, headerVersion, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }

        _logger?.LogDebug("Fire-and-forget request sent (correlation {CorrelationId})", correlationId);
    }

    private async ValueTask WriteRequestAsync<TRequest, TResponse>(
        TRequest request,
        int correlationId,
        short apiVersion,
        short headerVersion,
        CancellationToken cancellationToken)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        if (_writer is null)
            throw new InvalidOperationException("Not connected");

        // Build the request body first to calculate size
        var bodyBuffer = new ArrayBufferWriter<byte>();
        var bodyWriter = new KafkaProtocolWriter(bodyBuffer);

        // Write request header
        var header = new RequestHeader
        {
            ApiKey = TRequest.ApiKey,
            ApiVersion = apiVersion,
            CorrelationId = correlationId,
            ClientId = _clientId,
            HeaderVersion = headerVersion
        };
        header.Write(ref bodyWriter);

        // Write request body
        request.Write(ref bodyWriter, apiVersion);

        // Write size prefix + body to the pipe
        var totalSize = bodyBuffer.WrittenCount;
        var memory = _writer.GetMemory(4 + totalSize);

        BinaryPrimitives.WriteInt32BigEndian(memory.Span, totalSize);
        bodyBuffer.WrittenSpan.CopyTo(memory.Span[4..]);

        _writer.Advance(4 + totalSize);

        var result = await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);

        if (result.IsCompleted || result.IsCanceled)
        {
            throw new IOException("Connection closed while writing");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        if (_reader is null)
            return;

        _logger?.LogDebug("Receive loop started for {Host}:{Port}", _host, _port);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                var buffer = result.Buffer;

                _logger?.LogTrace("Received {Length} bytes from {Host}:{Port}", buffer.Length, _host, _port);

                while (TryReadResponse(ref buffer, out var correlationId, out var responseData))
                {
                    _logger?.LogDebug("Received response for correlation ID {CorrelationId}, {Length} bytes", correlationId, responseData.Length);

                    if (_pendingRequests.TryGetValue(correlationId, out var pending))
                    {
                        pending.Complete(responseData);
                    }
                    else
                    {
                        _logger?.LogWarning("Received response for unknown correlation ID {CorrelationId}", correlationId);
                    }
                }

                _reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    _logger?.LogDebug("Receive loop completed (connection closed) for {Host}:{Port}", _host, _port);
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in receive loop");
            FailAllPendingRequests(ex);
        }
    }

    private bool TryReadResponse(
        ref ReadOnlySequence<byte> buffer,
        out int correlationId,
        out PooledResponseBuffer responseData)
    {
        correlationId = 0;
        responseData = default;

        if (buffer.Length < 4)
            return false;

        // Read size prefix
        Span<byte> sizeBuffer = stackalloc byte[4];
        buffer.Slice(0, 4).CopyTo(sizeBuffer);
        var size = BinaryPrimitives.ReadInt32BigEndian(sizeBuffer);

        if (buffer.Length < 4 + size)
            return false;

        // Extract response data (including header)
        var responseBuffer = buffer.Slice(4, size);

        // Read correlation ID from response header
        Span<byte> correlationBuffer = stackalloc byte[4];
        responseBuffer.Slice(0, 4).CopyTo(correlationBuffer);
        correlationId = BinaryPrimitives.ReadInt32BigEndian(correlationBuffer);

        // Use ArrayPool for responses <= 1MB, direct allocation for larger ones
        // ArrayPool.Shared typically pools arrays up to ~1MB (2^20 bytes)
        const int maxPooledSize = 1024 * 1024;
        byte[] responseArray;
        bool isPooled;

        if (size <= maxPooledSize)
        {
            responseArray = ArrayPool<byte>.Shared.Rent(size);
            isPooled = true;
        }
        else
        {
            responseArray = new byte[size];
            isPooled = false;
        }

        responseBuffer.CopyTo(responseArray);
        responseData = new PooledResponseBuffer(responseArray, size, isPooled);

        buffer = buffer.Slice(4 + size);
        return true;
    }

    private void FailAllPendingRequests(Exception ex)
    {
        foreach (var kvp in _pendingRequests)
        {
            if (_pendingRequests.TryRemove(kvp.Key, out var pending))
            {
                pending.Fail(ex);
                pending.Dispose();
            }
        }
    }

    private SslClientAuthenticationOptions BuildSslClientAuthenticationOptions()
    {
        var tlsConfig = _options.TlsConfig;
        var options = new SslClientAuthenticationOptions
        {
            TargetHost = tlsConfig?.TargetHost ?? _host,
            RemoteCertificateValidationCallback = _options.RemoteCertificateValidationCallback
        };

        // Configure enabled SSL protocols if specified
        if (tlsConfig?.EnabledSslProtocols is not null)
        {
            options.EnabledSslProtocols = tlsConfig.EnabledSslProtocols.Value;
        }

        // Configure certificate revocation checking
        if (tlsConfig is not null)
        {
            options.CertificateRevocationCheckMode = tlsConfig.CheckCertificateRevocation
                ? X509RevocationMode.Online
                : X509RevocationMode.NoCheck;
        }

        // Configure server certificate validation
        if (tlsConfig is not null && !tlsConfig.ValidateServerCertificate)
        {
            // Disable server certificate validation (not recommended for production)
            // This is intentional when user explicitly sets ValidateServerCertificate = false
#pragma warning disable CA5359 // Do not disable certificate validation
            options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
#pragma warning restore CA5359
        }
        else if (options.RemoteCertificateValidationCallback is null && HasCustomCaCertificate(tlsConfig))
        {
            // Custom CA certificate validation
            var caCertificates = LoadCaCertificatesWithOwnership(tlsConfig!);
            options.RemoteCertificateValidationCallback = (_, certificate, chain, sslPolicyErrors) =>
                ValidateServerCertificate(certificate, chain, sslPolicyErrors, caCertificates);
        }

        // Configure client certificate for mTLS
        if (tlsConfig is not null)
        {
            var clientCert = LoadClientCertificateWithOwnership(tlsConfig);
            if (clientCert is not null)
            {
                options.ClientCertificates = [clientCert];
            }
        }

        return options;
    }

    private static bool HasCustomCaCertificate(TlsConfig? tlsConfig)
    {
        if (tlsConfig is null)
            return false;

        return tlsConfig.CaCertificatePath is not null ||
               tlsConfig.CaCertificateObject is not null ||
               tlsConfig.CaCertificateCollection is not null;
    }

    /// <summary>
    /// Loads CA certificates and tracks ownership for disposal.
    /// Certificates loaded from files are owned by this instance and will be disposed.
    /// Certificates provided directly via TlsConfig are NOT owned.
    /// </summary>
    private X509Certificate2Collection LoadCaCertificatesWithOwnership(TlsConfig tlsConfig)
    {
        if (tlsConfig.CaCertificateCollection is not null)
        {
            // Not owned - provided by caller
            return tlsConfig.CaCertificateCollection;
        }

        if (tlsConfig.CaCertificateObject is not null)
        {
            // Not owned - provided by caller
            return [tlsConfig.CaCertificateObject];
        }

        if (tlsConfig.CaCertificatePath is not null)
        {
            // Owned - loaded from file, must dispose
            var collection = LoadCertificatesFromFile(tlsConfig.CaCertificatePath);
            _loadedCaCertificates = collection;
            return collection;
        }

        return [];
    }

    private static X509Certificate2Collection LoadCertificatesFromFile(string path)
    {
        var collection = new X509Certificate2Collection();
        var extension = Path.GetExtension(path).ToLowerInvariant();

        if (extension is ".pfx" or ".p12")
        {
            // Load PFX/PKCS12 file using modern API
            collection.Add(X509CertificateLoader.LoadPkcs12FromFile(path, password: null));
        }
        else
        {
            // Assume PEM format - can contain multiple certificates
            collection.ImportFromPemFile(path);
        }

        return collection;
    }

    /// <summary>
    /// Loads client certificate and tracks ownership for disposal.
    /// Certificates loaded from files are owned by this instance and will be disposed.
    /// Certificates provided directly via TlsConfig are NOT owned.
    /// </summary>
    private X509Certificate2? LoadClientCertificateWithOwnership(TlsConfig tlsConfig)
    {
        // If an in-memory certificate is provided, use it directly (not owned)
        if (tlsConfig.ClientCertificate is not null)
        {
            return tlsConfig.ClientCertificate;
        }

        // If certificate path is provided, load from file (owned - must dispose)
        if (tlsConfig.ClientCertificatePath is not null)
        {
            var certPath = tlsConfig.ClientCertificatePath;
            var extension = Path.GetExtension(certPath).ToLowerInvariant();

            X509Certificate2 cert;
            if (extension is ".pfx" or ".p12")
            {
                // Load PFX/PKCS12 file (contains both certificate and private key) using modern API
                cert = X509CertificateLoader.LoadPkcs12FromFile(
                    certPath,
                    string.IsNullOrEmpty(tlsConfig.ClientKeyPassword) ? null : tlsConfig.ClientKeyPassword);
            }
            else
            {
                // PEM format - need separate key file
                if (tlsConfig.ClientKeyPath is null)
                {
                    throw new InvalidOperationException(
                        "Client key path is required when using PEM certificate format");
                }

                cert = string.IsNullOrEmpty(tlsConfig.ClientKeyPassword)
                    ? X509Certificate2.CreateFromPemFile(certPath, tlsConfig.ClientKeyPath)
                    : X509Certificate2.CreateFromEncryptedPemFile(certPath, tlsConfig.ClientKeyPassword, tlsConfig.ClientKeyPath);
            }

            _loadedClientCertificate = cert;
            return cert;
        }

        return null;
    }

    private static bool ValidateServerCertificate(
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors,
        X509Certificate2Collection trustedCaCertificates)
    {
        if (certificate is null)
            return false;

        // If there are no policy errors, the certificate is valid
        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        // If the only error is an untrusted root, validate against our custom CA
        if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors && chain is not null)
        {
            // Track if we created a new certificate to dispose it later
            X509Certificate2? ownedCert = null;
            try
            {
                var cert2 = certificate as X509Certificate2 ?? (ownedCert = new X509Certificate2(certificate));

                // Build a new chain with our custom trust store
                using var customChain = new X509Chain();
                customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                customChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                customChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;

                foreach (var caCert in trustedCaCertificates)
                {
                    customChain.ChainPolicy.CustomTrustStore.Add(caCert);
                }

                if (customChain.Build(cert2))
                {
                    // Check chain status for errors other than UntrustedRoot
                    foreach (var status in customChain.ChainStatus)
                    {
                        if (status.Status != X509ChainStatusFlags.UntrustedRoot &&
                            status.Status != X509ChainStatusFlags.NoError)
                        {
                            return false;
                        }
                    }

                    // Verify the chain ends with one of our trusted CAs
                    var rootCert = customChain.ChainElements[^1].Certificate;
                    foreach (var caCert in trustedCaCertificates)
                    {
                        if (rootCert.Thumbprint == caCert.Thumbprint)
                            return true;
                    }
                }
            }
            finally
            {
                ownedCert?.Dispose();
            }
        }

        return false;
    }

    private async ValueTask PerformSaslAuthenticationAsync(CancellationToken cancellationToken)
    {
        if (_stream is null)
            throw new InvalidOperationException("Not connected");

        _logger?.LogDebug("Starting SASL authentication with mechanism {Mechanism}", _options.SaslMechanism);

        // Create the appropriate authenticator
        ISaslAuthenticator authenticator = _options.SaslMechanism switch
        {
            SaslMechanism.Plain => new PlainAuthenticator(
                _options.SaslUsername ?? throw new InvalidOperationException("SASL username not configured"),
                _options.SaslPassword ?? throw new InvalidOperationException("SASL password not configured")),
            SaslMechanism.ScramSha256 => new ScramAuthenticator(
                SaslMechanism.ScramSha256,
                _options.SaslUsername ?? throw new InvalidOperationException("SASL username not configured"),
                _options.SaslPassword ?? throw new InvalidOperationException("SASL password not configured")),
            SaslMechanism.ScramSha512 => new ScramAuthenticator(
                SaslMechanism.ScramSha512,
                _options.SaslUsername ?? throw new InvalidOperationException("SASL username not configured"),
                _options.SaslPassword ?? throw new InvalidOperationException("SASL password not configured")),
            SaslMechanism.Gssapi => new GssapiAuthenticator(
                _options.GssapiConfig ?? throw new InvalidOperationException("GSSAPI configuration not provided"),
                _host),
            SaslMechanism.OAuthBearer => CreateOAuthBearerAuthenticator(),
            _ => throw new InvalidOperationException($"Unsupported SASL mechanism: {_options.SaslMechanism}")
        };

        try
        {
            // Step 1: Send SaslHandshake to negotiate mechanism
            var handshakeResponse = await SendSaslMessageAsync<SaslHandshakeRequest, SaslHandshakeResponse>(
                new SaslHandshakeRequest { Mechanism = authenticator.MechanismName },
                1, // Use v1 for SaslHandshake
                cancellationToken).ConfigureAwait(false);

            if (handshakeResponse.ErrorCode != ErrorCode.None)
            {
                throw new AuthenticationException(
                    $"SASL handshake failed: {handshakeResponse.ErrorCode}. " +
                    $"Supported mechanisms: {string.Join(", ", handshakeResponse.Mechanisms)}");
            }

            _logger?.LogDebug("SASL handshake successful, starting authentication");

            // Step 2: Perform authentication exchanges
            // For OAUTHBEARER, ensure token is fetched before getting initial response
            if (authenticator is OAuthBearerAuthenticator oauthAuthenticator)
            {
                await oauthAuthenticator.GetTokenAsync(cancellationToken).ConfigureAwait(false);
            }

            var authBytes = authenticator.GetInitialResponse();

            while (!authenticator.IsComplete)
            {
                var authResponse = await SendSaslMessageAsync<SaslAuthenticateRequest, SaslAuthenticateResponse>(
                    new SaslAuthenticateRequest { AuthBytes = authBytes },
                    2, // Use v2 for SaslAuthenticate (flexible version)
                    cancellationToken).ConfigureAwait(false);

                if (authResponse.ErrorCode != ErrorCode.None)
                {
                    throw new AuthenticationException(
                        $"SASL authentication failed: {authResponse.ErrorCode}" +
                        (authResponse.ErrorMessage is not null ? $" - {authResponse.ErrorMessage}" : ""));
                }

                if (authenticator.IsComplete)
                    break;

                var challenge = authResponse.AuthBytes;
                var response = authenticator.EvaluateChallenge(challenge);

                if (response is null)
                    break;

                authBytes = response;
            }

            _logger?.LogInformation("SASL authentication successful with mechanism {Mechanism}", _options.SaslMechanism);
        }
        finally
        {
            // Dispose the authenticator if it implements IDisposable (e.g., GssapiAuthenticator)
            (authenticator as IDisposable)?.Dispose();
        }
    }

    private OAuthBearerAuthenticator CreateOAuthBearerAuthenticator()
    {
        // Priority: static token > token provider > config-based provider
        if (_options.OAuthBearerToken is not null)
        {
            return new OAuthBearerAuthenticator(_options.OAuthBearerToken);
        }

        if (_options.OAuthBearerTokenProvider is not null)
        {
            return new OAuthBearerAuthenticator(_options.OAuthBearerTokenProvider);
        }

        if (_options.OAuthBearerConfig is not null)
        {
            // Create and track the provider for disposal
            _ownedTokenProvider = new OAuthBearerTokenProvider(_options.OAuthBearerConfig);
            return new OAuthBearerAuthenticator(_ownedTokenProvider.GetTokenAsync);
        }

        throw new InvalidOperationException(
            "OAUTHBEARER authentication requires either OAuthBearerToken, OAuthBearerTokenProvider, or OAuthBearerConfig to be configured");
    }

    private async ValueTask<TResponse> SendSaslMessageAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        if (_stream is null)
            throw new InvalidOperationException("Not connected");

        var correlationId = Interlocked.Increment(ref _correlationId);
        var headerVersion = TRequest.GetRequestHeaderVersion(apiVersion);

        // Build the request
        var bodyBuffer = new ArrayBufferWriter<byte>();
        var bodyWriter = new KafkaProtocolWriter(bodyBuffer);

        var header = new RequestHeader
        {
            ApiKey = TRequest.ApiKey,
            ApiVersion = apiVersion,
            CorrelationId = correlationId,
            ClientId = _clientId,
            HeaderVersion = headerVersion
        };
        header.Write(ref bodyWriter);
        request.Write(ref bodyWriter, apiVersion);

        // Write to stream directly (no pipe yet)
        var totalSize = bodyBuffer.WrittenCount;
        var buffer = ArrayPool<byte>.Shared.Rent(4 + totalSize);
        try
        {
            BinaryPrimitives.WriteInt32BigEndian(buffer, totalSize);
            bodyBuffer.WrittenSpan.CopyTo(buffer.AsSpan(4));

            await _stream.WriteAsync(buffer.AsMemory(0, 4 + totalSize), cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        // Read response
        var sizeBuffer = ArrayPool<byte>.Shared.Rent(4);
        try
        {
            await ReadExactlyAsync(_stream, sizeBuffer.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
            var responseSize = BinaryPrimitives.ReadInt32BigEndian(sizeBuffer);

            var responseBuffer = ArrayPool<byte>.Shared.Rent(responseSize);
            try
            {
                await ReadExactlyAsync(_stream, responseBuffer.AsMemory(0, responseSize), cancellationToken).ConfigureAwait(false);

                // Parse response header
                var responseCorrelationId = BinaryPrimitives.ReadInt32BigEndian(responseBuffer);
                if (responseCorrelationId != correlationId)
                {
                    throw new InvalidOperationException(
                        $"Correlation ID mismatch: expected {correlationId}, got {responseCorrelationId}");
                }

                // Skip response header and parse body
                var responseHeaderVersion = TRequest.GetResponseHeaderVersion(apiVersion);
                var offset = 4; // Correlation ID

                if (responseHeaderVersion >= 1)
                {
                    // Skip tagged fields
                    var span = responseBuffer.AsSpan(offset);
                    var (tagCount, bytesRead) = ReadUnsignedVarInt(span);
                    offset += bytesRead;

                    for (var i = 0; i < tagCount; i++)
                    {
                        span = responseBuffer.AsSpan(offset);
                        var (_, tagBytesRead) = ReadUnsignedVarInt(span);
                        offset += tagBytesRead;

                        span = responseBuffer.AsSpan(offset);
                        var (size, sizeBytesRead) = ReadUnsignedVarInt(span);
                        offset += sizeBytesRead + size;
                    }
                }

                var reader = new KafkaProtocolReader(responseBuffer.AsMemory(offset, responseSize - offset));
                return (TResponse)TResponse.Read(ref reader, apiVersion);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(responseBuffer);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(sizeBuffer);
        }
    }

    private static async ValueTask ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new IOException("Connection closed unexpectedly");
            totalRead += read;
        }
    }

    private static (int value, int bytesRead) ReadUnsignedVarInt(ReadOnlySpan<byte> span)
    {
        var result = 0;
        var shift = 0;
        var bytesRead = 0;

        while (bytesRead < span.Length && shift < 35)
        {
            var b = span[bytesRead++];
            result |= (b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                return (result, bytesRead);

            shift += 7;
        }

        return (result, bytesRead);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        _receiveCts?.Cancel();

        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during shutdown
            }
        }

        _receiveCts?.Dispose();

        if (_reader is not null)
            await _reader.CompleteAsync().ConfigureAwait(false);

        if (_writer is not null)
            await _writer.CompleteAsync().ConfigureAwait(false);

        _stream?.Dispose();
        _socket?.Dispose();

        _writeLock.Dispose();
        _ownedTokenProvider?.Dispose();

        // Dispose certificates loaded from files
        if (_loadedCaCertificates is not null)
        {
            foreach (var cert in _loadedCaCertificates)
            {
                cert.Dispose();
            }
        }
        _loadedClientCertificate?.Dispose();

        FailAllPendingRequests(new ObjectDisposedException(nameof(KafkaConnection)));
    }

    private sealed class PendingRequest : IDisposable
    {
        private readonly TaskCompletionSource<PooledResponseBuffer> _tcs;
        private readonly short _responseHeaderVersion;
        private PooledResponseBuffer? _buffer;
        private bool _completed;

        public PendingRequest(short responseHeaderVersion, CancellationToken cancellationToken)
        {
            _responseHeaderVersion = responseHeaderVersion;
            _tcs = new TaskCompletionSource<PooledResponseBuffer>(TaskCreationOptions.RunContinuationsAsynchronously);

            cancellationToken.Register(() =>
            {
                if (_tcs.TrySetCanceled(cancellationToken))
                {
                    // Request was cancelled before completion - dispose the buffer if we received one
                    Dispose();
                }
            });
        }

        public Task<PooledResponseBuffer> Task => _tcs.Task;

        public void Complete(PooledResponseBuffer pooledBuffer)
        {
            lock (this)
            {
                if (_completed)
                {
                    // Already completed/failed - dispose the buffer immediately
                    pooledBuffer.Dispose();
                    return;
                }

                _buffer = pooledBuffer;

                // Skip the response header (correlation ID already read, skip tagged fields if flexible)
                var offset = 4; // Correlation ID already parsed
                if (_responseHeaderVersion >= 1)
                {
                    // Skip tagged fields - read varint count and skip
                    var span = pooledBuffer.Data.Span[offset..];
                    var (tagCount, bytesRead) = ReadUnsignedVarInt(span);
                    offset += bytesRead;

                    for (var i = 0; i < tagCount; i++)
                    {
                        span = pooledBuffer.Data.Span[offset..];
                        var (_, tagBytesRead) = ReadUnsignedVarInt(span);
                        offset += tagBytesRead;

                        span = pooledBuffer.Data.Span[offset..];
                        var (size, sizeBytesRead) = ReadUnsignedVarInt(span);
                        offset += sizeBytesRead + size;
                    }
                }

                // Transfer ownership by creating a new buffer with adjusted offset
                // The caller (TryReadResponse) should not dispose the original buffer
                var slicedBuffer = pooledBuffer.Slice(offset);

                if (_tcs.TrySetResult(slicedBuffer))
                {
                    _completed = true;
                }
                else
                {
                    // Failed to set result (already cancelled/failed) - dispose buffer
                    Dispose();
                }
            }
        }

        public void Fail(Exception ex)
        {
            lock (this)
            {
                if (_tcs.TrySetException(ex))
                {
                    _completed = true;
                }
                // Dispose buffer on failure path
                Dispose();
            }
        }

        public void Dispose()
        {
            lock (this)
            {
                if (_buffer.HasValue && _buffer.Value.IsPooled)
                {
                    _buffer.Value.Dispose();
                    _buffer = null;
                }
            }
        }

        private static (int value, int bytesRead) ReadUnsignedVarInt(ReadOnlySpan<byte> span)
        {
            var result = 0;
            var shift = 0;
            var bytesRead = 0;

            while (bytesRead < span.Length && shift < 35)
            {
                var b = span[bytesRead++];
                result |= (b & 0x7F) << shift;

                if ((b & 0x80) == 0)
                    return (result, bytesRead);

                shift += 7;
            }

            return (result, bytesRead);
        }
    }
}

/// <summary>
/// Connection options.
/// </summary>
public sealed class ConnectionOptions
{
    /// <summary>
    /// Whether to use TLS.
    /// </summary>
    public bool UseTls { get; init; }

    /// <summary>
    /// TLS configuration for SSL/mTLS connections.
    /// When set, <see cref="UseTls"/> is automatically treated as true.
    /// </summary>
    public TlsConfig? TlsConfig { get; init; }

    /// <summary>
    /// Custom certificate validation callback.
    /// </summary>
    public RemoteCertificateValidationCallback? RemoteCertificateValidationCallback { get; init; }

    /// <summary>
    /// SASL authentication mechanism.
    /// </summary>
    public SaslMechanism SaslMechanism { get; init; } = SaslMechanism.None;

    /// <summary>
    /// SASL username for PLAIN and SCRAM authentication.
    /// </summary>
    public string? SaslUsername { get; init; }

    /// <summary>
    /// SASL password for PLAIN and SCRAM authentication.
    /// </summary>
    public string? SaslPassword { get; init; }

    /// <summary>
    /// GSSAPI (Kerberos) configuration. Required when SaslMechanism is Gssapi.
    /// </summary>
    public GssapiConfig? GssapiConfig { get; init; }

    /// <summary>
    /// OAuth bearer configuration for OAUTHBEARER authentication.
    /// </summary>
    public OAuthBearerConfig? OAuthBearerConfig { get; init; }

    /// <summary>
    /// Custom OAuth bearer token provider function for OAUTHBEARER authentication.
    /// Takes precedence over <see cref="OAuthBearerConfig"/> if both are specified.
    /// </summary>
    public Func<CancellationToken, ValueTask<OAuthBearerToken>>? OAuthBearerTokenProvider { get; init; }

    /// <summary>
    /// Static OAuth bearer token for OAUTHBEARER authentication.
    /// Use this for pre-obtained tokens; for dynamic token retrieval, use OAuthBearerTokenProvider instead.
    /// </summary>
    public OAuthBearerToken? OAuthBearerToken { get; init; }

    /// <summary>
    /// Send buffer size in bytes.
    /// </summary>
    public int SendBufferSize { get; init; }

    /// <summary>
    /// Receive buffer size in bytes.
    /// </summary>
    public int ReceiveBufferSize { get; init; }

    /// <summary>
    /// Minimum segment size for pipe.
    /// </summary>
    public int MinimumSegmentSize { get; init; } = 4096;

    /// <summary>
    /// Minimum read size for pipe reader.
    /// </summary>
    public int MinimumReadSize { get; init; } = 256;

    /// <summary>
    /// Connection timeout.
    /// </summary>
    public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Request timeout.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Wrapper for response buffers that may be pooled or allocated.
/// Ensures proper cleanup when the buffer is no longer needed.
/// </summary>
internal readonly struct PooledResponseBuffer : IDisposable
{
    private readonly byte[] _buffer;
    private readonly int _offset;

    public PooledResponseBuffer(byte[] buffer, int length, bool isPooled, int offset = 0)
    {
        _buffer = buffer;
        Length = length;
        IsPooled = isPooled;
        _offset = offset;
    }

    public byte[] Buffer => _buffer;
    public int Length { get; }
    public bool IsPooled { get; }

    public ReadOnlyMemory<byte> Data => _buffer.AsMemory(_offset, Length);

    /// <summary>
    /// Creates a new view of the buffer with an adjusted offset.
    /// Ownership is transferred - the caller should not dispose the original buffer.
    /// </summary>
    public PooledResponseBuffer Slice(int additionalOffset)
    {
        return new PooledResponseBuffer(_buffer, Length - additionalOffset, IsPooled, _offset + additionalOffset);
    }

    public void Dispose()
    {
        if (IsPooled && _buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }
    }
}

