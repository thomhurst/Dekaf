using System.Net.Sockets;
using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

/// <summary>
/// A broker killed without a clean shutdown stays in cluster metadata until its session expires,
/// so admin requests routed to it fail at the transport for several seconds. These requests are
/// retried until the operation's API timeout instead of a fixed attempt count.
/// </summary>
public sealed class AdminClientTransportRetryTests
{
    private const string GroupId = "transport-group";

    private static AdminClientOptions FastRetryOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        RetryBackoffMs = 1,
        RetryBackoffMaxMs = 5
    };

    [Test]
    public async Task DescribeConsumerGroupsAsync_TransportFailuresBeyondRetryCount_SucceedsWithinApiTimeout()
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(
            FastRetryOptions(),
            ApiKey.ConsumerGroupDescribe);
        SetupFindCoordinator(connection);
        var calls = 0;

        connection.SendAsync<ConsumerGroupDescribeRequest, ConsumerGroupDescribeResponse>(
                Arg.Any<ConsumerGroupDescribeRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) <= 5)
                    throw new SocketException((int)SocketError.ConnectionRefused);

                return ValueTask.FromResult(new ConsumerGroupDescribeResponse
                {
                    Groups =
                    [
                        new ConsumerGroupDescribeGroup
                        {
                            ErrorCode = ErrorCode.None,
                            GroupId = GroupId,
                            GroupState = "Stable",
                            GroupEpoch = 1,
                            AssignmentEpoch = 1,
                            AssignorName = "uniform",
                            Members = []
                        }
                    ]
                });
            });

        var groups = await admin.DescribeConsumerGroupsAsync([GroupId]);

        await Assert.That(groups[GroupId].State).IsEqualTo("Stable");
        await Assert.That(calls).IsEqualTo(6);
    }

    [Test]
    public async Task DescribeConfigsAsync_TransportFailuresBeyondRetryCount_SucceedsWithinApiTimeout()
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(
            FastRetryOptions(),
            ApiKey.DescribeConfigs);
        var calls = 0;

        connection.SendAsync<DescribeConfigsRequest, DescribeConfigsResponse>(
                Arg.Any<DescribeConfigsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) <= 5)
                    throw new IOException("connection reset by peer");

                return ValueTask.FromResult(new DescribeConfigsResponse
                {
                    Results =
                    [
                        new DescribeConfigsResult
                        {
                            ErrorCode = ErrorCode.None,
                            ResourceType = (sbyte)ConfigResourceType.Topic,
                            ResourceName = "orders",
                            Configs = []
                        }
                    ]
                });
            });

        var result = await admin.DescribeConfigsAsync([ConfigResource.Topic("orders")]);

        await Assert.That(result.ContainsKey(ConfigResource.Topic("orders"))).IsTrue();
        await Assert.That(calls).IsEqualTo(6);
    }

    [Test]
    public async Task DescribeConfigsAsync_TransportFailuresPastTimeout_ThrowsTypedTimeoutWithTransportCause()
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(
            FastRetryOptions(),
            ApiKey.DescribeConfigs);
        var calls = 0;

        connection.SendAsync<DescribeConfigsRequest, DescribeConfigsResponse>(
                Arg.Any<DescribeConfigsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask<DescribeConfigsResponse>>(_ =>
            {
                Interlocked.Increment(ref calls);
                throw new SocketException((int)SocketError.ConnectionRefused);
            });

        var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
            await admin.DescribeConfigsAsync(
                [ConfigResource.Topic("orders")],
                new DescribeConfigsOptions { TimeoutMs = 300 }));

        await Assert.That(exception!.InnerException).IsTypeOf<SocketException>();
        // More attempts than the count-bounded retry allows, but bounded by the per-call timeout.
        await Assert.That(calls).IsGreaterThan(4);
    }

    [Test]
    public async Task DescribeConsumerGroupsAsync_BrokerAnsweredRetriableError_StaysCountBounded()
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(
            FastRetryOptions(),
            ApiKey.ConsumerGroupDescribe);
        SetupFindCoordinator(connection);
        var calls = 0;

        connection.SendAsync<ConsumerGroupDescribeRequest, ConsumerGroupDescribeResponse>(
                Arg.Any<ConsumerGroupDescribeRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref calls);
                return ValueTask.FromResult(new ConsumerGroupDescribeResponse
                {
                    Groups =
                    [
                        new ConsumerGroupDescribeGroup
                        {
                            ErrorCode = ErrorCode.CoordinatorLoadInProgress,
                            GroupId = GroupId,
                            GroupState = "",
                            AssignorName = "",
                            Members = []
                        }
                    ]
                });
            });

        var exception = await Assert.ThrowsAsync<GroupException>(async () =>
            await admin.DescribeConsumerGroupsAsync([GroupId]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.CoordinatorLoadInProgress);
        await Assert.That(calls).IsEqualTo(4);
    }

    [Test]
    public async Task DescribeAclsAsync_BrokerNeverAnswers_InFlightRequestEndsAtApiTimeout()
    {
        // The mocked connection applies no request timeout of its own, standing in for a broker
        // that accepted the request and stopped answering: only the API timeout can end it.
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(
            FastRetryOptions(),
            ApiKey.DescribeAcls);
        var calls = 0;

        connection.SendAsync<DescribeAclsRequest, DescribeAclsResponse>(
                Arg.Any<DescribeAclsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Interlocked.Increment(ref calls);
                return WaitForCancellationAsync(call.ArgAt<CancellationToken>(2));
            });

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
            await admin.DescribeAclsAsync(new AclBindingFilter(), new DescribeAclsOptions { TimeoutMs = 200 }));
        stopwatch.Stop();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);
        await Assert.That(exception.Message).Contains(nameof(IAdminClient.DescribeAclsAsync));
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(30));

        static async ValueTask<DescribeAclsResponse> WaitForCancellationAsync(CancellationToken token)
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new System.Diagnostics.UnreachableException();
        }
    }

    [Test]
    public async Task DescribeAclsAsync_CallerCancelsInFlightRequest_ThrowsCancellation()
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(
            FastRetryOptions(),
            ApiKey.DescribeAcls);
        using var cts = new CancellationTokenSource();

        connection.SendAsync<DescribeAclsRequest, DescribeAclsResponse>(
                Arg.Any<DescribeAclsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                cts.Cancel();
                return ValueTask.FromCanceled<DescribeAclsResponse>(call.ArgAt<CancellationToken>(2));
            });

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await admin.DescribeAclsAsync(new AclBindingFilter(), cancellationToken: cts.Token));
    }

    [Test]
    public async Task DescribeAclsAsync_RetriableEofBeyondRetryCount_RecoversWithinApiTimeout()
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(
            FastRetryOptions(),
            ApiKey.DescribeAcls);
        var calls = 0;

        connection.SendAsync<DescribeAclsRequest, DescribeAclsResponse>(
                Arg.Any<DescribeAclsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref calls) <= FailuresBeyondRetryCount
                ? throw ConnectionClosedByBroker()
                : ValueTask.FromResult(new DescribeAclsResponse { Resources = [] }));

        var result = await admin.DescribeAclsAsync(new AclBindingFilter());

        await Assert.That(result).IsEmpty();
        await Assert.That(calls).IsEqualTo(FailuresBeyondRetryCount + 1);
    }

    [Test]
    public async Task DescribeClientQuotasAsync_RetriableEofBeyondRetryCount_RecoversWithinApiTimeout()
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(
            FastRetryOptions(),
            ApiKey.DescribeClientQuotas);
        var calls = 0;

        connection.SendAsync<DescribeClientQuotasRequest, DescribeClientQuotasResponse>(
                Arg.Any<DescribeClientQuotasRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref calls) <= FailuresBeyondRetryCount
                ? throw ConnectionClosedByBroker()
                : ValueTask.FromResult(new DescribeClientQuotasResponse { Entries = [] }));

        var result = await admin.DescribeClientQuotasAsync(new ClientQuotaFilter { Components = [] });

        await Assert.That(result).IsEmpty();
        await Assert.That(calls).IsEqualTo(FailuresBeyondRetryCount + 1);
    }

    [Test]
    public async Task ListConsumerGroupOffsetsAsync_RetriableEofBeyondRetryCount_RecoversWithinApiTimeout()
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(
            FastRetryOptions(),
            ApiKey.OffsetFetch);
        SetupFindCoordinator(connection);
        var calls = 0;

        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref calls) <= FailuresBeyondRetryCount
                ? throw ConnectionClosedByBroker()
                : ValueTask.FromResult(new OffsetFetchResponse
                {
                    Groups =
                    [
                        new OffsetFetchResponseGroup
                        {
                            GroupId = GroupId,
                            ErrorCode = ErrorCode.None,
                            Topics =
                            [
                                new OffsetFetchResponseTopic
                                {
                                    Name = "orders",
                                    Partitions =
                                    [
                                        new OffsetFetchResponsePartition
                                        {
                                            PartitionIndex = 0,
                                            CommittedOffset = 42,
                                            ErrorCode = ErrorCode.None
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }));

        var result = await admin.ListConsumerGroupOffsetsAsync(GroupId);

        await Assert.That(result[new TopicPartition("orders", 0)]).IsEqualTo(42);
        await Assert.That(calls).IsEqualTo(FailuresBeyondRetryCount + 1);
    }

    // More consecutive failures than the count-bounded retry (RetryHelper.MaxRetries) allows.
    private const int FailuresBeyondRetryCount = 6;

    // What a connection reports when the broker closes it before the response arrives.
    private static KafkaException ConnectionClosedByBroker() =>
        new(ErrorCode.NetworkException, "Connection closed by the broker (EOF).");

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
}
