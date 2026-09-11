using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Steady-state administrative ACL mapping and SCRAM grouping/derivation costs.</summary>
[MemoryDiagnoser]
public class AdminDetailedSecurityBenchmarks
{
    [Params("acl-1", "acl-32", "acl-mixed-32", "scram-delete-1", "scram-delete-32", "scram-upsert-1")]
    public string Scenario { get; set; } = "acl-1";

    private AdminClient _admin = null!;
    private AclBinding[] _bindings = null!;
    private UserScramCredentialAlteration[] _alterations = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var count = Scenario.EndsWith("32", StringComparison.Ordinal) ? 32 : 1;
        var names = Enumerable.Range(0, count).Select(index => $"entity-{index}").ToArray();
        _bindings = names.Select(name => AclBinding.Allow(ResourcePattern.Topic(name), "User:fixture", AclOperation.Read)).ToArray();
        _alterations = names.Select(name => Scenario == "scram-upsert-1"
            ? (UserScramCredentialAlteration)new UserScramCredentialUpsertion
            { User = name, Mechanism = ScramMechanism.ScramSha256, Iterations = 4096, Password = "benchmark-fixture", Salt = new byte[32] }
            : new UserScramCredentialDeletion { User = name, Mechanism = ScramMechanism.ScramSha256 }).ToArray();
        var metadata = new MetadataResponse
        { Brokers = [new() { NodeId = 1, Host = "localhost", Port = 9092 }], ControllerId = 1, ClusterId = "fixture", Topics = [] };
        var connection = new Connection(metadata,
            new() { Results = names.Select((_, index) => new AclCreationResult
            { ErrorCode = Scenario == "acl-mixed-32" && index == count - 1 ? ErrorCode.ClusterAuthorizationFailed : ErrorCode.None }).ToArray() },
            new() { Results = names.Select(name => new AlterUserScramCredentialsResult { User = name }).ToArray() });
        var pool = new Pool(connection);
        var manager = new MetadataManager(pool, ["localhost:9092"]);
        manager.Metadata.Update(metadata);
        manager.SetApiVersion(ApiKey.Metadata, 9, 13);
        manager.SetApiVersion(ApiKey.CreateAcls, 2, 3);
        manager.SetApiVersion(ApiKey.AlterUserScramCredentials, 0, 0);
        _admin = new AdminClient(new() { BootstrapServers = ["localhost:9092"] }, pool, manager, ownsResources: true);
        if (await Mutate() != count) throw new InvalidOperationException("Unexpected result count.");
        if (Scenario == "acl-mixed-32")
        {
            var results = await _admin.CreateAclsDetailedAsync(_bindings);
            if (!results[0].Result.IsSuccess || results[^1].Result.ErrorCode != ErrorCode.ClusterAuthorizationFailed)
                throw new InvalidOperationException("Mixed ACL results were lost.");
        }
    }

    [Benchmark]
    public async ValueTask<int> Mutate() => Scenario.StartsWith("acl", StringComparison.Ordinal)
        ? (await _admin.CreateAclsDetailedAsync(_bindings)).Count
        : (await _admin.AlterUserScramCredentialsDetailedAsync(_alterations)).Count;

    [GlobalCleanup]
    public async Task Cleanup() => await _admin.DisposeAsync();

    private sealed class Connection(MetadataResponse metadata, CreateAclsResponse acls, AlterUserScramCredentialsResponse scram) : IKafkaConnection
    {
        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse
        {
            IKafkaResponse response = request switch
            { MetadataRequest => metadata, CreateAclsRequest => acls, AlterUserScramCredentialsRequest => scram, _ => throw new NotSupportedException() };
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
