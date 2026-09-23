using System.Net.Sockets;
using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
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
    public async Task DescribeConfigsAsync_ZeroTimeout_ThrowsWithoutSending()
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

        // An explicit zero is an already-expired deadline, not "use the default timeout".
        var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
            await admin.DescribeConfigsAsync(
                [ConfigResource.Topic("orders")],
                new DescribeConfigsOptions { TimeoutMs = 0 }));

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);
        await Assert.That(calls).IsEqualTo(0);
    }

    [Test]
    public async Task DescribeConfigsAsync_InitializationNeverCompletes_EndsAtApiTimeout()
    {
        // A fresh client whose bootstrap broker accepts nothing: initialization counts against
        // the per-call timeout instead of running to its own, much longer, timeout first.
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(call.ArgAt<CancellationToken>(2)));
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(call.ArgAt<CancellationToken>(1)));
        await using var admin = new AdminClient(
            FastRetryOptions(),
            pool,
            new MetadataManager(pool, ["localhost:9092"]));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
            await admin.DescribeConfigsAsync(
                [ConfigResource.Topic("orders")],
                new DescribeConfigsOptions { TimeoutMs = 200 }));
        stopwatch.Stop();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(30));

        static async ValueTask<IKafkaConnection> WaitForCancellationAsync(CancellationToken token)
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new System.Diagnostics.UnreachableException();
        }
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
    public async Task DescribeConfigsAsync_CallerCancelsInFlightRequest_ReportsCallersToken()
    {
        // The send observes a token linked to the API timeout. The caller's cancellation must
        // still be reported with the caller's own token, so token-specific catch filters work.
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(
            FastRetryOptions(),
            ApiKey.DescribeConfigs);
        using var cancellation = new CancellationTokenSource();
        connection.SendAsync<DescribeConfigsRequest, DescribeConfigsResponse>(
                Arg.Any<DescribeConfigsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                cancellation.Cancel();
                return ValueTask.FromCanceled<DescribeConfigsResponse>(call.ArgAt<CancellationToken>(2));
            });

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await admin.DescribeConfigsAsync(
                [ConfigResource.Topic("orders")],
                new DescribeConfigsOptions { TimeoutMs = 30_000 },
                cancellation.Token));

        await Assert.That(exception!.CancellationToken == cancellation.Token).IsTrue();
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

    [Test]
    public async Task DescribeClientQuotasAsync_TransportFailuresPastTimeout_EndsAtPerCallTimeout()
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(
            FastRetryOptions(),
            ApiKey.DescribeClientQuotas);
        var calls = 0;

        connection.SendAsync<DescribeClientQuotasRequest, DescribeClientQuotasResponse>(
                Arg.Any<DescribeClientQuotasRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask<DescribeClientQuotasResponse>>(_ =>
            {
                Interlocked.Increment(ref calls);
                throw new SocketException((int)SocketError.ConnectionRefused);
            });

        // DescribeClientQuotasOptions.TimeoutMs bounds the retries, not the 60 s default.
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
            await admin.DescribeClientQuotasAsync(
                new ClientQuotaFilter { Components = [] },
                new DescribeClientQuotasOptions { TimeoutMs = 300 }));
        stopwatch.Stop();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);
        await Assert.That(exception.InnerException).IsTypeOf<SocketException>();
        await Assert.That(calls).IsGreaterThan(0);
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(30));
    }

    // Admin APIs whose options carry a client-side TimeoutMs that is not sent to the broker.
    [Test]
    [Arguments(nameof(IAdminClient.DescribeClientQuotasAsync))]
    [Arguments(nameof(IAdminClient.AlterClientQuotasAsync))]
    [Arguments(nameof(IAdminClient.DescribeUserScramCredentialsAsync))]
    [Arguments(nameof(IAdminClient.AlterUserScramCredentialsAsync))]
    [Arguments(nameof(IAdminClient.AlterConfigsAsync))]
    [Arguments(nameof(IAdminClient.IncrementalAlterConfigsAsync))]
    [Arguments(nameof(IAdminClient.CreateAclsAsync))]
    [Arguments(nameof(IAdminClient.DeleteConsumerGroupOffsetsAsync))]
    [Arguments(nameof(IAdminClient.ListConsumerGroupsAsync))]
    public async Task PerCallTimeout_Zero_ThrowsApiTimeoutWithoutSending(string api)
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(FastRetryOptions());

        var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
            await InvokeWithTimeoutAsync(admin, api, timeoutMs: 0));

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);
        await Assert.That(exception.Message).Contains(api);
        await Assert.That(SentRequests(connection)).IsEqualTo(0);
    }

    [Test]
    [Arguments(nameof(IAdminClient.DescribeClientQuotasAsync))]
    [Arguments(nameof(IAdminClient.AlterClientQuotasAsync))]
    [Arguments(nameof(IAdminClient.DescribeUserScramCredentialsAsync))]
    [Arguments(nameof(IAdminClient.AlterUserScramCredentialsAsync))]
    [Arguments(nameof(IAdminClient.AlterConfigsAsync))]
    [Arguments(nameof(IAdminClient.IncrementalAlterConfigsAsync))]
    [Arguments(nameof(IAdminClient.CreateAclsAsync))]
    [Arguments(nameof(IAdminClient.DeleteConsumerGroupOffsetsAsync))]
    [Arguments(nameof(IAdminClient.ListConsumerGroupsAsync))]
    public async Task PerCallTimeout_Negative_IsRejected(string api)
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(FastRetryOptions());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await InvokeWithTimeoutAsync(admin, api, timeoutMs: -1));

        await Assert.That(SentRequests(connection)).IsEqualTo(0);
    }

    [Test]
    public async Task DescribeConsumerGroupsAsync_InitializationNeverCompletes_EndsAtDefaultApiTimeout()
    {
        // An API without a per-call timeout: the default API timeout starts before
        // initialization, so a bootstrap broker that accepts nothing cannot extend the call.
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(call.ArgAt<CancellationToken>(2)));
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(call.ArgAt<CancellationToken>(1)));
        await using var admin = new AdminClient(
            FastRetryOptions(),
            pool,
            new MetadataManager(pool, ["localhost:9092"]))
        {
            DefaultApiTimeoutBudgetMs = 200
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
            await admin.DescribeConsumerGroupsAsync([GroupId]));
        stopwatch.Stop();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);
        await Assert.That(exception.Message).Contains(nameof(IAdminClient.DescribeConsumerGroupsAsync));
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(30));

        static async ValueTask<IKafkaConnection> WaitForCancellationAsync(CancellationToken token)
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new System.Diagnostics.UnreachableException();
        }
    }

    private static async ValueTask InvokeWithTimeoutAsync(AdminClient admin, string api, int timeoutMs)
    {
        switch (api)
        {
            case nameof(IAdminClient.DescribeClientQuotasAsync):
                await admin.DescribeClientQuotasAsync(
                    new ClientQuotaFilter { Components = [] },
                    new DescribeClientQuotasOptions { TimeoutMs = timeoutMs });
                break;
            case nameof(IAdminClient.AlterClientQuotasAsync):
                await admin.AlterClientQuotasAsync(
                    [
                        new ClientQuotaAlteration
                        {
                            Entity = ClientQuotaEntity.ForUser("alice"),
                            Operations = [ClientQuotaOperation.Set("producer_byte_rate", 1024)]
                        }
                    ],
                    new AlterClientQuotasOptions { TimeoutMs = timeoutMs });
                break;
            case nameof(IAdminClient.DescribeUserScramCredentialsAsync):
                await admin.DescribeUserScramCredentialsAsync(
                    ["alice"],
                    new DescribeUserScramCredentialsOptions { TimeoutMs = timeoutMs });
                break;
            case nameof(IAdminClient.AlterUserScramCredentialsAsync):
                await admin.AlterUserScramCredentialsAsync(
                    [new UserScramCredentialDeletion { User = "alice", Mechanism = ScramMechanism.ScramSha256 }],
                    new AlterUserScramCredentialsOptions { TimeoutMs = timeoutMs });
                break;
            case nameof(IAdminClient.AlterConfigsAsync):
                await admin.AlterConfigsAsync(
                    new Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>>
                    {
                        [ConfigResource.Topic("orders")] = [new ConfigEntry { Name = "retention.ms", Value = "1000" }]
                    },
                    new AlterConfigsOptions { TimeoutMs = timeoutMs });
                break;
            case nameof(IAdminClient.IncrementalAlterConfigsAsync):
                await admin.IncrementalAlterConfigsAsync(
                    new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
                    {
                        [ConfigResource.Topic("orders")] = [new ConfigAlter { Name = "retention.ms", Value = "1000" }]
                    },
                    new IncrementalAlterConfigsOptions { TimeoutMs = timeoutMs });
                break;
            case nameof(IAdminClient.CreateAclsAsync):
                await admin.CreateAclsAsync(
                    [
                        new AclBinding
                        {
                            Pattern = new ResourcePattern { Type = ResourceType.Topic, Name = "orders" },
                            Entry = new AccessControlEntry
                            {
                                Principal = "User:alice",
                                Operation = AclOperation.Read,
                                Permission = AclPermissionType.Allow
                            }
                        }
                    ],
                    new CreateAclsOptions { TimeoutMs = timeoutMs });
                break;
            case nameof(IAdminClient.DeleteConsumerGroupOffsetsAsync):
                await admin.DeleteConsumerGroupOffsetsAsync(
                    GroupId,
                    [new TopicPartition("orders", 0)],
                    new DeleteConsumerGroupOffsetsOptions { TimeoutMs = timeoutMs });
                break;
            case nameof(IAdminClient.ListConsumerGroupsAsync):
                await admin.ListConsumerGroupsAsync(new ListConsumerGroupsOptions { TimeoutMs = timeoutMs });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(api), api, null);
        }
    }

    // APIs whose TimeoutMs is also the broker-side operation timeout sent in the request.
    [Test]
    [Arguments(nameof(IAdminClient.CreateTopicsAsync))]
    [Arguments("DeleteTopicsAsync(names)")]
    [Arguments("DeleteTopicsAsync(ids)")]
    [Arguments(nameof(IAdminClient.CreatePartitionsAsync))]
    [Arguments(nameof(IAdminClient.UpdateFeaturesAsync))]
    [Arguments(nameof(IAdminClient.ElectLeadersAsync))]
    [Arguments(nameof(IAdminClient.AlterPartitionReassignmentsAsync))]
    [Arguments(nameof(IAdminClient.AddRaftVoterAsync))]
    [Arguments(nameof(IAdminClient.FenceProducersAsync))]
    public async Task OperationTimeout_Positive_BoundsTheWholeCall(string api)
    {
        // No broker is reachable to enforce the operation timeout carried in the request, so the
        // client must stop at it instead of retrying for the default API timeout.
        await using var admin = CreateAdminWithUnreachableBootstrap();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
            await InvokeWithOperationTimeoutAsync(admin, api, timeoutMs: 300));
        stopwatch.Stop();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);
        await Assert.That(exception.Configured).IsEqualTo(TimeSpan.FromMilliseconds(300));
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(20));
    }

    [Test]
    [Arguments(nameof(IAdminClient.FenceProducersAsync))]
    [Arguments(nameof(AdminClient.ForceTerminateTransactionAsync))]
    public async Task FencingTimeout_OptionsOmitted_BoundedByRequestTimeout(string api)
    {
        // Without options the fencing timeout sent to the coordinator is RequestTimeoutMs, so the
        // call is bounded by that value and not by the separate default API budget.
        var options = FastRetryOptions();
        options = new AdminClientOptions
        {
            BootstrapServers = options.BootstrapServers,
            RetryBackoffMs = options.RetryBackoffMs,
            RetryBackoffMaxMs = options.RetryBackoffMaxMs,
            RequestTimeoutMs = 400
        };
        await using var admin = CreateAdminWithUnreachableBootstrap(defaultApiTimeoutBudgetMs: 5_000, options: options);

        var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
        {
            if (api == nameof(IAdminClient.FenceProducersAsync))
                await admin.FenceProducersAsync(["txn-1"]);
            else
                await admin.ForceTerminateTransactionAsync("txn-1");
        });

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);
        await Assert.That(exception.Configured).IsEqualTo(TimeSpan.FromMilliseconds(400));
    }

    [Test]
    public async Task CreateTopicsAsync_ZeroOperationTimeout_StillSendsTheRequest()
    {
        // Zero keeps its broker meaning: start the creation and do not wait for it to complete.
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(
            FastRetryOptions(),
            ApiKey.CreateTopics);
        var sentTimeoutMs = -1;

        connection.SendAsync<CreateTopicsRequest, CreateTopicsResponse>(
                Arg.Any<CreateTopicsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                sentTimeoutMs = call.ArgAt<CreateTopicsRequest>(0).TimeoutMs;
                return ValueTask.FromResult(new CreateTopicsResponse
                {
                    Topics = [new CreateTopicsResponseTopic { Name = "retry-topic", ErrorCode = ErrorCode.None }]
                });
            });

        // retry-topic is in the mocked metadata, so the leader wait after creation completes.
        await admin.CreateTopicsAsync([new NewTopic { Name = "retry-topic" }], new CreateTopicsOptions { TimeoutMs = 0 });

        await Assert.That(sentTimeoutMs).IsEqualTo(0);
    }

    [Test]
    public async Task AlterConfigsAsync_OptionsOmitted_UsesTheOptionsDefaultTimeout()
    {
        // new AlterConfigsOptions() documents 30 s; omitting the options must not select the
        // longer default budget of APIs without a per-call timeout.
        await using var admin = CreateAdminWithUnreachableBootstrap(defaultApiTimeoutBudgetMs: 45_000);

        var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
            await admin.AlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>>
            {
                [ConfigResource.Topic("orders")] = [new ConfigEntry { Name = "retention.ms", Value = "1000" }]
            }));

        await Assert.That(exception!.Configured)
            .IsEqualTo(TimeSpan.FromMilliseconds(new AlterConfigsOptions().TimeoutMs));
    }

    [Test]
    public async Task DescribeFeaturesAsync_TimeoutLongerThanDefaultBudget_KeepsRetrying()
    {
        // An explicitly timed call must not stop at the default budget of its inner retry.
        var outage = System.Diagnostics.Stopwatch.StartNew();
        var (admin, _) = AdminClientIdempotentRetryTests.CreateAdminWithConnection(
            FastRetryOptions(),
            connection => new FailingUntilConnection(connection, outage, TimeSpan.FromMilliseconds(800)),
            ApiKey.ApiVersions);
        await using var disposeAdmin = admin;
        SetDefaultApiTimeoutBudget(admin, 200);

        var features = await admin.DescribeFeaturesAsync(new DescribeFeaturesOptions { TimeoutMs = 10_000 });

        await Assert.That(features).IsNotNull();
        await Assert.That(outage.Elapsed).IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(800));
    }

    [Test]
    public async Task DeleteConsumerGroupsDetailedAsync_TimeoutLongerThanDefaultBudget_KeepsRetrying()
    {
        var outage = System.Diagnostics.Stopwatch.StartNew();
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithConnection(
            FastRetryOptions(),
            connection => new FailingUntilConnection(connection, outage, TimeSpan.FromMilliseconds(800)),
            ApiKey.DeleteGroups);
        await using var disposeAdmin = admin;
        SetDefaultApiTimeoutBudget(admin, 200);
        SetupFindCoordinator(connection);
        connection.SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
                Arg.Any<DeleteGroupsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new DeleteGroupsResponse
            {
                Results = [new DeleteGroupsResponseResult { GroupId = GroupId, ErrorCode = ErrorCode.None }]
            }));

        var results = await admin.DeleteConsumerGroupsDetailedAsync(
            [GroupId],
            new ConsumerGroupMutationOptions { TimeoutMs = 10_000 });

        await Assert.That(results[GroupId].IsSuccess).IsTrue();
    }

    // The helpers build the client; the budget is an init-only test hook.
    private static void SetDefaultApiTimeoutBudget(AdminClient admin, int budgetMs) =>
        typeof(AdminClient)
            .GetProperty(nameof(AdminClient.DefaultApiTimeoutBudgetMs),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(admin, budgetMs);

    private static AdminClient CreateAdminWithUnreachableBootstrap(
        int defaultApiTimeoutBudgetMs = AdminClient.DefaultApiTimeoutMs,
        AdminClientOptions? options = null)
    {
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(call.ArgAt<CancellationToken>(2)));
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(call.ArgAt<CancellationToken>(1)));
        return new AdminClient(options ?? FastRetryOptions(), pool, new MetadataManager(pool, ["localhost:9092"]))
        {
            DefaultApiTimeoutBudgetMs = defaultApiTimeoutBudgetMs
        };

        static async ValueTask<IKafkaConnection> WaitForCancellationAsync(CancellationToken token)
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new System.Diagnostics.UnreachableException();
        }
    }

    private static async ValueTask InvokeWithOperationTimeoutAsync(AdminClient admin, string api, int timeoutMs)
    {
        switch (api)
        {
            case nameof(IAdminClient.CreateTopicsAsync):
                await admin.CreateTopicsAsync([new NewTopic { Name = "orders" }], new CreateTopicsOptions { TimeoutMs = timeoutMs });
                break;
            case "DeleteTopicsAsync(names)":
                await admin.DeleteTopicsAsync(["orders"], new DeleteTopicsOptions { TimeoutMs = timeoutMs });
                break;
            case "DeleteTopicsAsync(ids)":
                await ((ITopicIdAdminClient)admin).DeleteTopicsAsync([Guid.NewGuid()], new DeleteTopicsOptions { TimeoutMs = timeoutMs });
                break;
            case nameof(IAdminClient.CreatePartitionsAsync):
                await ((IPartitionExpansionAdminClient)admin).CreatePartitionsAsync(
                    new Dictionary<string, NewPartitions> { ["orders"] = new() { TotalCount = 2 } },
                    new CreatePartitionsOptions { TimeoutMs = timeoutMs });
                break;
            case nameof(IAdminClient.UpdateFeaturesAsync):
                await admin.UpdateFeaturesAsync(
                    new Dictionary<string, FeatureUpdate> { ["metadata.version"] = new() { MaxVersionLevel = 1 } },
                    new UpdateFeaturesOptions { TimeoutMs = timeoutMs });
                break;
            case nameof(IAdminClient.ElectLeadersAsync):
                await admin.ElectLeadersAsync(
                    ElectionType.Preferred,
                    [new TopicPartition("orders", 0)],
                    new ElectLeadersOptions { TimeoutMs = timeoutMs });
                break;
            case nameof(IAdminClient.AlterPartitionReassignmentsAsync):
                await admin.AlterPartitionReassignmentsAsync(
                    new Dictionary<TopicPartition, Optional<NewPartitionReassignment>>
                    {
                        [new TopicPartition("orders", 0)] = Optional.Some(NewPartitionReassignment.ToReplicas(1))
                    },
                    new AlterPartitionReassignmentsOptions { TimeoutMs = timeoutMs });
                break;
            case nameof(IAdminClient.AddRaftVoterAsync):
                await admin.AddRaftVoterAsync(
                    3,
                    Guid.NewGuid(),
                    [new RaftVoterEndpoint { Name = "CONTROLLER", Host = "localhost", Port = 9093 }],
                    new AddRaftVoterOptions { TimeoutMs = timeoutMs });
                break;
            case nameof(IAdminClient.FenceProducersAsync):
                await admin.FenceProducersAsync(["txn-1"], new FenceProducersOptions { TimeoutMs = timeoutMs });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(api), api, null);
        }
    }

    // Fails every send at the transport until the outage ends, then delegates to the substitute.
    private sealed class FailingUntilConnection(
        IKafkaConnection inner,
        System.Diagnostics.Stopwatch clock,
        TimeSpan outage) : IKafkaConnection
    {
        private bool Down => clock.Elapsed < outage;

        public int BrokerId => inner.BrokerId;
        public string Host => inner.Host;
        public int Port => inner.Port;
        public bool IsConnected => inner.IsConnected;

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            Down
                ? ValueTask.FromException<TResponse>(new SocketException((int)SocketError.ConnectionRefused))
                : inner.SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

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

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => inner.ConnectAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private static int SentRequests(IKafkaConnection connection) =>
        connection.ReceivedCalls().Count(static call => call.GetMethodInfo().Name == nameof(IKafkaConnection.SendAsync));

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
