using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures complete fetch-response handling with synchronous, pooled transport responses.</summary>
[MemoryDiagnoser]
public class ConsumerFollowerOffsetRetryBenchmarks
{
    private const string Topic = "replica-benchmark";
    private static readonly TopicPartition Partition = new(Topic, 0);
    private readonly List<TopicPartition> _partitions = [Partition];
    private KafkaConsumer<byte[], byte[]> _consumer = null!;
    private BenchmarkConnection _leader = null!;
    private BenchmarkConnection _follower = null!;
    private ConcurrentDictionary<TopicPartition, long> _positions = null!;
    private Action<TopicPartition, long, bool> _setPosition = null!;
    private Func<int, List<TopicPartition>, int, CancellationToken, ValueTask<List<PendingFetchData>?>> _fetch = null!;
    private BufferedConsumerHarness.PrefetchResponseHandler _prefetch = null!;
    private int _epoch;
    private long _expectedFollowerPosition;

    [Params(false, true)]
    public bool Prefetch { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _leader = new BenchmarkConnection(1, "leader", 9092);
        _follower = new BenchmarkConnection(2, "follower", 9093);
        var pool = new BenchmarkPool(_leader, _follower);
        var metadata = new MetadataManager(pool, ["localhost:9092"]);
        metadata.SetApiVersion(ApiKey.Fetch, FetchRequest.LowestSupportedVersion, FetchRequest.HighestSupportedVersion);
        metadata.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "leader", Port = 9092 },
                new BrokerMetadata { NodeId = 2, Host = "follower", Port = 9093 }
            ],
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = Topic,
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 0, LeaderId = 1, LeaderEpoch = 5,
                            ReplicaNodes = [1, 2], IsrNodes = [1, 2]
                        }
                    ]
                }
            ]
        });
        _consumer = new KafkaConsumer<byte[], byte[]>(new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], ClientRack = "rack-a",
            EnableFetchSessions = false, AutoOffsetReset = AutoOffsetReset.Latest
        }, Serializers.ByteArray, Serializers.ByteArray, pool, metadata);
        _consumer.Assign(Partition);
        var preservesFollowerPosition = BufferedConsumerHarness.BindFollowerFetchHandlers(
            _consumer, out _fetch, out _prefetch, out _setPosition);
        _positions = BufferedConsumerHarness.GetFetchPositions(_consumer);
        _epoch = (int)BufferedConsumerHarness.GetPrivateField(_consumer, "_fetchBufferEpoch")!;
        // Main resets follower errors to Latest; the fix preserves the requested offset.
        // Validate each declared contract rather than learning expectations from output.
        _expectedFollowerPosition = preservesFollowerPosition ? 42 : -1;
        ValidateScenarios();
    }

    [Benchmark]
    public long SuccessfulFetch() => Run(1, ErrorCode.None);

    [Benchmark]
    public long FollowerError() => Run(2, ErrorCode.OffsetOutOfRange);

    // Measure two response handlers with the same explicit position restoration on
    // both revisions. This isolates handler cost; it does not measure retry scheduling
    // or baseline offset lookup after its known incorrect follower reset.
    [Benchmark]
    public long FollowerErrorThenLeaderSuccess()
    {
        Run(2, ErrorCode.OffsetOutOfRange);
        _positions[Partition] = 42;
        _setPosition(Partition, 42, false);
        Fetch(1);
        return _positions[Partition];
    }

    [Benchmark]
    public long LeaderError() => Run(1, ErrorCode.OffsetOutOfRange);

    [Benchmark]
    public void ResponsePoolControl() => _leader.CreateResponse().ReturnToPool();

    private long Run(int brokerId, ErrorCode error)
    {
        _positions[Partition] = 42;
        _setPosition(Partition, 42, false);
        _leader.Error = brokerId == 1 ? error : ErrorCode.None;
        _follower.Error = brokerId == 2 ? error : ErrorCode.None;
        Fetch(brokerId);
        return _positions[Partition];
    }

    private void Fetch(int brokerId)
    {
        if (Prefetch)
            _prefetch(brokerId, _partitions, 0, 1, 0, _epoch, default).GetAwaiter().GetResult();
        else
        {
            var pending = _fetch(brokerId, _partitions, _epoch, default).GetAwaiter().GetResult();
            if (pending is not null)
                throw new InvalidOperationException("The empty-response fixture unexpectedly queued records.");
        }
    }

    private void ValidateScenarios()
    {
        if (SuccessfulFetch() != 42 || _leader.SendCount != 1 || _follower.SendCount != 0)
            throw new InvalidOperationException("SuccessfulFetch must request only the leader and preserve the position.");
        if (FollowerError() != _expectedFollowerPosition || _leader.SendCount != 1 || _follower.SendCount != 1)
            throw new InvalidOperationException("FollowerError must match the revision's declared position policy without an inline retry.");
        if (FollowerErrorThenLeaderSuccess() != 42 || _leader.SendCount != 2 || _follower.SendCount != 2
            || _leader.LastFetchOffset != 42 || _follower.LastFetchOffset != 42)
            throw new InvalidOperationException("The controlled retry must request the leader at the explicitly restored position.");
        if (LeaderError() != -1 || _leader.SendCount != 3 || _follower.SendCount != 2)
            throw new InvalidOperationException("Only the leader error may reset the position to Latest.");
        _leader.Error = ErrorCode.None;
        _follower.Error = ErrorCode.None;
    }

    [GlobalCleanup]
    public ValueTask Cleanup() => _consumer.DisposeAsync();

    private sealed class BenchmarkConnection(int brokerId, string host, int port) : IKafkaConnection
    {
        private readonly FetchResponseTopic[] _topics = new FetchResponseTopic[1];
        private readonly FetchResponsePartition[] _partitions = new FetchResponsePartition[1];
        internal ErrorCode Error;
        internal long SendCount;
        internal long LastFetchOffset;
        public int BrokerId => brokerId;
        public string Host => host;
        public int Port => port;
        public bool IsConnected => true;

        internal FetchResponse CreateResponse()
        {
            var partition = FetchResponsePartition.Rent();
            partition.PartitionIndex = 0;
            partition.ErrorCode = Error;
            partition.HighWatermark = 42;
            partition.LastStableOffset = 42;
            partition.LogStartOffset = 0;
            _partitions[0] = partition;
            var topic = FetchResponseTopic.Rent();
            topic.Topic = Topic;
            topic.Partitions = _partitions;
            _topics[0] = topic;
            var response = FetchResponse.Rent();
            response.Responses = _topics;
            return response;
        }

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse
        {
            if (request is not FetchRequest fetch)
                throw new InvalidOperationException("The fixture supports only fetch requests.");
            SendCount++;
            LastFetchOffset = fetch.Topics[0].Partitions[0].FetchOffset;
            return new((TResponse)(object)CreateResponse());
        }
        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => default;
        public ValueTask DisposeAsync() => default;
    }

    private sealed class BenchmarkPool(BenchmarkConnection leader, BenchmarkConnection follower) : IConnectionPool
    {
        public ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken cancellationToken = default) => new(GetConnection(brokerId));
        public ValueTask<IKafkaConnection> GetConnectionByIndexAsync(int brokerId, int index, CancellationToken cancellationToken = default) => new(GetConnection(brokerId));
        public ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            if (host == leader.Host && port == leader.Port)
                return new(leader);
            if (host == follower.Host && port == follower.Port)
                return new(follower);
            throw new InvalidOperationException("Unexpected broker endpoint.");
        }
        private IKafkaConnection GetConnection(int brokerId) => brokerId switch
        {
            1 => leader,
            2 => follower,
            _ => throw new InvalidOperationException("Unexpected broker ID.")
        };
        public void RegisterBroker(int brokerId, string host, int port) { }
        public ValueTask<int> ScaleConnectionGroupAsync(int brokerId, int newCount, CancellationToken cancellationToken = default) => new(1);
        public ValueTask<IKafkaConnection?> ShrinkConnectionGroupAsync(int brokerId, int newCount, CancellationToken cancellationToken = default) => new((IKafkaConnection?)null);
        public ValueTask RemoveConnectionAsync(int brokerId) => default;
        public ValueTask CloseAllAsync() => default;
        public ValueTask DisposeAsync() => default;
    }
}
