using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Cold administrative operation costs; transport is synchronous and network-free.</summary>
[MemoryDiagnoser]
public class AdminMemberRemovalBenchmarks
{
    [Params(1, 32)] public int Members { get; set; }
    [Params("LegacyStatic", "SelectedStatic", "SelectedDynamic", "RemoveAllMixed")]
    public string Mode { get; set; } = "LegacyStatic";
    private IAdminClient _admin = null!;
    private MetadataManager _metadata = null!;
    private ConsumerGroupMemberToRemove[] _legacy = null!;
    private ConsumerGroupMemberRemovalOptions _options = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var metadataResponse = new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
            ControllerId = 1, ClusterId = "fixture", Topics = []
        };
        _legacy = new ConsumerGroupMemberToRemove[Members];
        var identities = new ConsumerGroupMemberIdentity[Members];
        var described = new DescribeGroupsResponseMember[Members];
        var outcomes = new LeaveGroupResponseMember[Members];
        for (var index = 0; index < Members; index++)
        {
            var instanceId = "instance-" + index;
            var memberId = "member-" + index;
            var dynamic = Mode == "SelectedDynamic" || (Mode == "RemoveAllMixed" && index % 2 == 0);
            _legacy[index] = new ConsumerGroupMemberToRemove { GroupInstanceId = instanceId };
            identities[index] = dynamic ? new ConsumerGroupMemberIdentity { MemberId = memberId }
                : new ConsumerGroupMemberIdentity { GroupInstanceId = instanceId };
            described[index] = new DescribeGroupsResponseMember { MemberId = memberId, GroupInstanceId = dynamic ? null : instanceId };
            outcomes[index] = new LeaveGroupResponseMember
            {
                MemberId = dynamic ? memberId : string.Empty, GroupInstanceId = dynamic ? null : instanceId, ErrorCode = ErrorCode.None
            };
        }
        _options = new ConsumerGroupMemberRemovalOptions { RemoveAll = Mode == "RemoveAllMixed", Members = Mode == "RemoveAllMixed" ? [] : identities };
        var connection = new Connection(metadataResponse,
            new DescribeGroupsResponse { Groups = [new DescribeGroupsResponseGroup
            {
                GroupId = "group", ErrorCode = ErrorCode.None, GroupState = "Stable", ProtocolType = "consumer", Members = described
            }] },
            new LeaveGroupResponse { ErrorCode = ErrorCode.None, Members = outcomes },
            new FindCoordinatorResponse { Coordinators = [new Coordinator
            {
                Key = "group", NodeId = 1, Host = "localhost", Port = 9092, ErrorCode = ErrorCode.None
            }] });
        var pool = new Pool(connection);
        var metadata = _metadata = new MetadataManager(pool, ["localhost:9092"]);
        metadata.Metadata.Update(metadataResponse);
        metadata.SetApiVersion(ApiKey.Metadata, 9, 13);
        metadata.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
        metadata.SetApiVersion(ApiKey.DescribeGroups, 5, 6);
        metadata.SetApiVersion(ApiKey.LeaveGroup, 3, 5);
        _admin = new AdminClient(new AdminClientOptions { BootstrapServers = ["localhost:9092"] }, pool, metadata);
        var result = await Remove();
        if (!result.Succeeded || result.Members.Count != Members)
            throw new InvalidOperationException("Fixture must remove every requested identity.");
        for (var index = 0; index < Members; index++)
        {
            if (result.Members[index].GroupInstanceId != (identities[index].GroupInstanceId ?? string.Empty)
                || result.Members[index].MemberId != (identities[index].MemberId ?? string.Empty))
                throw new InvalidOperationException("Fixture member identity mismatch.");
        }
    }

    [Benchmark]
    public ValueTask<RemoveMembersFromConsumerGroupResult> Remove() => Mode == "LegacyStatic"
        ? _admin.RemoveMembersFromConsumerGroupAsync("group", _legacy)
        : _admin.RemoveMembersFromConsumerGroupAsync("group", _options);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _admin.DisposeAsync();
        await _metadata.DisposeAsync();
    }

    private sealed class Connection(MetadataResponse metadata, DescribeGroupsResponse description,
        LeaveGroupResponse outcome, FindCoordinatorResponse coordinator) : IKafkaConnection
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
                DescribeGroupsRequest => description,
                LeaveGroupRequest => outcome,
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
