using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientDeleteShareGroupsTests
{
    private const string FirstGroupId = "share-a";
    private const string SecondGroupId = "share-b";

    [Test]
    public async Task DeleteShareGroupsAsync_RejectsInvalidInput()
    {
        var (admin, _) = CreateAdmin();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await admin.DeleteShareGroupsAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await admin.DeleteShareGroupsAsync([""]));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await admin.DeleteShareGroupsAsync([FirstGroupId, FirstGroupId]));
    }

    [Test]
    public async Task DeleteShareGroupsAsync_EmptyInputReturnsWithoutNetworkCall()
    {
        var (admin, connection) = CreateAdmin();

        var results = await admin.DeleteShareGroupsAsync([]);

        await Assert.That(results).IsEmpty();
        await connection.DidNotReceive().SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            Arg.Any<FindCoordinatorRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteShareGroupsAsync_PreCancelledTokenStopsBeforeNetworkCall()
    {
        var (admin, connection) = CreateAdmin();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await admin.DeleteShareGroupsAsync([FirstGroupId], cancellation.Token));
        await connection.DidNotReceive().SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            Arg.Any<FindCoordinatorRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteShareGroupsAsync_PreservesPerGroupResultsAndUsesGroupCoordinator()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
                Arg.Any<DeleteGroupsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new DeleteGroupsResponse
            {
                Results =
                [
                    Result(FirstGroupId, ErrorCode.None),
                    Result(SecondGroupId, ErrorCode.NonEmptyGroup)
                ]
            }));

        var results = await admin.DeleteShareGroupsAsync([FirstGroupId, SecondGroupId]);

        await Assert.That(results[FirstGroupId].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results[SecondGroupId].ErrorCode).IsEqualTo(ErrorCode.NonEmptyGroup);
        await connection.Received(2).SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            Arg.Is<FindCoordinatorRequest>(request =>
                request != null && request.KeyType == CoordinatorType.Group),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
        await connection.Received(1).SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
            Arg.Is<DeleteGroupsRequest>(request =>
                request != null &&
                request.GroupsNames.Count == 2 &&
                request.GroupsNames.Contains(FirstGroupId) &&
                request.GroupsNames.Contains(SecondGroupId)),
            2,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteShareGroupsAsync_TimeoutRetriesThenPropagates()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
                Arg.Any<DeleteGroupsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask<DeleteGroupsResponse>>(_ =>
                throw new KafkaException(ErrorCode.RequestTimedOut, "simulated timeout"));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.DeleteShareGroupsAsync([FirstGroupId]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
        await connection.Received(4).SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
            Arg.Any<DeleteGroupsRequest>(),
            2,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteShareGroupsAsync_GroupNotFoundAfterAmbiguousSendIsSuccess()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        var calls = 0;
        connection.SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
                Arg.Any<DeleteGroupsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new KafkaException(ErrorCode.RequestTimedOut, "simulated timeout");

                return ValueTask.FromResult(new DeleteGroupsResponse
                {
                    Results = [Result(FirstGroupId, ErrorCode.GroupIdNotFound)]
                });
            });

        var results = await admin.DeleteShareGroupsAsync([FirstGroupId]);

        await Assert.That(results[FirstGroupId].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(calls).IsEqualTo(2);
    }

    private static DeleteGroupsResponseResult Result(string groupId, ErrorCode errorCode) => new()
    {
        GroupId = groupId,
        ErrorCode = errorCode
    };

    private static void SetupFindCoordinator(IKafkaConnection connection)
    {
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<FindCoordinatorRequest>()!;
                return ValueTask.FromResult(new FindCoordinatorResponse
                {
                    Coordinators =
                    [
                        new Coordinator
                        {
                            Key = request.Key,
                            NodeId = 1,
                            Host = "localhost",
                            Port = 9092,
                            ErrorCode = ErrorCode.None
                        }
                    ]
                });
            });
    }

    private static (AdminClient Admin, IKafkaConnection Connection) CreateAdmin()
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
        metadataManager.SetApiVersion(ApiKey.Metadata, 9, 13);
        metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 6);
        metadataManager.SetApiVersion(ApiKey.DeleteGroups, 2, 2);

        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateMetadataResponse()));

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
