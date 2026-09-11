using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Steady-state administrative request construction and detailed outcome mapping.</summary>
[MemoryDiagnoser]
public class AdminDetailedConsumerGroupMutationBenchmarks
{
    [Params(1, 16)]
    public int Count { get; set; }

    [Params("alter", "alter-topic-ids", "delete", "groups")]
    public string Operation { get; set; } = "alter";

    private AdminClient _admin = null!;
    private MetadataManager _metadata = null!;
    private string[] _topics = null!;
    private TopicPartitionOffset[] _offsets = null!;
    private TopicPartition[] _partitions = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _topics = Enumerable.Range(0, Count).Select(static index => $"topic-{index}").ToArray();
        _offsets = _topics.Select(static topic => new TopicPartitionOffset(topic, 0, 42)).ToArray();
        _partitions = _topics.Select(static topic => new TopicPartition(topic, 0)).ToArray();
        var topicIds = _topics.ToDictionary(static topic => topic, static _ => Guid.NewGuid());
        var metadata = new MetadataResponse
        {
            Brokers = [new() { NodeId = 1, Host = "localhost", Port = 9092 }], ControllerId = 1,
            Topics = _topics.Select(topic => new TopicMetadata
            {
                Name = topic, TopicId = topicIds[topic], ErrorCode = ErrorCode.None, Partitions = []
            }).ToArray()
        };
        var connection = new Connection(metadata,
            new() { Topics = _topics.Select(topic => new OffsetCommitResponseTopic { Name = topic, TopicId = topicIds[topic], Partitions = [new() { PartitionIndex = 0, ErrorCode = ErrorCode.None }] }).ToArray() },
            new() { Topics = _topics.Select(static topic => new OffsetDeleteResponseTopic { Name = topic, Partitions = [new() { PartitionIndex = 0, ErrorCode = ErrorCode.None }] }).ToArray() },
            new() { Results = _topics.Select(static group => new DeleteGroupsResponseResult { GroupId = group }).ToArray() });
        var pool = new Pool(connection);
        _metadata = new MetadataManager(pool, ["localhost:9092"]);
        _metadata.Metadata.Update(metadata);
        _metadata.SetApiVersion(ApiKey.Metadata, 9, 13);
        _metadata.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
        var offsetCommitVersion = Operation == "alter-topic-ids" ? OffsetCommitRequest.TopicIdVersion : (short)8;
        _metadata.SetApiVersion(ApiKey.OffsetCommit, offsetCommitVersion, offsetCommitVersion);
        _metadata.SetApiVersion(ApiKey.OffsetDelete, 0, 0);
        _metadata.SetApiVersion(ApiKey.DeleteGroups, 2, 2);
        _admin = new AdminClient(new() { BootstrapServers = ["localhost:9092"] }, pool, _metadata);
        var altered = await _admin.AlterConsumerGroupOffsetsDetailedAsync("group", _offsets);
        var deleted = await _admin.DeleteConsumerGroupOffsetsDetailedAsync("group", _partitions);
        var groups = await _admin.DeleteConsumerGroupsDetailedAsync(_topics);
        if (altered.Count != Count || deleted.Count != Count || groups.Count != Count ||
            altered.Values.Any(static result => !result.IsSuccess) || deleted.Values.Any(static result => !result.IsSuccess) || groups.Values.Any(static result => !result.IsSuccess))
            throw new InvalidOperationException("The fixture did not return every confirmed result.");
    }

    // Costs are per administrative operation, with Count entity results; no message-path work.
    [Benchmark]
    public async ValueTask<int> Mutate() => Operation switch
    {
        "delete" => (await _admin.DeleteConsumerGroupOffsetsDetailedAsync("group", _partitions)).Count,
        "groups" => (await _admin.DeleteConsumerGroupsDetailedAsync(_topics)).Count,
        _ => (await _admin.AlterConsumerGroupOffsetsDetailedAsync("group", _offsets)).Count
    };

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _admin.DisposeAsync();
        await _metadata.DisposeAsync();
    }

    private sealed class Connection(MetadataResponse metadata, OffsetCommitResponse alter,
        OffsetDeleteResponse delete, DeleteGroupsResponse groups) : IKafkaConnection
    {
        private readonly Dictionary<string, FindCoordinatorResponse> _coordinators = CreateCoordinators(groups);

        private static Dictionary<string, FindCoordinatorResponse> CreateCoordinators(DeleteGroupsResponse groups)
        {
            var result = new Dictionary<string, FindCoordinatorResponse>(StringComparer.Ordinal);
            foreach (var group in groups.Results)
                result.Add(group.GroupId, Response(group.GroupId));
            result.Add("group", Response("group"));
            return result;

            static FindCoordinatorResponse Response(string groupId) => new()
            {
                Coordinators = [new() { Key = groupId, NodeId = 1, Host = "localhost", Port = 9092 }]
            };
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
                MetadataRequest => metadata,
                FindCoordinatorRequest coordinator => _coordinators[coordinator.Key],
                OffsetCommitRequest => alter,
                OffsetDeleteRequest => delete,
                DeleteGroupsRequest => groups,
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
