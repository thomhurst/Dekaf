using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks;

/// <summary>Administrative result allocation and mapping with cached controller responses.</summary>
public sealed class AdminFixture(string scenario) : IAsyncDisposable
{
    public string Scenario { get; } = scenario;
    private static readonly CreateTopicsOptions ValidateOnly = new() { ValidateOnly = true };

    private AdminClient _admin = null!;
    private MetadataManager _metadata = null!;
    private Connection _connection = null!;
    private string _operation = null!;
    private string[] _names = null!;
    private NewTopic[] _topics = null!;
    private Dictionary<string, NewPartitions> _expansions = null!;
    private Dictionary<TopicPartition, Optional<NewPartitionReassignment>> _reassignments = null!;
    private bool _createdNetworkTopic;

    public async Task InitializeAsync()
    {
        var parts = Scenario.Split(':');
        _operation = parts[0];
        var count = int.Parse(parts[1]);
        if (count is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(Scenario));
        _names = Enumerable.Range(0, count).Select(static i => $"topic-{i}").ToArray();
        _topics = _names.Select(static name => new NewTopic { Name = name }).ToArray();
        _expansions = _names.ToDictionary(static name => name, static _ => new NewPartitions { TotalCount = 2 });
        _reassignments = _names.ToDictionary(static name => new TopicPartition(name, 0),
            static _ => (Optional<NewPartitionReassignment>)NewPartitionReassignment.ToReplicas(1));
        if (_operation.StartsWith("network-", StringComparison.Ordinal))
        {
            var endpoint = Environment.GetEnvironmentVariable("ADMIN_MUTATION_BOOTSTRAP")
                ?? throw new InvalidOperationException("ADMIN_MUTATION_BOOTSTRAP is required for network cases.");
            _admin = (AdminClient)new AdminClientBuilder().WithBootstrapServers(endpoint).Build();
            if (_operation == "network-mixed")
            {
                await _admin.CreateTopicsAsync([_topics[^1]]);
                _createdNetworkTopic = true;
            }
            await ValidateAsync();
            return;
        }
        var metadata = new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
            ControllerId = 1,
            ClusterId = "fixture",
            Topics = []
        };
        var success = _names.Select(name => new CreateTopicsResponseTopic
        {
            Name = name,
            ErrorCode = _operation == "mixed" && name == _names[^1] ? ErrorCode.TopicAuthorizationFailed : ErrorCode.None
        }).ToArray();
        var retry = (CreateTopicsResponseTopic[])success.Clone();
        retry[^1] = new() { Name = _names[^1], ErrorCode = ErrorCode.NotController };
        _connection = new(metadata,
            new() { Topics = success }, new() { Topics = retry }, new() { Topics = [success[^1]] },
            new() { Responses = _names.Select(static name => new DeleteTopicsResponseTopic { Name = name }).ToArray() },
            new() { Results = _names.Select(static name => new CreatePartitionsResponseResult { Name = name }).ToArray() },
            new()
            {
                Responses = _names.Select(static name => new AlterPartitionReassignmentsResponseTopic
                { Name = name, Partitions = [new() { PartitionIndex = 0 }] }).ToArray()
            });
        var pool = new Pool(_connection);
        _metadata = new MetadataManager(pool, ["localhost:9092"]);
        _metadata.Metadata.Update(metadata);
        _metadata.SetApiVersion(ApiKey.Metadata, 9, 13);
        _metadata.SetApiVersion(ApiKey.CreateTopics, 5, 7);
        _metadata.SetApiVersion(ApiKey.DeleteTopics, 4, 6);
        _metadata.SetApiVersion(ApiKey.CreatePartitions, 2, 3);
        _metadata.SetApiVersion(ApiKey.AlterPartitionReassignments, 0, 1);
        _admin = new AdminClient(new AdminClientOptions
        {
            BootstrapServers = ["localhost:9092"],
            RetryBackoffMs = 1,
            RetryBackoffMaxMs = 1
        }, pool, _metadata);
        await ValidateAsync();
    }

    public async ValueTask ValidateAsync()
    {
        var count = _names.Length;
        if (await Call() != count) throw new InvalidOperationException("Incorrect mutation count.");
        if (_operation.StartsWith("legacy-", StringComparison.Ordinal) || _operation == "network-legacy") return;
#if CANDIDATE
        if (_operation == "network-mixed")
        {
            var networkResults = await _admin.CreateTopicsDetailedAsync(_topics, ValidateOnly);
            if (networkResults.Count != count) throw new InvalidOperationException("Incomplete broker results.");
            for (var i = 0; i < count; i++)
                if (networkResults[_names[i]].ErrorCode != (i == count - 1 ? ErrorCode.TopicAlreadyExists : ErrorCode.None))
                    throw new InvalidOperationException("Unexpected broker validate-only outcome.");
            return;
        }
        if (_operation == "mixed")
        {
            var results = await _admin.CreateTopicsDetailedAsync(_topics);
            if (results[_names[^1]].ErrorCode != ErrorCode.TopicAuthorizationFailed || !results[_names[0]].IsSuccess)
                throw new InvalidOperationException("Mixed outcomes were not retained.");
        }
        else if (_operation is "retry" or "wrapped" or "disconnected" or "disposed" && _connection.CreateCalls != 2)
            throw new InvalidOperationException("Expected one controller retry.");
        else if (_operation == "reassign-retry" && _connection.ReassignCalls != 3)
            throw new InvalidOperationException("Expected a partial rejection followed by a top-level rejection.");
        else if (_operation == "unregistered" && _connection.CreateCalls != 0)
            throw new InvalidOperationException("An unregistered controller must not receive a mutation.");

        PrepareMutation();
        IEnumerable<AdminMutationResult> values = _operation switch
        {
            "delete" => (await _admin.DeleteTopicsDetailedAsync(_names)).Values,
            "expand" => (await _admin.CreatePartitionsDetailedAsync(_expansions)).Values,
            "reassign" or "reassign-retry" => (await _admin.AlterPartitionReassignmentsDetailedAsync(_reassignments)).Values,
            _ => (await _admin.CreateTopicsDetailedAsync(_topics)).Values
        };
        var successes = 0;
        foreach (var result in values)
        {
            if (result.IsSuccess) successes++;
            else if (_operation == "unregistered" && result.Outcome == AdminMutationOutcome.NotAttempted
                && result.Exception is InvalidOperationException)
                continue;
            else if (_operation is "wrapped" or "disconnected" or "disposed"
                && result.Outcome == AdminMutationOutcome.Unknown && result.Exception is InvalidOperationException)
                continue;
            else if (_operation != "mixed" || result.ErrorCode != ErrorCode.TopicAuthorizationFailed)
                throw new InvalidOperationException("Unexpected mutation outcome.");
        }
        var expectedSuccesses = _operation == "unregistered" ? 0
            : count - (_operation is "mixed" or "wrapped" or "disconnected" or "disposed" ? 1 : 0);
        if (successes != expectedSuccesses)
            throw new InvalidOperationException("Incorrect successful mutation count.");

#endif
    }

    public async ValueTask<int> Call()
    {
        if (_operation == "network-legacy")
        {
            await _admin.CreateTopicsAsync(_topics, ValidateOnly);
            return _names.Length;
        }
#if CANDIDATE
        if (_operation == "network-mixed")
            return (await _admin.CreateTopicsDetailedAsync(_topics, ValidateOnly)).Count;
#endif
        PrepareMutation();
        switch (_operation)
        {
            case "legacy-create":
                await _admin.CreateTopicsAsync(_topics, ValidateOnly);
                return _names.Length;
            case "legacy-delete":
                await _admin.DeleteTopicsAsync(_names);
                return _names.Length;
#if CANDIDATE
            case "create":
            case "mixed":
            case "retry":
            case "wrapped":
            case "disconnected":
            case "disposed":
            case "unregistered":
                return (await _admin.CreateTopicsDetailedAsync(_topics)).Count;
            case "delete":
                return (await _admin.DeleteTopicsDetailedAsync(_names)).Count;
            case "expand":
                return (await _admin.CreatePartitionsDetailedAsync(_expansions)).Count;
            case "reassign":
            case "reassign-retry":
                return (await _admin.AlterPartitionReassignmentsDetailedAsync(_reassignments)).Count;
#endif
            default: throw new InvalidOperationException(Scenario);
        }
    }

    private void PrepareMutation()
    {
        _connection.Unregistered = _operation == "unregistered";
        _connection.RetryNext = _operation is "retry" or "wrapped" or "disconnected" or "disposed";
        _connection.Failure = _operation switch
        {
            "wrapped" or "disconnected" or "disposed" => _operation,
            _ => null
        };
        _connection.ReassignRetry = _operation == "reassign-retry";
        _connection.ReassignCalls = 0;
        _connection.CreateCalls = 0;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_createdNetworkTopic) await _admin.DeleteTopicsAsync([_names[^1]]);
        }
        finally
        {
            if (_admin is not null) await _admin.DisposeAsync();
            if (_metadata is not null) await _metadata.DisposeAsync();
        }
    }

    private sealed class Connection(MetadataResponse metadata, CreateTopicsResponse create,
        CreateTopicsResponse retry, CreateTopicsResponse retried, DeleteTopicsResponse delete,
        CreatePartitionsResponse expand, AlterPartitionReassignmentsResponse reassign) : IKafkaConnection
    {
        private static readonly ApiVersionsResponse Versions = new()
        {
            ErrorCode = ErrorCode.None,
            ApiKeys =
            [
                new() { ApiKey = ApiKey.Metadata, MinVersion = 9, MaxVersion = 13 },
                new() { ApiKey = ApiKey.CreateTopics, MinVersion = 5, MaxVersion = 7 },
                new() { ApiKey = ApiKey.DeleteTopics, MinVersion = 4, MaxVersion = 6 },
                new() { ApiKey = ApiKey.CreatePartitions, MinVersion = 2, MaxVersion = 3 },
                new() { ApiKey = ApiKey.AlterPartitionReassignments, MinVersion = 0, MaxVersion = 1 }
            ]
        };
        public bool RetryNext { get; set; }
        public bool Unregistered { get; set; }
        public int CreateCalls { get; set; }
        public string? Failure { get; set; }
        public bool ReassignRetry { get; set; }
        public int ReassignCalls { get; set; }
        private readonly AlterPartitionReassignmentsResponse _partialReassignment = new()
        {
            Responses = reassign.Responses.Select((topic, index) => new AlterPartitionReassignmentsResponseTopic
            {
                Name = topic.Name,
                Partitions = [new() { PartitionIndex = 0, ErrorCode = index == reassign.Responses.Count - 1 ? ErrorCode.NotController : ErrorCode.None }]
            }).ToArray()
        };
        private readonly AlterPartitionReassignmentsResponse _rejectedReassignment = new() { ErrorCode = ErrorCode.NotController, Responses = [] };
        private readonly AlterPartitionReassignmentsResponse _retriedReassignment = new() { Responses = [reassign.Responses[^1]] };
        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse
        {
            IKafkaResponse response = request switch
            {
                ApiVersionsRequest => Versions,
                MetadataRequest => metadata,
                CreateTopicsRequest topics => NextCreate(topics.Topics.Count),
                DeleteTopicsRequest => delete,
                CreatePartitionsRequest => expand,
                AlterPartitionReassignmentsRequest partitions => NextReassign(partitions.Topics.Count),
                _ => throw new NotSupportedException(typeof(TRequest).Name)
            };
            return ValueTask.FromResult((TResponse)response);
        }
        private CreateTopicsResponse NextCreate(int count)
        {
            CreateCalls++;
            if (!RetryNext && Failure is { } failure)
                throw failure switch
                {
                    "wrapped" => new InvalidOperationException("Transport unavailable", new IOException("Response lost")),
                    "disconnected" => new InvalidOperationException("Not connected"),
                    "disposed" => new ObjectDisposedException(nameof(Connection)),
                    _ => new InvalidOperationException("Unknown failure fixture.")
                };
            if (!RetryNext) return count == create.Topics.Count ? create : retried;
            RetryNext = false;
            return retry;
        }
        private AlterPartitionReassignmentsResponse NextReassign(int count)
        {
            ReassignCalls++;
            if (!ReassignRetry) return reassign;
            if (ReassignCalls == 1) return _partialReassignment;
            if (count != 1) throw new InvalidOperationException("Confirmed reassignment was replayed.");
            return ReassignCalls == 2 ? _rejectedReassignment : _retriedReassignment;
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
        public ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken token = default) =>
            connection.Unregistered ? throw new InvalidOperationException("Unknown broker ID: fixture") : ValueTask.FromResult<IKafkaConnection>(connection);
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
