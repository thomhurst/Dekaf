using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientIdempotentRetryTests
{
    private const string TopicName = "retry-topic";
    private const string GroupId = "retry-group";

    [Test]
    public async Task DeleteTopicsAsync_UnknownTopicOnRetry_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.DeleteTopics);
        var calls = 0;

        connection.SendAsync<DeleteTopicsRequest, DeleteTopicsResponse>(
                Arg.Any<DeleteTopicsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new KafkaException(ErrorCode.RequestTimedOut, "simulated timeout");

                return ValueTask.FromResult(CreateUnknownTopicResponse());
            });

        await admin.DeleteTopicsAsync([TopicName]);

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DeleteTopicsAsync_RefreshAfterDeleteNeverAnswers_CompletesWithinTimeout(bool byId)
    {
        // The delete succeeded; the refresh that follows only updates the cached listing. A broker
        // that stops answering must neither push the call past TimeoutMs nor fail it.
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.DeleteTopics);
        var topicId = Guid.NewGuid();

        connection.SendAsync<DeleteTopicsRequest, DeleteTopicsResponse>(
                Arg.Any<DeleteTopicsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new DeleteTopicsResponse
            {
                Responses =
                [
                    new DeleteTopicsResponseTopic { Name = TopicName, TopicId = topicId, ErrorCode = ErrorCode.None }
                ]
            }));
        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(call.ArgAt<CancellationToken>(2)));

        var options = new DeleteTopicsOptions { TimeoutMs = 300 };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var delete = byId
            ? ((ITopicIdAdminClient)admin).DeleteTopicsAsync([topicId], options).AsTask()
            : admin.DeleteTopicsAsync([TopicName], options).AsTask();
        await delete.WaitAsync(TimeSpan.FromSeconds(20));
        stopwatch.Stop();

        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));

        static async ValueTask<MetadataResponse> WaitForCancellationAsync(CancellationToken token)
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new System.Diagnostics.UnreachableException();
        }
    }

    [Test]
    public async Task DeleteTopicsAsync_UnknownTopicWithoutPriorSendFailure_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.DeleteTopics);
        var calls = 0;

        connection.SendAsync<DeleteTopicsRequest, DeleteTopicsResponse>(
                Arg.Any<DeleteTopicsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref calls);
                return ValueTask.FromResult(CreateUnknownTopicResponse());
            });

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.DeleteTopicsAsync([TopicName]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(calls).IsEqualTo(4);
    }

    [Test]
    public async Task DeleteConsumerGroupsAsync_GroupIdNotFoundOnRetry_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.DeleteGroups);
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
                    Results =
                    [
                        new DeleteGroupsResponseResult
                        {
                            GroupId = GroupId,
                            ErrorCode = ErrorCode.GroupIdNotFound
                        }
                    ]
                });
            });

        await admin.DeleteConsumerGroupsAsync([GroupId]);

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task DeleteConsumerGroupsAsync_GroupIdNotFoundAfterNonAmbiguousRetry_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.DeleteGroups);
        SetupFindCoordinator(connection);
        var calls = 0;

        connection.SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
                Arg.Any<DeleteGroupsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(new DeleteGroupsResponse
            {
                Results =
                [
                    new DeleteGroupsResponseResult
                    {
                        GroupId = GroupId,
                        ErrorCode = Interlocked.Increment(ref calls) == 1
                            ? ErrorCode.NotCoordinator
                            : ErrorCode.GroupIdNotFound
                    }
                ]
            }));

        var exception = await Assert.ThrowsAsync<GroupException>(async () =>
            await admin.DeleteConsumerGroupsAsync([GroupId]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task CreatePartitionsAsync_InvalidPartitionsOnRetry_WhenMetadataShowsTarget_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.CreatePartitions);
        var calls = 0;

        connection.SendAsync<CreatePartitionsRequest, CreatePartitionsResponse>(
                Arg.Any<CreatePartitionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new KafkaException(ErrorCode.RequestTimedOut, "simulated timeout");

                return ValueTask.FromResult(new CreatePartitionsResponse
                {
                    Results =
                    [
                        new CreatePartitionsResponseResult
                        {
                            Name = TopicName,
                            ErrorCode = ErrorCode.InvalidPartitions
                        }
                    ]
                });
            });

        await admin.CreatePartitionsAsync(new Dictionary<string, int>
        {
            [TopicName] = 3
        });

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task CreatePartitionsAsync_InvalidPartitionsAfterNonAmbiguousRetry_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.CreatePartitions);
        var calls = 0;

        connection.SendAsync<CreatePartitionsRequest, CreatePartitionsResponse>(
                Arg.Any<CreatePartitionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(new CreatePartitionsResponse
            {
                Results =
                [
                    new CreatePartitionsResponseResult
                    {
                        Name = TopicName,
                        ErrorCode = Interlocked.Increment(ref calls) == 1
                            ? ErrorCode.NotController
                            : ErrorCode.InvalidPartitions
                    }
                ]
            }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.CreatePartitionsAsync(new Dictionary<string, int>
            {
                [TopicName] = 3
            }));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.InvalidPartitions);
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task DeleteConsumerGroupOffsetsAsync_GroupIdNotFoundOnRetry_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.OffsetDelete);
        SetupFindCoordinator(connection);
        var calls = 0;

        connection.SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
                Arg.Any<OffsetDeleteRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new KafkaException(ErrorCode.RequestTimedOut, "simulated timeout");

                return ValueTask.FromResult(new OffsetDeleteResponse
                {
                    ErrorCode = ErrorCode.GroupIdNotFound,
                    Topics = []
                });
            });

        await admin.DeleteConsumerGroupOffsetsAsync(GroupId, [new TopicPartition(TopicName, 0)]);

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task DeleteConsumerGroupOffsetsAsync_GroupIdNotFoundAfterNonAmbiguousRetry_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.OffsetDelete);
        SetupFindCoordinator(connection);
        var calls = 0;

        connection.SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
                Arg.Any<OffsetDeleteRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(new OffsetDeleteResponse
            {
                ErrorCode = Interlocked.Increment(ref calls) == 1
                    ? ErrorCode.NotCoordinator
                    : ErrorCode.GroupIdNotFound,
                Topics = []
            }));

        var exception = await Assert.ThrowsAsync<GroupException>(async () =>
            await admin.DeleteConsumerGroupOffsetsAsync(GroupId, [new TopicPartition(TopicName, 0)]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task DeleteShareGroupOffsetsAsync_GroupIdNotFoundOnRetry_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.DeleteShareGroupOffsets);
        SetupFindCoordinator(connection);
        var calls = 0;

        connection.SendAsync<DeleteShareGroupOffsetsRequest, DeleteShareGroupOffsetsResponse>(
                Arg.Any<DeleteShareGroupOffsetsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new KafkaException(ErrorCode.RequestTimedOut, "simulated timeout");

                return ValueTask.FromResult(new DeleteShareGroupOffsetsResponse
                {
                    ErrorCode = ErrorCode.GroupIdNotFound,
                    Responses = []
                });
            });

        await admin.DeleteShareGroupOffsetsAsync(GroupId, [TopicName]);

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task DeleteShareGroupOffsetsAsync_GroupIdNotFoundAfterNonAmbiguousRetry_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.DeleteShareGroupOffsets);
        SetupFindCoordinator(connection);
        var calls = 0;

        connection.SendAsync<DeleteShareGroupOffsetsRequest, DeleteShareGroupOffsetsResponse>(
                Arg.Any<DeleteShareGroupOffsetsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(new DeleteShareGroupOffsetsResponse
            {
                ErrorCode = Interlocked.Increment(ref calls) == 1
                    ? ErrorCode.NotCoordinator
                    : ErrorCode.GroupIdNotFound,
                Responses = []
            }));

        var exception = await Assert.ThrowsAsync<GroupException>(async () =>
            await admin.DeleteShareGroupOffsetsAsync(GroupId, [TopicName]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task ElectLeadersAsync_ElectionNotNeededOnRetry_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.ElectLeaders);
        var calls = 0;

        connection.SendAsync<ElectLeadersRequest, ElectLeadersResponse>(
                Arg.Any<ElectLeadersRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new KafkaException(ErrorCode.RequestTimedOut, "simulated timeout");

                return ValueTask.FromResult(new ElectLeadersResponse
                {
                    ErrorCode = ErrorCode.None,
                    ReplicaElectionResults =
                    [
                        new ElectLeadersResponseTopic
                        {
                            Topic = TopicName,
                            PartitionResult =
                            [
                                new ElectLeadersResponsePartition
                                {
                                    PartitionId = 0,
                                    ErrorCode = ErrorCode.ElectionNotNeeded,
                                    ErrorMessage = "already leader"
                                }
                            ]
                        }
                    ]
                });
            });

        var results = await admin.ElectLeadersAsync(
            ElectionType.Preferred,
            [new TopicPartition(TopicName, 0)]);

        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(results[new TopicPartition(TopicName, 0)].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results[new TopicPartition(TopicName, 0)].ErrorMessage).IsNull();
    }

    [Test]
    public async Task ElectLeadersAsync_ElectionNotNeededWithoutPriorRetry_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.ElectLeaders);

        connection.SendAsync<ElectLeadersRequest, ElectLeadersResponse>(
                Arg.Any<ElectLeadersRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ElectLeadersResponse
            {
                ErrorCode = ErrorCode.None,
                ReplicaElectionResults =
                [
                    new ElectLeadersResponseTopic
                    {
                        Topic = TopicName,
                        PartitionResult =
                        [
                            new ElectLeadersResponsePartition
                            {
                                PartitionId = 0,
                                ErrorCode = ErrorCode.ElectionNotNeeded,
                                ErrorMessage = "already leader"
                            }
                        ]
                    }
                ]
            }));

        var results = await admin.ElectLeadersAsync(
            ElectionType.Preferred,
            [new TopicPartition(TopicName, 0)]);

        await Assert.That(results[new TopicPartition(TopicName, 0)].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results[new TopicPartition(TopicName, 0)].ErrorMessage).IsNull();
    }

    [Test]
    public async Task DeleteAclsAsync_LostResponse_ThrowsAmbiguousFailureWithoutReplay()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.DeleteAcls);
        var calls = 0;
        var lostResponse = new IOException("response lost");

        connection.SendAsync<DeleteAclsRequest, DeleteAclsResponse>(
                Arg.Any<DeleteAclsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask<DeleteAclsResponse>>(_ =>
            {
                // A replay would match nothing and report an empty result, hiding what the
                // lost request deleted.
                if (Interlocked.Increment(ref calls) == 1)
                    throw lostResponse;

                return ValueTask.FromResult(new DeleteAclsResponse
                {
                    FilterResults = [new DeleteAclsFilterResult { ErrorCode = ErrorCode.None, MatchingAcls = [] }]
                });
            });

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.DeleteAclsAsync([AclBindingFilter.MatchAll()]));

        await Assert.That(exception!.IsRetriable).IsFalse();
        await Assert.That(exception.InnerException).IsSameReferenceAs(lostResponse);
        await Assert.That(calls).IsEqualTo(1);
    }

    [Test]
    public async Task DeleteAclsAsync_ConnectionRetiredBeforeWrite_RetriesAndReturnsDeletedBindings()
    {
        // A connection retired between lease and send fails before any byte of the frame is
        // written, so nothing can have been deleted and the request is retried.
        WriteObservingConnection? observed = null;
        var (admin, _) = CreateAdminWithConnection(
            new AdminClientOptions { BootstrapServers = ["localhost:9092"] },
            connection => observed = new WriteObservingConnection(connection),
            ApiKey.DeleteAcls);
        var calls = 0;

        observed!.DeleteAclsHandler = (writeStarted, _) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new ObjectDisposedException("KafkaConnection", "Connection has been retired");

                writeStarted();
                return ValueTask.FromResult(new DeleteAclsResponse
                {
                    FilterResults =
                    [
                        new DeleteAclsFilterResult
                        {
                            ErrorCode = ErrorCode.None,
                            MatchingAcls =
                            [
                                new DeleteAclsMatchingAcl
                                {
                                    ErrorCode = ErrorCode.None,
                                    ResourceType = (sbyte)ResourceType.Topic,
                                    ResourceName = "orders",
                                    Principal = "User:alice",
                                    Host = "*",
                                    Operation = (sbyte)AclOperation.Read,
                                    PermissionType = (sbyte)AclPermissionType.Allow
                                }
                            ]
                        }
                    ]
                });
            };

        var deleted = await admin.DeleteAclsAsync([AclBindingFilter.MatchAll()]);

        await Assert.That(deleted.Count).IsEqualTo(1);
        await Assert.That(deleted[0].Pattern.Name).IsEqualTo("orders");
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task DeleteAclsAsync_ApiTimeoutWhileWaitingToWrite_ThrowsApiTimeout()
    {
        // The API timeout ends the wait for the write lock before the frame write starts: nothing
        // was sent, so the caller gets the API timeout, not the unknown-outcome error.
        WriteObservingConnection? observed = null;
        var (admin, _) = CreateAdminWithConnection(
            new AdminClientOptions { BootstrapServers = ["localhost:9092"] },
            connection => observed = new WriteObservingConnection(connection),
            ApiKey.DeleteAcls);

        observed!.DeleteAclsHandler = static (_, token) => WaitForCancellationAsync(token);

        var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
            await admin.DeleteAclsAsync([AclBindingFilter.MatchAll()], new DeleteAclsOptions { TimeoutMs = 200 }));

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);

        static async ValueTask<DeleteAclsResponse> WaitForCancellationAsync(CancellationToken token)
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new System.Diagnostics.UnreachableException();
        }
    }

    [Test]
    public async Task DeleteAclsAsync_FailureAfterWriteStarts_ThrowsAmbiguousFailureWithoutReplay()
    {
        WriteObservingConnection? observed = null;
        var (admin, _) = CreateAdminWithConnection(
            new AdminClientOptions { BootstrapServers = ["localhost:9092"] },
            connection => observed = new WriteObservingConnection(connection),
            ApiKey.DeleteAcls);
        var calls = 0;
        var lostResponse = new IOException("connection reset after the frame was written");

        observed!.DeleteAclsHandler = (writeStarted, _) =>
            {
                Interlocked.Increment(ref calls);
                writeStarted();
                throw lostResponse;
            };

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.DeleteAclsAsync([AclBindingFilter.MatchAll()]));

        await Assert.That(exception!.IsRetriable).IsFalse();
        await Assert.That(exception.InnerException).IsSameReferenceAs(lostResponse);
        await Assert.That(calls).IsEqualTo(1);
    }

    [Test]
    public async Task DeleteAclsAsync_ApiTimeoutEndsInFlightSend_ThrowsAmbiguousFailure()
    {
        // The controller accepted the deletion and never answered: the API timeout ends the send,
        // and the outcome is as unknown as after a lost response.
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.DeleteAcls);
        var calls = 0;

        connection.SendAsync<DeleteAclsRequest, DeleteAclsResponse>(
                Arg.Any<DeleteAclsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Interlocked.Increment(ref calls);
                return WaitForCancellationAsync(call.ArgAt<CancellationToken>(2));
            });

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.DeleteAclsAsync([AclBindingFilter.MatchAll()], new DeleteAclsOptions { TimeoutMs = 200 }));

        await Assert.That(exception).IsNotTypeOf<KafkaTimeoutException>();
        await Assert.That(exception!.IsRetriable).IsFalse();
        await Assert.That(exception.Message).Contains("may have been deleted");
        await Assert.That(exception.InnerException).IsAssignableTo<OperationCanceledException>();
        await Assert.That(calls).IsEqualTo(1);

        static async ValueTask<DeleteAclsResponse> WaitForCancellationAsync(CancellationToken token)
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new System.Diagnostics.UnreachableException();
        }
    }

    [Test]
    public async Task DeleteAclsAsync_CallerCancelsInFlightSend_ThrowsCancellation()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.DeleteAcls);
        using var cts = new CancellationTokenSource();

        connection.SendAsync<DeleteAclsRequest, DeleteAclsResponse>(
                Arg.Any<DeleteAclsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                cts.Cancel();
                return ValueTask.FromCanceled<DeleteAclsResponse>(call.ArgAt<CancellationToken>(2));
            });

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await admin.DeleteAclsAsync([AclBindingFilter.MatchAll()], cancellationToken: cts.Token));
    }

    [Test]
    public async Task DeleteAclsAsync_ConnectionRefusedBeforeSend_RetriesAndReturnsDeletedBindings()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.DeleteAcls);
        var sendCalls = 0;
        var pool = GetPool(admin);
        var leaseCalls = 0;
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref leaseCalls) == 1
                ? ValueTask.FromException<IKafkaConnection>(new System.Net.Sockets.SocketException(
                    (int)System.Net.Sockets.SocketError.ConnectionRefused))
                : ValueTask.FromResult(connection));

        connection.SendAsync<DeleteAclsRequest, DeleteAclsResponse>(
                Arg.Any<DeleteAclsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref sendCalls);
                return ValueTask.FromResult(new DeleteAclsResponse
                {
                    FilterResults =
                    [
                        new DeleteAclsFilterResult
                        {
                            ErrorCode = ErrorCode.None,
                            MatchingAcls =
                            [
                                new DeleteAclsMatchingAcl
                                {
                                    ErrorCode = ErrorCode.None,
                                    ResourceType = (sbyte)ResourceType.Topic,
                                    ResourceName = TopicName,
                                    Principal = "User:alice",
                                    Host = "*",
                                    Operation = (sbyte)AclOperation.Read,
                                    PermissionType = (sbyte)AclPermissionType.Allow
                                }
                            ]
                        }
                    ]
                });
            });

        var deleted = await admin.DeleteAclsAsync([AclBindingFilter.MatchAll()]);

        await Assert.That(deleted.Count).IsEqualTo(1);
        await Assert.That(sendCalls).IsEqualTo(1);
    }

    [Test]
    public async Task ExpireDelegationTokenAsync_TokenNotFoundOnRetryAfterLostResponse_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.ExpireDelegationToken);
        var calls = 0;
        var before = DateTimeOffset.UtcNow;

        connection.SendAsync<ExpireDelegationTokenRequest, ExpireDelegationTokenResponse>(
                Arg.Any<ExpireDelegationTokenRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask<ExpireDelegationTokenResponse>>(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new IOException("response lost");

                return ValueTask.FromResult(new ExpireDelegationTokenResponse
                {
                    ErrorCode = ErrorCode.DelegationTokenNotFound
                });
            });

        var expiry = await admin.ExpireDelegationTokenAsync([1, 2, 3]);

        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(expiry).IsGreaterThanOrEqualTo(before);
        await Assert.That(expiry).IsLessThanOrEqualTo(DateTimeOffset.UtcNow);
    }

    [Test]
    public async Task ExpireDelegationTokenAsync_FailureBeforeWriteThenTokenNotFound_Throws()
    {
        // The first attempt fails before any byte is written (a retired connection), so it cannot
        // have expired the token. DELEGATION_TOKEN_NOT_FOUND on the retry means the token was never
        // there, and must not be reported as a successful expiry.
        WriteObservingConnection? observed = null;
        var (admin, _) = CreateAdminWithConnection(
            new AdminClientOptions { BootstrapServers = ["localhost:9092"] },
            connection => observed = new WriteObservingConnection(connection),
            ApiKey.ExpireDelegationToken);
        var calls = 0;

        observed!.ExpireDelegationTokenHandler = (writeStarted, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                throw new ObjectDisposedException("KafkaConnection", "Connection has been retired");

            writeStarted();
            return ValueTask.FromResult(new ExpireDelegationTokenResponse
            {
                ErrorCode = ErrorCode.DelegationTokenNotFound
            });
        };

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.ExpireDelegationTokenAsync([1, 2, 3]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.DelegationTokenNotFound);
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task ExpireDelegationTokenAsync_FailureAfterWriteThenTokenNotFound_TreatedAsSuccess()
    {
        WriteObservingConnection? observed = null;
        var (admin, _) = CreateAdminWithConnection(
            new AdminClientOptions { BootstrapServers = ["localhost:9092"] },
            connection => observed = new WriteObservingConnection(connection),
            ApiKey.ExpireDelegationToken);
        var calls = 0;

        observed!.ExpireDelegationTokenHandler = (writeStarted, _) =>
        {
            writeStarted();
            if (Interlocked.Increment(ref calls) == 1)
                throw new IOException("response lost");

            return ValueTask.FromResult(new ExpireDelegationTokenResponse
            {
                ErrorCode = ErrorCode.DelegationTokenNotFound
            });
        };

        await admin.ExpireDelegationTokenAsync([1, 2, 3]);

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task ExpireDelegationTokenAsync_TokenNotFoundWithoutPriorSendFailure_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.ExpireDelegationToken);

        connection.SendAsync<ExpireDelegationTokenRequest, ExpireDelegationTokenResponse>(
                Arg.Any<ExpireDelegationTokenRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ExpireDelegationTokenResponse
            {
                ErrorCode = ErrorCode.DelegationTokenNotFound
            }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.ExpireDelegationTokenAsync([1, 2, 3]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.DelegationTokenNotFound);
    }

    [Test]
    public async Task AddRaftVoterAsync_DuplicateVoterOnRetry_WhenQuorumShowsVoter_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.AddRaftVoter, ApiKey.DescribeQuorum);
        var directoryId = Guid.NewGuid();
        var calls = SetupAddRaftVoterLostResponse(connection);
        SetupQuorumVoters(connection, (VoterId, directoryId));

        await admin.AddRaftVoterAsync(VoterId, directoryId, [VoterEndpoint]);

        await Assert.That(calls()).IsEqualTo(2);
    }

    [Test]
    public async Task AddRaftVoterAsync_QuorumCheckOutlastsDefaultBudget_UsesTheCallTimeout()
    {
        // The DUPLICATE_VOTER check runs inside AddRaftVoter's attempt. With TimeoutMs longer than
        // the default API budget, a quorum read that keeps failing must keep retrying under the
        // call's deadline instead of stopping at a default budget of its own.
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.AddRaftVoter, ApiKey.DescribeQuorum);
        typeof(AdminClient)
            .GetProperty(nameof(AdminClient.DefaultApiTimeoutBudgetMs),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(admin, 200);
        var directoryId = Guid.NewGuid();
        var calls = SetupAddRaftVoterLostResponse(connection);
        SetupQuorumVoters(connection, (VoterId, directoryId));
        var quorum = await connection.SendAsync<DescribeQuorumRequest, DescribeQuorumResponse>(new DescribeQuorumRequest { Topics = [] }, 0);
        var outage = System.Diagnostics.Stopwatch.StartNew();
        connection.SendAsync<DescribeQuorumRequest, DescribeQuorumResponse>(
                Arg.Any<DescribeQuorumRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => outage.Elapsed < TimeSpan.FromMilliseconds(800)
                ? ValueTask.FromException<DescribeQuorumResponse>(new IOException("controller unreachable"))
                : ValueTask.FromResult(quorum));

        await admin.AddRaftVoterAsync(VoterId, directoryId, [VoterEndpoint], new AddRaftVoterOptions { TimeoutMs = 10_000 });

        await Assert.That(calls()).IsEqualTo(2);
        await Assert.That(outage.Elapsed).IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(800));
    }

    [Test]
    public async Task AddRaftVoterAsync_DuplicateVoterOnRetry_WhenQuorumShowsOtherDirectory_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.AddRaftVoter, ApiKey.DescribeQuorum);
        var calls = SetupAddRaftVoterLostResponse(connection);
        SetupQuorumVoters(connection, (VoterId, Guid.NewGuid()));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.AddRaftVoterAsync(VoterId, Guid.NewGuid(), [VoterEndpoint]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.DuplicateVoter);
        await Assert.That(calls()).IsEqualTo(2);
    }

    [Test]
    public async Task AddRaftVoterAsync_DuplicateVoterWithoutPriorSendFailure_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.AddRaftVoter, ApiKey.DescribeQuorum);
        var directoryId = Guid.NewGuid();
        SetupQuorumVoters(connection, (VoterId, directoryId));
        connection.SendAsync<AddRaftVoterRequest, AddRaftVoterResponse>(
                Arg.Any<AddRaftVoterRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new AddRaftVoterResponse { ErrorCode = ErrorCode.DuplicateVoter }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.AddRaftVoterAsync(VoterId, directoryId, [VoterEndpoint]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.DuplicateVoter);
    }

    [Test]
    public async Task RemoveRaftVoterAsync_VoterNotFoundOnRetry_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.RemoveRaftVoter);
        var calls = 0;

        connection.SendAsync<RemoveRaftVoterRequest, RemoveRaftVoterResponse>(
                Arg.Any<RemoveRaftVoterRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask<RemoveRaftVoterResponse>>(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new IOException("response lost");

                return ValueTask.FromResult(new RemoveRaftVoterResponse { ErrorCode = ErrorCode.VoterNotFound });
            });

        await admin.RemoveRaftVoterAsync(VoterId, Guid.NewGuid());

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task RemoveRaftVoterAsync_VoterNotFoundWithoutPriorSendFailure_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.RemoveRaftVoter);

        connection.SendAsync<RemoveRaftVoterRequest, RemoveRaftVoterResponse>(
                Arg.Any<RemoveRaftVoterRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new RemoveRaftVoterResponse { ErrorCode = ErrorCode.VoterNotFound }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.RemoveRaftVoterAsync(VoterId, Guid.NewGuid()));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.VoterNotFound);
    }

    [Test]
    public async Task UnregisterBrokerAsync_BrokerIdNotRegisteredOnRetry_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.UnregisterBroker);
        var calls = 0;

        connection.SendAsync<UnregisterBrokerRequest, UnregisterBrokerResponse>(
                Arg.Any<UnregisterBrokerRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask<UnregisterBrokerResponse>>(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new IOException("response lost");

                return ValueTask.FromResult(new UnregisterBrokerResponse { ErrorCode = ErrorCode.BrokerIdNotRegistered });
            });

        await admin.UnregisterBrokerAsync(4);

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task UnregisterBrokerAsync_BrokerIdNotRegisteredWithoutPriorSendFailure_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.UnregisterBroker);

        connection.SendAsync<UnregisterBrokerRequest, UnregisterBrokerResponse>(
                Arg.Any<UnregisterBrokerRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new UnregisterBrokerResponse { ErrorCode = ErrorCode.BrokerIdNotRegistered }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.UnregisterBrokerAsync(4));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.BrokerIdNotRegistered);
    }

    [Test]
    public async Task AlterUserScramCredentialsAsync_DeletionNotFoundOnRetry_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.AlterUserScramCredentials);
        var calls = 0;

        connection.SendAsync<AlterUserScramCredentialsRequest, AlterUserScramCredentialsResponse>(
                Arg.Any<AlterUserScramCredentialsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask<AlterUserScramCredentialsResponse>>(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new IOException("response lost");

                return ValueTask.FromResult(new AlterUserScramCredentialsResponse
                {
                    Results = [new AlterUserScramCredentialsResult { User = "alice", ErrorCode = ErrorCode.ResourceNotFound }]
                });
            });

        await admin.AlterUserScramCredentialsAsync(
            [new UserScramCredentialDeletion { User = "alice", Mechanism = ScramMechanism.ScramSha256 }]);

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task AlterUserScramCredentialsAsync_UpsertOnlyUserNotFoundOnRetry_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.AlterUserScramCredentials);
        var calls = 0;

        connection.SendAsync<AlterUserScramCredentialsRequest, AlterUserScramCredentialsResponse>(
                Arg.Any<AlterUserScramCredentialsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask<AlterUserScramCredentialsResponse>>(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new IOException("response lost");

                return ValueTask.FromResult(new AlterUserScramCredentialsResponse
                {
                    Results = [new AlterUserScramCredentialsResult { User = "bob", ErrorCode = ErrorCode.ResourceNotFound }]
                });
            });

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.AlterUserScramCredentialsAsync(
            [
                new UserScramCredentialDeletion { User = "alice", Mechanism = ScramMechanism.ScramSha256 },
                new UserScramCredentialUpsertion
                {
                    User = "bob",
                    Mechanism = ScramMechanism.ScramSha256,
                    Password = "secret",
                    Iterations = 4096
                }
            ]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.ResourceNotFound);
    }

    [Test]
    public async Task AlterUserScramCredentialsAsync_MixedUserNotFoundOnRetry_Throws()
    {
        // "alice" has a deletion and an upsertion. RESOURCE_NOT_FOUND on the replay may mean the
        // lost request applied both, or that it was rejected as a whole and the upsertion never
        // applied. The client cannot tell which, so it must not report success.
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.AlterUserScramCredentials);
        var calls = 0;

        connection.SendAsync<AlterUserScramCredentialsRequest, AlterUserScramCredentialsResponse>(
                Arg.Any<AlterUserScramCredentialsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask<AlterUserScramCredentialsResponse>>(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new IOException("response lost");

                return ValueTask.FromResult(new AlterUserScramCredentialsResponse
                {
                    Results = [new AlterUserScramCredentialsResult { User = "alice", ErrorCode = ErrorCode.ResourceNotFound }]
                });
            });

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.AlterUserScramCredentialsAsync(
            [
                new UserScramCredentialDeletion { User = "alice", Mechanism = ScramMechanism.ScramSha256 },
                new UserScramCredentialUpsertion
                {
                    User = "alice",
                    Mechanism = ScramMechanism.ScramSha512,
                    Password = "secret",
                    Iterations = 4096
                }
            ]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.ResourceNotFound);
        await Assert.That(calls).IsEqualTo(2);
    }

    private const int VoterId = 3;

    private static readonly RaftVoterEndpoint VoterEndpoint = new()
    {
        Name = "CONTROLLER",
        Host = "controller-3",
        Port = 9093
    };

    private static Func<int> SetupAddRaftVoterLostResponse(IKafkaConnection connection)
    {
        var calls = 0;
        connection.SendAsync<AddRaftVoterRequest, AddRaftVoterResponse>(
                Arg.Any<AddRaftVoterRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask<AddRaftVoterResponse>>(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new IOException("response lost");

                return ValueTask.FromResult(new AddRaftVoterResponse { ErrorCode = ErrorCode.DuplicateVoter });
            });
        return () => Volatile.Read(ref calls);
    }

    private static void SetupQuorumVoters(IKafkaConnection connection, params (int Id, Guid DirectoryId)[] voters)
    {
        connection.SendAsync<DescribeQuorumRequest, DescribeQuorumResponse>(
                Arg.Any<DescribeQuorumRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new DescribeQuorumResponse
            {
                ErrorCode = ErrorCode.None,
                Topics =
                [
                    new DescribeQuorumResponseTopic
                    {
                        TopicName = "__cluster_metadata",
                        Partitions =
                        [
                            new DescribeQuorumResponsePartition
                            {
                                PartitionIndex = 0,
                                ErrorCode = ErrorCode.None,
                                LeaderId = 1,
                                LeaderEpoch = 1,
                                HighWatermark = 10,
                                CurrentVoters = voters
                                    .Select(voter => new DescribeQuorumReplicaState
                                    {
                                        ReplicaId = voter.Id,
                                        ReplicaDirectoryId = voter.DirectoryId
                                    })
                                    .ToList(),
                                Observers = []
                            }
                        ]
                    }
                ],
                Nodes = []
            }));
    }

    private static IConnectionPool GetPool(AdminClient admin) =>
        (IConnectionPool)typeof(AdminClient)
            .GetField("_connectionPool", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(admin)!;

    private static void SetupFindCoordinator(IKafkaConnection connection)
    {
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                Coordinators =
                [
                    new Coordinator
                    {
                        Key = GroupId,
                        NodeId = 1,
                        Host = "localhost",
                        Port = 9092,
                        ErrorCode = ErrorCode.None
                    }
                ]
            }));
    }

    private static DeleteTopicsResponse CreateUnknownTopicResponse() => new()
    {
        Responses =
        [
            new DeleteTopicsResponseTopic
            {
                Name = TopicName,
                ErrorCode = ErrorCode.UnknownTopicOrPartition
            }
        ]
    };

    internal static (AdminClient Admin, IKafkaConnection Connection) CreateAdminWithMockConnection(
        params ApiKey[] extraApiKeys) =>
        CreateAdminWithMockConnection(
            new AdminClientOptions { BootstrapServers = ["localhost:9092"] },
            extraApiKeys);

    internal static (AdminClient Admin, IKafkaConnection Connection) CreateAdminWithMockConnection(
        AdminClientOptions options,
        params ApiKey[] extraApiKeys) =>
        CreateAdminWithConnection(options, static connection => connection, extraApiKeys);

    // wrapConnection decorates the configured substitute; the pool hands out the decorator.
    internal static (AdminClient Admin, IKafkaConnection Connection) CreateAdminWithConnection(
        AdminClientOptions options,
        Func<IKafkaConnection, IKafkaConnection> wrapConnection,
        params ApiKey[] extraApiKeys)
    {
        var connection = Substitute.For<IKafkaConnection>();
        var pooledConnection = wrapConnection(connection);
        connection.BrokerId.Returns(1);
        connection.Host.Returns("localhost");
        connection.Port.Returns(9092);
        connection.IsConnected.Returns(true);

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(pooledConnection));
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(pooledConnection));

        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        metadataManager.Metadata.Update(CreateMetadataResponse());
        metadataManager.SetApiVersion(ApiKey.Metadata, 9, 13);
        metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);

        foreach (var apiKey in extraApiKeys)
            metadataManager.SetApiVersion(apiKey, 0, 99);

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
                ApiKeys = extraApiKeys
                    .Append(ApiKey.Metadata)
                    .Append(ApiKey.FindCoordinator)
                    .Distinct()
                    .Select(apiKey => new ApiVersion(apiKey, 0, 99))
                    .ToList()
            }));

        var admin = new AdminClient(options, pool, metadataManager);

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
        Topics =
        [
            new TopicMetadata
            {
                Name = TopicName,
                ErrorCode = ErrorCode.None,
                Partitions =
                [
                    new PartitionMetadata
                    {
                        PartitionIndex = 0,
                        LeaderId = 1,
                        LeaderEpoch = 1,
                        ReplicaNodes = [1],
                        IsrNodes = [1],
                        OfflineReplicas = [],
                        ErrorCode = ErrorCode.None
                    },
                    new PartitionMetadata
                    {
                        PartitionIndex = 1,
                        LeaderId = 1,
                        LeaderEpoch = 1,
                        ReplicaNodes = [1],
                        IsrNodes = [1],
                        OfflineReplicas = [],
                        ErrorCode = ErrorCode.None
                    },
                    new PartitionMetadata
                    {
                        PartitionIndex = 2,
                        LeaderId = 1,
                        LeaderEpoch = 1,
                        ReplicaNodes = [1],
                        IsrNodes = [1],
                        OfflineReplicas = [],
                        ErrorCode = ErrorCode.None
                    }
                ]
            }
        ]
    };

    // Reports the frame-write start of DeleteAcls sends through DeleteAclsHandler, as
    // KafkaConnection does, and delegates everything else to the configured substitute.
    private sealed class WriteObservingConnection(IKafkaConnection inner)
        : IKafkaConnection, IKafkaRequestWriteObserverConnection
    {
        public Func<Action, CancellationToken, ValueTask<DeleteAclsResponse>>? DeleteAclsHandler { get; set; }

        // Also serves plain SendAsync, with a write start nobody observes, so a caller that does
        // not use the write observation sees the same broker.
        public Func<Action, CancellationToken, ValueTask<ExpireDelegationTokenResponse>>? ExpireDelegationTokenHandler { get; set; }

        public int BrokerId => inner.BrokerId;
        public string Host => inner.Host;
        public int Port => inner.Port;
        public bool IsConnected => inner.IsConnected;

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            request is ExpireDelegationTokenRequest && ExpireDelegationTokenHandler is { } expire
                ? (ValueTask<TResponse>)(object)expire(static () => { }, cancellationToken)
                : inner.SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public ValueTask<TResponse> SendWithWriteObservationAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, Action requestWriteStarted, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            if (request is DeleteAclsRequest && DeleteAclsHandler is { } handler)
                return (ValueTask<TResponse>)(object)handler(requestWriteStarted, cancellationToken);
            if (request is ExpireDelegationTokenRequest && ExpireDelegationTokenHandler is { } expire)
                return (ValueTask<TResponse>)(object)expire(requestWriteStarted, cancellationToken);

            requestWriteStarted();
            return inner.SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
        }

        public ValueTask<PipelinedResponse<TResponse>> SendPipelinedWithWriteObservationAfterWriteAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, Action requestWriteStarted, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            throw new NotSupportedException();

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            inner.SendFireAndForgetAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            inner.SendPipelinedAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            inner.SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            inner.SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) =>
            inner.ConnectAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
