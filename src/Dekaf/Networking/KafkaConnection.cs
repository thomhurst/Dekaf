using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks.Sources;
using Dekaf.Internal;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Security;
using Dekaf.Security.Sasl;
using Microsoft.Extensions.Logging;

namespace Dekaf.Networking;

/// <summary>
/// Helper methods for connection configuration.
/// </summary>
internal static class ConnectionHelper
{
    /// <summary>
    /// Reads a variable-length unsigned integer from a span using Kafka's varint encoding.
    /// Shared by KafkaConnection and PooledPendingRequest for tagged field parsing.
    /// </summary>
    public static (int value, int bytesRead, bool success) ReadUnsignedVarInt(ReadOnlySpan<byte> span)
    {
        var result = 0;
        var shift = 0;
        var bytesRead = 0;

        while (bytesRead < span.Length && shift < 35)
        {
            var b = span[bytesRead++];
            result |= (b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                return (result, bytesRead, true);

            shift += 7;
        }

        // Incomplete data (ran out of bytes) or malformed (exceeded 5-byte VarInt limit)
        return (result, bytesRead, false);
    }
    // Minimum pause threshold for pipeline backpressure (16 MB)
    private const long MinimumPauseThresholdBytes = 16L * 1024 * 1024;

    // Divisor for per-connection pipeline budget allocation (25% = 1/4)
    //
    // Rationale for 25% allocation:
    // - BufferMemory is primarily for producer batch accumulation (main memory pool)
    // - Pipeline buffering is a separate, transient layer for network I/O
    // - 25% provides sufficient headroom for TCP send buffers and in-flight data
    // - Leaves 75% for producer batches, maintaining primary allocation semantics
    // - Prevents pipeline from consuming producer's batch memory pool
    //
    // Example with 256 MB BufferMemory, 2 connections per broker, 3 brokers:
    // - Per-connection budget: 256 MB / (2 * 3) = 42.7 MB
    // - Pipeline allocation: 42.7 MB / 4 = 10.7 MB per connection
    // - Total pipeline memory: 10.7 MB * 6 connections = 64 MB (25% of total)
    // - Producer batch memory: 192 MB (75% of total)
    private const int BufferMemoryDivisor = 4;

    /// <summary>
    /// Calculates pipeline backpressure thresholds based on BufferMemory configuration.
    /// Uses 16 MB floor for modern RAM environments, scales up proportionally with BufferMemory.
    /// </summary>
    /// <param name="bufferMemory">Total producer BufferMemory in bytes</param>
    /// <param name="connectionsPerBroker">Number of connections per broker (must be positive)</param>
    /// <returns>Tuple of (pauseThreshold, resumeThreshold) in bytes</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when connectionsPerBroker is less than or equal to zero.
    /// </exception>
    public static (long PauseThreshold, long ResumeThreshold) CalculatePipelineThresholds(
        ulong bufferMemory,
        int connectionsPerBroker)
    {
        if (connectionsPerBroker <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(connectionsPerBroker),
                connectionsPerBroker,
                "Connections per broker must be positive");
        }

        // Calculate per-pipe budget: BufferMemory / ConnectionsPerBroker / BufferMemoryDivisor
        // Division by BufferMemoryDivisor reserves 1/4 of per-connection memory for pipeline buffering
        // Use 16 MB floor for high-throughput modern environments
        var perPipeBudget = bufferMemory / (ulong)connectionsPerBroker / BufferMemoryDivisor;

        // Ensure we don't overflow long when casting
        var pauseThreshold = perPipeBudget > (ulong)long.MaxValue
            ? long.MaxValue
            : Math.Max(MinimumPauseThresholdBytes, (long)perPipeBudget);

        var resumeThreshold = pauseThreshold / 2;

        return (pauseThreshold, resumeThreshold);
    }
}

/// <summary>
/// A multiplexed connection to a Kafka broker using System.IO.Pipelines.
/// </summary>
public sealed partial class KafkaConnection : IKafkaConnection
{
    private readonly string _host;
    private readonly int _port;
    private readonly string? _clientId;
    private readonly ILogger _logger;
    private readonly ConnectionOptions _options;
    private readonly ulong _bufferMemory;
    private readonly int _connectionsPerBroker;

    private Socket? _socket;
    private Stream? _stream;
    private PipeReader? _reader;
    private PipeWriter? _writer;

    // IMPORTANT: Use global correlation ID counter to prevent TCP port reuse issues.
    // When connections are rapidly closed and reopened, the OS may reuse local TCP ports.
    // If a late packet from an old connection arrives on a new connection with matching
    // correlation ID, it could be incorrectly matched to the wrong pending request.
    // Using globally unique correlation IDs prevents this issue.
    private static int s_globalCorrelationId;
    private readonly ConcurrentDictionary<int, PooledPendingRequest> _pendingRequests = new();
    private readonly PendingRequestPool _pendingRequestPool = new();
    private readonly CancellationTokenSourcePool _timeoutCtsPool = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private Task? _receiveTask;
    private CancellationTokenSource? _receiveCts;
    private OAuthBearerTokenProvider? _ownedTokenProvider;
    private volatile bool _disposed;

    // SASL re-authentication (KIP-368)
    private SaslSessionState? _saslSessionState;
    private Timer? _reauthTimer;
    private readonly SemaphoreSlim _reauthLock = new(1, 1);
    private volatile bool _reauthenticating;

    // Certificates loaded from files that we own and must dispose
    private X509Certificate2Collection? _loadedCaCertificates;
    private X509Certificate2? _loadedClientCertificate;

    // Thread-local reusable buffer for request serialization to avoid per-request allocations
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_requestBuffer;

    public int BrokerId { get; private set; } = -1;
    public string Host => _host;
    public int Port => _port;
    public bool IsConnected => !_disposed && (_socket?.Connected ?? false);

    /// <summary>
    /// Gets the current SASL session state, if SASL authentication was performed.
    /// Returns null if no SASL authentication occurred or if re-authentication is disabled.
    /// </summary>
    internal SaslSessionState? SaslSession => _saslSessionState;

    public KafkaConnection(
        string host,
        int port,
        string? clientId = null,
        ConnectionOptions? options = null,
        ILogger<KafkaConnection>? logger = null,
        ulong bufferMemory = 33554432,
        int connectionsPerBroker = 1)
    {
        _host = host;
        _port = port;
        _clientId = clientId;
        _options = options ?? new ConnectionOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<KafkaConnection>.Instance;
        _bufferMemory = bufferMemory;
        _connectionsPerBroker = connectionsPerBroker;
    }

    public KafkaConnection(
        int brokerId,
        string host,
        int port,
        string? clientId = null,
        ConnectionOptions? options = null,
        ILogger<KafkaConnection>? logger = null,
        ulong bufferMemory = 33554432,
        int connectionsPerBroker = 1)
        : this(host, port, clientId, options, logger, bufferMemory, connectionsPerBroker)
    {
        BrokerId = brokerId;
    }

    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaConnection));

        if (IsConnected)
            return;

        LogConnecting(_host, _port);

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

        // Calculate pipeline backpressure thresholds based on BufferMemory
        var (pauseThreshold, resumeThreshold) = ConnectionHelper.CalculatePipelineThresholds(
            _bufferMemory,
            _connectionsPerBroker);

        LogConfiguringPipe(BrokerId, pauseThreshold, resumeThreshold);

        var pipe = new Pipe(new PipeOptions(
            pool: MemoryPool<byte>.Shared,
            minimumSegmentSize: _options.MinimumSegmentSize,
            useSynchronizationContext: false,
            pauseWriterThreshold: pauseThreshold,
            resumeWriterThreshold: resumeThreshold));

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

        LogConnected(_host, _port);
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

        var correlationId = Interlocked.Increment(ref s_globalCorrelationId);
        var headerVersion = TRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = TRequest.GetResponseHeaderVersion(apiVersion);

        var pending = _pendingRequestPool.Rent();
        pending.Initialize(responseHeaderVersion, cancellationToken);
        _pendingRequests[correlationId] = pending;

        // Write phase
        LogSendingRequest(TRequest.ApiKey, correlationId, apiVersion, _host, _port);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteRequestAsync<TRequest, TResponse>(request, correlationId, apiVersion, headerVersion, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Write failed — clean up pending request before propagating
            if (_pendingRequests.TryRemove(correlationId, out var removed))
            {
                _pendingRequestPool.Return(removed);
            }

            throw;
        }
        finally
        {
            _writeLock.Release();
        }

        LogRequestSentWaitingForResponse(correlationId);

        // Response phase: await response with timeout and parse
        return await AwaitAndParseResponseAsync<TRequest, TResponse>(
            pending, correlationId, apiVersion, cancellationToken).ConfigureAwait(false);
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

        var correlationId = Interlocked.Increment(ref s_globalCorrelationId);
        var headerVersion = TRequest.GetRequestHeaderVersion(apiVersion);

        // Don't register a pending request - we won't receive a response

        LogSendingFireAndForgetRequest(TRequest.ApiKey, correlationId, apiVersion, _host, _port);

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

        LogFireAndForgetRequestSent(correlationId);
    }

    public async Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
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

        var correlationId = Interlocked.Increment(ref s_globalCorrelationId);
        var headerVersion = TRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = TRequest.GetResponseHeaderVersion(apiVersion);

        var pending = _pendingRequestPool.Rent();
        pending.Initialize(responseHeaderVersion, cancellationToken);
        _pendingRequests[correlationId] = pending;

        try
        {
            // Write phase: serialize and send the request under write lock
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

            // Response phase: await response with timeout, then parse
            return await AwaitAndParseResponseAsync<TRequest, TResponse>(
                pending, correlationId, apiVersion, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // On failure, ensure we clean up the pending request
            if (_pendingRequests.TryRemove(correlationId, out var removed))
            {
                _pendingRequestPool.Return(removed);
            }

            throw;
        }
    }

    /// <summary>
    /// Awaits the response for a pending request, applies timeout, and parses the response.
    /// Shared between SendAsync and SendPipelinedAsync.
    /// </summary>
    private async Task<TResponse> AwaitAndParseResponseAsync<TRequest, TResponse>(
        PooledPendingRequest pending,
        int correlationId,
        short apiVersion,
        CancellationToken cancellationToken)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        try
        {
            LogWaitingForResponse(correlationId);

            PooledResponseBuffer pooledBuffer;
            if (!cancellationToken.CanBeCanceled)
            {
                var timeoutCts = _timeoutCtsPool.Rent();
                try
                {
                    timeoutCts.CancelAfter(_options.RequestTimeout);
                    pending.RegisterCancellation(timeoutCts.Token);

                    try
                    {
                        pooledBuffer = await pending.AsValueTask().ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                    {
                        throw new TimeoutException($"Request {TRequest.ApiKey} (correlation {correlationId}) timed out after {_options.RequestTimeout.TotalSeconds}s waiting for response from {_host}:{_port}");
                    }
                }
                finally
                {
                    pending.DisposeRegistration();
                    _timeoutCtsPool.Return(timeoutCts);
                }
            }
            else
            {
                var timeoutCts = _timeoutCtsPool.Rent();
                try
                {
                    timeoutCts.CancelAfter(_options.RequestTimeout);
                    using var reg = cancellationToken.Register(static s => ((CancellationTokenSource)s!).Cancel(), timeoutCts);
                    pending.RegisterCancellation(timeoutCts.Token);

                    try
                    {
                        pooledBuffer = await pending.AsValueTask().ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        throw new TimeoutException($"Request {TRequest.ApiKey} (correlation {correlationId}) timed out after {_options.RequestTimeout.TotalSeconds}s waiting for response from {_host}:{_port}");
                    }
                }
                finally
                {
                    pending.DisposeRegistration();
                    _timeoutCtsPool.Return(timeoutCts);
                }
            }

            LogResponseReceived(correlationId);

            var isFetchResponse = TRequest.ApiKey == ApiKey.Fetch;

            if (isFetchResponse)
            {
                var memoryOwner = pooledBuffer.TransferOwnership();
                ResponseParsingContext.SetPooledMemory(memoryOwner);
                try
                {
                    var reader = new KafkaProtocolReader(pooledBuffer.Data);
                    var response = (TResponse)TResponse.Read(ref reader, apiVersion);

                    if (ResponseParsingContext.WasMemoryUsed)
                    {
                        var takenMemory = ResponseParsingContext.TakePooledMemory();
                        if (response is FetchResponse fetchResponse && takenMemory is not null)
                        {
                            fetchResponse.PooledMemoryOwner = takenMemory;
                        }
                        else
                        {
                            takenMemory?.Dispose();
                        }
                    }
                    else
                    {
                        memoryOwner.Dispose();
                    }

                    return response;
                }
                finally
                {
                    ResponseParsingContext.Reset();
                }
            }
            else
            {
                try
                {
                    var reader = new KafkaProtocolReader(pooledBuffer.Data);
                    return (TResponse)TResponse.Read(ref reader, apiVersion);
                }
                finally
                {
                    pooledBuffer.Dispose();
                }
            }
        }
        finally
        {
            if (_pendingRequests.TryRemove(correlationId, out var removed))
            {
                _pendingRequestPool.Return(removed);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ArrayBufferWriter<byte> GetRequestBuffer()
    {
        var buffer = t_requestBuffer;
        if (buffer is null)
        {
            buffer = new ArrayBufferWriter<byte>(4096);
            t_requestBuffer = buffer;
        }
        else
        {
            buffer.Clear();
        }
        return buffer;
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
        var bodyBuffer = GetRequestBuffer();
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

        // Apply RequestTimeout to flush operation using pooled CTS
        var timeoutCts = _timeoutCtsPool.Rent();
        try
        {
            timeoutCts.CancelAfter(_options.RequestTimeout);
            using var reg = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(static s => ((CancellationTokenSource)s!).Cancel(), timeoutCts)
                : default;

            FlushResult result;
            try
            {
                result = await _writer.FlushAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                LogFlushTimeout(_options.RequestTimeout.TotalMilliseconds, correlationId, BrokerId);

                throw new KafkaException(
                    $"Flush timeout after {(int)_options.RequestTimeout.TotalMilliseconds}ms on connection to broker {BrokerId}");
            }

            if (result.IsCompleted || result.IsCanceled)
            {
                throw new IOException("Connection closed while writing");
            }
        }
        finally
        {
            _timeoutCtsPool.Return(timeoutCts);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        if (_reader is null)
            return;

        LogReceiveLoopStarted(_host, _port);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Apply RequestTimeout to each read operation using pooled CTS
                var timeoutCts = _timeoutCtsPool.Rent();
                try
                {
                    ReadResult result;
                    try
                    {
                        timeoutCts.CancelAfter(_options.RequestTimeout);
                        using var reg = cancellationToken.CanBeCanceled
                            ? cancellationToken.Register(static s => ((CancellationTokenSource)s!).Cancel(), timeoutCts)
                            : default;
                        result = await _reader.ReadAsync(timeoutCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        LogReceiveTimeout(_options.RequestTimeout.TotalMilliseconds, BrokerId);

                        // Mark connection as failed to trigger reconnection
                        _disposed = true;

                        throw new KafkaException(
                            $"Receive timeout after {(int)_options.RequestTimeout.TotalMilliseconds}ms - connection to broker {BrokerId} failed");
                    }

                    var buffer = result.Buffer;

                    LogReceivedBytes(buffer.Length, _host, _port);

                    while (TryReadResponse(ref buffer, out var correlationId, out var responseData))
                    {
                        LogReceivedResponse(correlationId, responseData.Length);

                        if (_pendingRequests.TryGetValue(correlationId, out var pending))
                        {
                            if (!pending.TryComplete(responseData))
                            {
                                // Request was already cancelled/failed - dispose the buffer
                                responseData.Dispose();
                            }
                        }
                        else
                        {
                            LogUnknownCorrelationId(correlationId);
                            // No pending request - dispose the buffer
                            responseData.Dispose();
                        }
                    }

                    _reader.AdvanceTo(buffer.Start, buffer.End);

                    if (result.IsCompleted)
                    {
                        LogReceiveLoopCompleted(_host, _port);
                        break;
                    }
                }
                finally
                {
                    _timeoutCtsPool.Return(timeoutCts);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown — fail any pending requests so callers don't hang
            FailAllPendingRequests(new OperationCanceledException("Connection closing", cancellationToken));
        }
        catch (Exception ex)
        {
            LogReceiveLoopError(ex);
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

        // Use dedicated response pool for responses <= 4MB.
        // Kafka fetch responses are typically slightly over 1MB (due to protocol overhead
        // on top of max.partition.fetch.bytes default of 1,048,576), so ArrayPool<byte>.Shared
        // (which only pools up to 2^20 bytes) can't pool them, causing LOH allocations on every fetch.
        const int maxPooledSize = 4 * 1024 * 1024;
        byte[] responseArray;
        bool isPooled;

        if (size <= maxPooledSize)
        {
            responseArray = PooledResponseBuffer.Pool.Rent(size);
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
                pending.TrySetException(ex);
                // NOTE: Do NOT return to pool here. When using ConfigureAwait(false),
                // the continuation might run asynchronously on the thread pool.
                // If we call Return() (which calls Reset()), the version will be
                // incremented before GetResult() is called, causing a version mismatch.
                // The awaiter in SendAsync will eventually call GetResult(), then the
                // outer finally block will return the request to the pool.
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

        LogStartingSaslAuthentication(_options.SaslMechanism);

        var sessionLifetimeMs = await PerformSaslExchangeAsync(cancellationToken).ConfigureAwait(false);

        LogSaslAuthenticationSuccessful(_options.SaslMechanism);

        // Schedule re-authentication if the broker reported a session lifetime (KIP-368)
        ScheduleReauthentication(sessionLifetimeMs);
    }

    /// <summary>
    /// Performs the full SASL handshake + authenticate exchange and returns the session lifetime in milliseconds.
    /// This method is used for both initial authentication and re-authentication.
    /// </summary>
    private async ValueTask<long> PerformSaslExchangeAsync(CancellationToken cancellationToken)
    {
        // Create the appropriate authenticator
        ISaslAuthenticator authenticator = CreateSaslAuthenticator();

        long sessionLifetimeMs = 0;

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

            LogSaslHandshakeSuccessful();

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

                // Capture session lifetime from the last successful auth response (KIP-368)
                sessionLifetimeMs = authResponse.SessionLifetimeMs;

                if (authenticator.IsComplete)
                    break;

                var challenge = authResponse.AuthBytes;
                var response = authenticator.EvaluateChallenge(challenge);

                if (response is null)
                    break;

                authBytes = response;
            }
        }
        finally
        {
            // Dispose the authenticator if it implements IDisposable (e.g., GssapiAuthenticator)
            (authenticator as IDisposable)?.Dispose();
        }

        return sessionLifetimeMs;
    }

    private ISaslAuthenticator CreateSaslAuthenticator() => _options.SaslMechanism switch
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

    /// <summary>
    /// Schedules re-authentication based on the broker-reported session lifetime.
    /// </summary>
    private void ScheduleReauthentication(long sessionLifetimeMs)
    {
        var reauthConfig = _options.SaslReauthenticationConfig ?? new SaslReauthenticationConfig();

        if (!reauthConfig.Enabled)
        {
            LogSaslReauthenticationDisabled();
            return;
        }

        _saslSessionState = new SaslSessionState(sessionLifetimeMs, reauthConfig);

        if (!_saslSessionState.RequiresReauthentication)
        {
            LogSaslReauthenticationNotNeeded(sessionLifetimeMs);
            return;
        }

        var delayMs = _saslSessionState.ReauthenticationDelayMs;

        LogSchedulingReauthentication(sessionLifetimeMs, delayMs, _saslSessionState.ReauthenticationDeadline, BrokerId);

        // Replace existing timer, disposing old one AFTER assignment to avoid race window
        var oldTimer = _reauthTimer;
        _reauthTimer = new Timer(
            OnReauthenticationTimerElapsed,
            null,
            delayMs,
            Timeout.Infinite); // One-shot timer, re-scheduled after each re-auth
        oldTimer?.Dispose();
    }

    private void OnReauthenticationTimerElapsed(object? state)
    {
        var task = PerformReauthenticationAsync();
        if (!task.IsCompleted)
        {
            _ = task.ContinueWith(
                static (t, state) =>
                {
                    // Exception already logged inside PerformReauthenticationAsync
                    _ = t.Exception;
                },
                null,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    /// <summary>
    /// Performs re-authentication on the existing connection (KIP-368).
    /// Re-authentication uses standard SaslHandshake + SaslAuthenticate API messages sent
    /// through the normal multiplexed pipeline. The broker handles these in-band with
    /// other requests per KIP-368.
    /// </summary>
    internal async Task PerformReauthenticationAsync()
    {
        if (_disposed || !IsConnected)
            return;

        if (_reauthenticating)
            return;

        // Only one re-authentication at a time
        if (!await _reauthLock.WaitAsync(0).ConfigureAwait(false))
            return;

        try
        {
            _reauthenticating = true;

            LogStartingSaslReauthentication(BrokerId, _host, _port);

            // Use a timeout for re-authentication to prevent indefinite blocking
            using var timeoutCts = new CancellationTokenSource(_options.RequestTimeout);

            var sessionLifetimeMs = await PerformSaslReauthExchangeAsync(timeoutCts.Token)
                .ConfigureAwait(false);

            LogSaslReauthenticationSuccessful(BrokerId, sessionLifetimeMs);

            // Schedule the next re-authentication
            ScheduleReauthentication(sessionLifetimeMs);
        }
        catch (Exception ex) when (!_disposed)
        {
            LogSaslReauthenticationFailed(ex, BrokerId);

            // Don't tear down the connection - let it fail naturally when the broker
            // rejects requests after session expiry. The connection pool will handle
            // reconnection at that point.
        }
        finally
        {
            _reauthenticating = false;
            _reauthLock.Release();
        }
    }

    /// <summary>
    /// Performs SASL re-authentication exchange using the multiplexed pipeline.
    /// Re-authentication happens after the pipeline is established, so we use the
    /// pipeline-based SendAsync rather than the direct stream methods used during initial auth.
    /// Per KIP-368, re-authentication uses standard SaslHandshake + SaslAuthenticate API messages
    /// with correlation IDs, sent as normal Kafka protocol messages.
    /// </summary>
    private async ValueTask<long> PerformSaslReauthExchangeAsync(CancellationToken cancellationToken)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected");

        // Create the appropriate authenticator (fresh instance for each re-auth)
        ISaslAuthenticator authenticator = CreateSaslAuthenticator();

        long sessionLifetimeMs = 0;

        try
        {
            // Step 1: Send SaslHandshake to negotiate mechanism
            // Uses the multiplexed pipeline for in-band re-authentication per KIP-368
            var handshakeResponse = await SendAsync<SaslHandshakeRequest, SaslHandshakeResponse>(
                new SaslHandshakeRequest { Mechanism = authenticator.MechanismName },
                1,
                cancellationToken).ConfigureAwait(false);

            if (handshakeResponse.ErrorCode != ErrorCode.None)
            {
                throw new AuthenticationException(
                    $"SASL re-auth handshake failed: {handshakeResponse.ErrorCode}. " +
                    $"Supported mechanisms: {string.Join(", ", handshakeResponse.Mechanisms)}");
            }

            // Step 2: For OAUTHBEARER, fetch fresh token
            if (authenticator is OAuthBearerAuthenticator oauthAuthenticator)
            {
                await oauthAuthenticator.GetTokenAsync(cancellationToken).ConfigureAwait(false);
            }

            var authBytes = authenticator.GetInitialResponse();

            while (!authenticator.IsComplete)
            {
                var authResponse = await SendAsync<SaslAuthenticateRequest, SaslAuthenticateResponse>(
                    new SaslAuthenticateRequest { AuthBytes = authBytes },
                    2,
                    cancellationToken).ConfigureAwait(false);

                if (authResponse.ErrorCode != ErrorCode.None)
                {
                    throw new AuthenticationException(
                        $"SASL re-authentication failed: {authResponse.ErrorCode}" +
                        (authResponse.ErrorMessage is not null ? $" - {authResponse.ErrorMessage}" : ""));
                }

                sessionLifetimeMs = authResponse.SessionLifetimeMs;

                if (authenticator.IsComplete)
                    break;

                var challenge = authResponse.AuthBytes;
                var response = authenticator.EvaluateChallenge(challenge);

                if (response is null)
                    break;

                authBytes = response;
            }
        }
        finally
        {
            (authenticator as IDisposable)?.Dispose();
        }

        return sessionLifetimeMs;
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

        var correlationId = Interlocked.Increment(ref s_globalCorrelationId);
        var headerVersion = TRequest.GetRequestHeaderVersion(apiVersion);

        // Build the request
        var bodyBuffer = GetRequestBuffer();
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
            // Clear buffer to prevent SASL credential leakage (contains passwords, tokens, secrets)
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
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
                    var (tagCount, bytesRead, tagCountSuccess) = ConnectionHelper.ReadUnsignedVarInt(span);
                    if (!tagCountSuccess)
                    {
                        throw new InvalidOperationException(
                            $"Failed to read tagged field count VarInt at offset {offset} in SASL response (buffer length: {responseSize})");
                    }
                    offset += bytesRead;

                    for (var i = 0; i < tagCount; i++)
                    {
                        span = responseBuffer.AsSpan(offset);
                        var (_, tagBytesRead, tagSuccess) = ConnectionHelper.ReadUnsignedVarInt(span);
                        if (!tagSuccess)
                        {
                            throw new InvalidOperationException(
                                $"Failed to read tag key VarInt at offset {offset} in SASL response (buffer length: {responseSize}, tag index: {i}/{tagCount})");
                        }
                        offset += tagBytesRead;

                        span = responseBuffer.AsSpan(offset);
                        var (size, sizeBytesRead, sizeSuccess) = ConnectionHelper.ReadUnsignedVarInt(span);
                        if (!sizeSuccess)
                        {
                            throw new InvalidOperationException(
                                $"Failed to read tag size VarInt at offset {offset} in SASL response (buffer length: {responseSize}, tag index: {i}/{tagCount})");
                        }
                        offset += sizeBytesRead + size;
                    }
                }

                var reader = new KafkaProtocolReader(responseBuffer.AsMemory(offset, responseSize - offset));
                return (TResponse)TResponse.Read(ref reader, apiVersion);
            }
            finally
            {
                // Clear buffer to prevent SASL credential leakage (may contain auth tokens/session data)
                ArrayPool<byte>.Shared.Return(responseBuffer, clearArray: true);
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

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        LogConnectionDisposing(BrokerId, _pendingRequests.Count);

        _receiveCts?.Cancel();

        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
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

        _reauthTimer?.Dispose();
        _reauthLock.Dispose();
        _writeLock.Dispose();
        _timeoutCtsPool.Clear();
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

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connecting to {Host}:{Port}")]
    private partial void LogConnecting(string host, int port);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Configuring pipe for broker {BrokerId}: pauseThreshold={PauseThreshold} bytes, resumeThreshold={ResumeThreshold} bytes")]
    private partial void LogConfiguringPipe(int brokerId, long pauseThreshold, long resumeThreshold);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connected to {Host}:{Port}")]
    private partial void LogConnected(string host, int port);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending {ApiKey} request (correlation {CorrelationId}, version {Version}) to {Host}:{Port}")]
    private partial void LogSendingRequest(ApiKey apiKey, int correlationId, short version, string host, int port);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Request sent, waiting for response (correlation {CorrelationId})")]
    private partial void LogRequestSentWaitingForResponse(int correlationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending fire-and-forget {ApiKey} request (correlation {CorrelationId}, version {Version}) to {Host}:{Port}")]
    private partial void LogSendingFireAndForgetRequest(ApiKey apiKey, int correlationId, short version, string host, int port);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fire-and-forget request sent (correlation {CorrelationId})")]
    private partial void LogFireAndForgetRequestSent(int correlationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Waiting for response (correlation {CorrelationId})")]
    private partial void LogWaitingForResponse(int correlationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Response received for correlation {CorrelationId}")]
    private partial void LogResponseReceived(int correlationId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Flush timeout after {Timeout}ms for request {CorrelationId} to broker {BrokerId}")]
    private partial void LogFlushTimeout(double timeout, int correlationId, int brokerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Receive loop started for {Host}:{Port}")]
    private partial void LogReceiveLoopStarted(string host, int port);

    [LoggerMessage(Level = LogLevel.Error, Message = "Receive timeout after {Timeout}ms on broker {BrokerId} - marking connection as failed")]
    private partial void LogReceiveTimeout(double timeout, int brokerId);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Received {Length} bytes from {Host}:{Port}")]
    private partial void LogReceivedBytes(long length, string host, int port);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Received response for correlation ID {CorrelationId}, {Length} bytes")]
    private partial void LogReceivedResponse(int correlationId, int length);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Received response for unknown correlation ID {CorrelationId}")]
    private partial void LogUnknownCorrelationId(int correlationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Receive loop completed (connection closed) for {Host}:{Port}")]
    private partial void LogReceiveLoopCompleted(string host, int port);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error in receive loop")]
    private partial void LogReceiveLoopError(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting SASL authentication with mechanism {Mechanism}")]
    private partial void LogStartingSaslAuthentication(SaslMechanism mechanism);

    [LoggerMessage(Level = LogLevel.Information, Message = "SASL authentication successful with mechanism {Mechanism}")]
    private partial void LogSaslAuthenticationSuccessful(SaslMechanism mechanism);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SASL handshake successful, starting authentication")]
    private partial void LogSaslHandshakeSuccessful();

    [LoggerMessage(Level = LogLevel.Debug, Message = "SASL re-authentication is disabled")]
    private partial void LogSaslReauthenticationDisabled();

    [LoggerMessage(Level = LogLevel.Debug, Message = "SASL re-authentication not needed: sessionLifetimeMs={SessionLifetimeMs}")]
    private partial void LogSaslReauthenticationNotNeeded(long sessionLifetimeMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "SASL session lifetime is {SessionLifetimeMs}ms. Scheduling re-authentication in {DelayMs}ms (at {ReauthTime}) for broker {BrokerId}")]
    private partial void LogSchedulingReauthentication(long sessionLifetimeMs, long delayMs, DateTimeOffset? reauthTime, int brokerId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting SASL re-authentication for broker {BrokerId} at {Host}:{Port}")]
    private partial void LogStartingSaslReauthentication(int brokerId, string host, int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "SASL re-authentication successful for broker {BrokerId}. New session lifetime: {SessionLifetimeMs}ms")]
    private partial void LogSaslReauthenticationSuccessful(int brokerId, long sessionLifetimeMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "SASL re-authentication failed for broker {BrokerId}. Connection may be terminated by the broker when the session expires")]
    private partial void LogSaslReauthenticationFailed(Exception ex, int brokerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Disposing connection to broker {BrokerId}: {PendingRequestCount} pending requests")]
    private partial void LogConnectionDisposing(int brokerId, int pendingRequestCount);

    #endregion
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
    /// SASL re-authentication configuration (KIP-368).
    /// When enabled (default), connections will proactively re-authenticate
    /// before SASL session credentials expire.
    /// </summary>
    public SaslReauthenticationConfig? SaslReauthenticationConfig { get; init; }

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
    /// <summary>
    /// Dedicated pool for Kafka response buffers. Sized to accommodate fetch responses
    /// which are typically slightly over 1MB due to protocol overhead on top of
    /// max.partition.fetch.bytes (default 1,048,576). ArrayPool&lt;byte&gt;.Shared only pools
    /// arrays up to 2^20 (1,048,576) bytes, so responses of ~1,048,712 bytes would bypass
    /// pooling entirely, causing ~693 LOH allocations/sec under load.
    /// </summary>
    internal static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Create(
        maxArrayLength: 4 * 1024 * 1024,
        maxArraysPerBucket: 256);

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

    /// <summary>
    /// Transfers ownership of this buffer to a new PooledResponseMemory instance.
    /// After calling this, the buffer should NOT be disposed via this struct.
    /// </summary>
    public PooledResponseMemory TransferOwnership()
    {
        return new PooledResponseMemory(_buffer, Length, IsPooled, _offset);
    }

    public void Dispose()
    {
        if (IsPooled && _buffer is not null)
        {
            Pool.Return(_buffer);
        }
    }
}

/// <summary>
/// A class-based wrapper for pooled response memory that implements IPooledMemory.
/// Used to transfer buffer ownership from KafkaConnection to FetchResponse/RecordBatch
/// for zero-copy record parsing.
/// </summary>
internal sealed class PooledResponseMemory : IPooledMemory
{
    private byte[]? _buffer;
    private readonly int _length;
    private readonly bool _isPooled;
    private readonly int _offset;

    public PooledResponseMemory(byte[] buffer, int length, bool isPooled, int offset)
    {
        _buffer = buffer;
        _length = length;
        _isPooled = isPooled;
        _offset = offset;
    }

    public ReadOnlyMemory<byte> Memory => _buffer is not null
        ? _buffer.AsMemory(_offset, _length)
        : throw new ObjectDisposedException(nameof(PooledResponseMemory));

    public void Dispose()
    {
        var buffer = Interlocked.Exchange(ref _buffer, null);
        if (_isPooled && buffer is not null)
        {
            PooledResponseBuffer.Pool.Return(buffer);
        }
    }
}

/// <summary>
/// Thread-safe bounded pool for <see cref="PooledPendingRequest"/> instances.
/// Uses lock-free operations via <see cref="ConcurrentStack{T}"/> for high throughput.
/// </summary>
internal sealed class PendingRequestPool
{
    private const int MaxPoolSize = 256;
    private readonly ConcurrentStack<PooledPendingRequest> _pool = new();
    private int _poolCount;

    /// <summary>
    /// Gets a <see cref="PooledPendingRequest"/> from the pool, or creates a new one if empty.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PooledPendingRequest Rent()
    {
        if (_pool.TryPop(out var request))
        {
            Interlocked.Decrement(ref _poolCount);
            return request;
        }

        return new PooledPendingRequest();
    }

    /// <summary>
    /// Returns a <see cref="PooledPendingRequest"/> to the pool for reuse.
    /// If the pool is full, the instance is discarded.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(PooledPendingRequest request)
    {
        // Reset the request for reuse
        request.Reset();

        // Check if pool is full
        var count = Interlocked.Increment(ref _poolCount);
        if (count <= MaxPoolSize)
        {
            _pool.Push(request);
        }
        else
        {
            // Pool is full - decrement count and let GC handle the instance
            Interlocked.Decrement(ref _poolCount);
        }
    }
}

/// <summary>
/// A poolable pending request that implements <see cref="IValueTaskSource{T}"/> for zero-allocation
/// async completion. Uses <see cref="ManualResetValueTaskSourceCore{T}"/> internally.
/// </summary>
internal sealed class PooledPendingRequest : IValueTaskSource<PooledResponseBuffer>
{
    private ManualResetValueTaskSourceCore<PooledResponseBuffer> _core;
    private short _responseHeaderVersion;
    private CancellationTokenRegistration _cancellationRegistration;
    private int _state; // 0 = pending, 1 = completing, 2 = completed
    private PooledResponseBuffer? _pendingBuffer; // Buffer received but not yet processed

    /// <summary>
    /// Initializes the request for a new operation.
    /// </summary>
    public void Initialize(short responseHeaderVersion, CancellationToken cancellationToken)
    {
        _responseHeaderVersion = responseHeaderVersion;
        _state = 0;
        _pendingBuffer = null;

        // Register for cancellation if the token can be cancelled
        if (cancellationToken.CanBeCanceled)
        {
            _cancellationRegistration = cancellationToken.Register(
                static state => ((PooledPendingRequest)state!).OnCancelled(),
                this);
        }
    }

    /// <summary>
    /// Registers an additional cancellation token (e.g., for timeout).
    /// Replaces any existing registration.
    /// </summary>
    public void RegisterCancellation(CancellationToken cancellationToken)
    {
        if (cancellationToken.CanBeCanceled)
        {
            var newRegistration = cancellationToken.Register(
                static state => ((PooledPendingRequest)state!).OnCancelled(),
                this);

            // Dispose old registration and replace with new one
            var oldRegistration = _cancellationRegistration;
            _cancellationRegistration = newRegistration;
            oldRegistration.Dispose();
        }
    }

    /// <summary>
    /// Disposes the current cancellation registration without full reset.
    /// Call this before returning a pooled CTS to ensure the registration is cleaned up.
    /// </summary>
    public void DisposeRegistration()
    {
        _cancellationRegistration.Dispose();
        _cancellationRegistration = default;
    }

    private void OnCancelled()
    {
        TrySetCanceled();
    }

    /// <summary>
    /// Gets a <see cref="ValueTask{T}"/> bound to this source.
    /// </summary>
    public ValueTask<PooledResponseBuffer> AsValueTask() => new(this, _core.Version);

    /// <summary>
    /// Gets the current version token.
    /// </summary>
    public short Version => _core.Version;

    /// <summary>
    /// Attempts to complete the request with response data.
    /// Returns false if already completed (cancelled or failed).
    /// </summary>
    public bool TryComplete(PooledResponseBuffer pooledBuffer)
    {
        // Atomically claim the completing state
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
        {
            return false; // Already completing or completed
        }

        // CRITICAL: Parse the response BEFORE setting state to completed.
        // SetResult/SetException invokes the continuation synchronously, which may
        // return this request to the pool. We must not access any mutable state after
        // calling SetResult/SetException.
        PooledResponseBuffer? result = null;
        Exception? parseException = null;

        try
        {
            result = ParseAndSliceResponse(pooledBuffer);
        }
        catch (Exception ex)
        {
            pooledBuffer.Dispose();
            parseException = ex;
        }

        // Mark as completed BEFORE invoking continuation.
        // The continuation may return this instance to the pool, and we must not
        // access mutable state after that point.
        Volatile.Write(ref _state, 2);

        // Now invoke the continuation (this may synchronously return to caller)
        if (parseException is not null)
        {
            _core.SetException(parseException);
        }
        else
        {
            _core.SetResult(result!.Value);
        }

        return true;
    }

    /// <summary>
    /// Attempts to complete the request with an exception.
    /// Returns false if already completed.
    /// </summary>
    public bool TrySetException(Exception exception)
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
        {
            return false;
        }

        // Mark as completed BEFORE invoking continuation.
        // SetException invokes the continuation synchronously, which may return
        // this instance to the pool.
        Volatile.Write(ref _state, 2);
        _core.SetException(exception);
        return true;
    }

    /// <summary>
    /// Attempts to cancel the request.
    /// Returns false if already completed.
    /// </summary>
    public bool TrySetCanceled()
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
        {
            return false;
        }

        // Mark as completed BEFORE invoking continuation.
        // SetException invokes the continuation synchronously, which may return
        // this instance to the pool.
        Volatile.Write(ref _state, 2);
        _core.SetException(new OperationCanceledException());
        return true;
    }

    private PooledResponseBuffer? ParseAndSliceResponse(PooledResponseBuffer pooledBuffer)
    {
        // Skip the response header (correlation ID already read, skip tagged fields if flexible)
        var offset = 4; // Correlation ID already parsed
        var bufferLength = pooledBuffer.Length;

        // Bounds check: ensure we have at least the correlation ID
        if (offset > bufferLength)
        {
            throw new InvalidOperationException(
                $"Response buffer too small for correlation ID: buffer length {bufferLength}, required offset {offset}");
        }

        if (_responseHeaderVersion >= 1)
        {
            // Skip tagged fields - read varint count and skip
            if (offset >= bufferLength)
            {
                throw new InvalidOperationException(
                    $"Response buffer truncated before tagged field count: offset {offset}, buffer length {bufferLength}");
            }

            var span = pooledBuffer.Data.Span[offset..];
            var (tagCount, bytesRead, tagCountSuccess) = ConnectionHelper.ReadUnsignedVarInt(span);
            if (!tagCountSuccess)
            {
                throw new InvalidOperationException(
                    $"Incomplete or malformed VarInt for tagged field count at offset {offset} (buffer length: {bufferLength})");
            }
            offset += bytesRead;

            // Sanity check: tag count should be reasonable (< 1000)
            if (tagCount > 1000)
            {
                throw new InvalidOperationException(
                    $"Unreasonable tagged field count {tagCount} at offset {offset - bytesRead} (buffer length: {bufferLength}). Possible data corruption.");
            }

            for (var i = 0; i < tagCount; i++)
            {
                if (offset >= bufferLength)
                {
                    throw new InvalidOperationException(
                        $"Response buffer truncated while reading tag key at offset {offset} (buffer length: {bufferLength}, tag index: {i}/{tagCount})");
                }

                span = pooledBuffer.Data.Span[offset..];
                var (_, tagBytesRead, tagKeySuccess) = ConnectionHelper.ReadUnsignedVarInt(span);
                if (!tagKeySuccess)
                {
                    throw new InvalidOperationException(
                        $"Incomplete or malformed VarInt for tag key at offset {offset} (buffer length: {bufferLength}, tag index: {i}/{tagCount})");
                }
                offset += tagBytesRead;

                if (offset >= bufferLength)
                {
                    throw new InvalidOperationException(
                        $"Response buffer truncated while reading tag size at offset {offset} (buffer length: {bufferLength}, tag index: {i}/{tagCount})");
                }

                span = pooledBuffer.Data.Span[offset..];
                var (size, sizeBytesRead, tagSizeSuccess) = ConnectionHelper.ReadUnsignedVarInt(span);
                if (!tagSizeSuccess)
                {
                    throw new InvalidOperationException(
                        $"Incomplete or malformed VarInt for tag size at offset {offset} (buffer length: {bufferLength}, tag index: {i}/{tagCount})");
                }
                offset += sizeBytesRead + size;

                if (offset > bufferLength)
                {
                    throw new InvalidOperationException(
                        $"Tag data extends beyond buffer: offset {offset} after reading tag size {size} (buffer length: {bufferLength}, tag index: {i}/{tagCount})");
                }
            }
        }

        // Final bounds check
        if (offset > bufferLength)
        {
            throw new InvalidOperationException(
                $"Response header parsing exceeded buffer: final offset {offset}, buffer length {bufferLength}");
        }

        return pooledBuffer.Slice(offset);
    }

    /// <summary>
    /// Resets the request for reuse. Called when returning to pool.
    /// </summary>
    public void Reset()
    {
        // Dispose any pending buffer
        _pendingBuffer?.Dispose();
        _pendingBuffer = null;

        // Dispose cancellation registration
        _cancellationRegistration.Dispose();
        _cancellationRegistration = default;

        // Reset the core for reuse
        _core.Reset();
        _state = 0;
    }

    // IValueTaskSource implementation
    PooledResponseBuffer IValueTaskSource<PooledResponseBuffer>.GetResult(short token)
    {
        return _core.GetResult(token);
    }

    ValueTaskSourceStatus IValueTaskSource<PooledResponseBuffer>.GetStatus(short token)
    {
        return _core.GetStatus(token);
    }

    void IValueTaskSource<PooledResponseBuffer>.OnCompleted(
        Action<object?> continuation,
        object? state,
        short token,
        ValueTaskSourceOnCompletedFlags flags)
    {
        _core.OnCompleted(continuation, state, token, flags);
    }
}

