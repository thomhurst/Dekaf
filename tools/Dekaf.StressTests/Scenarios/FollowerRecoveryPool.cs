using System.Runtime.CompilerServices;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.StressTests.Scenarios;

/// <summary>
/// Replaces every sixteenth nonempty follower partition response with OFFSET_OUT_OF_RANGE.
/// Metadata, rack selection, network fetches and leader records remain real broker operations.
/// Wrappers are cached per connection; fault accounting is per partition response, not per record.
/// </summary>
internal sealed class FollowerRecoveryPool : IConnectionPool
{
    private readonly ConnectionPool _inner;
    private readonly string _topic;
    private readonly ConditionalWeakTable<IKafkaConnection, FaultingConnection> _connections = new();
    private readonly ConditionalWeakTable<IKafkaConnection, FaultingConnection>.CreateValueCallback _createConnection;
    public FollowerRecoveryOracle? Oracle { get; set; }

    public FollowerRecoveryPool(ConnectionPool inner, string topic)
    {
        _inner = inner;
        _topic = topic;
        _createConnection = connection => new FaultingConnection(this, connection);
    }

    public async ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken cancellationToken = default) =>
        _connections.GetValue(await _inner.GetConnectionAsync(brokerId, cancellationToken).ConfigureAwait(false), _createConnection);
    public async ValueTask<IKafkaConnection> GetConnectionByIndexAsync(int brokerId, int index, CancellationToken cancellationToken = default) =>
        _connections.GetValue(await _inner.GetConnectionByIndexAsync(brokerId, index, cancellationToken).ConfigureAwait(false), _createConnection);
    public ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken cancellationToken = default) =>
        _inner.GetConnectionAsync(host, port, cancellationToken);
    public void RegisterBroker(int brokerId, string host, int port) => _inner.RegisterBroker(brokerId, host, port);
    public ValueTask<int> ScaleConnectionGroupAsync(int brokerId, int newCount, CancellationToken cancellationToken = default) =>
        _inner.ScaleConnectionGroupAsync(brokerId, newCount, cancellationToken);
    public ValueTask<IKafkaConnection?> ShrinkConnectionGroupAsync(int brokerId, int newCount, CancellationToken cancellationToken = default) =>
        _inner.ShrinkConnectionGroupAsync(brokerId, newCount, cancellationToken);
    public ValueTask RemoveConnectionAsync(int brokerId) => _inner.RemoveConnectionAsync(brokerId);
    public ValueTask CloseAllAsync() => _inner.CloseAllAsync();
    public ValueTask DisposeAsync() => _inner.DisposeAsync();

    private async ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        IKafkaConnection connection, TRequest request, short apiVersion, CancellationToken cancellationToken)
        where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse
    {
        var oracle = Oracle;
        var epoch = oracle?.Epoch ?? 0;
        var response = await connection.SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken).ConfigureAwait(false);
        return ObserveCompletedResponse(connection, request, response, oracle, epoch);
    }

    private async Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
        IKafkaConnection connection, TRequest request, short apiVersion, bool callerTimeout, CancellationToken cancellationToken)
        where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse
    {
        var oracle = Oracle;
        var epoch = oracle?.Epoch ?? 0;
        var pending = callerTimeout
            ? connection.SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(request, apiVersion, cancellationToken)
            : connection.SendPipelinedAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
        var response = await pending.ConfigureAwait(false);
        return ObserveCompletedResponse(connection, request, response, oracle, epoch);
    }

    private TResponse ObserveCompletedResponse<TRequest, TResponse>(IKafkaConnection connection,
        TRequest request, TResponse response, FollowerRecoveryOracle? oracle, int epoch)
        where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse
    {
        if (oracle is null || request is not FetchRequest fetch || response is not FetchResponse fetched)
            return response;
        try
        {
            ObserveResponse(connection.BrokerId, fetch, fetched, oracle, epoch);
        }
        catch
        {
            // No caller owns a response rejected by the experiment's oracle.
            for (var t = 0; t < fetched.Responses.Count; t++)
                fetched.Responses[t].ReturnToPoolAfterFailedParse();
            fetched.PooledMemoryOwner?.Dispose();
            fetched.ReturnToPool();
            throw;
        }
        return response;
    }

    private void ObserveResponse(int brokerId, FetchRequest request, FetchResponse response, FollowerRecoveryOracle oracle, int epoch)
    {
        if (brokerId is not (1 or 2))
            throw new InvalidOperationException($"Unexpected fetch destination {brokerId} in the fixed replica assignment.");
        for (var t = 0; t < response.Responses.Count; t++)
        {
            var responseTopic = response.Responses[t];
            var requestTopic = FindRequestedTopic(request, responseTopic);
            for (var p = 0; p < responseTopic.Partitions.Count; p++)
            {
                var partition = responseTopic.Partitions[p];
                if (partition.ErrorCode != ErrorCode.None)
                    continue;
                var offset = FindRequestedOffset(requestTopic, partition.PartitionIndex);
                if (brokerId == 1)
                {
                    oracle.ObserveLeaderResponse(partition.PartitionIndex, offset, epoch);
                    continue;
                }
                if (partition.Records is not { Count: > 0 } records)
                    continue;
                if (!oracle.ObserveFollowerData(partition.PartitionIndex, offset, epoch))
                    continue;

                // These records never reach PendingFetchData. The response retains its
                // network memory owner until the consumer handles the injected error.
                for (var b = 0; b < records.Count; b++)
                    records[b].DisposeAndReturnUnownedConsumerBatch();
                if (records is List<Dekaf.Protocol.Records.RecordBatch> list)
                    FetchResponsePartition.ReturnRecordBatchList(list);
                partition.Records = null;
                partition.ErrorCode = ErrorCode.OffsetOutOfRange;
                partition.PreferredReadReplica = -1;
            }
        }
    }

    private FetchRequestTopic FindRequestedTopic(FetchRequest request, FetchResponseTopic response)
    {
        for (var t = 0; t < request.Topics.Count; t++)
        {
            var topic = request.Topics[t];
            if (topic.Topic == _topic && topic.TopicId == response.TopicId)
                return topic;
        }
        throw new InvalidOperationException("Fetch response did not match the recovery topic.");
    }

    private static long FindRequestedOffset(FetchRequestTopic request, int partition)
    {
        for (var p = 0; p < request.Partitions.Count; p++)
        {
            if (request.Partitions[p].Partition == partition)
                return request.Partitions[p].FetchOffset;
        }
        throw new InvalidOperationException("Fetch response partition was absent from the full request.");
    }

    private sealed class FaultingConnection(FollowerRecoveryPool owner, IKafkaConnection inner) : IKafkaConnection
    {
        public int BrokerId => inner.BrokerId;
        public string Host => inner.Host;
        public int Port => inner.Port;
        public bool IsConnected => inner.IsConnected;
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
            owner.SendAsync<TRequest, TResponse>(inner, request, apiVersion, cancellationToken);
        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
            inner.SendFireAndForgetAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
            owner.SendPipelinedAsync<TRequest, TResponse>(inner, request, apiVersion, callerTimeout: false, cancellationToken);
        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
            inner.SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
            owner.SendPipelinedAsync<TRequest, TResponse>(inner, request, apiVersion, callerTimeout: true, cancellationToken);
        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => inner.ConnectAsync(cancellationToken);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
