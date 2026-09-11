using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Steady-state per-call quota snapshots, request creation and complete-entity result correlation.</summary>
[MemoryDiagnoser]
public class AdminDetailedClientQuotaBenchmarks
{
    [Params(1, 32)]
    public int EntityCount { get; set; }

    [Params(false, true)]
    public bool Mixed { get; set; }

    private AdminClient _admin = null!;
    private ClientQuotaAlteration[] _alterations = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _alterations = Enumerable.Range(0, EntityCount).Select(index => ClientQuotaAlteration.Set(
            ClientQuotaEntity.For(ClientQuotaEntityComponent.User($"user-{index}"), ClientQuotaEntityComponent.ClientId(null)),
            "consumer_byte_rate", 4096)).ToArray();
        var metadata = new MetadataResponse
        {
            Brokers = [new() { NodeId = 1, Host = "localhost", Port = 9092 }],
            ControllerId = 1, Topics = []
        };
        var response = new AlterClientQuotasResponse
        {
            Entries = _alterations.Select((item, index) => new AlterClientQuotasResponseEntry
            {
                Entity = [new() { EntityType = "client-id", EntityName = null },
                    new() { EntityType = "user", EntityName = item.Entity.Components[0].Name }],
                ErrorCode = Mixed && index == EntityCount - 1 ? ErrorCode.ClusterAuthorizationFailed : ErrorCode.None,
                ErrorMessage = Mixed && index == EntityCount - 1 ? "denied" : null
            }).ToArray()
        };
        var pool = new Pool(new Connection(metadata, response));
        var manager = new MetadataManager(pool, ["localhost:9092"]);
        manager.Metadata.Update(metadata);
        manager.SetApiVersion(ApiKey.Metadata, 9, 13);
        manager.SetApiVersion(ApiKey.AlterClientQuotas, 0, 1);
        _admin = new AdminClient(new AdminClientOptions { BootstrapServers = ["localhost:9092"] }, pool, manager);
        var results = await Alter();
        if (results.Count != EntityCount || results[_alterations[^1].Entity].IsSuccess == Mixed)
            throw new InvalidOperationException("Quota benchmark returned incorrect outcomes.");
    }

    [Benchmark]
    public ValueTask<IReadOnlyDictionary<ClientQuotaEntity, AdminMutationResult>> Alter() =>
        _admin.AlterClientQuotasDetailedAsync(_alterations);

    [GlobalCleanup]
    public async Task Cleanup() => await _admin.DisposeAsync();

    private sealed class Connection(MetadataResponse metadata, AlterClientQuotasResponse response) : IKafkaConnection
    {
        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => request switch
            {
                MetadataRequest => ValueTask.FromResult((TResponse)(object)metadata),
                AlterClientQuotasRequest => ValueTask.FromResult((TResponse)(object)response),
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
