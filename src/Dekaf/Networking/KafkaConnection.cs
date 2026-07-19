using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks.Sources;
using Dekaf.Errors;
using Dekaf.Internal;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
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

    // Correlation ID is always first in the response header.
    internal const int MinimumResponseFrameSize = 4;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateResponseFrameSize(int frameSize, int maxFrameSize)
    {
        if (frameSize < MinimumResponseFrameSize || frameSize > maxFrameSize)
        {
            throw new KafkaException(
                $"Invalid response frame size {frameSize}. Expected between {MinimumResponseFrameSize} and {maxFrameSize} bytes.");
        }
    }
}

/// <summary>
/// A multiplexed connection to a Kafka broker. Requests are written directly to the
/// socket/stream; responses are framed by <see cref="ResponseFrameReader"/> straight
/// into pooled arrays (one user-space copy) and dispatched by correlation id.
/// </summary>
public sealed partial class KafkaConnection :
    IKafkaConnection,
    IIdleTrackedKafkaConnection,
    IRetirableKafkaConnection,
    IKafkaPipelinedWriteCompletionConnection
{
    private readonly string _host;
    private readonly int _port;
    private readonly string? _clientId;
    private readonly ILogger _logger;
    private readonly ConnectionOptions _options;
    private readonly ResponseBufferPool _responseBufferPool;
    private readonly bool _responseMemoryAdmissionsEnabled;
    private readonly Func<int, ResponseFrameAdmission> _responseFrameAdmission;
    private readonly Func<Task, TimeSpan, Task> _waitForAbandonedWriteAsync;

    private Socket? _socket;
    private string? _resolvedTargetHost;
    private Stream? _stream;
    // True once ConnectCoreAsync wrapped _stream in an SslStream. Captured at connect time
    // so the reader construction and teardown ordering don't re-derive it from stream types.
    private bool _isTls;
    private ResponseFrameReader? _frameReader;
    private PipeMemoryPool? _pipeMemoryPool;
    private readonly long _receiveTimeoutStopwatchTicks;

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
    // SASL handshake/auth responses are small; this pre-auth path must not honor untrusted large frame claims.
    private const int MaxSaslResponseFrameSize = 1024 * 1024;
    internal const int DefaultPreSerializeInitialCapacity = 4096;
    private const int MessageSizePrefixLength = 4;
    private const int RequestHeaderSizeSlack = 128;
    private static int s_globalCorrelationId;
    private readonly PendingRequestShard[] _pendingRequestShards;
    private readonly SemaphoreSlim _pendingRequestSlots;
    private readonly CancellationTokenSource _pendingRequestSlotCts = new();
    private readonly TaskCompletionSource _pendingRequestSlotOperationsDrained =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _pendingRequestSlotOperationCount;
    private int _pendingRequestSlotsClosed;
    private int _pendingRequestCount;
    private int _leaseCount;
    private int _activeOperationCount;
    private int _retirementState;
    private readonly CancelledCorrelationIdTracker _cancelledCorrelationIds = new(MaxCancelledCorrelationIds);
    private readonly PendingRequestPool _pendingRequestPool;
    private readonly CancellationTokenSourcePool _timeoutCtsPool;
    private readonly ClientTelemetryMetricCollector? _telemetryMetricCollector;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SemaphoreSlim _scatterGatherSenderLock = new(1, 1);
    private SocketScatterGatherSender? _scatterGatherSender;

    private Task? _receiveTask;
    private CancellationTokenSource? _receiveCts;
    private readonly object _receiveTimeoutGate = new();
    private Timer? _receiveTimeoutTimer;
    private long _receiveTimeoutDeadlineTimestamp;
    private int _receiveTimeoutExpired;
    private OAuthBearerTokenProvider? _ownedTokenProvider;
    private readonly object _disposeGate = new();
    private Task? _disposeTask;
    private int _disposed;
    private int _connected;
    private bool _hasReceivedResponse;
    private long _lastUsedTimestampMs = Dekaf.MonotonicClock.GetMilliseconds();
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

    int IRetirableKafkaConnection.LeaseCount => Volatile.Read(ref _leaseCount);

    int IRetirableKafkaConnection.ActiveOperationCount => Volatile.Read(ref _activeOperationCount);

    bool IRetirableKafkaConnection.TryAcquireLease() => TryAcquireLease();

    void IRetirableKafkaConnection.ReleaseLease() => ReleaseLease();

    void IRetirableKafkaConnection.BeginRetirement()
        => Interlocked.CompareExchange(ref _retirementState, 1, 0);

    void IRetirableKafkaConnection.CompleteRetirement() => Volatile.Write(ref _retirementState, 2);

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

    /// <remarks>
    /// <paramref name="bufferMemory"/>, <paramref name="connectionsPerBroker"/> and
    /// <paramref name="brokerCount"/> previously sized the input pipe's backpressure
    /// thresholds; the direct-read receive path no longer buffers beyond one receive
    /// buffer per connection. The parameters remain for API compatibility.
    /// </remarks>
    public KafkaConnection(
        string host,
        int port,
        string? clientId = null,
        ConnectionOptions? options = null,
        ILogger<KafkaConnection>? logger = null,
        ulong bufferMemory = 33554432,
        int connectionsPerBroker = 1,
        int brokerCount = 1)
        : this(host, port, clientId, options, logger, ResponseBufferPool.Default)
    {
    }

    internal KafkaConnection(
        string host,
        int port,
        string? clientId,
        ConnectionOptions? options,
        ILogger<KafkaConnection>? logger,
        ResponseBufferPool responseBufferPool,
        ClientTelemetryMetricCollector? telemetryMetricCollector = null,
        Func<Task, TimeSpan, Task>? waitForAbandonedWriteAsync = null,
        bool responseMemoryAdmissionsEnabled = false)
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
        _responseBufferPool = responseBufferPool;
        _responseMemoryAdmissionsEnabled = responseMemoryAdmissionsEnabled;
        _responseFrameAdmission = GetResponseFrameAdmission;
        _telemetryMetricCollector = telemetryMetricCollector;
        _waitForAbandonedWriteAsync = waitForAbandonedWriteAsync ?? WaitForAbandonedWriteAsync;
        _receiveTimeoutStopwatchTicks =
            (long)(_options.RequestTimeout.Ticks * (double)Stopwatch.Frequency / TimeSpan.TicksPerSecond);
    }

    /// <inheritdoc cref="KafkaConnection(string, int, string?, ConnectionOptions?, ILogger{KafkaConnection}?, ulong, int, int)"/>
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
        : this(host, port, clientId, options, logger, ResponseBufferPool.Default)
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
        ResponseBufferPool responseBufferPool,
        PipeMemoryPool? sharedPipeMemoryPool = null,
        ClientTelemetryMetricCollector? telemetryMetricCollector = null,
        bool responseMemoryAdmissionsEnabled = false)
        : this(
            host,
            port,
            clientId,
            options,
            logger,
            responseBufferPool,
            telemetryMetricCollector,
            responseMemoryAdmissionsEnabled: responseMemoryAdmissionsEnabled)
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
            _isTls = true;
        }

        _stream = networkStream;

        // Perform SASL authentication if configured
        if (_options.SaslMechanism != SaslMechanism.None)
        {
            await PerformSaslAuthenticationAsync(cancellationToken).ConfigureAwait(false);
        }

        // Release the previous reader's buffer (reconnect path) before its source pool
        // is potentially replaced below.
        _frameReader?.Dispose();

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

        var readBufferSize = _options.ReceiveBufferSize > 0 ? _options.ReceiveBufferSize : 65536;

        // Plain TCP reads bypass the Stream abstraction and receive directly from the
        // Socket; TLS reads must go through the SslStream for decryption. Either way the
        // frame body is received straight into the pooled response array — there is no
        // intermediate pipe, so response payloads are copied exactly once (issue #1757).
        _frameReader = new ResponseFrameReader(
            _socket,
            _isTls ? _stream : null,
            readBufferSize,
            _responseBufferPool,
            _pipeMemoryPool,
            OnReceiveLoopBytesRead);

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
        using var operation = TrackOperation();

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
        var responseMemoryPool = request is FetchRequest fetchRequest
            ? fetchRequest.ResponseMemoryPool
            : null;
        pending.Initialize(
            responseHeaderVersion,
            cancellationToken,
            registerCancellation: false,
            checkCrcs: request is FetchRequest { CheckCrcs: true },
            responseMemoryPool: responseMemoryPool);
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
        using var operation = TrackOperation();

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
        => SendPipelinedCoreAsync<TRequest, TResponse>(
            request,
            apiVersion,
            callerOwnsTimeout: false,
            cancellationToken);

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
        => SendPipelinedCoreAsync<TRequest, TResponse>(
            request,
            apiVersion,
            callerOwnsTimeout: true,
            cancellationToken);

    ValueTask<PipelinedResponse<TResponse>> IKafkaPipelinedWriteCompletionConnection.SendPipelinedAfterWriteAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken)
        => SendPipelinedAfterWriteCoreAsync<TRequest, TResponse>(
            request,
            apiVersion,
            callerOwnsTimeout: false,
            cancellationToken);

    ValueTask<PipelinedResponse<TResponse>>
        IKafkaPipelinedWriteCompletionConnection.SendPipelinedWithCallerTimeoutAfterWriteAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken)
        => SendPipelinedAfterWriteCoreAsync<TRequest, TResponse>(
            request,
            apiVersion,
            callerOwnsTimeout: true,
            cancellationToken);

    private async Task<TResponse> SendPipelinedCoreAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        bool callerOwnsTimeout,
        CancellationToken cancellationToken)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        var responseTask = await SendPipelinedAfterWriteCoreAsync<TRequest, TResponse>(
            request,
            apiVersion,
            callerOwnsTimeout,
            cancellationToken).ConfigureAwait(false);

        return await responseTask.AsValueTask().ConfigureAwait(false);
    }

    private async ValueTask<PipelinedResponse<TResponse>> SendPipelinedAfterWriteCoreAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        bool callerOwnsTimeout,
        CancellationToken cancellationToken)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        using var operation = TrackOperation();

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
        var responseMemoryPool = request is FetchRequest fetchRequest
            ? fetchRequest.ResponseMemoryPool
            : null;
        // The pipelined wrapper performs bounded internal parsing, then only signals the
        // sender. Resume it in the receive dispatch frame to avoid a ThreadPool round trip.
        pending.Initialize(
            responseHeaderVersion,
            cancellationToken,
            registerCancellation: false,
            checkCrcs: request is FetchRequest { CheckCrcs: true },
            runContinuationsAsynchronously: false,
            responseMemoryPool: responseMemoryPool);
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

            return PooledPipelinedResponse<TRequest, TResponse>.Rent(
                this,
                pending,
                correlationId,
                apiVersion,
                callerOwnsTimeout,
                telemetryStartTimestamp,
                cancellationToken);
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

    private void CompletePipelinedResponse(
        PooledPendingRequest pending,
        int correlationId,
        bool responseReceived)
    {
        if (!TryRemovePendingRequest(correlationId, out var removed))
            return;

        Debug.Assert(ReferenceEquals(removed.Request, pending));
        _pendingRequestPool.Return(removed.Request);
        if (!responseReceived)
            _cancelledCorrelationIds.TryAdd(correlationId);
    }

    internal static int GetPipelinedResponsePoolCount<TRequest, TResponse>()
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
        => PooledPipelinedResponse<TRequest, TResponse>.ApproximatePoolCount;

    internal static Action? PipelinedResponseBeforePublishTestHook;

    private sealed class PooledPipelinedResponse<TRequest, TResponse> :
        IPipelinedResponseSource<TResponse>
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        private const int MaxPoolSize = 256;
        private const int ConsumerActive = 0;
        private const int ConsumerAbandoned = 1;
        private const int ConsumerReturned = 2;
        private const int CompletionReady = 1;
        private const int ConsumerReady = 2;
        private const int ReturnReady = CompletionReady | ConsumerReady;

        private static readonly ConcurrentStack<PooledPipelinedResponse<TRequest, TResponse>> Pool = new();
        private static int s_poolCount;

        // The pending request already enters this completion from an asynchronous callback.
        // Resume the internal response consumer inline to avoid a second ThreadPool hop;
        // _returnReadiness prevents pooling until this completion frame exits. Producer
        // delivery sources still dispatch application continuations asynchronously.
        private ManualResetValueTaskSourceCore<TResponse> _core;
        private readonly Action _pendingContinuation;
        private KafkaConnection? _connection;
        private PooledPendingRequest? _pending;
        private ValueTask<PooledResponseBuffer> _pendingTask;
        private CancellationToken _cancellationToken;
        private CancellationTokenRegistration _callerRegistration;
        private CancellationTokenSourcePool.PooledCancellationTokenSource? _timeoutCts;
        private int _correlationId;
        private short _apiVersion;
        private bool _callerOwnsTimeout;
        private bool _canceled;
        private long _telemetryStartTimestamp;
        private int _consumerState;
        private int _returnReadiness;

        private PooledPipelinedResponse()
        {
            _pendingContinuation = CompletePendingResponse;
        }

        internal static int ApproximatePoolCount => Volatile.Read(ref s_poolCount);

        public static PipelinedResponse<TResponse> Rent(
            KafkaConnection connection,
            PooledPendingRequest pending,
            int correlationId,
            short apiVersion,
            bool callerOwnsTimeout,
            long telemetryStartTimestamp,
            CancellationToken cancellationToken)
        {
            if (!Pool.TryPop(out var operation))
            {
                operation = new PooledPipelinedResponse<TRequest, TResponse>();
            }
            else
            {
                Interlocked.Decrement(ref s_poolCount);
            }

            operation.Initialize(
                connection,
                pending,
                correlationId,
                apiVersion,
                callerOwnsTimeout,
                telemetryStartTimestamp,
                cancellationToken);
            return new PipelinedResponse<TResponse>(operation, operation._core.Version);
        }

        private void Initialize(
            KafkaConnection connection,
            PooledPendingRequest pending,
            int correlationId,
            short apiVersion,
            bool callerOwnsTimeout,
            long telemetryStartTimestamp,
            CancellationToken cancellationToken)
        {
            _connection = connection;
            _pending = pending;
            _correlationId = correlationId;
            _apiVersion = apiVersion;
            _callerOwnsTimeout = callerOwnsTimeout;
            _telemetryStartTimestamp = telemetryStartTimestamp;
            _cancellationToken = cancellationToken;
            _canceled = false;
            _consumerState = ConsumerActive;
            _returnReadiness = 0;
            connection.LogWaitingForResponse(correlationId);

            if (callerOwnsTimeout && cancellationToken.CanBeCanceled)
            {
                pending.RegisterCancellation(cancellationToken);
            }
            else
            {
                _timeoutCts = connection._timeoutCtsPool.Rent();
                _timeoutCts.CancelAfter(connection._options.RequestTimeout);
                if (cancellationToken.CanBeCanceled)
                {
                    _callerRegistration = cancellationToken.Register(
                        static state => ((CancellationTokenSource)state!).Cancel(),
                        _timeoutCts);
                }

                pending.RegisterCancellation(_timeoutCts.Token);
            }

            _pendingTask = pending.AsValueTask();
            var awaiter = _pendingTask.GetAwaiter();
            if (awaiter.IsCompleted)
                CompletePendingResponse();
            else
                awaiter.UnsafeOnCompleted(_pendingContinuation);
        }

        private void CompletePendingResponse()
        {
            var connection = _connection!;
            var pending = _pending!;
            var responseReceived = false;
            TResponse response = default!;
            Exception? exception = null;

            try
            {
                var pooledBuffer = _pendingTask.GetAwaiter().GetResult();
                responseReceived = true;
                connection.LogResponseReceived(_correlationId);

                response = ParsePipelinedResponse<TRequest, TResponse>(
                    pooledBuffer,
                    _apiVersion,
                    pending.CheckCrcs,
                    pending.TakeResponseMemoryReservation());

                connection.Touch();
                connection._telemetryMetricCollector?.RecordRequestLatency(
                    connection.BrokerId,
                    _telemetryStartTimestamp);
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                exception = _callerOwnsTimeout
                    ? connection.CreateResponseTimeoutException(
                        KafkaMessageMetadata<TRequest, TResponse>.ApiKey,
                        _correlationId)
                    : new OperationCanceledException(_cancellationToken);
            }
            catch (OperationCanceledException)
            {
                exception = connection.CreateResponseTimeoutException(
                    KafkaMessageMetadata<TRequest, TResponse>.ApiKey,
                    _correlationId);
            }
            catch (Exception responseException)
            {
                exception = responseException;
            }
            finally
            {
                pending.DisposeRegistration();
                _callerRegistration.Dispose();
                _callerRegistration = default;
                _timeoutCts?.Dispose();
                _timeoutCts = null;
                connection.CompletePipelinedResponse(pending, _correlationId, responseReceived);
            }

            var returnAbandoned = Interlocked.CompareExchange(
                ref _consumerState,
                ConsumerReturned,
                ConsumerAbandoned) == ConsumerAbandoned;
            var token = _core.Version;
            Volatile.Read(ref PipelinedResponseBeforePublishTestHook)?.Invoke();
            if (exception is null)
            {
                _core.SetResult(response);
            }
            else
            {
                _canceled = exception is OperationCanceledException;
                _core.SetException(exception);
            }

            // Abandon can win after the pre-publication check. Claim it again only after
            // publishing the terminal result. The return handshake prevents an active
            // consumer from pooling/re-renting this source until this completion frame has
            // finished both checks, avoiding an ABA race on _consumerState.
            returnAbandoned |= Interlocked.CompareExchange(
                ref _consumerState,
                ConsumerReturned,
                ConsumerAbandoned) == ConsumerAbandoned;
            if (returnAbandoned)
            {
                try
                {
                    _ = _core.GetResult(token);
                }
                catch
                {
                    // Abandoned callers intentionally discard terminal errors.
                }

                SignalReturnReady(ConsumerReady);
            }

            SignalReturnReady(CompletionReady);
        }

        public void Abandon(short token)
        {
            if (token != _core.Version
                || Interlocked.CompareExchange(
                    ref _consumerState,
                    ConsumerAbandoned,
                    ConsumerActive) != ConsumerActive)
            {
                return;
            }

            ReturnIfAbandoned();
        }

        private void ReturnIfAbandoned()
        {
            if (_core.GetStatus(_core.Version) == ValueTaskSourceStatus.Pending
                || Interlocked.CompareExchange(
                    ref _consumerState,
                    ConsumerReturned,
                    ConsumerAbandoned) != ConsumerAbandoned)
            {
                return;
            }

            try
            {
                _ = _core.GetResult(_core.Version);
            }
            catch
            {
                // Abandoned callers intentionally discard terminal errors.
            }

            SignalReturnReady(ConsumerReady);
        }

        private void SignalReturnReady(int readiness)
        {
            var previous = Interlocked.Or(ref _returnReadiness, readiness);
            if (previous != ReturnReady && (previous | readiness) == ReturnReady)
                ReturnToPool();
        }

        private void ReturnToPool()
        {
            _connection = null;
            _pending = null;
            _pendingTask = default;
            _cancellationToken = default;
            _correlationId = 0;
            _apiVersion = 0;
            _callerOwnsTimeout = false;
            _canceled = false;
            _telemetryStartTimestamp = 0;
            _returnReadiness = 0;
            _core.Reset();

            if (Interlocked.Increment(ref s_poolCount) <= MaxPoolSize)
            {
                Pool.Push(this);
            }
            else
            {
                Interlocked.Decrement(ref s_poolCount);
            }
        }

        TResponse IValueTaskSource<TResponse>.GetResult(short token)
        {
            try
            {
                return _core.GetResult(token);
            }
            finally
            {
                if (Interlocked.CompareExchange(
                    ref _consumerState,
                    ConsumerReturned,
                    ConsumerActive) == ConsumerActive)
                {
                    SignalReturnReady(ConsumerReady);
                }
            }
        }

        ValueTaskSourceStatus IValueTaskSource<TResponse>.GetStatus(short token)
        {
            var status = _core.GetStatus(token);
            return _canceled && status == ValueTaskSourceStatus.Faulted
                ? ValueTaskSourceStatus.Canceled
                : status;
        }

        void IValueTaskSource<TResponse>.OnCompleted(
            Action<object?> continuation,
            object? state,
            short token,
            ValueTaskSourceOnCompletedFlags flags)
            => _core.OnCompleted(continuation, state, token, flags);
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

            return ParsePipelinedResponse<TRequest, TResponse>(
                pooledBuffer,
                apiVersion,
                pending.CheckCrcs,
                pending.TakeResponseMemoryReservation());
        }
        finally
        {
            CompletePipelinedResponse(pending, correlationId, responseReceived);
        }
    }

    private static TResponse ParsePipelinedResponse<TRequest, TResponse>(
        PooledResponseBuffer pooledBuffer,
        short apiVersion,
        bool checkCrcs,
        IResponseMemoryReservation? reservation)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        if (KafkaMessageMetadata<TRequest, TResponse>.ApiKey == ApiKey.Fetch)
            return ParseFetchResponse<TRequest, TResponse>(
                pooledBuffer,
                apiVersion,
                checkCrcs,
                reservation);

        try
        {
            var reader = new KafkaProtocolReader(pooledBuffer.Data);
            return KafkaMessageMetadata<TRequest, TResponse>.ReadResponse(ref reader, apiVersion);
        }
        finally
        {
            pooledBuffer.Dispose();
            reservation?.Dispose();
        }
    }

    internal static TResponse ParseFetchResponse<TRequest, TResponse>(
        PooledResponseBuffer pooledBuffer,
        short apiVersion,
        bool checkCrcs = false,
        IResponseMemoryReservation? reservation = null)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        var memoryOwner = pooledBuffer.TransferOwnership(reservation);
        using var parsingScope = ResponseParsingContext.SetPooledMemory(memoryOwner, checkCrcs);

        try
        {
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
        catch
        {
            memoryOwner.Dispose();
            throw;
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
        using var writer = new RentedBufferWriter(
            GetPreSerializeInitialCapacity<TResponse>(request),
            MessageSizePrefixLength);

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

    internal static int GetPreSerializeInitialCapacity<TResponse>(IKafkaRequest<TResponse> request)
        where TResponse : IKafkaResponse
    {
        if (request is not IKafkaRequestBodySizeHint { RequestBodySizeHint: > 0 } sizeHint)
            return DefaultPreSerializeInitialCapacity;

        var hintedCapacity = Math.Min(
            (long)Array.MaxLength - MessageSizePrefixLength,
            (long)sizeHint.RequestBodySizeHint + RequestHeaderSizeSlack);

        return Math.Max(DefaultPreSerializeInitialCapacity, (int)hintedCapacity);
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
        if (!_isTls
            && _socket is not null
            && request is ProduceRequest produceRequest
            && TryPreSerializeSingleBatchProduceRequest(
                produceRequest,
                correlationId,
                apiVersion,
                headerVersion,
                out var metadataArray,
                out var prefixLength,
                out var encodedRecords,
                out var suffixOffset,
                out var suffixLength))
        {
            try
            {
                await SemaphoreHelper.AcquireOrThrowDisposedAsync(
                        _writeLock,
                        nameof(KafkaConnection),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                DekafPools.SerializationBuffers.Return(metadataArray, clearArray: false);
                throw;
            }

            var segmentedWriteTask = WriteSegmentedFrameHoldingLockAsync(
                metadataArray,
                prefixLength,
                encodedRecords,
                suffixOffset,
                suffixLength);
            await AwaitBorrowedFrameWriteAsync(
                    segmentedWriteTask,
                    correlationId,
                    callerOwnsTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var (serializedArray, serializedLength) = PreSerializeRequest<TRequest, TResponse>(request, correlationId, apiVersion, headerVersion);
        var clearSerializedArray = KafkaMessageMetadata<TRequest, TResponse>.ApiKey == ApiKey.SaslAuthenticate;
        try
        {
            await SemaphoreHelper.AcquireOrThrowDisposedAsync(_writeLock, nameof(KafkaConnection), cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Lock never acquired: no bytes written, so the buffer can be returned here.
            DekafPools.SerializationBuffers.Return(serializedArray, clearArray: clearSerializedArray);
            throw;
        }

        // From here the frame write owns both the lock and the buffer; WriteFrameHoldingLockAsync
        // releases them itself so a timed-out caller can abandon the wait without cancelling the
        // socket write mid-frame (see AwaitFrameWriteAsync).
        var writeTask = WriteFrameHoldingLockAsync(serializedArray, serializedLength, clearSerializedArray);
        await AwaitFrameWriteAsync(writeTask, correlationId, callerOwnsTimeout, cancellationToken).ConfigureAwait(false);
    }

    internal bool TryPreSerializeSingleBatchProduceRequest(
        ProduceRequest request,
        int correlationId,
        short apiVersion,
        short headerVersion,
        out byte[] metadataArray,
        out int prefixLength,
        out ArraySegment<byte> encodedRecords,
        out int suffixOffset,
        out int suffixLength)
    {
        metadataArray = null!;
        prefixLength = 0;
        encodedRecords = default;
        suffixOffset = 0;
        suffixLength = 0;

        if (!request.TryGetSingleBatch(out var topic, out var partition, out var batch))
            return false;

        if (!batch.CanWriteSegmented(partition.Compression))
            return false;

        using var metadataWriter = new RentedBufferWriter(
            DefaultPreSerializeInitialCapacity,
            MessageSizePrefixLength);
        var writer = new KafkaProtocolWriter(metadataWriter);
        var header = new RequestHeader
        {
            ApiKey = ApiKey.Produce,
            ApiVersion = apiVersion,
            CorrelationId = correlationId,
            ClientId = _clientId,
            HeaderVersion = headerVersion
        };

        header.Write(ref writer);
        var headerLength = writer.BytesWritten;

        writer.WriteCompactNullableString(request.TransactionalId);
        writer.WriteInt16(request.Acks);
        writer.WriteInt32(request.TimeoutMs);
        writer.WriteUnsignedVarInt(2); // one topic, compact count is item count + 1
        writer.WriteCompactString(topic.Name);
        writer.WriteUnsignedVarInt(2); // one partition
        writer.WriteInt32(partition.Index);

        var encodedBatchSize = batch.GetEncodedSize(partition.Compression);
        writer.WriteUnsignedVarInt(checked(encodedBatchSize + 1));
        if (!batch.TryWriteSegmentedHeader(
                metadataWriter,
                partition.Compression,
                out var encodedRecordsMemory)
            || !MemoryMarshal.TryGetArray(encodedRecordsMemory, out encodedRecords))
        {
            encodedRecords = default;
            return false;
        }

        var segmentedBatchSize = RecordBatch.TotalBatchHeaderSize + encodedRecords.Count;
        if (encodedBatchSize != segmentedBatchSize)
        {
            throw new KafkaException(
                ErrorCode.CorruptMessage,
                $"PRODUCE segmented framing mismatch for partition {partition.Index}: " +
                $"declared batch length {encodedBatchSize} but segmented header and records total " +
                $"{segmentedBatchSize} bytes.");
        }

        writer.AddBytesWritten(encodedBatchSize);
        prefixLength = metadataWriter.WrittenCount;
        writer.WriteEmptyTaggedFields(); // partition
        writer.WriteEmptyTaggedFields(); // topic
        writer.WriteEmptyTaggedFields(); // request
        suffixOffset = prefixLength;
        suffixLength = metadataWriter.WrittenCount - prefixLength;

        var bodyLength = writer.BytesWritten - headerLength;
        if (request.RequestBodySizeHint > 0 && bodyLength != request.RequestBodySizeHint)
        {
            throw new KafkaException(
                ErrorCode.CorruptMessage,
                $"PRODUCE segmented body mismatch: size hint {request.RequestBodySizeHint} but serialized {bodyLength} bytes.");
        }

        var totalLength = checked(metadataWriter.WrittenCount + encodedRecords.Count);
        (metadataArray, _) = metadataWriter.DetachBuffer();
        BinaryPrimitives.WriteInt32BigEndian(metadataArray, totalLength - MessageSizePrefixLength);
        return true;
    }

    private async Task WriteSegmentedFrameHoldingLockAsync(
        byte[] metadataArray,
        int prefixLength,
        ArraySegment<byte> encodedRecords,
        int suffixOffset,
        int suffixLength)
    {
        var scatterGatherSenderLockHeld = false;
        try
        {
            await _scatterGatherSenderLock.WaitAsync().ConfigureAwait(false);
            scatterGatherSenderLockHeld = true;

            var socket = _socket ?? throw new InvalidOperationException("Not connected");
            if (_stream is null)
                throw new InvalidOperationException("Not connected");

            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(KafkaConnection));

            var sender = _scatterGatherSender ??= new SocketScatterGatherSender();
            var sendSegments = sender.Segments;
            sendSegments.Add(new ArraySegment<byte>(metadataArray, 0, prefixLength));
            if (encodedRecords.Count > 0)
                sendSegments.Add(encodedRecords);
            if (suffixLength > 0)
                sendSegments.Add(new ArraySegment<byte>(metadataArray, suffixOffset, suffixLength));

            while (sendSegments.Count > 0)
            {
                var bytesSent = await sender.SendAsync(socket).ConfigureAwait(false);
                if (bytesSent <= 0)
                    throw new IOException("Socket closed while writing a Kafka request frame.");

                ConsumeSentSegments(sendSegments, bytesSent);
            }
        }
        catch
        {
            AbortAfterWriteFailure();
            throw;
        }
        finally
        {
            if (scatterGatherSenderLockHeld)
            {
                _scatterGatherSender?.Segments.Clear();
                SemaphoreHelper.ReleaseSafely(_scatterGatherSenderLock);
            }

            DekafPools.SerializationBuffers.Return(metadataArray, clearArray: false);
            SemaphoreHelper.ReleaseSafely(_writeLock);
        }
    }

    internal static void ConsumeSentSegments(List<ArraySegment<byte>> segments, int bytesSent)
    {
        while (bytesSent > 0)
        {
            var segment = segments[0];
            if (bytesSent < segment.Count)
            {
                segments[0] = new ArraySegment<byte>(
                    segment.Array!,
                    segment.Offset + bytesSent,
                    segment.Count - bytesSent);
                return;
            }

            bytesSent -= segment.Count;
            segments.RemoveAt(0);
        }
    }

    /// <summary>
    /// Writes one complete frame while holding the write lock, then releases the lock and
    /// returns the serialization buffer. The socket write is deliberately not cancellable:
    /// cancelling <see cref="Stream.WriteAsync(ReadOnlyMemory{byte}, CancellationToken)"/>
    /// mid-frame can leave a partial frame on the wire, desyncing the outgoing stream so the
    /// broker misparses it (broker-side InvalidRequestException on PRODUCE followed by a socket
    /// close). Callers that time out abandon the await instead (<see cref="AwaitFrameWriteAsync"/>);
    /// the frame finishes in the background and the connection stays frame-aligned and usable.
    /// </summary>
    private async Task WriteFrameHoldingLockAsync(byte[] serializedData, int length, bool clearArray)
    {
        try
        {
            if (_stream is null)
                throw new InvalidOperationException("Not connected");

            // A previous write on this connection may have faulted mid-frame, leaving the
            // outgoing stream misaligned. Writers already queued on _writeLock must not append
            // another frame to a poisoned stream.
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(KafkaConnection));

            await _stream.WriteAsync(serializedData.AsMemory(0, length), CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // The write faulted (or the connection was already unusable): part of the frame may
            // have been transmitted, so the outgoing stream can no longer be trusted.
            AbortAfterWriteFailure();
            throw;
        }
        finally
        {
            DekafPools.SerializationBuffers.Return(serializedData, clearArray: clearArray);
            SemaphoreHelper.ReleaseSafely(_writeLock);
        }
    }

    /// <summary>
    /// Awaits a frame write with timeout/cancellation semantics. On timeout or caller
    /// cancellation the wait is abandoned — never cancelled — so the frame either completes in
    /// the background (connection stays usable; a transient broker stall no longer tears the
    /// connection down) or faults (connection aborts). A background observer gives an abandoned
    /// write one further request timeout to finish before forcibly aborting the connection to
    /// unblock a socket write stuck on a dead broker.
    /// </summary>
    private ValueTask AwaitFrameWriteAsync(
        Task writeTask,
        int correlationId,
        bool callerOwnsTimeout,
        CancellationToken cancellationToken)
        => AwaitFrameWriteCoreAsync(
            writeTask,
            correlationId,
            callerOwnsTimeout,
            payloadIsBorrowed: false,
            cancellationToken);

    private ValueTask AwaitBorrowedFrameWriteAsync(
        Task writeTask,
        int correlationId,
        bool callerOwnsTimeout,
        CancellationToken cancellationToken)
        => AwaitFrameWriteCoreAsync(
            writeTask,
            correlationId,
            callerOwnsTimeout,
            payloadIsBorrowed: true,
            cancellationToken);

    private async ValueTask AwaitFrameWriteCoreAsync(
        Task writeTask,
        int correlationId,
        bool callerOwnsTimeout,
        bool payloadIsBorrowed,
        CancellationToken cancellationToken)
    {
#if DEBUG
        System.Diagnostics.Debug.Assert(!callerOwnsTimeout || cancellationToken.CanBeCanceled,
            "callerOwnsTimeout path requires a timeout-bearing token");
#endif

        // When callerOwnsTimeout is true, the caller's cancellationToken already carries
        // a timeout (e.g. BrokerSender's sendTimeoutCts), so we skip the WaitAsync timer
        // allocation — a hot-path optimization.
        if (callerOwnsTimeout)
        {
            try
            {
                await writeTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            // callerOwnsTimeout contract: token fires only on timeout, never explicit user cancellation.
            // Any OperationCanceledException here means the caller's timeout elapsed.
            catch (OperationCanceledException)
            {
                if (payloadIsBorrowed)
                    await AbortAndObserveFrameWriteAsync(writeTask).ConfigureAwait(false);
                else
                    AbandonFrameWrite(writeTask, correlationId);

                LogFlushTimeout(_options.RequestTimeout.TotalMilliseconds, correlationId, BrokerId);

                throw new KafkaException(
                    ErrorCode.RequestTimedOut,
                    $"Flush timeout after {(int)_options.RequestTimeout.TotalMilliseconds}ms on connection to broker {BrokerId}");
            }
        }
        else
        {
            try
            {
                await writeTask.WaitAsync(_options.RequestTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (payloadIsBorrowed)
                    await AbortAndObserveFrameWriteAsync(writeTask).ConfigureAwait(false);
                else
                    AbandonFrameWrite(writeTask, correlationId);

                throw new OperationCanceledException(cancellationToken);
            }
            catch (TimeoutException)
            {
                if (payloadIsBorrowed)
                    await AbortAndObserveFrameWriteAsync(writeTask).ConfigureAwait(false);
                else
                    AbandonFrameWrite(writeTask, correlationId);

                LogFlushTimeout(_options.RequestTimeout.TotalMilliseconds, correlationId, BrokerId);

                throw new KafkaException(
                    ErrorCode.RequestTimedOut,
                    $"Flush timeout after {(int)_options.RequestTimeout.TotalMilliseconds}ms on connection to broker {BrokerId}");
            }
        }
    }

    /// <summary>
    /// Borrowed producer buffers cannot outlive their caller. Abort a stalled socket write and
    /// observe its completion before the caller can release or recycle the batch resources.
    /// </summary>
    private async Task AbortAndObserveFrameWriteAsync(Task writeTask)
    {
        if (!writeTask.IsCompleted)
            AbortAfterWriteFailure();

        try
        {
            await writeTask.ConfigureAwait(false);
        }
        catch
        {
            // The timeout/cancellation remains the caller-visible failure.
        }
    }

    private void AbandonFrameWrite(Task writeTask, int correlationId)
        => _ = ObserveAbandonedFrameWriteAsync(writeTask, correlationId);

    private async Task ObserveAbandonedFrameWriteAsync(Task writeTask, int correlationId)
    {
        try
        {
            await _waitForAbandonedWriteAsync(writeTask, _options.RequestTimeout).ConfigureAwait(false);

            // Completed after the caller gave up: the frame is intact, the outgoing stream is
            // still frame-aligned, and the connection remains usable. Any late response is
            // dropped by DispatchResponse as an unknown correlation id.
            LogAbandonedWriteCompleted(correlationId, BrokerId);
        }
        catch (TimeoutException)
        {
            // Stuck for a full further request timeout: the broker is not draining the socket.
            // Abort the connection; disposing the socket unblocks the pending write, which then
            // faults and is observed below.
            LogAbandonedWriteStuck(_options.RequestTimeout.TotalMilliseconds, correlationId, BrokerId);
            AbortAfterWriteFailure();
            try
            {
                await writeTask.ConfigureAwait(false);
            }
            catch
            {
                // Exception observed; the connection is already aborted.
            }
        }
        catch
        {
            // The write faulted after the caller abandoned it; WriteFrameHoldingLockAsync
            // already aborted the connection. Nothing to do beyond observing the exception.
        }
    }

    private static Task WaitForAbandonedWriteAsync(Task writeTask, TimeSpan timeout)
        => writeTask.WaitAsync(timeout);

    /// <summary>
    /// Marks the connection unusable after a frame write faulted or stayed stuck past its grace
    /// period. A faulted <see cref="Stream.WriteAsync(ReadOnlyMemory{byte}, CancellationToken)"/>
    /// can have transmitted part of the frame, so the outgoing stream is no longer frame-aligned:
    /// any further request written to it is misparsed by the broker (surfacing as broker-side
    /// InvalidRequestException on PRODUCE, after which the broker closes the socket while the
    /// producer keeps retrying into 30s timeouts). Marking the connection disposed makes the
    /// ConnectionPool replace it. Starting full disposal releases the pipe/stream owners and
    /// unblocks the receive loop so in-flight requests fail promptly instead of each waiting
    /// out its request timeout.
    /// </summary>
    private void AbortAfterWriteFailure()
    {
        var disposeTask = EnsureDisposeStarted();
        _ = ObserveWriteAbortDisposeAsync(disposeTask);
    }

    private async Task ObserveWriteAbortDisposeAsync(Task disposeTask)
    {
        try
        {
            await disposeTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogWriteAbortDisposeFailed(ex, BrokerId);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        // Capture the reader so a concurrent reconnect can never swap it mid-loop.
        var frameReader = _frameReader;
        if (frameReader is null)
            return;

        if (_responseMemoryAdmissionsEnabled)
        {
            await ReceiveBoundedLoopAsync(frameReader, cancellationToken).ConfigureAwait(false);
            return;
        }

        LogReceiveLoopStarted(_host, _port);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ResponseFrame frame;
                try
                {
                    frame = await frameReader.ReadFrameAsync().ConfigureAwait(false);
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    // Disposal aborted the read source underneath the in-flight read
                    // (reads are not cancellable; Abort is the wake-up mechanism).
                    // The post-loop block fails pending requests as "Connection closing".
                    break;
                }
                catch (Exception) when (ConsumeReceiveTimeoutExpired())
                {
                    // OnReceiveTimeout aborted the source: no bytes arrived within
                    // RequestTimeout while requests were pending. Without this, the
                    // connection stays in the pool appearing healthy and every
                    // subsequent request on it also times out (#670).
                    throw CreateReceiveTimeoutException();
                }

                // The remote peer closed the connection (EOF). Fail any pending requests
                // immediately so callers don't hang waiting for responses that will
                // never arrive. Without this, pending requests rely on their individual
                // RequestTimeout (30s default), which delays error detection and can
                // cause indefinite hangs when combined with BrokerSender retry cycles.
                if (frame.IsEndOfStream)
                {
                    LogReceiveLoopCompleted(_host, _port);
                    MarkDisposed(); // Prevent new requests from being queued on a dead connection
                    FailAllPendingRequests(new KafkaException(
                        "Connection closed by remote peer (EOF)"));
                    break;
                }

                DispatchResponse(frame.CorrelationId, frame.Buffer, reservation: null);
            }

            // While loop exited normally (no exception thrown, so no catch block runs).
            // Two cases: cancellation between iterations, or EOF break (already handled above).
            if (cancellationToken.IsCancellationRequested)
            {
                MarkDisposed();
                FailAllPendingRequests(new OperationCanceledException("Connection closing", cancellationToken));
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
            if (!_hasReceivedResponse)
                LogReceiveLoopEndedBeforeFirstResponse(ex, _host, _port);
            else
                LogReceiveLoopError(ex);

            MarkDisposed(); // Prevent new requests from being queued on a dead connection
            FailAllPendingRequests(ex);
        }
        finally
        {
            DisarmReceiveTimeout();

            // The loop is the only reader; once it exits nothing else touches the reader's
            // buffers, so they can be returned eagerly (DisposeAsync's call is then a no-op).
            frameReader.Dispose();
        }
    }

    // Intentionally separate from ReceiveLoopAsync: the producer receive loop must not pay
    // a per-frame admission branch, larger frame carrier, or reservation handoff.
    private async Task ReceiveBoundedLoopAsync(
        ResponseFrameReader frameReader,
        CancellationToken cancellationToken)
    {
        LogReceiveLoopStarted(_host, _port);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                BoundedResponseFrame frame;
                try
                {
                    frame = await frameReader
                        .ReadBoundedFrameAsync(_responseFrameAdmission)
                        .ConfigureAwait(false);
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception) when (ConsumeReceiveTimeoutExpired())
                {
                    throw CreateReceiveTimeoutException();
                }

                if (frame.IsEndOfStream)
                {
                    LogReceiveLoopCompleted(_host, _port);
                    MarkDisposed();
                    FailAllPendingRequests(new KafkaException(
                        "Connection closed by remote peer (EOF)"));
                    break;
                }

                if (frame.IsDiscarded)
                {
                    DispatchDiscardedResponse(frame.CorrelationId);
                    continue;
                }

                DispatchResponse(frame.CorrelationId, frame.Buffer, frame.Reservation);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                MarkDisposed();
                FailAllPendingRequests(new OperationCanceledException("Connection closing", cancellationToken));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            MarkDisposed();
            FailAllPendingRequests(new OperationCanceledException("Connection closing", cancellationToken));
        }
        catch (Exception ex)
        {
            if (!_hasReceivedResponse)
                LogReceiveLoopEndedBeforeFirstResponse(ex, _host, _port);
            else
                LogReceiveLoopError(ex);

            MarkDisposed();
            FailAllPendingRequests(ex);
        }
        finally
        {
            DisarmReceiveTimeout();
            frameReader.Dispose();
        }
    }

    /// <summary>
    /// Invoked by <see cref="ResponseFrameReader"/> after every successful source read.
    /// Extends the receive-timeout deadline on byte progress (a large frame arriving
    /// slowly must not trip the timeout while the broker is still sending). This is the
    /// hot path — a single volatile store, no lock and no <see cref="Timer.Change(TimeSpan, TimeSpan)"/>;
    /// <see cref="OnReceiveTimeout"/> re-arms itself for the remainder when it fires
    /// before the extended deadline.
    /// </summary>
    private void OnReceiveLoopBytesRead(int bytesRead)
    {
        LogReceivedBytes(bytesRead, _host, _port);

        if (Volatile.Read(ref _receiveTimeoutDeadlineTimestamp) != 0)
            Volatile.Write(ref _receiveTimeoutDeadlineTimestamp, GetReceiveTimeoutDeadlineTimestamp());
    }

    private KafkaException CreateReceiveTimeoutException()
    {
        LogReceiveTimeout(_options.RequestTimeout.TotalMilliseconds, BrokerId);
        MarkDisposed();

        return new KafkaException(
            ErrorCode.RequestTimedOut,
            $"Receive timeout after {(int)_options.RequestTimeout.TotalMilliseconds}ms - connection to broker {BrokerId} failed");
    }

    private void DispatchResponse(
        int correlationId,
        PooledResponseBuffer responseData,
        IResponseMemoryReservation? reservation)
    {
        LogReceivedResponse(correlationId, responseData.Length);

        if (TryGetPendingRequest(correlationId, out var pending))
        {
            _hasReceivedResponse = true;
            if (!pending.Request.TryComplete(pending.Version, responseData, reservation))
            {
                responseData.Dispose();
                reservation?.Dispose();
            }
        }
        else if (_cancelledCorrelationIds.TryRemove(correlationId))
        {
            _hasReceivedResponse = true;
            LogLateResponseForCancelledRequest(correlationId);
            responseData.Dispose();
            reservation?.Dispose();
        }
        else
        {
            LogUnknownCorrelationId(correlationId);
            responseData.Dispose();
            reservation?.Dispose();
        }
    }

    private ResponseFrameAdmission GetResponseFrameAdmission(int correlationId)
    {
        if (!TryGetPendingRequest(correlationId, out var pending))
            return ResponseFrameAdmission.Discarded;

        if (!pending.Request.TryGetResponseMemoryPool(pending.Version, out var memoryPool))
            return ResponseFrameAdmission.Discarded;

        return memoryPool is null
            ? ResponseFrameAdmission.Unrestricted
            : new ResponseFrameAdmission(memoryPool, Discard: false);
    }

    private void DispatchDiscardedResponse(int correlationId)
    {
        if (_cancelledCorrelationIds.TryRemove(correlationId))
        {
            _hasReceivedResponse = true;
            LogLateResponseForCancelledRequest(correlationId);
            return;
        }

        LogUnknownCorrelationId(correlationId);
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
                    return default;

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
        DisarmReceiveTimeout();
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

        Interlocked.Increment(ref _pendingRequestCount);
        RefreshReceiveTimeout();
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

            RefreshReceiveTimeout();

            ReleasePendingRequestSlot();
        }

        return removed;
    }

    private int GetPendingRequestCount() => Volatile.Read(ref _pendingRequestCount);

    private bool HasPendingRequests() => Volatile.Read(ref _pendingRequestCount) != 0;

    private bool TryAcquireLease()
    {
        if (Volatile.Read(ref _retirementState) != 0 || Volatile.Read(ref _disposed) != 0)
            return false;

        Interlocked.Increment(ref _leaseCount);
        if (Volatile.Read(ref _retirementState) == 0 && Volatile.Read(ref _disposed) == 0)
            return true;

        ReleaseLease();
        return false;
    }

    private void ReleaseLease()
    {
        var remaining = Interlocked.Decrement(ref _leaseCount);
        Debug.Assert(remaining >= 0);
    }

    private TrackedOperation TrackOperation()
    {
        Interlocked.Increment(ref _activeOperationCount);
        if (Volatile.Read(ref _retirementState) < 2 && Volatile.Read(ref _disposed) == 0)
            return new TrackedOperation(this);

        EndTrackedOperation();
        throw new ObjectDisposedException(nameof(KafkaConnection), "Connection has been retired");
    }

    private void EndTrackedOperation()
    {
        var remaining = Interlocked.Decrement(ref _activeOperationCount);
        Debug.Assert(remaining >= 0);
    }

    private readonly struct TrackedOperation(KafkaConnection connection) : IDisposable
    {
        public void Dispose() => connection.EndTrackedOperation();
    }

    private void RefreshReceiveTimeout()
    {
        lock (_receiveTimeoutGate)
        {
            if (GetPendingRequestCount() == 0)
                DisarmReceiveTimeout();
            else
                ArmReceiveTimeout();
        }
    }

    private void ArmReceiveTimeout()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        Volatile.Write(ref _receiveTimeoutExpired, 0);
        Volatile.Write(ref _receiveTimeoutDeadlineTimestamp, GetReceiveTimeoutDeadlineTimestamp());
        try
        {
            GetOrCreateReceiveTimeoutTimer().Change(_options.RequestTimeout, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException) when (Volatile.Read(ref _disposed) != 0)
        {
        }
    }

    private void DisarmReceiveTimeout()
    {
        Volatile.Write(ref _receiveTimeoutExpired, 0);
        Volatile.Write(ref _receiveTimeoutDeadlineTimestamp, 0);
        var timer = Volatile.Read(ref _receiveTimeoutTimer);
        if (timer is null)
            return;

        try
        {
            timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException) when (Volatile.Read(ref _disposed) != 0)
        {
        }
    }

    private Timer GetOrCreateReceiveTimeoutTimer()
    {
        var existing = Volatile.Read(ref _receiveTimeoutTimer);
        if (existing is not null)
            return existing;

        var created = new Timer(static state => ((KafkaConnection)state!).OnReceiveTimeout(), this,
            Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        existing = Interlocked.CompareExchange(ref _receiveTimeoutTimer, created, null);
        if (existing is null)
            return created;

        created.Dispose();
        return existing;
    }

    private void OnReceiveTimeout()
    {
        if (Volatile.Read(ref _disposed) != 0 || !HasPendingRequests())
            return;

        var deadlineTimestamp = Volatile.Read(ref _receiveTimeoutDeadlineTimestamp);
        if (deadlineTimestamp == 0)
            return;

        var remainingTimestamp = deadlineTimestamp - Stopwatch.GetTimestamp();
        if (remainingTimestamp > 0)
        {
            // Byte progress moved the deadline forward since the timer was armed
            // (OnReceiveLoopBytesRead only stamps the deadline) — sleep out the remainder.
            RearmReceiveTimeoutForRemainder(remainingTimestamp);
            return;
        }

        Volatile.Write(ref _receiveTimeoutExpired, 1);

        // Reads are not cancellable — aborting the source is how the timer interrupts an
        // in-flight read (see ResponseFrameReader.Abort). The receive loop observes the
        // faulted read together with the expired flag and tears the connection down.
        // The pending-request check above keeps a drained connection alive; the remaining
        // race window (last request completing between that check and the abort) at worst
        // tears down a connection the pool would replace anyway.
        _frameReader?.Abort();
    }

    private void RearmReceiveTimeoutForRemainder(long remainingTimestamp)
    {
        var remaining = TimeSpan.FromSeconds(remainingTimestamp / (double)Stopwatch.Frequency);
        lock (_receiveTimeoutGate)
        {
            if (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _receiveTimeoutDeadlineTimestamp) == 0)
                return;

            try
            {
                GetOrCreateReceiveTimeoutTimer().Change(remaining, Timeout.InfiniteTimeSpan);
            }
            catch (ObjectDisposedException) when (Volatile.Read(ref _disposed) != 0)
            {
            }
        }
    }

    private bool ConsumeReceiveTimeoutExpired()
        => Interlocked.Exchange(ref _receiveTimeoutExpired, 0) != 0;

    private long GetReceiveTimeoutDeadlineTimestamp()
        => Stopwatch.GetTimestamp() + _receiveTimeoutStopwatchTicks;

    private void Touch() => Volatile.Write(ref _lastUsedTimestampMs, Dekaf.MonotonicClock.GetMilliseconds());

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
            ImportCertificatesFromPemFile(collection, path);
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
                if (!string.IsNullOrEmpty(tlsConfig.ClientKeyPassword))
                {
                    throw new PlatformNotSupportedException(
                        "Encrypted PEM client certificates require net10.0 or later. Use a PFX/P12 client certificate for netstandard2.0.");
                }

                if (tlsConfig.ClientKeyPath is null)
                {
                    throw new InvalidOperationException(
                        "Client key path is required when using PEM certificate format");
                }

                cert = PreparePemClientCertificate(
                    CreateCertificateFromPemFile(certPath, tlsConfig.ClientKeyPath));
#else
                // PEM format - need separate key file
                if (tlsConfig.ClientKeyPath is null)
                {
                    throw new InvalidOperationException(
                        "Client key path is required when using PEM certificate format");
                }

                var pemCertificate = string.IsNullOrEmpty(tlsConfig.ClientKeyPassword)
                    ? X509Certificate2.CreateFromPemFile(certPath, tlsConfig.ClientKeyPath)
                    : X509Certificate2.CreateFromEncryptedPemFile(certPath, tlsConfig.ClientKeyPassword, tlsConfig.ClientKeyPath);
                cert = PreparePemClientCertificate(pemCertificate);
#endif
            }

            _loadedClientCertificate = cert;
            return cert;
        }

        return null;
    }

    /// <summary>
    /// Re-imports a PEM certificate/key pair through PKCS#12 so Windows Schannel receives
    /// a persisted key-provider handle. The ephemeral key returned by CreateFromPemFile can
    /// otherwise fail client-credential acquisition with SEC_E_UNKNOWN_CREDENTIALS.
    /// </summary>
    private static X509Certificate2 PreparePemClientCertificate(X509Certificate2 certificate)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return certificate;

        byte[]? pkcs12 = null;
        try
        {
            pkcs12 = certificate.Export(X509ContentType.Pfx);
#if NETSTANDARD2_0
#pragma warning disable SYSLIB0057
            return new X509Certificate2(
                pkcs12,
                password: (string?)null,
                X509KeyStorageFlags.DefaultKeySet);
#pragma warning restore SYSLIB0057
#else
            return X509CertificateLoader.LoadPkcs12(
                pkcs12,
                password: null,
                X509KeyStorageFlags.DefaultKeySet);
#endif
        }
        finally
        {
            if (pkcs12 is not null)
                CryptographicOperations.ZeroMemory(pkcs12);
            certificate.Dispose();
        }
    }

#if NETSTANDARD2_0
    private static void ImportCertificatesFromPemFile(X509Certificate2Collection collection, string path)
    {
        var method = typeof(X509Certificate2Collection).GetMethod(
            "ImportFromPemFile",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
            binder: null,
            types: [typeof(string)],
            modifiers: null);

        if (method is null)
        {
            throw new PlatformNotSupportedException(
                "PEM CA certificate files require a runtime with X509Certificate2Collection.ImportFromPemFile support. Use a PFX/P12 CA file instead.");
        }

        InvokeCertificateApi(() =>
        {
            method.Invoke(collection, [path]);
            return null;
        });
    }

    private static X509Certificate2 CreateCertificateFromPemFile(string certPath, string keyPath)
    {
        var method = typeof(X509Certificate2).GetMethod(
            "CreateFromPemFile",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            binder: null,
            types: [typeof(string), typeof(string)],
            modifiers: null);

        if (method is null)
        {
            throw new PlatformNotSupportedException(
                "PEM client certificates require a runtime with X509Certificate2.CreateFromPemFile support. Use a PFX/P12 client certificate instead.");
        }

        return (X509Certificate2)InvokeCertificateApi(() => method.Invoke(obj: null, parameters: [certPath, keyPath]))!;
    }

    private static object? InvokeCertificateApi(Func<object?> action)
    {
        try
        {
            return action();
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
#endif

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
            ConnectionHelper.ValidateResponseFrameSize(responseSize, MaxSaslResponseFrameSize);

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

    private sealed class SocketScatterGatherSender : IValueTaskSource<int>, IDisposable
    {
        private readonly SocketAsyncEventArgs _eventArgs = new();
        private ManualResetValueTaskSourceCore<int> _core = new()
        {
            RunContinuationsAsynchronously = true
        };

        public SocketScatterGatherSender()
        {
            _eventArgs.Completed += CompleteSend;
        }

        public List<ArraySegment<byte>> Segments { get; } = new(3);

        public ValueTask<int> SendAsync(Socket socket)
        {
            Debug.Assert(_eventArgs.BufferList is null, "Scatter/gather sender reused before completion");
            _core.Reset();
            _eventArgs.BufferList = Segments;
            _eventArgs.SocketFlags = SocketFlags.None;

            try
            {
                if (socket.SendAsync(_eventArgs))
                    return new ValueTask<int>(this, _core.Version);

                var bytesTransferred = GetSynchronousResult();
                _eventArgs.BufferList = null;
                return new ValueTask<int>(bytesTransferred);
            }
            catch
            {
                _eventArgs.BufferList = null;
                throw;
            }
        }

        private int GetSynchronousResult()
        {
            if (_eventArgs.SocketError != SocketError.Success)
                throw new SocketException((int)_eventArgs.SocketError);

            return _eventArgs.BytesTransferred;
        }

        private void CompleteSend(object? sender, SocketAsyncEventArgs eventArgs)
        {
            eventArgs.BufferList = null;
            if (eventArgs.SocketError == SocketError.Success)
                _core.SetResult(eventArgs.BytesTransferred);
            else
                _core.SetException(new SocketException((int)eventArgs.SocketError));
        }

        public int GetResult(short token) => _core.GetResult(token);

        public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

        public void OnCompleted(
            Action<object?> continuation,
            object? state,
            short token,
            ValueTaskSourceOnCompletedFlags flags)
            => _core.OnCompleted(continuation, state, token, flags);

        public void Dispose()
        {
            _eventArgs.Completed -= CompleteSend;
            _eventArgs.Dispose();
        }
    }

    public ValueTask DisposeAsync()
        => new(EnsureDisposeStarted());

    private Task EnsureDisposeStarted()
    {
        lock (_disposeGate)
        {
            return _disposeTask ??= DisposeCoreAsync();
        }
    }

    private async Task DisposeCoreAsync()
    {
        MarkDisposed();
        Volatile.Write(ref _connected, 0);
        var pendingRequestSlotOperationsDrained = ClosePendingRequestSlotsAsync();
        CancelPendingRequestSlotWaiters();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var pendingRequestCount = GetPendingRequestCount();
            LogConnectionDisposing(BrokerId, pendingRequestCount);
        }

        _receiveCts?.Cancel();
        // Reads are not cancellable — abort the read source so an in-flight
        // ResponseFrameReader read faults and the receive loop observes the
        // cancellation promptly instead of waiting for the fallback timeout below.
        _frameReader?.Abort();

        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The receive loop did not exit within 5 seconds despite the aborted
                // source. Nothing further can unblock it — proceed with best-effort
                // disposal (the reader skips buffer recycling while a read is in flight).
                LogReceiveLoopShutdownFailed(ex, BrokerId);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _receiveCts?.Dispose();
        _receiveTimeoutTimer?.Dispose();

        // Return the reader's pooled buffers. Normally a no-op (the receive loop's finally
        // block already disposed it); covers connections that never started a receive loop.
        // If a source read is somehow still in flight (hung teardown above), the reader
        // internally skips buffer returns rather than recycling memory a stray receive
        // could still write into.
        _frameReader?.Dispose();

        // Source teardown, mirroring the old pipe owners: TLS disposes the SslStream first
        // (best-effort close_notify, cascades to the inner NetworkStream via
        // leaveInnerStreamOpen: false), then the socket. Plain TCP closes the socket and
        // then the NetworkStream wrapper (created with ownsSocket: false). Streams injected
        // by tests (no TLS flag) take the plain branch, which also disposes them.
        if (_isTls && _stream is not null)
        {
            try
            {
                await _stream.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // The socket may already have been aborted (receive timeout/disposal wake).
            }

            _socket?.Dispose();
        }
        else
        {
            _socket?.Dispose();
            _stream?.Dispose();
        }

        // Placed after reader/source teardown so all outstanding IMemoryOwner<byte> objects
        // are returned before the pool is released for GC.
        // Only dispose per-connection pools — shared pools are owned by the ConnectionPool.
        if (_sharedPipeMemoryPool is null)
        {
            _pipeMemoryPool?.Dispose();
        }

        _reauthTimer?.Dispose();
        _reauthLock.Dispose();
        _connectLock.Dispose();
        // The sender is shared by segmented writes. Use its dedicated lifetime lock so
        // background write-failure disposal cannot transiently reacquire _writeLock after the
        // faulted writer releases it. This still waits for an active socket send to unwind.
        await _scatterGatherSenderLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _scatterGatherSender?.Dispose();
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_scatterGatherSenderLock);
        }

        // Do not dispose either write semaphore here. Writers queued before disposal must still
        // acquire them, observe the disposed connection, and release them from finally blocks.
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connected to {Host}:{Port}")]
    private partial void LogConnected(string host, int port);

    internal const int SendingRequestEventId = 1001;
    internal const int RequestSentWaitingForResponseEventId = 1002;

    [LoggerMessage(
        EventId = SendingRequestEventId,
        EventName = "SendingRequest",
        Level = LogLevel.Debug,
        Message = "Sending {ApiKey} request (correlation {CorrelationId}, version {Version}) to {Host}:{Port}")]
    private partial void LogSendingRequest(ApiKey apiKey, int correlationId, short version, string host, int port);

    [LoggerMessage(
        EventId = RequestSentWaitingForResponseEventId,
        EventName = "RequestSentWaitingForResponse",
        Level = LogLevel.Debug,
        Message = "Request sent, waiting for response (correlation {CorrelationId})")]
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to dispose connection to broker {BrokerId} after a write failure")]
    private partial void LogWriteAbortDisposeFailed(Exception ex, int brokerId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Abandoned frame write for request {CorrelationId} to broker {BrokerId} completed; connection remains usable")]
    private partial void LogAbandonedWriteCompleted(int correlationId, int brokerId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Abandoned frame write for request {CorrelationId} to broker {BrokerId} still incomplete after a further {Timeout}ms; aborting connection")]
    private partial void LogAbandonedWriteStuck(double timeout, int correlationId, int brokerId);

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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Receive loop ended before first response from {Host}:{Port}; broker may still be starting")]
    private partial void LogReceiveLoopEndedBeforeFirstResponse(Exception ex, string host, int port);

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
    /// No longer used: the receive path reads frames directly into pooled response
    /// buffers instead of going through a pipe. Retained for compatibility.
    /// </summary>
    public int MinimumSegmentSize { get; init; } = 4096;

    /// <summary>
    /// Minimum read size for pipe reader.
    /// No longer used: the receive path reads frames directly into pooled response
    /// buffers instead of going through a pipe. Retained for compatibility.
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
    // Managed arrays at or above this size enter the LOH and force full collections while
    // high-throughput consumers warm their response-buffer buckets.
    internal const int NativeMemoryThresholdBytes = 85_000;

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
    private const int DefaultManagedArraysPerBucket = 4;
#if !NETSTANDARD2_0
    private const long NativeMemoryPressureRefreshMilliseconds = 1_000;
#endif
    internal static readonly ResponseBufferPool Default = new(
        DefaultMaxArrayLength,
        DefaultManagedArraysPerBucket,
        maxRetainedNativeBuffers: DefaultManagedArraysPerBucket,
        monitorNativeMemoryPressure: true);
    private static readonly ConcurrentDictionary<
        (int MaxArrayLength, int ManagedArraysPerBucket, int MaxRetainedNativeBuffers),
        ResponseBufferPool>
        s_sharedPools = new();
    private readonly ConcurrentDictionary<int, NativeBufferBucket> _nativeBuckets = new();
    private int _retainedNativeBufferCount;
    private int _highNativeMemoryPressure;
#if !NETSTANDARD2_0
    private long _nextNativeMemoryPressureRefresh;
    private readonly bool _monitorNativeMemoryPressure;
#endif

    internal ArrayPool<byte> Pool { get; }
    internal int MaxArrayLength { get; }
    internal int ManagedArraysPerBucket { get; }
    internal int MaxRetainedNativeBuffers { get; }
    internal int RetainedNativeBufferCount => Volatile.Read(ref _retainedNativeBufferCount);

    internal ResponseBufferPool(
        int maxArrayLength,
        int managedArraysPerBucket = DefaultManagedArraysPerBucket,
        int? maxRetainedNativeBuffers = null,
        bool monitorNativeMemoryPressure = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(managedArraysPerBucket);
        var nativeRetentionLimit = maxRetainedNativeBuffers ?? managedArraysPerBucket;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nativeRetentionLimit);
        MaxArrayLength = maxArrayLength;
        ManagedArraysPerBucket = managedArraysPerBucket;
        MaxRetainedNativeBuffers = nativeRetentionLimit;
#if !NETSTANDARD2_0
        _monitorNativeMemoryPressure = monitorNativeMemoryPressure;
#endif
        Pool = ArrayPool<byte>.Create(
            maxArrayLength: maxArrayLength,
            maxArraysPerBucket: managedArraysPerBucket);
    }

    /// <summary>
    /// Returns a process-shared <see cref="ResponseBufferPool"/> sized for the given
    /// <c>FetchMaxBytes</c> configuration. The pool's max array length is rounded up to
    /// a MiB tier after adding protocol overhead, with a minimum of <see cref="DefaultMaxArrayLength"/>.
    /// </summary>
    internal static ResponseBufferPool Create(
        int fetchMaxBytes,
        int managedArraysPerBucket = DefaultManagedArraysPerBucket,
        int? maxRetainedNativeBuffers = null)
    {
        var maxArrayLength = ComputeMaxArrayLength(fetchMaxBytes);
        var nativeRetentionLimit = maxRetainedNativeBuffers ?? managedArraysPerBucket;
        return maxArrayLength == DefaultMaxArrayLength
            && managedArraysPerBucket == DefaultManagedArraysPerBucket
            && nativeRetentionLimit == DefaultManagedArraysPerBucket
            ? Default
            : s_sharedPools.GetOrAdd(
                (maxArrayLength, managedArraysPerBucket, nativeRetentionLimit),
                static configuration => new ResponseBufferPool(
                    configuration.MaxArrayLength,
                    configuration.ManagedArraysPerBucket,
                    configuration.MaxRetainedNativeBuffers,
                    monitorNativeMemoryPressure: true));
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

    internal NativeResponseBuffer RentNative(int minimumLength)
    {
        var capacity = RoundUpToPowerOfTwo(minimumLength);
        var bucket = _nativeBuckets.GetOrAdd(
            capacity,
            static _ => new NativeBufferBucket());
        if (bucket.TryRent(out var pointer))
        {
            Interlocked.Decrement(ref _retainedNativeBufferCount);
            return NativeResponseBuffer.Rent(this, pointer, capacity);
        }

        return NativeResponseBuffer.Rent(this, Marshal.AllocHGlobal(capacity), capacity);
    }

    internal void ReturnNative(IntPtr pointer, int capacity)
    {
#if !NETSTANDARD2_0
        RefreshNativeMemoryPressure();
#endif
        if (Volatile.Read(ref _highNativeMemoryPressure) != 0)
        {
            Marshal.FreeHGlobal(pointer);
            return;
        }

        if (Interlocked.Increment(ref _retainedNativeBufferCount) > MaxRetainedNativeBuffers)
        {
            Interlocked.Decrement(ref _retainedNativeBufferCount);
            Marshal.FreeHGlobal(pointer);
            return;
        }

        var bucket = _nativeBuckets.GetOrAdd(
            capacity,
            static _ => new NativeBufferBucket());
        bucket.Return(pointer);

        // Pressure can begin after the first check but before this buffer is published.
        if (Volatile.Read(ref _highNativeMemoryPressure) != 0)
            TrimNativeBuffers();
    }

    internal int TrimNativeBuffers()
    {
        var released = 0;
        foreach (var bucket in _nativeBuckets.Values)
            released += bucket.Trim();

        if (released > 0)
            Interlocked.Add(ref _retainedNativeBufferCount, -released);
        return released;
    }

    internal static bool IsHighMemoryLoad(long memoryLoadBytes, long highMemoryLoadThresholdBytes)
        => highMemoryLoadThresholdBytes > 0 && memoryLoadBytes >= highMemoryLoadThresholdBytes;

    internal void UpdateNativeMemoryPressure(long memoryLoadBytes, long highMemoryLoadThresholdBytes)
    {
        var isHigh = IsHighMemoryLoad(memoryLoadBytes, highMemoryLoadThresholdBytes);
        var wasHigh = Interlocked.Exchange(ref _highNativeMemoryPressure, isHigh ? 1 : 0) != 0;
        if (isHigh && !wasHigh)
            TrimNativeBuffers();
    }

#if !NETSTANDARD2_0
    private void RefreshNativeMemoryPressure()
    {
        // Sampling is demand-driven to avoid one background timer per shared pool. An idle
        // pool remains bounded by MaxRetainedNativeBuffers and trims on its next return.
        if (!_monitorNativeMemoryPressure)
            return;

        var now = Environment.TickCount64;
        var nextRefresh = Volatile.Read(ref _nextNativeMemoryPressureRefresh);
        if (now < nextRefresh
            || Interlocked.CompareExchange(
                ref _nextNativeMemoryPressureRefresh,
                now + NativeMemoryPressureRefreshMilliseconds,
                nextRefresh) != nextRefresh)
        {
            return;
        }

        var memoryInfo = GC.GetGCMemoryInfo();
        UpdateNativeMemoryPressure(
            memoryInfo.MemoryLoadBytes,
            memoryInfo.HighMemoryLoadThresholdBytes);
    }
#endif

    private static int RoundUpToPowerOfTwo(int value)
    {
        if (value > 1 << 30)
            return value;

        var rounded = (uint)(value - 1);
        rounded |= rounded >> 1;
        rounded |= rounded >> 2;
        rounded |= rounded >> 4;
        rounded |= rounded >> 8;
        rounded |= rounded >> 16;
        rounded++;
        return (int)rounded;
    }

    private sealed class NativeBufferBucket
    {
        private readonly ConcurrentStack<IntPtr> _buffers = new();

        internal bool TryRent(out IntPtr pointer) => _buffers.TryPop(out pointer);

        internal void Return(IntPtr pointer) => _buffers.Push(pointer);

        internal int Trim()
        {
            var released = 0;
            while (_buffers.TryPop(out var pointer))
            {
                Marshal.FreeHGlobal(pointer);
                released++;
            }

            return released;
        }
    }
}

/// <summary>
/// Contiguous pooled response storage outside the managed large object heap. Ownership
/// follows the same explicit transfer and disposal path as managed pooled responses.
/// </summary>
internal sealed unsafe class NativeResponseBuffer : MemoryManager<byte>
{
    private static readonly NativeResponseBufferPool s_pool = new();

    private ResponseBufferPool? _pool;
    private IntPtr _pointer;
    private int _capacity;
    private int _disposed = 1;

    private NativeResponseBuffer() { }

    internal IntPtr Address => _pointer;

    internal static NativeResponseBuffer Rent(ResponseBufferPool pool, IntPtr pointer, int capacity)
    {
        var buffer = s_pool.Rent();
        buffer._pool = pool;
        buffer._pointer = pointer;
        buffer._capacity = capacity;
        Volatile.Write(ref buffer._disposed, 0);
        return buffer;
    }

    public override Span<byte> GetSpan()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return new Span<byte>((void*)_pointer, _capacity);
    }

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)elementIndex, (uint)_capacity);
        return new MemoryHandle((byte*)_pointer + elementIndex, default, this);
    }

    public override void Unpin() { }

    internal void Return() => Dispose(disposing: true);

    protected override void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var pool = _pool!;
        var pointer = _pointer;
        var capacity = _capacity;
        _pool = null;
        _pointer = IntPtr.Zero;
        _capacity = 0;
        pool.ReturnNative(pointer, capacity);
        s_pool.Return(this);
    }

    private sealed class NativeResponseBufferPool() : ObjectPool<NativeResponseBuffer>(maxPoolSize: 256)
    {
        protected override NativeResponseBuffer Create() => new();
        protected override void Reset(NativeResponseBuffer item) { }
    }
}

internal readonly struct PooledResponseBuffer : IDisposable
{
    private readonly byte[]? _buffer;
    private readonly NativeResponseBuffer? _nativeBuffer;
    private readonly int _offset;
    private readonly ResponseBufferPool? _pool;

    public PooledResponseBuffer(
        byte[] buffer,
        int length,
        bool isPooled,
        int offset = 0,
        ResponseBufferPool? pool = null)
    {
        _buffer = buffer;
        _nativeBuffer = null;
        Length = length;
        IsPooled = isPooled;
        _offset = offset;
        _pool = pool;
    }

    internal PooledResponseBuffer(
        NativeResponseBuffer buffer,
        int length,
        int offset = 0)
    {
        _buffer = null;
        _nativeBuffer = buffer;
        Length = length;
        IsPooled = true;
        _offset = offset;
        _pool = null;
    }

    public byte[] Buffer => _buffer ?? throw new InvalidOperationException("Response uses native memory.");
    public int Length { get; }
    public bool IsPooled { get; }
    internal bool UsesNativeMemory => _nativeBuffer is not null;

    public ReadOnlyMemory<byte> Data => _nativeBuffer is not null
        ? _nativeBuffer.Memory.Slice(_offset, Length)
        : _buffer.AsMemory(_offset, Length);

    /// <summary>
    /// Creates a new view of the buffer with an adjusted offset.
    /// Ownership is transferred - the caller should not dispose the original buffer.
    /// </summary>
    public PooledResponseBuffer Slice(int additionalOffset)
    {
        return _nativeBuffer is not null
            ? new PooledResponseBuffer(
                _nativeBuffer,
                Length - additionalOffset,
                _offset + additionalOffset)
            : new PooledResponseBuffer(
                _buffer!,
                Length - additionalOffset,
                IsPooled,
                _offset + additionalOffset,
                _pool);
    }

    /// <summary>
    /// Transfers ownership of this buffer to a new PooledResponseMemory instance.
    /// After calling this, the buffer should NOT be disposed via this struct.
    /// </summary>
    public PooledResponseMemory TransferOwnership(
        IResponseMemoryReservation? reservation = null)
    {
        return _nativeBuffer is not null
            ? PooledResponseMemory.Create(_nativeBuffer, Length, _offset, reservation)
            : PooledResponseMemory.Create(_buffer!, Length, IsPooled, _offset, _pool, reservation);
    }

    public void Dispose()
    {
        if (_nativeBuffer is not null)
        {
            _nativeBuffer.Return();
        }
        else if (IsPooled && _buffer is not null)
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
    private NativeResponseBuffer? _nativeBuffer;
    private int _length;
    private bool _isPooled;
    private int _offset;
    private ResponseBufferPool? _pool;
    private IResponseMemoryReservation? _reservation;
    private int _pooled;
    private int _disposed = 1;

    private PooledResponseMemory() { }

    internal static PooledResponseMemory Create(
        byte[] buffer,
        int length,
        bool isPooled,
        int offset,
        ResponseBufferPool? pool = null,
        IResponseMemoryReservation? reservation = null)
    {
        var memory = s_pool.Rent();
        memory.Initialize(buffer, length, isPooled, offset, pool, reservation, pooled: true);
        return memory;
    }

    internal static PooledResponseMemory Create(
        NativeResponseBuffer buffer,
        int length,
        int offset,
        IResponseMemoryReservation? reservation = null)
    {
        var memory = s_pool.Rent();
        memory._buffer = null;
        memory._nativeBuffer = buffer;
        memory._length = length;
        memory._isPooled = true;
        memory._offset = offset;
        memory._pool = null;
        memory._reservation = reservation;
        memory._pooled = 1;
        Volatile.Write(ref memory._disposed, 0);
        return memory;
    }

    private void Initialize(
        byte[] buffer,
        int length,
        bool isPooled,
        int offset,
        ResponseBufferPool? pool,
        IResponseMemoryReservation? reservation,
        bool pooled)
    {
        _buffer = buffer;
        _nativeBuffer = null;
        _length = length;
        _isPooled = isPooled;
        _offset = offset;
        _pool = pool;
        _reservation = reservation;
        _pooled = pooled ? 1 : 0;
        Volatile.Write(ref _disposed, 0);
    }

    public ReadOnlyMemory<byte> Memory => _nativeBuffer is not null
        ? _nativeBuffer.Memory.Slice(_offset, _length)
        : _buffer is not null
            ? _buffer.AsMemory(_offset, _length)
            : throw new ObjectDisposedException(nameof(PooledResponseMemory));

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var buffer = Interlocked.Exchange(ref _buffer, null);
        var nativeBuffer = Interlocked.Exchange(ref _nativeBuffer, null);
        var reservation = Interlocked.Exchange(ref _reservation, null);
        try
        {
            nativeBuffer?.Return();
            if (_isPooled && buffer is not null)
            {
                Debug.Assert(_pool is not null, "Pooled buffer must have a non-null pool reference");
                _pool!.Pool.Return(buffer);
            }
        }
        finally
        {
            reservation?.Dispose();
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
            item._nativeBuffer = null;
            item._length = 0;
            item._isPooled = false;
            item._offset = 0;
            item._pool = null;
            item._reservation = null;
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
    private bool _checkCrcs;
    private IResponseMemoryPool? _responseMemoryPool;
    private IResponseMemoryReservation? _responseMemoryReservation;
    private int _state; // High 16 bits = core version; low 16 bits = State*

    /// <summary>
    /// Initializes the request for a new operation.
    /// </summary>
    public void Initialize(
        short responseHeaderVersion,
        CancellationToken cancellationToken,
        bool registerCancellation = true,
        bool checkCrcs = false,
        bool runContinuationsAsynchronously = true,
        IResponseMemoryPool? responseMemoryPool = null)
    {
        _responseHeaderVersion = responseHeaderVersion;
        _cancellationToken = cancellationToken;
        _checkCrcs = checkCrcs;
        _responseMemoryPool = responseMemoryPool;
        _core.RunContinuationsAsynchronously = runContinuationsAsynchronously;
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

    public bool CheckCrcs => _checkCrcs;

    public bool TryGetResponseMemoryPool(
        short expectedVersion,
        out IResponseMemoryPool? responseMemoryPool)
    {
        if (Volatile.Read(ref _state) != CreateState(expectedVersion, StatePending))
        {
            responseMemoryPool = null;
            return false;
        }

        responseMemoryPool = _responseMemoryPool;
        return true;
    }

    public IResponseMemoryReservation? TakeResponseMemoryReservation() =>
        Interlocked.Exchange(ref _responseMemoryReservation, null);

    /// <summary>
    /// Attempts to complete the request with response data.
    /// Returns false if already completed (cancelled or failed).
    /// </summary>
    public bool TryComplete(PooledResponseBuffer pooledBuffer)
        => TryComplete(_core.Version, pooledBuffer, reservation: null);

    /// <summary>
    /// Attempts to complete the request with response data for the captured version.
    /// Returns false if the request was already completed or reused.
    /// </summary>
    public bool TryComplete(short expectedVersion, PooledResponseBuffer pooledBuffer)
        => TryComplete(expectedVersion, pooledBuffer, reservation: null);

    internal bool TryComplete(
        short expectedVersion,
        PooledResponseBuffer pooledBuffer,
        IResponseMemoryReservation? reservation)
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
        _responseMemoryReservation = reservation;

        try
        {
            result = ParseAndSliceResponse(pooledBuffer);
        }
        catch (Exception ex)
        {
            pooledBuffer.Dispose();
            Interlocked.Exchange(ref _responseMemoryReservation, null)?.Dispose();
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
        _checkCrcs = false;
        _responseMemoryPool = null;
        Interlocked.Exchange(ref _responseMemoryReservation, null)?.Dispose();

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

