using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Steady-state administrative request construction and detailed outcome mapping.</summary>
[MemoryDiagnoser]
public class AdminDetailedShareGroupOffsetBenchmarks
{
    [Params(1, 16)]
    public int Count { get; set; }

    [Params(false, true)]
    public bool Delete { get; set; }

    private AdminClient _admin = null!;
    private MetadataManager _metadata = null!;
    private string[] _topics = null!;
    private ShareGroupOffsetAlteration[] _offsets = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _topics = Enumerable.Range(0, Count).Select(static index => $"topic-{index}").ToArray();
        _offsets = _topics.Select(static topic => new ShareGroupOffsetAlteration { TopicPartition = new(topic, 0), StartOffset = 42 }).ToArray();
        var metadata = new MetadataResponse { Brokers = [new() { NodeId = 1, Host = "localhost", Port = 9092 }], ControllerId = 1, Topics = [] };
        var connection = new Connection(metadata,
            new() { Responses = _topics.Select(static topic => new AlterShareGroupOffsetsResponseTopic { TopicName = topic, Partitions = [new() { PartitionIndex = 0 }] }).ToArray() },
            new() { Responses = _topics.Select(static topic => new DeleteShareGroupOffsetsResponseTopic { TopicName = topic }).ToArray() });
        var pool = new Pool(connection);
        _metadata = new MetadataManager(pool, ["localhost:9092"]);
        _metadata.Metadata.Update(metadata);
        _metadata.SetApiVersion(ApiKey.Metadata, 9, 13);
        _metadata.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
        _metadata.SetApiVersion(ApiKey.AlterShareGroupOffsets, 0, 0);
        _metadata.SetApiVersion(ApiKey.DeleteShareGroupOffsets, 0, 0);
        _admin = new AdminClient(new() { BootstrapServers = ["localhost:9092"] }, pool, _metadata);
        var altered = await _admin.AlterShareGroupOffsetsDetailedAsync("group", _offsets);
        var deleted = await _admin.DeleteShareGroupOffsetsDetailedAsync("group", _topics);
        if (altered.Count != Count || deleted.Count != Count ||
            altered.Values.Any(static result => !result.IsSuccess) || deleted.Values.Any(static result => !result.IsSuccess))
            throw new InvalidOperationException("The fixture did not return every confirmed result.");
    }

    // Costs are per administrative operation, with Count entity results; no message-path work.
    [Benchmark]
    public async ValueTask<int> Mutate() => Delete
        ? (await _admin.DeleteShareGroupOffsetsDetailedAsync("group", _topics)).Count
        : (await _admin.AlterShareGroupOffsetsDetailedAsync("group", _offsets)).Count;

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _admin.DisposeAsync();
        await _metadata.DisposeAsync();
    }

    private sealed class Connection(MetadataResponse metadata, AlterShareGroupOffsetsResponse alter,
        DeleteShareGroupOffsetsResponse delete) : IKafkaConnection
    {
        private readonly FindCoordinatorResponse _coordinator = new() { Coordinators = [new() { Key = "group", NodeId = 1, Host = "localhost", Port = 9092 }] };
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
                FindCoordinatorRequest => _coordinator,
                AlterShareGroupOffsetsRequest => alter,
                DeleteShareGroupOffsetsRequest => delete,
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
