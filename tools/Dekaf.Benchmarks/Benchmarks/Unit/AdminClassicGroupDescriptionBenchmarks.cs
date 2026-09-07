using System.Buffers;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Administrative operation costs with cached responses; no producer/consumer message delivery is measured.</summary>
[MemoryDiagnoser]
public class AdminClassicGroupDescriptionBenchmarks
{
    [Params(1, 16)]
    public int Groups { get; set; }
    [Params(false, true)]
    public bool MixedOutcomes { get; set; }
    private AdminClient _admin = null!;
    private MetadataManager _metadata = null!;
    private string[] _groups = null!;
    private readonly DescribeClassicGroupsOptions _options = new() { IncludeAuthorizedOperations = true };

    [GlobalSetup]
    public async Task Setup()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16(0);
        writer.WriteInt32(1);
        writer.WriteString("orders");
        writer.WriteInt32(1);
        writer.WriteInt32(0);
        writer.WriteBytes([]);
        var assignment = buffer.WrittenMemory.ToArray();
        var groups = new DescribeGroupsResponseGroup[Groups];
        _groups = new string[Groups];
        var coordinators = new Dictionary<string, FindCoordinatorResponse>(Groups, StringComparer.Ordinal);
        for (var i = 0; i < Groups; i++)
        {
            var id = $"group-{i}";
            _groups[i] = id;
            coordinators.Add(id, new() { Coordinators = [new Coordinator { Key = id, NodeId = 1, Host = "localhost", Port = 9092 }] });
            groups[i] = new()
            {
                GroupId = id, GroupState = "Stable", ProtocolType = MixedOutcomes ? "connect" : "consumer", ProtocolData = "range",
                ErrorCode = MixedOutcomes && i == Groups - 1 ? ErrorCode.GroupAuthorizationFailed : ErrorCode.None,
                AuthorizedOperations = 123,
                Members = [new DescribeGroupsResponseMember
                {
                    MemberId = id, ClientId = "client", ClientHost = "host", MemberMetadata = assignment, MemberAssignment = assignment
                }]
            };
        }
        var pool = new Pool(new Connection(coordinators, new() { Groups = groups }));
        _metadata = new MetadataManager(pool, ["localhost:9092"]);
        _metadata.Metadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
            ControllerId = 1, ClusterId = "fixture", Topics = []
        });
        _metadata.SetApiVersion(ApiKey.FindCoordinator, 4, 4);
        _metadata.SetApiVersion(ApiKey.DescribeGroups, 5, 5);
        _admin = new AdminClient(new AdminClientOptions { BootstrapServers = ["localhost:9092"] }, pool, _metadata);
        var results = await Describe();
        if (results.Count != Groups)
            throw new InvalidOperationException("A result is required for every group.");
        for (var i = 0; i < Groups; i++)
        {
            var result = results[_groups[i]];
            var denied = MixedOutcomes && i == Groups - 1;
            if (result.ErrorCode != (denied ? ErrorCode.GroupAuthorizationFailed : ErrorCode.None) ||
                (!denied && (result.Description?.Members.Count != 1 ||
                    (result.Description.Members[0].Assignment is not null) == MixedOutcomes)))
                throw new InvalidOperationException("Invalid protocol mapping or per-group outcome.");
        }
        // Elapsed workload warmup is explicit in every fresh benchmark process.
        // This minimum does not itself prove steady state; acceptance also needs runtime time series.
        var warmup = Stopwatch.StartNew();
        long completed = 0;
        while (warmup.Elapsed < TimeSpan.FromSeconds(20))
        {
            await Describe();
            completed++;
        }
        Console.WriteLine($"Admin warmup: {warmup.Elapsed.TotalSeconds:F3} s; {completed} completed operations");
    }

    [Benchmark]
    public ValueTask<IReadOnlyDictionary<string, ClassicGroupDescriptionResult>> Describe() =>
        _admin.DescribeClassicGroupsAsync(_groups, _options);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _admin.DisposeAsync();
        await _metadata.DisposeAsync();
    }

    private sealed class Connection(Dictionary<string, FindCoordinatorResponse> coordinators, DescribeGroupsResponse descriptions) : IKafkaConnection
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
                FindCoordinatorRequest find => coordinators[find.Key!],
                DescribeGroupsRequest => descriptions,
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
