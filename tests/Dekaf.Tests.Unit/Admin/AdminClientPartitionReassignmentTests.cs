using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientPartitionReassignmentTests
{
    [Test]
    public async Task AlterPartitionReassignmentsAsync_SendsControllerRequest()
    {
        var (admin, connection) = CreateAdminWithMockConnection();
        AlterPartitionReassignmentsRequest? capturedRequest = null;

        connection.SendAsync<AlterPartitionReassignmentsRequest, AlterPartitionReassignmentsResponse>(
                Arg.Any<AlterPartitionReassignmentsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedRequest = callInfo.Arg<AlterPartitionReassignmentsRequest>();
                return ValueTask.FromResult(CreateAlterResponse(ErrorCode.None));
            });

        await admin.AlterPartitionReassignmentsAsync(
            new Dictionary<TopicPartition, Optional<NewPartitionReassignment>>
            {
                [new("test-topic", 1)] = NewPartitionReassignment.ToReplicas(3, 2, 1),
                [new("test-topic", 0)] = Optional.None<NewPartitionReassignment>()
            },
            new AlterPartitionReassignmentsOptions
            {
                TimeoutMs = 123,
                AllowReplicationFactorChange = false
            });

        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.TimeoutMs).IsEqualTo(123);
        await Assert.That(capturedRequest.AllowReplicationFactorChange).IsFalse();
        await Assert.That(capturedRequest.Topics.Count).IsEqualTo(1);
        await Assert.That(capturedRequest.Topics[0].Name).IsEqualTo("test-topic");
        await Assert.That(capturedRequest.Topics[0].Partitions[0].PartitionIndex).IsEqualTo(0);
        await Assert.That(capturedRequest.Topics[0].Partitions[0].Replicas).IsNull();
        await Assert.That(capturedRequest.Topics[0].Partitions[1].PartitionIndex).IsEqualTo(1);
        await Assert.That(capturedRequest.Topics[0].Partitions[1].Replicas).IsEquivalentTo([3, 2, 1]);
    }

    [Test]
    public async Task AlterPartitionReassignmentsAsync_PartitionError_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection();

        connection.SendAsync<AlterPartitionReassignmentsRequest, AlterPartitionReassignmentsResponse>(
                Arg.Any<AlterPartitionReassignmentsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateAlterResponse(ErrorCode.InvalidReplicaAssignment)));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.AlterPartitionReassignmentsAsync(
                new Dictionary<TopicPartition, Optional<NewPartitionReassignment>>
                {
                    [new("test-topic", 0)] = NewPartitionReassignment.ToReplicas(1, 2)
                }));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.InvalidReplicaAssignment);
    }

    [Test]
    public async Task AlterPartitionReassignmentsAsync_Retry_ToleratesAlreadyAppliedResult()
    {
        var (admin, connection) = CreateAdminWithMockConnection();
        var originalReplicas = new List<int> { 1, 2 };
        var sendCalls = 0;
        AlterPartitionReassignmentsRequest? retryRequest = null;

        connection.SendAsync<AlterPartitionReassignmentsRequest, AlterPartitionReassignmentsResponse>(
                Arg.Any<AlterPartitionReassignmentsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                sendCalls++;
                if (sendCalls == 1)
                {
                    originalReplicas.Clear();
                    originalReplicas.Add(9);
                    throw new KafkaException(ErrorCode.RequestTimedOut, "simulated timeout");
                }

                retryRequest = callInfo.Arg<AlterPartitionReassignmentsRequest>();
                return ValueTask.FromResult(CreateAlterResponse(ErrorCode.ReassignmentInProgress));
            });

        await admin.AlterPartitionReassignmentsAsync(
            new Dictionary<TopicPartition, Optional<NewPartitionReassignment>>
            {
                [new("test-topic", 0)] = new NewPartitionReassignment { TargetReplicas = originalReplicas }
            });

        await Assert.That(sendCalls).IsEqualTo(2);
        await Assert.That(retryRequest!.Topics[0].Partitions[0].Replicas).IsEquivalentTo([1, 2]);
    }

    [Test]
    public async Task ListPartitionReassignmentsAsync_MapsResponseToResult()
    {
        var (admin, connection) = CreateAdminWithMockConnection();
        ListPartitionReassignmentsRequest? capturedRequest = null;

        connection.SendAsync<ListPartitionReassignmentsRequest, ListPartitionReassignmentsResponse>(
                Arg.Any<ListPartitionReassignmentsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedRequest = callInfo.Arg<ListPartitionReassignmentsRequest>();
                return ValueTask.FromResult(new ListPartitionReassignmentsResponse
                {
                    ErrorCode = ErrorCode.None,
                    Topics =
                    [
                        new ListPartitionReassignmentsResponseTopic
                        {
                            Name = "test-topic",
                            Partitions =
                            [
                                new OngoingPartitionReassignmentData
                                {
                                    PartitionIndex = 0,
                                    Replicas = [1, 2],
                                    AddingReplicas = [3],
                                    RemovingReplicas = [1]
                                }
                            ]
                        }
                    ]
                });
            });

        var result = await admin.ListPartitionReassignmentsAsync([new TopicPartition("test-topic", 0)]);

        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.Topics).IsNotNull();
        await Assert.That(capturedRequest.Topics![0].PartitionIndexes).IsEquivalentTo([0]);
        await Assert.That(result[new TopicPartition("test-topic", 0)].Replicas).IsEquivalentTo([1, 2]);
        await Assert.That(result[new TopicPartition("test-topic", 0)].AddingReplicas).IsEquivalentTo([3]);
        await Assert.That(result[new TopicPartition("test-topic", 0)].RemovingReplicas).IsEquivalentTo([1]);
    }

    [Test]
    public async Task ListPartitionReassignmentsAsync_TopLevelError_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection();

        connection.SendAsync<ListPartitionReassignmentsRequest, ListPartitionReassignmentsResponse>(
                Arg.Any<ListPartitionReassignmentsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ListPartitionReassignmentsResponse
            {
                ErrorCode = ErrorCode.ClusterAuthorizationFailed,
                ErrorMessage = "not authorized",
                Topics = []
            }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.ListPartitionReassignmentsAsync());

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.ClusterAuthorizationFailed);
    }

    private static AlterPartitionReassignmentsResponse CreateAlterResponse(ErrorCode partitionError) => new()
    {
        ErrorCode = ErrorCode.None,
        Responses =
        [
            new AlterPartitionReassignmentsResponseTopic
            {
                Name = "test-topic",
                Partitions =
                [
                    new AlterPartitionReassignmentsResponsePartition
                    {
                        PartitionIndex = 0,
                        ErrorCode = partitionError
                    }
                ]
            }
        ]
    };

    private static (AdminClient Admin, IKafkaConnection Connection) CreateAdminWithMockConnection()
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.BrokerId.Returns(1);
        connection.Host.Returns("localhost");
        connection.Port.Returns(9092);
        connection.IsConnected.Returns(true);

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));

        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        metadataManager.Metadata.Update(CreateMetadataResponse());
        metadataManager.SetApiVersion(ApiKey.AlterPartitionReassignments, 0, 1);
        metadataManager.SetApiVersion(ApiKey.ListPartitionReassignments, 0, 0);
        metadataManager.SetApiVersion(ApiKey.Metadata, 9, 13);

        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateMetadataResponse()));

        connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(ApiKey.Metadata, 9, 13),
                    new ApiVersion(ApiKey.AlterPartitionReassignments, 0, 1),
                    new ApiVersion(ApiKey.ListPartitionReassignments, 0, 0)
                ]
            }));

        var admin = new AdminClient(
            new AdminClientOptions { BootstrapServers = ["localhost:9092"] },
            pool,
            metadataManager);

        return (admin, connection);
    }

    private static MetadataResponse CreateMetadataResponse() => new()
    {
        Brokers =
        [
            new BrokerMetadata
            {
                NodeId = 1,
                Host = "localhost",
                Port = 9092
            }
        ],
        ClusterId = "test-cluster",
        ControllerId = 1,
        Topics = []
    };
}
