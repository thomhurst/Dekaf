using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

public sealed class TransactionOffsetCommitTests
{
    private static readonly Guid TopicId = new("00112233-4455-6677-8899-aabbccddeeff");

    [Test]
    public async Task TV2_WithV6_UsesTopicIdAndConsumerMembership()
    {
        await using var harness = CreateHarness(
            transactionVersion: 2,
            txnOffsetCommitMaxVersion: 6,
            topicId: TopicId);
        var groupMetadata = new ConsumerGroupMetadata
        {
            GroupId = "group-1",
            GenerationId = 7,
            MemberId = "member-1",
            GroupInstanceId = "instance-1"
        };

        await harness.Producer.SendOffsetsToTransactionInternalAsync(
            [new TopicPartitionOffset("orders", 0, 42, leaderEpoch: 3) { Metadata = "metadata-1" }],
            groupMetadata,
            CancellationToken.None);

        var request = harness.Connection.CommitRequests.Single();
        await Assert.That(request.GenerationIdOrMemberEpoch).IsEqualTo(7);
        await Assert.That(request.MemberId).IsEqualTo("member-1");
        await Assert.That(request.GroupInstanceId).IsEqualTo("instance-1");
        await Assert.That(request.Topics.Single().TopicId).IsEqualTo(TopicId);
        await Assert.That(request.Topics.Single().Partitions.Single().CommittedLeaderEpoch).IsEqualTo(3);
        await Assert.That(request.Topics.Single().Partitions.Single().CommittedMetadata).IsEqualTo("metadata-1");
        await Assert.That(harness.Connection.Requests
                .Select(static recorded => (recorded.ApiKey, recorded.ApiVersion))
                .SequenceEqual(
            [
                (ApiKey.FindCoordinator, (short)5),
                (ApiKey.TxnOffsetCommit, (short)6)
            ]))
            .IsTrue();
    }

    [Test]
    public async Task TV2_WithV6ButUnavailableTopicId_FallsBackToV5()
    {
        await using var harness = CreateHarness(transactionVersion: 2, txnOffsetCommitMaxVersion: 6);

        await harness.Producer.SendOffsetsToTransactionInternalAsync(
            [new TopicPartitionOffset("orders", 0, 42)],
            "group-1",
            CancellationToken.None);

        var request = harness.Connection.CommitRequests.Single();
        await Assert.That(request.Topics.Single().Name).IsEqualTo("orders");
        await Assert.That(request.Topics.Single().TopicId).IsEqualTo(Guid.Empty);
        await Assert.That(harness.Connection.Requests
                .Select(static recorded => (recorded.ApiKey, recorded.ApiVersion))
                .SequenceEqual(
            [
                (ApiKey.FindCoordinator, (short)5),
                (ApiKey.Metadata, (short)13),
                (ApiKey.TxnOffsetCommit, (short)5)
            ]))
            .IsTrue();
    }

    [Test]
    public async Task TV2_V6UnknownTopicId_RefreshesMetadataAndRetries()
    {
        var outcomes = new Queue<object>([ErrorCode.UnknownTopicId, ErrorCode.None]);
        await using var harness = CreateHarness(
            transactionVersion: 2,
            txnOffsetCommitMaxVersion: 6,
            commitOutcomes: outcomes,
            topicId: TopicId);

        await harness.Producer.SendOffsetsToTransactionInternalAsync(
            [new TopicPartitionOffset("orders", 0, 42)],
            "group-1",
            CancellationToken.None);

        await Assert.That(harness.Connection.CommitRequests).Count().IsEqualTo(2);
        await Assert.That(harness.Connection.Requests.Count(static request =>
            request.ApiKey == ApiKey.Metadata)).IsEqualTo(1);
    }

    [Test]
    [Arguments(ErrorCode.GroupIdNotFound)]
    [Arguments(ErrorCode.StaleMemberEpoch)]
    public async Task TV2_V6MembershipError_IsAbortable(ErrorCode errorCode)
    {
        var outcomes = new Queue<object>([errorCode]);
        await using var harness = CreateHarness(
            transactionVersion: 2,
            txnOffsetCommitMaxVersion: 6,
            commitOutcomes: outcomes,
            topicId: TopicId);

        var exception = await Assert.That(() => harness.Producer.SendOffsetsToTransactionInternalAsync(
                [new TopicPartitionOffset("orders", 0, 42)],
                "group-1",
                CancellationToken.None).AsTask())
            .Throws<AbortableTransactionException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(errorCode);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.AbortableError);
    }

    [Test]
    public async Task TV2_WithV5_SkipsAddOffsetsAndUsesV5()
    {
        await using var harness = CreateHarness(transactionVersion: 2, txnOffsetCommitMaxVersion: 5);

        await harness.Producer.SendOffsetsToTransactionInternalAsync(
            [new TopicPartitionOffset("orders", 0, 42)],
            "group-1",
            CancellationToken.None);

        await Assert.That(harness.Connection.Requests
                .Select(static request => (request.ApiKey, request.ApiVersion))
                .SequenceEqual(
                [
                    (ApiKey.FindCoordinator, (short)5),
                    (ApiKey.TxnOffsetCommit, (short)5)
                ]))
            .IsTrue();
    }

    [Test]
    public async Task TV1_WithV5Capability_RetainsAddOffsetsAndCapsAtV4()
    {
        await using var harness = CreateHarness(transactionVersion: 1, txnOffsetCommitMaxVersion: 5);

        await harness.Producer.SendOffsetsToTransactionInternalAsync(
            [new TopicPartitionOffset("orders", 0, 42)],
            "group-1",
            CancellationToken.None);

        await Assert.That(harness.Connection.Requests
                .Select(static request => (request.ApiKey, request.ApiVersion))
                .SequenceEqual(
                [
                    (ApiKey.AddOffsetsToTxn, (short)4),
                    (ApiKey.FindCoordinator, (short)5),
                    (ApiKey.TxnOffsetCommit, (short)4)
                ]))
            .IsTrue();
    }

    [Test]
    public async Task TV2_WithoutV5_FallsBackToAddOffsetsAndV4()
    {
        await using var harness = CreateHarness(transactionVersion: 2, txnOffsetCommitMaxVersion: 4);

        await harness.Producer.SendOffsetsToTransactionInternalAsync(
            [new TopicPartitionOffset("orders", 0, 42)],
            "group-1",
            CancellationToken.None);

        await Assert.That(harness.Connection.Requests
                .Select(static request => (request.ApiKey, request.ApiVersion))
                .SequenceEqual(
                [
                    (ApiKey.FindCoordinator, (short)5),
                    (ApiKey.AddOffsetsToTxn, (short)4),
                    (ApiKey.TxnOffsetCommit, (short)4)
                ]))
            .IsTrue();
    }

    [Test]
    public async Task TV2_LostCommitResponse_RetriesV5WithoutReenumeratingOffsets()
    {
        var outcomes = new Queue<object>([new IOException("response lost"), ErrorCode.None]);
        await using var harness = CreateHarness(
            transactionVersion: 2,
            txnOffsetCommitMaxVersion: 5,
            commitOutcomes: outcomes);
        var enumerationCount = 0;

        IEnumerable<TopicPartitionOffset> SingleUseOffsets()
        {
            enumerationCount++;
            if (enumerationCount > 1)
                throw new InvalidOperationException("Offsets were re-enumerated");

            yield return new TopicPartitionOffset("orders", 0, 42);
        }

        await harness.Producer.SendOffsetsToTransactionInternalAsync(
            SingleUseOffsets(),
            "group-1",
            CancellationToken.None);

        await Assert.That(enumerationCount).IsEqualTo(1);
        await Assert.That(harness.Connection.Requests
                .Select(static request => (request.ApiKey, request.ApiVersion))
                .SequenceEqual(
            [
                (ApiKey.FindCoordinator, (short)5),
                (ApiKey.TxnOffsetCommit, (short)5),
                (ApiKey.FindCoordinator, (short)5),
                (ApiKey.TxnOffsetCommit, (short)5)
            ]))
            .IsTrue();
    }

    [Test]
    public async Task TV2_NotCoordinator_RefreshesOnlyGroupCoordinator()
    {
        var outcomes = new Queue<object>([ErrorCode.NotCoordinator, ErrorCode.None]);
        await using var harness = CreateHarness(
            transactionVersion: 2,
            txnOffsetCommitMaxVersion: 5,
            commitOutcomes: outcomes);

        await harness.Producer.SendOffsetsToTransactionInternalAsync(
            [new TopicPartitionOffset("orders", 0, 42)],
            "group-1",
            CancellationToken.None);

        var coordinatorKeys = harness.Connection.Requests
            .Where(static request => request.ApiKey == ApiKey.FindCoordinator)
            .Select(static request => request.CoordinatorKey!)
            .ToArray();
        await Assert.That(coordinatorKeys).IsEquivalentTo(["group-1", "group-1"]);
    }

    [Test]
    public async Task TV2_TransactionAbortable_UsesKip890Classification()
    {
        var outcomes = new Queue<object>([ErrorCode.TransactionAbortable]);
        await using var harness = CreateHarness(
            transactionVersion: 2,
            txnOffsetCommitMaxVersion: 5,
            commitOutcomes: outcomes);

        var exception = await Assert.That(() => harness.Producer.SendOffsetsToTransactionInternalAsync(
                [new TopicPartitionOffset("orders", 0, 42)],
                "group-1",
                CancellationToken.None).AsTask())
            .Throws<AbortableTransactionException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.TransactionAbortable);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.AbortableError);
    }

    private static Harness CreateHarness(
        short transactionVersion,
        short txnOffsetCommitMaxVersion,
        Queue<object>? commitOutcomes = null,
        Guid topicId = default)
    {
        var connection = new RecordingConnection(txnOffsetCommitMaxVersion, commitOutcomes, topicId);
        var connectionPool = new ConnectionPool(
            "transaction-offset-tests",
            connectionOptions: null,
            connectionsPerBroker: 1,
            connectionFactory: (_, _, _, _, _) => ValueTask.FromResult<IKafkaConnection>(connection));
        connectionPool.RegisterBroker(1, "localhost", 9092);

        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.Metadata.Update(CreateMetadataResponse(topicId));
        metadataManager.ObserveClusterCapabilities(
            "cluster-a",
            KafkaConnectionCapabilities.Create(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys = [],
                FinalizedFeaturesEpoch = 1,
                FinalizedFeatures =
                [
                    new FinalizedFeature(
                        "transaction.version",
                        transactionVersion,
                        transactionVersion)
                ]
            }));

        var producer = new KafkaProducer<string, string>(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                TransactionalId = "transaction-1",
                RetryBackoffMs = 0,
                RetryBackoffMaxMs = 0,
                CloseTimeoutMs = 100,
                MaxBlockMs = 100
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager,
            DekafMemoryBudget.Global);
        SetField(producer, "_initialized", true);
        SetField(producer, "_producerId", 42L);
        SetField(producer, "_producerEpoch", (short)3);
        SetField(producer, "_transactionCoordinatorId", 1);
        SetField(producer, "_currentTransactionFeatureVersion", transactionVersion);
        producer._currentTransactionUsesTV2 = transactionVersion >= 2;
        producer._transactionState = TransactionState.InTransaction;

        return new Harness(producer, connectionPool, metadataManager, connection);
    }

    private static void SetField<T>(object target, string name, T value)
        => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(target, value);

    private sealed class Harness(
        KafkaProducer<string, string> producer,
        ConnectionPool connectionPool,
        MetadataManager metadataManager,
        RecordingConnection connection) : IAsyncDisposable
    {
        internal KafkaProducer<string, string> Producer { get; } = producer;
        internal RecordingConnection Connection { get; } = connection;

        public async ValueTask DisposeAsync()
        {
            Producer._transactionState = TransactionState.Ready;
            await Producer.DisposeAsync().ConfigureAwait(false);
            await metadataManager.DisposeAsync().ConfigureAwait(false);
            await connectionPool.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class RecordingConnection(
        short txnOffsetCommitMaxVersion,
        Queue<object>? commitOutcomes,
        Guid topicId) : IKafkaConnection, IKafkaCapabilityProvider
    {
        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public KafkaConnectionCapabilities Capabilities { get; } =
            KafkaConnectionCapabilities.Create(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(ApiKey.AddOffsetsToTxn, 3, 4),
                    new ApiVersion(ApiKey.FindCoordinator, 4, 5),
                    new ApiVersion(ApiKey.Metadata, 9, 13),
                    new ApiVersion(ApiKey.TxnOffsetCommit, 3, txnOffsetCommitMaxVersion)
                ]
            });
        internal List<RecordedRequest> Requests { get; } = [];
        internal List<TxnOffsetCommitRequest> CommitRequests { get; } = [];

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            Requests.Add(new RecordedRequest(
                KafkaMessageMetadata<TRequest, TResponse>.ApiKey,
                apiVersion,
                request is FindCoordinatorRequest coordinatorRequest ? coordinatorRequest.Key : null));

            IKafkaResponse response = request switch
            {
                AddOffsetsToTxnRequest => new AddOffsetsToTxnResponse { ErrorCode = ErrorCode.None },
                FindCoordinatorRequest findRequest => new FindCoordinatorResponse
                {
                    Coordinators =
                    [
                        new Coordinator
                        {
                            Key = findRequest.Key,
                            NodeId = 1,
                            Host = "localhost",
                            Port = 9092,
                            ErrorCode = ErrorCode.None
                        }
                    ]
                },
                MetadataRequest => CreateMetadataResponse(topicId),
                TxnOffsetCommitRequest txnOffsetCommit => CreateCommitResponse(txnOffsetCommit),
                _ => throw new NotSupportedException(typeof(TRequest).Name)
            };

            return ValueTask.FromResult((TResponse)response);
        }

        private TxnOffsetCommitResponse CreateCommitResponse(TxnOffsetCommitRequest request)
        {
            CommitRequests.Add(request);
            var outcome = commitOutcomes is { Count: > 0 }
                ? commitOutcomes.Dequeue()
                : ErrorCode.None;
            if (outcome is Exception exception)
                throw exception;

            var errorCode = (ErrorCode)outcome;
            return new TxnOffsetCommitResponse
            {
                Topics = request.Topics.Select(topic => new TxnOffsetCommitResponseTopic
                {
                    Name = topic.Name,
                    TopicId = topic.TopicId,
                    Partitions = topic.Partitions.Select(partition =>
                        new TxnOffsetCommitResponsePartition
                        {
                            PartitionIndex = partition.PartitionIndex,
                            ErrorCode = errorCode
                        }).ToArray()
                }).ToArray()
            };
        }

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

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
    }

    private readonly record struct RecordedRequest(
        ApiKey ApiKey,
        short ApiVersion,
        string? CoordinatorKey);

    private static MetadataResponse CreateMetadataResponse(Guid topicId) => new()
    {
        Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
        Topics =
        [
            new TopicMetadata
            {
                ErrorCode = ErrorCode.None,
                Name = "orders",
                TopicId = topicId,
                Partitions =
                [
                    new PartitionMetadata
                    {
                        ErrorCode = ErrorCode.None,
                        PartitionIndex = 0,
                        LeaderId = 1,
                        ReplicaNodes = [1],
                        IsrNodes = [1]
                    }
                ]
            }
        ]
    };
}
