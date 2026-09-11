using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Steady-state config snapshots, endpoint grouping, request creation and result correlation.</summary>
[MemoryDiagnoser]
public class AdminDetailedConfigBenchmarks
{
    [Params("topic-1", "topic-32", "mixed-32", "routed-3")]
    public string Scenario { get; set; } = "topic-1";

    [Params(false, true)]
    public bool Incremental { get; set; }

    private AdminClient _admin = null!;
    private Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>> _replacement = null!;
    private Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>> _incremental = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        ConfigResource[] resources = Scenario == "routed-3"
            ? [ConfigResource.Topic("orders"), ConfigResource.Broker(2), ConfigResource.BrokerLogger(3)]
            : Enumerable.Range(0, Scenario == "topic-1" ? 1 : 32).Select(index => ConfigResource.Topic($"topic-{index}")).ToArray();
        _replacement = resources.ToDictionary(static resource => resource, static _ => (IReadOnlyList<ConfigEntry>)[new() { Name = "retention.ms", Value = "1000" }]);
        _incremental = resources.ToDictionary(static resource => resource, static _ => (IReadOnlyList<ConfigAlter>)[ConfigAlter.Set("retention.ms", "1000")]);
        var metadata = new MetadataResponse { Brokers = [new() { NodeId = 1, Host = "localhost", Port = 9092 },
            new() { NodeId = 2, Host = "localhost", Port = 9093 }, new() { NodeId = 3, Host = "localhost", Port = 9094 }], ControllerId = 1, Topics = [] };
        var connections = new Dictionary<int, Connection>();
        foreach (var id in new[] { 1, 2, 3 })
        {
            var scoped = id == 1 ? resources : System.Array.Empty<ConfigResource>();
            if (Scenario == "routed-3") scoped = [resources[id - 1]];
            connections.Add(id, new Connection(id, metadata,
                new() { Responses = scoped.Select(resource => new AlterConfigsResourceResponse
                {
                    ResourceType = (sbyte)resource.Type, ResourceName = resource.Name,
                    ErrorCode = Scenario == "mixed-32" && resource.Equals(resources[^1]) ? ErrorCode.TopicAuthorizationFailed : ErrorCode.None
                }).ToArray() },
                new() { Responses = scoped.Select(resource => new IncrementalAlterConfigsResourceResponse
                {
                    ResourceType = (sbyte)resource.Type, ResourceName = resource.Name,
                    ErrorCode = Scenario == "mixed-32" && resource.Equals(resources[^1]) ? ErrorCode.TopicAuthorizationFailed : ErrorCode.None
                }).ToArray() }));
        }
        var pool = new Pool(connections);
        var manager = new MetadataManager(pool, ["localhost:9092"]);
        manager.Metadata.Update(metadata);
        manager.SetApiVersion(ApiKey.Metadata, 9, 13);
        manager.SetApiVersion(ApiKey.AlterConfigs, 0, 2);
        manager.SetApiVersion(ApiKey.IncrementalAlterConfigs, 0, 1);
        _admin = new AdminClient(new AdminClientOptions { BootstrapServers = ["localhost:9092"] }, pool, manager, ownsResources: true);
        var result = await Alter();
        if (result.Count != resources.Length || result[resources[^1]].IsSuccess == (Scenario == "mixed-32"))
            throw new InvalidOperationException("Incorrect configuration benchmark outcomes.");
    }

    [Benchmark]
    public ValueTask<IReadOnlyDictionary<ConfigResource, AdminMutationResult>> Alter() => Incremental
        ? _admin.IncrementalAlterConfigsDetailedAsync(_incremental)
        : _admin.AlterConfigsDetailedAsync(_replacement);

    [GlobalCleanup]
    public async Task Cleanup() => await _admin.DisposeAsync();

    private sealed class Connection(int id, MetadataResponse metadata, AlterConfigsResponse replacement, IncrementalAlterConfigsResponse incremental) : IKafkaConnection
    {
        public int BrokerId => id;
        public string Host => "localhost";
        public int Port => 9091 + id;
        public bool IsConnected => true;
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => request switch
            {
                MetadataRequest => ValueTask.FromResult((TResponse)(object)metadata),
                AlterConfigsRequest => ValueTask.FromResult((TResponse)(object)replacement),
                IncrementalAlterConfigsRequest => ValueTask.FromResult((TResponse)(object)incremental),
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

    private sealed class Pool(Dictionary<int, Connection> connections) : IConnectionPool
    {
        public ValueTask<IKafkaConnection> GetConnectionAsync(int id, CancellationToken token = default) => ValueTask.FromResult<IKafkaConnection>(connections[id]);
        public ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken token = default) => ValueTask.FromResult<IKafkaConnection>(connections[port - 9091]);
        public ValueTask<IKafkaConnection> GetConnectionByIndexAsync(int id, int index, CancellationToken token = default) => ValueTask.FromResult<IKafkaConnection>(connections[id]);
        public void RegisterBroker(int id, string host, int port) { }
        public ValueTask<int> ScaleConnectionGroupAsync(int id, int count, CancellationToken token = default) => ValueTask.FromResult(1);
        public ValueTask<IKafkaConnection?> ShrinkConnectionGroupAsync(int id, int count, CancellationToken token = default) => ValueTask.FromResult<IKafkaConnection?>(null);
        public ValueTask RemoveConnectionAsync(int id) => ValueTask.CompletedTask;
        public ValueTask CloseAllAsync() => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
