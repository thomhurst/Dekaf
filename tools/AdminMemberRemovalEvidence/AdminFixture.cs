using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Testing;

namespace Dekaf.Benchmarks;

public sealed class AdminFixture : IAsyncDisposable
{
    private const string Group = "group";
    private readonly string _mode;
    private readonly int _count;
    private readonly string[] _memberIds;
    private readonly string[] _instanceIds;
    private readonly long[] _registrations;
    private readonly TopicPartition[] _partitions = [new("orders", 0)];
    private readonly InMemoryKafkaCluster _cluster = new();
    private IAdminClient _admin = null!;
    private InMemoryAdminClient _fake = null!;
    private MetadataManager _metadata = null!;
    private Connection _connection = null!;
    private ConsumerGroupMemberToRemove[] _legacy = null!;
    private Func<ValueTask<int>> _call = null!;
#if CANDIDATE
    private ConsumerGroupMemberRemovalOptions _options = null!;
#endif

    public AdminFixture(string testCase)
    {
        var parts = testCase.Split(':');
        _mode = parts[0];
        _count = int.Parse(parts[1]);
        if (_count <= 0) throw new ArgumentOutOfRangeException(nameof(testCase));
        _memberIds = Enumerable.Range(0, _count).Select(i => $"member-{i}").ToArray();
        _instanceIds = Enumerable.Range(0, _count).Select(i => $"instance-{i}").ToArray();
        _registrations = new long[_count];
    }

    public async Task InitializeAsync()
    {
        _legacy = new ConsumerGroupMemberToRemove[_count];
        var described = new DescribeGroupsResponseMember[_count];
        var outcomes = new LeaveGroupResponseMember[_count];
#if CANDIDATE
        var identities = new ConsumerGroupMemberIdentity[_count];
#endif
        for (var i = 0; i < _count; i++)
        {
            var dynamic = _mode is "dynamic" or "ambiguous" or "cancel" or "deadline"
                || (_mode is "all" or "fake-all" && i % 2 == 0);
            var instance = dynamic ? null : _instanceIds[i];
            _legacy[i] = new() { GroupInstanceId = _instanceIds[i] };
            described[i] = new() { MemberId = _memberIds[i], GroupInstanceId = instance };
            outcomes[i] = new()
            {
                MemberId = dynamic ? _memberIds[i] : string.Empty, GroupInstanceId = instance,
                ErrorCode = _mode == "partial" && i == _count - 1 ? ErrorCode.UnknownMemberId : ErrorCode.None
            };
#if CANDIDATE
            identities[i] = dynamic ? new() { MemberId = _memberIds[i] } : new() { GroupInstanceId = instance };
#endif
        }
        var metadata = new MetadataResponse
        {
            Brokers = [new() { NodeId = 1, Host = "127.0.0.1", Port = 9092 }],
            ControllerId = 1, ClusterId = "fixture", Topics = []
        };
        _connection = new(metadata,
            new() { Groups = [new() { GroupId = Group, ErrorCode = ErrorCode.None,
                GroupState = "Stable", ProtocolType = "consumer", Members = described }] },
            new() { ErrorCode = ErrorCode.None, Members = outcomes },
            new() { Coordinators = [new() { Key = Group, NodeId = 1, Host = "127.0.0.1", Port = 9092 }] });
        var pool = new Pool(_connection);
        _metadata = new(pool, ["127.0.0.1:9092"]);
        _metadata.Metadata.Update(metadata);
        _metadata.SetApiVersion(ApiKey.Metadata, 9, 13);
        _metadata.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
        _metadata.SetApiVersion(ApiKey.DescribeGroups, 5, 6);
        _metadata.SetApiVersion(ApiKey.LeaveGroup, 3, 5);
        _admin = new AdminClient(new() { BootstrapServers = ["127.0.0.1:9092"], RetryBackoffMs = 1, RetryBackoffMaxMs = 1 }, pool, _metadata);
        _cluster.CreateTopic("orders");
        _fake = new(_cluster);
#if CANDIDATE
        var all = _mode is "all" or "fake-all";
        _options = new() { RemoveAll = all, Members = all ? [] : identities, TimeoutMs = _mode is "deadline" or "fake-deadline" ? 0 : 30000 };
#endif
        _call = _mode switch
        {
            "legacy" => LegacyCall,
            "registration" => RegistrationCall,
#if CANDIDATE
            "static" or "dynamic" or "all" or "partial" or "retry" or "ambiguous"
                or "cancel" or "deadline" or "fake-all" or "fake-static"
                or "fake-replace" or "fake-deadline" => CandidateCall,
#endif
            _ => throw new NotSupportedException(_mode)
        };
        if (await Call() != _count) throw new InvalidOperationException("Fixture did not complete every member.");
        if (_mode == "legacy") Validate(await _admin.RemoveMembersFromConsumerGroupAsync(Group, _legacy));
#if CANDIDATE
        else if (_mode is "static" or "dynamic" or "all" or "partial")
            Validate(await _admin.RemoveMembersFromConsumerGroupAsync(Group, _options));
#endif
    }

    public ValueTask<int> Call() => _call();

    // These measured control bodies have no conditional compilation in A or B.
    private async ValueTask<int> LegacyCall()
    {
        var result = await _admin.RemoveMembersFromConsumerGroupAsync(Group, _legacy);
        if (result.Members.Count != _count || !result.Succeeded) throw new InvalidOperationException("Legacy removal failed.");
        return _count;
    }

    private ValueTask<int> RegistrationCall()
    {
        for (var i = 0; i < _count; i++)
            _cluster.RegisterConsumerGroupMember(Group, _memberIds[i], _partitions, out _registrations[i]);
        for (var i = 0; i < _count; i++)
            _cluster.UnregisterConsumerGroupMember(Group, _memberIds[i], _registrations[i]);
        if (_cluster.GetConsumerGroupGeneration(Group) != 0) throw new InvalidOperationException("Registration cycle left members behind.");
        return ValueTask.FromResult(_count);
    }

#if CANDIDATE
    private async ValueTask<int> CandidateCall()
    {
        if (_mode is "fake-all" or "fake-static" or "fake-replace" or "fake-deadline")
        {
            for (var i = 0; i < _count; i++)
                _cluster.RegisterConsumerGroupMember(Group, _memberIds[i], _partitions, out _registrations[i],
                    _mode == "fake-all" && i % 2 == 0 ? null : _instanceIds[i]);
            if (_mode == "fake-replace")
            {
                for (var i = 0; i < _count; i++)
                {
                    _cluster.RegisterConsumerGroupMember(Group, _memberIds[i], _partitions, out _, _instanceIds[i]);
                    _cluster.UnregisterConsumerGroupMember(Group, _memberIds[i], _registrations[i]);
                }
            }
            if (_mode == "fake-deadline")
            {
                try { await _fake.RemoveMembersFromConsumerGroupAsync(Group, _options); }
                catch (KafkaTimeoutException)
                {
                    if (_cluster.GetConsumerGroupGeneration(Group) == 0) throw new InvalidOperationException("Expired fake removal evicted members.");
                    for (var i = 0; i < _count; i++)
                        _cluster.UnregisterConsumerGroupMember(Group, _memberIds[i], _registrations[i]);
                    if (_cluster.GetConsumerGroupGeneration(Group) != 0) throw new InvalidOperationException("Fake deadline cleanup failed.");
                    return _count;
                }
                throw new InvalidOperationException("Expired fake removal completed.");
            }
            var removed = _mode is "fake-static" or "fake-replace" ? await _fake.RemoveMembersFromConsumerGroupAsync(Group, _legacy)
                : await _fake.RemoveMembersFromConsumerGroupAsync(Group, _options);
            if (removed.Members.Count != _count || !removed.Succeeded || _cluster.GetConsumerGroupGeneration(Group) != 0)
                throw new InvalidOperationException("Fake eviction left incomplete members.");
            return _count;
        }
        var before = _connection.Sends;
        _connection.RetryNext = _mode == "retry";
        _connection.FailNext = _mode == "ambiguous";
        using var cancellation = _mode == "cancel" ? new CancellationTokenSource() : null;
        _connection.CancelOnSend = cancellation;
        try
        {
            var result = await _admin.RemoveMembersFromConsumerGroupAsync(Group, _options, cancellation?.Token ?? default);
            if (_mode is "ambiguous" or "cancel" or "deadline") throw new InvalidOperationException("Expected failure did not occur.");
            Validate(result);
            if (_connection.Sends - before != (_mode == "retry" ? 2 : 1)) throw new InvalidOperationException("Unexpected removal send count.");
            return _count;
        }
        catch (KafkaTimeoutException) when (_mode == "deadline")
        {
            if (_connection.Sends != before) throw new InvalidOperationException("Expired removal sent a request.");
            return _count;
        }
        catch (OperationCanceledException) when (_mode == "cancel" && cancellation!.IsCancellationRequested)
        {
            if (_connection.Sends != before + 1) throw new InvalidOperationException("Cancellation did not stop the expected send.");
            return _count;
        }
        catch (KafkaException exception) when (_mode == "ambiguous" && !exception.IsRetriable && exception.InnerException is IOException)
        {
            if (_connection.Sends != before + 1) throw new InvalidOperationException("Ambiguous removal was replayed.");
            return _count;
        }
        finally { _connection.CancelOnSend = null; }
    }
#endif

    private void Validate(RemoveMembersFromConsumerGroupResult result)
    {
        if (result.Members.Count != _count) throw new InvalidOperationException("Wrong result count.");
        for (var i = 0; i < _count; i++)
        {
            var member = result.Members[i];
            var dynamic = _mode == "dynamic" || (_mode == "all" && i % 2 == 0);
            if (member.GroupInstanceId != (dynamic ? string.Empty : _instanceIds[i]) ||
                member.MemberId != (dynamic ? _memberIds[i] : string.Empty) ||
                member.ErrorCode != (_mode == "partial" && i == _count - 1 ? ErrorCode.UnknownMemberId : ErrorCode.None))
                throw new InvalidOperationException("Removal identity or outcome differs.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _admin.DisposeAsync();
        await _metadata.DisposeAsync();
        await _fake.DisposeAsync();
    }

    private sealed class Connection(MetadataResponse metadata, DescribeGroupsResponse description,
        LeaveGroupResponse outcome, FindCoordinatorResponse coordinator) : IKafkaConnection
    {
        private readonly LeaveGroupResponse _retry = new() { ErrorCode = ErrorCode.NotCoordinator, Members = [] };
        public long Sends { get; private set; }
        public bool RetryNext { get; set; }
        public bool FailNext { get; set; }
        public CancellationTokenSource? CancelOnSend { get; set; }
        public int BrokerId => 1;
        public string Host => "127.0.0.1";
        public int Port => 9092;
        public bool IsConnected => true;
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse
        {
            IKafkaResponse response = request switch
            {
                MetadataRequest => metadata, FindCoordinatorRequest => coordinator,
                DescribeGroupsRequest => description, LeaveGroupRequest => Remove(),
                _ => throw new NotSupportedException(typeof(TRequest).Name)
            };
            return ValueTask.FromResult((TResponse)response);
        }
        private LeaveGroupResponse Remove()
        {
            Sends++;
            CancelOnSend?.Cancel();
            if (FailNext) { FailNext = false; throw new IOException("Response lost after removal send."); }
            if (RetryNext) { RetryNext = false; return _retry; }
            return outcome;
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
