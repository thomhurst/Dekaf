using System.Reflection;
using Dekaf.Errors;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// A transactional producer whose produce requests fail, driven end to end: the real
/// <see cref="BrokerSender"/> answers each batch from a scripted broker that is also the transaction
/// coordinator, and the transaction state that results decides what commit, abort, SendOffsets and
/// the next transaction may do. A fence is fatal; every other failed batch (including a rejected
/// epoch, KIP-588) makes the transaction abortable, as in Java's
/// <c>TransactionManager.maybeTransitionToErrorState</c>.
/// </summary>
public sealed class TransactionalProduceFaultTests
{
    private const string Topic = "orders";

    [Test]
    [Arguments((short)1)]
    [Arguments((short)2)]
    [Timeout(60_000)]
    public async Task CommitAsync_AfterABatchFailedTerminally_ThrowsAbortableWithTheFailureAndSendsNoCommit(
        short transactionVersion,
        CancellationToken cancellationToken)
    {
        await using var harness = await TransactionalProduceHarness.CreateAsync(
            transactionVersion,
            produceError: static (partition, _) => partition == 1 ? ErrorCode.MessageTooLarge : ErrorCode.None);

        await using var transaction = harness.Producer.BeginTransaction();
        await transaction.ProduceAsync(Message(partition: 0), cancellationToken);
        // The caller does not await this produce: only the transaction can report the loss.
        var unobserved = transaction.ProduceAsync(Message(partition: 1), cancellationToken).AsTask();

        var commitException = await Assert.That(async () => await transaction.CommitAsync(cancellationToken))
            .Throws<AbortableTransactionException>();
        await Assert.That(commitException!.ErrorCode).IsEqualTo(ErrorCode.MessageTooLarge);
        // The failed batch's own error, so the caller can see which records were lost and why.
        await Assert.That(commitException.InnerException).IsTypeOf<ProduceException>();
        await Assert.That(async () => await unobserved).Throws<ProduceException>();
        await Assert.That(harness.Broker.CommitRequests).IsEqualTo(0);

        await transaction.AbortAsync(cancellationToken);
        await Assert.That(harness.Broker.AbortRequests).IsEqualTo(1);
        await harness.CommitOneRecordAsync(partition: 0, cancellationToken);
    }

    [Test]
    [Timeout(60_000)]
    public async Task CommitAsync_AfterABatchExceededTheDeliveryTimeout_ThrowsAbortable(
        CancellationToken cancellationToken)
    {
        await using var harness = await TransactionalProduceHarness.CreateAsync(
            transactionVersion: 2,
            produceError: static (partition, _) => partition == 1 ? ErrorCode.NotEnoughReplicas : ErrorCode.None,
            deliveryTimeoutMs: 1_000);

        await using var transaction = harness.Producer.BeginTransaction();
        await transaction.ProduceAsync(Message(partition: 0), cancellationToken);
        var unobserved = transaction.ProduceAsync(Message(partition: 1), cancellationToken).AsTask();

        var commitException = await Assert.That(async () => await transaction.CommitAsync(cancellationToken))
            .Throws<AbortableTransactionException>();
        await Assert.That(commitException!.InnerException).IsTypeOf<KafkaTimeoutException>();
        await Assert.That(async () => await unobserved).Throws<KafkaTimeoutException>();
        await Assert.That(harness.Broker.CommitRequests).IsEqualTo(0);
    }

    /// <summary>
    /// A fence on the produce path is fatal at once, and the rejected stamp is not resent until
    /// the delivery timeout.
    /// </summary>
    [Test]
    [Arguments((short)1)]
    [Arguments((short)2)]
    [Timeout(60_000)]
    public async Task ProduceAsync_ProducerFencedByTheBroker_FailsOnceAndTheProducerIsFatal(
        short transactionVersion,
        CancellationToken cancellationToken)
    {
        await using var harness = await TransactionalProduceHarness.CreateAsync(
            transactionVersion,
            produceError: static (_, _) => ErrorCode.ProducerFenced,
            // Long enough that a retry-until-delivery-timeout would outlast the test.
            deliveryTimeoutMs: 120_000);

        var transaction = harness.Producer.BeginTransaction();
        await Assert.That(async () => await transaction.ProduceAsync(Message(partition: 0), cancellationToken))
            .Throws<FatalTransactionException>();

        await Assert.That(harness.Broker.ProduceAttempts(partition: 0)).IsEqualTo(1);
        await Assert.That(async () => await transaction.CommitAsync(cancellationToken))
            .Throws<FatalTransactionException>();
        await Assert.That(harness.Broker.CommitRequests).IsEqualTo(0);
        await Assert.That(() => harness.Producer.BeginTransaction())
            .Throws<FatalTransactionException>();
    }

    /// <summary>
    /// A rejected epoch (which on a produce can also mean the coordinator timed the transaction
    /// out, KIP-588) or lost sequence state cannot be cured by resending the same stamp, and no
    /// record of the batch is in the transaction: the batch fails once, the transaction must
    /// abort, and after the abort the producer is usable again.
    /// </summary>
    [Test]
    [Arguments((short)1, ErrorCode.InvalidProducerEpoch)]
    [Arguments((short)2, ErrorCode.InvalidProducerEpoch)]
    [Arguments((short)2, ErrorCode.OutOfOrderSequenceNumber)]
    [Arguments((short)2, ErrorCode.UnknownProducerId)]
    [Timeout(60_000)]
    public async Task ProduceAsync_StampRejected_FailsOnceAndTheTransactionMustAbort(
        short transactionVersion,
        ErrorCode error,
        CancellationToken cancellationToken)
    {
        var failures = 0;
        await using var harness = await TransactionalProduceHarness.CreateAsync(
            transactionVersion,
            produceError: (_, _) => Interlocked.Increment(ref failures) == 1 ? error : ErrorCode.None,
            deliveryTimeoutMs: 120_000);

        await using var transaction = harness.Producer.BeginTransaction();
        var produceException = await Assert.That(
                async () => await transaction.ProduceAsync(Message(partition: 0), cancellationToken))
            .Throws<AbortableTransactionException>();
        await Assert.That(produceException!.ErrorCode).IsEqualTo(error);

        await Assert.That(harness.Broker.ProduceAttempts(partition: 0)).IsEqualTo(1);
        await Assert.That(async () => await transaction.CommitAsync(cancellationToken))
            .Throws<AbortableTransactionException>();
        await Assert.That(harness.Broker.CommitRequests).IsEqualTo(0);

        await transaction.AbortAsync(cancellationToken);
        await Assert.That(harness.Broker.AbortRequests).IsEqualTo(1);
        await harness.CommitOneRecordAsync(partition: 0, cancellationToken);
    }

    /// <summary>
    /// Offsets sent into an abortable transaction would be lost with it: SendOffsetsToTransaction
    /// refuses before contacting the coordinator (Java parity).
    /// </summary>
    [Test]
    [Timeout(60_000)]
    public async Task SendOffsetsToTransactionAsync_InAbortableError_ThrowsWithoutContactingTheCoordinator(
        CancellationToken cancellationToken)
    {
        await using var harness = await TransactionalProduceHarness.CreateAsync(
            transactionVersion: 2,
            produceError: static (_, _) => ErrorCode.MessageTooLarge);

        await using var transaction = harness.Producer.BeginTransaction();
        await Assert.That(async () => await transaction.ProduceAsync(Message(partition: 0), cancellationToken))
            .Throws<ProduceException>();
        var requestsBefore = harness.Broker.CoordinatorRequests;

        var exception = await Assert.That(async () => await transaction.SendOffsetsToTransactionAsync(
                [new TopicPartitionOffset("input", 0, 42)],
                "consumer-group",
                cancellationToken))
            .Throws<AbortableTransactionException>();
        await Assert.That(exception!.InnerException).IsTypeOf<ProduceException>();
        await Assert.That(harness.Broker.CoordinatorRequests).IsEqualTo(requestsBefore);
    }

    /// <summary>
    /// Every failure path of a pooled batch ends in the batch's own failure completion (a produce
    /// error, the delivery timeout, a reroute that finds no leader, a local rejection, a purge,
    /// disposal), so the transaction is told there exactly once per batch, with the batch's
    /// stamp, before any record completes.
    /// </summary>
    [Test]
    public async Task ReadyBatchFailure_ReportsOnceWithItsStampBeforeTheRecordsComplete()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            TransactionalId = "txn-produce-fault"
        };
        await using var accumulator = new RecordAccumulator(options);
        await using var sourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var sources = System.Buffers.ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(1);
        sources[0] = sourcePool.Rent();
        var completion = sources[0].Task;
        var reports = new List<(long ProducerId, short Epoch, ErrorCode ErrorCode, Exception Failure, bool Completed)>();
        accumulator.OnTransactionalBatchFailed = (producerId, epoch, errorCode, failure) =>
            reports.Add((producerId, epoch, errorCode, failure, completion.IsCompleted));

        var batch = new ReadyBatch(accumulator);
        batch.Initialize(
            new TopicPartition(Topic, 0),
            new Dekaf.Protocol.Records.RecordBatch
            {
                Records = Array.Empty<Dekaf.Protocol.Records.Record>(),
                ProducerId = 42,
                ProducerEpoch = 5
            },
            sources,
            completionSourcesCount: 1,
            recordCount: 1,
            dataSize: 100);
        batch.MarkPreSerialized();
        var leaderLost = new KafkaException(ErrorCode.LeaderNotAvailable, "No leader available for orders-0");

        batch.Fail(leaderLost);
        batch.Fail(new ObjectDisposedException("again"));

        await Assert.That(reports.Count).IsEqualTo(1);
        await Assert.That(reports[0].ProducerId).IsEqualTo(42L);
        await Assert.That(reports[0].Epoch).IsEqualTo((short)5);
        await Assert.That(reports[0].ErrorCode).IsEqualTo(ErrorCode.LeaderNotAvailable);
        await Assert.That(reports[0].Failure).IsSameReferenceAs(leaderLost);
        await Assert.That(reports[0].Completed).IsFalse();
        await Assert.That(async () => await completion).Throws<KafkaException>();
    }

    private static ProducerMessage<string, string> Message(int partition) => new()
    {
        Topic = Topic,
        Key = "key",
        Value = "value",
        Partition = partition
    };

    private sealed class TransactionalProduceHarness : IAsyncDisposable
    {
        private readonly ConnectionPool _pool;
        private readonly MetadataManager _metadata;

        private TransactionalProduceHarness(
            ConnectionPool pool,
            MetadataManager metadata,
            ScriptedTransactionalBroker broker,
            KafkaProducer<string, string> producer)
        {
            _pool = pool;
            _metadata = metadata;
            Broker = broker;
            Producer = producer;
        }

        public ScriptedTransactionalBroker Broker { get; }

        public KafkaProducer<string, string> Producer { get; }

        public static async Task<TransactionalProduceHarness> CreateAsync(
            short transactionVersion,
            Func<int, int, ErrorCode> produceError,
            int deliveryTimeoutMs = 30_000,
            int lingerMs = 0)
        {
            var broker = new ScriptedTransactionalBroker(produceError, bumpsEpochAtEndTxn: transactionVersion >= 2);
            var pool = new ConnectionPool(
                "txn-produce-fault-test",
                new ConnectionOptions { ReconnectBackoff = TimeSpan.Zero },
                1,
                (_, _, _, _, _) => new ValueTask<IKafkaConnection>(broker));
            pool.RegisterBroker(1, "localhost", 9092);

            var metadata = new MetadataManager(pool, ["localhost:9092"]);
            metadata.Metadata.Update(new MetadataResponse
            {
                Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
                Topics =
                [
                    new TopicMetadata
                    {
                        ErrorCode = ErrorCode.None,
                        Name = Topic,
                        TopicId = Guid.NewGuid(),
                        Partitions =
                        [
                            Partition(0),
                            Partition(1)
                        ]
                    }
                ]
            });
            typeof(MetadataManager)
                .GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(metadata, true);
            metadata.SetApiVersion(ApiKey.Produce, ProduceRequest.LowestSupportedVersion,
                ProduceRequest.ImplicitTransactionPartitionEnrollmentVersion);
            metadata.SetApiVersion(ApiKey.FindCoordinator, FindCoordinatorRequest.LowestSupportedVersion,
                FindCoordinatorRequest.HighestSupportedVersion);
            metadata.SetApiVersion(ApiKey.InitProducerId, InitProducerIdRequest.LowestSupportedVersion,
                InitProducerIdRequest.HighestSupportedVersion);
            metadata.SetApiVersion(ApiKey.AddPartitionsToTxn, AddPartitionsToTxnRequest.LowestSupportedVersion,
                AddPartitionsToTxnRequest.HighestSupportedVersion);
            metadata.SetApiVersion(ApiKey.EndTxn, EndTxnRequest.LowestSupportedVersion,
                EndTxnRequest.HighestSupportedVersion);
            metadata.SetApiVersion(ApiKey.Metadata, MetadataRequest.LowestSupportedVersion,
                MetadataRequest.HighestSupportedVersion);
            metadata.ObserveClusterCapabilities(
                "cluster-a",
                KafkaConnectionCapabilities.Create(new ApiVersionsResponse
                {
                    ErrorCode = ErrorCode.None,
                    ApiKeys = [],
                    FinalizedFeaturesEpoch = 1,
                    FinalizedFeatures = [new FinalizedFeature("transaction.version", transactionVersion, transactionVersion)]
                }));

            var producer = new KafkaProducer<string, string>(
                new ProducerOptions
                {
                    BootstrapServers = ["localhost:9092"],
                    TransactionalId = "txn-produce-fault",
                    LingerMs = lingerMs,
                    DeliveryTimeoutMs = deliveryTimeoutMs,
                    RequestTimeoutMs = Math.Min(deliveryTimeoutMs, 30_000),
                    RetryBackoffMs = 10,
                    RetryBackoffMaxMs = 10,
                    MaxBlockMs = 10_000,
                    CloseTimeoutMs = 1_000
                },
                Serializers.String,
                Serializers.String,
                pool,
                metadata,
                DekafMemoryBudget.Global);

            var harness = new TransactionalProduceHarness(pool, metadata, broker, producer);
            await producer.InitializeAsync();
            await producer.InitTransactionsAsync();
            return harness;
        }

        /// <summary>
        /// Commits a fresh transaction of one record: the producer is usable and its identity current.
        /// </summary>
        public async Task CommitOneRecordAsync(int partition, CancellationToken cancellationToken)
        {
            var succeed = Broker.ProduceError;
            Broker.ProduceError = static (_, _) => ErrorCode.None;
            try
            {
                var commitsBefore = Broker.CommitRequests;
                await using var transaction = Producer.BeginTransaction();
                await transaction.ProduceAsync(Message(partition), cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                await Assert.That(Broker.CommitRequests).IsEqualTo(commitsBefore + 1);
            }
            finally
            {
                Broker.ProduceError = succeed;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Producer.DisposeAsync();
            await _metadata.DisposeAsync();
            await _pool.DisposeAsync();
        }

        private static PartitionMetadata Partition(int index) => new()
        {
            ErrorCode = ErrorCode.None,
            PartitionIndex = index,
            LeaderId = 1,
            LeaderEpoch = 0,
            ReplicaNodes = [1],
            IsrNodes = [1]
        };
    }

    internal readonly record struct ProducedBatch(
        int Partition,
        long ProducerId,
        short ProducerEpoch,
        int BaseSequence,
        int RecordCount);

    /// <summary>
    /// One broker that is also the transaction coordinator. It fences the producer epoch the way
    /// Kafka does: EndTxn under transaction version 2 and InitProducerId both bump it.
    /// </summary>
    private sealed class ScriptedTransactionalBroker(Func<int, int, ErrorCode> produceError, bool bumpsEpochAtEndTxn) :
        IKafkaConnection,
        IRetirableKafkaConnection,
        IKafkaPipelinedWriteCompletionConnection,
        IKafkaRequestWriteObserverConnection
    {
        private const long ProducerIdValue = 4242;

        private readonly object _sync = new();
        private readonly List<ProducedBatch> _produced = [];
        private readonly Dictionary<int, int> _attemptsByPartition = [];
        private readonly TaskCompletionSource _abortReceived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<(int Count, TaskCompletionSource Signal)> _produceWaiters = [];
        private int _produceRequests;
        private short _epoch = -1;
        private int _commitRequests;
        private int _abortRequests;
        private int _coordinatorRequests;

        public Func<int, int, ErrorCode> ProduceError { get; set; } = produceError;

        /// <summary>Delays the answer to the n-th produce request (1-based) until the task completes.</summary>
        public Func<int, Task?>? HoldProduceResponse { get; set; }

        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;

        public int CommitRequests => Volatile.Read(ref _commitRequests);
        public int AbortRequests => Volatile.Read(ref _abortRequests);
        public int CoordinatorRequests => Volatile.Read(ref _coordinatorRequests);

        public short CurrentEpoch
        {
            get
            {
                lock (_sync)
                    return _epoch;
            }
        }

        public IReadOnlyList<ProducedBatch> ProducedBatches
        {
            get
            {
                lock (_sync)
                    return [.. _produced];
            }
        }

        public int ProduceAttempts(int partition)
        {
            lock (_sync)
                return _attemptsByPartition.GetValueOrDefault(partition);
        }

        public Task WaitForAbortAsync(CancellationToken cancellationToken)
            => _abortReceived.Task.WaitAsync(cancellationToken);

        public Task WaitForProduceAttemptsAsync(int count, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                if (_produceRequests >= count)
                    return Task.CompletedTask;

                var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _produceWaiters.Add((count, signal));
                return signal.Task.WaitAsync(cancellationToken);
            }
        }

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            if (request is not MetadataRequest)
                Interlocked.Increment(ref _coordinatorRequests);

            IKafkaResponse response = request switch
            {
                FindCoordinatorRequest find => new FindCoordinatorResponse
                {
                    Coordinators =
                    [
                        new Coordinator
                        {
                            Key = find.Key,
                            NodeId = 1,
                            Host = "localhost",
                            Port = 9092,
                            ErrorCode = ErrorCode.None
                        }
                    ]
                },
                InitProducerIdRequest => InitProducerId(),
                AddPartitionsToTxnRequest add => new AddPartitionsToTxnResponse
                {
                    Results = add.Topics.Select(topic => new AddPartitionsToTxnTopicResult
                    {
                        Name = topic.Name,
                        Partitions = topic.Partitions.Select(partition => new AddPartitionsToTxnPartitionResult
                        {
                            PartitionIndex = partition,
                            ErrorCode = ErrorCode.None
                        }).ToArray()
                    }).ToArray()
                },
                EndTxnRequest endTxn => EndTxn(endTxn),
                MetadataRequest => new MetadataResponse
                {
                    Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
                    Topics = []
                },
                _ => throw new NotSupportedException(typeof(TRequest).Name)
            };

            return ValueTask.FromResult((TResponse)response);
        }

        public ValueTask<TResponse> SendWithWriteObservationAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            Action requestWriteStarted,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            requestWriteStarted();
            return SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
        }

        public async ValueTask<PipelinedResponse<TResponse>> SendPipelinedAfterWriteAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            if (request is not ProduceRequest produce)
            {
                var response = await SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken)
                    .ConfigureAwait(false);
                return new PipelinedResponse<TResponse>(Task.FromResult(response));
            }

            return new PipelinedResponse<TResponse>(AnswerProduceAsync<TResponse>(produce));
        }

        public ValueTask<PipelinedResponse<TResponse>> SendPipelinedWithWriteObservationAfterWriteAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            Action requestWriteStarted,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            requestWriteStarted();
            return SendPipelinedAfterWriteAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
        }

        public ValueTask<PipelinedResponse<TResponse>> SendPipelinedWithCallerTimeoutAfterWriteAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => SendPipelinedAfterWriteAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        private async Task<TResponse> AnswerProduceAsync<TResponse>(ProduceRequest produce)
            where TResponse : IKafkaResponse
        {
            // Everything the answer needs is read before the first await: the sender reuses the
            // request's structures once the write completes.
            var partitions = new List<(int Partition, ErrorCode Error)>();
            int requestNumber;
            List<TaskCompletionSource>? ready = null;
            lock (_sync)
            {
                requestNumber = ++_produceRequests;
                for (var t = 0; t < produce.TopicEntryCount; t++)
                {
                    var topic = produce.GetTopicEntry(t);
                    for (var p = 0; p < topic.PartitionEntryCount; p++)
                    {
                        var entry = topic.GetPartitionEntry(p);
                        _attemptsByPartition[entry.Index] = _attemptsByPartition.GetValueOrDefault(entry.Index) + 1;
                        foreach (var batch in entry.Records)
                        {
                            _produced.Add(new ProducedBatch(
                                entry.Index,
                                batch.ProducerId,
                                batch.ProducerEpoch,
                                batch.BaseSequence,
                                batch.Records.Count));
                        }

                        partitions.Add((entry.Index, ProduceError(entry.Index, requestNumber)));
                    }
                }

                for (var i = _produceWaiters.Count - 1; i >= 0; i--)
                {
                    if (_produceRequests < _produceWaiters[i].Count)
                        continue;

                    (ready ??= []).Add(_produceWaiters[i].Signal);
                    _produceWaiters.RemoveAt(i);
                }
            }

            if (ready is not null)
            {
                foreach (var signal in ready)
                    signal.TrySetResult();
            }

            if (HoldProduceResponse?.Invoke(requestNumber) is { } hold)
                await hold.ConfigureAwait(false);

            var response = new ProduceResponse
            {
                TopicCount = 1,
                Responses =
                [
                    new ProduceResponseTopicData
                    {
                        Name = Topic,
                        PartitionCount = partitions.Count,
                        PartitionResponses = partitions.Select(partition => new ProduceResponsePartitionData
                        {
                            Index = partition.Partition,
                            ErrorCode = partition.Error,
                            BaseOffset = partition.Error == ErrorCode.None ? 0 : -1
                        }).ToArray()
                    }
                ]
            };
            return (TResponse)(IKafkaResponse)response;
        }

        private InitProducerIdResponse InitProducerId()
        {
            lock (_sync)
            {
                _epoch++;
                return new InitProducerIdResponse
                {
                    ErrorCode = ErrorCode.None,
                    ProducerId = ProducerIdValue,
                    ProducerEpoch = _epoch
                };
            }
        }

        private EndTxnResponse EndTxn(EndTxnRequest request)
        {
            if (request.Committed)
            {
                Interlocked.Increment(ref _commitRequests);
            }
            else
            {
                Interlocked.Increment(ref _abortRequests);
                _abortReceived.TrySetResult();
            }

            lock (_sync)
            {
                // Transaction version 2 bumps the epoch at every transaction end and returns it.
                if (bumpsEpochAtEndTxn)
                    _epoch++;
                return new EndTxnResponse
                {
                    ErrorCode = ErrorCode.None,
                    ProducerId = ProducerIdValue,
                    ProducerEpoch = _epoch
                };
            }
        }

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        int IRetirableKafkaConnection.LeaseCount => 0;
        int IRetirableKafkaConnection.ActiveOperationCount => 0;
        bool IRetirableKafkaConnection.TryAcquireLease() => true;
        void IRetirableKafkaConnection.ReleaseLease() { }
        void IRetirableKafkaConnection.BeginRetirement() { }
        void IRetirableKafkaConnection.CompleteRetirement() { }
    }
}
