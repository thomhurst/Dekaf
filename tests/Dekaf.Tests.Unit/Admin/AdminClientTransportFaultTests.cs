using System.Diagnostics;
using System.Net.Sockets;
using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

/// <summary>
/// Forced transport faults against admin operations: a killed broker stays in metadata until its
/// session expires, so reads must keep retrying until the API timeout, and a mutation whose
/// response was lost must accept the "already applied" answer of its replay.
/// </summary>
public sealed class AdminClientTransportFaultTests
{
    private const string GroupId = "fault-group";

    // More consecutive transport failures than the count-bounded retry (RetryHelper.MaxRetries) allows.
    private const int FailuresBeyondRetryCount = 6;

    [Test]
    public async Task DescribeAclsAsync_TransportFailuresBeyondRetryCount_RecoverWithinApiTimeout()
    {
        var (admin, connection) = CreateAdmin(ApiKey.DescribeAcls);
        var calls = 0;

        connection.SendAsync<DescribeAclsRequest, DescribeAclsResponse>(
                Arg.Any<DescribeAclsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref calls) <= FailuresBeyondRetryCount
                ? throw new SocketException((int)SocketError.ConnectionRefused)
                : ValueTask.FromResult(new DescribeAclsResponse { Resources = [] }));

        var result = await admin.DescribeAclsAsync(new AclBindingFilter());

        await Assert.That(result).IsEmpty();
        await Assert.That(calls).IsEqualTo(FailuresBeyondRetryCount + 1);
    }

    [Test]
    public async Task DescribeClientQuotasAsync_ConnectionResetsBeyondRetryCount_RecoverWithinApiTimeout()
    {
        var (admin, connection) = CreateAdmin(ApiKey.DescribeClientQuotas);
        var calls = 0;

        connection.SendAsync<DescribeClientQuotasRequest, DescribeClientQuotasResponse>(
                Arg.Any<DescribeClientQuotasRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref calls) <= FailuresBeyondRetryCount
                ? throw new IOException("connection reset by peer")
                : ValueTask.FromResult(new DescribeClientQuotasResponse { Entries = [] }));

        var result = await admin.DescribeClientQuotasAsync(new ClientQuotaFilter { Components = [] });

        await Assert.That(result).IsEmpty();
        await Assert.That(calls).IsEqualTo(FailuresBeyondRetryCount + 1);
    }

    [Test]
    public async Task ListConsumerGroupOffsetsAsync_CoordinatorUnreachableBeyondRetryCount_RecoversWithinApiTimeout()
    {
        var (admin, connection) = CreateAdmin(ApiKey.OffsetFetch);
        SetupFindCoordinator(connection);
        var calls = 0;

        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref calls) <= FailuresBeyondRetryCount
                ? throw new KafkaException(ErrorCode.NetworkException, "Connection closed by the broker (EOF).")
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
    public async Task DescribeAclsAsync_TransportFailuresPastApiTimeout_ThrowsTypedTimeoutCarryingTheCause()
    {
        var (admin, connection) = CreateAdmin(ApiKey.DescribeAcls, defaultApiTimeoutMs: 200);

        connection.SendAsync<DescribeAclsRequest, DescribeAclsResponse>(
                Arg.Any<DescribeAclsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask<DescribeAclsResponse>>(_ =>
                throw new SocketException((int)SocketError.ConnectionRefused));

        var stopwatch = Stopwatch.StartNew();
        var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
            await admin.DescribeAclsAsync(new AclBindingFilter()));
        stopwatch.Stop();

        await Assert.That(exception!.InnerException).IsTypeOf<SocketException>();
        await Assert.That(exception.Message).Contains(nameof(IAdminClient.DescribeAclsAsync));
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task DescribeAclsAsync_RetriableBrokerError_KeepsTheBoundedRetryCount()
    {
        var (admin, connection) = CreateAdmin(ApiKey.DescribeAcls);
        var calls = 0;

        connection.SendAsync<DescribeAclsRequest, DescribeAclsResponse>(
                Arg.Any<DescribeAclsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref calls);
                return ValueTask.FromResult(new DescribeAclsResponse
                {
                    ErrorCode = ErrorCode.RequestTimedOut,
                    Resources = []
                });
            });

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.DescribeAclsAsync(new AclBindingFilter()));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
        await Assert.That(calls).IsEqualTo(4);
    }

    [Test]
    public async Task AddRaftVoterAsync_DuplicateVoterAfterLostResponse_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdmin(ApiKey.AddRaftVoter);
        var calls = 0;

        connection.SendAsync<AddRaftVoterRequest, AddRaftVoterResponse>(
                Arg.Any<AddRaftVoterRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref calls) == 1
                ? throw new IOException("response lost")
                : ValueTask.FromResult(new AddRaftVoterResponse { ErrorCode = ErrorCode.DuplicateVoter }));

        await AddVoterAsync(admin);

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task AddRaftVoterAsync_DuplicateVoterOnFirstAttempt_Throws()
    {
        var (admin, connection) = CreateAdmin(ApiKey.AddRaftVoter);

        connection.SendAsync<AddRaftVoterRequest, AddRaftVoterResponse>(
                Arg.Any<AddRaftVoterRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new AddRaftVoterResponse { ErrorCode = ErrorCode.DuplicateVoter }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () => await AddVoterAsync(admin));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.DuplicateVoter);
    }

    [Test]
    public async Task RemoveRaftVoterAsync_VoterNotFoundAfterLostResponse_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdmin(ApiKey.RemoveRaftVoter);
        var calls = 0;

        connection.SendAsync<RemoveRaftVoterRequest, RemoveRaftVoterResponse>(
                Arg.Any<RemoveRaftVoterRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref calls) == 1
                ? throw new IOException("response lost")
                : ValueTask.FromResult(new RemoveRaftVoterResponse { ErrorCode = ErrorCode.VoterNotFound }));

        await admin.RemoveRaftVoterAsync(voterId: 2, voterDirectoryId: Guid.NewGuid());

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task RemoveRaftVoterAsync_VoterNotFoundOnFirstAttempt_Throws()
    {
        var (admin, connection) = CreateAdmin(ApiKey.RemoveRaftVoter);

        connection.SendAsync<RemoveRaftVoterRequest, RemoveRaftVoterResponse>(
                Arg.Any<RemoveRaftVoterRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new RemoveRaftVoterResponse { ErrorCode = ErrorCode.VoterNotFound }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.RemoveRaftVoterAsync(voterId: 2, voterDirectoryId: Guid.NewGuid()));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.VoterNotFound);
    }

    [Test]
    public async Task UnregisterBrokerAsync_BrokerIdNotRegisteredAfterLostResponse_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdmin(ApiKey.UnregisterBroker);
        var calls = 0;

        connection.SendAsync<UnregisterBrokerRequest, UnregisterBrokerResponse>(
                Arg.Any<UnregisterBrokerRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref calls) == 1
                ? throw new IOException("response lost")
                : ValueTask.FromResult(new UnregisterBrokerResponse { ErrorCode = ErrorCode.BrokerIdNotRegistered }));

        await admin.UnregisterBrokerAsync(brokerId: 3);

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task UnregisterBrokerAsync_BrokerIdNotRegisteredOnFirstAttempt_Throws()
    {
        var (admin, connection) = CreateAdmin(ApiKey.UnregisterBroker);

        connection.SendAsync<UnregisterBrokerRequest, UnregisterBrokerResponse>(
                Arg.Any<UnregisterBrokerRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new UnregisterBrokerResponse { ErrorCode = ErrorCode.BrokerIdNotRegistered }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.UnregisterBrokerAsync(brokerId: 3));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.BrokerIdNotRegistered);
    }

    [Test]
    public async Task ExpireDelegationTokenAsync_ImmediateExpiryReplayFindsNoToken_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdmin(ApiKey.ExpireDelegationToken);
        var calls = 0;

        connection.SendAsync<ExpireDelegationTokenRequest, ExpireDelegationTokenResponse>(
                Arg.Any<ExpireDelegationTokenRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref calls) == 1
                ? throw new IOException("response lost")
                : ValueTask.FromResult(new ExpireDelegationTokenResponse { ErrorCode = ErrorCode.DelegationTokenNotFound }));

        var before = DateTimeOffset.UtcNow;
        var expiry = await admin.ExpireDelegationTokenAsync([1, 2, 3]);

        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(expiry).IsGreaterThanOrEqualTo(before.AddSeconds(-1));
    }

    [Test]
    public async Task ExpireDelegationTokenAsync_TokenNotFoundOnFirstAttempt_Throws()
    {
        var (admin, connection) = CreateAdmin(ApiKey.ExpireDelegationToken);

        connection.SendAsync<ExpireDelegationTokenRequest, ExpireDelegationTokenResponse>(
                Arg.Any<ExpireDelegationTokenRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ExpireDelegationTokenResponse { ErrorCode = ErrorCode.DelegationTokenNotFound }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.ExpireDelegationTokenAsync([1, 2, 3]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.DelegationTokenNotFound);
    }

    [Test]
    public async Task ExpireDelegationTokenAsync_PositivePeriodReplayFindsNoToken_Throws()
    {
        // A positive period only moves the expiry, so a replay that finds no token did not
        // follow our own removal.
        var (admin, connection) = CreateAdmin(ApiKey.ExpireDelegationToken);
        var calls = 0;

        connection.SendAsync<ExpireDelegationTokenRequest, ExpireDelegationTokenResponse>(
                Arg.Any<ExpireDelegationTokenRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref calls) == 1
                ? throw new IOException("response lost")
                : ValueTask.FromResult(new ExpireDelegationTokenResponse { ErrorCode = ErrorCode.DelegationTokenNotFound }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.ExpireDelegationTokenAsync([1, 2, 3], TimeSpan.FromHours(1)));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.DelegationTokenNotFound);
    }

    [Test]
    public async Task AlterUserScramCredentialsAsync_DeletionReplayFindsNoCredential_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdmin(ApiKey.AlterUserScramCredentials);
        var calls = 0;

        connection.SendAsync<AlterUserScramCredentialsRequest, AlterUserScramCredentialsResponse>(
                Arg.Any<AlterUserScramCredentialsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref calls) == 1
                ? throw new IOException("response lost")
                : ValueTask.FromResult(new AlterUserScramCredentialsResponse
                {
                    Results =
                    [
                        new AlterUserScramCredentialsResult { User = "alice", ErrorCode = ErrorCode.ResourceNotFound }
                    ]
                }));

        await admin.AlterUserScramCredentialsAsync(
        [
            new UserScramCredentialDeletion { User = "alice", Mechanism = ScramMechanism.ScramSha256 }
        ]);

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task AlterUserScramCredentialsAsync_ReplayForUserWithUpsertion_StillThrows()
    {
        var (admin, connection) = CreateAdmin(ApiKey.AlterUserScramCredentials);
        var calls = 0;

        connection.SendAsync<AlterUserScramCredentialsRequest, AlterUserScramCredentialsResponse>(
                Arg.Any<AlterUserScramCredentialsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref calls) == 1
                ? throw new IOException("response lost")
                : ValueTask.FromResult(new AlterUserScramCredentialsResponse
                {
                    Results =
                    [
                        new AlterUserScramCredentialsResult { User = "alice", ErrorCode = ErrorCode.ResourceNotFound }
                    ]
                }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.AlterUserScramCredentialsAsync(
            [
                new UserScramCredentialDeletion { User = "alice", Mechanism = ScramMechanism.ScramSha256 },
                new UserScramCredentialUpsertion
                {
                    User = "alice",
                    Mechanism = ScramMechanism.ScramSha512,
                    Iterations = 4096,
                    Password = "secret"
                }
            ]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.ResourceNotFound);
    }

    [Test]
    public async Task DeleteAclsAsync_LostResponse_ReplaysAndReturnsTheReplayResult()
    {
        var (admin, connection) = CreateAdmin(ApiKey.DeleteAcls);
        var calls = 0;

        connection.SendAsync<DeleteAclsRequest, DeleteAclsResponse>(
                Arg.Any<DeleteAclsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref calls) == 1
                ? throw new IOException("response lost")
                : ValueTask.FromResult(new DeleteAclsResponse
                {
                    FilterResults = [new DeleteAclsFilterResult { MatchingAcls = [] }]
                }));

        var deleted = await admin.DeleteAclsAsync([new AclBindingFilter()]);

        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(deleted).IsEmpty();
    }

    [Test]
    public async Task WithDefaultApiTimeout_BelowOneMillisecond_Throws()
    {
        await Assert.That(() => new AdminClientBuilder().WithDefaultApiTimeout(TimeSpan.Zero))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new AdminClientOptions { DefaultApiTimeoutMs = 0 })
            .Throws<ArgumentOutOfRangeException>();
    }

    private static ValueTask AddVoterAsync(AdminClient admin) => admin.AddRaftVoterAsync(
        voterId: 2,
        voterDirectoryId: Guid.NewGuid(),
        endpoints:
        [
            new RaftVoterEndpoint
            {
                Name = "CONTROLLER",
                Host = "localhost",
                Port = 9093
            }
        ]);

    private static (AdminClient Admin, IKafkaConnection Connection) CreateAdmin(
        ApiKey apiKey,
        int defaultApiTimeoutMs = 30_000) =>
        AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(
            new AdminClientOptions
            {
                BootstrapServers = ["localhost:9092"],
                RetryBackoffMs = 1,
                RetryBackoffMaxMs = 5,
                DefaultApiTimeoutMs = defaultApiTimeoutMs
            },
            apiKey);

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
