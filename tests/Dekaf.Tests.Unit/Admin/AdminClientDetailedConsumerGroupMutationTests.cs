using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientDetailedConsumerGroupMutationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task GroupDeletion_DiscoveryFailureDoesNotSkipHealthySiblings(bool deniedFirst)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        var sent = new List<string>();
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<FindCoordinatorRequest>();
                if (request.Key == "denied")
                    throw new KafkaException(ErrorCode.GroupAuthorizationFailed, "discovery denied");
                return ValueTask.FromResult(Coordinator(request, 1));
            });
        Setup(connection, "groups", groups =>
        {
            sent.AddRange(groups);
            return groups.Select(static group => (group, ErrorCode.None)).ToArray();
        });
        var results = await admin.DeleteConsumerGroupsDetailedAsync(deniedFirst
            ? ["denied", "healthy", "sibling"] : ["healthy", "denied", "sibling"]);
        await Assert.That(sent).IsEquivalentTo(["healthy", "sibling"]);
        await Assert.That(results["healthy"].IsSuccess).IsTrue();
        await Assert.That(results["sibling"].IsSuccess).IsTrue();
        await Assert.That(results["denied"].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(results["denied"].Exception).IsTypeOf<KafkaException>();
    }

    [Test]
    [Arguments("io")]
    [Arguments("registration")]
    [Arguments("version")]
    public async Task GroupDeletion_LeaseFailureDoesNotSkipHealthyCoordinator(string failure)
    {
        IKafkaConnection unsupported = new UnsupportedDeleteGroupsConnection();
        var (admin, connection) = CreateAdmin(configurePool: pool =>
            pool.GetConnectionAsync(2, Arg.Any<CancellationToken>()).Returns(_ => failure switch
            {
                "io" => throw new IOException("coordinator connection unavailable"),
                "registration" => throw new InvalidOperationException("coordinator not registered"),
                _ => ValueTask.FromResult(unsupported)
            }));
        await using var owned = admin;
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult(Coordinator(call.Arg<FindCoordinatorRequest>(),
                call.Arg<FindCoordinatorRequest>().Key == "bad" ? 2 : 3)));
        var sent = new List<string>();
        Setup(connection, "groups", groups =>
        {
            sent.AddRange(groups);
            return groups.Select(static group => (group, ErrorCode.None)).ToArray();
        });
        var results = await admin.DeleteConsumerGroupsDetailedAsync(["bad", "good"]);
        await Assert.That(sent).IsEquivalentTo(["good"]);
        await Assert.That(results["good"].IsSuccess).IsTrue();
        await Assert.That(results["bad"].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(results["bad"].Exception).IsNotNull();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    public async Task TopicIdMappingFailurePreservesIndependentOutcomes(bool allMissing, bool loseResponse)
    {
        var goodId = Guid.NewGuid();
        var (admin, connection) = CreateAdmin(OffsetCommitRequest.TopicIdVersion, metadata =>
            metadata.Metadata.Update(new MetadataResponse
            {
                Brokers = [new() { NodeId = 1, Host = "localhost", Port = 9092 }], ControllerId = 1,
                Topics = allMissing ? [] : [new() { Name = "good", TopicId = goodId, ErrorCode = ErrorCode.None, Partitions = [] }]
            }));
        await using var owned = admin;
        var sends = 0;
        connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(Arg.Any<OffsetCommitRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                sends++;
                var request = call.Arg<OffsetCommitRequest>();
                if (request.Topics.Count != 1 || request.Topics[0].TopicId != goodId)
                    throw new InvalidOperationException("The request must contain only the mapped topic.");
                if (loseResponse) throw new IOException("response lost");
                return ValueTask.FromResult(new OffsetCommitResponse
                {
                    Topics = [new() { TopicId = goodId, Partitions = [new() { PartitionIndex = 0, ErrorCode = ErrorCode.None }] }]
                });
            });
        var results = await admin.AlterConsumerGroupOffsetsDetailedAsync("group",
            [new("bad", 0, 10), new("good", 0, 20), new("bad", 1, 30)]);
        await Assert.That(sends).IsEqualTo(allMissing ? 0 : 1);
        await Assert.That(results[new("bad", 0)].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(results[new("bad", 1)].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        var expected = (allMissing, loseResponse) switch
        {
            (true, _) => AdminMutationOutcome.NotAttempted,
            (_, true) => AdminMutationOutcome.Unknown,
            _ => AdminMutationOutcome.Succeeded
        };
        await Assert.That(results[new("good", 0)].Outcome).IsEqualTo(expected);
    }

    [Test]
    public async Task GroupDeletion_BatchesByCoordinatorAndRegroupsOnlyRejectedGroups()
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        var discoveries = new Dictionary<string, int>();
        var requests = new List<string[]>();
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<FindCoordinatorRequest>();
                discoveries.TryGetValue(request.Key, out var count);
                discoveries[request.Key] = ++count;
                var node = request.Key == "third" ? 2 : request.Key == "bad" && count > 1 ? 3 : 1;
                return ValueTask.FromResult(Coordinator(request, node));
            });
        Setup(connection, "groups", groups =>
        {
            requests.Add(groups);
            return groups.Select(group => (group, group == "bad" && discoveries[group] == 1 ? ErrorCode.NotCoordinator : ErrorCode.None)).ToArray();
        });
        var results = await admin.DeleteConsumerGroupsDetailedAsync(["good", "bad", "third"]);
        await Assert.That(results.Values.All(static result => result.IsSuccess)).IsTrue();
        await Assert.That(requests.Count).IsEqualTo(3);
        await Assert.That(requests[0]).IsEquivalentTo(["good", "bad"]);
        await Assert.That(requests[1]).IsEquivalentTo(["third"]);
        await Assert.That(requests[2]).IsEquivalentTo(["bad"]);
        await Assert.That(discoveries["good"]).IsEqualTo(1);
        await Assert.That(discoveries["bad"]).IsEqualTo(2);
        await Assert.That(discoveries["third"]).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task GroupDeletion_LaterFailureNeverReplaysCompletedCoordinator(bool discoveryFailure)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        var goodSends = 0;
        var badSends = 0;
        var badDiscoveries = 0;
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<FindCoordinatorRequest>();
                if (request.Key == "bad" && ++badDiscoveries > 1 && discoveryFailure)
                    throw new KafkaException(ErrorCode.GroupAuthorizationFailed, "discovery denied");
                return ValueTask.FromResult(Coordinator(request, request.Key == "good" ? 1 : 2));
            });
        Setup(connection, "groups", groups =>
        {
            if (groups[0] == "good") { goodSends++; return [("good", ErrorCode.None)]; }
            if (++badSends == 1) return [("bad", ErrorCode.NotCoordinator)];
            throw new IOException("lost response on retry");
        });
        var results = await Invoke(admin, "groups");
        await Assert.That(goodSends).IsEqualTo(1);
        await Assert.That(badSends).IsEqualTo(discoveryFailure ? 1 : 2);
        await Assert.That(results["good"].IsSuccess).IsTrue();
        await Assert.That(results["bad"].Outcome).IsEqualTo(discoveryFailure ? AdminMutationOutcome.Failed : AdminMutationOutcome.Unknown);
        await Assert.That(results["bad"].ErrorCode).IsEqualTo(discoveryFailure ? ErrorCode.NotCoordinator : (ErrorCode?)null);
    }

    [Test]
    public async Task GroupDeletion_CancellationDuringBatchedSendMakesAllSentGroupsUnknown()
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        using var cancellation = new CancellationTokenSource();
        var sends = 0;
        Setup(connection, "groups", groups =>
        {
            sends++;
            if (groups.Length != 2) throw new InvalidOperationException("Expected one coordinator batch.");
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });
        var results = await Invoke(admin, "groups", cancellationToken: cancellation.Token);
        await Assert.That(sends).IsEqualTo(1);
        await Assert.That(results.Values.All(static result => result.Outcome == AdminMutationOutcome.Unknown)).IsTrue();
    }

    [Test]
    [Arguments("groups")]
    [Arguments("alter")]
    [Arguments("delete")]
    public async Task MixedResults_RetainSuccessAndBrokerCode(string operation)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        Setup(connection, operation, names => names.Select(name => (name, name == "good" ? ErrorCode.None : ErrorCode.GroupAuthorizationFailed)).ToArray());
        var results = await Invoke(admin, operation);
        await Assert.That(results["good"].IsSuccess).IsTrue();
        await Assert.That(results["bad"].Outcome).IsEqualTo(AdminMutationOutcome.Failed);
        await Assert.That(results["bad"].ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That(results["bad"].ErrorMessage).IsNull(); // These protocol responses have no error-message field.
    }

    [Test]
    [Arguments("groups", ErrorCode.NotCoordinator)]
    [Arguments("alter", ErrorCode.NotCoordinator)]
    [Arguments("delete", ErrorCode.NotCoordinator)]
    [Arguments("groups", ErrorCode.CoordinatorNotAvailable)]
    [Arguments("alter", ErrorCode.CoordinatorLoadInProgress)]
    [Arguments("delete", ErrorCode.CoordinatorLoadInProgress)]
    public async Task CoordinatorRejection_RediscoverAndRetryOnlyRejectedEntities(string operation, ErrorCode error)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        var goodCalls = 0;
        var badCalls = 0;
        var discoveries = 0;
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                discoveries++;
                return ValueTask.FromResult(Coordinator(call.Arg<FindCoordinatorRequest>(), discoveries));
            });
        Setup(connection, operation, names => names.Select(name =>
        {
            if (name == "good") { goodCalls++; return (name, ErrorCode.None); }
            return (name, ++badCalls == 1 ? error : ErrorCode.None);
        }).ToArray());
        var results = await Invoke(admin, operation);
        await Assert.That(results.Values.All(static result => result.IsSuccess)).IsTrue();
        await Assert.That(goodCalls).IsEqualTo(1);
        await Assert.That(badCalls).IsEqualTo(2);
        await Assert.That(discoveries).IsEqualTo(operation == "groups" ? 3 : 2);
    }

    [Test]
    [Arguments("groups", false)]
    [Arguments("alter", false)]
    [Arguments("delete", false)]
    [Arguments("groups", true)]
    [Arguments("alter", true)]
    [Arguments("delete", true)]
    public async Task AmbiguousFailure_IsUnknownWithoutReplay(string operation, bool brokerTimeout)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        var entitiesSent = 0;
        Setup(connection, operation, names =>
        {
            entitiesSent += names.Length;
            if (!brokerTimeout) throw new IOException("lost response");
            return names.Select(static name => (name, ErrorCode.RequestTimedOut)).ToArray();
        });
        var results = await Invoke(admin, operation);
        await Assert.That(entitiesSent).IsEqualTo(2);
        foreach (var result in results.Values)
        {
            await Assert.That(result.Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
            await Assert.That(result.ErrorCode).IsEqualTo(brokerTimeout ? ErrorCode.RequestTimedOut : (ErrorCode?)null);
        }
    }

    [Test]
    [Arguments("groups")]
    [Arguments("alter")]
    [Arguments("delete")]
    public async Task MissingDuplicateAndUnrequestedEntries_NeverConfirmSuccess(string operation)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        Setup(connection, operation, _ => [("good", ErrorCode.None), ("good", ErrorCode.None), ("other", ErrorCode.None)]);
        var results = await Invoke(admin, operation);
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results.Values.All(static result => result.Outcome == AdminMutationOutcome.Unknown)).IsTrue();
    }

    [Test]
    [Arguments("groups")]
    [Arguments("alter")]
    [Arguments("delete")]
    public async Task CancellationAfterResponse_PreservesSuccessAndConfirmedRejection(string operation)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        using var cancellation = new CancellationTokenSource();
        Setup(connection, operation, names =>
        {
            if (names.Contains("bad")) cancellation.Cancel();
            return names.Select(name => (name, name == "good" ? ErrorCode.None : ErrorCode.NotCoordinator)).ToArray();
        });
        var results = await Invoke(admin, operation, cancellationToken: cancellation.Token);
        await Assert.That(results["good"].IsSuccess).IsTrue();
        await Assert.That(results["bad"].Outcome).IsEqualTo(AdminMutationOutcome.Failed);
        await Assert.That(results["bad"].ErrorCode).IsEqualTo(ErrorCode.NotCoordinator);
    }

    [Test]
    [Arguments("groups")]
    [Arguments("alter")]
    [Arguments("delete")]
    public async Task ZeroTimeout_IsNotAttempted(string operation)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        var results = await Invoke(admin, operation, timeoutMs: 0);
        foreach (var result in results.Values)
        {
            await Assert.That(result.Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
            await Assert.That(result.Exception).IsTypeOf<KafkaTimeoutException>();
        }
        await Assert.That(connection.ReceivedCalls()).IsEmpty();
    }

    [Test]
    [Arguments("groups")]
    [Arguments("alter")]
    [Arguments("delete")]
    public async Task CancellationDuringSend_IsUnknownAndLeavesUnsentGroupsNotAttempted(string operation)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        using var cancellation = new CancellationTokenSource();
        if (operation == "groups") UseSeparateGroupCoordinators(connection);
        Setup(connection, operation, _ => { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); });
        var results = await Invoke(admin, operation, cancellationToken: cancellation.Token);
        await Assert.That(results["good"].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
        await Assert.That(results["bad"].Outcome).IsEqualTo(operation == "groups" ? AdminMutationOutcome.NotAttempted : AdminMutationOutcome.Unknown);
    }

    [Test]
    public async Task DeleteOffsets_GroupErrorAppliesToEveryRequestedPartition()
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        connection.SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(Arg.Any<OffsetDeleteRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new OffsetDeleteResponse { ErrorCode = ErrorCode.GroupIdNotFound, Topics = [] }));
        var results = await Invoke(admin, "delete");
        await Assert.That(results.Values.All(static result => result.ErrorCode == ErrorCode.GroupIdNotFound && result.Outcome == AdminMutationOutcome.Failed)).IsTrue();
    }

    [Test]
    public async Task AlterOffsets_RetryPreservesExactPartitionPayload()
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        var requests = new List<OffsetCommitRequest>();
        connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(Arg.Any<OffsetCommitRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<OffsetCommitRequest>();
                requests.Add(request);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [new() { Name = "topic", Partitions = request.Topics[0].Partitions
                    .Select(partition => new OffsetCommitResponsePartition { PartitionIndex = partition.PartitionIndex,
                        ErrorCode = requests.Count == 1 && partition.PartitionIndex == 1 ? ErrorCode.NotCoordinator : ErrorCode.None }).ToArray() }] });
            });
        var results = await admin.AlterConsumerGroupOffsetsDetailedAsync("group", [new("topic", 0, 12), new("topic", 1, 24) { LeaderEpoch = 7, Metadata = "retained" }]);
        await Assert.That(results.Values.All(static result => result.IsSuccess)).IsTrue();
        await Assert.That(requests.Count).IsEqualTo(2);
        var retried = requests[1].Topics.Single().Partitions.Single();
        await Assert.That(retried.PartitionIndex).IsEqualTo(1);
        await Assert.That(retried.CommittedOffset).IsEqualTo(24);
        await Assert.That(retried.CommittedLeaderEpoch).IsEqualTo(7);
        await Assert.That(retried.CommittedMetadata).IsEqualTo("retained");
        await Assert.That(requests[1].GenerationIdOrMemberEpoch).IsEqualTo(-1);
        await Assert.That(requests[1].MemberId).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task EmptyInvalidAndPreCanceledInputs_DoNotSend()
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        await Assert.That((await admin.DeleteConsumerGroupsDetailedAsync([])).Count).IsEqualTo(0);
        await Assert.That((await admin.AlterConsumerGroupOffsetsDetailedAsync("group", [])).Count).IsEqualTo(0);
        await Assert.That((await admin.DeleteConsumerGroupOffsetsDetailedAsync("group", [])).Count).IsEqualTo(0);
        await Assert.ThrowsAsync<ArgumentNullException>(() => admin.DeleteConsumerGroupsDetailedAsync(null!).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => admin.DeleteConsumerGroupsDetailedAsync(["duplicate", "duplicate"]).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => admin.AlterConsumerGroupOffsetsDetailedAsync(" ", []).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => admin.AlterConsumerGroupOffsetsDetailedAsync("group", [new("topic", 0, 1), new("topic", 0, 2)]).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => admin.DeleteConsumerGroupOffsetsDetailedAsync("group", [new("topic", 0), new("topic", 0)]).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => admin.DeleteConsumerGroupOffsetsDetailedAsync("group", [new("topic", -1)]).AsTask());
        foreach (var operation in new[] { "groups", "alter", "delete" })
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Invoke(admin, operation, -1).AsTask());
            await Assert.ThrowsAsync<OperationCanceledException>(() => Invoke(admin, operation, cancellationToken: new(true)).AsTask());
        }
        await Assert.That(connection.ReceivedCalls()).IsEmpty();
        IAdminClient unsupported = Substitute.For<IAdminClient>();
        await Assert.ThrowsAsync<NotSupportedException>(() => unsupported.DeleteConsumerGroupsDetailedAsync([]).AsTask());
        await Assert.ThrowsAsync<NotSupportedException>(() => unsupported.AlterConsumerGroupOffsetsDetailedAsync("group", []).AsTask());
        await Assert.ThrowsAsync<NotSupportedException>(() => unsupported.DeleteConsumerGroupOffsetsDetailedAsync("group", []).AsTask());
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    public async Task TopicIdResponse_RequiresCurrentUniqueRequestedIdentity(bool stale, bool duplicate)
    {
        var goodId = Guid.NewGuid();
        var badId = Guid.NewGuid();
        MetadataManager? manager = null;
        MetadataResponse Metadata(Guid currentGoodId) => new()
        {
            Brokers = [new() { NodeId = 1, Host = "localhost", Port = 9092 }], ControllerId = 1,
            Topics = [new() { Name = "good", TopicId = currentGoodId, ErrorCode = ErrorCode.None, Partitions = [] },
                new() { Name = "bad", TopicId = badId, ErrorCode = ErrorCode.None, Partitions = [] }]
        };
        var (admin, connection) = CreateAdmin(OffsetCommitRequest.TopicIdVersion, metadata =>
        {
            manager = metadata;
            metadata.Metadata.Update(Metadata(goodId));
        });
        await using var owned = admin;
        connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(Arg.Any<OffsetCommitRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<OffsetCommitRequest>();
                if (call.Arg<short>() != OffsetCommitRequest.TopicIdVersion || request.Topics[0].TopicId != goodId || request.Topics[1].TopicId != badId)
                    throw new InvalidOperationException("Request did not capture the requested topic IDs.");
                if (stale) manager!.Metadata.Update(Metadata(Guid.NewGuid()));
                var topics = new List<OffsetCommitResponseTopic>
                {
                    new() { TopicId = badId, Partitions = [new() { PartitionIndex = 0, ErrorCode = ErrorCode.None }] },
                    new() { TopicId = goodId, Partitions = [new() { PartitionIndex = 0, ErrorCode = ErrorCode.None }] },
                    new() { TopicId = Guid.NewGuid(), Partitions = [new() { PartitionIndex = 0, ErrorCode = ErrorCode.None }] }
                };
                if (duplicate) topics.Add(topics[1]);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = topics });
            });
        var results = await Invoke(admin, "alter");
        await Assert.That(results["bad"].IsSuccess).IsTrue();
        await Assert.That(results["good"].Outcome).IsEqualTo(stale || duplicate ? AdminMutationOutcome.Unknown : AdminMutationOutcome.Succeeded);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DuplicateMetadataTopicIds_ExcludeEveryAmbiguousTopicAndPreserveSiblings(bool onlyAmbiguous)
    {
        var duplicateId = Guid.NewGuid();
        var healthyId = Guid.NewGuid();
        var (admin, connection) = CreateAdmin(OffsetCommitRequest.TopicIdVersion, metadata =>
            metadata.Metadata.Update(new MetadataResponse
            {
                Brokers = [new() { NodeId = 1, Host = "localhost", Port = 9092 }], ControllerId = 1,
                Topics = [new() { Name = "first", TopicId = duplicateId, ErrorCode = ErrorCode.None, Partitions = [] },
                    new() { Name = "second", TopicId = duplicateId, ErrorCode = ErrorCode.None, Partitions = [] },
                    new() { Name = "third", TopicId = duplicateId, ErrorCode = ErrorCode.None, Partitions = [] },
                    new() { Name = "healthy", TopicId = healthyId, ErrorCode = ErrorCode.None, Partitions = [] }]
            }));
        await using var owned = admin;
        var requests = new List<OffsetCommitRequest>();
        connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(Arg.Any<OffsetCommitRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                requests.Add(call.Arg<OffsetCommitRequest>());
                return ValueTask.FromResult(new OffsetCommitResponse
                {
                    Topics = [new() { TopicId = healthyId, Partitions = [new() { PartitionIndex = 0, ErrorCode = ErrorCode.None }] }]
                });
            });
        var offsets = new List<TopicPartitionOffset> { new("first", 0, 10), new("first", 1, 20) };
        if (!onlyAmbiguous) offsets.Add(new("healthy", 0, 30));
        offsets.Add(new("second", 0, 40));
        offsets.Add(new("third", 0, 50));
        var results = await admin.AlterConsumerGroupOffsetsDetailedAsync("group", offsets);
        await Assert.That(results.Count).IsEqualTo(offsets.Count);
        foreach (var offset in offsets)
            await Assert.That(results[new(offset.Topic, offset.Partition)].Outcome).IsEqualTo(
                offset.Topic == "healthy" ? AdminMutationOutcome.Succeeded : AdminMutationOutcome.NotAttempted);
        await Assert.That(requests.Count).IsEqualTo(onlyAmbiguous ? 0 : 1);
        if (!onlyAmbiguous)
        {
            await Assert.That(requests[0].Topics.Count).IsEqualTo(1);
            await Assert.That(requests[0].Topics[0].TopicId).IsEqualTo(healthyId);
        }
    }

    [Test]
    [Arguments(ErrorCode.None)]
    [Arguments(ErrorCode.NotCoordinator)]
    public async Task DuplicateResponseTopicIds_InvalidateDisjointPartitionsWithoutRetry(ErrorCode firstError)
    {
        var duplicateId = Guid.NewGuid();
        var healthyId = Guid.NewGuid();
        var (admin, connection) = CreateAdmin(OffsetCommitRequest.TopicIdVersion, metadata =>
            metadata.Metadata.Update(new MetadataResponse
            {
                Brokers = [new() { NodeId = 1, Host = "localhost", Port = 9092 }], ControllerId = 1,
                Topics = [new() { Name = "duplicate", TopicId = duplicateId, ErrorCode = ErrorCode.None, Partitions = [] },
                    new() { Name = "healthy", TopicId = healthyId, ErrorCode = ErrorCode.None, Partitions = [] }]
            }));
        await using var owned = admin;
        var sends = 0;
        connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(Arg.Any<OffsetCommitRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                sends++;
                return ValueTask.FromResult(new OffsetCommitResponse
                {
                    Topics = [new() { TopicId = duplicateId, Partitions = [new() { PartitionIndex = 0, ErrorCode = firstError }] },
                        new() { TopicId = healthyId, Partitions = [new() { PartitionIndex = 0, ErrorCode = ErrorCode.None }] },
                        new() { TopicId = duplicateId, Partitions = [new() { PartitionIndex = 1, ErrorCode = ErrorCode.None }] },
                        new() { TopicId = duplicateId, Partitions = [new() { PartitionIndex = 2, ErrorCode = ErrorCode.None }] }]
                });
            });
        var results = await admin.AlterConsumerGroupOffsetsDetailedAsync("group",
            [new("duplicate", 0, 10), new("duplicate", 1, 20), new("duplicate", 2, 30), new("healthy", 0, 40)]);
        await Assert.That(results.Count).IsEqualTo(4);
        await Assert.That(results[new("healthy", 0)].IsSuccess).IsTrue();
        for (var partition = 0; partition < 3; partition++)
            await Assert.That(results[new("duplicate", partition)].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
        await Assert.That(sends).IsEqualTo(1);
    }

    [Test]
    [Arguments("groups", ApiKey.DeleteGroups)]
    [Arguments("alter", ApiKey.OffsetCommit)]
    [Arguments("delete", ApiKey.OffsetDelete)]
    public async Task UnsupportedBroker_IsNotAttempted(string operation, ApiKey apiKey)
    {
        var (admin, _) = CreateAdmin(configure: metadata => metadata.SetApiVersion(apiKey, 100, 100));
        await using var owned = admin;
        var results = await Invoke(admin, operation);
        foreach (var result in results.Values)
        {
            await Assert.That(result.Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
            await Assert.That(result.ErrorCode).IsNull();
            await Assert.That(result.Exception).IsTypeOf<BrokerVersionException>();
        }
    }

    [Test]
    public async Task DeadlineDuringSend_IsUnknownAndDoesNotResetForNextGroup()
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        UseSeparateGroupCoordinators(connection);
        var sent = 0;
        connection.SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(Arg.Any<DeleteGroupsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                sent++;
                return BlockUntilCanceled(call.Arg<CancellationToken>());
            });
        var results = await Invoke(admin, "groups", timeoutMs: 100);
        await Assert.That(sent).IsEqualTo(1);
        await Assert.That(results["good"].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
        await Assert.That(results["good"].Exception).IsTypeOf<KafkaTimeoutException>();
        await Assert.That(results["bad"].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(results["bad"].Exception).IsTypeOf<KafkaTimeoutException>();
    }

    private static async ValueTask<DeleteGroupsResponse> BlockUntilCanceled(CancellationToken token)
    {
        await Task.Delay(Timeout.Infinite, token);
        throw new InvalidOperationException("The deadline did not interrupt the request.");
    }

    private static void UseSeparateGroupCoordinators(IKafkaConnection connection) =>
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult(Coordinator(call.Arg<FindCoordinatorRequest>(), call.Arg<FindCoordinatorRequest>().Key == "good" ? 1 : 2)));

    private static (AdminClient Admin, IKafkaConnection Connection) CreateAdmin(short offsetCommitVersion = 8,
        Action<MetadataManager>? configure = null, Action<IConnectionPool>? configurePool = null)
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.BrokerId.Returns(1);
        connection.Host.Returns("localhost");
        connection.Port.Returns(9092);
        connection.IsConnected.Returns(true);
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(connection));
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(connection));
        configurePool?.Invoke(pool);
        var metadata = new MetadataResponse { Brokers = [new() { NodeId = 1, Host = "localhost", Port = 9092 }], ControllerId = 1, Topics = [] };
        var manager = new MetadataManager(pool, ["localhost:9092"]);
        manager.Metadata.Update(metadata);
        foreach (var key in new[] { ApiKey.Metadata, ApiKey.FindCoordinator, ApiKey.DeleteGroups, ApiKey.OffsetCommit, ApiKey.OffsetDelete })
            manager.SetApiVersion(key, 0, key == ApiKey.OffsetCommit ? offsetCommitVersion : (short)99);
        configure?.Invoke(manager);
        var result = (Admin: new AdminClient(new() { BootstrapServers = ["localhost:9092"], RetryBackoffMs = 1, RetryBackoffMaxMs = 1 }, pool, manager), Connection: connection);
        connection.SendAsync<MetadataRequest, MetadataResponse>(Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(metadata));
        result.Connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(Arg.Any<ApiVersionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ApiVersionsResponse { ErrorCode = ErrorCode.None, ApiKeys = new[] { ApiKey.Metadata, ApiKey.FindCoordinator, ApiKey.DeleteGroups, ApiKey.OffsetCommit, ApiKey.OffsetDelete }
                .Select(key => new ApiVersion(key, 0, key == ApiKey.OffsetCommit ? offsetCommitVersion : (short)99)).ToArray() }));
        result.Connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult(Coordinator(call.Arg<FindCoordinatorRequest>(), 1)));
        return result;
    }

    private static FindCoordinatorResponse Coordinator(FindCoordinatorRequest request, int nodeId) => new()
    {
        Coordinators = [new Coordinator { Key = request.Key, NodeId = nodeId, Host = "localhost", Port = 9092 }]
    };

    private sealed class UnsupportedDeleteGroupsConnection : IKafkaConnection, IKafkaCapabilityProvider
    {
        public int BrokerId => 2;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public KafkaConnectionCapabilities Capabilities { get; } = KafkaConnectionCapabilities.Create(new ApiVersionsResponse
        {
            ErrorCode = ErrorCode.None, ApiKeys = [new(ApiKey.DeleteGroups, 100, 100)]
        });
        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new InvalidOperationException("Unsupported mutation was sent.");
        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
    }

    private static async ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> Invoke(AdminClient admin, string operation,
        int timeoutMs = 30000, CancellationToken cancellationToken = default)
    {
        if (operation == "groups") return await admin.DeleteConsumerGroupsDetailedAsync(["good", "bad"], new() { TimeoutMs = timeoutMs }, cancellationToken);
        var results = operation == "alter"
            ? await admin.AlterConsumerGroupOffsetsDetailedAsync("group", [new("good", 0, 12), new("bad", 0, 24)], new() { TimeoutMs = timeoutMs }, cancellationToken)
            : await admin.DeleteConsumerGroupOffsetsDetailedAsync("group", [new("good", 0), new("bad", 0)], new() { TimeoutMs = timeoutMs }, cancellationToken);
        return results.ToDictionary(static pair => pair.Key.Topic, static pair => pair.Value);
    }

    private static void Setup(IKafkaConnection connection, string operation, Func<string[], (string Name, ErrorCode Code)[]> respond)
    {
        if (operation == "groups")
            connection.SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(Arg.Any<DeleteGroupsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => ValueTask.FromResult(new DeleteGroupsResponse { Results = respond(call.Arg<DeleteGroupsRequest>().GroupsNames.ToArray())
                    .Select(static item => new DeleteGroupsResponseResult { GroupId = item.Name, ErrorCode = item.Code }).ToArray() }));
        else if (operation == "alter")
            connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(Arg.Any<OffsetCommitRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => ValueTask.FromResult(new OffsetCommitResponse { Topics = respond(call.Arg<OffsetCommitRequest>().Topics.Select(static topic => topic.Name).ToArray())
                    .Select(static item => new OffsetCommitResponseTopic { Name = item.Name, Partitions = [new() { PartitionIndex = 0, ErrorCode = item.Code }] }).ToArray() }));
        else
            connection.SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(Arg.Any<OffsetDeleteRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => ValueTask.FromResult(new OffsetDeleteResponse { Topics = respond(call.Arg<OffsetDeleteRequest>().Topics.Select(static topic => topic.Name).ToArray())
                    .Select(static item => new OffsetDeleteResponseTopic { Name = item.Name, Partitions = [new() { PartitionIndex = 0, ErrorCode = item.Code }] }).ToArray() }));
    }
}
