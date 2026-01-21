using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Dekaf.Protocol;
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
    private volatile bool _disposed;

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

        if (_options.UseTls)
        {
            var sslStream = new SslStream(networkStream, leaveInnerStreamOpen: false);
            await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = _host,
                RemoteCertificateValidationCallback = _options.RemoteCertificateValidationCallback
            }, cancellationToken).ConfigureAwait(false);
            networkStream = sslStream;
        }

        _stream = networkStream;

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

            var responseData = await pending.Task.ConfigureAwait(false);

            var reader = new KafkaProtocolReader(responseData);
            return (TResponse)TResponse.Read(ref reader, apiVersion);
        }
        finally
        {
            _pendingRequests.TryRemove(correlationId, out _);
        }
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

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                var buffer = result.Buffer;

                while (TryReadResponse(ref buffer, out var correlationId, out var responseData))
                {
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
                    break;
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
        out ReadOnlyMemory<byte> responseData)
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

        // Get the response data (we keep the header for the caller to parse properly)
        responseData = responseBuffer.ToArray();

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
            }
        }
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

        FailAllPendingRequests(new ObjectDisposedException(nameof(KafkaConnection)));
    }

    private sealed class PendingRequest
    {
        private readonly TaskCompletionSource<ReadOnlyMemory<byte>> _tcs;
        private readonly short _responseHeaderVersion;

        public PendingRequest(short responseHeaderVersion, CancellationToken cancellationToken)
        {
            _responseHeaderVersion = responseHeaderVersion;
            _tcs = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);

            cancellationToken.Register(() => _tcs.TrySetCanceled(cancellationToken));
        }

        public Task<ReadOnlyMemory<byte>> Task => _tcs.Task;

        public void Complete(ReadOnlyMemory<byte> data)
        {
            // Skip the response header (correlation ID already read, skip tagged fields if flexible)
            var offset = 4; // Correlation ID already parsed
            if (_responseHeaderVersion >= 1)
            {
                // Skip tagged fields - read varint count and skip
                var span = data.Span[offset..];
                var (tagCount, bytesRead) = ReadUnsignedVarInt(span);
                offset += bytesRead;

                for (var i = 0; i < tagCount; i++)
                {
                    span = data.Span[offset..];
                    var (_, tagBytesRead) = ReadUnsignedVarInt(span);
                    offset += tagBytesRead;

                    span = data.Span[offset..];
                    var (size, sizeBytesRead) = ReadUnsignedVarInt(span);
                    offset += sizeBytesRead + size;
                }
            }

            _tcs.TrySetResult(data[offset..]);
        }

        public void Fail(Exception ex)
        {
            _tcs.TrySetException(ex);
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
    /// Custom certificate validation callback.
    /// </summary>
    public RemoteCertificateValidationCallback? RemoteCertificateValidationCallback { get; init; }

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
