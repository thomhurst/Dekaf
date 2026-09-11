using System.Buffers;
using System.Reflection;
using System.Threading.Tasks.Sources;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Exercises production polling with deterministic broker responses and persistent iterators.
/// The optional asynchronous response remains pending until the polling caller suspends, allowing
/// allocation comparisons to expose polling state machines hidden by synchronous parser fixtures.
/// This fixture does not model real broker latency or replace Kafka integration validation.
/// </summary>
[MemoryDiagnoser]
public class ShareConsumerPollBenchmarks
{
    private const string Topic = "share-poll";
    private static readonly Guid TopicId = Guid.Parse("b69e8b44-c498-4d45-b148-aaea33762965");
    private KafkaShareConsumer<int, int> _compatibility = null!;
    private KafkaShareConsumer<int, int> _borrowed = null!;
    private MetadataManager _metadata = null!;
    private Connection _connection = null!;
    private IAsyncEnumerator<ShareConsumeResult<int, int>> _records = null!;
    private IAsyncEnumerator<ShareConsumeBatch<int, int>> _batches = null!;
    private int _acquiredRecordCount;

    [Params(1, 1024)]
    public int RecordCount { get; set; }

    [Params(1, 16)]
    public int BatchCount { get; set; } = 1;

    [Params(false, true)]
    public bool AsynchronousResponse { get; set; }

    internal int IdleFetchMaxWaitMs { get; set; } = 200;
    internal bool RenewalMode { get; set; }
    internal int ReplayChunkSize { get; set; }
    internal ShareAcquisitionShape AcquisitionShape { get; set; }

    [GlobalSetup]
    public async ValueTask Setup()
    {
        var encodedValue = new ArrayBufferWriter<byte>();
        Serializers.Int32.Serialize(42, ref encodedValue, default);
        var records = new Record[RecordCount];
        for (var index = 0; index < records.Length; index++)
            records[index] = new Record { OffsetDelta = index, IsKeyNull = true, Value = encodedValue.WrittenMemory };
        var bytes = new ArrayBufferWriter<byte>();
        for (var index = 0; index < BatchCount; index++)
        {
            using var batch = new RecordBatch
            {
                BaseOffset = 1000 + index * RecordCount, LastOffsetDelta = RecordCount - 1, Records = records
            };
            batch.Write(bytes);
        }
        var totalRecords = checked(RecordCount * BatchCount);
        var acquired = ShareAcquisitionFixture.Create(1000, totalRecords, AcquisitionShape);
        var offsets = ShareAcquisitionFixture.Offsets(acquired);
        _acquiredRecordCount = offsets.Length;
        var response = new ShareFetchResponse
        {
            ErrorCode = ErrorCode.None,
            Responses = [new ShareFetchResponseTopic
            {
                TopicId = TopicId,
                Partitions = [new ShareFetchResponsePartition
                {
                    PartitionIndex = 0,
                    CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
                    RecordBytes = bytes.WrittenMemory,
                    AcquiredRecords = acquired
                }]
            }],
            NodeEndpoints = []
        };
        _connection = new Connection(response, AsynchronousResponse);
        var pool = new Pool(_connection);
        _metadata = new MetadataManager(pool, ["localhost:9092"]);
        _connection.MetadataResponse = new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
            Topics = [new TopicMetadata
            {
                ErrorCode = ErrorCode.None, Name = Topic, TopicId = TopicId,
                Partitions = [new PartitionMetadata
                {
                    ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = 1,
                    ReplicaNodes = [1], IsrNodes = [1]
                }]
            }]
        };
        _metadata.Metadata.Update(_connection.MetadataResponse);
        _compatibility = CreateConsumer(pool);
        _borrowed = CreateConsumer(pool, ReplayChunkSize);
        _records = _compatibility.PollAsync().GetAsyncEnumerator();
        _batches = _borrowed.PollBatchesAsync().GetAsyncEnumerator();
        await ValidatePolling(offsets, acquired);
        long expected = 0;
        foreach (var offset in offsets)
            expected += offset + 42;
        var borrowedChecksum = ReplayChunkSize != 0 ? await PollBorrowedChunkedRenewalCycle()
            : RenewalMode ? await PollBorrowedRenewalCycle() : await PollBorrowedBatch();
        if (await PollCompatibilityBatch() != expected || borrowedChecksum != expected)
            throw new InvalidOperationException("Polling changed the acquired records.");
    }

    [Benchmark]
    public async ValueTask<long> PollCompatibilityBatch()
    {
        long checksum = 0;
        for (var index = 0; index < _acquiredRecordCount; index++)
        {
            var next = _records.MoveNextAsync();
            if (!next.IsCompleted)
                _connection.CompletePendingFetch();
            if (!await next)
                throw new InvalidOperationException("Polling ended before the batch was delivered.");
            checksum += _records.Current.Offset + _records.Current.Value;
        }
        return checksum;
    }

    [Benchmark]
    public async ValueTask<long> PollBorrowedBatch()
    {
        long checksum = 0;
        for (var index = 0; index < BatchCount; index++)
        {
            var next = _batches.MoveNextAsync();
            if (!next.IsCompleted)
                _connection.CompletePendingFetch();
            if (!await next)
                throw new InvalidOperationException("Batch polling ended before delivery.");
            foreach (var record in _batches.Current)
                checksum += record.Offset + record.Value;
        }
        return checksum;
    }

    internal async ValueTask<long> PollBorrowedRenewalCycle()
    {
        var next = _batches.MoveNextAsync();
        if (!next.IsCompleted)
            _connection.CompletePendingFetch();
        if (!await next)
            throw new InvalidOperationException("Batch polling ended before delivery.");
        long checksum = 0;
        foreach (var record in _batches.Current)
        {
            checksum += record.Offset + record.Value;
            _batches.Current.Acknowledge(record, AcknowledgeType.Renew);
        }
        await _borrowed.CommitAsync();
        if (!await _batches.MoveNextAsync())
            throw new InvalidOperationException("Renewal replay ended before delivery.");
        var acknowledged = 0;
        foreach (var record in _batches.Current)
        {
            if (++acknowledged == _acquiredRecordCount)
                break; // Leave the last renewal active while earlier Accepts are pending.
            _batches.Current.Acknowledge(record);
        }
        var commitsBeforeReplay = _connection.AcknowledgeRequests;
        if (!await _batches.MoveNextAsync())
            throw new InvalidOperationException("Unread renewal was lost.");
        if (_connection.AcknowledgeRequests != commitsBeforeReplay + 1)
            throw new InvalidOperationException("Pending replay dispositions were not sent.");
        foreach (var record in _batches.Current)
            _batches.Current.Acknowledge(record);
        return checksum;
    }

    internal async ValueTask<long> PollBorrowedChunkedRenewalCycle()
    {
        for (var batchIndex = 0; batchIndex < BatchCount; batchIndex++)
        {
            var next = _batches.MoveNextAsync();
            if (!next.IsCompleted)
                _connection.CompletePendingFetch();
            if (!await next)
                throw new InvalidOperationException("Batch polling ended before acquisition.");
            foreach (var record in _batches.Current)
                _batches.Current.Acknowledge(record, AcknowledgeType.Renew);
        }
        await _borrowed.CommitAsync();

        var replayed = 0;
        long checksum = 0;
        while (replayed < _acquiredRecordCount)
        {
            if (!await _batches.MoveNextAsync())
                throw new InvalidOperationException("Renewal replay ended before completion.");
            if (_batches.Current.Count > ReplayChunkSize)
                throw new InvalidOperationException("Renewal replay exceeded its poll budget.");
            foreach (var record in _batches.Current)
            {
                checksum += record.Offset + record.Value;
                _batches.Current.Acknowledge(record);
                replayed++;
            }
        }
        await _borrowed.CommitAsync();
        if (replayed != _acquiredRecordCount)
            throw new InvalidOperationException("Renewal replay changed acquisition cardinality.");
        return checksum;
    }

    private async ValueTask ValidatePolling(long[] offsets, ShareFetchAcquiredRecords[] acquired)
    {
        var rangeIndex = 0;
        foreach (var offset in offsets)
        {
            while (offset > acquired[rangeIndex].LastOffset)
                rangeIndex++;
            var next = _records.MoveNextAsync();
            if (!next.IsCompleted)
                _connection.CompletePendingFetch();
            if (!await next || _records.Current.Offset != offset || _records.Current.Value != 42
                || _records.Current.DeliveryCount != acquired[rangeIndex].DeliveryCount)
                throw new InvalidOperationException("Compatibility polling changed acquisition order or delivery count.");
        }

        var recordIndex = 0;
        rangeIndex = 0;
        for (var batchIndex = 0; batchIndex < BatchCount; batchIndex++)
        {
            var next = _batches.MoveNextAsync();
            if (!next.IsCompleted)
                _connection.CompletePendingFetch();
            if (!await next)
                throw new InvalidOperationException("Borrowed polling lost an acquired batch.");
            foreach (var record in _batches.Current)
            {
                if (recordIndex >= offsets.Length || record.Offset != offsets[recordIndex++])
                    throw new InvalidOperationException("Borrowed polling changed acquisition order.");
                while (record.Offset > acquired[rangeIndex].LastOffset)
                    rangeIndex++;
                if (record.Value != 42 || record.DeliveryCount != acquired[rangeIndex].DeliveryCount)
                    throw new InvalidOperationException("Borrowed polling changed record data or delivery count.");
                if (RenewalMode)
                    _batches.Current.Acknowledge(record);
            }
        }
        if (recordIndex != offsets.Length)
            throw new InvalidOperationException("Borrowed polling changed acquisition cardinality.");
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        _metadataRestoreTimer?.Dispose();
        _connection.Asynchronous = false;
        await _records.DisposeAsync();
        await _batches.DisposeAsync();
        await _compatibility.DisposeAsync();
        await _borrowed.DisposeAsync();
        await _metadata.DisposeAsync();
    }

    private static readonly FieldInfo PendingReleaseTask = typeof(KafkaShareConsumer<int, int>)
        .GetField("_pendingReleaseTask", BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal async ValueTask PrepareUnsubscribeCycles()
    {
        await _batches.DisposeAsync();
        await _borrowed.CommitAsync();
        _connection.CaptureReleases = true;
    }

    internal async ValueTask<long> PollThenUnsubscribe()
    {
        _borrowed.Subscribe(Topic);
        _batches = _borrowed.PollBatchesAsync().GetAsyncEnumerator();
        var next = _batches.MoveNextAsync();
        if (!next.IsCompleted)
            _connection.CompletePendingFetch();
        if (!await next)
            throw new InvalidOperationException("No first batch to unsubscribe from.");
        _borrowed.Unsubscribe();
        if (PendingReleaseTask.GetValue(_borrowed) is Task pending)
            await pending;
        await _batches.DisposeAsync();
        return _connection.ReleasedOffsets;
    }

    private Action<ShareGroupHeartbeatAssignment>? _publishIdleAssignment;
    private Func<ValueTask<bool>>? _idleMoveNext;
    private static readonly ShareGroupHeartbeatAssignment EmptyAssignment = new() { TopicPartitions = [] };
    private static readonly ShareGroupHeartbeatAssignment AssignedPartition = new()
    {
        TopicPartitions = [new ShareGroupHeartbeatTopicPartitions { TopicId = TopicId, Partitions = [0] }]
    };

    private Func<bool, CancellationToken, ValueTask<bool>>? _sendLegacyHeartbeat;
    private Func<CancellationToken, ValueTask<bool>>? _sendHeartbeat;

    internal void PrepareSubscriptionHeartbeats()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var coordinator = typeof(KafkaShareConsumer<int, int>).GetField("_coordinator", flags)!.GetValue(_compatibility)!;
        typeof(ShareConsumerCoordinator).GetField("_coordinatorId", flags)!.SetValue(coordinator, 1);
        var method = typeof(ShareConsumerCoordinator).GetMethod("SendShareGroupHeartbeatAsync", flags)!;
        // Keep the same fixture compatible with the baseline's redundant initial-join argument.
        if (method.GetParameters().Length == 1)
            _sendHeartbeat = method.CreateDelegate<Func<CancellationToken, ValueTask<bool>>>(coordinator);
        else
            _sendLegacyHeartbeat = method.CreateDelegate<Func<bool, CancellationToken, ValueTask<bool>>>(coordinator);
    }

    internal ValueTask<bool> SendSubscriptionHeartbeat()
    {
        var pending = _sendHeartbeat is { } send
            ? send(CancellationToken.None) : _sendLegacyHeartbeat!(false, CancellationToken.None);
        _connection.CompleteHeartbeat();
        return pending;
    }

    internal void RepeatSubscription(bool batch)
        => (batch ? _borrowed : _compatibility).Subscribe(Topic);

    internal void PrepareIdlePolling(bool batch)
    {
        var consumer = batch ? _borrowed : _compatibility;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var coordinator = typeof(KafkaShareConsumer<int, int>).GetField("_coordinator", flags)!.GetValue(consumer)!;
        _publishIdleAssignment = typeof(ShareConsumerCoordinator)
            .GetMethod("ProcessShareGroupAssignment", flags)!
            .CreateDelegate<Action<ShareGroupHeartbeatAssignment>>(coordinator);
        _idleMoveNext = batch ? _batches.MoveNextAsync : _records.MoveNextAsync;
        PublishEmptyAssignment();
    }

    internal ValueTask<bool> BeginIdleRound()
    {
        PublishEmptyAssignment();
        return _idleMoveNext!();
    }

    internal void PublishIdleAssignment() => _publishIdleAssignment!(AssignedPartition);
    internal void PublishEmptyAssignment() => _publishIdleAssignment!(EmptyAssignment);
    private MetadataResponse? _missingLeader;
    private Timer? _metadataRestoreTimer;
    private TaskCompletionSource<bool>? _metadataRestored;
    private bool _asynchronousMetadata;
    private MissingLeaderAssignment? _missingLeaderAssignment;

    internal void PrepareMissingLeaderPolling(bool batch, bool asynchronousMetadata = false)
    {
        _asynchronousMetadata = asynchronousMetadata;
        PrepareIdlePolling(batch);
        PublishIdleAssignment();
        _missingLeader = new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
            Topics = [new TopicMetadata
            {
                ErrorCode = ErrorCode.None, Name = Topic, TopicId = TopicId,
                Partitions = [new PartitionMetadata
                {
                    ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = -1,
                    ReplicaNodes = [1], IsrNodes = [1]
                }]
            }]
        };
        _metadataRestoreTimer = new Timer(static state =>
        {
            var self = (ShareConsumerPollBenchmarks)state!;
            try
            {
                self._metadata.Metadata.Update(self._connection.MetadataResponse);
                self._connection.PendingMetadata?.TrySetResult(self._connection.MetadataResponse);
                self._metadataRestored!.TrySetResult(true);
            }
            catch (Exception error)
            {
                self._connection.PendingMetadata?.TrySetException(error);
                self._metadataRestored!.TrySetException(error);
            }
        }, this, Timeout.Infinite, Timeout.Infinite);
        // The first assignment enumeration in these warmed polls is broker routing.
        // Arm restoration only after routing has inspected the unavailable leader.
        // Unlike arming after MoveNextAsync returns, this also lets the old synchronous
        // spin finish. Subsequent enumerations use the ordinary HashSet enumerator.
        _missingLeaderAssignment = new MissingLeaderAssignment(this) { new(Topic, 0) };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var consumer = batch ? _borrowed : _compatibility;
        var coordinator = typeof(KafkaShareConsumer<int, int>).GetField("_coordinator", flags)!.GetValue(consumer)!;
        typeof(ShareConsumerCoordinator).GetField("_assignedPartitions", flags)!
            .SetValue(coordinator, _missingLeaderAssignment);
    }

    internal async ValueTask<bool> PollWithMissingLeader(int metadataDelayMs)
    {
        _metadata.Metadata.Update(_missingLeader!);
        var restored = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _metadataRestored = restored;
        if (_asynchronousMetadata)
            _connection.PendingMetadata = new TaskCompletionSource<MetadataResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _missingLeaderAssignment!.Prepare(metadataDelayMs);
        try
        {
            var delivered = await _idleMoveNext!();
            if (!_missingLeaderAssignment.ObservedMissingLeader)
                throw new InvalidOperationException("Polling did not inspect the missing leader.");
            return delivered;
        }
        finally
        {
            // Join the fixture's independent metadata publication before the next
            // operation, including when the client already refreshed it itself.
            try
            {
                if (_missingLeaderAssignment.ObservedMissingLeader)
                    await restored.Task;
            }
            finally
            {
                _metadataRestored = null;
                _connection.PendingMetadata = null;
            }
        }
    }

    private sealed class MissingLeaderAssignment(ShareConsumerPollBenchmarks poll)
        : HashSet<TopicPartition>, IEnumerable<TopicPartition>
    {
        private int _delayMs;
        internal bool ObservedMissingLeader { get; private set; }

        internal void Prepare(int delayMs)
        {
            _delayMs = delayMs;
            ObservedMissingLeader = false;
        }

        IEnumerator<TopicPartition> IEnumerable<TopicPartition>.GetEnumerator()
            => ObservedMissingLeader ? base.GetEnumerator() : ObserveRouting();

        private IEnumerator<TopicPartition> ObserveRouting()
        {
            using var partitions = base.GetEnumerator();
            while (partitions.MoveNext())
                yield return partitions.Current;

            if (poll._metadata.Metadata.GetPartitionLeader(Topic, 0) is not null)
                throw new InvalidOperationException("Metadata was restored before missing-leader routing completed.");
            ObservedMissingLeader = true;
            poll._metadataRestoreTimer!.Change(_delayMs, Timeout.Infinite);
        }
    }
    private KafkaShareConsumer<int, int> CreateConsumer(Pool pool, int maxPollRecords = 0)
    {
        var consumer = new KafkaShareConsumer<int, int>(new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], GroupId = "share-poll-benchmark",
            FetchMaxWaitMs = IdleFetchMaxWaitMs,
            MaxPollRecords = maxPollRecords == 0 ? checked(RecordCount * BatchCount) : maxPollRecords,
            AcknowledgementMode = RenewalMode ? ShareAcknowledgementMode.Explicit : ShareAcknowledgementMode.Implicit
        }, Serializers.Int32, Serializers.Int32, pool, _metadata);
        // Bypass joining a real group only during fixture setup. Fetching, session bookkeeping,
        // inline acknowledgements, parsing, delivery, and iterator cleanup use production code.
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(KafkaShareConsumer<int, int>).GetField("_initialized", flags)!.SetValue(consumer, true);
        var coordinator = typeof(KafkaShareConsumer<int, int>).GetField("_coordinator", flags)!.GetValue(consumer)!;
        typeof(ShareConsumerCoordinator).GetField("_memberId", flags)!.SetValue(coordinator, "member-1");
        typeof(ShareConsumerCoordinator).GetField("_state", flags)!.SetValue(coordinator, CoordinatorState.Stable);
        typeof(ShareConsumerCoordinator).GetField("_assignedPartitions", flags)!
            .SetValue(coordinator, new HashSet<TopicPartition> { new(Topic, 0) });
        consumer.Subscribe(Topic);
        return consumer;
    }

    private sealed class Connection(ShareFetchResponse fetch, bool asynchronous)
        : IKafkaConnection, IKafkaCapabilityProvider, IValueTaskSource<ShareFetchResponse>, IValueTaskSource<ShareGroupHeartbeatResponse>
    {
        private ManualResetValueTaskSourceCore<ShareFetchResponse> _completion;
        private bool _pending;
        private ManualResetValueTaskSourceCore<ShareGroupHeartbeatResponse> _heartbeatCompletion;
        private bool _heartbeatPending;
        private readonly ShareGroupHeartbeatResponse _heartbeat = new() { ErrorCode = ErrorCode.None, MemberEpoch = 1 };
        internal MetadataResponse MetadataResponse { get; set; } = null!;
        internal TaskCompletionSource<MetadataResponse>? PendingMetadata { get; set; }
        internal bool Asynchronous { get; set; } = asynchronous;
        internal int AcknowledgeRequests { get; private set; }
        internal long ReleasedOffsets { get; private set; }
        internal bool CaptureReleases { get; set; }
        private readonly ShareAcknowledgeResponse _acknowledge = new()
        {
            ErrorCode = ErrorCode.None, Responses = [], NodeEndpoints = []
        };
        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public KafkaConnectionCapabilities Capabilities { get; } = KafkaConnectionCapabilities.Create(new ApiVersionsResponse
        {
            ErrorCode = ErrorCode.None,
            ApiKeys = [new ApiVersion(ApiKey.ShareGroupHeartbeat, 0, 1), new ApiVersion(ApiKey.ShareFetch, 1, 2), new ApiVersion(ApiKey.ShareAcknowledge, 1, 2),
                new ApiVersion(ApiKey.Metadata, MetadataRequest.LowestSupportedVersion, MetadataRequest.HighestSupportedVersion)]
        });

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse
        {
            if (request is MetadataRequest && PendingMetadata is { } metadata)
                return AwaitMetadataAsync<TResponse>(metadata.Task, token);
            if (request is ShareAcknowledgeRequest acknowledge)
            {
                AcknowledgeRequests++;
                if (CaptureReleases)
                {
                    if (acknowledge.ShareSessionEpoch != ShareSessionManager.CloseEpoch)
                        throw new InvalidOperationException("Unsubscribe did not close the broker session.");
                    ReleasedOffsets = 0;
                    foreach (var topic in acknowledge.Topics)
                    foreach (var partition in topic.Partitions)
                    foreach (var batch in partition.AcknowledgementBatches!)
                    {
                        foreach (var type in batch.AcknowledgeTypes)
                            if (type != (byte)AcknowledgeType.Release)
                                throw new InvalidOperationException("Unsubscribe sent a non-release disposition.");
                    }
                    // Model the broker's final-session release, including unparsed records.
                    foreach (var topic in fetch.Responses)
                    foreach (var partition in topic.Partitions)
                    foreach (var acquired in partition.AcquiredRecords)
                        ReleasedOffsets += acquired.LastOffset - acquired.FirstOffset + 1;
                }
            }
            if (Asynchronous && request is ShareGroupHeartbeatRequest)
            {
                if (_heartbeatPending)
                    throw new InvalidOperationException("The fixture allows one pending heartbeat at a time.");
                _heartbeatCompletion.Reset();
                _heartbeatPending = true;
                return new ValueTask<TResponse>((IValueTaskSource<TResponse>)(object)this, _heartbeatCompletion.Version);
            }
            if (Asynchronous && request is ShareFetchRequest)
            {
                if (_pending)
                    throw new InvalidOperationException("The fixture allows one pending fetch at a time.");
                _completion.Reset();
                _pending = true;
                return new ValueTask<TResponse>((IValueTaskSource<TResponse>)(object)this, _completion.Version);
            }
            IKafkaResponse response = request switch
            {
                ShareGroupHeartbeatRequest => _heartbeat,
                ShareFetchRequest => fetch,
                ShareAcknowledgeRequest => _acknowledge,
                MetadataRequest => MetadataResponse,
                _ => throw new NotSupportedException(typeof(TRequest).Name)
            };
            return ValueTask.FromResult((TResponse)response);
        }

        private static async ValueTask<TResponse> AwaitMetadataAsync<TResponse>(
            Task<MetadataResponse> pending, CancellationToken token)
            where TResponse : IKafkaResponse
            => (TResponse)(IKafkaResponse)await pending.WaitAsync(token);

        internal void CompletePendingFetch()
        {
            if (!_pending)
                throw new InvalidOperationException("Polling suspended without a pending fixture response.");
            _pending = false;
            _completion.SetResult(fetch);
        }

        internal void CompleteHeartbeat()
        {
            if (!_heartbeatPending) return;
            _heartbeatPending = false;
            _heartbeatCompletion.SetResult(_heartbeat);
        }

        ShareGroupHeartbeatResponse IValueTaskSource<ShareGroupHeartbeatResponse>.GetResult(short token) => _heartbeatCompletion.GetResult(token);
        ValueTaskSourceStatus IValueTaskSource<ShareGroupHeartbeatResponse>.GetStatus(short token) => _heartbeatCompletion.GetStatus(token);
        void IValueTaskSource<ShareGroupHeartbeatResponse>.OnCompleted(Action<object?> continuation, object? state,
            short token, ValueTaskSourceOnCompletedFlags flags) => _heartbeatCompletion.OnCompleted(continuation, state, token, flags);

        ShareFetchResponse IValueTaskSource<ShareFetchResponse>.GetResult(short token) => _completion.GetResult(token);
        ValueTaskSourceStatus IValueTaskSource<ShareFetchResponse>.GetStatus(short token) => _completion.GetStatus(token);
        void IValueTaskSource<ShareFetchResponse>.OnCompleted(Action<object?> continuation, object? state,
            short token, ValueTaskSourceOnCompletedFlags flags) => _completion.OnCompleted(continuation, state, token, flags);
        public ValueTask ConnectAsync(CancellationToken token = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
    }

    private sealed class Pool(IKafkaConnection connection) : IConnectionPool
    {
        public ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken token = default) => ValueTask.FromResult(connection);
        public ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken token = default) => ValueTask.FromResult(connection);
        public ValueTask<IKafkaConnection> GetConnectionByIndexAsync(int brokerId, int index, CancellationToken token = default) => ValueTask.FromResult(connection);
        public void RegisterBroker(int id, string host, int port) { }
        public ValueTask<int> ScaleConnectionGroupAsync(int id, int count, CancellationToken token = default) => ValueTask.FromResult(1);
        public ValueTask<IKafkaConnection?> ShrinkConnectionGroupAsync(int id, int count, CancellationToken token = default) => ValueTask.FromResult<IKafkaConnection?>(null);
        public ValueTask RemoveConnectionAsync(int id) => ValueTask.CompletedTask;
        public ValueTask CloseAllAsync() => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
