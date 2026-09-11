using System.Runtime.CompilerServices;
using Dekaf.Compression;
using Dekaf.Diagnostics;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Retry;
using Dekaf.Serialization;
using Dekaf.Telemetry;
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
internal sealed partial class KafkaShareConsumer<TKey, TValue> :
    IKafkaShareBatchConsumer<TKey, TValue>,
    IApplicationTelemetryShareConsumer,
    IShareConsumerConfiguration,
    IHostedShareConsumer,
    IRawShareRecordAccessor,
    IKafkaClientInstanceIdentity,
    IKafkaClientStatusProvider
{
    private const int MaxDeserializerPreparationAttempts = 2;
    private readonly ShareConsumerOptions _options;
    private readonly IDeserializer<TKey> _keyDeserializer;
    private readonly IDeserializer<TValue> _valueDeserializer;
    private readonly IAsyncDeserializerPreparer<TKey>? _keyDeserializerPreparer;
    private readonly IAsyncDeserializerPreparer<TValue>? _valueDeserializerPreparer;
    private readonly bool _hasDeserializerPreparers;
    private readonly bool _inputIsolatedDeserializers;
    private readonly RecordHeaderRoutingPlan? _recordHeaderRoutingPlan;
    private readonly Headers? _recordHeaderDeserializationHeaders;
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
    private readonly ShareConsumerCoordinator _coordinator;
    private readonly ShareSessionManager _sessionManager = new();
    private readonly AcknowledgementTracker _ackTracker = new();
    private readonly CompressionCodecRegistry _compressionCodecs;
    private readonly ClientTelemetryManager _telemetryManager;
    private readonly ClientTelemetryMetricCollector _telemetryMetricCollector;
    private readonly ILogger _logger;
    private ShareAcknowledgementCommitCallback? _acknowledgementCommitCallback;

    // ThreadStatic reusable SerializationContext to avoid per-record allocations in ParsePartitionRecords.
    // Matches the pattern used by ConsumeResult<TKey, TValue> in the regular consumer.
    [ThreadStatic]
    private static SerializationContext t_serializationContext;

    private volatile HashSet<string> _subscriptionSnapshot = new();
    private volatile TopicPartitionSet _assignmentSnapshot = new HashSet<TopicPartition>();

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized;
    private int _closed;
    private int _disposed;
    private readonly bool _ownsInfrastructure;
    private volatile Task? _pendingReleaseTask;
    private Dictionary<RenewedRecordKey, RenewedRecordState>? _renewedRecords;
    private readonly List<ShareRecordBatchOwner> _polledBatchOwners = [];
    private Dictionary<TopicPartition, ShareRecordBatchOwner.Pool>? _recordBatchOwnerPools;
    private int _recordBatchScopes;
    private ShareRecordBufferPool? _recordBuffers;
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
                ConnectionTimeoutMax = options.ConnectionTimeoutMax,
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
                SaslScramMaxIterations = options.SaslScramMaxIterations,
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
        _acknowledgementCommitCallback = options.AcknowledgementCommitCallback;
        _keyDeserializer = RecordHeaderDeserializer.WrapIfNeeded(keyDeserializer);
        _valueDeserializer = RecordHeaderDeserializer.WrapIfNeeded(valueDeserializer);
        _inputIsolatedDeserializers = _keyDeserializer is IInputIsolatedDeserializer
            && _valueDeserializer is IInputIsolatedDeserializer;
        _recordHeaderRoutingPlan = RecordHeaderRoutingPlan.Create(
            _keyDeserializer,
            _valueDeserializer);
        _recordHeaderDeserializationHeaders = _recordHeaderRoutingPlan?.NeedsMaterializedHeaders is true
            ? new Headers(2)
            : null;
        _keyDeserializerPreparer = _keyDeserializer is IAsyncDeserializerPreparer<TKey> keyDeserializerPreparer &&
            _keyDeserializer is not IAsyncDeserializerPreparationRequirement { RequiresPreparation: false }
                ? keyDeserializerPreparer
                : null;
        _valueDeserializerPreparer = _valueDeserializer is IAsyncDeserializerPreparer<TValue> valueDeserializerPreparer &&
            _valueDeserializer is not IAsyncDeserializerPreparationRequirement { RequiresPreparation: false }
                ? valueDeserializerPreparer
                : null;
        _hasDeserializerPreparers = _keyDeserializerPreparer is not null ||
            _valueDeserializerPreparer is not null;
        _connectionPool = infrastructure.Pool;
        _metadataManager = infrastructure.Metadata;
        _ownsInfrastructure = ownsInfrastructure;
        _logger = loggerFactory?.CreateLogger<KafkaShareConsumer<TKey, TValue>>()
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<KafkaShareConsumer<TKey, TValue>>.Instance;

        _compressionCodecs = CompressionCodecRegistry.Default;
        _telemetryMetricCollector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.ShareConsumer);
        _telemetryMetricCollector.RegisterMetricsForSubscription(options.ApplicationMetrics);
        _telemetryManager = new ClientTelemetryManager(
            _connectionPool,
            _metadataManager,
            loggerFactory?.CreateLogger<ClientTelemetryManager>(),
            metricCollector: _telemetryMetricCollector,
            compressionCodecs: _compressionCodecs);

        _coordinator = new ShareConsumerCoordinator(
            options,
            _connectionPool,
            _metadataManager,
            loggerFactory?.CreateLogger<ShareConsumerCoordinator>(),
            telemetryMetrics: _telemetryMetricCollector.ShareConsumerMetrics);
    }

    private ShareConsumerTelemetryMetrics? ShareMetrics => _telemetryMetricCollector.ShareConsumerMetrics;
    private ShareConsumerTelemetryMetrics.FetchSample? _activeTelemetryFetch;

    public StringSet Subscription => _subscriptionSnapshot;
    public TopicPartitionSet Assignment => _assignmentSnapshot;
    public string? MemberId => _coordinator.MemberId;

    /// <inheritdoc />
    public string? ClusterId => _metadataManager.ClusterId;

    /// <inheritdoc />
    public Guid? ClientInstanceId => _telemetryManager.ClientInstanceId;

    /// <inheritdoc />
    public KafkaClientStatus GetStatus()
    {
        var stopped = Volatile.Read(ref _closed) != 0 || Volatile.Read(ref _disposed) != 0;
        return KafkaClientStatusFactory.Capture(
            KafkaClientRole.ShareConsumer,
            _connectionPool,
            _metadataManager,
            stopped,
            clientInstanceId: ClientInstanceId,
            consumerGroup: _coordinator.CaptureGroupStatus());
    }

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
            await _telemetryManager.StartAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public IKafkaShareConsumer<TKey, TValue> Subscribe(params string[] topics)
    {
        var subscription = new HashSet<string>(topics);
        if (_subscriptionSnapshot.SetEquals(subscription))
            return this;
        ClearBufferedRecords(releaseAcquisitions: true);
        // Cleared windows no longer participate in assignment-revocation cleanup.
        // Recheck their outcomes when the replacement assignment becomes available.
        _hasBufferedAcquisitionReleases |= _ackTracker.HasPending;
        _recordBatchOwnerPools?.Clear();
        _subscriptionSnapshot = subscription;
        _coordinator.UpdateSubscription(subscription);
        return this;
    }

    public IKafkaShareConsumer<TKey, TValue> Unsubscribe()
    {
        _batchAcknowledgements?.ReleaseActiveAcquisitions();
        ClearBufferedRecords(releaseAcquisitions: true);
        // Release any pending acks back to the group so other members can claim them,
        // rather than waiting for the broker's acquisition lock timeout to expire.
        if (HasPendingAcknowledgements || _assignmentSnapshot.Count != 0)
        {
            ReleasePendingAcks();
        }

        _activeShareBatch?.Dispose();
        _activeShareBatch = null;
        _batchAcknowledgements?.Clear();
        var subscription = new HashSet<string>();
        _subscriptionSnapshot = subscription;
        _recordBatchOwnerPools?.Clear();
        _coordinator.UpdateSubscription(subscription);
        _sessionManager.ResetAll();
        ClearRenewedRecords();
        return this;
    }

    public async IAsyncEnumerable<ShareConsumeResult<TKey, TValue>> PollAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();
        SelectConsumptionMode(batch: false);
        await WaitForPendingReleaseAsync(cancellationToken).ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested && Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _closed) == 0)
        {
            using var recordScope = BeginRecordBatchScope();
            if (_subscriptionSnapshot.Count == 0 || Volatile.Read(ref _closed) != 0 || Volatile.Read(ref _disposed) != 0)
                yield break;

            ShareMetrics?.PollStarted();
            ShareMetrics?.BeginPollWait();
            try
            {
                await _coordinator.EnsureActiveGroupAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                ShareMetrics?.EndPollWait();
            }
            if (_subscriptionSnapshot.Count == 0)
                yield break;
            _assignmentSnapshot = _coordinator.Assignment;

            var assignment = _assignmentSnapshot;
            RemoveRenewedRecordsOutsideAssignment(assignment);
            _hasBufferedAcquisitionReleases |= RemoveBufferedRecordsOutsideAssignment(assignment);
            if (_hasBufferedAcquisitionReleases)
            {
                // Removed partitions cannot acknowledge inline with the next fetch.
                // Keep the flag until acknowledgement outcomes are processed, including
                // inline failures that arrive after the assignment has changed.
                if (_ackTracker.HasPendingOutsideAssignment(assignment))
                {
                    ShareMetrics?.BeginPollWait();
                    try
                    {
                        await CommitCoreAsync(cancellationToken, releaseImplicit: false).ConfigureAwait(false);
                    }
                    finally
                    {
                        ShareMetrics?.EndPollWait();
                    }
                }
            }
            if (assignment.Count == 0)
            {
                ClearBufferedRecords();
                ShareMetrics?.BeginPollWait();
                try
                {
                    await WaitForAssignmentChangeAsync(assignment, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    ShareMetrics?.EndPollWait();
                }
                continue;
            }

            if (_pendingFetches is null && _bufferedRecordCount == 0)
            {
                ShareMetrics?.BeginPollWait();
                var fetchTasks = StartPollFetch(assignment, cancellationToken,
                    out var pendingAcks, out var sentAcknowledgementPartitionCount);
                using var responseScope = new BufferedShareFetchResponseScope(this, fetchTasks);
                ShareFetchBrokerResult[] fetchResults;
                try
                {
                    fetchResults = await Task.WhenAll(fetchTasks).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    RequeueAcknowledgements(pendingAcks);
                    InvokeAcknowledgementCommitCallback(pendingAcks, ex);
                    throw;
                }
                finally
                {
                    ShareMetrics?.EndPollWait();
                }

                var stopped = false;
                try
                {
                    CompletePollFetch(fetchResults, pendingAcks, sentAcknowledgementPartitionCount);
                }
                finally
                {
                    // Keep successful acquisitions even when another broker's bookkeeping fails.
                    // A late response after close/disposal remains owned by the response scope.
                    stopped = Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _closed) != 0;
                    if (!stopped)
                    {
                        _pendingFetches = fetchTasks;
                        AdvancePendingFetchCursor();
                    }
                }
                if (stopped)
                    yield break;
                if (fetchResults.Length == 0)
                {
                    // No broker request provided long-poll back-pressure. Refresh missing
                    // leaders and use the configured retry delay only if none is available.
                    ShareMetrics?.BeginPollWait();
                    try
                    {
                        await PrepareMissingLeaderRetryAsync(assignment, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        ShareMetrics?.EndPollWait();
                    }
                    continue;
                }
            }
            else if (_ackTracker.HasPending)
            {
                // Drain acknowledgements without acquiring another response while records remain.
                ShareMetrics?.BeginPollWait();
                try
                {
                    await CommitCoreAsync(cancellationToken, releaseImplicit: false).ConfigureAwait(false);
                }
                finally
                {
                    ShareMetrics?.EndPollWait();
                }
            }

            // A hosted stop observes every in-flight reply without delivering fetched
            // records or replaying renewed work. Close releases remaining acquisitions.
            if (_hostedProcessing && cancellationToken.IsCancellationRequested)
                yield break;

            var recordCount = 0;
            var reservedRenewalBudget = _renewedRecords is not null;
            if (reservedRenewalBudget)
            {
                // Reserve the fresh-record budget before choosing renewal replays. This remains
                // partition-lazy when renewal is inactive, preserving first-partition delivery.
                var firstUndisclosedOwner = _polledBatchOwners.Count;
                try
                {
                    while (_bufferedRecordCount < _options.MaxPollRecords &&
                        await BufferNextPartitionAsync(assignment, _options.MaxPollRecords - _bufferedRecordCount,
                            cancellationToken).ConfigureAwait(false))
                    {
                    }
                    RemoveBufferedRenewalDuplicates(_options.MaxPollRecords);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    ClearBufferedRecords();
                    ReleaseUndisclosedBatchOwners(firstUndisclosedOwner);
                    throw;
                }
            }

            if (_bufferedRecordCount < _options.MaxPollRecords && _renewedRecords is { Count: > 0 })
            {
                var renewedRecords = GetActiveRenewedRecords(
                    assignment,
                    _options.MaxPollRecords - Math.Min(_bufferedRecordCount, _options.MaxPollRecords));
                foreach (var renewedRecord in renewedRecords)
                {
                    if (_hostedProcessing)
                    {
                        // A previous handler can finish another record from this replay snapshot.
                        if (_renewedRecords is null || !_renewedRecords.TryGetValue(
                                new RenewedRecordKey(renewedRecord.Topic, renewedRecord.Partition, renewedRecord.Offset),
                                out var state) || !state.Active)
                            continue;
                        _acquisitionStartedTimestamp = state.ReceiptTimestamp;
                    }
                    Interlocked.Increment(ref _renewedRecordReplayCount);
                    recordCount++;
                    yield return renewedRecord;
                }
            }

            ShareRecordBatchOwner? pinnedOwner = null;
            while (recordCount < _options.MaxPollRecords && !cancellationToken.IsCancellationRequested)
            {
                if (_bufferedRecordCount == 0 &&
                    !await BufferNextPartitionAsync(assignment, _options.MaxPollRecords - recordCount,
                        cancellationToken).ConfigureAwait(false))
                    break;
                if (!TryTakeBufferedRecord(ref pinnedOwner, out var record))
                    continue;

                if (!reservedRenewalBudget && _renewedRecords is not null)
                    RemoveRenewedRecord(record.Topic, record.Partition, record.Offset);
                if (_options.AcknowledgementMode == ShareAcknowledgementMode.Implicit)
                    _ackTracker.TrackDeliveredRecords(new(record.Topic, record.Partition), record.Offset, record.Offset);

                recordCount++;
                yield return record;
            }

            // If this poll round returned nothing, the broker's MaxWaitMs already
            // provided back-pressure. No additional client-side delay needed.
        }
    }

    private List<Task<ShareFetchBrokerResult>> StartPollFetch(
        TopicPartitionSet assignment,
        CancellationToken cancellationToken,
        out Dictionary<TopicPartition, List<AcknowledgementBatchData>>? pendingAcks,
        out int sentAcknowledgementPartitionCount)
    {
        _rawRecords?.Clear();
        // All readable slices belong to the new poll; overwrite payload bytes as needed.
        _rawBuffer?.ResetWrittenCount();

        // Flush pending acks from previous poll as inline acknowledgements with the fetch
        pendingAcks = HasPendingAcknowledgements ? FlushAcknowledgements() : null;

        // Group assigned partitions by leader broker
        var partitionsByBroker = GroupPartitionsByLeader(assignment);

        // Send fetch requests to all brokers concurrently. Session epochs are per-broker
        // and independent, so parallelism is safe. This avoids waiting for each broker's
        // MaxWaitMs sequentially when partitions span multiple brokers.
        var fetchTasks = new List<Task<ShareFetchBrokerResult>>(
            partitionsByBroker.Count);
        sentAcknowledgementPartitionCount = 0;

        foreach (var (brokerId, partitions) in partitionsByBroker)
        {
            var brokerAcks = SelectAcknowledgements(pendingAcks, partitions);
            sentAcknowledgementPartitionCount += brokerAcks?.Count ?? 0;
            fetchTasks.Add(SendShareFetchForBrokerAsync(
                brokerId,
                partitions,
                brokerAcks,
                cancellationToken));
        }

        return fetchTasks;
    }

    private void CompletePollFetch(
        ShareFetchBrokerResult[] fetchResults,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? pendingAcks,
        int sentAcknowledgementPartitionCount)
    {
        // Process every response before yielding. Session epochs and inline
        // acknowledgements must advance even when an earlier broker fills the poll budget.
        Exception? firstFetchError = null;
        Dictionary<TopicPartition, Exception>? acknowledgementErrors = null;
        if (pendingAcks is not null && sentAcknowledgementPartitionCount != pendingAcks.Count)
        {
            var unsentAcknowledgements = GetUnsentAcknowledgements(pendingAcks, fetchResults);
            RequeueAcknowledgements(unsentAcknowledgements);
            AddAcknowledgementErrors(
                ref acknowledgementErrors,
                unsentAcknowledgements,
                KafkaException.FromErrorCode(
                    ErrorCode.UnknownTopicOrPartition,
                    "Inline ShareFetch acknowledgement could not resolve a partition leader."));
        }

        foreach (var fetchResult in fetchResults)
        {
            var (brokerId, version, response, sentAcks, error, acknowledgementError) = fetchResult;

            if (error is not null)
            {
                RequeueAcknowledgements(sentAcks);
                AddAcknowledgementErrors(ref acknowledgementErrors, sentAcks, acknowledgementError ?? error);
                firstFetchError ??= error;
                continue;
            }

            if (response is null)
            {
                RequeueAcknowledgements(sentAcks);
                if (acknowledgementError is not null)
                    AddAcknowledgementErrors(ref acknowledgementErrors, sentAcks, acknowledgementError);
                else if (sentAcks is not null)
                {
                    // Cancellation before the write is not an acknowledgement outcome.
                    // Retain these dispositions for final commit without reporting success.
                    foreach (var partition in sentAcks.Keys)
                        pendingAcks!.Remove(partition);
                }
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
                    // Note: _ackTracker may still hold pending acks from the now-invalid session.
                    // On next CommitAsync those acks will be sent with epoch 0 (new session).
                    // Renewal records represent partition locks, not fetch-session membership.
                    // Keep active records available for replay and pending Renew records ready
                    // for activation after the new session accepts them.
                }
                RequeueAcknowledgements(sentAcks);
                AddAcknowledgementErrors(
                    ref acknowledgementErrors,
                    sentAcks,
                    KafkaException.FromErrorCode(
                        response.ErrorCode,
                        $"Inline ShareFetch acknowledgement failed for broker {brokerId}: " +
                        $"{response.ErrorCode} - {response.ErrorMessage}"));
                continue;
            }

            // Advance the session epoch BEFORE yielding so that even if
            // the caller breaks from the async enumerable (disposing the iterator),
            // the epoch is already correct for a subsequent ShareAcknowledge/CommitAsync.
            _sessionManager.IncrementEpoch(brokerId);
            if (version >= 1)
                Volatile.Write(ref _acquisitionLockTimeoutMs, response.AcquisitionLockTimeoutMs);

            var acknowledgementFailures = GetAcknowledgementFailures(response, sentAcks);
            if (acknowledgementFailures is not null)
            {
                SplitAcknowledgements(
                    sentAcks,
                    acknowledgementFailures,
                    out var successfulAcknowledgements,
                    out var failedAcknowledgements);
                RecordAcknowledgementFailureMetrics(failedAcknowledgements);
                ApplySuccessfulAcknowledgements(successfulAcknowledgements, fetchResult.ReceivedTimestamp);
                RequeueAcknowledgements(failedAcknowledgements);
                AddAcknowledgementErrors(
                    ref acknowledgementErrors,
                    acknowledgementFailures.Errors);
                LogInlineAcknowledgeFailed(brokerId, acknowledgementFailures.FirstError);
            }
            else
            {
                ApplySuccessfulAcknowledgements(sentAcks, fetchResult.ReceivedTimestamp);
            }
        }

        if (_hasBufferedAcquisitionReleases && !_ackTracker.HasPending)
            _hasBufferedAcquisitionReleases = false;

        InvokeAcknowledgementCommitCallback(pendingAcks, acknowledgementErrors);

        if (firstFetchError is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstFetchError).Throw();
    }

    public void Acknowledge(ShareConsumeResult<TKey, TValue> record, AcknowledgeType type = AcknowledgeType.Accept)
    {
        ThrowIfDisposed();
        SelectConsumptionMode(batch: false);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((byte)type, (byte)AcknowledgeType.Renew);

        if (type == AcknowledgeType.Renew
            && _options.AcknowledgementMode != ShareAcknowledgementMode.Explicit)
        {
            throw new InvalidOperationException(
                "Renew acknowledgements require explicit acknowledgement mode.");
        }

        // Retain borrowed storage before queueing Renew; an expired record must
        // fail without leaving a renewal acknowledgement behind.
        TrackRenewalDisposition(record, type);
        record.AcknowledgeType = type;
        var tp = new TopicPartition(record.Topic, record.Partition);
        _ackTracker.Acknowledge(
            tp,
            record.Offset,
            type,
            requireTracked: _options.AcknowledgementMode == ShareAcknowledgementMode.Implicit);
    }

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        await CommitCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask CommitCoreAsync(CancellationToken cancellationToken, bool releaseImplicit = false)
    {
        if (!HasPendingAcknowledgements)
            return;

        var pendingAcks = FlushAcknowledgements(releaseImplicit);
        if (pendingAcks.Count == 0)
            return;

        // Group by broker
        var acksByBroker = GroupAcksByLeader(pendingAcks, out var unresolvedAcknowledgements);

        // Send to all brokers in parallel, collecting per-broker results
        var tasks = new List<Task<AcknowledgeBrokerResult>>(acksByBroker.Count);
        foreach (var (brokerId, acknowledgements) in acksByBroker)
        {
            tasks.Add(SendAcknowledgeForCommitAsync(
                brokerId,
                acknowledgements,
                cancellationToken));
        }

        // Keep the healthy path's existing task collection and awaiter. Metadata
        // recovery allocates additional state only when a leader is unresolved.
        var results = await (unresolvedAcknowledgements is null
            ? Task.WhenAll(tasks)
            : RefreshUnresolvedAcknowledgementsAsync(tasks, unresolvedAcknowledgements, cancellationToken))
            .ConfigureAwait(false);

        // Re-queue failed partitions so they can be retried on the next commit
        Exception? firstError = null;
        Dictionary<TopicPartition, Exception>? acknowledgementErrors = null;
        if (unresolvedAcknowledgements is { Count: > 0 })
        {
            var exception = KafkaException.FromErrorCode(
                ErrorCode.UnknownTopicOrPartition,
                "ShareAcknowledge could not resolve a partition leader.");
            RequeueAcknowledgements(unresolvedAcknowledgements);
            AddAcknowledgementErrors(
                ref acknowledgementErrors,
                unresolvedAcknowledgements,
                exception);
            firstError = exception;
        }

        foreach (var result in results)
        {
            ApplySuccessfulAcknowledgements(result.SuccessfulAcknowledgements);
            RequeueAcknowledgements(result.FailedAcknowledgements);
            if (result.WasNotSent)
            {
                foreach (var partition in result.FailedAcknowledgements!.Keys)
                    pendingAcks.Remove(partition);
                firstError ??= new OperationCanceledException(cancellationToken);
                continue;
            }
            AddAcknowledgementErrors(ref acknowledgementErrors, result.Errors);
            if (result.Error is not null)
                AddAcknowledgementErrors(ref acknowledgementErrors, result.FailedAcknowledgements, result.Error);
            if (result.Error is OperationCanceledException && cancellationToken.IsCancellationRequested)
                firstError = result.Error;
            else
                firstError ??= result.Error;
        }

        if (_hasBufferedAcquisitionReleases && !_ackTracker.HasPending)
            _hasBufferedAcquisitionReleases = false;

        InvokeAcknowledgementCommitCallback(pendingAcks, acknowledgementErrors);

        if (firstError is not null)
        {
            if (firstError is BrokerVersionException or OperationCanceledException)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstError).Throw();

            throw new KafkaException(
                $"CommitAsync partially failed — failed partitions have been re-queued for retry",
                firstError);
        }
    }

    private async Task<AcknowledgeBrokerResult[]> RefreshUnresolvedAcknowledgementsAsync(
        List<Task<AcknowledgeBrokerResult>> tasks,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> unresolved,
        CancellationToken cancellationToken)
    {
        // Observe every existing request before another acknowledgement can use the
        // same broker session. Successful outcomes remain in tasks for normal handling.
        await Task.WhenAll(tasks).ConfigureAwait(false);
        try
        {
            await PrepareRequestRetryAsync(0, cancellationToken, _assignmentSnapshot).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            // A canceled refresh submitted no acknowledgement. Preserve its outcomes
            // without reporting a broker result, and still process earlier requests.
            var failed = new Dictionary<TopicPartition, List<AcknowledgementBatchData>>(unresolved);
            unresolved.Clear();
            tasks.Add(Task.FromResult(error is OperationCanceledException && cancellationToken.IsCancellationRequested
                ? AcknowledgeBrokerResult.NotSent(failed)
                : new AcknowledgeBrokerResult(null, failed, error, null)));
            return await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        var resolved = GroupAcksByLeader(unresolved, out var remaining);
        unresolved.Clear();
        if (remaining is not null)
        {
            foreach (var (partition, batches) in remaining)
                unresolved.Add(partition, batches);
        }
        foreach (var (brokerId, acknowledgements) in resolved)
            tasks.Add(SendAcknowledgeForCommitAsync(brokerId, acknowledgements, cancellationToken));
        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task<AcknowledgeBrokerResult> SendAcknowledgeForCommitAsync(
        int brokerId,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> acknowledgements,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SendAcknowledgeAsync(
                    brokerId,
                    acknowledgements,
                    retryRetriableFailures: true,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new AcknowledgeBrokerResult(null, acknowledgements, ex, null);
        }
    }

    public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        await WaitForPendingReleaseAsync(cancellationToken).ConfigureAwait(false);
        if (Interlocked.Exchange(ref _closed, 1) != 0)
            return;
        _coordinator.NotifyAssignmentChange();

        LogClosingShareConsumer();
        ClearBufferedRecords();
        ClearRenewedRecords();
        _activeShareBatch?.Dispose();
        _activeShareBatch = null;

        // Step 1: Release delivered records that have not reached a poll/commit boundary.
        // Preserve explicit and previously submitted outcomes, including Renew. Closing
        // the share sessions below releases any remaining acquisition locks.
        if (HasPendingAcknowledgements)
        {
            try
            {
                await CommitCoreAsync(cancellationToken, releaseImplicit: true).ConfigureAwait(false);
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

        // Step 3: Leave the share group, then stop telemetry even if leaving fails.
        try
        {
            await _coordinator.LeaveGroupAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _batchAcknowledgements?.Dispose();
            await _telemetryManager.StopAsync(TimeSpan.FromSeconds(5), CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _coordinator.NotifyAssignmentChange();

        _recordBatchOwnerPools?.Clear();
        ClearBufferedRecords();
        ClearRenewedRecords();
        // An active iterator releases its scope when disposed/unwound, just like
        // its response frames. This also cleans up direct internal parser callers.
        if (_recordBatchScopes == 0)
        {
            ReleasePolledBatchOwners();
            _recordBuffers?.Dispose();
        }

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
        await _telemetryManager.DisposeAsync().ConfigureAwait(false);

        if (_ownsInfrastructure)
        {
            await _connectionPool.DisposeAsync().ConfigureAwait(false);
        }

        _rawRecords = null;
        _rawBuffer = null;
        _initLock.Dispose();
    }

    /// <summary>
    /// Overrides all pending acks to Release and sends them via ShareAcknowledge.
    /// Best-effort — errors are logged but not thrown.
    /// </summary>
    private void ReleasePendingAcks()
    {
        var pending = FlushAcknowledgements();
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
        // A final acknowledgement closes the broker session and releases every
        // acquisition, including producer batches that have not been parsed yet.
        // Include assigned brokers even when no local acknowledgement was recorded.
        foreach (var partition in _assignmentSnapshot)
        {
            var leader = _metadataManager.Metadata.GetPartitionLeader(partition.Topic, partition.Partition);
            if (leader is not null && !acksByBroker.ContainsKey(leader.NodeId))
                acksByBroker.Add(leader.NodeId, new Dictionary<TopicPartition, List<AcknowledgementBatchData>>());
        }
        if (acksByBroker.Count == 0)
            return;

        // Send in the background — best effort, don't block the synchronous caller.
        // Store the task so DisposeAsync can await it before tearing down the connection pool.
        // Chain after any prior release task so DisposeAsync only needs to await the latest reference.
        var prior = _pendingReleaseTask;
        _pendingReleaseTask = ReleaseAsync();

        async Task ReleaseAsync()
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
                    var result = await SendAcknowledgeAsync(
                            brokerId,
                            acks,
                            retryRetriableFailures: false,
                            cancellationToken: CancellationToken.None,
                            closeSession: true)
                        .ConfigureAwait(false);
                    if (result.Error is not null)
                        LogAcknowledgeRequestFailed(brokerId, result.Error);
                }
                catch (Exception ex)
                {
                    LogAcknowledgeRequestFailed(brokerId, ex);
                }
            }
        }
    }

    private async ValueTask WaitForPendingReleaseAsync(CancellationToken cancellationToken)
    {
        if (_pendingReleaseTask is not { } pending)
            return;
        await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (ReferenceEquals(_pendingReleaseTask, pending))
            _pendingReleaseTask = null;
    }

    private async Task<ShareFetchBrokerResult> SendShareFetchForPartitionsAsync(
        int brokerId,
        List<TopicPartition> partitions,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? pendingAcks,
        CancellationToken cancellationToken)
    {
        var brokerAcks = SelectAcknowledgements(pendingAcks, partitions);
        var result = await SendShareFetchForBrokerAsync(
                brokerId,
                partitions,
                brokerAcks,
                cancellationToken)
            .ConfigureAwait(false);
        if (result.Error is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(result.Error).Throw();

        return result;
    }

    private async Task<ShareFetchBrokerResult> SendShareFetchForBrokerAsync(
        int brokerId,
        List<TopicPartition> partitions,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? brokerAcks,
        CancellationToken cancellationToken)
    {
        var requestContext = GetHostedRequestContext(brokerId);
        var isRenewAck = ContainsRenewAcknowledgement(brokerAcks);

        for (var attempt = 0; ; attempt++)
        {
            requestContext?.Reset();
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
                PrepareFetchTelemetry(brokerId, request);
                ShareFetchResponse response;
                try
                {
                    response = await SendHostedRequestAsync<ShareFetchRequest, ShareFetchResponse>(
                        connection, request, version, requestContext, cancellationToken)
                    .ConfigureAwait(false);
                }
                catch
                {
                    if (requestContext is null || requestContext.WriteStarted)
                    {
                        ShareMetrics?.FetchFailed(brokerId);
                    }
                    throw;
                }
                var receivedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                ShareMetrics?.FetchCompleted(brokerId, response.ThrottleTimeMs, response.ErrorCode != ErrorCode.None);

                if (attempt < RetryHelper.MaxRetries && response.ErrorCode.IsRetriable())
                {
                    response.Dispose();
                    var retryError = await PrepareShareFetchRetryAsync(attempt, cancellationToken)
                        .ConfigureAwait(false);
                    if (retryError is not null)
                    {
                        return new ShareFetchBrokerResult(
                            brokerId,
                            version,
                            null,
                            brokerAcks,
                            retryError,
                            retryError);
                    }

                    continue;
                }

                return new ShareFetchBrokerResult(brokerId, version, response, brokerAcks, null, null)
                {
                    ReceivedTimestamp = receivedTimestamp
                };
            }
            catch (OperationCanceledException) when (requestContext is { WriteStarted: false }
                && cancellationToken.IsCancellationRequested && !_hostedRequestCancellationToken.IsCancellationRequested)
            {
                return new ShareFetchBrokerResult(brokerId, 0, null, brokerAcks, null, null);
            }
            catch (Exception ex) when (ex is OperationCanceledException or BrokerVersionException)
            {
                return new ShareFetchBrokerResult(brokerId, 0, null, brokerAcks, ex, ex);
            }
            catch (Exception ex)
            {
                if (attempt < RetryHelper.MaxRetries && RetryHelper.IsRetriableRequestFailure(ex))
                {
                    var retryError = await PrepareShareFetchRetryAsync(attempt, cancellationToken)
                        .ConfigureAwait(false);
                    if (retryError is not null)
                    {
                        return new ShareFetchBrokerResult(
                            brokerId,
                            0,
                            null,
                            brokerAcks,
                            retryError,
                            retryError);
                    }

                    continue;
                }

                LogFetchFailed(brokerId, ex);
                _sessionManager.ResetSession(brokerId);
                return new ShareFetchBrokerResult(brokerId, 0, null, brokerAcks, null, ex);
            }
        }
    }

    private async ValueTask<Exception?> PrepareShareFetchRetryAsync(
        int zeroBasedAttempt,
        CancellationToken cancellationToken)
    {
        try
        {
            await PrepareRequestRetryAsync(zeroBasedAttempt, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
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
        var submitted = false;
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
            ShareMetrics?.StartFetch(brokerId, 0, resetRecords: false);
            submitted = true;
            var response = await connection.SendAsync<ShareFetchRequest, ShareFetchResponse>(
                request, version, cancellationToken).ConfigureAwait(false);
            ShareMetrics?.FetchCompleted(brokerId, response.ThrottleTimeMs);
            submitted = false;
            response.Dispose();
        }
        catch
        {
            if (submitted) ShareMetrics?.FetchFailed(brokerId);
            // Best-effort session close
        }
    }

    private async Task<AcknowledgeBrokerResult> SendAcknowledgeAsync(
        int brokerId,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> topicAcks,
        bool retryRetriableFailures,
        CancellationToken cancellationToken,
        bool closeSession = false)
    {
        var requestContext = GetHostedRequestContext(brokerId);
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? successfulAcknowledgements = null;
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? failedAcknowledgements = null;
        Dictionary<TopicPartition, Exception>? acknowledgementErrors = null;
        Exception? firstError = null;
        var pendingAcknowledgements = topicAcks;

        for (var attempt = 0; ; attempt++)
        {
            requestContext?.Reset();
            try
            {
                var topics = BuildShareAcknowledgeTopics(pendingAcknowledgements);
                var isRenewAck = ContainsRenewAcknowledgement(pendingAcknowledgements);
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
                    ShareSessionEpoch = closeSession ? ShareSessionManager.CloseEpoch : _sessionManager.GetSessionEpoch(brokerId),
                    IsRenewAck = isRenewAck,
                    Topics = topics
                };
                PrepareAcknowledgementTelemetry(brokerId, request);
                ShareAcknowledgeResponse response;
                try
                {
                    response = await SendHostedRequestAsync<ShareAcknowledgeRequest, ShareAcknowledgeResponse>(
                        connection, request, shareAckVersion, requestContext, cancellationToken)
                    .ConfigureAwait(false);
                }
                catch
                {
                    if (requestContext is null || requestContext.WriteStarted)
                        ShareMetrics?.AcknowledgementRequestCompleted(brokerId, failed: true);
                    throw;
                }
                var receivedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                ShareMetrics?.AcknowledgementRequestCompleted(brokerId, response.ErrorCode != ErrorCode.None);

                if (response.ErrorCode != ErrorCode.None)
                {
                    if (!closeSession && response.ErrorCode is (ErrorCode.ShareSessionNotFound
                        or ErrorCode.InvalidShareSessionEpoch))
                    {
                        _sessionManager.ResetSession(brokerId);
                    }
                    else if (!closeSession)
                    {
                        _sessionManager.IncrementEpoch(brokerId);
                    }

                    throw KafkaException.FromErrorCode(response.ErrorCode,
                        $"ShareAcknowledge failed for broker {brokerId}: {response.ErrorCode} - {response.ErrorMessage}");
                }

                if (!closeSession)
                    _sessionManager.IncrementEpoch(brokerId);

                if (isRenewAck)
                    Volatile.Write(ref _acquisitionLockTimeoutMs, response.AcquisitionLockTimeoutMs);

                var acknowledgementFailures = GetAcknowledgementFailures(
                    response,
                    pendingAcknowledgements);
                if (acknowledgementFailures is null)
                {
                    RecordRenewalReceipt(pendingAcknowledgements, receivedTimestamp);
                    MergeAcknowledgements(ref successfulAcknowledgements, pendingAcknowledgements);
                    return new AcknowledgeBrokerResult(
                        successfulAcknowledgements,
                        failedAcknowledgements,
                        firstError,
                        acknowledgementErrors);
                }

                SplitAcknowledgements(
                    pendingAcknowledgements,
                    acknowledgementFailures,
                    out var successfulThisAttempt,
                    out var failedThisAttempt);
                RecordAcknowledgementFailureMetrics(failedThisAttempt);
                RecordRenewalReceipt(successfulThisAttempt, receivedTimestamp);
                MergeAcknowledgements(ref successfulAcknowledgements, successfulThisAttempt);

                SplitRetriableAcknowledgements(
                    failedThisAttempt!,
                    acknowledgementFailures,
                    retryRetriableFailures,
                    out var retriableAcknowledgements,
                    out var terminalAcknowledgements);
                if (terminalAcknowledgements is not null)
                {
                    MergeAcknowledgements(ref failedAcknowledgements, terminalAcknowledgements);
                    AddAcknowledgementErrors(
                        ref acknowledgementErrors,
                        acknowledgementFailures.Errors,
                        terminalAcknowledgements.Keys);
                    firstError ??= acknowledgementFailures.GetFirstError(terminalAcknowledgements.Keys);
                }

                if (retriableAcknowledgements is not null && attempt < RetryHelper.MaxRetries)
                {
                    pendingAcknowledgements = retriableAcknowledgements;
                    await PrepareRequestRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (retriableAcknowledgements is not null)
                {
                    MergeAcknowledgements(ref failedAcknowledgements, retriableAcknowledgements);
                    AddAcknowledgementErrors(
                        ref acknowledgementErrors,
                        acknowledgementFailures.Errors,
                        retriableAcknowledgements.Keys);
                    firstError ??= acknowledgementFailures.GetFirstError(retriableAcknowledgements.Keys);
                }

                return new AcknowledgeBrokerResult(
                    successfulAcknowledgements,
                    failedAcknowledgements,
                    firstError,
                    acknowledgementErrors);
            }
            catch (BrokerVersionException)
            {
                throw;
            }
            catch (OperationCanceledException) when (attempt == 0 && requestContext is { WriteStarted: false }
                && cancellationToken.IsCancellationRequested && !_hostedRequestCancellationToken.IsCancellationRequested)
            {
                return AcknowledgeBrokerResult.NotSent(pendingAcknowledgements);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                MergeAcknowledgements(ref failedAcknowledgements, pendingAcknowledgements);
                return new AcknowledgeBrokerResult(
                    successfulAcknowledgements,
                    failedAcknowledgements,
                    ex,
                    acknowledgementErrors);
            }
            catch (Exception ex) when (retryRetriableFailures
                                       && attempt < RetryHelper.MaxRetries
                                       && RetryHelper.IsRetriableRequestFailure(ex))
            {
                try
                {
                    await PrepareRequestRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException cancellationException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    MergeAcknowledgements(ref failedAcknowledgements, pendingAcknowledgements);
                    return new AcknowledgeBrokerResult(
                        successfulAcknowledgements,
                        failedAcknowledgements,
                        cancellationException,
                        acknowledgementErrors);
                }
            }
            catch (Exception ex)
            {
                MergeAcknowledgements(ref failedAcknowledgements, pendingAcknowledgements);
                AddAcknowledgementErrors(
                    ref acknowledgementErrors,
                    pendingAcknowledgements,
                    ex);
                return new AcknowledgeBrokerResult(
                    successfulAcknowledgements,
                    failedAcknowledgements,
                    firstError ?? ex,
                    acknowledgementErrors);
            }
        }
    }

    private async ValueTask PrepareRequestRetryAsync(
        int zeroBasedAttempt,
        CancellationToken cancellationToken,
        TopicPartitionSet? assignmentAwaitingLeader = null)
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

        if (assignmentAwaitingLeader is not null)
        {
            foreach (var partition in assignmentAwaitingLeader)
            {
                if (_metadataManager.Metadata.GetPartitionLeader(partition.Topic, partition.Partition) is not null)
                    return;
            }
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
    /// Keep this no-preparer path isolated from the preparation-aware parser below: it is the
    /// established hot path and is covered by before/after allocation and throughput benchmarks.
    /// ShareFetchResponse borrows frame bytes during response decoding. This later RecordBatch.Read
    /// runs outside that parsing scope and copies into independent pooled batch storage, so a renewed
    /// record retains its batch rather than the complete multi-partition response frame.
    /// </summary>
    private List<ShareConsumeResult<TKey, TValue>> ParsePartitionRecords(
        TopicInfo topicInfo,
        ShareFetchResponsePartition partition,
        int maxRecords)
    {
        var firstUndisclosedOwner = _polledBatchOwners.Count;
        var parserState = new DeserializerPreparationParserState();
        List<ShareConsumeResult<TKey, TValue>> results = [];
        try
        {
            ParsePartitionRecordsCore(topicInfo, partition, maxRecords, results, ref parserState);
            return results;
        }
        catch
        {
            // This eager call never returned its results. Release only its owners;
            // earlier partition deliveries still borrow their storage until the next poll.
            ReleaseUndisclosedBatchOwners(firstUndisclosedOwner);
            throw;
        }
        finally
        {
            parserState.DisposeCurrentBatch();
        }
    }

    private void ParsePartitionRecordsCore(
        TopicInfo topicInfo,
        ShareFetchResponsePartition partition,
        int maxRecords,
        List<ShareConsumeResult<TKey, TValue>> results,
        ref DeserializerPreparationParserState parserState)
    {
        if (_activeTelemetryFetch is null)
            ParsePartitionRecordsCore<RecordTelemetryDisabled>(topicInfo, partition, maxRecords, results, ref parserState);
        else
            ParsePartitionRecordsCore<RecordTelemetryEnabled>(topicInfo, partition, maxRecords, results, ref parserState);
    }

    private void ParsePartitionRecordsCore<TRecordTelemetry>(
        TopicInfo topicInfo,
        ShareFetchResponsePartition partition,
        int maxRecords,
        List<ShareConsumeResult<TKey, TValue>> results,
        ref DeserializerPreparationParserState parserState)
        where TRecordTelemetry : struct
    {
        var telemetryFetch = _activeTelemetryFetch;
        var initialCount = results.Count;
        long parsedBytes = 0;
        // A parser call covers one partition; reuse its owner pool across source batches.
        ShareRecordBatchOwner.Pool? ownerPool = null;
        while (results.Count < maxRecords)
        {
            if (parserState.CurrentBatch is null)
            {
                if (parserState.NextBatchByteOffset >= partition.RecordBytes.Length)
                    break;
                var reader = new KafkaProtocolReader(partition.RecordBytes[parserState.NextBatchByteOffset..]);
                try
                {
                    parserState.CurrentBatch = RecordBatch.ReadForShareConsumer(ref reader, _compressionCodecs,
                        _recordBuffers ??= new ShareRecordBufferPool(_options.FetchMaxBytes));
                }
                catch (InsufficientDataException)
                {
                    parserState.NextBatchByteOffset = partition.RecordBytes.Length;
                    break;
                }
                parserState.NextBatchByteOffset += checked((int)reader.Consumed);
                parserState.CurrentBatch.ConfigureHeaderRouting(_recordHeaderRoutingPlan);
            }

            var batch = parserState.CurrentBatch;
            var records = batch.Records;
            while (parserState.RecordIndex < records.Count && results.Count < maxRecords)
            {
                var record = records[parserState.RecordIndex];
                var offset = batch.BaseOffset + record.OffsetDelta;
                var deliveryCount = FindDeliveryCount(
                    partition.AcquiredRecords, offset, ref parserState.AcquiredRecordIndex);
                if (deliveryCount < 0)
                {
                    parserState.RecordIndex++;
                    continue;
                }
                t_serializationContext.Topic = topicInfo.Name;
                t_serializationContext.Component = SerializationComponent.Key;
                t_serializationContext.KeyData = ReadOnlyMemory<byte>.Empty;
                t_serializationContext.IsNull = record.IsKeyNull;
                var headerRouting = record.CreateHeaderRoutingLookup(
                    _recordHeaderRoutingPlan);
                var materializedHeaders = _recordHeaderDeserializationHeaders;
                if (materializedHeaders is not null)
                    headerRouting.CopyTo(materializedHeaders);
                t_serializationContext.Headers = headerRouting.KeyRequiresMaterializedHeaders
                    ? materializedHeaders
                    : null;
                var key = record.IsKeyNull
                    ? default
                    : RecordHeaderDeserializer.Deserialize(
                        _keyDeserializer,
                        record.Key,
                        t_serializationContext,
                        in headerRouting);

                t_serializationContext.Component = SerializationComponent.Value;
                t_serializationContext.KeyData = SerializationContext.NormalizeKeyData(
                    record.Key,
                    record.IsKeyNull);
                t_serializationContext.IsNull = record.IsValueNull;
                t_serializationContext.Headers = headerRouting.ValueRequiresMaterializedHeaders
                    ? materializedHeaders
                    : null;
                var value = record.IsValueNull
                    ? default!
                    : RecordHeaderDeserializer.Deserialize(
                        _valueDeserializer,
                        record.Value,
                        t_serializationContext,
                        in headerRouting);

                var headers = Array.Empty<Header>();
                if (record.Headers is not null && record.HeaderCount > 0)
                {
                    headers = new Header[record.HeaderCount];
                    Array.Copy(record.Headers, headers, record.HeaderCount);
                }

                if (_rawRecords is not null)
                {
                    CaptureRawRecord(new TopicPartitionOffset(topicInfo.Name, partition.PartitionIndex, offset),
                        record.IsKeyNull ? (ReadOnlyMemory<byte>?)null : record.Key,
                        record.IsValueNull ? (ReadOnlyMemory<byte>?)null : record.Value);
                }

                if (!_inputIsolatedDeserializers || headers.Length != 0)
                    parserState.CurrentOwner ??= RetainParsedBatch(
                        ownerPool ??= GetRecordBatchOwnerPool(new TopicPartition(topicInfo.Name, partition.PartitionIndex)), batch);
                var result = new ShareConsumeResult<TKey, TValue>
                {
                    Topic = topicInfo.Name,
                    Partition = partition.PartitionIndex,
                    Offset = offset,
                    Key = key,
                    Value = value,
                    Headers = headers,
                    TimestampMs = batch.BaseTimestamp + record.TimestampDelta,
                    DeliveryCount = deliveryCount
                };
                if (parserState.CurrentOwner is { } owner)
                    result.AttachBatchOwner(owner);
                results.Add(result);
                if (typeof(TRecordTelemetry) == typeof(RecordTelemetryEnabled))
                    parsedBytes += record.Length + Record.VarIntSize(record.Length);
                parserState.RecordIndex++;
            }
            if (parserState.RecordIndex >= records.Count)
                parserState.DisposeCurrentBatch();
        }
        if (typeof(TRecordTelemetry) == typeof(RecordTelemetryEnabled))
            ShareMetrics!.Parsed(telemetryFetch!, parsedBytes, results.Count - initialCount);
    }

    // The caller preserves parserState across preparation awaits so each record batch is
    // parsed and decompressed once even when several records require cold preparation.
    internal PendingDeserializerPreparation? ParsePartitionRecordsWithPreparation(
        TopicInfo topicInfo,
        ShareFetchResponsePartition partition,
        int maxRecords,
        List<ShareConsumeResult<TKey, TValue>> results,
        ref DeserializerPreparationParserState parserState,
        bool hasRetainedKey,
        TKey? retainedKey)
    {
        var telemetryFetch = _activeTelemetryFetch;
        var initialCount = results.Count;
        try
        {
            var pending = telemetryFetch is null
                ? ParsePartitionRecordsWithPreparationCore<RecordTelemetryDisabled>(
                    topicInfo, partition, maxRecords, results, ref parserState, hasRetainedKey, retainedKey)
                : ParsePartitionRecordsWithPreparationCore<RecordTelemetryEnabled>(
                    topicInfo, partition, maxRecords, results, ref parserState, hasRetainedKey, retainedKey);
            if (telemetryFetch is not null)
            {
                telemetryFetch.PendingRecords += results.Count - initialCount;
                if (pending is null)
                {
                    // A preparation suspension keeps this window's accounting local
                    // until it completes. Earlier delivered windows stay committed.
                    ShareMetrics!.Parsed(telemetryFetch, telemetryFetch.PendingBytes, telemetryFetch.PendingRecords);
                    telemetryFetch.ResetPending();
                }
            }
            return pending;
        }
        catch
        {
            telemetryFetch?.ResetPending();
            throw;
        }
    }

    private PendingDeserializerPreparation? ParsePartitionRecordsWithPreparationCore<TRecordTelemetry>(
        TopicInfo topicInfo,
        ShareFetchResponsePartition partition,
        int maxRecords,
        List<ShareConsumeResult<TKey, TValue>> results,
        ref DeserializerPreparationParserState parserState,
        bool hasRetainedKey,
        TKey? retainedKey)
        where TRecordTelemetry : struct
    {
        var telemetryFetch = _activeTelemetryFetch;
        long parsedBytes = 0;
        try
        {
            ShareRecordBatchOwner.Pool? ownerPool = null;
            while (results.Count < maxRecords)
            {
                if (parserState.CurrentBatch is null)
                {
                    if (parserState.NextBatchByteOffset >= partition.RecordBytes.Length)
                        break;

                    var reader = new KafkaProtocolReader(
                        partition.RecordBytes[parserState.NextBatchByteOffset..]);
                    try
                    {
                        parserState.CurrentBatch = RecordBatch.ReadForShareConsumer(ref reader, _compressionCodecs,
                            _recordBuffers ??= new ShareRecordBufferPool(_options.FetchMaxBytes));
                    }
                    catch (InsufficientDataException)
                    {
                        parserState.NextBatchByteOffset = partition.RecordBytes.Length;
                        break; // Partial batch
                    }

                    parserState.NextBatchByteOffset += checked((int)reader.Consumed);
                    parserState.CurrentBatch.ConfigureHeaderRouting(_recordHeaderRoutingPlan);
                }

                var batch = parserState.CurrentBatch;
                var records = batch.Records;
                while (parserState.RecordIndex < records.Count && results.Count < maxRecords)
                {
                    var record = records[parserState.RecordIndex];
                    var offset = batch.BaseOffset + record.OffsetDelta;

                    var deliveryCount = FindDeliveryCount(
                        partition.AcquiredRecords,
                        offset,
                        ref parserState.AcquiredRecordIndex);
                    if (deliveryCount < 0)
                    {
                        parserState.RecordIndex++;
                        continue;
                    }

                    t_serializationContext.Topic = topicInfo.Name;
                    t_serializationContext.Component = SerializationComponent.Key;
                    t_serializationContext.KeyData = ReadOnlyMemory<byte>.Empty;
                    t_serializationContext.IsNull = record.IsKeyNull;
                    var headerRouting = record.CreateHeaderRoutingLookup(
                        _recordHeaderRoutingPlan);
                    var materializedHeaders = _recordHeaderDeserializationHeaders;
                    if (materializedHeaders is not null)
                        headerRouting.CopyTo(materializedHeaders);
                    t_serializationContext.Headers = headerRouting.KeyRequiresMaterializedHeaders
                        ? materializedHeaders
                        : null;
                    TKey? key = default;
                    if (!record.IsKeyNull)
                    {
                        if (hasRetainedKey)
                        {
                            key = retainedKey;
                        }
                        else if (_keyDeserializerPreparer is { } keyPreparer)
                        {
                            if (!TryDeserializePrepared(
                                    keyPreparer,
                                    record.Key,
                                    t_serializationContext,
                                    in headerRouting,
                                    out key))
                            {
                                return CreatePendingPreparation(
                                    SerializationComponent.Key,
                                    topicInfo.Name,
                                    offset,
                                    record,
                                    retainedKey: default,
                                    hasRetainedKey: false);
                            }
                        }
                        else
                        {
                            key = RecordHeaderDeserializer.Deserialize(
                                _keyDeserializer,
                                record.Key,
                                t_serializationContext,
                                in headerRouting);
                        }
                    }

                    t_serializationContext.Component = SerializationComponent.Value;
                    t_serializationContext.KeyData = SerializationContext.NormalizeKeyData(
                        record.Key,
                        record.IsKeyNull);
                    t_serializationContext.IsNull = record.IsValueNull;
                    t_serializationContext.Headers = headerRouting.ValueRequiresMaterializedHeaders
                        ? materializedHeaders
                        : null;
                    TValue value;
                    if (record.IsValueNull)
                    {
                        value = default!;
                    }
                    else if (_valueDeserializerPreparer is { } valuePreparer)
                    {
                        if (!TryDeserializePrepared(
                                valuePreparer,
                                record.Value,
                                t_serializationContext,
                                in headerRouting,
                                out value))
                        {
                            return CreatePendingPreparation(
                                SerializationComponent.Value,
                                topicInfo.Name,
                                offset,
                                record,
                                key,
                                hasRetainedKey: !record.IsKeyNull);
                        }
                    }
                    else
                    {
                        value = RecordHeaderDeserializer.Deserialize(
                            _valueDeserializer,
                            record.Value,
                            t_serializationContext,
                            in headerRouting);
                    }

                    var headers = Array.Empty<Header>();
                    if (record.Headers is not null && record.HeaderCount > 0)
                    {
                        headers = new Header[record.HeaderCount];
                        Array.Copy(record.Headers, headers, record.HeaderCount);
                    }

                    if (_rawRecords is not null)
                    {
                        CaptureRawRecord(new TopicPartitionOffset(topicInfo.Name, partition.PartitionIndex, offset),
                            record.IsKeyNull ? (ReadOnlyMemory<byte>?)null : record.Key,
                            record.IsValueNull ? (ReadOnlyMemory<byte>?)null : record.Value);
                    }

                    if (!_inputIsolatedDeserializers || headers.Length != 0)
                        parserState.CurrentOwner ??= RetainParsedBatch(
                            ownerPool ??= GetRecordBatchOwnerPool(new TopicPartition(topicInfo.Name, partition.PartitionIndex)), batch);
                    var result = new ShareConsumeResult<TKey, TValue>
                    {
                        Topic = topicInfo.Name,
                        Partition = partition.PartitionIndex,
                        Offset = offset,
                        Key = key,
                        Value = value,
                        Headers = headers,
                        TimestampMs = batch.BaseTimestamp + record.TimestampDelta,
                        DeliveryCount = deliveryCount
                    };
                    if (parserState.CurrentOwner is { } owner)
                        result.AttachBatchOwner(owner);
                    results.Add(result);
                    if (typeof(TRecordTelemetry) == typeof(RecordTelemetryEnabled))
                        parsedBytes += record.Length + Record.VarIntSize(record.Length);
                    parserState.RecordIndex++;
                    hasRetainedKey = false;
                    retainedKey = default;
                }

                if (parserState.RecordIndex >= records.Count)
                    parserState.DisposeCurrentBatch();
            }

            return null;
        }
        finally
        {
            if (typeof(TRecordTelemetry) == typeof(RecordTelemetryEnabled))
                telemetryFetch!.PendingBytes += parsedBytes;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryDeserializePrepared<T>(
        IAsyncDeserializerPreparer<T> preparer,
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers,
        out T value) =>
        preparer is IRecordHeaderAsyncDeserializerPreparer<T> headerPreparer
            ? headerPreparer.TryDeserialize(data, context, in headers, out value)
            : preparer.TryDeserialize(data, context, out value);

    private PendingDeserializerPreparation CreatePendingPreparation(
        SerializationComponent component,
        string topic,
        long offset,
        in Record record,
        TKey? retainedKey,
        bool hasRetainedKey)
    {
        var keyData = record.IsKeyNull ? default : record.Key.ToArray().AsMemory();
        var data = component == SerializationComponent.Key
            ? keyData
            : record.IsValueNull
                ? ReadOnlyMemory<byte>.Empty
                : record.Value.ToArray();
        Header[]? headers = null;
        if (record.Headers is not null && record.HeaderCount > 0)
        {
            headers = new Header[record.HeaderCount];
            for (var index = 0; index < record.HeaderCount; index++)
            {
                var header = record.Headers[index];
                headers[index] = new Header(header.Key, header.GetValueAsArray());
            }
        }

        var durableRecord = new Record
        {
            Headers = headers,
            HeaderCount = headers?.Length ?? 0
        };
        if (_recordHeaderRoutingPlan is not null)
            durableRecord = durableRecord.IndexHeaders(_recordHeaderRoutingPlan);

        return new PendingDeserializerPreparation(
            component,
            offset,
            data,
            new SerializationContext
            {
                Topic = topic,
                Component = component,
                KeyData = component == SerializationComponent.Value ? keyData : ReadOnlyMemory<byte>.Empty,
                IsNull = component == SerializationComponent.Key ? record.IsKeyNull : record.IsValueNull
            },
            durableRecord.CreateHeaderRoutingLookup(_recordHeaderRoutingPlan),
            retainedKey,
            hasRetainedKey);
    }

    internal ValueTask PrepareDeserializerAsync(
        PendingDeserializerPreparation pending,
        CancellationToken cancellationToken) =>
        pending.Component == SerializationComponent.Key
            ? PrepareDeserializerAsync(
                _keyDeserializerPreparer ??
                    throw new InvalidOperationException("Key deserializer does not support preparation."),
                pending.Data,
                pending.Context,
                pending.HeaderRouting,
                cancellationToken)
            : PrepareDeserializerAsync(
                _valueDeserializerPreparer ??
                    throw new InvalidOperationException("Value deserializer does not support preparation."),
                pending.Data,
                pending.Context,
                pending.HeaderRouting,
                cancellationToken);

    private static ValueTask PrepareDeserializerAsync<T>(
        IAsyncDeserializerPreparer<T> preparer,
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        RecordHeaderRoutingLookup headers,
        CancellationToken cancellationToken) =>
        preparer is IRecordHeaderAsyncDeserializerPreparer<T> headerPreparer
            ? headerPreparer.PrepareAsync(data, context, headers, cancellationToken)
            : preparer.PrepareAsync(data, context, cancellationToken);

    internal sealed class PendingDeserializerPreparation(
        SerializationComponent component,
        long offset,
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        RecordHeaderRoutingLookup headerRouting,
        TKey? retainedKey,
        bool hasRetainedKey)
    {
        internal SerializationComponent Component { get; } = component;
        internal long Offset { get; } = offset;
        internal ReadOnlyMemory<byte> Data { get; } = data;
        internal SerializationContext Context { get; } = context;
        internal RecordHeaderRoutingLookup HeaderRouting { get; } = headerRouting;
        internal TKey? RetainedKey { get; } = retainedKey;
        internal bool HasRetainedKey { get; } = hasRetainedKey;
    }

    internal struct DeserializerPreparationParserState
    {
        internal RecordBatch? CurrentBatch;
        internal ShareRecordBatchOwner? CurrentOwner;
        internal int NextBatchByteOffset;
        internal int RecordIndex;
        internal int AcquiredRecordIndex;

        internal void DisposeCurrentBatch()
        {
            if (CurrentOwner is null)
                CurrentBatch?.DisposeAndReturnUnownedConsumerBatch();
            else
                CurrentOwner.CompleteParsing();
            CurrentOwner = null;
            CurrentBatch = null;
            RecordIndex = 0;
        }
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

    private static Dictionary<TopicPartition, List<AcknowledgementBatchData>> GetUnsentAcknowledgements(
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> pendingAcknowledgements,
        ShareFetchBrokerResult[] fetchResults)
    {
        var unsentAcknowledgements = new Dictionary<TopicPartition, List<AcknowledgementBatchData>>();
        foreach (var (topicPartition, batches) in pendingAcknowledgements)
        {
            var sent = false;
            foreach (var fetchResult in fetchResults)
            {
                if (fetchResult.SentAcknowledgements?.ContainsKey(topicPartition) != true)
                    continue;

                sent = true;
                break;
            }

            if (!sent)
                unsentAcknowledgements[topicPartition] = batches;
        }

        return unsentAcknowledgements;
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
        if (_batchAcknowledgements is { } batchAcknowledgements)
            batchAcknowledgements.RequeueAcknowledgements(acknowledgements);
        else if (acknowledgements is not null && acknowledgements.Count > 0)
            _ackTracker.RequeueAcks(acknowledgements);
    }

    private AcknowledgementFailures? GetAcknowledgementFailures(
        ShareFetchResponse response,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? acknowledgements)
    {
        if (acknowledgements is null || acknowledgements.Count == 0)
            return null;

        Dictionary<TopicPartition, KafkaException>? errors = null;
        foreach (var topic in response.Responses)
        {
            foreach (var partition in topic.Partitions)
            {
                if (partition.AcknowledgeErrorCode == ErrorCode.None)
                    continue;

                var error = KafkaException.FromErrorCode(
                    partition.AcknowledgeErrorCode,
                    $"Inline ShareFetch acknowledgement failed for partition " +
                    $"{partition.PartitionIndex}: {partition.AcknowledgeErrorMessage}");
                var topicInfo = _metadataManager.Metadata.GetTopic(topic.TopicId);
                if (topicInfo is null)
                    return AcknowledgementFailures.ForAll(acknowledgements.Keys, error);

                var topicPartition = new TopicPartition(topicInfo.Name, partition.PartitionIndex);
                if (!acknowledgements.ContainsKey(topicPartition))
                    continue;

                errors ??= [];
                errors[topicPartition] = error;
            }
        }

        return errors is null ? null : new AcknowledgementFailures(errors);
    }

    private AcknowledgementFailures? GetAcknowledgementFailures(
        ShareAcknowledgeResponse response,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> acknowledgements)
    {
        Dictionary<TopicPartition, KafkaException>? errors = null;
        foreach (var topic in response.Responses)
        {
            foreach (var partition in topic.Partitions)
            {
                if (partition.ErrorCode == ErrorCode.None)
                    continue;

                var error = KafkaException.FromErrorCode(
                    partition.ErrorCode,
                    $"ShareAcknowledge failed for partition {partition.PartitionIndex}: " +
                    partition.ErrorMessage);
                var topicInfo = _metadataManager.Metadata.GetTopic(topic.TopicId);
                if (topicInfo is null)
                    return AcknowledgementFailures.ForAll(acknowledgements.Keys, error);

                var topicPartition = new TopicPartition(topicInfo.Name, partition.PartitionIndex);
                if (!acknowledgements.ContainsKey(topicPartition))
                    continue;

                errors ??= [];
                errors[topicPartition] = error;
            }
        }

        return errors is null ? null : new AcknowledgementFailures(errors);
    }

    private static void SplitAcknowledgements(
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? acknowledgements,
        AcknowledgementFailures failures,
        out Dictionary<TopicPartition, List<AcknowledgementBatchData>>? successful,
        out Dictionary<TopicPartition, List<AcknowledgementBatchData>>? failed)
    {
        successful = null;
        failed = null;
        if (acknowledgements is null)
            return;

        foreach (var (topicPartition, batches) in acknowledgements)
        {
            if (failures.Errors.ContainsKey(topicPartition))
            {
                failed ??= [];
                failed[topicPartition] = batches;
            }
            else
            {
                successful ??= [];
                successful[topicPartition] = batches;
            }
        }
    }

    private static void SplitRetriableAcknowledgements(
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> acknowledgements,
        AcknowledgementFailures failures,
        bool retryRetriableFailures,
        out Dictionary<TopicPartition, List<AcknowledgementBatchData>>? retriable,
        out Dictionary<TopicPartition, List<AcknowledgementBatchData>>? terminal)
    {
        retriable = null;
        terminal = null;
        foreach (var (topicPartition, batches) in acknowledgements)
        {
            var canRetry = retryRetriableFailures
                           && failures.Errors[topicPartition].ErrorCode is { } errorCode
                           && errorCode.IsRetriable();
            if (canRetry)
            {
                retriable ??= [];
                retriable[topicPartition] = batches;
            }
            else
            {
                terminal ??= [];
                terminal[topicPartition] = batches;
            }
        }
    }

    private static void MergeAcknowledgements(
        ref Dictionary<TopicPartition, List<AcknowledgementBatchData>>? target,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? source)
    {
        if (source is null)
            return;

        target ??= [];
        foreach (var (topicPartition, batches) in source)
            target[topicPartition] = batches;
    }

    private void AddAcknowledgementErrors(
        ref Dictionary<TopicPartition, Exception>? target,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? acknowledgements,
        Exception exception)
    {
        if (_acknowledgementCommitCallback is null || acknowledgements is null)
            return;

        target ??= [];
        foreach (var topicPartition in acknowledgements.Keys)
            target.TryAdd(topicPartition, exception);
    }

    private void AddAcknowledgementErrors<TException>(
        ref Dictionary<TopicPartition, Exception>? target,
        Dictionary<TopicPartition, TException>? source)
        where TException : Exception
    {
        if (_acknowledgementCommitCallback is null || source is null)
            return;

        target ??= [];
        foreach (var (topicPartition, exception) in source)
            target[topicPartition] = exception;
    }

    private void AddAcknowledgementErrors<TException>(
        ref Dictionary<TopicPartition, Exception>? target,
        Dictionary<TopicPartition, TException> source,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>.KeyCollection topicPartitions)
        where TException : Exception
    {
        if (_acknowledgementCommitCallback is null)
            return;

        target ??= [];
        foreach (var topicPartition in topicPartitions)
        {
            if (source.TryGetValue(topicPartition, out var exception))
                target[topicPartition] = exception;
        }
    }

    private void InvokeAcknowledgementCommitCallback(
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? acknowledgements,
        Exception exception)
    {
        Dictionary<TopicPartition, Exception>? errors = null;
        AddAcknowledgementErrors(ref errors, acknowledgements, exception);
        InvokeAcknowledgementCommitCallback(acknowledgements, errors);
    }

    private void InvokeAcknowledgementCommitCallback(
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? acknowledgements,
        Dictionary<TopicPartition, Exception>? errors)
    {
        var callback = _acknowledgementCommitCallback;
        if (callback is null || acknowledgements is null || acknowledgements.Count == 0)
            return;

        try
        {
            AcknowledgementCommitCallbackInvoker.Invoke(callback, acknowledgements, errors);
        }
        catch (Exception ex)
        {
            LogAcknowledgementCommitCallbackFailed(ex);
        }
    }

    private void TrackRenewalDisposition(
        ShareConsumeResult<TKey, TValue> record,
        AcknowledgeType type)
    {
        if (type != AcknowledgeType.Renew)
        {
            // Hosted handlers finish renewed work in place. A terminal disposition must
            // stop local replay immediately; the ack tracker still retains broker submission.
            if (_hostedProcessing)
                RemoveRenewedRecord(record.Topic, record.Partition, record.Offset);
            return;
        }

        var key = new RenewedRecordKey(record.Topic, record.Partition, record.Offset);
        if (_renewedRecords is not null && _renewedRecords.TryGetValue(key, out var state))
        {
            state.Record = record;
            state.Active = false;
        }
        else
        {
            var renewed = new RenewedRecordState(record);
            (_renewedRecords ??= [])[key] = renewed;
        }

        Interlocked.Increment(ref _renewalRequestCount);
        LogRenewalRequested(record.Topic, record.Partition, record.Offset);
    }

    private void ApplySuccessfulAcknowledgements(
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? acknowledgements,
        long receivedTimestamp = 0)
    {
        if (_batchAcknowledgements is { } batchAcknowledgements)
        {
            batchAcknowledgements.ApplySuccessfulAcknowledgements(acknowledgements);
            return;
        }
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
                        {
                            if (receivedTimestamp != 0)
                                state.ReceiptTimestamp = receivedTimestamp;
                            state.Active = true;
                        }
                    }
                    else
                    {
                        ReleaseRenewedRecord(key);
                    }
                }
            }
        }

        if (_renewedRecords.Count == 0)
            _renewedRecords = null;
    }

    private void RecordRenewalReceipt(
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? acknowledgements, long receivedTimestamp)
    {
        if (!_hostedProcessing || _renewedRecords is null || acknowledgements is null)
            return;

        // Broker tasks only read the dictionary and update their own records. Activation and
        // dictionary mutation remain after Task.WhenAll. Retried partitions keep distinct times.
        foreach (var (partition, batches) in acknowledgements)
            foreach (var batch in batches)
                for (var index = 0; index < batch.AcknowledgeTypes.Length; index++)
                    if (batch.AcknowledgeTypes[index] == (byte)AcknowledgeType.Renew
                        && _renewedRecords.TryGetValue(new RenewedRecordKey(
                            partition.Topic, partition.Partition, batch.FirstOffset + index), out var state))
                        state.ReceiptTimestamp = -receivedTimestamp;
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

            // A terminal CommitAsync inside the replay handler can remove the
            // renewal state. Keep this delivery's payload alive until the next poll.
            if (state.Record.RetainedBatchOwner is { } retainedOwner)
            {
                var owner = retainedOwner.RefreshGeneration();
                state.Record.AttachBatchOwner(owner);
                if (!owner.RenewalPinned)
                {
                    owner.Retain();
                    try
                    {
                        _polledBatchOwners.Add(owner);
                        owner.RenewalPinned = true;
                    }
                    catch
                    {
                        owner.Release();
                        throw;
                    }
                }
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

        ReleaseRenewedRecord(new RenewedRecordKey(topic, partition, offset));
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
            ReleaseRenewedRecord(key);

        if (_renewedRecords.Count == 0)
            _renewedRecords = null;
    }

    private void ReleaseRenewedRecord(RenewedRecordKey key)
    {
        if (_renewedRecords!.Remove(key, out var state))
            state.Release();
    }

    private void ClearRenewedRecords()
    {
        if (_renewedRecords is null)
            return;
        foreach (var state in _renewedRecords.Values)
            state.Release();
        _renewedRecords = null;
    }

    private ShareRecordBatchOwner.Pool GetRecordBatchOwnerPool(TopicPartition partition)
    {
        _recordBatchOwnerPools ??= new();
        if (!_recordBatchOwnerPools.TryGetValue(partition, out var pool))
        {
            pool = new ShareRecordBatchOwner.Pool(partition);
            _recordBatchOwnerPools.Add(partition, pool);
        }
        return pool;
    }

    private ShareRecordBatchOwner RetainParsedBatch(ShareRecordBatchOwner.Pool pool, RecordBatch batch)
    {
        var owner = pool.Rent(batch);
        _polledBatchOwners.Add(owner);
        return owner;
    }

    internal RecordBatchScope BeginRecordBatchScope()
    {
        // Keep the last delivery valid between iterator disposal and the next poll,
        // so callers can acknowledge Renew after consuming a single record.
        ReleasePolledBatchOwners();
        _recordBatchScopeId++;
        _recordBatchScopes++;
        return new RecordBatchScope(this);
    }

    private void ReleaseUndisclosedBatchOwners(int firstOwner)
    {
        for (var index = _polledBatchOwners.Count - 1; index >= firstOwner; index--)
        {
            var owner = _polledBatchOwners[index];
            _polledBatchOwners.RemoveAt(index);
            owner.RenewalPinned = false;
            owner.Release();
        }
    }

    private void ReleasePolledBatchOwners()
    {
        foreach (var owner in _polledBatchOwners)
        {
            owner.RenewalPinned = false;
            owner.ReleasePoll();
        }
        _polledBatchOwners.Clear();
    }

    internal readonly struct RecordBatchScope(KafkaShareConsumer<TKey, TValue> consumer) : IDisposable
    {
        public void Dispose()
        {
            consumer._recordBatchScopes--;
            if (consumer._recordBatchScopes == 0)
            {
                consumer.DisposeDeferredFetches();
                if (Volatile.Read(ref consumer._disposed) != 0)
                {
                    consumer.ReleasePolledBatchOwners();
                    consumer._recordBuffers?.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Groups acknowledgement data by leader broker.
    /// </summary>
    private Dictionary<int, Dictionary<TopicPartition, List<AcknowledgementBatchData>>> GroupAcksByLeader(
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> acks)
        => GroupAcksByLeader(acks, out _);

    private Dictionary<int, Dictionary<TopicPartition, List<AcknowledgementBatchData>>> GroupAcksByLeader(
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> acks,
        out Dictionary<TopicPartition, List<AcknowledgementBatchData>>? unresolvedAcknowledgements)
    {
        var result = new Dictionary<int, Dictionary<TopicPartition, List<AcknowledgementBatchData>>>();
        unresolvedAcknowledgements = null;

        foreach (var (tp, batches) in acks)
        {
            var topicInfo = _metadataManager.Metadata.GetTopic(tp.Topic);
            if (topicInfo is null)
            {
                unresolvedAcknowledgements ??= [];
                unresolvedAcknowledgements[tp] = batches;
                continue;
            }

            var leaderNode = _metadataManager.Metadata.GetPartitionLeader(tp.Topic, tp.Partition);
            if (leaderNode is null)
            {
                unresolvedAcknowledgements ??= [];
                unresolvedAcknowledgements[tp] = batches;
                continue;
            }

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

    private sealed class RenewedRecordState
    {
        private ShareConsumeResult<TKey, TValue> _record;

        internal RenewedRecordState(ShareConsumeResult<TKey, TValue> record)
        {
            record.BatchOwner?.Retain();
            _record = record;
        }

        internal ShareConsumeResult<TKey, TValue> Record
        {
            get => _record;
            set
            {
                if (ReferenceEquals(_record, value))
                    return;
                var previousOwner = _record.RetainedBatchOwner;
                var nextOwner = value.BatchOwner;
                if (!ReferenceEquals(previousOwner, nextOwner))
                {
                    nextOwner?.Retain();
                    previousOwner?.Release();
                }
                _record = value;
            }
        }

        internal void Release() => _record.RetainedBatchOwner?.Release();
        // Preserve the receipt timestamp used by hosted handlers without adding
        // another field for pending/active state.
        internal long ReceiptTimestamp { get; set; }
        internal bool Active
        {
            get => ReceiptTimestamp > 0;
            set => ReceiptTimestamp = value ? Math.Max(1, Math.Abs(ReceiptTimestamp)) : 0;
        }
    }

    private sealed class AcknowledgementFailures(
        Dictionary<TopicPartition, KafkaException> errors)
    {
        internal Dictionary<TopicPartition, KafkaException> Errors { get; } = errors;
        internal KafkaException FirstError => Errors.Values.First();

        internal KafkaException GetFirstError(IEnumerable<TopicPartition> topicPartitions)
        {
            foreach (var topicPartition in topicPartitions)
            {
                if (Errors.TryGetValue(topicPartition, out var error))
                    return error;
            }

            return FirstError;
        }

        internal static AcknowledgementFailures ForAll(
            IEnumerable<TopicPartition> topicPartitions,
            KafkaException error)
        {
            var errors = new Dictionary<TopicPartition, KafkaException>();
            foreach (var topicPartition in topicPartitions)
                errors[topicPartition] = error;

            return new AcknowledgementFailures(errors);
        }
    }

    private readonly record struct AcknowledgeBrokerResult(
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? SuccessfulAcknowledgements,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? FailedAcknowledgements,
        Exception? Error,
        Dictionary<TopicPartition, Exception>? Errors)
    {
        // Pending acknowledgements without a response or failure were never submitted.
        // Reuse the existing representation without enlarging every broker result.
        internal bool WasNotSent => FailedAcknowledgements is not null
            && SuccessfulAcknowledgements is null && Error is null && Errors is null;

        internal static AcknowledgeBrokerResult NotSent(
            Dictionary<TopicPartition, List<AcknowledgementBatchData>> acknowledgements)
            => new(null, acknowledgements, null, null);
    }

    private readonly struct ShareFetchResponseScope(List<Task<ShareFetchBrokerResult>> tasks) : IDisposable
    {
        public void Dispose()
        {
            // Task.WhenAll has observed every task before the poll can leave this scope.
            foreach (var task in tasks)
            {
                if (task.Status == TaskStatus.RanToCompletion)
                    task.GetAwaiter().GetResult().Response?.Dispose();
            }
        }
    }

    private readonly struct BufferedShareFetchResponseScope(
        KafkaShareConsumer<TKey, TValue> consumer, List<Task<ShareFetchBrokerResult>> tasks) : IDisposable
    {
        public void Dispose()
        {
            if (!ReferenceEquals(consumer._pendingFetches, tasks))
                new ShareFetchResponseScope(tasks).Dispose();
        }
    }

    private readonly record struct ShareFetchBrokerResult(
        int BrokerId,
        short Version,
        ShareFetchResponse? Response,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? SentAcknowledgements,
        Exception? Error,
        Exception? AcknowledgementError)
    {
        // Capture before awaiting other brokers or preparing deserializers. Every record in
        // this response shares the same local receipt time, including records buffered for yield.
        public long ReceivedTimestamp { get; init; }
    }

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

    [LoggerMessage(Level = LogLevel.Error, Message = "Share acknowledgement commit callback threw an exception")]
    private partial void LogAcknowledgementCommitCallbackFailed(Exception exception);

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
