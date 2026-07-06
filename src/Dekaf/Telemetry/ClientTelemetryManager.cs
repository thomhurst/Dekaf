using Dekaf.Compression;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using CompressionType = Dekaf.Protocol.Records.CompressionType;

namespace Dekaf.Telemetry;

internal sealed record ClientTelemetrySubscription(
    Guid ClientInstanceId,
    int SubscriptionId,
    sbyte CompressionType,
    int PushIntervalMs,
    int TelemetryMaxBytes,
    bool DeltaTemporality,
    IReadOnlyList<string> RequestedMetrics);

internal interface IClientTelemetryPayloadProvider
{
    ReadOnlyMemory<byte> Collect(
        ClientTelemetrySubscription subscription,
        ClientTelemetryMetricSnapshot metrics,
        bool terminating);
}

internal sealed class EmptyClientTelemetryPayloadProvider : IClientTelemetryPayloadProvider
{
    public static EmptyClientTelemetryPayloadProvider Instance { get; } = new();

    public ReadOnlyMemory<byte> Collect(
        ClientTelemetrySubscription subscription,
        ClientTelemetryMetricSnapshot metrics,
        bool terminating) =>
        ReadOnlyMemory<byte>.Empty;
}

internal sealed partial class ClientTelemetryManager : IAsyncDisposable
{
    private static readonly TimeSpan DefaultStopTimeout = TimeSpan.FromSeconds(5);

    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
    private readonly ClientTelemetryMetricCollector? _metricCollector;
    private readonly CompressionCodecRegistry _compressionCodecs;
    private readonly IClientTelemetryPayloadProvider _payloadProvider;
    private readonly ILogger<ClientTelemetryManager> _logger;
    private readonly SemaphoreSlim _startLock = new(1, 1);

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private ClientTelemetrySubscription? _subscription;
    private int _started;
    private int _stopped;
    private int _disabled;
    private int _disposed;

    public ClientTelemetryManager(
        IConnectionPool connectionPool,
        MetadataManager metadataManager,
        ILogger<ClientTelemetryManager>? logger = null,
        ClientTelemetryMetricCollector? metricCollector = null,
        CompressionCodecRegistry? compressionCodecs = null,
        IClientTelemetryPayloadProvider? payloadProvider = null)
    {
        _connectionPool = connectionPool;
        _metadataManager = metadataManager;
        _metricCollector = metricCollector;
        _compressionCodecs = compressionCodecs ?? CompressionCodecRegistry.Default;
        _logger = logger ?? NullLogger<ClientTelemetryManager>.Instance;
        _payloadProvider = payloadProvider ?? new ClientTelemetryPayloadProvider(_compressionCodecs);
    }

    internal Guid ClientInstanceId => _subscription?.ClientInstanceId ?? Guid.Empty;
    internal int SubscriptionId => _subscription?.SubscriptionId ?? -1;
    internal bool IsDisabled => Volatile.Read(ref _disabled) != 0;
    internal bool IsStarted => Volatile.Read(ref _started) != 0;

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0 ||
            Volatile.Read(ref _stopped) != 0 ||
            Volatile.Read(ref _started) != 0 ||
            Volatile.Read(ref _disabled) != 0)
        {
            return;
        }

        await _startLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _disposed) != 0 ||
                Volatile.Read(ref _stopped) != 0 ||
                Volatile.Read(ref _started) != 0 ||
                Volatile.Read(ref _disabled) != 0)
            {
                return;
            }

            var subscription = await TryFetchSubscriptionAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
            if (subscription is null)
            {
                return;
            }

            if (Volatile.Read(ref _disposed) != 0 ||
                Volatile.Read(ref _stopped) != 0 ||
                Volatile.Read(ref _disabled) != 0)
            {
                return;
            }

            _subscription = subscription;
            var loopCts = new CancellationTokenSource();
            _loopCts = loopCts;
            Volatile.Write(ref _started, 1);
            _loopTask = Task.Run(() => RunLoopAsync(loopCts.Token), CancellationToken.None);
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async ValueTask StopAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        if (timeout <= TimeSpan.Zero)
        {
            timeout = DefaultStopTimeout;
        }

        await _startLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
            var stopToken = linkedCts.Token;

            _loopCts?.Cancel();

            var loopTask = _loopTask;
            if (loopTask is not null)
            {
                try
                {
                    await loopTask.WaitAsync(stopToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Shutdown is bounded by stopToken.
                }
                catch (Exception ex)
                {
                    LogTelemetryLoopFailed(ex);
                }
            }

            if (!stopToken.IsCancellationRequested &&
                Volatile.Read(ref _disabled) == 0 &&
                _subscription is { } subscription)
            {
                try
                {
                    _ = await PushTelemetryAsync(subscription, terminating: true, stopToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Shutdown is bounded by stopToken.
                }
                catch (Exception ex)
                {
                    LogTerminatingPushFailed(ex);
                }
            }

            _loopCts?.Dispose();
            _loopCts = null;
            _loopTask = null;
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await StopAsync(DefaultStopTimeout).ConfigureAwait(false);
        _startLock.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && Volatile.Read(ref _disabled) == 0)
            {
                var subscription = _subscription;
                if (subscription is null)
                {
                    return;
                }

                await Task.Delay(
                    TimeSpan.FromMilliseconds(Math.Max(1, subscription.PushIntervalMs)),
                    cancellationToken).ConfigureAwait(false);

                var errorCode = await PushTelemetryAsync(
                    subscription,
                    terminating: false,
                    cancellationToken).ConfigureAwait(false);

                if (errorCode == ErrorCode.UnknownSubscriptionId)
                {
                    var refreshed = await TryFetchSubscriptionAsync(
                        subscription.ClientInstanceId,
                        cancellationToken).ConfigureAwait(false);

                    if (refreshed is not null)
                    {
                        _subscription = refreshed;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown.
        }
        catch (Exception ex)
        {
            LogTelemetryLoopFailed(ex);
        }
    }

    private async ValueTask<ClientTelemetrySubscription?> TryFetchSubscriptionAsync(
        Guid clientInstanceId,
        CancellationToken cancellationToken)
    {
        if (!TryGetNegotiatedVersion(
                ApiKey.GetTelemetrySubscriptions,
                GetTelemetrySubscriptionsRequest.LowestSupportedVersion,
                GetTelemetrySubscriptionsRequest.HighestSupportedVersion,
                out var apiVersion) ||
            !TryGetNegotiatedVersion(
                ApiKey.PushTelemetry,
                PushTelemetryRequest.LowestSupportedVersion,
                PushTelemetryRequest.HighestSupportedVersion,
                out _))
        {
            Disable();
            return null;
        }

        try
        {
            var connection = await GetTelemetryConnectionAsync(cancellationToken).ConfigureAwait(false);
            if (connection is null)
            {
                return null;
            }

            var response = await connection.SendAsync<GetTelemetrySubscriptionsRequest, GetTelemetrySubscriptionsResponse>(
                new GetTelemetrySubscriptionsRequest { ClientInstanceId = clientInstanceId },
                apiVersion,
                cancellationToken).ConfigureAwait(false);

            if (response.ErrorCode == ErrorCode.UnsupportedVersion)
            {
                Disable();
                return null;
            }

            if (response.ErrorCode != ErrorCode.None)
            {
                LogSubscriptionRejected(response.ErrorCode);
                return null;
            }

            if (!TrySelectCompression(response.AcceptedCompressionTypes, out var compressionType))
            {
                Disable();
                return null;
            }

            var assignedClientInstanceId = response.ClientInstanceId != Guid.Empty
                ? response.ClientInstanceId
                : clientInstanceId;

            if (assignedClientInstanceId == Guid.Empty)
            {
                LogSubscriptionRejected(ErrorCode.InvalidRequest);
                return null;
            }

            return new ClientTelemetrySubscription(
                assignedClientInstanceId,
                response.SubscriptionId,
                compressionType,
                response.PushIntervalMs,
                response.TelemetryMaxBytes,
                response.DeltaTemporality,
                response.RequestedMetrics);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogSubscriptionFailed(ex);
            return null;
        }
    }

    private async ValueTask<ErrorCode> PushTelemetryAsync(
        ClientTelemetrySubscription subscription,
        bool terminating,
        CancellationToken cancellationToken)
    {
        if (!TryGetNegotiatedVersion(
                ApiKey.PushTelemetry,
                PushTelemetryRequest.LowestSupportedVersion,
                PushTelemetryRequest.HighestSupportedVersion,
                out var apiVersion))
        {
            Disable();
            return ErrorCode.UnsupportedVersion;
        }

        try
        {
            var connection = await GetTelemetryConnectionAsync(cancellationToken).ConfigureAwait(false);
            if (connection is null)
            {
                return ErrorCode.BrokerNotAvailable;
            }

            var metricSnapshot = _metricCollector?.Collect(subscription) ??
                ClientTelemetryMetricSnapshot.Empty(subscription.DeltaTemporality);
            var payload = _payloadProvider.Collect(subscription, metricSnapshot, terminating);
            if (subscription.TelemetryMaxBytes > 0 && payload.Length > subscription.TelemetryMaxBytes)
            {
                LogTelemetryPayloadTooLarge(payload.Length, subscription.TelemetryMaxBytes);
                payload = ReadOnlyMemory<byte>.Empty;
            }

            var response = await SendPushTelemetryAsync(
                connection,
                subscription,
                terminating,
                payload,
                apiVersion,
                cancellationToken).ConfigureAwait(false);

            if (response.ErrorCode == ErrorCode.TelemetryTooLarge && payload.Length > 0)
            {
                LogTelemetryPayloadRejectedTooLarge(payload.Length);
                payload = ReadOnlyMemory<byte>.Empty;
                response = await SendPushTelemetryAsync(
                    connection,
                    subscription,
                    terminating,
                    payload,
                    apiVersion,
                    cancellationToken).ConfigureAwait(false);
            }

            if (response.ErrorCode == ErrorCode.UnsupportedVersion)
            {
                Disable();
            }
            else if (IsFatalPushError(response.ErrorCode))
            {
                LogPushRejected(response.ErrorCode);
                Disable();
            }
            else if (response.ErrorCode != ErrorCode.None &&
                     response.ErrorCode != ErrorCode.UnknownSubscriptionId)
            {
                LogPushRejected(response.ErrorCode);
            }

            return response.ErrorCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogPushFailed(ex, terminating);
            return ErrorCode.NetworkException;
        }
    }

    private static ValueTask<PushTelemetryResponse> SendPushTelemetryAsync(
        IKafkaConnection connection,
        ClientTelemetrySubscription subscription,
        bool terminating,
        ReadOnlyMemory<byte> payload,
        short apiVersion,
        CancellationToken cancellationToken) =>
        connection.SendAsync<PushTelemetryRequest, PushTelemetryResponse>(
            new PushTelemetryRequest
            {
                ClientInstanceId = subscription.ClientInstanceId,
                SubscriptionId = subscription.SubscriptionId,
                Terminating = terminating,
                CompressionType = subscription.CompressionType,
                Metrics = payload
            },
            apiVersion,
            cancellationToken);

    private static bool IsFatalPushError(ErrorCode errorCode) =>
        errorCode == ErrorCode.InvalidRequest ||
        errorCode == ErrorCode.InvalidRecord;

    private async ValueTask<IKafkaConnection?> GetTelemetryConnectionAsync(CancellationToken cancellationToken)
    {
        var brokerId = SelectBrokerId();
        return brokerId is null
            ? null
            : await _connectionPool.GetConnectionAsync(brokerId.Value, cancellationToken).ConfigureAwait(false);
    }

    private int? SelectBrokerId()
    {
        var controllerId = _metadataManager.Metadata.ControllerId;
        if (controllerId >= 0 && _metadataManager.Metadata.GetBroker(controllerId) is not null)
        {
            return controllerId;
        }

        var brokers = _metadataManager.Metadata.GetBrokers();
        return brokers.Count == 0 ? null : brokers[0].NodeId;
    }

    private bool TryGetNegotiatedVersion(ApiKey apiKey, short lowestSupportedVersion, short highestSupportedVersion, out short apiVersion)
    {
        apiVersion = lowestSupportedVersion;
        if (!_metadataManager.HasApiKey(apiKey))
        {
            return false;
        }

        var negotiated = _metadataManager.GetNegotiatedApiVersion(apiKey, lowestSupportedVersion, highestSupportedVersion);
        if (negotiated < lowestSupportedVersion || negotiated > highestSupportedVersion)
        {
            return false;
        }

        apiVersion = negotiated;
        return true;
    }

    private bool TrySelectCompression(IReadOnlyList<sbyte> acceptedCompressionTypes, out sbyte compressionType)
    {
        compressionType = (sbyte)CompressionType.None;

        if (acceptedCompressionTypes.Count == 0)
        {
            return true;
        }

        for (var i = 0; i < acceptedCompressionTypes.Count; i++)
        {
            if (TryGetTelemetryCompressionType(acceptedCompressionTypes[i], out var type) &&
                _compressionCodecs.IsSupported(type))
            {
                compressionType = acceptedCompressionTypes[i];
                return true;
            }
        }

        return false;
    }

    private static bool TryGetTelemetryCompressionType(sbyte compressionTypeId, out CompressionType compressionType)
    {
        compressionType = compressionTypeId switch
        {
            0 => CompressionType.None,
            1 => CompressionType.Gzip,
            2 => CompressionType.Snappy,
            3 => CompressionType.Lz4,
            4 => CompressionType.Zstd,
            _ => default
        };

        return compressionTypeId is >= 0 and <= 4;
    }

    private void Disable()
    {
        if (Interlocked.Exchange(ref _disabled, 1) == 0)
        {
            _loopCts?.Cancel();
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client telemetry subscription failed")]
    private partial void LogSubscriptionFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client telemetry subscription rejected with {ErrorCode}")]
    private partial void LogSubscriptionRejected(ErrorCode errorCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client telemetry push failed (terminating={Terminating})")]
    private partial void LogPushFailed(Exception exception, bool terminating);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client telemetry push rejected with {ErrorCode}")]
    private partial void LogPushRejected(ErrorCode errorCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client telemetry terminating push failed")]
    private partial void LogTerminatingPushFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client telemetry loop failed")]
    private partial void LogTelemetryLoopFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client telemetry payload too large ({PayloadSize} > {MaxSize}); sending empty payload")]
    private partial void LogTelemetryPayloadTooLarge(int payloadSize, int maxSize);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client telemetry payload rejected as too large ({PayloadSize}); retrying with empty payload")]
    private partial void LogTelemetryPayloadRejectedTooLarge(int payloadSize);
}
