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
    private readonly string _mode;
    private readonly int _count;
    private readonly string[] _ids;
    private readonly TopicPartition _partition = new("orders", 0);
    private AdminClient _admin = null!;
    private MetadataManager _metadata = null!;
    private InMemoryAdminClient _inventory = null!;
    private Connection _connection = null!;
    private Func<ValueTask<int>> _call = null!;
#if CANDIDATE
    private Dictionary<string, ListShareGroupOffsetsSpec> _specs = null!;
    private readonly ListShareGroupOffsetsOptions _zeroDeadline = new() { TimeoutMs = 0 };
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
        var groups = new DescribeShareGroupOffsetsResponseGroup[_count];
        var coordinators = new Dictionary<string, FindCoordinatorResponse>(StringComparer.Ordinal);
        var individual = new Dictionary<string, DescribeShareGroupOffsetsResponse>(StringComparer.Ordinal);
        var version = _mode == "batch0" ? (short)0 : (short)1;
        for (var i = 0; i < _count; i++)
        {
            var id = _ids[i];
            coordinators.Add(id, new() { Coordinators = [new() { Key = id, NodeId = 1, Host = "127.0.0.1", Port = 9092 }] });
            groups[i] = new()
            {
                GroupId = id,
                ErrorCode = _mode == "mixed" && i == _count - 1 ? ErrorCode.GroupAuthorizationFailed : ErrorCode.None,
                Topics = [new() { TopicName = "orders", Partitions = [new()
                {
                    PartitionIndex = 0, StartOffset = 42, LeaderEpoch = 7, Lag = version == 0 ? -1 : 9,
                    ErrorCode = _mode == "mixed" && i == 0 ? ErrorCode.TopicAuthorizationFailed : ErrorCode.None
                }] }]
            };
            individual.Add(id, new() { Groups = [groups[i]] });
        }
        var retryGroups = (DescribeShareGroupOffsetsResponseGroup[])groups.Clone();
        retryGroups[^1] = new() { GroupId = _ids[^1], Topics = [], ErrorCode = ErrorCode.NotCoordinator };
        var metadata = new MetadataResponse
        {
            Brokers = [new() { NodeId = 1, Host = "127.0.0.1", Port = 9092 }],
            ControllerId = 1, ClusterId = "fixture", Topics = []
        };
        _connection = new(coordinators, individual, new() { Groups = groups }, new() { Groups = retryGroups }, metadata);
        var pool = new Pool(_connection);
        _metadata = new MetadataManager(pool, ["127.0.0.1:9092"]);
        _metadata.Metadata.Update(metadata);
        _metadata.SetApiVersion(ApiKey.FindCoordinator, 4, 4);
        _metadata.SetApiVersion(ApiKey.DescribeShareGroupOffsets, 0, version);
        _metadata.SetApiVersion(ApiKey.Metadata, 9, 13);
        _admin = new(new() { BootstrapServers = ["127.0.0.1:9092"], RetryBackoffMs = 1, RetryBackoffMaxMs = 1 }, pool, _metadata);
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        _inventory = new(cluster);
        foreach (var id in _ids)
            await _inventory.AlterShareGroupOffsetsAsync(id, [new() { TopicPartition = _partition, StartOffset = 0 }]);
#if CANDIDATE
        _specs = _ids.ToDictionary(id => id, _ => new ListShareGroupOffsetsSpec(), StringComparer.Ordinal);
        if (_mode is "empty" or "empty0" or "inventory-empty") _specs.Clear();
#endif
        _call = _mode switch
        {
            "legacy" => LegacyCall,
            "inventory" => InventoryCall,
#if CANDIDATE
            "batch" or "batch0" or "mixed" or "retry" or "cancel" or "deadline"
                or "inventory-batch" or "empty" or "empty0" or "inventory-empty" => CandidateCall,
#endif
            _ => throw new NotSupportedException(_mode)
        };
        Observe(await Call());
        if (_mode is "legacy" or "inventory")
        {
            foreach (var id in _ids)
            {
                var offsets = _mode == "legacy" ? await _admin.DescribeShareGroupOffsetsAsync(id)
                    : await _inventory.DescribeShareGroupOffsetsAsync(id);
                if (offsets.Count != 1 || offsets[0].StartOffset != (_mode == "legacy" ? 42 : 0) || offsets[0].ErrorCode != ErrorCode.None)
                    throw new InvalidOperationException("Single-group fixture differs from the expected offsets.");
            }
        }
#if CANDIDATE
        else if (_mode is not ("deadline" or "cancel" or "empty" or "empty0" or "inventory-empty"))
        {
            _connection.RetryNext = _mode == "retry";
            var results = _mode == "inventory-batch" ? await _inventory.ListShareGroupOffsetsAsync(_specs)
                : await _admin.ListShareGroupOffsetsAsync(_specs);
            for (var i = 0; i < _count; i++)
            {
                var result = results[_ids[i]];
                var denied = _mode == "mixed" && i == _count - 1;
                if (result.ErrorCode != (denied ? ErrorCode.GroupAuthorizationFailed : ErrorCode.None))
                    throw new InvalidOperationException("Group outcome differs.");
                if (!denied)
                {
                    var offset = result.Offsets[_partition];
                    if (offset.StartOffset != (_mode == "inventory-batch" ? 0 : 42) ||
                        offset.ErrorCode != (_mode == "mixed" && i == 0 ? ErrorCode.TopicAuthorizationFailed : ErrorCode.None) ||
                        (_mode != "inventory-batch" && (offset.LeaderEpoch != 7 || offset.Lag != (version == 0 ? -1 : 9))))
                        throw new InvalidOperationException("Partition details differ.");
                }
            }
        }
#endif
    }

    public ValueTask<int> Call() => _call();

    // Keep the measured control method bodies identical in A and B. Candidate-only
    // APIs must not enlarge the control methods' async state machines.
    private async ValueTask<int> LegacyCall()
    {
        var completed = 0;
        foreach (var id in _ids) completed += (await _admin.DescribeShareGroupOffsetsAsync(id)).Count;
        return Observe(completed);
    }

    private async ValueTask<int> InventoryCall()
    {
        var completed = 0;
        foreach (var id in _ids) completed += (await _inventory.DescribeShareGroupOffsetsAsync(id)).Count;
        return Observe(completed);
    }

#if CANDIDATE
    private async ValueTask<int> CandidateCall()
    {
        switch (_mode)
        {
            case "empty":
            case "empty0":
                var beforeEmpty = _connection.Requests;
                var empty = await _admin.ListShareGroupOffsetsAsync(_specs, _mode == "empty0" ? _zeroDeadline : null);
                if (_connection.Requests != beforeEmpty) throw new InvalidOperationException("Empty query sent a request.");
                return Observe(empty.Count);
            case "inventory-empty": return Observe((await _inventory.ListShareGroupOffsetsAsync(_specs)).Count);
            case "batch":
            case "batch0":
            case "mixed":
            case "retry":
                _connection.RetryNext = _mode == "retry";
                return Observe((await _admin.ListShareGroupOffsetsAsync(_specs)).Count);
            case "inventory-batch": return Observe((await _inventory.ListShareGroupOffsetsAsync(_specs)).Count);
            case "deadline":
                var beforeDeadline = _connection.Requests;
                try { await _admin.ListShareGroupOffsetsAsync(_specs, _zeroDeadline); }
                catch (KafkaTimeoutException exception) when (exception.TimeoutKind == TimeoutKind.Api)
                {
                    if (_connection.Requests != beforeDeadline) throw new InvalidOperationException("Expired query sent a request.");
                    return _count;
                }
                throw new InvalidOperationException("An expired query unexpectedly completed.");
            case "cancel":
                using (var cancellation = new CancellationTokenSource())
                {
                    var beforeCancel = _connection.Requests;
                    _connection.CancelOnSend = cancellation;
                    try { await _admin.ListShareGroupOffsetsAsync(_specs, cancellationToken: cancellation.Token); }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                    {
                        if (_connection.Requests != beforeCancel + 1) throw new InvalidOperationException("Cancellation did not interrupt the expected send.");
                        return _count;
                    }
                    finally { _connection.CancelOnSend = null; }
                }
                throw new InvalidOperationException("An in-flight cancellation unexpectedly completed.");
            default: throw new NotSupportedException(_mode);
        }
    }
#endif

    private int Observe(int completed)
    {
        var expected = _mode is "empty" or "empty0" or "inventory-empty" ? 0 : _count;
        if (completed != expected) throw new InvalidOperationException("A requested group did not complete.");
        return completed;
    }

    public async ValueTask DisposeAsync()
    {
        await _admin.DisposeAsync();
        await _metadata.DisposeAsync();
        await _inventory.DisposeAsync();
    }

    private sealed class Connection(Dictionary<string, FindCoordinatorResponse> coordinators,
        Dictionary<string, DescribeShareGroupOffsetsResponse> individual, DescribeShareGroupOffsetsResponse all,
        DescribeShareGroupOffsetsResponse retry, MetadataResponse metadata) : IKafkaConnection
    {
        public bool RetryNext { get; set; }
        public long Requests { get; private set; }
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
                FindCoordinatorRequest find => coordinators[find.Key!],
                DescribeShareGroupOffsetsRequest describe => NextOffsets(describe),
                MetadataRequest => metadata,
                _ => throw new NotSupportedException(typeof(TRequest).Name)
            };
            return ValueTask.FromResult((TResponse)response);
        }
        private DescribeShareGroupOffsetsResponse NextOffsets(DescribeShareGroupOffsetsRequest request)
        {
            Requests++;
            CancelOnSend?.Cancel();
            if (!RetryNext) return request.Groups.Count == 1 ? individual[request.Groups[0].GroupId] : all;
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
