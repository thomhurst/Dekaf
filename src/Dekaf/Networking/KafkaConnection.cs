using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks.Sources;
using Dekaf.Errors;
using Dekaf.Internal;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Security;
using Dekaf.Security.Sasl;
using Dekaf.Telemetry;
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
    // Minimum pause threshold for pipeline backpressure (1 MB)
    // Lowered from 16 MB: the input pipe only needs to buffer enough for the read pump
    // to stay ahead of response parsing. Producer responses are small (< 1 KB) and consumer
    // fetch responses are copied into PooledResponseBuffer immediately in TryReadResponse.
    private const long MinimumPauseThresholdBytes = 1L * 1024 * 1024;

    // Maximum pause threshold per connection (4 MB)
    // Caps per-connection buffering to prevent a single connection from retaining excessive
    // memory in the MemoryPool. Without this cap, a single-broker/single-connection setup
    // with 256 MB BufferMemory would allow 64 MB per pipe — far more than needed for
    // transient network buffering.
    private const long MaximumPauseThresholdBytes = 4L * 1024 * 1024;

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
    // - Total connections: 2 * 3 = 6
    // - Per-connection budget: 256 MB / 6 / 4 = 10.7 MB (capped to 4 MB)
    // - Total pipeline memory: 4 MB * 6 connections = 24 MB
    // - Producer batch memory: ~232 MB
    private const int BufferMemoryDivisor = 4;

    /// <summary>
    /// Calculates pipeline backpressure thresholds based on BufferMemory configuration.
    /// Divides the pipeline budget across ALL connections (brokers * connectionsPerBroker)
    /// and caps per-connection thresholds to prevent unbounded memory retention in the
    /// shared MemoryPool. Uses 1 MB floor for low-memory configurations.
    /// </summary>
    /// <param name="bufferMemory">Total producer BufferMemory in bytes</param>
    /// <param name="connectionsPerBroker">Number of connections per broker (must be positive)</param>
    /// <param name="brokerCount">Number of brokers (must be positive, defaults to 1 for backward compatibility)</param>
    /// <returns>Tuple of (pauseThreshold, resumeThreshold) in bytes</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when connectionsPerBroker or brokerCount is less than or equal to zero.
    /// </exception>
    public static (long PauseThreshold, long ResumeThreshold) CalculatePipelineThresholds(
        ulong bufferMemory,
        int connectionsPerBroker,
        int brokerCount = 1)
    {
        if (connectionsPerBroker <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(connectionsPerBroker),
                connectionsPerBroker,
                "Connections per broker must be positive");
        }

        if (brokerCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(brokerCount),
                brokerCount,
                "Broker count must be positive");
        }

        // Calculate per-pipe budget: BufferMemory / TotalConnections / BufferMemoryDivisor
        // TotalConnections = connectionsPerBroker * brokerCount
        // Division by BufferMemoryDivisor reserves 1/4 of per-connection memory for pipeline buffering
        var totalConnections = (ulong)connectionsPerBroker * (ulong)brokerCount;
        var perPipeBudget = bufferMemory / totalConnections / BufferMemoryDivisor;

        var pauseThreshold = Math.Clamp((long)perPipeBudget, MinimumPauseThresholdBytes, MaximumPauseThresholdBytes);

        var resumeThreshold = pauseThreshold / 2;

        return (pauseThreshold, resumeThreshold);
    }
}

/// <summary>
/// A multiplexed connection to a Kafka broker using System.IO.Pipelines.
/// </summary>
public sealed partial class KafkaConnection : IKafkaConnection, IIdleTrackedKafkaConnection
{
    private readonly string _host;
    private readonly int _port;
    private readonly string? _clientId;
    private readonly ILogger _logger;
    private readonly ConnectionOptions _options;
    private readonly ulong _bufferMemory;
    private readonly int _connectionsPerBroker;
    private readonly int _brokerCount;
    private readonly ResponseBufferPool _responseBufferPool;

    private Socket? _socket;
    private string? _resolvedTargetHost;
    private Stream? _stream;
    private PipeReader? _reader;
    private SocketPipe? _socketPipe;
    private DuplexPipe? _duplexPipe;
    private PipeMemoryPool? _pipeMemoryPool;

    // When non-null, this connection uses a shared pool owned by the ConnectionPool.
    // The connection must NOT dispose the shared pool — only its owner does.
    // When null, the connection creates and owns its own per-connection pool.
    private readonly PipeMemoryPool? _sharedPipeMemoryPool;

    // IMPORTANT: Use global correlation ID counter to prevent TCP port reuse issues.
    // When connections are rapidly closed and reopened, the OS may reuse local TCP ports.
    // If a late packet from an old connection arrives on a new connection with matching
    // correlation ID, it could be incorrectly matched to the wrong pending request.
    // Using globally unique correlation IDs prevents this issue.

    // Cap on cancelled correlation IDs to prevent unbounded growth when brokers
    // silently drop responses for timed-out requests. The tracker evicts oldest IDs
    // so recent cancellations continue suppressing late responses after the cap is hit.
    private const int MaxCancelledCorrelationIds = 10_000;
    private const int PendingRequestShardCount = 16;
    private const int MinimumResponseFrameSize = 4; // Correlation ID is always first in the response header.
    // SASL handshake/auth responses are small; this pre-auth path must not honor untrusted large frame claims.
    private const int MaxSaslResponseFrameSize = 1024 * 1024;
    private static int s_globalCorrelationId;
    private readonly PendingRequestShard[] _pendingRequestShards;
    private readonly SemaphoreSlim _pendingRequestSlots;
    private readonly CancellationTokenSource _pendingRequestSlotCts = new();
    private readonly TaskCompletionSource _pendingRequestSlotOperationsDrained =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _pendingRequestSlotOperationCount;
    private int _pendingRequestSlotsClosed;
    private int _pendingRequestCount;
    private readonly CancelledCorrelationIdTracker _cancelledCorrelationIds = new(MaxCancelledCorrelationIds);
    private readonly PendingRequestPool _pendingRequestPool;
    private readonly CancellationTokenSourcePool _timeoutCtsPool;
    private readonly ClientTelemetryMetricCollector? _telemetryMetricCollector;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // Partial frame assembly state for incremental response consumption.
    // Only accessed from ReceiveLoopAsync (single-threaded reader).
    private PartialFrameContext _partialFrame;

    private Task? _receiveTask;
    private CancellationTokenSource? _receiveCts;
    private OAuthBearerTokenProvider? _ownedTokenProvider;
    private int _disposed;
    private int _connected;
    private long _lastUsedTimestampMs = Dekaf.Compatibility.EnvironmentCompat.TickCount64;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    // SASL re-authentication (KIP-368)
    private SaslSessionState? _saslSessionState;
    private Timer? _reauthTimer;
    private readonly SemaphoreSlim _reauthLock = new(1, 1);
    private volatile bool _reauthenticating;

    // Certificates loaded from files that we own and must dispose
    private X509Certificate2Collection? _loadedCaCertificates;
    private X509Certificate2? _loadedClientCertificate;

    public int BrokerId { get; private set; } = -1;
    public string Host => _host;
    public int Port => _port;
    public bool IsConnected => Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _connected) != 0 && (_socket?.Connected ?? false);

    long IIdleTrackedKafkaConnection.LastUsedTimestampMs => Volatile.Read(ref _lastUsedTimestampMs);

    int IIdleTrackedKafkaConnection.PendingRequestCount => GetPendingRequestCount();

    void IIdleTrackedKafkaConnection.Touch() => Touch();

    /// <summary>
    /// Gets the current SASL session state, if SASL authentication was performed.
    /// Returns null if no SASL authentication occurred or if re-authentication is disabled.
    /// </summary>
    internal SaslSessionState? SaslSession => _saslSessionState;

    private readonly struct PendingRequestEntry(PooledPendingRequest request, short version)
    {
        public PooledPendingRequest Request { get; } = request;
        public short Version { get; } = version;
    }

    private sealed class PendingRequestShard
    {
        public PendingRequestShard(int capacity)
        {
            Requests = new Dictionary<int, PendingRequestEntry>(capacity);
        }

        public object Gate { get; } = new();
        public Dictionary<int, PendingRequestEntry> Requests { get; }
    }

    public KafkaConnection(
        string host,
        int port,
        string? clientId = null,
        ConnectionOptions? options = null,
        ILogger<KafkaConnection>? logger = null,
        ulong bufferMemory = 33554432,
        int connectionsPerBroker = 1,
        int brokerCount = 1)
        : this(host, port, clientId, options, logger, bufferMemory, connectionsPerBroker, brokerCount, ResponseBufferPool.Default)
    {
    }

    internal KafkaConnection(
        string host,
        int port,
        string? clientId,
        ConnectionOptions? options,
        ILogger<KafkaConnection>? logger,
        ulong bufferMemory,
        int connectionsPerBroker,
        int brokerCount,
        ResponseBufferPool responseBufferPool,
        ClientTelemetryMetricCollector? telemetryMetricCollector = null)
    {
        _host = host;
        _port = port;
        _clientId = clientId;
        _options = options ?? new ConnectionOptions();
        var connectionSizes = PoolSizing.ForConnection(_options.MaxInFlightRequestsPerConnection);
        _pendingRequestShards = CreatePendingRequestShards(connectionSizes.PendingRequests);
        _pendingRequestSlots = new SemaphoreSlim(connectionSizes.PendingRequests, connectionSizes.PendingRequests);
        _pendingRequestPool = new PendingRequestPool(connectionSizes.PendingRequests);
        _timeoutCtsPool = new CancellationTokenSourcePool(connectionSizes.CancellationTokenSources);
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<KafkaConnection>.Instance;
        _bufferMemory = bufferMemory;
        _connectionsPerBroker = connectionsPerBroker;
        _brokerCount = Math.Max(1, brokerCount);
        _responseBufferPool = responseBufferPool;
        _telemetryMetricCollector = telemetryMetricCollector;
    }

    public KafkaConnection(
        int brokerId,
        string host,
        int port,
        string? clientId = null,
        ConnectionOptions? options = null,
        ILogger<KafkaConnection>? logger = null,
        ulong bufferMemory = 33554432,
        int connectionsPerBroker = 1,
        int brokerCount = 1)
        : this(host, port, clientId, options, logger, bufferMemory, connectionsPerBroker, brokerCount, ResponseBufferPool.Default)
    {
        BrokerId = brokerId;
    }

    /// <summary>
    /// Internal constructor used by <see cref="ConnectionPool"/>. When <paramref name="sharedPipeMemoryPool"/>
    /// is provided, all connections sharing the same pool contribute to a single bounded set of
    /// cached arrays instead of each connection independently retaining up to <c>maxArraysPerBucket</c>
    /// arrays. This prevents WorkingSet growth proportional to connection count in multi-broker
    /// scenarios with adaptive scaling.
    /// </summary>
    internal KafkaConnection(
        int brokerId,
        string host,
        int port,
        string? clientId,
        ConnectionOptions? options,
        ILogger<KafkaConnection>? logger,
        ulong bufferMemory,
        int connectionsPerBroker,
        int brokerCount,
        ResponseBufferPool responseBufferPool,
        PipeMemoryPool? sharedPipeMemoryPool = null,
        ClientTelemetryMetricCollector? telemetryMetricCollector = null)
        : this(host, port, clientId, options, logger, bufferMemory, connectionsPerBroker, brokerCount, responseBufferPool, telemetryMetricCollector)
    {
        BrokerId = brokerId;
        _sharedPipeMemoryPool = sharedPipeMemoryPool;
    }

    private static PendingRequestShard[] CreatePendingRequestShards(int capacity)
    {
        var shardCount = Math.Min(PendingRequestShardCount, capacity);
        var capacityPerShard = Math.Max(1, (capacity + shardCount - 1) / shardCount);
        var shards = new PendingRequestShard[shardCount];

        for (var i = 0; i < shards.Length; i++)
            shards[i] = new PendingRequestShard(capacityPerShard);

        return shards;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PendingRequestShard GetPendingRequestShard(int correlationId)
        => _pendingRequestShards[(int)((uint)correlationId % (uint)_pendingRequestShards.Length)];

    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConnection));

        if (IsConnected)
            return;

        await SemaphoreHelper.AcquireOrThrowDisposedAsync(_connectLock, nameof(KafkaConnection), cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(KafkaConnection));

            // Another caller completed connection while we waited
            if (IsConnected)
                return;

            await ConnectCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_connectLock);
        }
    }

    private async ValueTask ConnectCoreAsync(CancellationToken cancellationToken)
    {
        LogConnecting(_host, _port);

        using var connectTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectTimeoutCts.CancelAfter(_options.ConnectionTimeout);
        var (socket, targetHost) = await ConnectSocketAsync(connectTimeoutCts.Token).ConfigureAwait(false);
        _socket = socket;
        _resolvedTargetHost = targetHost;

        Stream networkStream = new NetworkStream(_socket, ownsSocket: false);

        if (_options.UseTls || _options.TlsConfig is not null)
        {
#if NETSTANDARD2_0
            var sslStream = new SslStream(
                networkStream,
                leaveInnerStreamOpen: false,
                BuildRemoteCertificateValidationCallback());
#else
            var sslStream = new SslStream(networkStream, leaveInnerStreamOpen: false);
#endif
            try
            {
#if NETSTANDARD2_0
                await AuthenticateAsClientNetStandardAsync(sslStream, targetHost, cancellationToken)
                    .ConfigureAwait(false);
#else
                var sslOptions = BuildSslClientAuthenticationOptions(targetHost);
                await sslStream.AuthenticateAsClientAsync(sslOptions, cancellationToken).ConfigureAwait(false);
#endif
            }
            catch (System.Security.Authentication.AuthenticationException ex)
            {
                // TLS handshake failures surface here — primarily a rejected certificate, but .NET
                // also throws this type for negotiation/protocol mismatches. We deliberately treat
                // them all as fatal: TLS configuration is cluster-wide, so the same failure would
                // recur on every broker, and trying the rest only delays surfacing the real cause.
                // Wrap as a Dekaf AuthenticationException (a KafkaException) so callers can catch it
                // and metadata refresh treats it as fatal instead of masking it behind a generic
                // "failed to refresh metadata" error.
                await sslStream.DisposeAsync().ConfigureAwait(false);
                throw new AuthenticationException($"TLS handshake failed: {ex.Message}", ex);
            }
            networkStream = sslStream;
        }

        _stream = networkStream;

        // Perform SASL authentication if configured
        if (_options.SaslMechanism != SaslMechanism.None)
        {
            await PerformSaslAuthenticationAsync(cancellationToken).ConfigureAwait(false);
        }

        // Calculate pipeline backpressure thresholds based on BufferMemory,
        // dividing across all connections (brokers * connectionsPerBroker)
        var (pauseThreshold, resumeThreshold) = ConnectionHelper.CalculatePipelineThresholds(
            _bufferMemory,
            _connectionsPerBroker,
            _brokerCount);

        LogConfiguringPipe(BrokerId, pauseThreshold, resumeThreshold);

        // Use a shared pool if provided by the ConnectionPool, otherwise create a per-connection
        // pool. The shared pool bounds total retained memory across all connections to a single
        // set of cached arrays, preventing WorkingSet growth proportional to connection count
        // in multi-broker scenarios. Per-connection pools are used as a fallback for connections
        // created outside of a ConnectionPool (e.g., in tests).
        if (_sharedPipeMemoryPool is not null)
        {
            // Shared pool — don't dispose the previous reference (it's the same shared instance).
            // On reconnect, _pipeMemoryPool already equals _sharedPipeMemoryPool; this is a
            // harmless no-op reassignment that keeps the code path uniform.
            _pipeMemoryPool = _sharedPipeMemoryPool;
        }
        else
        {
            // Per-connection pool prevents WorkingSet growth from ArrayPool<byte>.Shared's
            // TLS caches that grow per-thread but never shrink. See PipeMemoryPool for details.
            _pipeMemoryPool?.Dispose();
            _pipeMemoryPool = new PipeMemoryPool();
        }

        // Use a read pump to decouple reads from PipeReader signaling.
        // PipeReader.Create(Stream).ReadAsync can block indefinitely when concurrent reads
        // and writes target the same underlying socket. The pump reads into an internal Pipe,
        // ensuring PipeReader.ReadAsync always wakes promptly when data arrives.
        // PipeScheduler.Inline eliminates thread pool context switches — the reader continuation
        // runs directly on the pump thread (like Kestrel's SocketConnection).
        var inputPipeOptions = new PipeOptions(
            pool: _pipeMemoryPool,
            readerScheduler: PipeScheduler.Inline,
            writerScheduler: PipeScheduler.Inline,
            minimumSegmentSize: _options.MinimumSegmentSize,
            pauseWriterThreshold: pauseThreshold,
            resumeWriterThreshold: resumeThreshold,
            useSynchronizationContext: false);

        var readBufferSize = _options.ReceiveBufferSize > 0 ? _options.ReceiveBufferSize : 65536;

        if (_stream is NetworkStream plainStream)
        {
            // Plain TCP: read directly from the Socket, bypassing the Stream abstraction.
            // SocketPipe takes full ownership of both the socket and the NetworkStream.
            _socketPipe = new SocketPipe(_socket!, plainStream, inputPipeOptions, readBufferSize);
            _reader = _socketPipe.Input;
        }
        else
        {
            // TLS: SslStream requires the Stream abstraction for decryption.
            // DuplexPipe takes full ownership of the SslStream and the socket.
            _duplexPipe = new DuplexPipe(_stream, _socket!, inputPipeOptions, readBufferSize);
            _reader = _duplexPipe.Input;
        }

        _receiveCts = new CancellationTokenSource();
        _receiveTask = ReceiveLoopAsync(_receiveCts.Token);
        Touch();
        Volatile.Write(ref _connected, 1);
        _telemetryMetricCollector?.RecordConnectionCreated();

        LogConnected(_host, _port);
    }

    private async ValueTask<(Socket Socket, string TargetHost)> ConnectSocketAsync(CancellationToken cancellationToken)
    {
        var endpoints = await _options.DnsResolver
            .ResolveAsync(_host, _port, _options.ClientDnsLookup, cancellationToken)
            .ConfigureAwait(false);

        if (endpoints.Count == 0)
            throw new SocketException((int)SocketError.HostNotFound);

        Exception? lastException = null;
        foreach (var endpoint in endpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var socket = CreateSocket(endpoint.Address.AddressFamily);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(endpoint.Address, endpoint.Port), cancellationToken)
                    .ConfigureAwait(false);

                _options.DnsResolver.MarkSuccessful(_host, _port, _options.ClientDnsLookup, endpoint.Address);
                return (socket, endpoint.TargetHost);
            }
            catch (OperationCanceledException)
            {
                socket.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                socket.Dispose();
                lastException = ex;
            }
        }

        if (lastException is not null)
            ExceptionDispatchInfo.Capture(lastException).Throw();

        throw new SocketException((int)SocketError.HostNotFound);
    }

    private Socket CreateSocket(AddressFamily addressFamily)
    {
        var socket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };

        if (_options.SendBufferSize > 0)
            socket.SendBufferSize = _options.SendBufferSize;
        if (_options.ReceiveBufferSize > 0)
            socket.ReceiveBufferSize = _options.ReceiveBufferSize;
        ConfigureTcpKeepAlive(socket);

        return socket;
    }

    public async ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConnection));

        if (!IsConnected)
            throw new InvalidOperationException("Not connected");

        Touch();
        var correlationId = Interlocked.Increment(ref s_globalCorrelationId);
        var headerVersion = KafkaMessageMetadata<TRequest, TResponse>.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = KafkaMessageMetadata<TRequest, TResponse>.GetResponseHeaderVersion(apiVersion);

        await ReservePendingRequestSlotAsync(cancellationToken).ConfigureAwait(false);
        var pending = _pendingRequestPool.Rent();
        pending.Initialize(responseHeaderVersion, cancellationToken, registerCancellation: false);
        try
        {
            AddPendingRequest(correlationId, pending);
        }
        catch
        {
            ReleasePendingRequestSlot();
            _pendingRequestPool.Return(pending);
            throw;
        }

        ThrowIfDisposedAfterAddingPendingRequest(correlationId);

        try
        {
            var telemetryStartTimestamp = _telemetryMetricCollector is null ? 0 : Stopwatch.GetTimestamp();

            // Write phase
            LogSendingRequest(KafkaMessageMetadata<TRequest, TResponse>.ApiKey, correlationId, apiVersion, _host, _port);

            await PreSerializeAndWriteAsync<TRequest, TResponse>(request, correlationId, apiVersion, headerVersion, cancellationToken)
                .ConfigureAwait(false);

            LogRequestSentWaitingForResponse(correlationId);

            // Response phase: await response with timeout and parse
            var response = await AwaitAndParseResponseAsync<TRequest, TResponse>(
                pending, correlationId, apiVersion, callerOwnsTimeout: false, cancellationToken).ConfigureAwait(false);
            Touch();
            _telemetryMetricCollector?.RecordRequestLatency(BrokerId, telemetryStartTimestamp);
            return response;
        }
        catch
        {
            // Clean up pending request on any failure (write lock cancelled, write failed,
            // or response error). AwaitAndParseResponseAsync has its own finally that also
            // tries TryRemove — the second attempt harmlessly returns false.
            if (TryRemovePendingRequest(correlationId, out var removed))
            {
                _pendingRequestPool.Return(removed.Request);
            }

            throw;
        }
    }

    public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
        => SendFireAndForgetCoreAsync<TRequest, TResponse>(request, apiVersion, callerOwnsTimeout: false, cancellationToken);

    /// <summary>
    /// Fire-and-forget overload that skips the per-write CancellationTokenSource rent and
    /// CancellationTokenRegistration allocation. The caller's token must already carry a
    /// timeout (e.g. BrokerSender's sendTimeoutCts).
    /// </summary>
    /// <remarks>
    /// The caller's token MUST be exclusively a timeout token (e.g., from a CancellationTokenSource
    /// configured with only CancelAfter). Do NOT pass a linked user-cancellation token;
    /// use the standard Send methods instead, which correctly distinguish timeout from explicit cancellation.
    /// If the token does not carry a timeout, flush operations may block indefinitely.
    /// </remarks>
    public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
        => SendFireAndForgetCoreAsync<TRequest, TResponse>(request, apiVersion, callerOwnsTimeout: true, cancellationToken);

    private async ValueTask SendFireAndForgetCoreAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        bool callerOwnsTimeout,
        CancellationToken cancellationToken)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConnection));

        if (!IsConnected)
            throw new InvalidOperationException("Not connected");

        Touch();
        var correlationId = Interlocked.Increment(ref s_globalCorrelationId);
        var headerVersion = KafkaMessageMetadata<TRequest, TResponse>.GetRequestHeaderVersion(apiVersion);

        // Don't register a pending request - we won't receive a response

        LogSendingFireAndForgetRequest(KafkaMessageMetadata<TRequest, TResponse>.ApiKey, correlationId, apiVersion, _host, _port);

        await PreSerializeAndWriteAsync<TRequest, TResponse>(request, correlationId, apiVersion, headerVersion, cancellationToken, callerOwnsTimeout)
            .ConfigureAwait(false);

        Touch();
        LogFireAndForgetRequestSent(correlationId);
    }

    public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
        => SendPipelinedCoreAsync<TRequest, TResponse>(request, apiVersion, callerOwnsTimeout: false, cancellationToken);

    /// <summary>
    /// Pipelined overload that skips the per-write CancellationTokenSource rent and
    /// CancellationTokenRegistration allocation. The caller's token must already carry a
    /// timeout (e.g. BrokerSender's sendTimeoutCts).
    /// </summary>
    /// <remarks>
    /// The caller's token MUST be exclusively a timeout token (e.g., from a CancellationTokenSource
    /// configured with only CancelAfter). Do NOT pass a linked user-cancellation token;
    /// use the standard Send methods instead, which correctly distinguish timeout from explicit cancellation.
    /// If the token does not carry a timeout, flush operations may block indefinitely.
    /// </remarks>
    public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
        => SendPipelinedCoreAsync<TRequest, TResponse>(request, apiVersion, callerOwnsTimeout: true, cancellationToken);

    private async Task<TResponse> SendPipelinedCoreAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        bool callerOwnsTimeout,
        CancellationToken cancellationToken)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConnection));

        if (!IsConnected)
            throw new InvalidOperationException("Not connected");

        Touch();
        var correlationId = Interlocked.Increment(ref s_globalCorrelationId);
        var headerVersion = KafkaMessageMetadata<TRequest, TResponse>.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = KafkaMessageMetadata<TRequest, TResponse>.GetResponseHeaderVersion(apiVersion);

        await ReservePendingRequestSlotAsync(cancellationToken).ConfigureAwait(false);
        var pending = _pendingRequestPool.Rent();
        pending.Initialize(responseHeaderVersion, cancellationToken, registerCancellation: false);
        try
        {
            AddPendingRequest(correlationId, pending);
        }
        catch
        {
            ReleasePendingRequestSlot();
            _pendingRequestPool.Return(pending);
            throw;
        }

        ThrowIfDisposedAfterAddingPendingRequest(correlationId);

        try
        {
            var telemetryStartTimestamp = _telemetryMetricCollector is null ? 0 : Stopwatch.GetTimestamp();

            await PreSerializeAndWriteAsync<TRequest, TResponse>(request, correlationId, apiVersion, headerVersion, cancellationToken, callerOwnsTimeout)
                .ConfigureAwait(false);

            // Response phase: await response with timeout, then parse
            var response = await AwaitAndParseResponseAsync<TRequest, TResponse>(
                pending, correlationId, apiVersion, callerOwnsTimeout, cancellationToken).ConfigureAwait(false);
            Touch();
            _telemetryMetricCollector?.RecordRequestLatency(BrokerId, telemetryStartTimestamp);
            return response;
        }
        catch
        {
            // On failure, ensure we clean up the pending request
            if (TryRemovePendingRequest(correlationId, out var removed))
            {
                _pendingRequestPool.Return(removed.Request);
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
        bool callerOwnsTimeout,
        CancellationToken cancellationToken)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        var responseReceived = false;
        try
        {
            LogWaitingForResponse(correlationId);

            PooledResponseBuffer pooledBuffer;
            if (callerOwnsTimeout && cancellationToken.CanBeCanceled)
            {
                pending.RegisterCancellation(cancellationToken);
                try
                {
                    pooledBuffer = await pending.AsValueTask().ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw CreateResponseTimeoutException(KafkaMessageMetadata<TRequest, TResponse>.ApiKey, correlationId);
                }
                finally
                {
                    pending.DisposeRegistration();
                }
            }
            else
            {
                using var timeoutCts = _timeoutCtsPool.Rent();
                timeoutCts.CancelAfter(_options.RequestTimeout);
                using var reg = cancellationToken.CanBeCanceled
                    ? cancellationToken.Register(static s => ((CancellationTokenSource)s!).Cancel(), timeoutCts)
                    : default;
                pending.RegisterCancellation(timeoutCts.Token);

                try
                {
                    pooledBuffer = await pending.AsValueTask().ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    throw CreateResponseTimeoutException(KafkaMessageMetadata<TRequest, TResponse>.ApiKey, correlationId);
                }
                finally
                {
                    pending.DisposeRegistration();
                }
            }

            responseReceived = true;

            LogResponseReceived(correlationId);

            var isFetchResponse = KafkaMessageMetadata<TRequest, TResponse>.ApiKey == ApiKey.Fetch;

            if (isFetchResponse)
            {
                var memoryOwner = pooledBuffer.TransferOwnership();
                using var parsingScope = ResponseParsingContext.SetPooledMemory(memoryOwner);

                var reader = new KafkaProtocolReader(pooledBuffer.Data);
                var response = KafkaMessageMetadata<TRequest, TResponse>.ReadResponse(ref reader, apiVersion);

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
            else
            {
                try
                {
                    var reader = new KafkaProtocolReader(pooledBuffer.Data);
                    return KafkaMessageMetadata<TRequest, TResponse>.ReadResponse(ref reader, apiVersion);
                }
                finally
                {
                    pooledBuffer.Dispose();
                }
            }
        }
        finally
        {
            if (TryRemovePendingRequest(correlationId, out var removed))
            {
                _pendingRequestPool.Return(removed.Request);

                // Broker may still send a response for a timed-out/cancelled request.
                // Record it so the receive loop discards silently instead of warning.
                // If the receive loop already called TryComplete (and lost the CAS),
                // this entry becomes a harmless no-op cleaned up by DisposeAsync.
                if (!responseReceived)
                {
                    _cancelledCorrelationIds.TryAdd(correlationId);
                }
            }
        }
    }

    private TimeoutException CreateResponseTimeoutException(ApiKey apiKey, int correlationId) =>
        new($"Request {apiKey} (correlation {correlationId}) timed out after {_options.RequestTimeout.TotalSeconds}s waiting for response from {_host}:{_port}");

    /// <summary>
    /// Serializes a request (size prefix + header + body) directly into a pooled buffer,
    /// outside the write lock. Returns the rented array and the number of valid bytes.
    /// The caller must return the array to <see cref="ArrayPool{T}.Shared"/> after use.
    /// </summary>
    /// <remarks>
    /// Serialization runs outside the write lock to reduce lock hold time. The request is
    /// serialized directly into the rented array (starting at offset 4 to reserve space for
    /// the size prefix), eliminating the intermediate copy through a thread-local buffer.
    /// The ArrayPool rent/return is per-request to the broker (per-batch for produce, per-fetch
    /// for consume), so the overhead is negligible.
    /// </remarks>
    private (byte[] Buffer, int Length) PreSerializeRequest<TRequest, TResponse>(
        TRequest request,
        int correlationId,
        short apiVersion,
        short headerVersion)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        // Serialize directly into a rented array at offset 4 (reserving space for the size
        // prefix). The only copy is rented array -> PipeWriter under the write lock.
        using var writer = new RentedBufferWriter(initialCapacity: 4096, offset: 4);

        var bodyWriter = new KafkaProtocolWriter(writer);
        var header = new RequestHeader
        {
            ApiKey = KafkaMessageMetadata<TRequest, TResponse>.ApiKey,
            ApiVersion = apiVersion,
            CorrelationId = correlationId,
            ClientId = _clientId,
            HeaderVersion = headerVersion
        };

        header.Write(ref bodyWriter);
        request.Write(ref bodyWriter, apiVersion);

        // Backpatch the 4-byte size prefix at the start of the rented array.
        // DetachBuffer transfers ownership to the caller; Dispose becomes a no-op.
        var (rentedArray, totalLength) = writer.DetachBuffer();
        BinaryPrimitives.WriteInt32BigEndian(rentedArray, bodyWriter.BytesWritten);

        return (rentedArray, totalLength);
    }


    /// <summary>
    /// Pre-serializes a request outside the write lock, then acquires the lock to write
    /// the pre-serialized bytes and flush. This minimizes lock hold time by keeping
    /// CPU-bound serialization out of the critical section.
    /// </summary>
    private async ValueTask PreSerializeAndWriteAsync<TRequest, TResponse>(
        TRequest request,
        int correlationId,
        short apiVersion,
        short headerVersion,
        CancellationToken cancellationToken,
        bool callerOwnsTimeout = false)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        var (serializedArray, serializedLength) = PreSerializeRequest<TRequest, TResponse>(request, correlationId, apiVersion, headerVersion);
        var clearSerializedArray = KafkaMessageMetadata<TRequest, TResponse>.ApiKey == ApiKey.SaslAuthenticate;
        byte[]? arrayToReturn = serializedArray;
        try
        {
            await SemaphoreHelper.AcquireOrThrowDisposedAsync(_writeLock, nameof(KafkaConnection), cancellationToken)
                .ConfigureAwait(false);
            try
            {
                await WritePreSerializedToStreamAsync(
                        serializedArray,
                        serializedLength,
                        correlationId,
                        cancellationToken,
                        callerOwnsTimeout)
                    .ConfigureAwait(false);
            }
            finally
            {
                SemaphoreHelper.ReleaseSafely(_writeLock);
            }
        }
        finally
        {
            if (arrayToReturn is not null)
                DekafPools.SerializationBuffers.Return(arrayToReturn, clearArray: clearSerializedArray);
        }
    }

    private async ValueTask WritePreSerializedToStreamAsync(
        byte[] serializedData,
        int length,
        int correlationId,
        CancellationToken cancellationToken,
        bool callerOwnsTimeout = false)
    {
        if (_stream is null)
            throw new InvalidOperationException("Not connected");

#if DEBUG
        System.Diagnostics.Debug.Assert(!callerOwnsTimeout || cancellationToken.CanBeCanceled,
            "callerOwnsTimeout path requires a timeout-bearing token");
#endif

        var memory = serializedData.AsMemory(0, length);

        // Apply timeout to the write operation.
        // When callerOwnsTimeout is true, the caller's cancellationToken already carries
        // a timeout (e.g. BrokerSender's sendTimeoutCts), so we skip the per-write CTS
        // rent and CancellationTokenRegistration allocation — a hot-path optimization.
        if (callerOwnsTimeout)
        {
            try
            {
                await _stream.WriteAsync(memory, cancellationToken).ConfigureAwait(false);
            }
            // callerOwnsTimeout contract: token fires only on timeout, never explicit user cancellation.
            // Any OperationCanceledException here means the caller's timeout elapsed.
            catch (OperationCanceledException)
            {
                LogFlushTimeout(_options.RequestTimeout.TotalMilliseconds, correlationId, BrokerId);

                throw new KafkaException(
                    $"Flush timeout after {(int)_options.RequestTimeout.TotalMilliseconds}ms on connection to broker {BrokerId}");
            }
        }
        else
        {
            using var timeoutCts = _timeoutCtsPool.Rent();
            timeoutCts.CancelAfter(_options.RequestTimeout);
            using var reg = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(static s => ((CancellationTokenSource)s!).Cancel(), timeoutCts)
                : default;

            try
            {
                await _stream.WriteAsync(memory, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                LogFlushTimeout(_options.RequestTimeout.TotalMilliseconds, correlationId, BrokerId);

                throw new KafkaException(
                    $"Flush timeout after {(int)_options.RequestTimeout.TotalMilliseconds}ms on connection to broker {BrokerId}");
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        if (_reader is null)
            return;

        LogReceiveLoopStarted(_host, _port);

        try
        {
            var timeoutCts = _timeoutCtsPool.Rent();
            var reg = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(static s => ((CancellationTokenSource)s!).Cancel(), timeoutCts)
                : default;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    ReadResult result;
                    var readHasRequestTimeout = HasPendingRequests();
                    try
                    {
                        timeoutCts.TryReset();

                        // Only arm RequestTimeout while a response is outstanding. Idle
                        // connections are governed by ConnectionsMaxIdleMs instead.
                        if (readHasRequestTimeout)
                            timeoutCts.CancelAfter(_options.RequestTimeout);

                        // ReadResult state machine (System.IO.Pipelines):
                        //
                        // ReadAsync can complete in three ways:
                        //
                        // 1. IsCompleted=false, IsCanceled=false (normal data available):
                        //    Data is available in result.Buffer. Process all complete responses,
                        //    then call AdvanceTo to indicate consumed/examined positions.
                        //
                        // 2. IsCompleted=true:
                        //    The PipeWriter was completed (end of stream). For PipeReader.Create(stream),
                        //    this means the underlying stream reached EOF — the remote peer closed the
                        //    connection. Any remaining data in the buffer is still valid and must be
                        //    processed before exiting. We process responses first, then break out of
                        //    the receive loop.
                        //
                        // 3. IsCanceled=true:
                        //    The read was canceled. With Pipe.Reader (used via SocketPipe/DuplexPipe since
                        //    PR #458), CancellationToken-based cancellation returns IsCanceled=true instead
                        //    of throwing OperationCanceledException. Both paths must be handled.
                        result = await _reader!.ReadAsync(readHasRequestTimeout ? timeoutCts.Token : cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (readHasRequestTimeout && timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        if (!HasPendingRequests())
                            continue;

                        throw CreateReceiveTimeoutException();
                    }

                    // Pipe.Reader.ReadAsync returns IsCanceled=true on token cancellation instead of
                    // throwing OperationCanceledException (unlike PipeReader.Create(Stream)). Without
                    // this check, timeouts are silently swallowed: the connection stays in the pool
                    // appearing healthy, and all subsequent requests on it also time out (#670).
                    if (result.IsCanceled)
                    {
                        if (readHasRequestTimeout && timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                        {
                            if (!HasPendingRequests())
                            {
                                _reader!.AdvanceTo(result.Buffer.Start, result.Buffer.Start);
                                continue;
                            }

                            throw CreateReceiveTimeoutException();
                        }

                        if (!cancellationToken.IsCancellationRequested)
                        {
                            _reader!.AdvanceTo(result.Buffer.Start, result.Buffer.Start);
                            continue;
                        }

                        // Outer cancellation (disposal) — exit cleanly
                        break;
                    }

                    var buffer = result.Buffer;

                    LogReceivedBytes(buffer.Length, _host, _port);

                    // Phase 1: Continue assembling a partial frame if one is in progress.
                    // The partial frame consumes data from the pipe buffer as it copies,
                    // keeping the buffer below the pause threshold for large responses.
                    if (_partialFrame.IsActive)
                    {
                        if (ContinuePartialFrame(ref buffer, ref _partialFrame))
                        {
                            // Frame complete — dispatch it
                            var responseData = new PooledResponseBuffer(
                                _partialFrame.Buffer!, _partialFrame.FrameSize,
                                _partialFrame.IsPooled, pool: _responseBufferPool);
                            DispatchResponse(_partialFrame.CorrelationId, responseData);
                            _partialFrame = default;
                        }
                    }

                    // Phase 2: Process any complete responses available in the buffer.
                    // This is the fast path for small responses (e.g., producer acks)
                    // that fit entirely within the pipe buffer.
                    if (!_partialFrame.IsActive)
                    {
                        while (TryReadResponse(ref buffer, out var correlationId, out var responseData))
                        {
                            DispatchResponse(correlationId, responseData);
                        }

                        // Phase 3: Start incremental assembly for frames that don't fit
                        // in the current buffer, keeping the pipe below the pause threshold.
                        if (buffer.Length >= 8)
                        {
                            TryStartPartialFrame(ref buffer, ref _partialFrame, _responseBufferPool);
                        }
                    }

                    _reader!.AdvanceTo(buffer.Start, buffer.End);

                    // After processing all available responses, check if the stream has ended.
                    // IsCompleted=true means the remote peer closed the connection (EOF).
                    // The remote peer closed the connection. Fail any pending requests
                    // immediately so callers don't hang waiting for responses that will
                    // never arrive. Without this, pending requests rely on their individual
                    // RequestTimeout (30s default), which delays error detection and can
                    // cause indefinite hangs when combined with BrokerSender retry cycles.
                    if (result.IsCompleted)
                    {
                        LogReceiveLoopCompleted(_host, _port);
                        MarkDisposed(); // Prevent new requests from being queued on a dead connection
                        FailAllPendingRequests(new KafkaException(
                            "Connection closed by remote peer (EOF)"));
                        break;
                    }
                }

                // While loop exited normally (no exception thrown, so no catch block runs).
                // Two cases: cancellation between iterations, or EOF break (already handled above).
                if (cancellationToken.IsCancellationRequested)
                {
                    MarkDisposed();
                    FailAllPendingRequests(new OperationCanceledException("Connection closing", cancellationToken));
                }
            }
            finally
            {
                reg.Dispose();
                timeoutCts.Dispose();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown — fail any pending requests so callers don't hang
            MarkDisposed();
            FailAllPendingRequests(new OperationCanceledException("Connection closing", cancellationToken));
        }
        catch (Exception ex)
        {
            LogReceiveLoopError(ex);
            MarkDisposed(); // Prevent new requests from being queued on a dead connection
            FailAllPendingRequests(ex);
        }
    }

    private KafkaException CreateReceiveTimeoutException()
    {
        LogReceiveTimeout(_options.RequestTimeout.TotalMilliseconds, BrokerId);
        MarkDisposed();

        return new KafkaException(
            $"Receive timeout after {(int)_options.RequestTimeout.TotalMilliseconds}ms - connection to broker {BrokerId} failed");
    }

    private void DispatchResponse(int correlationId, PooledResponseBuffer responseData)
    {
        LogReceivedResponse(correlationId, responseData.Length);

        if (TryGetPendingRequest(correlationId, out var pending))
        {
            if (!pending.Request.TryComplete(pending.Version, responseData))
            {
                responseData.Dispose();
            }
        }
        else if (_cancelledCorrelationIds.TryRemove(correlationId))
        {
            LogLateResponseForCancelledRequest(correlationId);
            responseData.Dispose();
        }
        else
        {
            LogUnknownCorrelationId(correlationId);
            responseData.Dispose();
        }
    }

    private ValueTask ReservePendingRequestSlotAsync(CancellationToken cancellationToken)
    {
        if (!TryEnterPendingRequestSlotOperation())
            throw new ObjectDisposedException(nameof(KafkaConnection));

        try
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(KafkaConnection));

            if (_pendingRequestSlots.Wait(0, cancellationToken))
            {
                if (Volatile.Read(ref _disposed) == 0)
                    return ValueTaskCompatibility.CompletedTask;

                _pendingRequestSlots.Release();
                throw new ObjectDisposedException(nameof(KafkaConnection));
            }
        }
        catch (ObjectDisposedException) when (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _pendingRequestSlotsClosed) != 0)
        {
            throw new ObjectDisposedException(nameof(KafkaConnection));
        }
        finally
        {
            ExitPendingRequestSlotOperation();
        }

        return ReservePendingRequestSlotSlowAsync(cancellationToken);
    }

    private async ValueTask ReservePendingRequestSlotSlowAsync(CancellationToken cancellationToken)
    {
        if (!TryEnterPendingRequestSlotOperation())
            throw new ObjectDisposedException(nameof(KafkaConnection));

        try
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(KafkaConnection));

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _pendingRequestSlotCts.Token);

            await _pendingRequestSlots.WaitAsync(linkedCts.Token).ConfigureAwait(false);

            if (Volatile.Read(ref _disposed) == 0)
                return;

            _pendingRequestSlots.Release();
            throw new ObjectDisposedException(nameof(KafkaConnection));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (Volatile.Read(ref _disposed) != 0 && !cancellationToken.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(KafkaConnection));
        }
        catch (ObjectDisposedException) when (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _pendingRequestSlotsClosed) != 0)
        {
            throw new ObjectDisposedException(nameof(KafkaConnection));
        }
        finally
        {
            ExitPendingRequestSlotOperation();
        }
    }

    private void ReleasePendingRequestSlot()
    {
        if (!TryEnterPendingRequestSlotOperation())
            return;

        try
        {
            _pendingRequestSlots.Release();
        }
        catch (ObjectDisposedException) when (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _pendingRequestSlotsClosed) != 0)
        {
        }
        finally
        {
            ExitPendingRequestSlotOperation();
        }
    }

    private void ReleasePendingRequestSlots(int count)
    {
        if (count <= 0 || !TryEnterPendingRequestSlotOperation())
            return;

        try
        {
            _pendingRequestSlots.Release(count);
        }
        catch (ObjectDisposedException) when (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _pendingRequestSlotsClosed) != 0)
        {
        }
        finally
        {
            ExitPendingRequestSlotOperation();
        }
    }

    private void MarkDisposed()
    {
        Volatile.Write(ref _disposed, 1);
        CancelPendingRequestSlotWaiters();
    }

    private void CancelPendingRequestSlotWaiters()
    {
        try
        {
            _pendingRequestSlotCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private bool TryEnterPendingRequestSlotOperation()
    {
        if (Volatile.Read(ref _pendingRequestSlotsClosed) != 0)
            return false;

        Interlocked.Increment(ref _pendingRequestSlotOperationCount);
        if (Volatile.Read(ref _pendingRequestSlotsClosed) == 0)
            return true;

        ExitPendingRequestSlotOperation();
        return false;
    }

    private void ExitPendingRequestSlotOperation()
    {
        var count = Interlocked.Decrement(ref _pendingRequestSlotOperationCount);
        Debug.Assert(count >= 0);

        if (count == 0 && Volatile.Read(ref _pendingRequestSlotsClosed) != 0)
        {
            _pendingRequestSlotOperationsDrained.TrySetResult();
        }
    }

    private Task ClosePendingRequestSlotsAsync()
    {
        Volatile.Write(ref _pendingRequestSlotsClosed, 1);

        return Volatile.Read(ref _pendingRequestSlotOperationCount) == 0
            ? Task.CompletedTask
            : _pendingRequestSlotOperationsDrained.Task;
    }

    private void AddPendingRequest(int correlationId, PooledPendingRequest request)
    {
        var shard = GetPendingRequestShard(correlationId);
        lock (shard.Gate)
        {
            if (!shard.Requests.TryAdd(correlationId, new PendingRequestEntry(request, request.Version)))
                throw new InvalidOperationException($"Duplicate pending request correlation id {correlationId}.");
        }

        if (Interlocked.Increment(ref _pendingRequestCount) == 1)
            _reader?.CancelPendingRead();
    }

    private bool TryGetPendingRequest(int correlationId, out PendingRequestEntry entry)
    {
        var shard = GetPendingRequestShard(correlationId);
        lock (shard.Gate)
        {
            return shard.Requests.TryGetValue(correlationId, out entry);
        }
    }

    private bool TryRemovePendingRequest(int correlationId, out PendingRequestEntry entry)
    {
        bool removed;
        var shard = GetPendingRequestShard(correlationId);
        lock (shard.Gate)
        {
            removed = shard.Requests.Remove(correlationId, out entry);
        }

        if (removed)
        {
            var remaining = Interlocked.Decrement(ref _pendingRequestCount);
            Debug.Assert(remaining >= 0);

            if (remaining == 0)
                _reader?.CancelPendingRead();

            ReleasePendingRequestSlot();
        }

        return removed;
    }

    private int GetPendingRequestCount() => Volatile.Read(ref _pendingRequestCount);

    private bool HasPendingRequests() => Volatile.Read(ref _pendingRequestCount) != 0;

    private void Touch() => Volatile.Write(ref _lastUsedTimestampMs, Dekaf.Compatibility.EnvironmentCompat.TickCount64);

    private bool TryReadResponse(
        ref ReadOnlySequence<byte> buffer,
        out int correlationId,
        out PooledResponseBuffer responseData)
    {
        correlationId = 0;
        responseData = default;

        if (buffer.Length < 4)
            return false;

        // Single-segment fast path: avoids ReadOnlySequence per-segment iteration overhead
        if (buffer.IsSingleSegment)
        {
            return TryReadResponseSingleSegment(ref buffer, out correlationId, out responseData);
        }

        // Read size prefix
        Span<byte> sizeBuffer = stackalloc byte[4];
        buffer.Slice(0, 4).CopyTo(sizeBuffer);
        var size = BinaryPrimitives.ReadInt32BigEndian(sizeBuffer);
        ValidateResponseFrameSize(size, _responseBufferPool.MaxArrayLength);

        var frameLength = 4L + size;
        if (buffer.Length < frameLength)
            return false;

        // Extract response data (including header)
        var responseBuffer = buffer.Slice(4, size);

        // Read correlation ID from response header
        Span<byte> correlationBuffer = stackalloc byte[4];
        responseBuffer.Slice(0, 4).CopyTo(correlationBuffer);
        correlationId = BinaryPrimitives.ReadInt32BigEndian(correlationBuffer);

        var (responseArray, isPooled) = RentResponseArray(size);

        responseBuffer.CopyTo(responseArray);
        responseData = new PooledResponseBuffer(responseArray, size, isPooled, pool: _responseBufferPool);

        buffer = buffer.Slice(frameLength);
        return true;
    }

    /// <summary>
    /// Fast path for single-segment buffers. Reads size prefix, correlation ID, and copies
    /// response data using Span operations, avoiding ReadOnlySequence per-segment iteration overhead.
    /// </summary>
    private bool TryReadResponseSingleSegment(
        ref ReadOnlySequence<byte> buffer,
        out int correlationId,
        out PooledResponseBuffer responseData)
    {
        correlationId = 0;
        responseData = default;

        var span = buffer.First.Span;

        // Read size prefix directly from span
        var size = BinaryPrimitives.ReadInt32BigEndian(span);
        ValidateResponseFrameSize(size, _responseBufferPool.MaxArrayLength);

        var frameLength = 4L + size;
        if (span.Length < frameLength)
            return false;

        // Read correlation ID directly from span (offset 4 = past size prefix)
        correlationId = BinaryPrimitives.ReadInt32BigEndian(span.Slice(4));

        var (responseArray, isPooled) = RentResponseArray(size);

        // Copy using Span.CopyTo — single memcpy, no segment iteration
        span.Slice(4, size).CopyTo(responseArray);
        responseData = new PooledResponseBuffer(responseArray, size, isPooled, pool: _responseBufferPool);

        buffer = buffer.Slice(frameLength);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ValidateResponseFrameSize(int frameSize, int maxFrameSize)
    {
        if (frameSize < MinimumResponseFrameSize || frameSize > maxFrameSize)
        {
            throw new KafkaException(
                $"Invalid response frame size {frameSize}. Expected between {MinimumResponseFrameSize} and {maxFrameSize} bytes.");
        }
    }

    /// <summary>
    /// Rents a response buffer from the dedicated pool, or allocates directly for oversized responses.
    /// Multi-partition fetch responses (e.g., 6 partitions x 1MB) easily exceed 4MB.
    /// Unpooled responses go to LOH and require Gen2 GC to reclaim.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (byte[] Array, bool IsPooled) RentResponseArray(int size)
    {
        ValidateResponseFrameSize(size, _responseBufferPool.MaxArrayLength);

        if (size <= _responseBufferPool.MaxArrayLength)
        {
            return (_responseBufferPool.Pool.Rent(size), true);
        }

        return (new byte[size], false);
    }

    /// <summary>
    /// After adding a pending request, double-check that the connection hasn't been disposed.
    /// Closes the race where FailAllPendingRequests iterates the dictionary between our first
    /// _disposed check and the dictionary add. If _disposed was written between the two checks,
    /// FailAllPendingRequests may have already iterated past our entry.
    /// </summary>
    private void ThrowIfDisposedAfterAddingPendingRequest(int correlationId)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            if (TryRemovePendingRequest(correlationId, out var removed))
            {
                _pendingRequestPool.Return(removed.Request);
            }
            throw new ObjectDisposedException(nameof(KafkaConnection));
        }
    }

    private void FailAllPendingRequests(Exception ex)
    {
        // Clean up any partial frame being assembled
        if (_partialFrame.IsActive)
        {
            new PooledResponseBuffer(
                _partialFrame.Buffer!, _partialFrame.FrameSize,
                _partialFrame.IsPooled, pool: _responseBufferPool).Dispose();
            _partialFrame = default;
        }

        // Do NOT remove from _pendingRequests here. The awaiter's finally block in
        // AwaitAndParseResponseAsync (or the catch in SendAsync/SendPipelinedAsync)
        // will TryRemove and return the request to the pool.
        //
        // If we remove here, the awaiter can't find the request in the table,
        // so it never returns it to the pool — causing a pool leak that forces
        // constant allocation of new PooledPendingRequest objects.
        foreach (var shard in _pendingRequestShards)
        {
            lock (shard.Gate)
            {
                foreach (var slot in shard.Requests.Values)
                    slot.Request.TrySetException(slot.Version, ex);
            }
        }
    }

    private void ReturnAllPendingRequestsToPool()
    {
        var releasedSlots = 0;
        foreach (var shard in _pendingRequestShards)
        {
            lock (shard.Gate)
            {
                foreach (var slot in shard.Requests.Values)
                {
                    _pendingRequestPool.Return(slot.Request);
                    releasedSlots++;
                }

                shard.Requests.Clear();
            }
        }

        if (releasedSlots != 0)
        {
            var remaining = Interlocked.Add(ref _pendingRequestCount, -releasedSlots);
            Debug.Assert(remaining >= 0);
        }

        ReleasePendingRequestSlots(releasedSlots);
    }

#if NETSTANDARD2_0
    private async ValueTask AuthenticateAsClientNetStandardAsync(
        SslStream sslStream,
        string targetHost,
        CancellationToken cancellationToken)
    {
        var tlsConfig = _options.TlsConfig;
        var clientCertificates = BuildClientCertificates(tlsConfig);
        var enabledProtocols = tlsConfig?.EnabledSslProtocols ?? System.Security.Authentication.SslProtocols.None;
        var checkCertificateRevocation = tlsConfig?.CheckCertificateRevocation ?? false;

        await sslStream
            .AuthenticateAsClientAsync(
                tlsConfig?.TargetHost ?? targetHost,
                clientCertificates,
                enabledProtocols,
                checkCertificateRevocation)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }
#endif

#if !NETSTANDARD2_0
    private SslClientAuthenticationOptions BuildSslClientAuthenticationOptions(string targetHost)
    {
        var tlsConfig = _options.TlsConfig;
        var options = new SslClientAuthenticationOptions
        {
            TargetHost = tlsConfig?.TargetHost ?? targetHost,
            RemoteCertificateValidationCallback = BuildRemoteCertificateValidationCallback()
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

        // Configure client certificate for mTLS
        var clientCertificates = BuildClientCertificates(tlsConfig);
        if (clientCertificates is not null)
        {
            options.ClientCertificates = clientCertificates;
        }

        return options;
    }
#endif

    private RemoteCertificateValidationCallback? BuildRemoteCertificateValidationCallback()
    {
        var tlsConfig = _options.TlsConfig;
        var callback = _options.RemoteCertificateValidationCallback;
        var validateServerCertificateHostName = tlsConfig?.ValidateServerCertificateHostName ?? true;

        // Configure server certificate validation
        if (tlsConfig is not null && !tlsConfig.ValidateServerCertificate)
        {
            // Disable server certificate validation (not recommended for production)
            // This is intentional when user explicitly sets ValidateServerCertificate = false
#pragma warning disable CA5359 // Do not disable certificate validation
            return (_, _, _, _) => true;
#pragma warning restore CA5359
        }

        if (callback is not null)
        {
            return callback;
        }

        if (HasCustomCaCertificate(tlsConfig))
        {
            // Custom CA certificate validation
            var caCertificates = LoadCaCertificatesWithOwnership(tlsConfig!);
            return (_, certificate, chain, sslPolicyErrors) =>
                ValidateServerCertificate(
                    certificate,
                    chain,
                    ApplyServerCertificateHostNamePolicy(sslPolicyErrors, validateServerCertificateHostName),
                    caCertificates);
        }

        if (!validateServerCertificateHostName)
        {
            return static (_, _, _, sslPolicyErrors) =>
                ApplyServerCertificateHostNamePolicy(sslPolicyErrors, validateServerCertificateHostName: false) ==
                SslPolicyErrors.None;
        }

        return null;
    }

    internal static SslPolicyErrors ApplyServerCertificateHostNamePolicy(
        SslPolicyErrors sslPolicyErrors,
        bool validateServerCertificateHostName)
    {
        return validateServerCertificateHostName
            ? sslPolicyErrors
            : sslPolicyErrors & ~SslPolicyErrors.RemoteCertificateNameMismatch;
    }

    private X509CertificateCollection? BuildClientCertificates(TlsConfig? tlsConfig)
    {
        if (tlsConfig is null)
            return null;

        var clientCert = LoadClientCertificateWithOwnership(tlsConfig);
        if (clientCert is null)
            return null;

        var certificates = new X509CertificateCollection();
        certificates.Add(clientCert);
        return certificates;
    }

    private void ConfigureTcpKeepAlive(Socket socket)
    {
        if (!_options.EnableTcpKeepAlive)
            return;

        TrySetSocketOption(socket, SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 1);

#if !NETSTANDARD2_0
        var keepAliveTimeSeconds = ToPositiveSeconds(_options.TcpKeepAliveTime);
        if (keepAliveTimeSeconds > 0)
            TrySetSocketOption(socket, SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, keepAliveTimeSeconds);

        var keepAliveIntervalSeconds = ToPositiveSeconds(_options.TcpKeepAliveInterval);
        if (keepAliveIntervalSeconds > 0)
            TrySetSocketOption(socket, SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, keepAliveIntervalSeconds);

        if (_options.TcpKeepAliveRetryCount > 0)
            TrySetSocketOption(socket, SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, _options.TcpKeepAliveRetryCount);
#endif
    }

    private static int ToPositiveSeconds(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
            return 0;

        return (int)Math.Min(Math.Ceiling(value.TotalSeconds), int.MaxValue);
    }

    private static void TrySetSocketOption(Socket socket, SocketOptionLevel level, SocketOptionName name, int value)
    {
        try
        {
            socket.SetSocketOption(level, name, value);
        }
        catch (SocketException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }
        catch (NotSupportedException)
        {
        }
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
#if NETSTANDARD2_0
#pragma warning disable SYSLIB0057
            collection.Add(new X509Certificate2(path));
#pragma warning restore SYSLIB0057
#else
            // Load PFX/PKCS12 file using modern API
            collection.Add(X509CertificateLoader.LoadPkcs12FromFile(path, password: null));
#endif
        }
        else
        {
#if NETSTANDARD2_0
            throw new PlatformNotSupportedException("PEM CA certificate files require net10.0 or later. Use a PFX/P12 CA file for netstandard2.0.");
#else
            // Assume PEM format - can contain multiple certificates
            collection.ImportFromPemFile(path);
#endif
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
#if NETSTANDARD2_0
#pragma warning disable SYSLIB0057
                cert = string.IsNullOrEmpty(tlsConfig.ClientKeyPassword)
                    ? new X509Certificate2(certPath)
                    : new X509Certificate2(certPath, tlsConfig.ClientKeyPassword);
#pragma warning restore SYSLIB0057
#else
                // Load PFX/PKCS12 file (contains both certificate and private key) using modern API
                cert = X509CertificateLoader.LoadPkcs12FromFile(
                    certPath,
                    string.IsNullOrEmpty(tlsConfig.ClientKeyPassword) ? null : tlsConfig.ClientKeyPassword);
#endif
            }
            else
            {
#if NETSTANDARD2_0
                throw new PlatformNotSupportedException("PEM client certificates require net10.0 or later. Use a PFX/P12 client certificate for netstandard2.0.");
#else
                // PEM format - need separate key file
                if (tlsConfig.ClientKeyPath is null)
                {
                    throw new InvalidOperationException(
                        "Client key path is required when using PEM certificate format");
                }

                cert = string.IsNullOrEmpty(tlsConfig.ClientKeyPassword)
                    ? X509Certificate2.CreateFromPemFile(certPath, tlsConfig.ClientKeyPath)
                    : X509Certificate2.CreateFromEncryptedPemFile(certPath, tlsConfig.ClientKeyPassword, tlsConfig.ClientKeyPath);
#endif
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
#if NETSTANDARD2_0
                foreach (var caCert in trustedCaCertificates)
                {
                    customChain.ChainPolicy.ExtraStore.Add(caCert);
                }
#else
                customChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;

                foreach (var caCert in trustedCaCertificates)
                {
                    customChain.ChainPolicy.CustomTrustStore.Add(caCert);
                }
#endif

                if (customChain.Build(cert2))
                {
                    return IsChainRootedInTrustedCa(customChain, trustedCaCertificates);
                }
            }
            finally
            {
                ownedCert?.Dispose();
            }
        }

        return false;
    }

    private static bool IsChainRootedInTrustedCa(
        X509Chain chain,
        X509Certificate2Collection trustedCaCertificates)
    {
        foreach (var status in chain.ChainStatus)
        {
            if (status.Status != X509ChainStatusFlags.UntrustedRoot &&
                status.Status != X509ChainStatusFlags.NoError)
            {
                return false;
            }
        }

        if (chain.ChainElements.Count == 0)
            return false;

        var rootCert = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
        foreach (var caCert in trustedCaCertificates)
        {
            if (string.Equals(rootCert.Thumbprint, caCert.Thumbprint, StringComparison.OrdinalIgnoreCase))
                return true;
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
            await PrepareSaslAuthenticatorAsync(authenticator, cancellationToken).ConfigureAwait(false);

            var authBytes = authenticator.GetInitialResponse();

            // Always send the initial client response at least once. Single-round mechanisms
            // (PLAIN, OAUTHBEARER) mark themselves complete in GetInitialResponse, so gating
            // the send on !IsComplete would skip transmitting credentials entirely.
            do
            {
                var sentAuthBytes = authBytes;
                SaslAuthenticateResponse authResponse;
                try
                {
                    authResponse = await SendSaslMessageAsync<SaslAuthenticateRequest, SaslAuthenticateResponse>(
                        new SaslAuthenticateRequest { AuthBytes = sentAuthBytes },
                        2, // Use v2 for SaslAuthenticate (flexible version)
                        cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    SaslCredentialBuffers.Clear(sentAuthBytes);
                }

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
            while (!authenticator.IsComplete);
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
            _options.SaslPassword ?? throw new InvalidOperationException("SASL password not configured"),
            _options.SaslScramTokenAuth),
        SaslMechanism.ScramSha512 => new ScramAuthenticator(
            SaslMechanism.ScramSha512,
            _options.SaslUsername ?? throw new InvalidOperationException("SASL username not configured"),
            _options.SaslPassword ?? throw new InvalidOperationException("SASL password not configured"),
            _options.SaslScramTokenAuth),
        SaslMechanism.Gssapi => new GssapiAuthenticator(
            _options.GssapiConfig ?? throw new InvalidOperationException("GSSAPI configuration not provided"),
            _resolvedTargetHost ?? _host),
        SaslMechanism.OAuthBearer => CreateOAuthBearerAuthenticator(),
        SaslMechanism.AwsMskIam => new AwsMskIamAuthenticator(_options.AwsMskIamConfig, _host),
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
        if (Volatile.Read(ref _disposed) != 0 || !IsConnected)
            return;

        if (_reauthenticating)
            return;

        // Only one re-authentication at a time
        try
        {
            if (!await _reauthLock.WaitAsync(0).ConfigureAwait(false))
                return;
        }
        catch (ObjectDisposedException)
        {
            // Connection is being disposed — abandon re-authentication silently.
            return;
        }

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
        catch (Exception ex) when (Volatile.Read(ref _disposed) == 0)
        {
            LogSaslReauthenticationFailed(ex, BrokerId);

            // Don't tear down the connection - let it fail naturally when the broker
            // rejects requests after session expiry. The connection pool will handle
            // reconnection at that point.
        }
        finally
        {
            _reauthenticating = false;
            SemaphoreHelper.ReleaseSafely(_reauthLock);
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

            // Step 2: Fetch any credentials needed before creating the synchronous initial response.
            await PrepareSaslAuthenticatorAsync(authenticator, cancellationToken).ConfigureAwait(false);

            var authBytes = authenticator.GetInitialResponse();

            // Always send the initial client response at least once (see PerformSaslExchangeAsync).
            do
            {
                var sentAuthBytes = authBytes;
                SaslAuthenticateResponse authResponse;
                try
                {
                    authResponse = await SendAsync<SaslAuthenticateRequest, SaslAuthenticateResponse>(
                        new SaslAuthenticateRequest { AuthBytes = sentAuthBytes },
                        2,
                        cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    SaslCredentialBuffers.Clear(sentAuthBytes);
                }

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
            while (!authenticator.IsComplete);
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

    private static async ValueTask PrepareSaslAuthenticatorAsync(
        ISaslAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        switch (authenticator)
        {
            case OAuthBearerAuthenticator oauthAuthenticator:
                await oauthAuthenticator.GetTokenAsync(cancellationToken).ConfigureAwait(false);
                break;
            case AwsMskIamAuthenticator awsMskIamAuthenticator:
                await awsMskIamAuthenticator.GetCredentialsAsync(cancellationToken).ConfigureAwait(false);
                break;
        }
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
        var headerVersion = KafkaMessageMetadata<TRequest, TResponse>.GetRequestHeaderVersion(apiVersion);

        // Write to stream directly (no pipe yet). Use the pooled serializer path instead
        // of the thread-local SASL buffer so credential-bearing requests can be cleared.
        var (buffer, totalSize) = PreSerializeRequest<TRequest, TResponse>(
            request,
            correlationId,
            apiVersion,
            headerVersion);
        var clearSerializedArray = KafkaMessageMetadata<TRequest, TResponse>.ApiKey == ApiKey.SaslAuthenticate;
        try
        {
            await _stream.WriteAsync(buffer.AsMemory(0, totalSize), cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DekafPools.SerializationBuffers.Return(buffer, clearArray: clearSerializedArray);
        }

        // Read response
        var sizeBuffer = ArrayPool<byte>.Shared.Rent(4);
        try
        {
            await ReadExactlyAsync(_stream, sizeBuffer.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
            var responseSize = BinaryPrimitives.ReadInt32BigEndian(sizeBuffer);
            ValidateResponseFrameSize(responseSize, MaxSaslResponseFrameSize);

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
                var responseHeaderVersion = KafkaMessageMetadata<TRequest, TResponse>.GetResponseHeaderVersion(apiVersion);
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
                return KafkaMessageMetadata<TRequest, TResponse>.ReadResponse(ref reader, apiVersion);
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
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        Volatile.Write(ref _connected, 0);
        var pendingRequestSlotOperationsDrained = ClosePendingRequestSlotsAsync();
        CancelPendingRequestSlotWaiters();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var pendingRequestCount = GetPendingRequestCount();
            LogConnectionDisposing(BrokerId, pendingRequestCount);
        }

        _receiveCts?.Cancel();

        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The receive loop did not exit within 5 seconds (e.g., blocked on ReadAsync).
                // Complete the pipe reader to force ReadAsync to return with IsCompleted=true,
                // then wait briefly for the loop to observe it and exit gracefully. This prevents
                // a race where the pipe is disposed while the receive loop still holds a ReadResult.
                LogReceiveLoopShutdownFailed(ex, BrokerId);

                if (_reader is not null)
                {
                    await _reader.CompleteAsync().ConfigureAwait(false);

                    try
                    {
                        await _receiveTask.WaitAsync(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
                    }
                    catch (Exception innerEx) when (innerEx is not OperationCanceledException)
                    {
                        // Best effort — proceed with disposal regardless.
                        LogReceiveLoopShutdownRetryFailed(innerEx, BrokerId);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        _receiveCts?.Dispose();

        // Invariant: at most one pipe type is created per connection (plain TCP or TLS, never both).
        Debug.Assert(_socketPipe is null || _duplexPipe is null,
            "Both _socketPipe and _duplexPipe are non-null — this should never happen.");

        // Each pipe takes full ownership of all underlying resources (socket, streams).
        // DisposeAsync handles reader completion, socket/stream closure, and read pump teardown.
        if (_socketPipe is not null)
        {
            await _socketPipe.DisposeAsync().ConfigureAwait(false);
        }
        else if (_duplexPipe is not null)
        {
            await _duplexPipe.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            // No pipe was created (e.g., connection failed during setup).
            // Dispose both as a fallback since no pipe took ownership.
            _stream?.Dispose();
            _socket?.Dispose();
        }

        // Placed after pipe completion so all outstanding IMemoryOwner<byte> objects are
        // returned before the pool is released for GC.
        // Only dispose per-connection pools — shared pools are owned by the ConnectionPool.
        if (_sharedPipeMemoryPool is null)
        {
            _pipeMemoryPool?.Dispose();
        }

        _reauthTimer?.Dispose();
        _reauthLock.Dispose();
        _connectLock.Dispose();
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

        // Sweep any remaining entries that had no awaiter (edge case: request registered
        // but cancelled before AwaitAndParseResponseAsync was entered). These won't be
        // cleaned up by a continuation since none was registered.
        ReturnAllPendingRequestsToPool();

        await pendingRequestSlotOperationsDrained.ConfigureAwait(false);
        _pendingRequestSlots.Dispose();
        _pendingRequestSlotCts.Dispose();

        _cancelledCorrelationIds.Clear();
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Received late response for cancelled/timed-out correlation ID {CorrelationId}, discarding")]
    private partial void LogLateResponseForCancelledRequest(int correlationId);

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Receive loop for broker {BrokerId} did not exit within 5s timeout during disposal")]
    private partial void LogReceiveLoopShutdownFailed(Exception ex, int brokerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Receive loop for broker {BrokerId} did not exit after pipe reader completion (500ms retry) during disposal")]
    private partial void LogReceiveLoopShutdownRetryFailed(Exception ex, int brokerId);

    #endregion

    /// <summary>
    /// Tracks the state of a response frame being assembled incrementally
    /// across multiple pipe read cycles. Used when the response is larger
    /// than what's currently available in the pipe buffer.
    /// </summary>
    internal struct PartialFrameContext
    {
        public byte[]? Buffer;
        public int FrameSize;
        public int Offset;
        public int CorrelationId;
        public bool IsPooled;

        public readonly bool IsActive => Buffer is not null;
        public readonly int Remaining => FrameSize - Offset;
    }

    /// <summary>
    /// Called when the pipe buffer has the 4-byte frame size header but not the full payload.
    /// Rents a response buffer, copies all available data, and consumes it from the pipe.
    /// Requires at least 8 bytes (4-byte size + 4-byte correlation ID).
    /// </summary>
    internal static bool TryStartPartialFrame(
        ref ReadOnlySequence<byte> buffer,
        ref PartialFrameContext context,
        ResponseBufferPool responseBufferPool)
    {
        if (buffer.Length < 8) // Need size header + correlation ID
            return false;

        // Read frame size
        Span<byte> sizeSpan = stackalloc byte[4];
        buffer.Slice(0, 4).CopyTo(sizeSpan);
        var frameSize = BinaryPrimitives.ReadInt32BigEndian(sizeSpan);
        ValidateResponseFrameSize(frameSize, responseBufferPool.MaxArrayLength);

        // If the full frame is available, don't start partial — let TryReadResponse handle it
        var frameLength = 4L + frameSize;
        if (buffer.Length >= frameLength)
            return false;

        // Read correlation ID (first 4 bytes of payload, at offset 4)
        Span<byte> corrSpan = stackalloc byte[4];
        buffer.Slice(4, 4).CopyTo(corrSpan);
        var correlationId = BinaryPrimitives.ReadInt32BigEndian(corrSpan);

        // Rent response buffer (same logic as RentResponseArray instance method)
        var (responseArray, isPooled) = frameSize <= responseBufferPool.MaxArrayLength
            ? (responseBufferPool.Pool.Rent(frameSize), true)
            : (new byte[frameSize], false);

        // Copy all available payload (skip the 4-byte size header, it's not part of the response)
        var availablePayload = (int)(buffer.Length - 4);
        buffer.Slice(4, availablePayload).CopyTo(responseArray);

        context = new PartialFrameContext
        {
            Buffer = responseArray,
            FrameSize = frameSize,
            Offset = availablePayload,
            CorrelationId = correlationId,
            IsPooled = isPooled
        };

        // Consume everything from the pipe buffer
        buffer = buffer.Slice(buffer.End);
        return true;
    }

    /// <summary>
    /// Copies available data from the pipe into the partial frame buffer.
    /// Returns true when the frame is complete.
    /// </summary>
    internal static bool ContinuePartialFrame(
        ref ReadOnlySequence<byte> buffer,
        ref PartialFrameContext context)
    {
        var remaining = context.Remaining;
        var available = (int)Math.Min(buffer.Length, remaining);

        if (available > 0)
        {
            var destination = context.Buffer.AsSpan(context.Offset, available);

            if (buffer.IsSingleSegment)
                buffer.First.Span.Slice(0, available).CopyTo(destination);
            else
                buffer.Slice(0, available).CopyTo(destination);

            context.Offset += available;
            buffer = buffer.Slice(available);
        }

        return context.Remaining == 0;
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
    /// Whether SCRAM authentication uses Kafka delegation token credentials.
    /// </summary>
    public bool SaslScramTokenAuth { get; init; }

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
    /// AWS_MSK_IAM configuration.
    /// </summary>
    public AwsMskIamConfig? AwsMskIamConfig { get; init; }

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
    /// Whether to enable TCP keepalive on broker sockets.
    /// </summary>
    public bool EnableTcpKeepAlive { get; init; } = DefaultEnableTcpKeepAlive;

    /// <summary>
    /// Idle time before TCP keepalive probes start. Unsupported platforms ignore this.
    /// </summary>
    public TimeSpan TcpKeepAliveTime { get; init; } = DefaultTcpKeepAliveTime;

    /// <summary>
    /// Interval between TCP keepalive probes. Unsupported platforms ignore this.
    /// </summary>
    public TimeSpan TcpKeepAliveInterval { get; init; } = DefaultTcpKeepAliveInterval;

    /// <summary>
    /// Number of TCP keepalive probes before the connection is considered dead.
    /// Unsupported platforms ignore this.
    /// </summary>
    public int TcpKeepAliveRetryCount { get; init; } = DefaultTcpKeepAliveRetryCount;

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
    public TimeSpan ConnectionTimeout { get; init; } = DefaultConnectionTimeout;

    /// <summary>
    /// Request timeout.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Initial delay before reconnecting to a broker after a connection failure.
    /// Equivalent to Kafka's <c>reconnect.backoff.ms</c>. Set to zero to disable the delay.
    /// </summary>
    public TimeSpan ReconnectBackoff { get; init; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Maximum delay before reconnecting to a broker after repeated connection failures.
    /// Equivalent to Kafka's <c>reconnect.backoff.max.ms</c>.
    /// </summary>
    public TimeSpan ReconnectBackoffMax { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum idle time before an unused connection is closed. Set to -1 to disable.
    /// </summary>
    public int ConnectionsMaxIdleMs
    {
        get => _connectionsMaxIdleMs;
        init => _connectionsMaxIdleMs = ValidateConnectionsMaxIdleMs(value, nameof(ConnectionsMaxIdleMs));
    }

    /// <summary>
    /// Maximum in-flight requests per connection. Used internally to derive pool sizes.
    /// </summary>
    internal int MaxInFlightRequestsPerConnection { get; init; } = 5;

    /// <summary>
    /// Controls how broker hostnames are resolved before connecting.
    /// </summary>
    public ClientDnsLookup ClientDnsLookup { get; init; } = ClientDnsLookup.UseAllDnsIps;

    /// <summary>
    /// DNS resolver used by connections. Internal for deterministic tests.
    /// </summary>
    internal ClientDnsEndpointResolver DnsResolver { get; init; } = ClientDnsEndpointResolver.Default;

    internal const int DefaultConnectionsMaxIdleMs = 540000;
    internal static readonly TimeSpan DefaultConnectionTimeout = TimeSpan.FromSeconds(30);
    internal const bool DefaultEnableTcpKeepAlive = true;
    internal static readonly TimeSpan DefaultTcpKeepAliveTime = TimeSpan.FromMinutes(2);
    internal static readonly TimeSpan DefaultTcpKeepAliveInterval = TimeSpan.FromSeconds(30);
    internal const int DefaultTcpKeepAliveRetryCount = 3;

    internal static int ValidateConnectionsMaxIdleMs(int value, string paramName)
    {
        if (value < -1)
            throw new ArgumentOutOfRangeException(paramName, "ConnectionsMaxIdleMs must be -1 or greater.");

        return value;
    }

    internal static int ToConnectionsMaxIdleMs(TimeSpan value, string paramName)
    {
        if (value == Timeout.InfiniteTimeSpan)
            return -1;

        if (value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                paramName,
                "Connections max idle must be non-negative, or Timeout.InfiniteTimeSpan to disable.");

        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.TotalMilliseconds, int.MaxValue, paramName);
        return (int)value.TotalMilliseconds;
    }

    private readonly int _connectionsMaxIdleMs = DefaultConnectionsMaxIdleMs;
}

/// <summary>
/// An adaptive pool for response buffers whose maximum array size derives from consumer
/// configuration (e.g., <c>FetchMaxBytes</c>). This replaces the previous static
/// <c>ArrayPool&lt;byte&gt;</c> so that large multi-partition fetch responses are pooled
/// instead of falling through to unpooled LOH allocations.
/// </summary>
internal sealed class ResponseBufferPool
{
    /// <summary>
    /// Overhead bytes added to <c>FetchMaxBytes</c> to account for Kafka protocol framing
    /// (message size prefix, response headers, tagged fields, per-partition overhead).
    /// </summary>
    internal const int ProtocolOverheadBytes = 1024 * 1024; // 1 MB

    /// <summary>
    /// Default maximum array length when no consumer configuration is available.
    /// Matches the previous static pool size (16 MB).
    /// </summary>
    internal const int DefaultMaxArrayLength = 16 * 1024 * 1024;
    private const int SizeTierBytes = 1024 * 1024;

    /// <summary>
    /// Default shared instance used by producers, admin clients, and connections
    /// that don't have consumer-specific configuration.
    /// </summary>
    internal static readonly ResponseBufferPool Default = new(DefaultMaxArrayLength);
    private static readonly ConcurrentDictionary<int, ResponseBufferPool> s_sharedPools = new();

    internal ArrayPool<byte> Pool { get; }
    internal int MaxArrayLength { get; }

    internal ResponseBufferPool(int maxArrayLength)
    {
        MaxArrayLength = maxArrayLength;
        // Limit bucket count to cap worst-case memory retention.
        // 4 arrays per bucket: default pool = 4 × 16 MB = ~64 MB, consumer pool = 4 × 51 MB = ~204 MB.
        // Previous values (32/8) caused up to 512 MB retention for the default pool alone.
        const int maxArraysPerBucket = 4;
        Pool = ArrayPool<byte>.Create(
            maxArrayLength: maxArrayLength,
            maxArraysPerBucket: maxArraysPerBucket);
    }

    /// <summary>
    /// Returns a process-shared <see cref="ResponseBufferPool"/> sized for the given
    /// <c>FetchMaxBytes</c> configuration. The pool's max array length is rounded up to
    /// a MiB tier after adding protocol overhead, with a minimum of <see cref="DefaultMaxArrayLength"/>.
    /// </summary>
    internal static ResponseBufferPool Create(int fetchMaxBytes)
    {
        var maxArrayLength = ComputeMaxArrayLength(fetchMaxBytes);
        return maxArrayLength == DefaultMaxArrayLength
            ? Default
            : s_sharedPools.GetOrAdd(maxArrayLength, static length => new ResponseBufferPool(length));
    }

    internal static int ComputeMaxArrayLength(int fetchMaxBytes)
    {
        // Use long arithmetic to avoid silent overflow when fetchMaxBytes is near int.MaxValue
        var requiredLength = Math.Min((long)fetchMaxBytes + ProtocolOverheadBytes, int.MaxValue);
        var tieredLength = RoundUpToMiBTier(requiredLength);
        return Math.Max(tieredLength, DefaultMaxArrayLength);
    }

    private static int RoundUpToMiBTier(long value)
    {
        if (value >= int.MaxValue)
            return int.MaxValue;

        var rounded = ((value + SizeTierBytes - 1) / SizeTierBytes) * SizeTierBytes;
        return rounded >= int.MaxValue ? int.MaxValue : (int)rounded;
    }
}

internal readonly struct PooledResponseBuffer : IDisposable
{
    private readonly byte[] _buffer;
    private readonly int _offset;
    private readonly ResponseBufferPool? _pool;

    public PooledResponseBuffer(byte[] buffer, int length, bool isPooled, int offset = 0, ResponseBufferPool? pool = null)
    {
        _buffer = buffer;
        Length = length;
        IsPooled = isPooled;
        _offset = offset;
        _pool = pool;
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
        return new PooledResponseBuffer(_buffer, Length - additionalOffset, IsPooled, _offset + additionalOffset, _pool);
    }

    /// <summary>
    /// Transfers ownership of this buffer to a new PooledResponseMemory instance.
    /// After calling this, the buffer should NOT be disposed via this struct.
    /// </summary>
    public PooledResponseMemory TransferOwnership()
    {
        return PooledResponseMemory.Create(_buffer, Length, IsPooled, _offset, _pool);
    }

    public void Dispose()
    {
        if (IsPooled && _buffer is not null)
        {
            Debug.Assert(_pool is not null, "Pooled buffer must have a non-null pool reference");
            _pool!.Pool.Return(_buffer);
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
    private static readonly PooledResponseMemoryPool s_pool = new();

    private byte[]? _buffer;
    private int _length;
    private bool _isPooled;
    private int _offset;
    private ResponseBufferPool? _pool;
    private int _pooled;
    private int _disposed = 1;

    private PooledResponseMemory() { }

    internal static PooledResponseMemory Create(byte[] buffer, int length, bool isPooled, int offset, ResponseBufferPool? pool = null)
    {
        var memory = s_pool.Rent();
        memory.Initialize(buffer, length, isPooled, offset, pool, pooled: true);
        return memory;
    }

    private void Initialize(byte[] buffer, int length, bool isPooled, int offset, ResponseBufferPool? pool, bool pooled)
    {
        _buffer = buffer;
        _length = length;
        _isPooled = isPooled;
        _offset = offset;
        _pool = pool;
        _pooled = pooled ? 1 : 0;
        Volatile.Write(ref _disposed, 0);
    }

    public ReadOnlyMemory<byte> Memory => _buffer is not null
        ? _buffer.AsMemory(_offset, _length)
        : throw new ObjectDisposedException(nameof(PooledResponseMemory));

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var buffer = Interlocked.Exchange(ref _buffer, null);
        if (_isPooled && buffer is not null)
        {
            Debug.Assert(_pool is not null, "Pooled buffer must have a non-null pool reference");
            _pool!.Pool.Return(buffer);
        }

        if (Volatile.Read(ref _pooled) != 0)
            s_pool.Return(this);
    }

    private sealed class PooledResponseMemoryPool() : ObjectPool<PooledResponseMemory>(maxPoolSize: 256)
    {
        protected override PooledResponseMemory Create() => new();

        protected override void Reset(PooledResponseMemory item)
        {
            item._buffer = null;
            item._length = 0;
            item._isPooled = false;
            item._offset = 0;
            item._pool = null;
            item._pooled = 0;
            Volatile.Write(ref item._disposed, 1);
        }
    }
}

/// <summary>
/// Thread-safe bounded pool for <see cref="PooledPendingRequest"/> instances.
/// Uses lock-free operations via <see cref="ConcurrentStack{T}"/> for high throughput.
/// </summary>
internal sealed class PendingRequestPool
{
    private const int DefaultMaxPoolSize = 256;
    private readonly int _maxPoolSize;
    private readonly ConcurrentStack<PooledPendingRequest> _pool = new();
    private int _poolCount;

    public PendingRequestPool() : this(DefaultMaxPoolSize) { }

    public PendingRequestPool(int maxPoolSize) { _maxPoolSize = maxPoolSize; }

    public int ApproximateCount => Volatile.Read(ref _poolCount);

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
        if (count <= _maxPoolSize)
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
    /// <summary>
    /// Maximum reasonable number of tagged fields in a Kafka response header.
    /// Values exceeding this threshold indicate data corruption or protocol misalignment.
    /// </summary>
    private const int MaxReasonableTagCount = 1000;

    private const int StatePending = 0;
    private const int StateCompleting = 1;
    private const int StateCompleted = 2;

    private ManualResetValueTaskSourceCore<PooledResponseBuffer> _core = new() { RunContinuationsAsynchronously = true };
    private short _responseHeaderVersion;
    private CancellationTokenRegistration _cancellationRegistration;
    private CancellationToken _cancellationToken;
    private int _state; // High 16 bits = core version; low 16 bits = State*

    /// <summary>
    /// Initializes the request for a new operation.
    /// </summary>
    public void Initialize(short responseHeaderVersion, CancellationToken cancellationToken, bool registerCancellation = true)
    {
        _responseHeaderVersion = responseHeaderVersion;
        _cancellationToken = cancellationToken;
        _state = CreateState(_core.Version, StatePending);

        // Register for cancellation if the token can be cancelled
        if (registerCancellation && cancellationToken.CanBeCanceled)
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
        _cancellationToken = cancellationToken;
        var newRegistration = cancellationToken.CanBeCanceled
            ? cancellationToken.Register(
                static state => ((PooledPendingRequest)state!).OnCancelled(),
                this)
            : default;

        var oldRegistration = _cancellationRegistration;
        _cancellationRegistration = newRegistration;
        oldRegistration.Dispose();
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
        TrySetCanceled(_cancellationToken);
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
        => TryComplete(_core.Version, pooledBuffer);

    /// <summary>
    /// Attempts to complete the request with response data for the captured version.
    /// Returns false if the request was already completed or reused.
    /// </summary>
    public bool TryComplete(short expectedVersion, PooledResponseBuffer pooledBuffer)
    {
        // Atomically claim the completing state
        if (!TryClaimCompletion(expectedVersion))
        {
            return false; // Already completing or completed
        }

        // CRITICAL: Parse the response BEFORE setting state to completed.
        // SetResult/SetException invokes the continuation synchronously, which may
        // return this request to the pool. We must not access any mutable state after
        // calling SetResult/SetException.
        PooledResponseBuffer result = default;
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
        Volatile.Write(ref _state, CreateState(expectedVersion, StateCompleted));

        // Publish the completion after all mutable request state has been read.
        if (parseException is not null)
        {
            _core.SetException(parseException);
        }
        else
        {
            _core.SetResult(result);
        }

        return true;
    }

    /// <summary>
    /// Attempts to complete the request with an exception.
    /// Returns false if already completed.
    /// </summary>
    public bool TrySetException(Exception exception)
        => TrySetException(_core.Version, exception);

    /// <summary>
    /// Attempts to complete the captured request version with an exception.
    /// Returns false if already completed or reused.
    /// </summary>
    public bool TrySetException(short expectedVersion, Exception exception)
    {
        if (!TryClaimCompletion(expectedVersion))
        {
            return false;
        }

        // Mark as completed before publishing completion; a continuation may
        // return this instance to the pool afterwards.
        Volatile.Write(ref _state, CreateState(expectedVersion, StateCompleted));
        _core.SetException(exception);
        return true;
    }

    /// <summary>
    /// Attempts to cancel the request.
    /// Returns false if already completed.
    /// </summary>
    public bool TrySetCanceled()
        => TrySetCanceled(_cancellationToken);

    /// <summary>
    /// Attempts to cancel the request with the token that triggered cancellation.
    /// Returns false if already completed.
    /// </summary>
    public bool TrySetCanceled(CancellationToken cancellationToken)
    {
        var version = _core.Version;
        if (!TryClaimCompletion(version))
        {
            return false;
        }

        // Mark as completed before publishing completion; a continuation may
        // return this instance to the pool afterwards.
        Volatile.Write(ref _state, CreateState(version, StateCompleted));
        _core.SetException(new OperationCanceledException(cancellationToken));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryClaimCompletion(short expectedVersion)
    {
        var pendingState = CreateState(expectedVersion, StatePending);
        var completingState = CreateState(expectedVersion, StateCompleting);
        return Interlocked.CompareExchange(ref _state, completingState, pendingState) == pendingState;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CreateState(short version, int state)
        => ((ushort)version << 16) | state;

    private PooledResponseBuffer ParseAndSliceResponse(PooledResponseBuffer pooledBuffer)
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

            // Sanity check: tag count should be reasonable
            if (tagCount > MaxReasonableTagCount)
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
        // Dispose cancellation registration
        _cancellationRegistration.Dispose();
        _cancellationRegistration = default;
        _cancellationToken = default;

        // Reset the core for reuse
        _core.Reset();
        _state = CreateState(_core.Version, StatePending);
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

