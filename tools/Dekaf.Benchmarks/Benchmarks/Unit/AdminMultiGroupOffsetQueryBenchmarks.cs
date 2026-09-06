using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Cached wire responses isolate multi-group OffsetFetch conversion and one unstable retry.</summary>
[MemoryDiagnoser]
public class AdminMultiGroupOffsetQueryBenchmarks
{
    [Params(1, 16)]
    public int Groups { get; set; }

    [Params(false, true)]
    public bool UnstableFirstResponse { get; set; }

    private AdminClient _admin = null!;
    private MetadataManager _metadata = null!;
    private Connection _connection = null!;
    private Dictionary<string, ListConsumerGroupOffsetsSpec> _specs = null!;
    private readonly ListConsumerGroupOffsetsOptions _stable = new() { RequireStable = true };

    [GlobalSetup]
    public void Setup()
    {
        var metadata = new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
            ControllerId = 1, ClusterId = "fixture", Topics = []
        };
        var stableGroups = new OffsetFetchResponseGroup[Groups];
        var unstableGroups = new OffsetFetchResponseGroup[Groups];
        var coordinators = new Coordinator[Groups];
        _specs = new(Groups, StringComparer.Ordinal);
        for (var i = 0; i < Groups; i++)
        {
            var groupId = $"group-{i}";
            _specs.Add(groupId, new());
            coordinators[i] = new() { Key = groupId, NodeId = 1, Host = "localhost", Port = 9092, ErrorCode = ErrorCode.None };
            stableGroups[i] = new()
            {
                GroupId = groupId, ErrorCode = ErrorCode.None,
                Topics = [new OffsetFetchResponseTopic
                {
                    Name = "orders",
                    Partitions = [new OffsetFetchResponsePartition
                    {
                        PartitionIndex = 0, CommittedOffset = 42, CommittedLeaderEpoch = 7,
                        Metadata = "checkpoint", ErrorCode = ErrorCode.None
                    }]
                }]
            };
            unstableGroups[i] = new() { GroupId = groupId, ErrorCode = ErrorCode.UnstableOffsetCommit, Topics = [] };
        }
        _connection = new(metadata, new() { Groups = stableGroups }, new() { Groups = unstableGroups },
            new() { Coordinators = coordinators });
        var pool = new Pool(_connection);
        _metadata = new MetadataManager(pool, ["localhost:9092"]);
        _metadata.Metadata.Update(metadata);
        _metadata.SetApiVersion(ApiKey.Metadata, 9, 13);
        _metadata.SetApiVersion(ApiKey.FindCoordinator, 4, 6);
        _metadata.SetApiVersion(ApiKey.OffsetFetch, 6, 9);
        _admin = new AdminClient(new AdminClientOptions
        {
            BootstrapServers = ["localhost:9092"], RetryBackoffMs = 1
        }, pool, _metadata);
        // Populate coordinator caches before measurement, then verify the chosen response shape.
        _admin.ListConsumerGroupOffsetsAsync(_specs, _stable).GetAwaiter().GetResult();
        var results = Query().GetAwaiter().GetResult();
        if (results.Count != Groups || (UnstableFirstResponse && _connection.Fetches != 2))
            throw new InvalidOperationException("Expected all groups and exactly one unstable retry.");
        foreach (var result in results.Values)
        {
            if (result.ErrorCode != ErrorCode.None || result.Offsets[new("orders", 0)].Offset?.Offset != 42)
                throw new InvalidOperationException("Expected committed checkpoint after stability polling.");
        }
    }

    [Benchmark]
    public ValueTask<IReadOnlyDictionary<string, ConsumerGroupOffsetsResult>> Query()
    {
        _connection.ReturnUnstable = UnstableFirstResponse;
        _connection.Fetches = 0;
        _connection.MetadataRefreshes = 0;
        return _admin.ListConsumerGroupOffsetsAsync(_specs, _stable);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _admin.DisposeAsync().GetAwaiter().GetResult();
        _metadata.DisposeAsync().GetAwaiter().GetResult();
    }

    private sealed class Connection(MetadataResponse metadata, OffsetFetchResponse stable, OffsetFetchResponse unstable, FindCoordinatorResponse coordinator) : IKafkaConnection
    {
        public bool ReturnUnstable { get; set; }
        public int Fetches { get; set; }
        public int MetadataRefreshes { get; set; }

        private OffsetFetchResponse NextOffsets()
        {
            Fetches++;
            return ReturnUnstable && Fetches == 1 ? unstable : stable;
        }
        private MetadataResponse RefreshMetadata()
        {
            MetadataRefreshes++;
            return metadata;
        }
        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse
        {
            IKafkaResponse response = request switch
            {
                FindCoordinatorRequest => coordinator,
                OffsetFetchRequest => NextOffsets(),
                MetadataRequest => RefreshMetadata(),
                _ => throw new NotSupportedException(typeof(TRequest).Name)
            };
            return ValueTask.FromResult((TResponse)response);
        }
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
