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
