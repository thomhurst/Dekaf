using System.Runtime.CompilerServices;
using Dekaf.Compression;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Retry;
using Dekaf.Serialization;
using Microsoft.Extensions.Logging;
#if NETSTANDARD2_0
using StringSet = System.Collections.Generic.IReadOnlyCollection<string>;
using TopicPartitionSet = System.Collections.Generic.IReadOnlyCollection<Dekaf.TopicPartition>;
#else
using StringSet = System.Collections.Generic.IReadOnlySet<string>;
using TopicPartitionSet = System.Collections.Generic.IReadOnlySet<Dekaf.TopicPartition>;
#endif

namespace Dekaf.ShareConsumer;

/// <summary>
/// KIP-932 share consumer implementation. Provides queue-semantics consumption with
/// record-level acknowledgement. Records are acquired with locks and must be acknowledged
/// (accepted, released, or rejected).
/// </summary>
internal sealed partial class KafkaShareConsumer<TKey, TValue> : IKafkaShareConsumer<TKey, TValue>
{
    private readonly ShareConsumerOptions _options;
    private readonly IDeserializer<TKey> _keyDeserializer;
    private readonly IDeserializer<TValue> _valueDeserializer;
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
    private readonly ShareConsumerCoordinator _coordinator;
    private readonly ShareSessionManager _sessionManager = new();
    private readonly AcknowledgementTracker _ackTracker = new();
    private readonly CompressionCodecRegistry _compressionCodecs;
    private readonly ILogger _logger;

    // ThreadStatic reusable SerializationContext to avoid per-record allocations in ParsePartitionRecords.
    // Matches the pattern used by ConsumeResult<TKey, TValue> in the regular consumer.
    [ThreadStatic]
    private static SerializationContext t_serializationContext;

    private volatile StringSet _subscriptionSnapshot = new HashSet<string>();
    private volatile TopicPartitionSet _assignmentSnapshot = new HashSet<TopicPartition>();

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized;
    private int _closed;
    private int _disposed;
    private readonly bool _ownsInfrastructure;
    private volatile Task? _pendingReleaseTask;
    private Dictionary<RenewedRecordKey, RenewedRecordState>? _renewedRecords;
    private int _acquisitionLockTimeoutMs = -1;
    private long _renewalRequestCount;
    private long _renewedRecordReplayCount;

    internal KafkaShareConsumer(
        ShareConsumerOptions options,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        IConnectionPool connectionPool,
        MetadataManager metadataManager,
        ILoggerFactory? loggerFactory = null)
        : this(options, keyDeserializer, valueDeserializer,
            (connectionPool, metadataManager),
            loggerFactory,
            ownsInfrastructure: false)
    {
    }

    private static (IConnectionPool, MetadataManager) CreateInfrastructure(
        ShareConsumerOptions options, ILoggerFactory? loggerFactory)
    {
        var connectionPool = new ConnectionPool(
            options.ClientId,
            new ConnectionOptions
            {
                UseTls = options.UseTls,
                TlsConfig = options.TlsConfig,
                RemoteCertificateValidationCallback = options.RemoteCertificateValidationCallback,
                ConnectionTimeout = options.ConnectionTimeout,
                EnableTcpKeepAlive = options.EnableTcpKeepAlive,
                TcpKeepAliveTime = options.TcpKeepAliveTime,
                TcpKeepAliveInterval = options.TcpKeepAliveInterval,
                TcpKeepAliveRetryCount = options.TcpKeepAliveRetryCount,
                RequestTimeout = TimeSpan.FromMilliseconds(options.RequestTimeoutMs),
                ReconnectBackoff = TimeSpan.FromMilliseconds(options.ReconnectBackoffMs),
                ReconnectBackoffMax = TimeSpan.FromMilliseconds(options.ReconnectBackoffMaxMs),
                ConnectionsMaxIdleMs = options.ConnectionsMaxIdleMs,
                SaslMechanism = options.SaslMechanism,
                SaslUsername = options.SaslUsername,
                SaslPassword = options.SaslPassword,
                SaslCredentialProvider = options.SaslCredentialProvider,
                SaslScramTokenAuth = options.SaslScramTokenAuth,
                GssapiConfig = options.GssapiConfig,
                OAuthBearerConfig = options.OAuthBearerConfig,
                OAuthBearerTokenProvider = options.OAuthBearerTokenProvider,
                AwsMskIamConfig = options.AwsMskIamConfig,
                SendBufferSize = options.SocketSendBufferBytes,
                ReceiveBufferSize = options.SocketReceiveBufferBytes,
                ClientDnsLookup = options.ClientDnsLookup
            },
            loggerFactory,
            connectionsPerBroker: options.ConnectionsPerBroker);

        var metadataManager = new MetadataManager(
            connectionPool,
            options.BootstrapServers,
            new MetadataOptions
            {
                MetadataClusterCheckEnabled = options.MetadataClusterCheckEnabled,
                RetryBackoffMs = options.RetryBackoffMs,
                RetryBackoffMaxMs = options.RetryBackoffMaxMs,
                BootstrapResolveTimeoutMs = options.BootstrapResolveTimeoutMs
            },
            logger: loggerFactory?.CreateLogger<MetadataManager>());

        return (connectionPool, metadataManager);
    }

    internal KafkaShareConsumer(
        ShareConsumerOptions options,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        ILoggerFactory? loggerFactory = null)
        : this(options, keyDeserializer, valueDeserializer,
            CreateInfrastructure(options, loggerFactory),
            loggerFactory,
            ownsInfrastructure: true)
    {
    }

    private KafkaShareConsumer(
        ShareConsumerOptions options,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        (IConnectionPool Pool, MetadataManager Metadata) infrastructure,
        ILoggerFactory? loggerFactory,
        bool ownsInfrastructure)
    {
        ExponentialRetryBackoff.Validate(options.RetryBackoffMs, options.RetryBackoffMaxMs);
        _options = options;
        _keyDeserializer = keyDeserializer;
        _valueDeserializer = valueDeserializer;
        _connectionPool = infrastructure.Pool;
        _metadataManager = infrastructure.Metadata;
        _ownsInfrastructure = ownsInfrastructure;
        _logger = loggerFactory?.CreateLogger<KafkaShareConsumer<TKey, TValue>>()
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<KafkaShareConsumer<TKey, TValue>>.Instance;

        _compressionCodecs = CompressionCodecRegistry.Default;

        _coordinator = new ShareConsumerCoordinator(
            options,
            _connectionPool,
            _metadataManager,
            loggerFactory?.CreateLogger<ShareConsumerCoordinator>());
    }

    public StringSet Subscription => _subscriptionSnapshot;
    public TopicPartitionSet Assignment => _assignmentSnapshot;
    public string? MemberId => _coordinator.MemberId;
    public int? AcquisitionLockTimeoutMs
    {
        get
        {
            var timeoutMs = Volatile.Read(ref _acquisitionLockTimeoutMs);
            return timeoutMs >= 0 ? timeoutMs : null;
        }
    }

    internal long RenewalRequestCount => Interlocked.Read(ref _renewalRequestCount);
    internal long RenewedRecordReplayCount => Interlocked.Read(ref _renewedRecordReplayCount);

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_initialized)
            return;

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
                return;

            await _metadataManager.InitializeAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public IKafkaShareConsumer<TKey, TValue> Subscribe(params string[] topics)
    {
        _subscriptionSnapshot = new HashSet<string>(topics);
        return this;
    }

    public IKafkaShareConsumer<TKey, TValue> Unsubscribe()
    {
        // Release any pending acks back to the group so other members can claim them,
        // rather than waiting for the broker's acquisition lock timeout to expire.
        if (_ackTracker.HasPending)
        {
            ReleasePendingAcks();
        }

        _subscriptionSnapshot = new HashSet<string>();
        _sessionManager.ResetAll();
        ClearRenewedRecords();
        return this;
    }

    public async IAsyncEnumerable<ShareConsumeResult<TKey, TValue>> PollAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_subscriptionSnapshot.Count == 0)
                yield break;

            // Ensure we're part of the share group
            await _coordinator.EnsureActiveGroupAsync(_subscriptionSnapshot, cancellationToken)
                .ConfigureAwait(false);
            _assignmentSnapshot = _coordinator.Assignment;

            var assignment = _assignmentSnapshot;
            RemoveRenewedRecordsOutsideAssignment(assignment);
            if (assignment.Count == 0)
            {
                // No partitions assigned (e.g. rebalance removed them while state is Stable).
                // Delay to avoid a spin-loop — reuse FetchMaxWaitMs as the broker's natural
                // back-pressure is absent when no fetch request is issued.
                await Task.Delay(_options.FetchMaxWaitMs, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // Flush pending acks from previous poll as inline acknowledgements with the fetch
            var pendingAcks = _ackTracker.HasPending ? _ackTracker.Flush() : null;

            // Group assigned partitions by leader broker
            var partitionsByBroker = GroupPartitionsByLeader(assignment);

            // Send fetch requests to all brokers concurrently. Session epochs are per-broker
            // and independent, so parallelism is safe. This avoids waiting for each broker's
            // MaxWaitMs sequentially when partitions span multiple brokers.
            var fetchTasks = new List<Task<ShareFetchBrokerResult>>(
                partitionsByBroker.Count);

            foreach (var (brokerId, partitions) in partitionsByBroker)
            {
                fetchTasks.Add(SendShareFetchForPartitionsAsync(brokerId, partitions, pendingAcks, cancellationToken));
            }

            try
            {
                await Task.WhenAll(fetchTasks).ConfigureAwait(false);
            }
            catch
            {
                if (pendingAcks is not null)
                    _ackTracker.RequeueAcks(pendingAcks);
                throw;
            }

            // Process responses sequentially — yielding records and tracking acks
            var recordCount = 0;
            List<ShareConsumeResult<TKey, TValue>>? fetchedRecords = _renewedRecords is null
                ? null
                : new List<ShareConsumeResult<TKey, TValue>>(_options.MaxPollRecords);

            foreach (var fetchTask in fetchTasks)
            {
                if ((fetchedRecords?.Count ?? recordCount) >= _options.MaxPollRecords)
                    break;

                var (brokerId, version, response, sentAcks) = fetchTask.Result;

                if (response is null)
                {
                    RequeueAcknowledgements(sentAcks);
                    continue;
                }

                // Handle top-level errors
                if (response.ErrorCode != ErrorCode.None)
                {
                    LogFetchTopLevelError(response.ErrorCode, response.ErrorMessage);
                    if (response.ErrorCode == ErrorCode.ShareSessionNotFound ||
                        response.ErrorCode == ErrorCode.InvalidShareSessionEpoch)
                    {
                        _sessionManager.ResetSession(brokerId);
                        ClearRenewedRecords();
                        // Note: _ackTracker may still hold pending acks from the now-invalid session.
                        // On next CommitAsync those acks will be sent with epoch 0 (new session).
                        // The broker will reject them if the old record locks have expired, which is
                        // safe — CommitAsync will re-queue the failed acks for retry.
                    }
                    RequeueAcknowledgements(sentAcks);
                    continue;
                }

                // Advance the session epoch BEFORE yielding so that even if
                // the caller breaks from the async enumerable (disposing the iterator),
                // the epoch is already correct for a subsequent ShareAcknowledge/CommitAsync.
                _sessionManager.IncrementEpoch(brokerId);
                if (version >= 1)
                    Volatile.Write(ref _acquisitionLockTimeoutMs, response.AcquisitionLockTimeoutMs);

                if (TryGetAcknowledgementError(response, out var acknowledgementError))
                {
                    RequeueAcknowledgements(sentAcks);
                    LogInlineAcknowledgeFailed(brokerId, acknowledgementError);
                }
                else
                {
                    ApplySuccessfulAcknowledgements(sentAcks);
                }

                // Process partition responses
                foreach (var topicResponse in response.Responses)
                {
                    var topicInfo = _metadataManager.Metadata.GetTopic(topicResponse.TopicId);
                    if (topicInfo is null)
                        continue;

                    foreach (var partition in topicResponse.Partitions)
                    {
                        if (partition.ErrorCode != ErrorCode.None)
                        {
                            LogPartitionFetchError(topicInfo.Name, partition.PartitionIndex,
                                partition.ErrorCode);
                            continue;
                        }

                        if (partition.RecordBytes.IsEmpty || partition.AcquiredRecords.Count == 0)
                            continue;

                        // Parse all records from this partition eagerly (KafkaProtocolReader is a
                        // ref struct and cannot be preserved across yield boundaries)
                        var parsed = ParsePartitionRecords(
                            topicInfo,
                            partition,
                            _options.MaxPollRecords - (fetchedRecords?.Count ?? recordCount));

                        var tp = new TopicPartition(topicInfo.Name, partition.PartitionIndex);

                        foreach (var result in parsed)
                        {
                            if ((fetchedRecords?.Count ?? recordCount) >= _options.MaxPollRecords)
                                break;

                            // Track only records actually yielded to the consumer so implicit
                            // acknowledgements do not include offsets truncated by MaxPollRecords.
                            if (_options.AcknowledgementMode == ShareAcknowledgementMode.Implicit)
                            {
                                _ackTracker.TrackDeliveredRecords(tp, result.Offset, result.Offset);
                            }

                            RemoveRenewedRecord(result.Topic, result.Partition, result.Offset);

                            if (fetchedRecords is not null)
                            {
                                fetchedRecords.Add(result);
                                continue;
                            }

                            recordCount++;
                            yield return result;
                        }
                    }
                }
            }

            if (recordCount < _options.MaxPollRecords && _renewedRecords is { Count: > 0 })
            {
                var renewedRecords = GetActiveRenewedRecords(
                    assignment,
                    _options.MaxPollRecords - recordCount);
                foreach (var renewedRecord in renewedRecords)
                {
                    Interlocked.Increment(ref _renewedRecordReplayCount);
                    recordCount++;
                    yield return renewedRecord;
                }
            }

            if (fetchedRecords is not null)
            {
                foreach (var fetchedRecord in fetchedRecords)
                {
                    if (recordCount >= _options.MaxPollRecords)
                        break;

                    recordCount++;
                    yield return fetchedRecord;
                }
            }

            // If this poll round returned nothing, the broker's MaxWaitMs already
            // provided back-pressure. No additional client-side delay needed.
        }
    }

    public void Acknowledge(ShareConsumeResult<TKey, TValue> record, AcknowledgeType type = AcknowledgeType.Accept)
    {
        ThrowIfDisposed();

        if (type == AcknowledgeType.Renew
            && _options.AcknowledgementMode != ShareAcknowledgementMode.Explicit)
        {
            throw new InvalidOperationException(
                "Renew acknowledgements require explicit acknowledgement mode.");
        }

        record.AcknowledgeType = type;
        var tp = new TopicPartition(record.Topic, record.Partition);
        _ackTracker.Acknowledge(
            tp,
            record.Offset,
            type,
            requireTracked: _options.AcknowledgementMode == ShareAcknowledgementMode.Implicit);

        TrackRenewalDisposition(record, type);
    }

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        if (!_ackTracker.HasPending)
            return;

        var pendingAcks = _ackTracker.Flush();
        if (pendingAcks.Count == 0)
            return;

        // Group by broker
        var acksByBroker = GroupAcksByLeader(pendingAcks);

        // Send to all brokers in parallel, collecting per-broker results
        var results = await Task.WhenAll(acksByBroker.Select(async kvp =>
        {
            try
            {
                await SendAcknowledgeAsync(
                        kvp.Key,
                        kvp.Value,
                        retryRetriableFailures: true,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return (Acks: kvp.Value, Error: (Exception?)null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return (Acks: kvp.Value, Error: ex);
            }
        })).ConfigureAwait(false);

        // Re-queue failed partitions so they can be retried on the next commit
        Exception? firstError = null;
        foreach (var (acks, error) in results)
        {
            if (error is null)
            {
                ApplySuccessfulAcknowledgements(acks);
                continue;
            }

            firstError ??= error;
            _ackTracker.RequeueAcks(acks);
        }

        if (firstError is not null)
        {
            if (firstError is BrokerVersionException)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstError).Throw();

            throw new KafkaException(
                $"CommitAsync partially failed — failed partitions have been re-queued for retry",
                firstError);
        }
    }

    public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
            return;

        LogClosingShareConsumer();
        ClearRenewedRecords();

        // Step 1: Flush all pending acks as Accept via ShareAcknowledge
        if (_ackTracker.HasPending)
        {
            try
            {
                await CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogFlushAcksFailed(ex);
            }
        }

        // Step 2: Close share sessions (send ShareFetch with epoch = -1)
        // This is a best-effort operation
        try
        {
            await CloseShareSessionsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogCloseSessionsFailed(ex);
        }

        // Step 3: Leave the share group
        await _coordinator.LeaveGroupAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        ClearRenewedRecords();

        // Ensure close is called
        if (Volatile.Read(ref _closed) == 0)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await CloseAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                LogDisposeCloseTimedOut();
            }
            catch
            {
                // Best-effort close during dispose
            }
        }

        // Await any pending release task from Unsubscribe before tearing down infrastructure
        if (_pendingReleaseTask is { } releaseTask)
        {
            try
            {
                await releaseTask.ConfigureAwait(false);
            }
            catch
            {
                // Best-effort — exceptions already logged inside the task
            }
        }

        await _coordinator.DisposeAsync().ConfigureAwait(false);

        if (_ownsInfrastructure)
        {
            await _connectionPool.DisposeAsync().ConfigureAwait(false);
        }

        _initLock.Dispose();
    }

    /// <summary>
    /// Overrides all pending acks to Release and sends them via ShareAcknowledge.
    /// Best-effort — errors are logged but not thrown.
    /// </summary>
    private void ReleasePendingAcks()
    {
        var pending = _ackTracker.Flush();
        if (pending.Count == 0)
            return;

        // Override all ack types to Release so the broker redelivers to other members
        var released = new Dictionary<TopicPartition, List<AcknowledgementBatchData>>();
        foreach (var (tp, batches) in pending)
        {
            var releasedBatches = new List<AcknowledgementBatchData>(batches.Count);
            foreach (var batch in batches)
            {
                var types = new byte[batch.AcknowledgeTypes.Length];
                Array.Fill(types, (byte)AcknowledgeType.Release);
                releasedBatches.Add(new AcknowledgementBatchData(batch.FirstOffset, batch.LastOffset, types));
            }
            released[tp] = releasedBatches;
        }

        // Best-effort send — fire and forget since Unsubscribe is synchronous
        var acksByBroker = GroupAcksByLeader(released);
        if (acksByBroker.Count == 0)
            return;

        // Send in the background — best effort, don't block the synchronous caller.
        // Store the task so DisposeAsync can await it before tearing down the connection pool.
        // Chain after any prior release task so DisposeAsync only needs to await the latest reference.
        var prior = _pendingReleaseTask;
        _pendingReleaseTask = Task.Run(async () =>
        {
            if (prior is not null)
            {
                try { await prior.ConfigureAwait(false); }
                catch { /* already logged inside the prior task */ }
            }

            foreach (var (brokerId, acks) in acksByBroker)
            {
                try
                {
                    await SendAcknowledgeAsync(
                            brokerId,
                            acks,
                            retryRetriableFailures: false,
                            cancellationToken: CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogAcknowledgeRequestFailed(brokerId, ex);
                }
            }
        });
    }

    private async Task<ShareFetchBrokerResult> SendShareFetchForPartitionsAsync(
        int brokerId,
        List<TopicPartition> partitions,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? pendingAcks,
        CancellationToken cancellationToken)
    {
        var brokerAcks = SelectAcknowledgements(pendingAcks, partitions);
        var isRenewAck = ContainsRenewAcknowledgement(brokerAcks);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var connectionLease = await _connectionPool.LeaseConnectionAsync(brokerId, cancellationToken)
                    .ConfigureAwait(false);
                var connection = connectionLease.Connection;
                var version = _metadataManager.GetNegotiatedApiVersion(
                    connection,
                    ApiKey.ShareFetch,
                    ShareFetchRequest.LowestSupportedVersion,
                    ShareFetchRequest.HighestSupportedVersion);
                EnsureRenewalSupported(ApiKey.ShareFetch, version, isRenewAck);
                var maxRecords = !isRenewAck && version >= 1 ? _options.MaxPollRecords : 0;
                var request = new ShareFetchRequest
                {
                    GroupId = _options.GroupId,
                    MemberId = _coordinator.MemberId!,
                    ShareSessionEpoch = _sessionManager.GetSessionEpoch(brokerId),
                    MaxWaitMs = isRenewAck ? 0 : _options.FetchMaxWaitMs,
                    MinBytes = isRenewAck ? 0 : _options.FetchMinBytes,
                    MaxBytes = isRenewAck ? 0 : _options.FetchMaxBytes,
                    MaxRecords = maxRecords,
                    BatchSize = isRenewAck ? 0 : maxRecords,
                    ShareAcquireMode = (sbyte)_options.ShareAcquireMode,
                    IsRenewAck = isRenewAck,
                    Topics = BuildShareFetchTopics(partitions, brokerAcks, version)
                };
                var response = (ShareFetchResponse)await connection
                    .SendAsync<ShareFetchRequest, ShareFetchResponse>(request, version, cancellationToken)
                    .ConfigureAwait(false);

                if (attempt < RetryHelper.MaxRetries && response.ErrorCode.IsRetriable())
                {
                    await PrepareRequestRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return new ShareFetchBrokerResult(brokerId, version, response, brokerAcks);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not BrokerVersionException)
            {
                if (attempt < RetryHelper.MaxRetries && RetryHelper.IsRetriableRequestFailure(ex))
                {
                    await PrepareRequestRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                LogFetchFailed(brokerId, ex);
                _sessionManager.ResetSession(brokerId);
                ClearRenewedRecords();
                return new ShareFetchBrokerResult(brokerId, 0, null, brokerAcks);
            }
        }
    }

    private async ValueTask CloseShareSessionsAsync(CancellationToken cancellationToken)
    {
        var assignment = _assignmentSnapshot;
        if (assignment.Count == 0)
            return;

        var partitionsByBroker = GroupPartitionsByLeader(assignment);

        // Send close requests to all brokers in parallel — these are fire-and-forget
        // with MaxWaitMs=0, so there's no reason to wait for each sequentially.
        var closeTasks = new List<Task>(partitionsByBroker.Count);

        foreach (var (brokerId, partitions) in partitionsByBroker)
        {
            closeTasks.Add(CloseSessionForBrokerAsync(brokerId, partitions, cancellationToken));
        }

        await Task.WhenAll(closeTasks).ConfigureAwait(false);
    }

    private async Task CloseSessionForBrokerAsync(
        int brokerId,
        List<TopicPartition> partitions,
        CancellationToken cancellationToken)
    {
        try
        {
            using var connectionLease = await _connectionPool.LeaseConnectionAsync(brokerId, cancellationToken)
                .ConfigureAwait(false);
            var connection = connectionLease.Connection;
            var version = _metadataManager.GetNegotiatedApiVersion(
                connection,
                ApiKey.ShareFetch,
                ShareFetchRequest.LowestSupportedVersion,
                ShareFetchRequest.HighestSupportedVersion);
            var request = new ShareFetchRequest
            {
                GroupId = _options.GroupId,
                MemberId = _coordinator.MemberId!,
                ShareSessionEpoch = ShareSessionManager.CloseEpoch,
                MaxWaitMs = 0,
                MinBytes = 0,
                MaxBytes = 0,
                MaxRecords = version >= 1 ? 1 : 0,
                ShareAcquireMode = (sbyte)_options.ShareAcquireMode,
                Topics = BuildShareFetchTopics(partitions, pendingAcks: null, version)
            };
            await connection.SendAsync<ShareFetchRequest, ShareFetchResponse>(
                request, version, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort session close
        }
    }

    private async Task SendAcknowledgeAsync(
        int brokerId,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> topicAcks,
        bool retryRetriableFailures,
        CancellationToken cancellationToken)
    {
        var topics = BuildShareAcknowledgeTopics(topicAcks);
        var isRenewAck = ContainsRenewAcknowledgement(topicAcks);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var connectionLease = await _connectionPool.LeaseConnectionAsync(brokerId, cancellationToken)
                    .ConfigureAwait(false);
                var connection = connectionLease.Connection;
                var shareAckVersion = _metadataManager.GetNegotiatedApiVersion(
                    connection,
                    ApiKey.ShareAcknowledge,
                    ShareAcknowledgeRequest.LowestSupportedVersion,
                    ShareAcknowledgeRequest.HighestSupportedVersion);
                EnsureRenewalSupported(ApiKey.ShareAcknowledge, shareAckVersion, isRenewAck);

                var request = new ShareAcknowledgeRequest
                {
                    GroupId = _options.GroupId,
                    MemberId = _coordinator.MemberId!,
                    ShareSessionEpoch = _sessionManager.GetSessionEpoch(brokerId),
                    IsRenewAck = isRenewAck,
                    Topics = topics
                };
                var response = (ShareAcknowledgeResponse)await connection
                    .SendAsync<ShareAcknowledgeRequest, ShareAcknowledgeResponse>(
                        request, shareAckVersion, cancellationToken)
                    .ConfigureAwait(false);

                if (response.ErrorCode != ErrorCode.None)
                {
                    if (response.ErrorCode is ErrorCode.ShareSessionNotFound
                        or ErrorCode.InvalidShareSessionEpoch)
                    {
                        _sessionManager.ResetSession(brokerId);
                        ClearRenewedRecords();
                    }
                    else
                    {
                        _sessionManager.IncrementEpoch(brokerId);
                    }

                    throw KafkaException.FromErrorCode(response.ErrorCode,
                        $"ShareAcknowledge failed for broker {brokerId}: {response.ErrorCode} - {response.ErrorMessage}");
                }

                _sessionManager.IncrementEpoch(brokerId);

                if (isRenewAck)
                    Volatile.Write(ref _acquisitionLockTimeoutMs, response.AcquisitionLockTimeoutMs);

                if (TryGetAcknowledgementError(response, out var acknowledgementError))
                    throw acknowledgementError;

                return;
            }
            catch (Exception ex) when (retryRetriableFailures
                                       && attempt < RetryHelper.MaxRetries
                                       && RetryHelper.IsRetriableRequestFailure(ex))
            {
                await PrepareRequestRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask PrepareRequestRetryAsync(int zeroBasedAttempt, CancellationToken cancellationToken)
    {
        try
        {
            await _metadataManager.RefreshMetadataAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (
            ex is not ObjectDisposedException
            && !cancellationToken.IsCancellationRequested)
        {
            // Best-effort: preserve the original request failure if retries exhaust.
        }
        catch (Exception ex) when (
            RetryHelper.IsRetriableRequestFailure(ex)
            && !cancellationToken.IsCancellationRequested)
        {
            // Same best-effort behavior for transient transport failures.
        }

        var delayMs = ExponentialRetryBackoff.CalculateDelayMilliseconds(
            _options.RetryBackoffMs,
            _options.RetryBackoffMaxMs,
            zeroBasedAttempt + 1);
        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Groups assigned partitions by their leader broker.
    /// </summary>
    private Dictionary<int, List<TopicPartition>> GroupPartitionsByLeader(
        TopicPartitionSet assignment)
    {
        var result = new Dictionary<int, List<TopicPartition>>();

        foreach (var tp in assignment)
        {
            var topicInfo = _metadataManager.Metadata.GetTopic(tp.Topic);
            if (topicInfo is null)
                continue;

            var leaderNode = _metadataManager.Metadata.GetPartitionLeader(tp.Topic, tp.Partition);
            if (leaderNode is null)
                continue;

            if (!result.TryGetValue(leaderNode.NodeId, out var list))
            {
                list = [];
                result[leaderNode.NodeId] = list;
            }
            list.Add(tp);
        }

        return result;
    }

    /// <summary>
    /// Parses records from a partition's raw record bytes eagerly. This is needed because
    /// KafkaProtocolReader is a ref struct and cannot cross yield boundaries.
    /// </summary>
    private List<ShareConsumeResult<TKey, TValue>> ParsePartitionRecords(
        TopicInfo topicInfo,
        ShareFetchResponsePartition partition,
        int maxRecords)
    {
        var results = new List<ShareConsumeResult<TKey, TValue>>();

        var reader = new KafkaProtocolReader(partition.RecordBytes);
        var acquiredRecordIndex = 0;
        while (!reader.End && results.Count < maxRecords)
        {
            RecordBatch batch;
            try
            {
                batch = RecordBatch.Read(ref reader, _compressionCodecs);
            }
            catch
            {
                break; // Partial batch
            }

            try
            {
                foreach (var record in batch.Records)
                {
                    if (results.Count >= maxRecords)
                        break;

                    var offset = batch.BaseOffset + record.OffsetDelta;

                    var deliveryCount = FindDeliveryCount(
                        partition.AcquiredRecords,
                        offset,
                        ref acquiredRecordIndex);
                    if (deliveryCount < 0)
                        continue;

                    t_serializationContext.Topic = topicInfo.Name;
                    t_serializationContext.Component = SerializationComponent.Key;
                    var key = record.IsKeyNull
                        ? default
                        : _keyDeserializer.Deserialize(record.Key, t_serializationContext);

                    t_serializationContext.Component = SerializationComponent.Value;
                    var value = record.IsValueNull
                        ? default!
                        : _valueDeserializer.Deserialize(record.Value, t_serializationContext);

                    var headers = Array.Empty<Header>();
                    if (record.Headers is not null && record.HeaderCount > 0)
                    {
                        headers = new Header[record.HeaderCount];
                        Array.Copy(record.Headers, headers, record.HeaderCount);
                    }

                    results.Add(new ShareConsumeResult<TKey, TValue>
                    {
                        Topic = topicInfo.Name,
                        Partition = partition.PartitionIndex,
                        Offset = offset,
                        Key = key,
                        Value = value,
                        Headers = headers,
                        TimestampMs = batch.BaseTimestamp + record.TimestampDelta,
                        DeliveryCount = deliveryCount
                    });
                }
            }
            finally
            {
                batch.DisposeAndReturnUnownedConsumerBatch();
            }
        }

        return results;
    }

    /// <summary>
    /// Builds ShareFetch request topics with optional inline acknowledgement batches.
    /// </summary>
    private List<ShareFetchRequestTopic> BuildShareFetchTopics(
        List<TopicPartition> partitions,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? pendingAcks,
        short version)
    {
        var topicMap = new Dictionary<string, (Guid TopicId, List<ShareFetchRequestPartition> Partitions)>();

        foreach (var tp in partitions)
        {
            var topicInfo = _metadataManager.Metadata.GetTopic(tp.Topic);
            if (topicInfo is null)
                continue;

            if (!topicMap.TryGetValue(tp.Topic, out var entry))
            {
                entry = (topicInfo.TopicId, []);
                topicMap[tp.Topic] = entry;
            }

            List<ShareFetchAcknowledgementBatch>? ackBatches = null;
            if (pendingAcks is not null && pendingAcks.TryGetValue(tp, out var batchDataList))
            {
                ackBatches = new List<ShareFetchAcknowledgementBatch>(batchDataList.Count);
                foreach (var bd in batchDataList)
                {
                    ackBatches.Add(new ShareFetchAcknowledgementBatch
                    {
                        FirstOffset = bd.FirstOffset,
                        LastOffset = bd.LastOffset,
                        AcknowledgeTypes = bd.AcknowledgeTypes
                    });
                }
            }

            var fetchPartition = new ShareFetchRequestPartition
            {
                PartitionIndex = tp.Partition,
                PartitionMaxBytes = version == 0 ? _options.MaxPartitionFetchBytes : 0,
                AcknowledgementBatches = ackBatches
            };

            entry.Partitions.Add(fetchPartition);
        }

        var topics = new List<ShareFetchRequestTopic>(topicMap.Count);
        foreach (var (_, (topicId, fetchPartitions)) in topicMap)
        {
            topics.Add(new ShareFetchRequestTopic
            {
                TopicId = topicId,
                Partitions = fetchPartitions
            });
        }

        return topics;
    }

    private static Dictionary<TopicPartition, List<AcknowledgementBatchData>>? SelectAcknowledgements(
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? pendingAcks,
        List<TopicPartition> partitions)
    {
        if (pendingAcks is null)
            return null;

        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? selected = null;
        foreach (var partition in partitions)
        {
            if (!pendingAcks.TryGetValue(partition, out var batches))
                continue;

            selected ??= [];
            selected[partition] = batches;
        }

        return selected;
    }

    private static bool ContainsRenewAcknowledgement(
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? acknowledgements)
    {
        if (acknowledgements is null)
            return false;

        foreach (var batches in acknowledgements.Values)
        {
            foreach (var batch in batches)
            {
                for (var i = 0; i < batch.AcknowledgeTypes.Length; i++)
                {
                    if (batch.AcknowledgeTypes[i] == (byte)AcknowledgeType.Renew)
                        return true;
                }
            }
        }

        return false;
    }

    private static void EnsureRenewalSupported(ApiKey apiKey, short version, bool isRenewAck)
    {
        if (!isRenewAck || version >= 2)
            return;

        throw new BrokerVersionException(
            ErrorCode.UnsupportedVersion,
            $"Broker does not support {apiKey} v2 required for Renew acknowledgements.");
    }

    private void RequeueAcknowledgements(
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? acknowledgements)
    {
        if (acknowledgements is not null && acknowledgements.Count > 0)
            _ackTracker.RequeueAcks(acknowledgements);
    }

    private static bool TryGetAcknowledgementError(
        ShareFetchResponse response,
        out KafkaException error)
    {
        foreach (var topic in response.Responses)
        {
            foreach (var partition in topic.Partitions)
            {
                if (partition.AcknowledgeErrorCode == ErrorCode.None)
                    continue;

                error = KafkaException.FromErrorCode(
                    partition.AcknowledgeErrorCode,
                    $"Inline ShareFetch acknowledgement failed for partition " +
                    $"{partition.PartitionIndex}: {partition.AcknowledgeErrorMessage}");
                return true;
            }
        }

        error = null!;
        return false;
    }

    private static bool TryGetAcknowledgementError(
        ShareAcknowledgeResponse response,
        out KafkaException error)
    {
        foreach (var topic in response.Responses)
        {
            foreach (var partition in topic.Partitions)
            {
                if (partition.ErrorCode == ErrorCode.None)
                    continue;

                error = KafkaException.FromErrorCode(
                    partition.ErrorCode,
                    $"ShareAcknowledge failed for partition {partition.PartitionIndex}: " +
                    partition.ErrorMessage);
                return true;
            }
        }

        error = null!;
        return false;
    }

    private void TrackRenewalDisposition(
        ShareConsumeResult<TKey, TValue> record,
        AcknowledgeType type)
    {
        if (type != AcknowledgeType.Renew)
            return;

        var key = new RenewedRecordKey(record.Topic, record.Partition, record.Offset);
        _renewedRecords ??= [];
        if (_renewedRecords.TryGetValue(key, out var state))
            state.Record = record;
        else
            _renewedRecords[key] = new RenewedRecordState(record);

        Interlocked.Increment(ref _renewalRequestCount);
        LogRenewalRequested(record.Topic, record.Partition, record.Offset);
    }

    private void ApplySuccessfulAcknowledgements(
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? acknowledgements)
    {
        if (_renewedRecords is null || acknowledgements is null)
            return;

        foreach (var (topicPartition, batches) in acknowledgements)
        {
            foreach (var batch in batches)
            {
                for (var i = 0; i < batch.AcknowledgeTypes.Length; i++)
                {
                    var key = new RenewedRecordKey(
                        topicPartition.Topic,
                        topicPartition.Partition,
                        batch.FirstOffset + i);
                    var type = (AcknowledgeType)batch.AcknowledgeTypes[i];
                    if (type == AcknowledgeType.Renew)
                    {
                        if (_renewedRecords.TryGetValue(key, out var state))
                            state.Active = true;
                    }
                    else
                    {
                        _renewedRecords.Remove(key);
                    }
                }
            }
        }

        if (_renewedRecords.Count == 0)
            _renewedRecords = null;
    }

    private List<ShareConsumeResult<TKey, TValue>> GetActiveRenewedRecords(
        TopicPartitionSet assignment,
        int maxRecords)
    {
        if (_renewedRecords is null || maxRecords <= 0)
            return [];

        var records = new List<ShareConsumeResult<TKey, TValue>>(
            Math.Min(_renewedRecords.Count, maxRecords));
        foreach (var (key, state) in _renewedRecords)
        {
            if (!state.Active
                || !assignment.Contains(new TopicPartition(key.Topic, key.Partition)))
            {
                continue;
            }

            records.Add(state.Record);
            if (records.Count == maxRecords)
                break;
        }

        return records;
    }

    private void RemoveRenewedRecord(string topic, int partition, long offset)
    {
        if (_renewedRecords is null)
            return;

        _renewedRecords.Remove(new RenewedRecordKey(topic, partition, offset));
        if (_renewedRecords.Count == 0)
            _renewedRecords = null;
    }

    private void RemoveRenewedRecordsOutsideAssignment(TopicPartitionSet assignment)
    {
        if (_renewedRecords is null)
            return;

        List<RenewedRecordKey>? removed = null;
        foreach (var key in _renewedRecords.Keys)
        {
            if (assignment.Contains(new TopicPartition(key.Topic, key.Partition)))
                continue;

            removed ??= [];
            removed.Add(key);
        }

        if (removed is null)
            return;

        foreach (var key in removed)
            _renewedRecords.Remove(key);

        if (_renewedRecords.Count == 0)
            _renewedRecords = null;
    }

    private void ClearRenewedRecords() => _renewedRecords = null;

    /// <summary>
    /// Groups acknowledgement data by leader broker.
    /// </summary>
    private Dictionary<int, Dictionary<TopicPartition, List<AcknowledgementBatchData>>> GroupAcksByLeader(
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> acks)
    {
        var result = new Dictionary<int, Dictionary<TopicPartition, List<AcknowledgementBatchData>>>();

        foreach (var (tp, batches) in acks)
        {
            var topicInfo = _metadataManager.Metadata.GetTopic(tp.Topic);
            if (topicInfo is null)
                continue;

            var leaderNode = _metadataManager.Metadata.GetPartitionLeader(tp.Topic, tp.Partition);
            if (leaderNode is null)
                continue;

            if (!result.TryGetValue(leaderNode.NodeId, out var ackMap))
            {
                ackMap = [];
                result[leaderNode.NodeId] = ackMap;
            }
            ackMap[tp] = batches;
        }

        return result;
    }

    /// <summary>
    /// Builds ShareAcknowledge request topics from grouped acknowledgement data.
    /// </summary>
    private List<ShareAcknowledgeTopic> BuildShareAcknowledgeTopics(
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> acks)
    {
        var topicMap = new Dictionary<string, (Guid TopicId, List<ShareAcknowledgePartition> Partitions)>();

        foreach (var (tp, batches) in acks)
        {
            var topicInfo = _metadataManager.Metadata.GetTopic(tp.Topic);
            if (topicInfo is null)
                continue;

            if (!topicMap.TryGetValue(tp.Topic, out var entry))
            {
                entry = (topicInfo.TopicId, []);
                topicMap[tp.Topic] = entry;
            }

            var ackBatches = new List<ShareAcknowledgeBatch>(batches.Count);
            foreach (var bd in batches)
            {
                ackBatches.Add(new ShareAcknowledgeBatch
                {
                    FirstOffset = bd.FirstOffset,
                    LastOffset = bd.LastOffset,
                    AcknowledgeTypes = bd.AcknowledgeTypes
                });
            }

            entry.Partitions.Add(new ShareAcknowledgePartition
            {
                PartitionIndex = tp.Partition,
                AcknowledgementBatches = ackBatches
            });
        }

        var topics = new List<ShareAcknowledgeTopic>(topicMap.Count);
        foreach (var (_, (topicId, ackPartitions)) in topicMap)
        {
            topics.Add(new ShareAcknowledgeTopic
            {
                TopicId = topicId,
                Partitions = ackPartitions
            });
        }

        return topics;
    }

    /// <summary>
    /// Finds the delivery count for an offset by searching the AcquiredRecords ranges.
    /// Returns -1 if the offset is not in any acquired range.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindDeliveryCount(
        IReadOnlyList<ShareFetchAcquiredRecords> acquiredRecords,
        long offset,
        ref int acquiredRecordIndex)
    {
        while (acquiredRecordIndex < acquiredRecords.Count)
        {
            var range = acquiredRecords[acquiredRecordIndex];
            if (offset >= range.FirstOffset && offset <= range.LastOffset)
                return range.DeliveryCount;

            if (offset < range.FirstOffset)
                return -1;

            acquiredRecordIndex++;
        }

        return -1;
    }

    private readonly record struct RenewedRecordKey(string Topic, int Partition, long Offset);

    private sealed class RenewedRecordState(ShareConsumeResult<TKey, TValue> record)
    {
        internal ShareConsumeResult<TKey, TValue> Record { get; set; } = record;
        internal bool Active { get; set; }
    }

    private readonly record struct ShareFetchBrokerResult(
        int BrokerId,
        short Version,
        ShareFetchResponse? Response,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? SentAcknowledgements);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfNotInitialized()
    {
        if (!_initialized)
            ThrowNotInitialized();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNotInitialized()
    {
        throw new InvalidOperationException(
            "Call InitializeAsync() or use BuildAsync() before consuming messages.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaShareConsumer<TKey, TValue>));
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Warning, Message = "ShareFetch failed for broker {BrokerId}")]
    private partial void LogFetchFailed(int brokerId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ShareFetch top-level error: {ErrorCode} - {ErrorMessage}")]
    private partial void LogFetchTopLevelError(ErrorCode errorCode, string? errorMessage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ShareFetch partition error: {Topic}-{Partition}: {ErrorCode}")]
    private partial void LogPartitionFetchError(string topic, int partition, ErrorCode errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ShareAcknowledge request failed for broker {BrokerId}")]
    private partial void LogAcknowledgeRequestFailed(int brokerId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Inline ShareFetch acknowledgement failed for broker {BrokerId}")]
    private partial void LogInlineAcknowledgeFailed(int brokerId, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Requested acquisition lock renewal for {Topic}-{Partition} at offset {Offset}")]
    private partial void LogRenewalRequested(string topic, int partition, long offset);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Closing share consumer")]
    private partial void LogClosingShareConsumer();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to flush acknowledgements during close")]
    private partial void LogFlushAcksFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to close share sessions")]
    private partial void LogCloseSessionsFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Graceful close timed out after 30s during dispose — broker may be unreachable")]
    private partial void LogDisposeCloseTimedOut();

    #endregion
}
