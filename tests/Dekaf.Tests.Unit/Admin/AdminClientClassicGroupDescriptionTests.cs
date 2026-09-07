using System.Buffers;
using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Testing;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientClassicGroupDescriptionTests
{
    [Test]
    [Arguments("consumer")]
    [Arguments("connect")]
    [Arguments("custom")]
    [Arguments("")]
    public async Task Descriptions_PreserveProtocolAndDecodeOnlyConsumer(string protocol)
    {
        var (admin, connection, _) = CreateAdmin();
        await using var disposal = admin;
        var assignment = AssignmentBytes();
        Respond(connection, new DescribeGroupsResponseGroup
        {
            GroupId = "group", GroupState = "Stable", ProtocolType = protocol, ProtocolData = "selected",
            AuthorizedOperations = 123, Members = [new DescribeGroupsResponseMember
            {
                MemberId = "member", GroupInstanceId = "instance", ClientId = "client", ClientHost = "host",
                MemberMetadata = [4, 5], MemberAssignment = assignment
            }]
        });
        IAdminClient capability = admin;
        var result = (await capability.DescribeClassicGroupsAsync(["group"],
            new() { IncludeAuthorizedOperations = true }))["group"];
        await Assert.That(result.ErrorCode).IsEqualTo(ErrorCode.None);
        var description = result.Description!;
        await Assert.That(description.ProtocolType).IsEqualTo(protocol);
        await Assert.That(description.ProtocolData).IsEqualTo("selected");
        await Assert.That(description.State).IsEqualTo("Stable");
        await Assert.That(description.CoordinatorId).IsEqualTo(1);
        await Assert.That(description.AuthorizedOperations).IsEqualTo(123);
        var member = description.Members.Single();
        await Assert.That(member.MemberId).IsEqualTo("member");
        await Assert.That(member.GroupInstanceId).IsEqualTo("instance");
        await Assert.That(member.ClientId).IsEqualTo("client");
        await Assert.That(member.ClientHost).IsEqualTo("host");
        await Assert.That(member.Metadata.ToArray()).IsEquivalentTo(new byte[] { 4, 5 });
        await Assert.That(member.AssignmentData.ToArray()).IsEquivalentTo(assignment);
        if (protocol == "consumer")
            await Assert.That(member.Assignment!).IsEquivalentTo([new TopicPartition("topic", 2)]);
        else
            await Assert.That(member.Assignment).IsNull();
        await connection.DidNotReceive().SendAsync<ConsumerGroupDescribeRequest, ConsumerGroupDescribeResponse>(
            Arg.Any<ConsumerGroupDescribeRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
        await connection.Received(1).SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(
            Arg.Is<DescribeGroupsRequest>(r => r.IncludeAuthorizedOperations), Arg.Is((short)5), Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(false, 123)]
    [Arguments(true, int.MinValue)]
    public async Task AuthorizedOperations_AreNullWhenNotRequestedOrUnavailable(bool requested, int operations)
    {
        var (admin, connection, _) = CreateAdmin();
        await using var disposal = admin;
        Respond(connection, new DescribeGroupsResponseGroup() { GroupId = "group", GroupState = "Empty", Members = [], AuthorizedOperations = operations });
        var results = await admin.DescribeClassicGroupsAsync(["group"], new() { IncludeAuthorizedOperations = requested });
        await Assert.That(results["group"].Description!.AuthorizedOperations).IsNull();
    }

    [Test]
    public async Task Batch_PreservesSuccessAndErrorsIncludingMissingResponse()
    {
        var (admin, connection, _) = CreateAdmin();
        await using var disposal = admin;
        Respond(connection, Group("ok"), Group("denied", ErrorCode.GroupAuthorizationFailed),
            Group("unknown", ErrorCode.GroupIdNotFound), Group("unrequested"));
        var results = await admin.DescribeClassicGroupsAsync(["ok", "denied", "unknown", "omitted"]);
        await Assert.That(results.Count).IsEqualTo(4);
        await Assert.That(results["ok"].Description!.State).IsEqualTo("Empty");
        await Assert.That(results["denied"].ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That(results["denied"].Description).IsNull();
        await Assert.That(results["denied"].ErrorMessage).IsEqualTo("broker detail");
        await Assert.That(results["unknown"].ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        await Assert.That(results["omitted"].ErrorCode).IsEqualTo(ErrorCode.UnknownServerError);
        await connection.Received(1).SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(
            Arg.Is<DescribeGroupsRequest>(r => r.Groups.Count == 4), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CoordinatorRetry_PreservesCompletedGroupsAndReportsExhaustion(bool exhaust)
    {
        var (admin, connection, pool) = CreateAdmin();
        await using var disposal = admin;
        var calls = 0;
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<FindCoordinatorResponse>(Coordinator(call.Arg<FindCoordinatorRequest>().Key!, calls == 0 ? 1 : 2)));
        connection.SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(Arg.Any<DescribeGroupsRequest>(),
                Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                calls++;
                var groups = call.Arg<DescribeGroupsRequest>().Groups;
                return new ValueTask<DescribeGroupsResponse>(new DescribeGroupsResponse
                {
                    Groups = groups.Select(id => Group(id, id == "retry" && (calls == 1 || exhaust)
                        ? ErrorCode.NotCoordinator : ErrorCode.None)).ToArray()
                });
            });
        var results = await admin.DescribeClassicGroupsAsync(["ok", "retry"]);
        await Assert.That(results["ok"].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results["retry"].ErrorCode).IsEqualTo(exhaust ? ErrorCode.NotCoordinator : ErrorCode.None);
        await Assert.That(calls).IsEqualTo(exhaust ? 4 : 2);
        await pool.Received().GetConnectionAsync(2, Arg.Any<CancellationToken>());
        await connection.Received(1).SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(
            Arg.Is<DescribeGroupsRequest>(r => r.Groups.Contains("ok")), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DiscoveryAuthorizationError_DoesNotDiscardAnotherGroup()
    {
        var (admin, connection, _) = CreateAdmin();
        await using var disposal = admin;
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<FindCoordinatorResponse>(Coordinator(call.Arg<FindCoordinatorRequest>().Key!, 1,
                call.Arg<FindCoordinatorRequest>().Key == "denied" ? ErrorCode.GroupAuthorizationFailed : ErrorCode.None)));
        Respond(connection, Group("ok"));
        var results = await admin.DescribeClassicGroupsAsync(["denied", "ok"]);
        await Assert.That(results["denied"].ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That(results["ok"].Description).IsNotNull();
    }

    [Test]
    public async Task ValidationAndEmptyInput_DoNotSendRequests()
    {
        var (admin, connection, _) = CreateAdmin();
        await using var disposal = admin;
        await Assert.That(async () => await admin.DescribeClassicGroupsAsync(null!)).Throws<ArgumentNullException>();
        await Assert.That(async () => await admin.DescribeClassicGroupsAsync([" "])).Throws<ArgumentException>();
        await Assert.That(async () => await admin.DescribeClassicGroupsAsync(["a", "a"])).Throws<ArgumentException>();
        await Assert.That(async () => await admin.DescribeClassicGroupsAsync([], new() { TimeoutMs = -1 })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(await admin.DescribeClassicGroupsAsync([])).IsEmpty();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.That(async () => await admin.DescribeClassicGroupsAsync([], cancellationToken: cancelled.Token)).Throws<OperationCanceledException>();
        await connection.DidNotReceive().SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
        IAdminClient unsupported = Substitute.For<IAdminClient>();
        await Assert.That(async () => await unsupported.DescribeClassicGroupsAsync(["a"])).Throws<NotSupportedException>();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task CancellationAndDeadline_ReachDiscoveryAndDescribe(bool timeout, bool discovery)
    {
        var (admin, connection, _) = CreateAdmin();
        await using var disposal = admin;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (discovery)
            connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(),
                    Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => new ValueTask<FindCoordinatorResponse>(Wait<FindCoordinatorResponse>(call.Arg<CancellationToken>())));
        else
            connection.SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(Arg.Any<DescribeGroupsRequest>(),
                    Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => new ValueTask<DescribeGroupsResponse>(Wait<DescribeGroupsResponse>(call.Arg<CancellationToken>())));
        using var cancellation = new CancellationTokenSource();
        var operation = admin.DescribeClassicGroupsAsync(["group"], new() { TimeoutMs = timeout ? 100 : 30000 }, cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (timeout)
            await Assert.That(async () => await operation).Throws<KafkaTimeoutException>();
        else
        {
            cancellation.Cancel();
            await Assert.That(async () => await operation).Throws<OperationCanceledException>();
        }
        async Task<T> Wait<T>(CancellationToken token)
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CancellationRacingRetriableFailure_PreservesCancellation(bool discovery)
    {
        var (admin, connection, _) = CreateAdmin();
        await using var disposal = admin;
        using var cancellation = new CancellationTokenSource();
        if (discovery)
            connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(),
                    Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    cancellation.Cancel();
                    return ValueTask.FromException<FindCoordinatorResponse>(new GroupException(ErrorCode.NotCoordinator, "moved"));
                });
        else
            connection.SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(Arg.Any<DescribeGroupsRequest>(),
                    Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    cancellation.Cancel();
                    return ValueTask.FromException<DescribeGroupsResponse>(new GroupException(ErrorCode.NotCoordinator, "moved"));
                });
        await Assert.That(async () => await admin.DescribeClassicGroupsAsync(["group"],
            cancellationToken: cancellation.Token)).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task MalformedConsumerAssignment_RetainsRawBytesWithoutFailingOtherMembers()
    {
        var (admin, connection, _) = CreateAdmin();
        await using var disposal = admin;
        Respond(connection, new DescribeGroupsResponseGroup
        {
            GroupId = "group", GroupState = "Stable", ProtocolType = "consumer",
            Members = [new() { MemberId = "bad", MemberAssignment = [0] },
                new() { MemberId = "good", MemberAssignment = AssignmentBytes() }]
        });
        var description = (await admin.DescribeClassicGroupsAsync(["group"]))["group"].Description!;
        await Assert.That(description.Members[0].Assignment).IsNull();
        await Assert.That(description.Members[0].AssignmentData.ToArray()).IsEquivalentTo(new byte[] { 0 });
        await Assert.That(description.Members[1].Assignment!).IsEquivalentTo([new TopicPartition("topic", 2)]);
    }

    internal static byte[] AssignmentBytes()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16(0);
        writer.WriteInt32(1);
        writer.WriteString("topic");
        writer.WriteInt32(1);
        writer.WriteInt32(2);
        writer.WriteBytes([]);
        return buffer.WrittenSpan.ToArray();
    }

    private static DescribeGroupsResponseGroup Group(string id, ErrorCode code = ErrorCode.None) => new()
    {
        GroupId = id, GroupState = "Empty", ProtocolType = "", ProtocolData = "", Members = [],
        ErrorCode = code, ErrorMessage = code == ErrorCode.None ? null : "broker detail"
    };
    private static FindCoordinatorResponse Coordinator(string id, int node, ErrorCode code = ErrorCode.None) => new()
    {
        Coordinators = [new Coordinator { Key = id, NodeId = node, Host = "localhost", Port = 9092, ErrorCode = code }]
    };
    private static void Respond(IKafkaConnection connection, params DescribeGroupsResponseGroup[] groups) =>
        connection.SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(Arg.Any<DescribeGroupsRequest>(),
                Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<DescribeGroupsResponse>(new DescribeGroupsResponse { Groups = groups }));

    private static (AdminClient Admin, IKafkaConnection Connection, IConnectionPool Pool) CreateAdmin()
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.BrokerId.Returns(1);
        connection.Host.Returns("localhost");
        connection.Port.Returns(9092);
        connection.IsConnected.Returns(true);
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new ValueTask<IKafkaConnection>(connection));
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new ValueTask<IKafkaConnection>(connection));
        var snapshot = new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
            ControllerId = 1, ClusterId = "test", Topics = []
        };
        connection.SendAsync<MetadataRequest, MetadataResponse>(Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<MetadataResponse>(snapshot));
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<FindCoordinatorResponse>(Coordinator(call.Arg<FindCoordinatorRequest>().Key!, 1)));
        var metadata = new MetadataManager(pool, ["localhost:9092"]);
        metadata.Metadata.Update(snapshot);
        metadata.SetApiVersion(ApiKey.FindCoordinator, 4, 4);
        metadata.SetApiVersion(ApiKey.DescribeGroups, 5, 5);
        metadata.SetApiVersion(ApiKey.ConsumerGroupDescribe, 0, 1);
        metadata.SetApiVersion(ApiKey.Metadata, 9, 13);
        return (new AdminClient(new AdminClientOptions
        {
            BootstrapServers = ["localhost:9092"], RetryBackoffMs = 1, RetryBackoffMaxMs = 1
        }, pool, metadata), connection, pool);
    }
}
