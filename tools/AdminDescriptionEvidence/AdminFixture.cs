using System.Buffers;
using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Testing;

namespace Dekaf.Benchmarks;

public sealed class AdminFixture : IAsyncDisposable
{
    private readonly string _mode;
    private readonly int _count;
    private readonly string[] _ids;
    private readonly TopicPartitionOffset[] _offsets = [new("orders", 0, 0)];
    private AdminClient _admin = null!;
    private MetadataManager _metadata = null!;
    private InMemoryAdminClient _inventory = null!;
    private Connection _connection = null!;
#if CANDIDATE
    private readonly DescribeClassicGroupsOptions _options = new() { IncludeAuthorizedOperations = true };
#endif

    public AdminFixture(string testCase)
    {
        var parts = testCase.Split(':');
        _mode = parts[0];
        _count = int.Parse(parts[1]);
        if (_count <= 0) throw new ArgumentOutOfRangeException(nameof(testCase));
        _ids = Enumerable.Range(0, _count).Select(i => $"group-{i}").ToArray();
    }

    public async Task InitializeAsync()
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
        var malformedBuffer = new ArrayBufferWriter<byte>();
        var malformedWriter = new KafkaProtocolWriter(malformedBuffer);
        malformedWriter.WriteInt16(0);
        malformedWriter.WriteInt32(-1);
        malformedWriter.WriteBytes([]);
        var malformed = malformedBuffer.WrittenMemory.ToArray();
        var groups = new DescribeGroupsResponseGroup[_count];
        var coordinatorResponses = new Dictionary<string, FindCoordinatorResponse>(StringComparer.Ordinal);
        for (var i = 0; i < _count; i++)
        {
            var id = _ids[i];
            coordinatorResponses.Add(id, new() { Coordinators = [new Coordinator { Key = id, NodeId = 1, Host = "localhost", Port = 9092 }] });
            groups[i] = new()
            {
                GroupId = id, GroupState = "Stable", ProtocolType = _mode == "mixed" ? "connect" : "consumer", ProtocolData = "range",
                ErrorCode = _mode == "mixed" && i == _count - 1 ? ErrorCode.GroupAuthorizationFailed : ErrorCode.None,
                AuthorizedOperations = 123,
                Members = [new DescribeGroupsResponseMember
                {
                    MemberId = id, ClientId = "client", ClientHost = "host", MemberMetadata = assignment, MemberAssignment = _mode == "malformed" ? malformed : assignment
                }]
            };
        }
        var retryGroups = (DescribeGroupsResponseGroup[])groups.Clone();
        retryGroups[^1] = new() { GroupId = _ids[^1], GroupState = "", Members = [], ErrorCode = ErrorCode.NotCoordinator };
        var metadata = new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
            ControllerId = 1, ClusterId = "fixture", Topics = []
        };
        _connection = new(coordinatorResponses, new() { Groups = groups }, new() { Groups = retryGroups }, metadata);
        var pool = new Pool(_connection);
        _metadata = new MetadataManager(pool, ["localhost:9092"]);
        _metadata.Metadata.Update(metadata);
        _metadata.SetApiVersion(ApiKey.FindCoordinator, 4, 4);
        _metadata.SetApiVersion(ApiKey.DescribeGroups, 5, 5);
        _metadata.SetApiVersion(ApiKey.Metadata, 9, 13);
        _admin = new AdminClient(new AdminClientOptions
        {
            BootstrapServers = ["localhost:9092"], RetryBackoffMs = 1, RetryBackoffMaxMs = 1
        }, pool, _metadata);
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        _inventory = new(cluster);
        foreach (var id in _ids)
            await _inventory.AlterConsumerGroupOffsetsAsync(id, [new TopicPartitionOffset("orders", 0, 0)]);
        if (await Call() != _count) throw new InvalidOperationException("Wrong result count.");
        if (_mode == "legacy")
        {
            var results = await _admin.DescribeConsumerGroupsAsync(_ids);
            foreach (var result in results.Values)
                if (result.ProtocolType != "consumer" || result.Members.Single().Assignment?.Count != 1)
                    throw new InvalidOperationException("Legacy consumer mapping differs.");
        }
        else if (_mode == "delete")
        {
            if ((await _inventory.ListGroupsAsync()).Count != 0) throw new InvalidOperationException("Group deletion left inventory behind.");
        }
        else if (_mode == "inventory")
        {
            var results = await _inventory.ListGroupsAsync();
            if (results.Any(group => group.GroupType != "classic" || group.ProtocolType != "" || group.State != "Empty"))
                throw new InvalidOperationException("Inventory mapping differs.");
        }
#if CANDIDATE
        else
        {
            _connection.RetryNext = _mode == "retry";
            var results = await _admin.DescribeClassicGroupsAsync(_ids, _options);
            for (var i = 0; i < _count; i++)
            {
                var result = results[_ids[i]];
                var denied = _mode == "mixed" && i == _count - 1;
                if (result.ErrorCode != (denied ? ErrorCode.GroupAuthorizationFailed : ErrorCode.None) ||
                    (!denied && (result.Description?.Members.Count != 1 ||
                        (result.Description.Members[0].Assignment is not null) != (_mode != "mixed" && _mode != "malformed"))))
                    throw new InvalidOperationException("Classic outcome or protocol mapping differs.");
            }
        }
#endif
    }

    public async ValueTask<int> Call()
    {
        switch (_mode)
        {
            case "legacy": return (await _admin.DescribeConsumerGroupsAsync(_ids)).Count;
            case "inventory": return (await _inventory.ListGroupsAsync()).Count;
            case "delete":
                foreach (var id in _ids) await _inventory.AlterConsumerGroupOffsetsAsync(id, _offsets);
                await _inventory.DeleteConsumerGroupsAsync(_ids);
                return _count;
#if CANDIDATE
            case "classic":
            case "mixed":
            case "malformed":
            case "retry":
                _connection.RetryNext = _mode == "retry";
                return (await _admin.DescribeClassicGroupsAsync(_ids, _options)).Count;
#endif
            default: throw new NotSupportedException(_mode);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _admin.DisposeAsync();
        await _metadata.DisposeAsync();
        await _inventory.DisposeAsync();
    }

    private sealed class Connection(Dictionary<string, FindCoordinatorResponse> coordinators,
        DescribeGroupsResponse descriptions, DescribeGroupsResponse retry, MetadataResponse metadata) : IKafkaConnection
    {
        private readonly DescribeGroupsResponse _retrySuccess = new() { Groups = [descriptions.Groups[^1]] };
        public bool RetryNext { get; set; }
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
                DescribeGroupsRequest describe => NextDescriptions(describe.Groups.Count),
                MetadataRequest => metadata,
                _ => throw new NotSupportedException(typeof(TRequest).Name)
            };
            return ValueTask.FromResult((TResponse)response);
        }
        private DescribeGroupsResponse NextDescriptions(int requestedCount)
        {
            if (!RetryNext) return requestedCount == descriptions.Groups.Count ? descriptions : _retrySuccess;
            RetryNext = false;
            return retry;
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
