using System.Linq.Expressions;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Steady-state share request construction, lease use and reply bookkeeping.</summary>
[MemoryDiagnoser]
public class HostedShareRequestBenchmarks
{
    [Params(false, true)]
    public bool Hosted { get; set; }

    [Params(1, 16)]
    public int PartitionCount { get; set; }

    private KafkaShareConsumer<string, string> _consumer = null!;
    private MetadataManager _metadata = null!;
    private Connection _connection = null!;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly List<TopicPartition> _partitions = [];
    private readonly Dictionary<TopicPartition, List<AcknowledgementBatchData>> _acknowledgements = [];
    private readonly List<ShareConsumeResult<string, string>> _records = [];
    private Func<int, List<TopicPartition>, Dictionary<TopicPartition, List<AcknowledgementBatchData>>,
        CancellationToken, Task> _fetch = null!;
    private Func<int, Dictionary<TopicPartition, List<AcknowledgementBatchData>>, bool,
        CancellationToken, Task> _acknowledge = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var topicId = Guid.NewGuid();
        var metadataPartitions = new PartitionMetadata[PartitionCount];
        var fetchPartitions = new ShareFetchResponsePartition[PartitionCount];
        var acknowledgePartitions = new ShareAcknowledgeResponsePartition[PartitionCount];
        for (var index = 0; index < PartitionCount; index++)
        {
            var partition = new TopicPartition("topic", index);
            _partitions.Add(partition);
            _records.Add(new ShareConsumeResult<string, string>
            {
                Topic = "topic", Partition = index, Offset = 42, Value = "value", DeliveryCount = 1
            });
            _acknowledgements.Add(partition,
                [new AcknowledgementBatchData(42, 42, [(byte)AcknowledgeType.Accept])]);
            metadataPartitions[index] = new PartitionMetadata
            {
                ErrorCode = ErrorCode.None, PartitionIndex = index, LeaderId = 1, ReplicaNodes = [1], IsrNodes = [1]
            };
            fetchPartitions[index] = new ShareFetchResponsePartition
            {
                PartitionIndex = index, CurrentLeader = new(), AcquiredRecords = []
            };
            acknowledgePartitions[index] = new ShareAcknowledgeResponsePartition
            {
                PartitionIndex = index, CurrentLeader = new()
            };
        }
        var metadata = new MetadataResponse
        {
            Brokers = [new() { NodeId = 1, Host = "localhost", Port = 9092 }],
            Topics = [new() { ErrorCode = ErrorCode.None, Name = "topic", TopicId = topicId, Partitions = metadataPartitions }]
        };
        var connection = _connection = new Connection(
            new() { ErrorCode = ErrorCode.None, Responses = [new() { TopicId = topicId, Partitions = fetchPartitions }], NodeEndpoints = [] },
            new() { Responses = [new() { TopicId = topicId, Partitions = acknowledgePartitions }], NodeEndpoints = [] });
        var pool = new Pool(connection);
        _metadata = new MetadataManager(pool, ["localhost:9092"]);
        _metadata.Metadata.Update(metadata);
        _metadata.SetApiVersion(ApiKey.ShareFetch, 2, 2);
        _metadata.SetApiVersion(ApiKey.ShareAcknowledge, 2, 2);
        _consumer = new KafkaShareConsumer<string, string>(
            new() { BootstrapServers = ["localhost:9092"], GroupId = "benchmark", AcknowledgementMode = ShareAcknowledgementMode.Explicit },
            Serializers.String, Serializers.String, pool, _metadata);
        if (Hosted)
        {
            ShareAcknowledgementCommitCallback observer = static _ => { };
            var hostedOverload = typeof(IHostedShareConsumer).GetMethod("ObserveAcknowledgements",
                [typeof(ShareAcknowledgementCommitCallback), typeof(CancellationToken)]);
            if (hostedOverload is null)
                ((IHostedShareConsumer)_consumer).ObserveAcknowledgements(observer);
            else
                hostedOverload.Invoke(_consumer, [observer, _shutdown.Token]);
        }
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(KafkaShareConsumer<string, string>).GetField("_initialized", flags)!.SetValue(_consumer, true);
        var coordinator = typeof(KafkaShareConsumer<string, string>).GetField("_coordinator", flags)!.GetValue(_consumer)!;
        typeof(ShareConsumerCoordinator).GetField("_memberId", flags)!.SetValue(coordinator, "benchmark-member");
        _fetch = typeof(KafkaShareConsumer<string, string>).GetMethod("SendShareFetchForPartitionsAsync", flags)!
            .CreateDelegate<Func<int, List<TopicPartition>, Dictionary<TopicPartition, List<AcknowledgementBatchData>>, CancellationToken, Task>>(_consumer);
        // Older products have four parameters; batch-session cleanup adds an optional fifth.
        // Bind the same direct-call adapter on both revisions outside measurement.
        var acknowledge = typeof(KafkaShareConsumer<string, string>).GetMethod("SendAcknowledgeAsync", flags)!;
        var broker = Expression.Parameter(typeof(int));
        var acknowledgements = Expression.Parameter(typeof(Dictionary<TopicPartition, List<AcknowledgementBatchData>>));
        var retry = Expression.Parameter(typeof(bool));
        var cancellation = Expression.Parameter(typeof(CancellationToken));
        Expression[] arguments = [broker, acknowledgements, retry, cancellation];
        if (acknowledge.GetParameters().Length == 5)
            arguments = [.. arguments, Expression.Constant(false)];
        _acknowledge = Expression.Lambda<Func<int, Dictionary<TopicPartition, List<AcknowledgementBatchData>>, bool, CancellationToken, Task>>(
            Expression.Call(Expression.Constant(_consumer), acknowledge, arguments),
            broker, acknowledgements, retry, cancellation).Compile();
        await ShareFetch();
        await ShareAcknowledge();
        var sessions = (ShareSessionManager)typeof(KafkaShareConsumer<string, string>)
            .GetField("_sessionManager", flags)!.GetValue(_consumer)!;
        if (connection.RequestCount != 2 || sessions.GetSessionEpoch(1) != 1)
            throw new InvalidOperationException("Both request paths must complete successful broker bookkeeping.");
        await Commit();
        if (connection.RequestCount != 3 || sessions.GetSessionEpoch(1) != 2)
            throw new InvalidOperationException("Commit must submit its tracked acknowledgement outcomes.");
    }

    // Costs are per request with PartitionCount acknowledgement batches. Cached replies exclude
    // network I/O, parsing, delivery and shutdown; setup binds delegates outside measurement.
    [Benchmark]
    public Task ShareFetch() => _fetch(1, _partitions, _acknowledgements, CancellationToken.None);

    [Benchmark]
    public Task ShareAcknowledge() => _acknowledge(1, _acknowledgements, false, CancellationToken.None);

    [Benchmark]
    public ValueTask Commit()
    {
        for (var index = 0; index < _records.Count; index++)
            _consumer.Acknowledge(_records[index], AcknowledgeType.Accept);
        return _consumer.CommitAsync();
    }

    internal ValueTask CommitWithDeferredReply()
    {
        _connection.DeferAcknowledgement = true;
        try
        {
            var pending = Commit();
            if (pending.IsCompleted)
                throw new InvalidOperationException("Commit must suspend until the broker reply is released.");
            _connection.CompleteAcknowledgement();
            return pending;
        }
        finally
        {
            _connection.DeferAcknowledgement = false;
        }
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _consumer.DisposeAsync();
        await _metadata.DisposeAsync();
        _shutdown.Dispose();
    }

    private sealed class Connection(ShareFetchResponse fetch, ShareAcknowledgeResponse acknowledge) : IKafkaConnection
    {
        private TaskCompletionSource<ShareAcknowledgeResponse>? _pendingAcknowledgement;
        internal bool DeferAcknowledgement { get; set; }
        internal int RequestCount { get; private set; }
        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse
        {
            RequestCount++;
            if (DeferAcknowledgement && request is ShareAcknowledgeRequest)
            {
                _pendingAcknowledgement = new(TaskCreationOptions.RunContinuationsAsynchronously);
                return AwaitAcknowledgementAsync<TResponse>(_pendingAcknowledgement.Task);
            }
            IKafkaResponse response = request switch
            {
                ShareFetchRequest => fetch,
                ShareAcknowledgeRequest => acknowledge,
                _ => throw new NotSupportedException(typeof(TRequest).Name)
            };
            return ValueTask.FromResult((TResponse)response);
        }

        internal void CompleteAcknowledgement()
        {
            var pending = _pendingAcknowledgement
                ?? throw new InvalidOperationException("No acknowledgement is awaiting its reply.");
            _pendingAcknowledgement = null;
            pending.SetResult(acknowledge);
        }

        private static async ValueTask<TResponse> AwaitAcknowledgementAsync<TResponse>(Task<ShareAcknowledgeResponse> pending)
            where TResponse : IKafkaResponse
            => (TResponse)(IKafkaResponse)await pending.ConfigureAwait(false);

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
