using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Administrative result allocation and mapping with cached controller responses.</summary>
[MemoryDiagnoser]
public class AdminDetailedMutationBenchmarks
{
    [Params("create:1", "create:16", "mixed:16", "retry:16", "delete:16", "expand:16", "reassign:16")]
    public string Scenario { get; set; } = "create:16";

    private AdminClient _admin = null!;
    private MetadataManager _metadata = null!;
    private Connection _connection = null!;
    private string _operation = null!;
    private string[] _names = null!;
    private NewTopic[] _topics = null!;
    private Dictionary<string, NewPartitions> _expansions = null!;
    private Dictionary<TopicPartition, Optional<NewPartitionReassignment>> _reassignments = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var parts = Scenario.Split(':');
        _operation = parts[0];
        var count = int.Parse(parts[1]);
        _names = Enumerable.Range(0, count).Select(static i => $"topic-{i}").ToArray();
        _topics = _names.Select(static name => new NewTopic { Name = name }).ToArray();
        _expansions = _names.ToDictionary(static name => name, static _ => new NewPartitions { TotalCount = 2 });
        _reassignments = _names.ToDictionary(static name => new TopicPartition(name, 0),
            static _ => (Optional<NewPartitionReassignment>)NewPartitionReassignment.ToReplicas(1));
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
        if (await Mutate() != count) throw new InvalidOperationException("Incorrect mutation count.");
        if (_operation == "mixed")
        {
            var results = await _admin.CreateTopicsDetailedAsync(_topics);
            if (results[_names[^1]].ErrorCode != ErrorCode.TopicAuthorizationFailed || !results[_names[0]].IsSuccess)
                throw new InvalidOperationException("Mixed outcomes were not retained.");
        }
        else if (_operation == "retry" && _connection.CreateCalls != 2)
            throw new InvalidOperationException("Expected one controller retry.");

        IEnumerable<AdminMutationResult> values = _operation switch
        {
            "delete" => (await _admin.DeleteTopicsDetailedAsync(_names)).Values,
            "expand" => (await _admin.CreatePartitionsDetailedAsync(_expansions)).Values,
            "reassign" => (await _admin.AlterPartitionReassignmentsDetailedAsync(_reassignments)).Values,
            _ => (await _admin.CreateTopicsDetailedAsync(_topics)).Values
        };
        var successes = 0;
        foreach (var result in values)
        {
            if (result.IsSuccess) successes++;
            else if (_operation != "mixed" || result.ErrorCode != ErrorCode.TopicAuthorizationFailed)
                throw new InvalidOperationException("Unexpected mutation outcome.");
        }
        if (successes != count - (_operation == "mixed" ? 1 : 0))
            throw new InvalidOperationException("Incorrect successful mutation count.");

        // Actual elapsed workload warmup, independent of BDN's iteration calibration.
        var started = Stopwatch.GetTimestamp();
        long completed = 0;
        while (Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(20))
        {
            await Mutate();
            completed++;
        }
        Console.WriteLine($"Workload warmup: {Stopwatch.GetElapsedTime(started).TotalSeconds:F3}s; {completed} completed admin calls.");
    }

    [Benchmark]
    public async ValueTask<int> Mutate()
    {
        _connection.RetryNext = _operation == "retry";
        _connection.CreateCalls = 0;
        return _operation switch
        {
            "create" or "mixed" or "retry" => (await _admin.CreateTopicsDetailedAsync(_topics)).Count,
            "delete" => (await _admin.DeleteTopicsDetailedAsync(_names)).Count,
            "expand" => (await _admin.CreatePartitionsDetailedAsync(_expansions)).Count,
            "reassign" => (await _admin.AlterPartitionReassignmentsDetailedAsync(_reassignments)).Count,
            _ => throw new InvalidOperationException(Scenario)
        };
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _admin.DisposeAsync();
        await _metadata.DisposeAsync();
    }

    private sealed class Connection(MetadataResponse metadata, CreateTopicsResponse create,
        CreateTopicsResponse retry, CreateTopicsResponse retried, DeleteTopicsResponse delete,
        CreatePartitionsResponse expand, AlterPartitionReassignmentsResponse reassign) : IKafkaConnection
    {
        public bool RetryNext { get; set; }
        public int CreateCalls { get; set; }
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
                CreateTopicsRequest topics => NextCreate(topics.Topics.Count),
                DeleteTopicsRequest => delete,
                CreatePartitionsRequest => expand,
                AlterPartitionReassignmentsRequest => reassign,
                _ => throw new NotSupportedException(typeof(TRequest).Name)
            };
            return ValueTask.FromResult((TResponse)response);
        }
        private CreateTopicsResponse NextCreate(int count)
        {
            CreateCalls++;
            if (!RetryNext) return count == create.Topics.Count ? create : retried;
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
