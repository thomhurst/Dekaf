using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Producer;

internal sealed class TestKafkaConnection :
    IKafkaConnection,
    IKafkaPipelinedWriteCompletionConnection,
    IRetirableKafkaConnection
{
    private int _leaseCount;
    private int _retirementState;

    public int BrokerId { get; init; } = 1;
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 9092;
    public bool IsConnected { get; set; } = true;

    public int SendPipelinedCalls;
    public int SendPipelinedWithCallerTimeoutCalls;
    public int SendPipelinedAfterWriteCalls;
    public int SendPipelinedWithCallerTimeoutAfterWriteCalls;
    public int SendFireAndForgetWithCallerTimeoutCalls;
    public int DisposeCalls;
    public int CompleteRetirementCalls;
    public int LeaseCountDuringRequest;

    public TaskCompletionSource LeaseCountObserved { get; } = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource DisposeStarted { get; } = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Opt-in switch for <see cref="CapturedProduceRequests"/>. Off by default so the many
    /// suites sharing this double don't pay per-send capture cost for diagnostics they never read.
    /// </summary>
    public bool CaptureProduceRequests { get; set; }

    /// <summary>
    /// Snapshot of every pipelined produce request's record batches, captured at write time
    /// (the sender's scratch request structures are cleared after each send, so the request
    /// object itself cannot be inspected later). Lock the list to read it.
    /// </summary>
    public List<(string Info, IReadOnlyList<object> RecordBatches)> CapturedProduceRequests { get; } = [];

    public Func<ValueTask<Task<ProduceResponse>>>? SendProducePipelinedAfterWrite { get; set; }
    public IPipelinedResponseSource<ProduceResponse>? PipelinedResponseSource { get; set; }
    public Func<ValueTask>? SendProduceFireAndForgetWithCallerTimeout { get; set; }
    public Func<Type, object>? SendResponse { get; set; }

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        if (SendResponse is null)
            throw new NotSupportedException();

        LeaseCountDuringRequest = Volatile.Read(ref _leaseCount);
        return ValueTask.FromResult((TResponse)SendResponse(typeof(TRequest)));
    }

    public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
        => SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

    public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        Interlocked.Increment(ref SendPipelinedCalls);
        throw new NotSupportedException();
    }

    public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        Interlocked.Increment(ref SendFireAndForgetWithCallerTimeoutCalls);
        return SendProduceFireAndForgetWithCallerTimeout?.Invoke() ?? ValueTask.CompletedTask;
    }

    public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        Interlocked.Increment(ref SendPipelinedWithCallerTimeoutCalls);
        throw new NotSupportedException();
    }

    public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        DisposeStarted.TrySetResult();
        Interlocked.Increment(ref DisposeCalls);
        return ValueTask.CompletedTask;
    }

    int IRetirableKafkaConnection.LeaseCount
    {
        get
        {
            LeaseCountObserved.TrySetResult();
            return Volatile.Read(ref _leaseCount);
        }
    }

    int IRetirableKafkaConnection.ActiveOperationCount => 0;

    bool IRetirableKafkaConnection.TryAcquireLease()
    {
        if (Volatile.Read(ref _retirementState) != 0)
            return false;

        Interlocked.Increment(ref _leaseCount);
        if (Volatile.Read(ref _retirementState) == 0)
            return true;

        ((IRetirableKafkaConnection)this).ReleaseLease();
        return false;
    }

    void IRetirableKafkaConnection.ReleaseLease() => Interlocked.Decrement(ref _leaseCount);

    void IRetirableKafkaConnection.BeginRetirement()
        => Interlocked.CompareExchange(ref _retirementState, 1, 0);

    void IRetirableKafkaConnection.CompleteRetirement()
    {
        Interlocked.Increment(ref CompleteRetirementCalls);
        Volatile.Write(ref _retirementState, 2);
    }

    public async ValueTask<PipelinedResponse<TResponse>> SendPipelinedAfterWriteAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        Interlocked.Increment(ref SendPipelinedAfterWriteCalls);

        if (CaptureProduceRequests && request is ProduceRequest produceRequest)
        {
            var recordBatches = new List<object>();
            var info = new System.Text.StringBuilder();
            foreach (var (topicName, partition) in EnumeratePartitions(produceRequest))
            {
                foreach (var recordBatch in partition.Records)
                {
                    recordBatches.Add(recordBatch);
                    info.Append($"{topicName}-{partition.Index}(seq={recordBatch.BaseSequence}) ");
                }
            }

            lock (CapturedProduceRequests)
                CapturedProduceRequests.Add((info.ToString(), recordBatches));
        }

        if (PipelinedResponseSource is not null)
        {
            var source = (IPipelinedResponseSource<TResponse>)(object)PipelinedResponseSource;
            return new PipelinedResponse<TResponse>(source, token: 0);
        }

        if (SendProducePipelinedAfterWrite is null)
            throw new NotSupportedException();

        var responseTask = await SendProducePipelinedAfterWrite().ConfigureAwait(false);
        return new PipelinedResponse<TResponse>(CastResponseTask<TResponse>(responseTask));
    }

    public async ValueTask<PipelinedResponse<TResponse>> SendPipelinedWithCallerTimeoutAfterWriteAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        Interlocked.Increment(ref SendPipelinedWithCallerTimeoutAfterWriteCalls);
        throw new NotSupportedException();
    }

    private static async Task<TResponse> CastResponseTask<TResponse>(Task<ProduceResponse> responseTask)
        where TResponse : IKafkaResponse
        => (TResponse)(IKafkaResponse)await responseTask.ConfigureAwait(false);

    // BrokerSender populates produce requests through internal scratch arrays that the public
    // TopicData/PartitionData properties never expose; read them reflectively so the capture
    // also sees coalesced multi-batch requests.
    private static readonly System.Reflection.FieldInfo TopicDataScratchField =
        typeof(ProduceRequest).GetField("_topicDataScratch",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

    private static readonly System.Reflection.FieldInfo TopicDataScratchCountField =
        typeof(ProduceRequest).GetField("_topicDataScratchCount",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

    private static readonly System.Reflection.FieldInfo PartitionDataScratchField =
        typeof(ProduceRequestTopicData).GetField("_partitionDataScratch",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

    private static readonly System.Reflection.FieldInfo PartitionDataScratchStartField =
        typeof(ProduceRequestTopicData).GetField("_partitionDataScratchStart",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

    private static readonly System.Reflection.FieldInfo PartitionDataScratchCountField =
        typeof(ProduceRequestTopicData).GetField("_partitionDataScratchCount",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

    private static IEnumerable<(string TopicName, ProduceRequestPartitionData Partition)> EnumeratePartitions(
        ProduceRequest request)
    {
        if (TopicDataScratchField.GetValue(request) is ProduceRequestTopicData[] topicScratch)
        {
            var topicCount = (int)TopicDataScratchCountField.GetValue(request)!;
            for (var t = 0; t < topicCount; t++)
            {
                foreach (var partition in EnumerateTopicPartitions(topicScratch[t]))
                    yield return (topicScratch[t].Name, partition);
            }
        }
        else
        {
            foreach (var topic in request.TopicData)
            {
                foreach (var partition in EnumerateTopicPartitions(topic))
                    yield return (topic.Name, partition);
            }
        }
    }

    private static IEnumerable<ProduceRequestPartitionData> EnumerateTopicPartitions(
        ProduceRequestTopicData topic)
    {
        if (PartitionDataScratchField.GetValue(topic) is ProduceRequestPartitionData[] partitionScratch)
        {
            var start = (int)PartitionDataScratchStartField.GetValue(topic)!;
            var count = (int)PartitionDataScratchCountField.GetValue(topic)!;
            for (var p = 0; p < count; p++)
                yield return partitionScratch[start + p];
        }
        else
        {
            foreach (var partition in topic.PartitionData)
                yield return partition;
        }
    }
}
