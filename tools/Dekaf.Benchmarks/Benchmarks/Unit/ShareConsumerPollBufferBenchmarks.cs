using System.Buffers;
using System.Reflection;
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
/// Exercises public classic polling with borrowed values, including batch ownership and
/// response continuation. Each operation consumes one MaxPollRecords-sized window and
/// disposes the iterator, retaining the consumer between operations. The broker fixture
/// reuses serialized responses: old revisions fetch again after truncating a response,
/// while fixed revisions drain the retained acquisition. This measures polling costs,
/// not unique-delivery throughput; unit and integration tests verify complete delivery.
/// </summary>
[MemoryDiagnoser]
public class ShareConsumerPollBufferBenchmarks
{
    private const int RecordsPerBatch = 64;
    private const int BatchCount = 2;
    private const string Topic = "share-poll-buffer";
    private static readonly Guid TopicId = Guid.Parse("fd97e852-d464-45fa-9973-dd11d4265dcb");
    private KafkaShareConsumer<int, ReadOnlyMemory<byte>> _consumer = null!;
    private MetadataManager _metadata = null!;
    private int _recordCount;
    private int _recordsPerPartition;
    private int _windowSize;
    private Action<ShareGroupHeartbeatAssignment> _updateAssignment = null!;
    private ShareGroupHeartbeatAssignment _heartbeatAssignment = null!;

    [Params(1, 64)]
    public int PartitionCount { get; set; }

    [Params(false, true)]
    public bool Overflow { get; set; }

    [Params(false, true)]
    public bool Prepared { get; set; }

    [Params(false, true)]
    public bool RenewalBuffering { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _recordsPerPartition = BatchCount * RecordsPerBatch;
        _recordCount = _recordsPerPartition * PartitionCount;
        var bytes = new ArrayBufferWriter<byte>();
        var payload = new byte[32];
        for (var batchIndex = 0; batchIndex < BatchCount; batchIndex++)
        {
            var records = new List<Record>(RecordsPerBatch);
            for (var index = 0; index < RecordsPerBatch; index++)
                records.Add(new Record { OffsetDelta = index, IsKeyNull = true, Value = payload });
            using var batch = new RecordBatch { BaseOffset = batchIndex * RecordsPerBatch, Records = records };
            batch.Write(bytes);
        }
        var partitionResponses = new ShareFetchResponsePartition[PartitionCount];
        var metadataPartitions = new PartitionMetadata[PartitionCount];
        var assignment = new HashSet<TopicPartition>();
        for (var partitionIndex = 0; partitionIndex < PartitionCount; partitionIndex++)
        {
            partitionResponses[partitionIndex] = new ShareFetchResponsePartition
            {
                PartitionIndex = partitionIndex, CurrentLeader = new(), RecordBytes = bytes.WrittenMemory,
                AcquiredRecords = [new ShareFetchAcquiredRecords
                {
                    FirstOffset = 0, LastOffset = _recordsPerPartition - 1, DeliveryCount = 1
                }]
            };
            metadataPartitions[partitionIndex] = new PartitionMetadata
            {
                PartitionIndex = partitionIndex, LeaderId = 1, ErrorCode = ErrorCode.None,
                ReplicaNodes = [1], IsrNodes = [1]
            };
            assignment.Add(new(Topic, partitionIndex));
        }
        var response = new ShareFetchResponse
        {
            ErrorCode = ErrorCode.None, NodeEndpoints = [],
            Responses = [new ShareFetchResponseTopic
            {
                TopicId = TopicId,
                Partitions = partitionResponses
            }]
        };
        var pool = new Pool(new Connection(response));
        var options = new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], GroupId = "poll-buffer-benchmark",
            AcknowledgementMode = ShareAcknowledgementMode.Explicit,
            MaxPollRecords = Overflow ? 16 : _recordCount
        };
        _windowSize = options.MaxPollRecords;
        _metadata = new MetadataManager(pool, options.BootstrapServers);
        _metadata.Metadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
            Topics = [new TopicMetadata
            {
                Name = Topic, TopicId = TopicId, ErrorCode = ErrorCode.None,
                Partitions = metadataPartitions
            }]
        });
        _consumer = new KafkaShareConsumer<int, ReadOnlyMemory<byte>>(options, Serializers.Int32,
            Prepared ? new ReadyBytesPreparer() : Serializers.RawBytes, pool, _metadata);
        var consumerType = _consumer.GetType();
        consumerType.GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_consumer, true);
        var coordinator = consumerType.GetField("_coordinator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_consumer)!;
        _updateAssignment = typeof(ShareConsumerCoordinator)
            .GetMethod("ProcessShareGroupAssignment", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Action<ShareGroupHeartbeatAssignment>>(coordinator);
        _heartbeatAssignment = new ShareGroupHeartbeatAssignment
        {
            TopicPartitions = [new ShareGroupHeartbeatTopicPartitions
            {
                TopicId = TopicId, Partitions = Enumerable.Range(0, PartitionCount).ToArray()
            }]
        };
        typeof(ShareConsumerCoordinator).GetField("_assignedPartitions", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(coordinator, assignment);
        typeof(ShareConsumerCoordinator).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(coordinator, CoordinatorState.Stable);
        _consumer.Subscribe(Topic);
        if (RenewalBuffering)
        {
            // An acknowledged renewal makes polling reserve fresh records before replay.
            // Each response fills the window, so the retained record is never replayed.
            // Its offset is outside the fresh range, keeping this path active throughout.
            _consumer.Acknowledge(new ShareConsumeResult<int, ReadOnlyMemory<byte>>
            {
                Topic = Topic, Partition = 0, Offset = _recordsPerPartition, Value = ReadOnlyMemory<byte>.Empty, DeliveryCount = 1
            }, AcknowledgeType.Renew);
            var tracker = (AcknowledgementTracker)consumerType.GetField("_ackTracker", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(_consumer)!;
            consumerType.GetMethod("ApplySuccessfulAcknowledgements", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_consumer, [tracker.Flush(), 0L]);
        }

        var count = 0;
        await foreach (var record in _consumer.PollAsync())
        {
            if (record.Offset != count % _recordsPerPartition || record.Partition != count / _recordsPerPartition ||
                record.Value.Length != payload.Length)
                throw new InvalidOperationException("Classic poll did not preserve the acquired record sequence.");
            if (++count == _windowSize) break;
        }
        if (RenewalBuffering && consumerType.GetField("_renewedRecords", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(_consumer) is not System.Collections.IDictionary { Count: 1 })
            throw new InvalidOperationException("The renewal-buffering benchmark must retain its acknowledged renewal.");
    }

    [Benchmark]
    public async ValueTask<long> PollWindow()
    {
        var count = 0;
        long checksum = 0;
        await foreach (var record in _consumer.PollAsync())
        {
            checksum += record.Offset + record.Value.Length;
            if (++count == _windowSize) break;
        }
        return checksum;
    }

    internal void RefreshAssignment() => _updateAssignment(_heartbeatAssignment);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _consumer.DisposeAsync();
        await _metadata.DisposeAsync();
    }

    private sealed class ReadyBytesPreparer : IDeserializer<ReadOnlyMemory<byte>>, IAsyncDeserializerPreparer<ReadOnlyMemory<byte>>
    {
        public ReadOnlyMemory<byte> Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) => data;
        public bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out ReadOnlyMemory<byte> value)
        {
            value = data;
            return true;
        }
        public ValueTask PrepareAsync(ReadOnlyMemory<byte> data, SerializationContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Benchmark deserializer is already prepared.");
    }

    private sealed class Connection(ShareFetchResponse response) : IKafkaConnection, IKafkaCapabilityProvider
    {
        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public KafkaConnectionCapabilities Capabilities { get; } = KafkaConnectionCapabilities.Create(new ApiVersionsResponse
        {
            ErrorCode = ErrorCode.None, ApiKeys = [new ApiVersion(ApiKey.ShareFetch, 0, 2)]
        });
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => request switch
            {
                ShareFetchRequest => ValueTask.FromResult((TResponse)(object)response),
                _ => throw new NotSupportedException()
            };
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

    private sealed class Pool(Connection connection) : IConnectionPool
    {
        public ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken token = default) => ValueTask.FromResult<IKafkaConnection>(connection);
        public ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken token = default) => ValueTask.FromResult<IKafkaConnection>(connection);
        public ValueTask<IKafkaConnection> GetConnectionByIndexAsync(int brokerId, int index, CancellationToken token = default) => ValueTask.FromResult<IKafkaConnection>(connection);
        public void RegisterBroker(int id, string host, int port) { }
        public ValueTask<int> ScaleConnectionGroupAsync(int id, int count, CancellationToken token = default) => ValueTask.FromResult(1);
        public ValueTask<IKafkaConnection?> ShrinkConnectionGroupAsync(int id, int count, CancellationToken token = default) => ValueTask.FromResult<IKafkaConnection?>(null);
        public ValueTask RemoveConnectionAsync(int id) => ValueTask.CompletedTask;
        public ValueTask CloseAllAsync() => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
