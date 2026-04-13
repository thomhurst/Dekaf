using System.Runtime.CompilerServices;
using Dekaf.Compression;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Microsoft.Extensions.Logging;

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

    private volatile IReadOnlySet<string> _subscriptionSnapshot = new HashSet<string>();
    private volatile IReadOnlySet<TopicPartition> _assignmentSnapshot = new HashSet<TopicPartition>();

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized;
    private int _closed;
    private int _disposed;
    private readonly bool _ownsInfrastructure;
    private volatile Task? _pendingReleaseTask;

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
                RequestTimeout = TimeSpan.FromMilliseconds(options.RequestTimeoutMs),
                SaslMechanism = options.SaslMechanism,
                SaslUsername = options.SaslUsername,
                SaslPassword = options.SaslPassword,
                GssapiConfig = options.GssapiConfig,
                OAuthBearerConfig = options.OAuthBearerConfig,
                OAuthBearerTokenProvider = options.OAuthBearerTokenProvider,
                SendBufferSize = options.SocketSendBufferBytes,
                ReceiveBufferSize = options.SocketReceiveBufferBytes
            },
            loggerFactory,
            connectionsPerBroker: options.ConnectionsPerBroker);

        var metadataManager = new MetadataManager(
            connectionPool,
            options.BootstrapServers,
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

    public IReadOnlySet<string> Subscription => _subscriptionSnapshot;
    public IReadOnlySet<TopicPartition> Assignment => _assignmentSnapshot;
    public string? MemberId => _coordinator.MemberId;

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

            var shareFetchVersion = _metadataManager.GetNegotiatedApiVersion(
                ApiKey.ShareFetch,
                ShareFetchRequest.LowestSupportedVersion,
                ShareFetchRequest.HighestSupportedVersion);

            // Send fetch requests to all brokers concurrently. Session epochs are per-broker
            // and independent, so parallelism is safe. This avoids waiting for each broker's
            // MaxWaitMs sequentially when partitions span multiple brokers.
            var fetchTasks = new List<Task<(int BrokerId, ShareFetchResponse? Response)>>(
                partitionsByBroker.Count);

            foreach (var (brokerId, partitions) in partitionsByBroker)
            {
                var topics = BuildShareFetchTopics(partitions, pendingAcks, shareFetchVersion);
                var sessionEpoch = _sessionManager.GetSessionEpoch(brokerId);
                var maxRecords = shareFetchVersion >= 1 ? _options.MaxPollRecords : 0;

                var request = new ShareFetchRequest
                {
                    GroupId = _options.GroupId,
                    MemberId = _coordinator.MemberId!,
                    ShareSessionEpoch = sessionEpoch,
                    MaxWaitMs = _options.FetchMaxWaitMs,
                    MinBytes = _options.FetchMinBytes,
                    MaxBytes = _options.FetchMaxBytes,
                    MaxRecords = maxRecords,
                    BatchSize = maxRecords,
                    Topics = topics
                };

                fetchTasks.Add(SendShareFetchAsync(brokerId, request, shareFetchVersion, cancellationToken));
            }

            await Task.WhenAll(fetchTasks).ConfigureAwait(false);

            // Process responses sequentially — yielding records and tracking acks
            var recordCount = 0;

            foreach (var fetchTask in fetchTasks)
            {
                if (recordCount >= _options.MaxPollRecords)
                    break;

                var (brokerId, response) = fetchTask.Result;

                if (response is null)
                    continue;

                // Handle top-level errors
                if (response.ErrorCode != ErrorCode.None)
                {
                    LogFetchTopLevelError(response.ErrorCode, response.ErrorMessage);
                    if (response.ErrorCode == ErrorCode.ShareSessionNotFound ||
                        response.ErrorCode == ErrorCode.InvalidShareSessionEpoch)
                    {
                        _sessionManager.ResetSession(brokerId);
                    }
                    continue;
                }

                // Check whether any partition in this response has acquired records.
                // We must advance the session epoch BEFORE yielding so that even if
                // the caller breaks from the async enumerable (disposing the iterator),
                // the epoch is already correct for a subsequent ShareAcknowledge/CommitAsync.
                // Without this, the session epoch stays at 0 while the broker expects 1,
                // causing InvalidShareSessionEpoch on the next request.
                var hasAcquiredRecords = false;
                for (var ti = 0; ti < response.Responses.Count && !hasAcquiredRecords; ti++)
                {
                    var respPartitions = response.Responses[ti].Partitions;
                    for (var pi = 0; pi < respPartitions.Count; pi++)
                    {
                        var p = respPartitions[pi];
                        if (p.ErrorCode == ErrorCode.None && !p.RecordBytes.IsEmpty &&
                            p.AcquiredRecords.Count > 0)
                        {
                            hasAcquiredRecords = true;
                            break;
                        }
                    }
                }

                if (hasAcquiredRecords)
                {
                    _sessionManager.IncrementEpoch(brokerId);
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
                            topicInfo, partition, _options.MaxPollRecords - recordCount);

                        var tp = new TopicPartition(topicInfo.Name, partition.PartitionIndex);

                        foreach (var result in parsed)
                        {
                            if (recordCount >= _options.MaxPollRecords)
                                break;

                            // Track only records actually yielded to the consumer so we don't
                            // silently acknowledge offsets truncated by MaxPollRecords.
                            _ackTracker.TrackDeliveredRecords(tp, result.Offset, result.Offset);

                            recordCount++;
                            yield return result;
                        }
                    }
                }
            }

            // If this poll round returned nothing, the broker's MaxWaitMs already
            // provided back-pressure. No additional client-side delay needed.
        }
    }

    public void Acknowledge(ShareConsumeResult<TKey, TValue> record, AcknowledgeType type = AcknowledgeType.Accept)
    {
        ThrowIfDisposed();

        record.AcknowledgeType = type;
        var tp = new TopicPartition(record.Topic, record.Partition);
        _ackTracker.Acknowledge(tp, record.Offset, type);
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

        var shareAckVersion = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.ShareAcknowledge,
            ShareAcknowledgeRequest.LowestSupportedVersion,
            ShareAcknowledgeRequest.HighestSupportedVersion);

        // Send to all brokers in parallel, collecting per-broker results
        var results = await Task.WhenAll(acksByBroker.Select(async kvp =>
        {
            try
            {
                await SendAcknowledgeAsync(kvp.Key, kvp.Value, shareAckVersion, cancellationToken)
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
                continue;

            firstError ??= error;
            _ackTracker.RequeueAcks(acks);
        }

        if (firstError is not null)
        {
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

        // Ensure close is called
        if (Interlocked.Exchange(ref _closed, 1) == 0)
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

        var shareAckVersion = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.ShareAcknowledge,
            ShareAcknowledgeRequest.LowestSupportedVersion,
            ShareAcknowledgeRequest.HighestSupportedVersion);

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
                    await SendAcknowledgeAsync(brokerId, acks, shareAckVersion, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogAcknowledgeRequestFailed(brokerId, ex);
                }
            }
        });
    }

    private async Task<(int BrokerId, ShareFetchResponse? Response)> SendShareFetchAsync(
        int brokerId, ShareFetchRequest request, short version, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await _connectionPool.GetConnectionAsync(brokerId, cancellationToken)
                .ConfigureAwait(false);
            var response = (ShareFetchResponse)await connection
                .SendAsync<ShareFetchRequest, ShareFetchResponse>(request, version, cancellationToken)
                .ConfigureAwait(false);
            return (brokerId, response);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFetchFailed(brokerId, ex);
            _sessionManager.ResetSession(brokerId);
            return (brokerId, null);
        }
    }

    private async ValueTask CloseShareSessionsAsync(CancellationToken cancellationToken)
    {
        var assignment = _assignmentSnapshot;
        if (assignment.Count == 0)
            return;

        var partitionsByBroker = GroupPartitionsByLeader(assignment);

        var shareFetchVersion = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.ShareFetch,
            ShareFetchRequest.LowestSupportedVersion,
            ShareFetchRequest.HighestSupportedVersion);

        foreach (var (brokerId, partitions) in partitionsByBroker)
        {
            var topics = BuildShareFetchTopics(partitions, pendingAcks: null, shareFetchVersion);

            var request = new ShareFetchRequest
            {
                GroupId = _options.GroupId,
                MemberId = _coordinator.MemberId!,
                ShareSessionEpoch = ShareSessionManager.CloseEpoch,
                MaxWaitMs = 0,
                MinBytes = 0,
                MaxBytes = 0,
                MaxRecords = shareFetchVersion >= 1 ? 1 : 0,
                Topics = topics
            };

            try
            {
                var connection = await _connectionPool.GetConnectionAsync(brokerId, cancellationToken)
                    .ConfigureAwait(false);
                await connection.SendAsync<ShareFetchRequest, ShareFetchResponse>(
                    request, shareFetchVersion, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort session close
            }
        }
    }

    private async Task SendAcknowledgeAsync(
        int brokerId,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> topicAcks,
        short shareAckVersion,
        CancellationToken cancellationToken)
    {
        var topics = BuildShareAcknowledgeTopics(topicAcks);
        var sessionEpoch = _sessionManager.GetSessionEpoch(brokerId);

        var request = new ShareAcknowledgeRequest
        {
            GroupId = _options.GroupId,
            MemberId = _coordinator.MemberId!,
            ShareSessionEpoch = sessionEpoch,
            Topics = topics
        };

        var connection = await _connectionPool.GetConnectionAsync(brokerId, cancellationToken)
            .ConfigureAwait(false);

        var response = (ShareAcknowledgeResponse)await connection
            .SendAsync<ShareAcknowledgeRequest, ShareAcknowledgeResponse>(
                request, shareAckVersion, cancellationToken)
            .ConfigureAwait(false);

        if (response.ErrorCode != ErrorCode.None)
        {
            throw new KafkaException(response.ErrorCode,
                $"ShareAcknowledge failed for broker {brokerId}: {response.ErrorCode} - {response.ErrorMessage}");
        }
    }

    /// <summary>
    /// Groups assigned partitions by their leader broker.
    /// </summary>
    private Dictionary<int, List<TopicPartition>> GroupPartitionsByLeader(
        IReadOnlySet<TopicPartition> assignment)
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

            foreach (var record in batch.Records)
            {
                if (results.Count >= maxRecords)
                    break;

                var offset = batch.BaseOffset + record.OffsetDelta;

                var deliveryCount = FindDeliveryCount(partition.AcquiredRecords, offset);
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

                Header[]? headers = null;
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

            batch.Dispose();
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
        long offset)
    {
        for (var i = 0; i < acquiredRecords.Count; i++)
        {
            var range = acquiredRecords[i];
            if (offset >= range.FirstOffset && offset <= range.LastOffset)
                return range.DeliveryCount;
        }
        return -1;
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
