using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

/// <summary>
/// Tests that CreateTopicsAsync retries are idempotent: a retriable failure after the
/// broker accepted the create must not surface TopicAlreadyExists from the retry attempt.
/// </summary>
public sealed class AdminClientCreateTopicsRetryTests
{
    private const string TopicName = "retry-topic";

    [Test]
    public async Task CreateTopicsAsync_TopicAlreadyExistsOnRetry_TreatedAsSuccess()
    {
        var (admin, connection, _) = CreateAdminWithMockConnection();

        var createCalls = 0;
        connection.SendAsync<CreateTopicsRequest, CreateTopicsResponse>(
                Arg.Any<CreateTopicsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                // First attempt: broker processed the create but the client saw a
                // retriable failure (e.g. response timeout). Retry hits AlreadyExists.
                if (Interlocked.Increment(ref createCalls) == 1)
                {
                    throw new KafkaException(ErrorCode.RequestTimedOut, "simulated timeout");
                }

                return ValueTask.FromResult(new CreateTopicsResponse
                {
                    Topics =
                    [
                        new CreateTopicsResponseTopic
                        {
                            Name = TopicName,
                            ErrorCode = ErrorCode.TopicAlreadyExists,
                            ErrorMessage = $"Topic '{TopicName}' already exists."
                        }
                    ]
                });
            });

        await admin.CreateTopicsAsync([new NewTopic { Name = TopicName, NumPartitions = 1 }]);

        await Assert.That(createCalls).IsEqualTo(2);
    }

    [Test]
    public async Task CreateTopicsAsync_ValidateOnly_TopicAlreadyExistsOnRetry_Throws()
    {
        var (admin, connection, _) = CreateAdminWithMockConnection();

        var createCalls = 0;
        connection.SendAsync<CreateTopicsRequest, CreateTopicsResponse>(
                Arg.Any<CreateTopicsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref createCalls) == 1)
                {
                    throw new KafkaException(ErrorCode.RequestTimedOut, "simulated timeout");
                }

                return ValueTask.FromResult(new CreateTopicsResponse
                {
                    Topics =
                    [
                        new CreateTopicsResponseTopic
                        {
                            Name = TopicName,
                            ErrorCode = ErrorCode.TopicAlreadyExists,
                            ErrorMessage = $"Topic '{TopicName}' already exists."
                        }
                    ]
                });
            });

        // Validate-only never mutates cluster state, so AlreadyExists on a retry is a
        // genuine conflict and must surface.
        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.CreateTopicsAsync(
                [new NewTopic { Name = TopicName, NumPartitions = 1 }],
                new CreateTopicsOptions { ValidateOnly = true }));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.TopicAlreadyExists);
    }

    [Test]
    public async Task CreateTopicsAsync_TopicAlreadyExistsAfterNonAmbiguousRetry_Throws()
    {
        var (admin, connection, _) = CreateAdminWithMockConnection();
        var createCalls = 0;

        connection.SendAsync<CreateTopicsRequest, CreateTopicsResponse>(
                Arg.Any<CreateTopicsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(new CreateTopicsResponse
            {
                Topics =
                [
                    new CreateTopicsResponseTopic
                    {
                        Name = TopicName,
                        ErrorCode = Interlocked.Increment(ref createCalls) == 1
                            ? ErrorCode.NotController
                            : ErrorCode.TopicAlreadyExists
                    }
                ]
            }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.CreateTopicsAsync([new NewTopic { Name = TopicName, NumPartitions = 1 }]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.TopicAlreadyExists);
        await Assert.That(createCalls).IsEqualTo(2);
    }

    [Test]
    public async Task CreateTopicsAsync_TopicAlreadyExistsOnFirstAttempt_Throws()
    {
        var (admin, connection, _) = CreateAdminWithMockConnection();

        connection.SendAsync<CreateTopicsRequest, CreateTopicsResponse>(
                Arg.Any<CreateTopicsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new CreateTopicsResponse
            {
                Topics =
                [
                    new CreateTopicsResponseTopic
                    {
                        Name = TopicName,
                        ErrorCode = ErrorCode.TopicAlreadyExists,
                        ErrorMessage = $"Topic '{TopicName}' already exists."
                    }
                ]
            }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.CreateTopicsAsync([new NewTopic { Name = TopicName, NumPartitions = 1 }]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.TopicAlreadyExists);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CreateTopicsAsync_NoController_RefreshesUntilControllerIsElected(bool detailed)
    {
        var (admin, connection, metadata) = CreateAdminWithMockConnection();
        await using var ownedAdmin = admin;
        metadata.Metadata.Update(CreateMetadataResponse(controllerId: -1));
        var refreshes = 0;
        var refreshesWhenSent = 0;
        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(CreateMetadataResponse(
                controllerId: ++refreshes < 5 ? -1 : 1)));
        connection.SendAsync<CreateTopicsRequest, CreateTopicsResponse>(
                Arg.Any<CreateTopicsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                refreshesWhenSent = refreshes;
                return ValueTask.FromResult(new CreateTopicsResponse
                {
                    Topics = [new CreateTopicsResponseTopic { Name = TopicName, ErrorCode = ErrorCode.None }]
                });
            });

        if (detailed)
        {
            var results = await admin.CreateTopicsDetailedAsync([new NewTopic { Name = TopicName, NumPartitions = 1 }]);
            await Assert.That(results[TopicName].IsSuccess).IsTrue();
        }
        else
        {
            await admin.CreateTopicsAsync([new NewTopic { Name = TopicName, NumPartitions = 1 }]);
        }

        // A missing route must not exhaust the three retries for broker error responses.
        await Assert.That(refreshesWhenSent).IsEqualTo(5);
        await connection.Received(1).SendAsync<CreateTopicsRequest, CreateTopicsResponse>(
            Arg.Any<CreateTopicsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CreateTopicsAsync_NoControllerUntilDeadline_ThrowsKafkaTimeoutWithoutSending(bool detailed)
    {
        var (admin, connection, metadata) = CreateAdminWithMockConnection();
        await using var ownedAdmin = admin;
        metadata.Metadata.Update(CreateMetadataResponse(controllerId: -1));
        var refreshes = 0;
        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => ++refreshes < 5
                ? ValueTask.FromResult(CreateMetadataResponse(controllerId: -1))
                : WaitForCancellationAsync(call.ArgAt<CancellationToken>(2)));

        KafkaTimeoutException? exception;
        if (detailed)
        {
            var results = await admin.CreateTopicsDetailedAsync([new NewTopic { Name = TopicName, NumPartitions = 1 }],
                new CreateTopicsOptions { TimeoutMs = 1000 });
            await Assert.That(results[TopicName].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
            exception = results[TopicName].Exception as KafkaTimeoutException;
            await Assert.That(exception).IsNotNull();
        }
        else
        {
            exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
                await admin.CreateTopicsAsync([new NewTopic { Name = TopicName, NumPartitions = 1 }],
                    new CreateTopicsOptions { TimeoutMs = 1000 }));
        }

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);
        var cause = exception.InnerException as KafkaException;
        await Assert.That(cause).IsNotNull();
        await Assert.That(cause!.ErrorCode).IsEqualTo(ErrorCode.BrokerNotAvailable);
        await Assert.That(cause.IsRetriable).IsTrue();
        await Assert.That(refreshes).IsEqualTo(5);
        await connection.DidNotReceive().SendAsync<CreateTopicsRequest, CreateTopicsResponse>(
            Arg.Any<CreateTopicsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateTopicsAsync_NoController_CallerCancellationStopsRecovery()
    {
        var (admin, connection, metadata) = CreateAdminWithMockConnection();
        await using var ownedAdmin = admin;
        using var cancellation = new CancellationTokenSource();
        metadata.Metadata.Update(CreateMetadataResponse(controllerId: -1));
        var recoveryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                recoveryStarted.TrySetResult();
                return WaitForCancellationAsync(call.ArgAt<CancellationToken>(2));
            });

        var pending = admin.CreateTopicsAsync([new NewTopic { Name = TopicName, NumPartitions = 1 }],
            cancellationToken: cancellation.Token).AsTask();
        try
        {
            await recoveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            cancellation.Cancel();
        }

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending);
        await Assert.That(exception!.CancellationToken).IsEqualTo(cancellation.Token);
        await connection.DidNotReceive().SendAsync<CreateTopicsRequest, CreateTopicsResponse>(
            Arg.Any<CreateTopicsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    private static async ValueTask<MetadataResponse> WaitForCancellationAsync(CancellationToken token)
    {
        await Task.Delay(Timeout.Infinite, token);
        throw new InvalidOperationException("Cancellation was expected.");
    }

    private static (AdminClient Admin, IKafkaConnection Connection, MetadataManager Metadata) CreateAdminWithMockConnection()
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
        metadataManager.SetApiVersion(ApiKey.CreateTopics, 5, 7);
        metadataManager.SetApiVersion(ApiKey.Metadata, 9, 13);

        // Metadata refreshes (retry helper + WaitForTopicLeadersAsync) must observe the
        // topic with all partition leaders elected so the create completes.
        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateMetadataResponse()));

        // Metadata refresh negotiates API versions on the connection first.
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
                    new ApiVersion(ApiKey.CreateTopics, 5, 7)
                ]
            }));

        var admin = new AdminClient(
            new AdminClientOptions { BootstrapServers = ["localhost:9092"], RetryBackoffMs = 1, RetryBackoffMaxMs = 5 },
            pool,
            metadataManager);

        return (admin, connection, metadataManager);
    }

    private static MetadataResponse CreateMetadataResponse(int controllerId = 1) => new()
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
        ControllerId = controllerId,
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
                    }
                ]
            }
        ]
    };
}
