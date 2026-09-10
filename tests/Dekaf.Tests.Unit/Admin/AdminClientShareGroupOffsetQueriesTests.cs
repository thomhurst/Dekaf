using System.Reflection;
using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Testing;
using NSubstitute;
using static Dekaf.Tests.Unit.Admin.AdminClientStreamsGroupManagementTests;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientShareGroupOffsetQueriesTests
{
    private static readonly TopicPartition Partition = new("input", 0);

    [Test]
    public async Task Query_CancellationDuringEmptyResultsPreventsSuccess()
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        using var cancellation = new CancellationTokenSource();
        var partitions = Substitute.For<IReadOnlyList<TopicPartition>>();
        partitions.Count.Returns(_ =>
        {
            cancellation.Cancel();
            return 0;
        });
        var requests = new Dictionary<string, IReadOnlyList<TopicPartition>?> { ["empty"] = partitions };
        // Inject cancellation after the public input snapshot, while the core materializes results.
        var method = typeof(AdminClient).GetMethod("ListShareGroupOffsetsCoreAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var query = (ValueTask<IReadOnlyDictionary<string, ShareGroupOffsetsResult>>)method.Invoke(
            admin, [requests, cancellation.Token])!;

        await Assert.That(async () => await query).Throws<OperationCanceledException>();
        await Assert.That(connection.ReceivedCalls()).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Query_CancellationDuringEmptySnapshotPreventsSuccess(bool inMemory)
    {
        await using IAdminClient admin = inMemory ? new InMemoryAdminClient(new InMemoryKafkaCluster()) : CreateAdmin().Admin;
        using var cancellation = new CancellationTokenSource();
        var specs = Substitute.For<IReadOnlyDictionary<string, ListShareGroupOffsetsSpec>>();
        specs.GetEnumerator().Returns(_ =>
        {
            cancellation.Cancel();
            return Enumerable.Empty<KeyValuePair<string, ListShareGroupOffsetsSpec>>().GetEnumerator();
        });
        await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(specs, cancellationToken: cancellation.Token))
            .Throws<OperationCanceledException>();
    }

    [Test]
    [Arguments(0)]
    [Arguments(30000)]
    public async Task Query_EmptyInputCompletesWithoutDeadlineWork(int timeoutMs)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        var query = admin.ListShareGroupOffsetsAsync(Specs(), new() { TimeoutMs = timeoutMs });
        await Assert.That(query.IsCompletedSuccessfully).IsTrue();
        await Assert.That(await query).IsEmpty();
        await Assert.That(connection.ReceivedCalls()).IsEmpty();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(Specs(), cancellationToken: cancellation.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(Specs(), new() { TimeoutMs = -1 }))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Query_ZeroDeadlinePreventsBrokerWork()
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(Specs("group"), new() { TimeoutMs = 0 }))
            .Throws<KafkaTimeoutException>();
        await Assert.That(connection.ReceivedCalls()).IsEmpty();
    }

    [Test]
    [Arguments((short)0)]
    [Arguments((short)1)]
    public async Task Query_BatchesGroupsAndPreservesPartitionDetails(short version)
    {
        var (admin, connection) = CreateAdmin(version);
        await using var owned = admin;
        connection.SendAsync<DescribeShareGroupOffsetsRequest, DescribeShareGroupOffsetsResponse>(Arg.Any<DescribeShareGroupOffsetsRequest>(), version, Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult(new DescribeShareGroupOffsetsResponse
            {
                Groups = call.ArgAt<DescribeShareGroupOffsetsRequest>(0).Groups.Select(group => Group(group.GroupId)).ToArray()
            }));
        IAdminClient client = admin;
        var results = await client.ListShareGroupOffsetsAsync(Specs("first", "second"));
        await Assert.That(results.Keys).IsEquivalentTo(["first", "second"]);
        var offset = results["first"].Offsets[Partition];
        await Assert.That(offset.StartOffset).IsEqualTo(42);
        await Assert.That(offset.LeaderEpoch).IsEqualTo(7);
        await Assert.That(offset.Lag).IsEqualTo(version == 0 ? -1 : 9);
        await Assert.That(results["first"].Offsets[new("input", 1)].ErrorCode).IsEqualTo(ErrorCode.TopicAuthorizationFailed);
        await Assert.That(results["first"].Offsets[new("input", 1)].ErrorMessage).IsEqualTo("denied");
        await connection.Received(1).SendAsync<DescribeShareGroupOffsetsRequest, DescribeShareGroupOffsetsResponse>(
            Arg.Is<DescribeShareGroupOffsetsRequest>(request => request.Groups.Count == 2 && request.Groups[0].Topics == null), version, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Query_RetriesOnlyAffectedGroupAfterCoordinatorMoves()
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        var calls = new List<string[]>();
        var secondLookups = 0;
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var key = call.ArgAt<FindCoordinatorRequest>(0).Key;
                var node = key == "second" && ++secondLookups > 1 ? 2 : 1;
                return ValueTask.FromResult(new FindCoordinatorResponse { Coordinators = [new Coordinator { Key = key, NodeId = node, Host = "localhost", Port = 9092 }] });
            });
        connection.SendAsync<DescribeShareGroupOffsetsRequest, DescribeShareGroupOffsetsResponse>(Arg.Any<DescribeShareGroupOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                calls.Add(call.ArgAt<DescribeShareGroupOffsetsRequest>(0).Groups.Select(group => group.GroupId).ToArray());
                return ValueTask.FromResult(new DescribeShareGroupOffsetsResponse { Groups = calls.Count == 1
                    ? [Group("first"), Group("second", ErrorCode.NotCoordinator)] : [Group("second")] });
            });
        var results = await admin.ListShareGroupOffsetsAsync(Specs("first", "second"));
        await Assert.That(calls.Count).IsEqualTo(2);
        await Assert.That(calls[1]).IsEquivalentTo(["second"]);
        await Assert.That(secondLookups).IsEqualTo(2);
        await Assert.That(results.Values.All(group => group.ErrorCode == ErrorCode.None)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Query_PreservesOtherGroupsWhenDiscoveryOrResponseFails(bool discovery)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        if (discovery)
            connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var key = call.ArgAt<FindCoordinatorRequest>(0).Key;
                    return ValueTask.FromResult(new FindCoordinatorResponse { Coordinators = [new Coordinator
                    { Key = key, NodeId = 1, Host = "localhost", Port = 9092, ErrorCode = key == "denied" ? ErrorCode.GroupAuthorizationFailed : ErrorCode.None }] });
                });
        connection.SendAsync<DescribeShareGroupOffsetsRequest, DescribeShareGroupOffsetsResponse>(Arg.Any<DescribeShareGroupOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new DescribeShareGroupOffsetsResponse { Groups = [Group("ok"), Group("denied", ErrorCode.GroupAuthorizationFailed)] }));
        var results = await admin.ListShareGroupOffsetsAsync(Specs("ok", "denied"));
        await Assert.That(results["ok"].Offsets[Partition].StartOffset).IsEqualTo(42);
        await Assert.That(results["denied"].ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
    }

    [Test]
    public async Task Query_ExhaustedRetriesRetainSuccessAndBrokerErrorMessage()
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        var completedRequests = 0;
        connection.SendAsync<DescribeShareGroupOffsetsRequest, DescribeShareGroupOffsetsResponse>(Arg.Any<DescribeShareGroupOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var groups = call.ArgAt<DescribeShareGroupOffsetsRequest>(0).Groups;
                completedRequests += groups.Count(group => group.GroupId == "ok");
                return ValueTask.FromResult(new DescribeShareGroupOffsetsResponse { Groups = groups.Select(group =>
                    Group(group.GroupId, group.GroupId == "ok" ? ErrorCode.None : ErrorCode.CoordinatorLoadInProgress)).ToArray() });
            });
        var result = await admin.ListShareGroupOffsetsAsync(Specs("ok", "retry"));
        await Assert.That(completedRequests).IsEqualTo(1);
        await Assert.That(result["retry"].ErrorCode).IsEqualTo(ErrorCode.CoordinatorLoadInProgress);
        await Assert.That(result["retry"].ErrorMessage).IsEqualTo("group error");
    }

    [Test]
    public async Task Query_MissingDuplicateAndUnrequestedOutcomesCannotBecomeSuccess()
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        connection.SendAsync<DescribeShareGroupOffsetsRequest, DescribeShareGroupOffsetsResponse>(Arg.Any<DescribeShareGroupOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new DescribeShareGroupOffsetsResponse { Groups = [Group("duplicate"), Group("duplicate"), Group("selected"), Group("unrequested")] }));
        var specs = Specs("missing", "duplicate", "selected");
        specs["selected"] = new() { TopicPartitions = [Partition, new("input", 2)] };
        var result = await admin.ListShareGroupOffsetsAsync(specs);
        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result["missing"].ErrorCode).IsEqualTo(ErrorCode.UnknownServerError);
        await Assert.That(result["duplicate"].ErrorCode).IsEqualTo(ErrorCode.UnknownServerError);
        await Assert.That(result["selected"].Offsets.Count).IsEqualTo(2);
        await Assert.That(result["selected"].Offsets[new("input", 2)].ErrorCode).IsEqualTo(ErrorCode.UnknownServerError);
    }

    [Test]
    public async Task Query_EmptySelectionAndInputDoNotSendRequests()
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        await Assert.That(await admin.ListShareGroupOffsetsAsync(Specs())).IsEmpty();
        var specs = Specs("none");
        specs["none"] = new() { TopicPartitions = [] };
        var result = await admin.ListShareGroupOffsetsAsync(specs);
        await Assert.That(result["none"].Offsets).IsEmpty();
        await Assert.That(connection.ReceivedCalls()).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Query_CancellationAndDeadlineIncludeSuspendedDiscovery(bool deadline)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<FindCoordinatorResponse>(Pause(call.ArgAt<CancellationToken>(2))));
        async Task<FindCoordinatorResponse> Pause(CancellationToken token)
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new FindCoordinatorResponse { Coordinators = [] };
        }
        var pending = admin.ListShareGroupOffsetsAsync(Specs("wait"), new() { TimeoutMs = deadline ? 100 : 30000 }, cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (deadline)
            await Assert.That(async () => await pending).Throws<KafkaTimeoutException>();
        else
        {
            cancellation.Cancel();
            await Assert.That(async () => await pending).Throws<OperationCanceledException>();
        }
        await connection.DidNotReceive().SendAsync<DescribeShareGroupOffsetsRequest, DescribeShareGroupOffsetsResponse>(Arg.Any<DescribeShareGroupOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Query_ValidatesBeforeSendingAndPreservesCustomClientCompatibility()
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(null!)).Throws<ArgumentNullException>();
        await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(new Dictionary<string, ListShareGroupOffsetsSpec> { [" "] = new() })).Throws<ArgumentException>();
        await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(new Dictionary<string, ListShareGroupOffsetsSpec> { ["g"] = null! })).Throws<ArgumentNullException>();
        await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(Specs("g"), new() { TimeoutMs = -1 })).Throws<ArgumentOutOfRangeException>();
        foreach (var partitions in new TopicPartition[][] { [Partition, Partition], [new("input", -1)], [new("", 0)] })
            await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(new Dictionary<string, ListShareGroupOffsetsSpec> { ["g"] = new() { TopicPartitions = partitions } })).Throws<ArgumentException>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(Specs("g"), cancellationToken: cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(async () => await Substitute.For<IAdminClient>().ListShareGroupOffsetsAsync(Specs("g"))).Throws<NotSupportedException>();
        await Assert.That(connection.ReceivedCalls()).IsEmpty();
    }

    [Test]
    public async Task Query_UnsupportedDestinationReturnsGroupOutcomes()
    {
        var (admin, connection) = CreateAdmin(supported: false);
        await using var owned = admin;
        var results = await admin.ListShareGroupOffsetsAsync(Specs("first", "second"));
        await Assert.That(results.Values.All(group => group.ErrorCode == ErrorCode.UnsupportedVersion)).IsTrue();
        await connection.DidNotReceive().SendAsync<DescribeShareGroupOffsetsRequest, DescribeShareGroupOffsetsResponse>(
            Arg.Any<DescribeShareGroupOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Query_TransportFailureOnOtherCoordinatorKeepsCompletedGroup()
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var key = call.ArgAt<FindCoordinatorRequest>(0).Key;
                return ValueTask.FromResult(new FindCoordinatorResponse { Coordinators =
                    [new() { Key = key, NodeId = key == "ok" ? 1 : 2, Host = "localhost", Port = 9092 }] });
            });
        var requests = new List<string>();
        var failed = false;
        connection.SendAsync<DescribeShareGroupOffsetsRequest, DescribeShareGroupOffsetsResponse>(Arg.Any<DescribeShareGroupOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var group = call.ArgAt<DescribeShareGroupOffsetsRequest>(0).Groups.Single().GroupId;
                requests.Add(group);
                if (group == "retry" && !failed)
                {
                    failed = true;
                    return ValueTask.FromException<DescribeShareGroupOffsetsResponse>(new IOException("disconnected"));
                }
                return ValueTask.FromResult(new DescribeShareGroupOffsetsResponse { Groups = [Group(group)] });
            });
        var results = await admin.ListShareGroupOffsetsAsync(Specs("ok", "retry"));
        await Assert.That(requests).IsEquivalentTo(["ok", "retry", "retry"]);
        await Assert.That(results.Values.All(group => group.ErrorCode == ErrorCode.None)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Query_CancellationRacingResponseDoesNotMaskInvariantFailure(bool invariant)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        using var cancellation = new CancellationTokenSource();
        connection.SendAsync<DescribeShareGroupOffsetsRequest, DescribeShareGroupOffsetsResponse>(Arg.Any<DescribeShareGroupOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return ValueTask.FromException<DescribeShareGroupOffsetsResponse>(invariant
                    ? new InvalidOperationException("invariant") : new GroupException(ErrorCode.NotCoordinator, "moved"));
            });
        if (invariant)
            await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(Specs("g"), cancellationToken: cancellation.Token)).Throws<InvalidOperationException>();
        else
            await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(Specs("g"), cancellationToken: cancellation.Token)).Throws<OperationCanceledException>();
    }

    private static Dictionary<string, ListShareGroupOffsetsSpec> Specs(params string[] groups) => groups.ToDictionary(group => group, _ => new ListShareGroupOffsetsSpec());

    private static DescribeShareGroupOffsetsResponseGroup Group(string groupId, ErrorCode error = ErrorCode.None) => new()
    {
        GroupId = groupId, ErrorCode = error, ErrorMessage = error == ErrorCode.None ? null : "group error",
        Topics = [new() { TopicName = "input", Partitions =
        [new() { PartitionIndex = 0, StartOffset = 42, LeaderEpoch = 7, Lag = 9 },
         new() { PartitionIndex = 1, ErrorCode = ErrorCode.TopicAuthorizationFailed, ErrorMessage = "denied" }] }]
    };

    private static (AdminClient Admin, IKafkaConnection Connection) CreateAdmin(short version = 1, bool supported = true)
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.BrokerId.Returns(1);
        connection.Host.Returns("localhost");
        connection.Port.Returns(9092);
        connection.IsConnected.Returns(true);
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(connection));
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(connection));
        var versions = new List<ApiVersion> { new(ApiKey.Metadata, 9, 13), new(ApiKey.FindCoordinator, 4, 6) };
        if (supported)
            versions.Add(new(ApiKey.DescribeShareGroupOffsets, 0, version));
        connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(Arg.Any<ApiVersionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ApiVersionsResponse { ErrorCode = ErrorCode.None, ApiKeys = versions }));
        connection.SendAsync<MetadataRequest, MetadataResponse>(Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(MetadataResponseFor(("input", Guid.NewGuid()))));
        SetupFindCoordinator(connection);
        var metadata = new MetadataManager(pool, ["localhost:9092"]);
        metadata.Metadata.Update(MetadataResponseFor(("input", Guid.NewGuid())));
        metadata.SetApiVersion(ApiKey.Metadata, 9, 13);
        metadata.SetApiVersion(ApiKey.FindCoordinator, 4, 6);
        if (supported)
            metadata.SetApiVersion(ApiKey.DescribeShareGroupOffsets, 0, version);
        return (new AdminClient(new() { BootstrapServers = ["localhost:9092"], RetryBackoffMs = 1, RetryBackoffMaxMs = 1 }, pool, metadata, ownsResources: true), connection);
    }
}
