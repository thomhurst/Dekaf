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
        var (admin, connection, _) = CreateAdmin(describeVersion: 6);
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
            Arg.Is<DescribeGroupsRequest>(r => r.Groups.Count == 4), Arg.Is((short)6), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task NonRetriableMetadataError_PreservesOtherGroupOutcomes()
    {
        var (admin, connection, _) = CreateAdmin();
        await using var disposal = admin;
        Respond(connection, Group("ok"), Group("unavailable", ErrorCode.BrokerNotAvailable));
        var results = await admin.DescribeClassicGroupsAsync(["ok", "unavailable"]);
        await Assert.That(results["ok"].Description).IsNotNull();
        await Assert.That(results["unavailable"].ErrorCode).IsEqualTo(ErrorCode.BrokerNotAvailable);
        await Assert.That(results["unavailable"].Description).IsNull();
        await connection.Received(1).SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(
            Arg.Any<DescribeGroupsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
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
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task CancellationDuringGroupEnumeration_IsObservedBeforeReturning(bool inMemory, bool empty)
    {
        using var cancellation = new CancellationTokenSource();
        IEnumerable<string> Groups()
        {
            if (!empty)
                yield return "group";
            cancellation.Cancel();
        }
        if (inMemory)
        {
            await using var admin = new InMemoryAdminClient(new InMemoryKafkaCluster());
            await Assert.That(async () => await admin.DescribeClassicGroupsAsync(Groups(), cancellationToken: cancellation.Token))
                .Throws<OperationCanceledException>();
        }
        else
        {
            var (admin, connection, _) = CreateAdmin();
            await using var disposal = admin;
            await Assert.That(async () => await admin.DescribeClassicGroupsAsync(Groups(), cancellationToken: cancellation.Token))
                .Throws<OperationCanceledException>();
            await connection.DidNotReceive().SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
        }
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

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task InitializationFaultRacingCancellation_PreservesCancellation(bool timeout, bool transport)
    {
        var (admin, _, pool) = CreateAdmin(initializeMetadata: false);
        await using var disposal = admin;
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<IKafkaConnection>(FailAfterCancellation(call.Arg<CancellationToken>())));

        var operation = admin.DescribeClassicGroupsAsync(["group"],
            new() { TimeoutMs = timeout ? 1000 : 30000 }, cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (timeout)
            await Assert.That(async () => await operation).Throws<KafkaTimeoutException>();
        else
        {
            cancellation.Cancel();
            await Assert.That(async () => await operation).Throws<OperationCanceledException>();
        }

        async Task<IKafkaConnection> FailAfterCancellation(CancellationToken token)
        {
            entered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Simulate a transport completing with its fault after cancellation has won.
                throw transport ? new IOException("connection failed")
                    : new GroupException(ErrorCode.GroupAuthorizationFailed, "denied");
            }
            throw new InvalidOperationException("Cancellation wait completed without cancellation.");
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InitializationFailure_PreservesUncanceledFault(bool transport)
    {
        var (admin, connection, pool) = CreateAdmin(initializeMetadata: false);
        await using var disposal = admin;
        Exception failure = transport ? new IOException("connection failed")
            : new GroupException(ErrorCode.GroupAuthorizationFailed, "denied");
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<IKafkaConnection>(failure));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => admin.DescribeClassicGroupsAsync(["group"]).AsTask());
        await Assert.That(exception!.InnerException).IsSameReferenceAs(failure);
        await connection.DidNotReceive().SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(
            Arg.Any<DescribeGroupsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InitializationInvariantFaultAfterCancellation_IsNotReclassified()
    {
        var (admin, _, pool) = CreateAdmin(initializeMetadata: false);
        await using var disposal = admin;
        using var cancellation = new CancellationTokenSource();
        var failure = new InvalidOperationException("invariant failed");
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return ValueTask.FromException<IKafkaConnection>(failure);
            });
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => admin.DescribeClassicGroupsAsync(["group"], cancellationToken: cancellation.Token).AsTask());
        await Assert.That(exception!.InnerException).IsSameReferenceAs(failure);
    }

    [Test]
    [Arguments((short)-1)]
    [Arguments((short)-2)]
    [Arguments(short.MinValue)]
    public async Task NegativeTopicNameLength_RetainsBytesWithoutDecodedAssignment(short length)
    {
        var (admin, connection, _) = CreateAdmin();
        await using var disposal = admin;
        var bytes = NegativeTopicNameAssignmentBytes(length);
        Respond(connection, new DescribeGroupsResponseGroup
        {
            GroupId = "group", GroupState = "Stable", ProtocolType = "consumer",
            Members = [new() { MemberId = "member", MemberAssignment = bytes }]
        });
        var member = (await admin.DescribeClassicGroupsAsync(["group"]))["group"].Description!.Members.Single();
        await Assert.That(member.Assignment).IsNull();
        await Assert.That(member.AssignmentData.ToArray()).IsEquivalentTo(bytes);
    }

    internal static byte[] NegativeTopicNameAssignmentBytes(short length)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16(0);
        writer.WriteInt32(1);
        writer.WriteInt16(length);
        writer.WriteInt32(1);
        writer.WriteInt32(2);
        writer.WriteBytes([]);
        return buffer.WrittenSpan.ToArray();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task NegativeAssignmentCount_RetainsBytesWithoutPublishingPartialAssignment(bool topicCount)
    {
        var (admin, connection, _) = CreateAdmin();
        await using var disposal = admin;
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16(0);
        writer.WriteInt32(topicCount ? -1 : 2);
        if (!topicCount)
        {
            writer.WriteString("valid");
            writer.WriteInt32(1);
            writer.WriteInt32(0);
            writer.WriteString("invalid");
            writer.WriteInt32(-1);
        }
        writer.WriteBytes([]);
        var bytes = buffer.WrittenSpan.ToArray();
        Respond(connection, new DescribeGroupsResponseGroup
        {
            GroupId = "group", GroupState = "Stable", ProtocolType = "consumer",
            Members = [new() { MemberId = "member", MemberAssignment = bytes }]
        });
        var member = (await admin.DescribeClassicGroupsAsync(["group"]))["group"].Description!.Members.Single();
        await Assert.That(member.Assignment).IsNull();
        await Assert.That(member.AssignmentData.ToArray()).IsEquivalentTo(bytes);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task IncompleteUserDataLength_DoesNotPublishPartialAssignment(int lengthBytes)
    {
        var bytes = AssignmentBytes();
        await AssertAssignmentAsync(bytes[..(bytes.Length - 4 + lengthBytes)], valid: false);
    }

    [Test]
    [Arguments(-2, 0, false)]
    [Arguments(-1, 0, true)]
    [Arguments(0, 0, true)]
    [Arguments(3, 2, false)]
    [Arguments(3, 3, true)]
    public async Task UserDataLength_RequiresCompleteNullablePayload(int length, int payloadBytes, bool valid)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteRawBytes(AssignmentBytes().AsSpan()[..^4]);
        writer.WriteInt32(length);
        writer.WriteRawBytes(new byte[payloadBytes]);
        await AssertAssignmentAsync(buffer.WrittenSpan.ToArray(), valid);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EmptyTopicAssignments_DoNotReservePartitionStorage(bool includePartition)
    {
        const int emptyTopics = 1024;
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16(0);
        writer.WriteInt32(emptyTopics + (includePartition ? 1 : 0));
        for (var i = 0; i < emptyTopics; i++)
        {
            writer.WriteString("t");
            writer.WriteInt32(0);
        }
        if (includePartition)
        {
            writer.WriteString("topic");
            writer.WriteInt32(1);
            writer.WriteInt32(2);
        }
        writer.WriteBytes([]);
        var bytes = buffer.WrittenSpan.ToArray();
        var parse = typeof(AdminClient).GetMethod("ParseMemberAssignment",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .CreateDelegate<Func<byte[]?, IReadOnlyList<TopicPartition>?>>();
        _ = parse(bytes);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var assignments = parse(bytes);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Allow decoded topic strings and generous fixed overhead, but not a
        // partition array sized from the number of empty topics in the frame.
        await Assert.That(allocated).IsLessThan(32L * emptyTopics + 1024);
        await Assert.That(assignments!.Count).IsEqualTo(includePartition ? 1 : 0);
        if (includePartition)
            await Assert.That(assignments[0]).IsEqualTo(new TopicPartition("topic", 2));

        var (admin, connection, _) = CreateAdmin();
        await using var disposal = admin;
        Respond(connection, new DescribeGroupsResponseGroup
        {
            GroupId = "group", GroupState = "Stable", ProtocolType = "consumer",
            Members = [new() { MemberId = "member", MemberAssignment = bytes }]
        });
        var member = (await admin.DescribeClassicGroupsAsync(["group"]))["group"].Description!.Members.Single();
        await Assert.That(member.Assignment!).IsEquivalentTo(assignments);
        await Assert.That(member.AssignmentData.ToArray()).IsEquivalentTo(bytes);
    }

    private static async Task AssertAssignmentAsync(byte[] bytes, bool valid)
    {
        var (admin, connection, _) = CreateAdmin();
        await using var disposal = admin;
        Respond(connection, new DescribeGroupsResponseGroup
        {
            GroupId = "group", GroupState = "Stable", ProtocolType = "consumer",
            Members = [new() { MemberId = "member", MemberAssignment = bytes }]
        });
        var member = (await admin.DescribeClassicGroupsAsync(["group"]))["group"].Description!.Members.Single();
        if (valid)
            await Assert.That(member.Assignment!).IsEquivalentTo([new TopicPartition("topic", 2)]);
        else
            await Assert.That(member.Assignment).IsNull();
        await Assert.That(member.AssignmentData.ToArray()).IsEquivalentTo(bytes);
    }

    [Test]
    [Arguments((short)-1)]
    [Arguments((short)-2)]
    [Arguments(short.MinValue)]
    [Arguments((short)0)]
    [Arguments((short)1)]
    [Arguments(short.MaxValue)]
    public async Task AssignmentVersion_RejectsNegativeAndPreservesFuturePrefix(short version)
    {
        await AssertAssignmentAsync(AssignmentBytes(version), valid: version >= 0);
    }

    [Test]
    [Arguments(-1)]
    [Arguments(int.MinValue)]
    public async Task NegativePartitionId_RetainsBytesWithoutDecodedAssignment(int partition)
    {
        await AssertAssignmentAsync(AssignmentBytes(partition: partition), valid: false);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task EmptyAssignmentTopic_RetainsBytesWithoutDecodedAssignment(int partitions)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16(0);
        writer.WriteInt32(1);
        writer.WriteString(string.Empty);
        writer.WriteInt32(partitions);
        if (partitions != 0)
            writer.WriteInt32(0);
        writer.WriteBytes([]);
        await AssertAssignmentAsync(buffer.WrittenSpan.ToArray(), valid: false);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RetryMetadataFaultRacingCancellation_PreservesCancellationOrTimeout(bool timeout)
    {
        var (admin, connection, _) = CreateAdmin();
        await using var disposal = admin;
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Respond(connection, Group("group", ErrorCode.NotCoordinator));
        connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(Arg.Any<ApiVersionsRequest>(),
                Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys = [new ApiVersion(ApiKey.Metadata, 9, 13), new ApiVersion(ApiKey.DescribeGroups, 5, 5)]
            }));
        connection.SendAsync<MetadataRequest, MetadataResponse>(Arg.Any<MetadataRequest>(),
                Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<MetadataResponse>(FailAfterCancellation(call.Arg<CancellationToken>())));

        var operation = admin.DescribeClassicGroupsAsync(["group"],
            new() { TimeoutMs = timeout ? 1000 : 30000 }, cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (timeout)
            await Assert.That(async () => await operation).Throws<KafkaTimeoutException>();
        else
        {
            cancellation.Cancel();
            await Assert.That(async () => await operation).Throws<OperationCanceledException>();
        }
        await connection.Received(1).SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(
            Arg.Any<DescribeGroupsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());

        async Task<MetadataResponse> FailAfterCancellation(CancellationToken token)
        {
            entered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw new AuthenticationException("refresh denied after cancellation");
            }
            throw new InvalidOperationException("Cancellation wait completed without cancellation.");
        }
    }

    internal static byte[] AssignmentBytes(short version = 0, int partition = 2)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16(version);
        writer.WriteInt32(1);
        writer.WriteString("topic");
        writer.WriteInt32(1);
        writer.WriteInt32(partition);
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

    private static (AdminClient Admin, IKafkaConnection Connection, IConnectionPool Pool) CreateAdmin(
        bool initializeMetadata = true, short describeVersion = 5)
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
        var metadata = new MetadataManager(pool, ["localhost:9092"],
            options: new MetadataOptions { MaxInitRetries = 0, EnableBackgroundRefresh = false });
        if (initializeMetadata) metadata.Metadata.Update(snapshot);
        metadata.SetApiVersion(ApiKey.FindCoordinator, 4, 4);
        metadata.SetApiVersion(ApiKey.DescribeGroups, describeVersion, describeVersion);
        metadata.SetApiVersion(ApiKey.ConsumerGroupDescribe, 0, 1);
        metadata.SetApiVersion(ApiKey.Metadata, 9, 13);
        return (new AdminClient(new AdminClientOptions
        {
            BootstrapServers = ["localhost:9092"], RetryBackoffMs = 1, RetryBackoffMaxMs = 1
        }, pool, metadata), connection, pool);
    }
}
