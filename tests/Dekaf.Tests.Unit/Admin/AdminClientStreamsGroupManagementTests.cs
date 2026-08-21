using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientStreamsGroupManagementTests
{
    private const string FirstGroup = "streams-a";
    private const string SecondGroup = "streams-b";
    private const string Topic = "input";
    private static readonly Guid TopicId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

    [Test]
    public async Task Operations_RejectInvalidInputs()
    {
        var (admin, _) = CreateAdmin();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await admin.ListStreamsGroupOffsetsAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await admin.ListStreamsGroupOffsetsAsync(new Dictionary<string, ListStreamsGroupOffsetsSpec>
            {
                [""] = new()
            }));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await admin.AlterStreamsGroupOffsetsAsync(FirstGroup,
                [new TopicPartitionOffset(Topic, 0, 1), new TopicPartitionOffset(Topic, 0, 2)]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await admin.AlterStreamsGroupOffsetsAsync(FirstGroup,
                [new TopicPartitionOffset(Topic, 0, -1)]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await admin.DeleteStreamsGroupOffsetsAsync(
                FirstGroup,
                [],
                new DeleteStreamsGroupOffsetsOptions { TimeoutMs = -1 }));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await admin.DeleteStreamsGroupsAsync([FirstGroup, FirstGroup]));
    }

    [Test]
    public async Task Operations_EmptyInputsReturnWithoutNetworkCalls()
    {
        var (admin, connection) = CreateAdmin();

        await Assert.That(await admin.ListStreamsGroupOffsetsAsync(
            new Dictionary<string, ListStreamsGroupOffsetsSpec>())).IsEmpty();
        await Assert.That(await admin.AlterStreamsGroupOffsetsAsync(FirstGroup, [])).IsEmpty();
        await Assert.That(await admin.DeleteStreamsGroupOffsetsAsync(FirstGroup, [])).IsEmpty();
        await Assert.That(await admin.DeleteStreamsGroupsAsync([])).IsEmpty();

        await connection.DidNotReceive().SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            Arg.Any<FindCoordinatorRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Operations_PreCancelledTokenStopsBeforeNetworkCalls()
    {
        var (admin, connection) = CreateAdmin();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await admin.DeleteStreamsGroupsAsync([FirstGroup], cancellationToken: cancellation.Token));

        await connection.DidNotReceive().SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            Arg.Any<FindCoordinatorRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListStreamsGroupOffsetsAsync_PreservesGroupAndPartitionErrors()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.ArgAt<OffsetFetchRequest>(0);
                return ValueTask.FromResult(new OffsetFetchResponse
                {
                    Groups =
                    [
                        new OffsetFetchResponseGroup
                        {
                            GroupId = request.Groups![0].GroupId,
                            ErrorCode = ErrorCode.GroupAuthorizationFailed,
                            Topics = []
                        },
                        new OffsetFetchResponseGroup
                        {
                            GroupId = request.Groups[1].GroupId,
                            ErrorCode = ErrorCode.None,
                            Topics =
                            [
                                new OffsetFetchResponseTopic
                                {
                                    TopicId = TopicId,
                                    Partitions =
                                    [
                                        Offset(0, 42, ErrorCode.None),
                                        Offset(1, -1, ErrorCode.TopicAuthorizationFailed)
                                    ]
                                }
                            ]
                        }
                    ]
                });
            });

        var specs = new Dictionary<string, ListStreamsGroupOffsetsSpec>
        {
            [FirstGroup] = new() { TopicPartitions = [new TopicPartition(Topic, 0)] },
            [SecondGroup] = new() { TopicPartitions = [new TopicPartition(Topic, 0), new TopicPartition(Topic, 1)] }
        };
        var results = await admin.ListStreamsGroupOffsetsAsync(specs, new ListStreamsGroupOffsetsOptions
        {
            RequireStable = true
        });

        await Assert.That(results[FirstGroup].ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That(results[SecondGroup].Offsets[new TopicPartition(Topic, 0)].Offset).IsEqualTo(42);
        await Assert.That(results[SecondGroup].Offsets[new TopicPartition(Topic, 1)].ErrorCode)
            .IsEqualTo(ErrorCode.TopicAuthorizationFailed);
        await connection.Received(1).SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
            Arg.Is<OffsetFetchRequest>(request =>
                request != null && request.RequireStable && request.Groups!.Count == 2 &&
                request.Groups.All(group => group.Topics![0].TopicId == TopicId)),
            10,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListStreamsGroupOffsetsAsync_PreservesCoordinatorErrorAndListsSiblingGroup()
    {
        var (admin, connection) = CreateAdmin();
        SetupSelectiveFindCoordinatorError(connection);
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new OffsetFetchResponse
            {
                Groups =
                [
                    new OffsetFetchResponseGroup
                    {
                        GroupId = SecondGroup,
                        ErrorCode = ErrorCode.None,
                        Topics = []
                    }
                ]
            }));
        var specs = new Dictionary<string, ListStreamsGroupOffsetsSpec>
        {
            [FirstGroup] = new(),
            [SecondGroup] = new()
        };

        var results = await admin.ListStreamsGroupOffsetsAsync(specs);

        await Assert.That(results[FirstGroup].ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That(results[SecondGroup].ErrorCode).IsEqualTo(ErrorCode.None);
        await connection.Received(1).SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
            Arg.Is<OffsetFetchRequest>(request =>
                request.Groups!.Count == 1 && request.Groups[0].GroupId == SecondGroup),
            9,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListStreamsGroupOffsetsAsync_FetchAllNegotiatesV9()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new OffsetFetchResponse
            {
                Groups =
                [
                    new OffsetFetchResponseGroup
                    {
                        GroupId = FirstGroup,
                        ErrorCode = ErrorCode.None,
                        Topics = []
                    }
                ]
            }));

        await admin.ListStreamsGroupOffsetsAsync(new Dictionary<string, ListStreamsGroupOffsetsSpec>
        {
            [FirstGroup] = new()
        });

        await connection.Received(1).SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
            Arg.Any<OffsetFetchRequest>(),
            9,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListStreamsGroupOffsetsAsync_RetriesTransientPartitionErrors()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(
                ValueTask.FromResult(ListResponse(Offset(0, -1, ErrorCode.UnstableOffsetCommit))),
                ValueTask.FromResult(ListResponse(Offset(0, 42, ErrorCode.None))));

        var results = await admin.ListStreamsGroupOffsetsAsync(
            new Dictionary<string, ListStreamsGroupOffsetsSpec>
            {
                [FirstGroup] = new() { TopicPartitions = [new TopicPartition(Topic, 0)] }
            },
            new ListStreamsGroupOffsetsOptions { RequireStable = true });

        await Assert.That(results[FirstGroup].Offsets[new TopicPartition(Topic, 0)].Offset).IsEqualTo(42);
        await connection.Received(2).SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
            Arg.Any<OffsetFetchRequest>(),
            10,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListStreamsGroupOffsetsAsync_TopicIdMismatchPreservesSiblingGroupResult()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        var mismatchTopicId = Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100");
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(
                ValueTask.FromResult(new OffsetFetchResponse
                {
                    Groups =
                    [
                        new OffsetFetchResponseGroup
                        {
                            GroupId = FirstGroup,
                            ErrorCode = ErrorCode.None,
                            Topics =
                            [
                                new OffsetFetchResponseTopic
                                {
                                    TopicId = mismatchTopicId,
                                    Partitions = [Offset(0, 10, ErrorCode.None)]
                                }
                            ]
                        },
                        new OffsetFetchResponseGroup
                        {
                            GroupId = SecondGroup,
                            ErrorCode = ErrorCode.None,
                            Topics =
                            [
                                new OffsetFetchResponseTopic
                                {
                                    TopicId = TopicId,
                                    Partitions = [Offset(0, 20, ErrorCode.None)]
                                }
                            ]
                        }
                    ]
                }),
                ValueTask.FromResult(ListResponse(Offset(0, 10, ErrorCode.None))));

        var results = await admin.ListStreamsGroupOffsetsAsync(
            new Dictionary<string, ListStreamsGroupOffsetsSpec>
            {
                [FirstGroup] = new() { TopicPartitions = [new TopicPartition(Topic, 0)] },
                [SecondGroup] = new() { TopicPartitions = [new TopicPartition(Topic, 0)] }
            });

        await Assert.That(results[FirstGroup].Offsets[new TopicPartition(Topic, 0)].Offset).IsEqualTo(10);
        await Assert.That(results[SecondGroup].Offsets[new TopicPartition(Topic, 0)].Offset).IsEqualTo(20);
        await connection.Received(2).SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
            Arg.Any<OffsetFetchRequest>(),
            10,
            Arg.Any<CancellationToken>());
        await connection.Received(1).SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
            Arg.Is<OffsetFetchRequest>(request =>
                request != null && request.Groups!.Count == 1 && request.Groups[0].GroupId == FirstGroup),
            10,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListStreamsGroupOffsetsAsync_RequireStableRejectsOffsetFetchV6()
    {
        var (admin, connection) = CreateAdmin(offsetFetchMaxVersion: 6);
        SetupFindCoordinator(connection);

        await Assert.ThrowsAsync<BrokerVersionException>(async () =>
            await admin.ListStreamsGroupOffsetsAsync(
                new Dictionary<string, ListStreamsGroupOffsetsSpec>
                {
                    [FirstGroup] = new() { TopicPartitions = [new TopicPartition(Topic, 0)] }
                },
                new ListStreamsGroupOffsetsOptions { RequireStable = true }));

        await connection.DidNotReceive().SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
            Arg.Any<OffsetFetchRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AlterStreamsGroupOffsetsAsync_PreservesPartitionErrorsAndTopicIds()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new OffsetCommitResponse
            {
                Topics =
                [
                    new OffsetCommitResponseTopic
                    {
                        TopicId = TopicId,
                        Partitions =
                        [
                            Commit(0, ErrorCode.None),
                            Commit(1, ErrorCode.TopicAuthorizationFailed)
                        ]
                    }
                ]
            }));

        var results = await admin.AlterStreamsGroupOffsetsAsync(FirstGroup,
            [new TopicPartitionOffset(Topic, 0, 42), new TopicPartitionOffset(Topic, 1, 43)]);

        await Assert.That(results[new TopicPartition(Topic, 0)].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results[new TopicPartition(Topic, 1)].ErrorCode)
            .IsEqualTo(ErrorCode.TopicAuthorizationFailed);
        await connection.Received(1).SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
            Arg.Is<OffsetCommitRequest>(request => request != null && request.Topics[0].TopicId == TopicId),
            10,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OffsetMutations_PreserveCoordinatorLookupErrors()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinatorError(connection, ErrorCode.GroupAuthorizationFailed);
        var partition = new TopicPartition(Topic, 0);

        var altered = await admin.AlterStreamsGroupOffsetsAsync(
            FirstGroup,
            [new TopicPartitionOffset(partition.Topic, partition.Partition, 42)]);
        var deleted = await admin.DeleteStreamsGroupOffsetsAsync(FirstGroup, [partition]);

        await Assert.That(altered[partition].ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That(deleted[partition].ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await connection.DidNotReceive().SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
            Arg.Any<OffsetCommitRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
        await connection.DidNotReceive().SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
            Arg.Any<OffsetDeleteRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AlterStreamsGroupOffsetsAsync_RetriesTransientAndMissingPartitions()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(
                ValueTask.FromResult(CommitResponse(Commit(0, ErrorCode.RequestTimedOut))),
                ValueTask.FromResult(CommitResponse(
                    Commit(0, ErrorCode.None),
                    Commit(1, ErrorCode.None))));

        var results = await admin.AlterStreamsGroupOffsetsAsync(FirstGroup,
            [new TopicPartitionOffset(Topic, 0, 42), new TopicPartitionOffset(Topic, 1, 43)]);

        await Assert.That(results.Values.All(result => result.ErrorCode == ErrorCode.None)).IsTrue();
        await connection.Received(2).SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
            Arg.Any<OffsetCommitRequest>(),
            10,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AlterStreamsGroupOffsetsAsync_ReturnsFinalRetriablePartitionErrors()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CommitResponse(
                Commit(0, ErrorCode.None),
                Commit(1, ErrorCode.RequestTimedOut))));
        var firstPartition = new TopicPartition(Topic, 0);
        var secondPartition = new TopicPartition(Topic, 1);

        var results = await admin.AlterStreamsGroupOffsetsAsync(FirstGroup,
            [new TopicPartitionOffset(Topic, 0, 42), new TopicPartitionOffset(Topic, 1, 43)]);

        await Assert.That(results[firstPartition].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results[secondPartition].ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
        await connection.Received(4).SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
            Arg.Any<OffsetCommitRequest>(),
            10,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteStreamsGroupOffsetsAsync_PreservesPartitionErrors()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
                Arg.Any<OffsetDeleteRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new OffsetDeleteResponse
            {
                ErrorCode = ErrorCode.None,
                Topics =
                [
                    new OffsetDeleteResponseTopic
                    {
                        Name = Topic,
                        Partitions =
                        [
                            Deleted(0, ErrorCode.None),
                            Deleted(1, ErrorCode.GroupSubscribedToTopic)
                        ]
                    }
                ]
            }));

        var results = await admin.DeleteStreamsGroupOffsetsAsync(FirstGroup,
            [new TopicPartition(Topic, 0), new TopicPartition(Topic, 1)]);

        await Assert.That(results[new TopicPartition(Topic, 0)].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results[new TopicPartition(Topic, 1)].ErrorCode)
            .IsEqualTo(ErrorCode.GroupSubscribedToTopic);
        await connection.Received(1).SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
            Arg.Any<OffsetDeleteRequest>(),
            0,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteStreamsGroupOffsetsAsync_RetriesTransientPartitionErrors()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
                Arg.Any<OffsetDeleteRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(
                ValueTask.FromResult(DeleteOffsetsResponse(Deleted(0, ErrorCode.RequestTimedOut))),
                ValueTask.FromResult(DeleteOffsetsResponse(Deleted(0, ErrorCode.None))));

        var partition = new TopicPartition(Topic, 0);
        var results = await admin.DeleteStreamsGroupOffsetsAsync(FirstGroup, [partition]);

        await Assert.That(results[partition].ErrorCode).IsEqualTo(ErrorCode.None);
        await connection.Received(2).SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
            Arg.Any<OffsetDeleteRequest>(),
            0,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteStreamsGroupOffsetsAsync_ReturnsFinalRetriablePartitionErrors()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
                Arg.Any<OffsetDeleteRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(DeleteOffsetsResponse(
                Deleted(0, ErrorCode.None),
                Deleted(1, ErrorCode.RequestTimedOut))));
        var firstPartition = new TopicPartition(Topic, 0);
        var secondPartition = new TopicPartition(Topic, 1);

        var results = await admin.DeleteStreamsGroupOffsetsAsync(
            FirstGroup,
            [firstPartition, secondPartition]);

        await Assert.That(results[firstPartition].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results[secondPartition].ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
        await connection.Received(4).SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
            Arg.Any<OffsetDeleteRequest>(),
            0,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteStreamsGroupOffsetsAsync_RetriesMetadataRefreshGroupErrors()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
                Arg.Any<OffsetDeleteRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(
                ValueTask.FromResult(new OffsetDeleteResponse
                {
                    ErrorCode = ErrorCode.BrokerNotAvailable,
                    Topics = []
                }),
                ValueTask.FromResult(DeleteOffsetsResponse(Deleted(0, ErrorCode.None))));
        var partition = new TopicPartition(Topic, 0);

        var results = await admin.DeleteStreamsGroupOffsetsAsync(FirstGroup, [partition]);

        await Assert.That(results[partition].ErrorCode).IsEqualTo(ErrorCode.None);
        await connection.Received(2).SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
            Arg.Any<OffsetDeleteRequest>(),
            0,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteStreamsGroupOffsetsAsync_RetriesOnlyMetadataRefreshPartitionErrors()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
                Arg.Any<OffsetDeleteRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(
                ValueTask.FromResult(DeleteOffsetsResponse(
                    Deleted(0, ErrorCode.None),
                    Deleted(1, ErrorCode.BrokerNotAvailable))),
                ValueTask.FromResult(DeleteOffsetsResponse(Deleted(1, ErrorCode.None))));
        var firstPartition = new TopicPartition(Topic, 0);
        var secondPartition = new TopicPartition(Topic, 1);

        var results = await admin.DeleteStreamsGroupOffsetsAsync(FirstGroup, [firstPartition, secondPartition]);

        await Assert.That(results.Values.All(static result => result.ErrorCode == ErrorCode.None)).IsTrue();
        await connection.Received(1).SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
            Arg.Is<OffsetDeleteRequest>(request =>
                request.Topics.Count == 1 &&
                request.Topics[0].Partitions.Count == 1 &&
                request.Topics[0].Partitions[0].PartitionIndex == 1),
            0,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteStreamsGroupOffsetsAsync_GroupMissingAfterPartitionRetryIsSuccess()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
                Arg.Any<OffsetDeleteRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(
                ValueTask.FromResult(DeleteOffsetsResponse(Deleted(0, ErrorCode.RequestTimedOut))),
                ValueTask.FromResult(new OffsetDeleteResponse
                {
                    ErrorCode = ErrorCode.GroupIdNotFound,
                    Topics = []
                }));
        var partition = new TopicPartition(Topic, 0);

        var results = await admin.DeleteStreamsGroupOffsetsAsync(FirstGroup, [partition]);

        await Assert.That(results[partition].ErrorCode).IsEqualTo(ErrorCode.None);
        await connection.Received(2).SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
            Arg.Any<OffsetDeleteRequest>(),
            0,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteStreamsGroupOffsetsAsync_GroupMissingConfirmsOnlyAmbiguousPartitions()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
                Arg.Any<OffsetDeleteRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(
                ValueTask.FromResult(DeleteOffsetsResponse(Deleted(0, ErrorCode.RequestTimedOut))),
                ValueTask.FromResult(new OffsetDeleteResponse
                {
                    ErrorCode = ErrorCode.GroupIdNotFound,
                    Topics = []
                }));
        var ambiguous = new TopicPartition(Topic, 0);
        var omitted = new TopicPartition(Topic, 1);

        var results = await admin.DeleteStreamsGroupOffsetsAsync(FirstGroup, [ambiguous, omitted]);

        await Assert.That(results[ambiguous].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results[omitted].ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
    }

    [Test]
    public async Task DeleteStreamsGroupOffsetsAsync_GroupMissingAfterGroupTimeoutIsSuccess()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
                Arg.Any<OffsetDeleteRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(
                ValueTask.FromResult(new OffsetDeleteResponse
                {
                    ErrorCode = ErrorCode.RequestTimedOut,
                    Topics = []
                }),
                ValueTask.FromResult(new OffsetDeleteResponse
                {
                    ErrorCode = ErrorCode.GroupIdNotFound,
                    Topics = []
                }));
        var partition = new TopicPartition(Topic, 0);

        var results = await admin.DeleteStreamsGroupOffsetsAsync(FirstGroup, [partition]);

        await Assert.That(results[partition].ErrorCode).IsEqualTo(ErrorCode.None);
        await connection.Received(2).SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
            Arg.Any<OffsetDeleteRequest>(),
            0,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteStreamsGroupsAsync_PreservesPerGroupErrors()
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
                    DeletedGroup(FirstGroup, ErrorCode.None),
                    DeletedGroup(SecondGroup, ErrorCode.NonEmptyGroup)
                ]
            }));

        var results = await admin.DeleteStreamsGroupsAsync([FirstGroup, SecondGroup]);

        await Assert.That(results[FirstGroup].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results[SecondGroup].ErrorCode).IsEqualTo(ErrorCode.NonEmptyGroup);
        await connection.Received(1).SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
            Arg.Is<DeleteGroupsRequest>(request => request != null && request.GroupsNames.Count == 2),
            2,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteStreamsGroupsAsync_RetriesTransientAndMissingGroups()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
                Arg.Any<DeleteGroupsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(
                ValueTask.FromResult(new DeleteGroupsResponse
                {
                    Results = [DeletedGroup(FirstGroup, ErrorCode.RequestTimedOut)]
                }),
                ValueTask.FromResult(new DeleteGroupsResponse
                {
                    Results =
                    [
                        DeletedGroup(FirstGroup, ErrorCode.None),
                        DeletedGroup(SecondGroup, ErrorCode.None)
                    ]
                }));

        var results = await admin.DeleteStreamsGroupsAsync([FirstGroup, SecondGroup]);

        await Assert.That(results.Values.All(result => result.ErrorCode == ErrorCode.None)).IsTrue();
        await connection.Received(2).SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
            Arg.Any<DeleteGroupsRequest>(),
            2,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteStreamsGroupsAsync_GroupMissingAfterTimeoutIsSuccess()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
                Arg.Any<DeleteGroupsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(
                ValueTask.FromResult(new DeleteGroupsResponse
                {
                    Results = [DeletedGroup(FirstGroup, ErrorCode.RequestTimedOut)]
                }),
                ValueTask.FromResult(new DeleteGroupsResponse
                {
                    Results = [DeletedGroup(FirstGroup, ErrorCode.GroupIdNotFound)]
                }));

        var results = await admin.DeleteStreamsGroupsAsync([FirstGroup]);

        await Assert.That(results[FirstGroup].ErrorCode).IsEqualTo(ErrorCode.None);
        await connection.Received(2).SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
            Arg.Any<DeleteGroupsRequest>(),
            2,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteStreamsGroupsAsync_PreservesCoordinatorLookupErrors()
    {
        var (admin, connection) = CreateAdmin();
        SetupSelectiveFindCoordinatorError(connection);
        connection.SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
                Arg.Any<DeleteGroupsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new DeleteGroupsResponse
            {
                Results = [DeletedGroup(SecondGroup, ErrorCode.None)]
            }));

        var results = await admin.DeleteStreamsGroupsAsync([FirstGroup, SecondGroup]);

        await Assert.That(results[FirstGroup].ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That(results[SecondGroup].ErrorCode).IsEqualTo(ErrorCode.None);
        await connection.Received(1).SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
            Arg.Is<DeleteGroupsRequest>(request =>
                request != null &&
                request.GroupsNames.Count == 1 &&
                request.GroupsNames[0] == SecondGroup),
            2,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteStreamsGroupsAsync_ReturnsFinalRetriableErrors()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
                Arg.Any<DeleteGroupsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.ArgAt<DeleteGroupsRequest>(0);
                return ValueTask.FromResult(new DeleteGroupsResponse
                {
                    Results = request.GroupsNames
                        .Select(groupId => DeletedGroup(
                            groupId,
                            groupId == FirstGroup ? ErrorCode.None : ErrorCode.RequestTimedOut))
                        .ToList()
                });
            });

        var results = await admin.DeleteStreamsGroupsAsync([FirstGroup, SecondGroup]);

        await Assert.That(results[FirstGroup].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results[SecondGroup].ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
        await connection.Received(4).SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
            Arg.Any<DeleteGroupsRequest>(),
            2,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListStreamsGroupOffsetsAsync_OperationTimeoutThrowsKafkaTimeoutException()
    {
        var (admin, connection) = CreateAdmin();
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(call.ArgAt<CancellationToken>(2)));

        var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
            await admin.ListStreamsGroupOffsetsAsync(
                new Dictionary<string, ListStreamsGroupOffsetsSpec> { [FirstGroup] = new() },
                new ListStreamsGroupOffsetsOptions { TimeoutMs = 20 }));

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);
        await Assert.That(exception.Configured).IsEqualTo(TimeSpan.FromMilliseconds(20));
    }

    [Test]
    public async Task StreamsGroupExtensions_UnsupportedClientThrowsNotSupportedException()
    {
        var admin = Substitute.For<IAdminClient>();

        async Task Act() => await admin.DeleteStreamsGroupsAsync([FirstGroup]);

        await Assert.That(Act).Throws<NotSupportedException>();
    }

    private static OffsetFetchResponsePartition Offset(int partition, long offset, ErrorCode errorCode) => new()
    {
        PartitionIndex = partition,
        CommittedOffset = offset,
        ErrorCode = errorCode
    };

    private static OffsetFetchResponse ListResponse(params OffsetFetchResponsePartition[] partitions) => new()
    {
        Groups =
        [
            new OffsetFetchResponseGroup
            {
                GroupId = FirstGroup,
                ErrorCode = ErrorCode.None,
                Topics = [new OffsetFetchResponseTopic { TopicId = TopicId, Partitions = partitions }]
            }
        ]
    };

    private static OffsetCommitResponsePartition Commit(int partition, ErrorCode errorCode) => new()
    {
        PartitionIndex = partition,
        ErrorCode = errorCode
    };

    private static OffsetCommitResponse CommitResponse(params OffsetCommitResponsePartition[] partitions) => new()
    {
        Topics = [new OffsetCommitResponseTopic { TopicId = TopicId, Partitions = partitions }]
    };

    private static OffsetDeleteResponsePartition Deleted(int partition, ErrorCode errorCode) => new()
    {
        PartitionIndex = partition,
        ErrorCode = errorCode
    };

    private static OffsetDeleteResponse DeleteOffsetsResponse(params OffsetDeleteResponsePartition[] partitions) => new()
    {
        ErrorCode = ErrorCode.None,
        Topics = [new OffsetDeleteResponseTopic { Name = Topic, Partitions = partitions }]
    };

    private static DeleteGroupsResponseResult DeletedGroup(string groupId, ErrorCode errorCode) => new()
    {
        GroupId = groupId,
        ErrorCode = errorCode
    };

    private static async ValueTask<OffsetFetchResponse> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("Cancellation was not observed.");
    }

    private static void SetupFindCoordinator(IKafkaConnection connection)
    {
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.ArgAt<FindCoordinatorRequest>(0);
                return ValueTask.FromResult(CoordinatorResponse(request.Key, ErrorCode.None));
            });
    }

    private static void SetupFindCoordinatorError(IKafkaConnection connection, ErrorCode errorCode)
    {
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.ArgAt<FindCoordinatorRequest>(0);
                return ValueTask.FromResult(CoordinatorResponse(request.Key, errorCode));
            });
    }

    private static void SetupSelectiveFindCoordinatorError(IKafkaConnection connection)
    {
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.ArgAt<FindCoordinatorRequest>(0);
                return ValueTask.FromResult(CoordinatorResponse(
                    request.Key,
                    request.Key == FirstGroup ? ErrorCode.GroupAuthorizationFailed : ErrorCode.None));
            });
    }

    private static FindCoordinatorResponse CoordinatorResponse(string groupId, ErrorCode errorCode) => new()
    {
        Coordinators =
        [
            new Coordinator
            {
                Key = groupId,
                NodeId = 1,
                Host = "localhost",
                Port = 9092,
                ErrorCode = errorCode
            }
        ]
    };

    private static (AdminClient Admin, IKafkaConnection Connection) CreateAdmin(
        short offsetFetchMaxVersion = 10)
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
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
            ClusterId = "test-cluster",
            ControllerId = 1,
            Topics =
            [
                new TopicMetadata
                {
                    Name = Topic,
                    TopicId = TopicId,
                    ErrorCode = ErrorCode.None,
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            PartitionIndex = 0,
                            LeaderId = 1,
                            ErrorCode = ErrorCode.None,
                            ReplicaNodes = [1],
                            IsrNodes = [1]
                        },
                        new PartitionMetadata
                        {
                            PartitionIndex = 1,
                            LeaderId = 1,
                            ErrorCode = ErrorCode.None,
                            ReplicaNodes = [1],
                            IsrNodes = [1]
                        }
                    ]
                }
            ]
        });
        metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 6);
        metadataManager.SetApiVersion(ApiKey.OffsetFetch, 6, offsetFetchMaxVersion);
        metadataManager.SetApiVersion(ApiKey.OffsetCommit, 8, 10);
        metadataManager.SetApiVersion(ApiKey.OffsetDelete, 0, 0);
        metadataManager.SetApiVersion(ApiKey.DeleteGroups, 2, 2);

        return (new AdminClient(
            new AdminClientOptions
            {
                BootstrapServers = ["localhost:9092"],
                RetryBackoffMs = 1,
                RetryBackoffMaxMs = 1
            },
            pool,
            metadataManager), connection);
    }
}
