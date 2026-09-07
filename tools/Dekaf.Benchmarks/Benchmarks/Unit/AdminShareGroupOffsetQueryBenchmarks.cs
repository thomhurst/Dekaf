using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Completed read-only admin queries over identical synchronous transport fixtures.</summary>
[MemoryDiagnoser]
public class AdminShareGroupOffsetQueryBenchmarks
{
    [Params(1, 32)] public int Groups { get; set; }
    [Params((short)0, (short)1)] public short Version { get; set; }
    private AdminClient _admin = null!;
    private MetadataManager _metadata = null!;
    private Dictionary<string, ListShareGroupOffsetsSpec> _specs = null!;

    [GlobalSetup]
    public void Setup()
    {
        var metadata = new MetadataResponse
        {
            Brokers = [new() { NodeId = 1, Host = "localhost", Port = 9092 }],
            ControllerId = 1, ClusterId = "fixture", Topics = []
        };
        var groups = new DescribeShareGroupOffsetsResponseGroup[Groups];
        var coordinators = new Coordinator[Groups];
        var individual = new Dictionary<string, DescribeShareGroupOffsetsResponse>(Groups, StringComparer.Ordinal);
        _specs = new(Groups, StringComparer.Ordinal);
        for (var index = 0; index < Groups; index++)
        {
            var groupId = $"group-{index}";
            _specs.Add(groupId, new());
            coordinators[index] = new() { Key = groupId, NodeId = 1, Host = "localhost", Port = 9092 };
            groups[index] = new()
            {
                GroupId = groupId, Topics = [new() { TopicName = "input", Partitions =
                [new() { PartitionIndex = 0, StartOffset = 42, LeaderEpoch = 7, Lag = Version == 0 ? -1 : 9 }] }]
            };
            individual.Add(groupId, new() { Groups = [groups[index]] });
        }
        var connection = new Connection(metadata, new() { Groups = groups }, individual, new() { Coordinators = coordinators });
        var pool = new Pool(connection);
        _metadata = new MetadataManager(pool, ["localhost:9092"]);
        _metadata.Metadata.Update(metadata);
        _metadata.SetApiVersion(ApiKey.Metadata, 9, 13);
        _metadata.SetApiVersion(ApiKey.FindCoordinator, 4, 6);
        _metadata.SetApiVersion(ApiKey.DescribeShareGroupOffsets, 0, Version);
        _admin = new(new() { BootstrapServers = ["localhost:9092"] }, pool, _metadata);
        var results = _admin.ListShareGroupOffsetsAsync(_specs).GetAwaiter().GetResult();
        foreach (var (groupId, result) in results)
        {
            var single = _admin.DescribeShareGroupOffsetsAsync(groupId).GetAwaiter().GetResult();
            var offset = result.Offsets[new("input", 0)];
            if (result.ErrorCode != ErrorCode.None || single.Count != 1 || offset.StartOffset != 42 ||
                single[0].StartOffset != offset.StartOffset || offset.LeaderEpoch != 7 || offset.Lag != (Version == 0 ? -1 : 9))
                throw new InvalidOperationException("The single and batched fixtures must query identical offsets.");
        }
        if (results.Count != Groups || Batch().GetAwaiter().GetResult() != Groups || Individual().GetAwaiter().GetResult() != Groups)
            throw new InvalidOperationException("Every requested group must complete.");
    }

    [Benchmark]
    public async ValueTask<int> Batch() => (await _admin.ListShareGroupOffsetsAsync(_specs)).Count;

    [Benchmark(Baseline = true)]
    public async ValueTask<int> Individual()
    {
        var completed = 0;
        foreach (var groupId in _specs.Keys)
            completed += (await _admin.DescribeShareGroupOffsetsAsync(groupId)).Count;
        return completed;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _admin.DisposeAsync().GetAwaiter().GetResult();
        _metadata.DisposeAsync().GetAwaiter().GetResult();
    }

    private sealed class Connection(MetadataResponse metadata, DescribeShareGroupOffsetsResponse batched,
        Dictionary<string, DescribeShareGroupOffsetsResponse> individual, FindCoordinatorResponse coordinator) : IKafkaConnection
    {
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
                DescribeShareGroupOffsetsRequest offsets => offsets.Groups.Count == 1
                    ? individual[offsets.Groups[0].GroupId] : batched,
                MetadataRequest => metadata,
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
