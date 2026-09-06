using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;
using static Dekaf.Tests.Unit.Admin.AdminClientStreamsGroupManagementTests;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientConsumerGroupOffsetQueriesTests
{
    private const string Group = "consumer-query";
    private const string Topic = "input";
    private static readonly Guid TopicId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

    [Test]
    [Arguments((short)7, false)]
    [Arguments((short)7, true)]
    [Arguments((short)9, false)]
    [Arguments((short)9, true)]
    public async Task RequireStable_DoesNotRefreshMetadataForPendingTransaction(short version, bool groupError)
    {
        var (admin, connection, _) = CreateQueryAdmin(version);
        await using var owned = admin;
        SetupFindCoordinator(connection);
        var requests = 0;
        var retryRefreshes = 0;
        connection.SendAsync<MetadataRequest, MetadataResponse>(Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (requests != 0)
                    retryRefreshes++;
                return ValueTask.FromResult(MetadataResponseFor((Topic, TopicId)));
            });
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var error = ++requests == 1 ? ErrorCode.UnstableOffsetCommit : ErrorCode.None;
                var response = Response(call.ArgAt<OffsetFetchRequest>(0), version,
                    [Partition(0, 42, groupError ? ErrorCode.None : error)]);
                if (groupError)
                    response = version < 8
                        ? new OffsetFetchResponse { ErrorCode = error, Topics = response.Topics }
                        : new OffsetFetchResponse
                        {
                            Groups = [new OffsetFetchResponseGroup
                            {
                                GroupId = Group, ErrorCode = error, Topics = response.Groups![0].Topics
                            }]
                        };
                return ValueTask.FromResult(response);
            });

        var results = await admin.ListConsumerGroupOffsetsAsync(Specs(0), new() { RequireStable = true });
        await Assert.That(results[Group].Offsets[new(Topic, 0)].Offset!.Value.Offset).IsEqualTo(42);
        await Assert.That(requests).IsEqualTo(2);
        await Assert.That(retryRefreshes).IsEqualTo(0);
    }

    [Test]
    public async Task Query_CancellationDoesNotHideUnrelatedInvalidOperation()
    {
        var (admin, connection, _) = CreateQueryAdmin(9);
        await using var owned = admin;
        SetupFindCoordinator(connection);
        using var cancellation = new CancellationTokenSource();
        var failure = new InvalidOperationException("Unrelated operation invariant failed");
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return ValueTask.FromException<OffsetFetchResponse>(failure);
            });
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.ListConsumerGroupOffsetsAsync(Specs(0), cancellationToken: cancellation.Token).AsTask());
        await Assert.That(exception).IsSameReferenceAs(failure);
    }

    [Test]
    [Arguments((short)6)]
    [Arguments((short)8)]
    [Arguments((short)10)]
    public async Task Query_PreservesCheckpointAbsenceAndPartitionErrors(short version)
    {
        var (admin, connection, _) = CreateQueryAdmin(version);
        await using var owned = admin;
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult(Response(call.ArgAt<OffsetFetchRequest>(0), version,
                [Partition(0, 42), Partition(1, -1), Partition(2, 123, ErrorCode.TopicAuthorizationFailed)])));

        IAdminClient client = admin;
        var results = await client.ListConsumerGroupOffsetsAsync(Specs(0, 1, 2));
        var group = results[Group];
        var checkpoint = group.Offsets[new(Topic, 0)].Offset!.Value;
        await Assert.That(group.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(checkpoint).IsEqualTo(new TopicPartitionOffset(Topic, 0, 42, 7) { Metadata = "checkpoint" });
        await Assert.That(group.Offsets[new(Topic, 1)].Offset).IsNull();
        await Assert.That(group.Offsets[new(Topic, 1)].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(group.Offsets[new(Topic, 2)].Offset).IsNull();
        await Assert.That(group.Offsets[new(Topic, 2)].ErrorCode).IsEqualTo(ErrorCode.TopicAuthorizationFailed);
        await connection.Received(1).SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
            Arg.Is<OffsetFetchRequest>(request => version != 10 || request.Groups![0].Topics![0].TopicId == TopicId),
            version, Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments((short)7, 2)]
    [Arguments((short)8, 1)]
    [Arguments((short)10, 1)]
    public async Task Query_BatchesGroupsAtCoordinatorWhenDestinationSupportsIt(short maximum, int expectedCalls)
    {
        var (admin, connection, _) = CreateQueryAdmin(maximum);
        await using var owned = admin;
        SetupFindCoordinator(connection);
        var negotiated = Math.Min(maximum, (short)9);
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult(Response(call.ArgAt<OffsetFetchRequest>(0), negotiated, [Partition(0, 42)])));
        var results = await admin.ListConsumerGroupOffsetsAsync(new Dictionary<string, ListConsumerGroupOffsetsSpec>
        {
            [Group] = new(), ["second"] = new()
        }, new ListConsumerGroupOffsetsOptions { RequireStable = true });
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results["second"].Offsets[new(Topic, 0)].Offset!.Value.Offset).IsEqualTo(42);
        await connection.Received(expectedCalls).SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
            Arg.Is<OffsetFetchRequest>(request => request.RequireStable && request.Topics == null), negotiated, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RequireStable_RejectsDestinationWithoutV7()
    {
        var (admin, connection, _) = CreateQueryAdmin(6);
        await using var owned = admin;
        SetupFindCoordinator(connection);
        await Assert.That(async () => await admin.ListConsumerGroupOffsetsAsync(Specs(0),
            new ListConsumerGroupOffsetsOptions { RequireStable = true })).Throws<KafkaException>();
        await connection.DidNotReceive().SendAsync<OffsetFetchRequest, OffsetFetchResponse>(Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RequireStable_RetriesBeyondNormalRetryCountAndKeepsCompletedGroups()
    {
        var (admin, connection, _) = CreateQueryAdmin(9);
        await using var owned = admin;
        SetupFindCoordinator(connection);
        var fifthRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stable = 0;
        var requests = 0;
        var completedGroupRequests = 0;
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.ArgAt<OffsetFetchRequest>(0);
                if (Interlocked.Increment(ref requests) == 5) fifthRequest.TrySetResult();
                var groups = request.Groups!.Select(group =>
                {
                    if (group.GroupId == "complete") Interlocked.Increment(ref completedGroupRequests);
                    return new OffsetFetchResponseGroup
                    {
                        GroupId = group.GroupId,
                        ErrorCode = ErrorCode.None,
                        Topics = [new OffsetFetchResponseTopic { Name = Topic, Partitions = [Partition(0, 42,
                            group.GroupId == Group && Volatile.Read(ref stable) == 0 ? ErrorCode.UnstableOffsetCommit : ErrorCode.None)] }]
                    };
                }).ToArray();
                return ValueTask.FromResult(new OffsetFetchResponse { Groups = groups });
            });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var pending = admin.ListConsumerGroupOffsetsAsync(new Dictionary<string, ListConsumerGroupOffsetsSpec>
        {
            [Group] = new(), ["complete"] = new()
        }, new ListConsumerGroupOffsetsOptions { RequireStable = true }, timeout.Token).AsTask();
        await fifthRequest.Task.WaitAsync(timeout.Token);
        await Assert.That(pending.IsCompleted).IsFalse();
        Volatile.Write(ref stable, 1);
        var results = await pending;
        await Assert.That(results[Group].Offsets[new(Topic, 0)].Offset!.Value.Offset).IsEqualTo(42);
        await Assert.That(completedGroupRequests).IsEqualTo(1);
    }

    [Test]
    public async Task RequireStable_RespectsTotalTimeout()
    {
        var (admin, connection, _) = CreateQueryAdmin(9);
        await using var owned = admin;
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult(Response(call.ArgAt<OffsetFetchRequest>(0), 9, [Partition(0, -1, ErrorCode.UnstableOffsetCommit)])));
        await Assert.That(async () => await admin.ListConsumerGroupOffsetsAsync(Specs(0),
            new ListConsumerGroupOffsetsOptions { RequireStable = true, TimeoutMs = 50 })).Throws<KafkaTimeoutException>();
    }

    [Test]
    public async Task Query_CancellationInterruptsPendingNetworkRequest()
    {
        var (admin, connection, _) = CreateQueryAdmin(9);
        await using var owned = admin;
        SetupFindCoordinator(connection);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource<OffsetFetchResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => { entered.TrySetResult(); return new ValueTask<OffsetFetchResponse>(never.Task.WaitAsync(call.ArgAt<CancellationToken>(2))); });
        using var cancellation = new CancellationTokenSource();
        var pending = admin.ListConsumerGroupOffsetsAsync(Specs(0), cancellationToken: cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.That(async () => await pending).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task Query_RediscoversMovedCoordinatorAndRenegotiatesDestination()
    {
        var (admin, bootstrap, pool) = CreateQueryAdmin(10);
        await using var owned = admin;
        var discoveries = 0;
        bootstrap.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(new FindCoordinatorResponse
            {
                Coordinators = [new Coordinator { Key = Group, NodeId = Interlocked.Increment(ref discoveries) == 1 ? 2 : 3,
                    Host = "localhost", Port = 9092, ErrorCode = ErrorCode.None }]
            }));
        var oldDestination = Substitute.For<IKafkaConnection>();
        oldDestination.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new OffsetFetchResponse { ErrorCode = ErrorCode.NotCoordinator, Topics = [] }));
        var newDestination = Substitute.For<IKafkaConnection>();
        newDestination.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult(Response(call.ArgAt<OffsetFetchRequest>(0), 10, [Partition(0, 42)])));
        pool.GetConnectionAsync(2, Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<IKafkaConnection>(new CapabilityConnection(oldDestination, 2, 7)));
        pool.GetConnectionAsync(3, Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<IKafkaConnection>(new CapabilityConnection(newDestination, 3, 10)));

        var result = await admin.ListConsumerGroupOffsetsAsync(Specs(0), new ListConsumerGroupOffsetsOptions { RequireStable = true });
        await Assert.That(result[Group].Offsets[new(Topic, 0)].Offset!.Value.Offset).IsEqualTo(42);
        await oldDestination.Received(1).SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
            Arg.Is<OffsetFetchRequest>(request => request.Topics![0].Name == Topic && request.Groups == null), 7, Arg.Any<CancellationToken>());
        await newDestination.Received(1).SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
            Arg.Is<OffsetFetchRequest>(request => request.Groups![0].Topics![0].TopicId == TopicId), 10, Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments((short)6)]
    [Arguments((short)9)]
    public async Task Query_PreservesGroupFailureAlongsideSuccessfulGroup(short version)
    {
        var (admin, connection, _) = CreateQueryAdmin(version);
        await using var owned = admin;
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.ArgAt<OffsetFetchRequest>(0);
                if (version < 8)
                    return ValueTask.FromResult(request.GroupId == Group
                        ? new OffsetFetchResponse { ErrorCode = ErrorCode.GroupAuthorizationFailed, Topics = [] }
                        : Response(request, version, [Partition(0, 42)]));
                return ValueTask.FromResult(new OffsetFetchResponse
                {
                    Groups = request.Groups!.Select(group => new OffsetFetchResponseGroup
                    {
                        GroupId = group.GroupId,
                        ErrorCode = group.GroupId == Group ? ErrorCode.GroupAuthorizationFailed : ErrorCode.None,
                        Topics = group.GroupId == Group ? [] : [new OffsetFetchResponseTopic { Name = Topic, Partitions = [Partition(0, 42)] }]
                    }).ToArray()
                });
            });
        var results = await admin.ListConsumerGroupOffsetsAsync(new Dictionary<string, ListConsumerGroupOffsetsSpec>
        {
            [Group] = new(), ["success"] = new()
        });
        await Assert.That(results[Group].ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That(results[Group].Offsets).IsEmpty();
        await Assert.That(results["success"].Offsets[new(Topic, 0)].Offset!.Value.Offset).IsEqualTo(42);
    }

    [Test]
    public async Task Query_ValidatesInputsAndHandlesEmptySelectionWithoutNetwork()
    {
        var (admin, connection, _) = CreateQueryAdmin();
        await using var owned = admin;
        await Assert.That(async () => await admin.ListConsumerGroupOffsetsAsync(Specs(0, 0))).Throws<ArgumentException>();
        await Assert.That(async () => await admin.ListConsumerGroupOffsetsAsync(Specs(-1))).Throws<ArgumentOutOfRangeException>();
        await Assert.That(async () => await admin.ListConsumerGroupOffsetsAsync(Specs(0),
            new ListConsumerGroupOffsetsOptions { TimeoutMs = -1 })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(await admin.ListConsumerGroupOffsetsAsync(new Dictionary<string, ListConsumerGroupOffsetsSpec>())).IsEmpty();
        var none = await admin.ListConsumerGroupOffsetsAsync(Specs());
        await Assert.That(none[Group].Offsets).IsEmpty();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(async () => await admin.ListConsumerGroupOffsetsAsync(Specs(0), cancellationToken: cancellation.Token))
            .Throws<OperationCanceledException>();
        await connection.DidNotReceive().SendAsync<OffsetFetchRequest, OffsetFetchResponse>(Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Query_CancellationWinsRaceWithMetadataRetryFailure()
    {
        var (admin, connection, _) = CreateQueryAdmin(9);
        await using var owned = admin;
        SetupFindCoordinator(connection);
        using var cancellation = new CancellationTokenSource();
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult(Response(call.ArgAt<OffsetFetchRequest>(0), 9, [Partition(0, -1, ErrorCode.NotCoordinator)])));
        connection.SendAsync<MetadataRequest, MetadataResponse>(Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return ValueTask.FromException<MetadataResponse>(new InvalidOperationException("Metadata unavailable"));
            });
        await Assert.That(async () => await admin.ListConsumerGroupOffsetsAsync(Specs(0),
            new ListConsumerGroupOffsetsOptions { RequireStable = true }, cancellation.Token)).Throws<OperationCanceledException>();
    }

    private static (AdminClient Admin, IKafkaConnection Connection, IConnectionPool Pool) CreateQueryAdmin(short maximum = 10)
    {
        var fixture = CreateAdminWithPool(maximum, ownsResources: true);
        fixture.Connection.SendAsync<MetadataRequest, MetadataResponse>(Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(MetadataResponseFor((Topic, TopicId))));
        return fixture;
    }

    private sealed class CapabilityConnection(IKafkaConnection inner, int brokerId, short maximum) : IKafkaConnection, IKafkaCapabilityProvider
    {
        public int BrokerId => brokerId;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public KafkaConnectionCapabilities Capabilities { get; } = KafkaConnectionCapabilities.Create(new ApiVersionsResponse
        {
            ErrorCode = ErrorCode.None, ApiKeys = [new ApiVersion(ApiKey.OffsetFetch, 6, maximum)]
        });
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => inner.SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
    }

    private static Dictionary<string, ListConsumerGroupOffsetsSpec> Specs(params int[] partitions) => new()
    {
        [Group] = new() { TopicPartitions = partitions.Select(static partition => new TopicPartition(Topic, partition)).ToArray() }
    };

    private static OffsetFetchResponsePartition Partition(int index, long offset, ErrorCode error = ErrorCode.None) => new()
    {
        PartitionIndex = index, CommittedOffset = offset, CommittedLeaderEpoch = 7, Metadata = "checkpoint", ErrorCode = error
    };

    private static OffsetFetchResponse Response(OffsetFetchRequest request, short version, OffsetFetchResponsePartition[] partitions)
    {
        OffsetFetchResponseTopic[] topics = [new() { Name = Topic, TopicId = TopicId, Partitions = partitions }];
        return version < 8
            ? new OffsetFetchResponse { Topics = topics }
            : new OffsetFetchResponse { Groups = request.Groups!.Select(group => new OffsetFetchResponseGroup { GroupId = group.GroupId, ErrorCode = ErrorCode.None, Topics = topics }).ToArray() };
    }
}
