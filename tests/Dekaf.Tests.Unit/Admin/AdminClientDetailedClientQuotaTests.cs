using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientDetailedClientQuotaTests
{
    private static readonly ClientQuotaEntity Default = ClientQuotaEntity.For(
        ClientQuotaEntityComponent.User("alice"), ClientQuotaEntityComponent.ClientId(null));
    private static readonly ClientQuotaEntity Named = ClientQuotaEntity.For(
        ClientQuotaEntityComponent.User("alice"), ClientQuotaEntityComponent.ClientId(""));

    [Test]
    public async Task MixedOutcomes_MatchCompleteIdentityRegardlessOfComponentOrder()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        Setup(connection, request => new()
        {
            Entries = [Entry(request.Entries[1], ErrorCode.ClusterAuthorizationFailed, "original denial"),
                new() { Entity = request.Entries[0].Entity.Reverse().ToArray(), ErrorCode = ErrorCode.None }]
        });
        var results = await admin.AlterClientQuotasDetailedAsync(Alterations());
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results[Default].IsSuccess).IsTrue();
        await Assert.That(results[Named].Outcome).IsEqualTo(AdminMutationOutcome.Failed);
        await Assert.That(results[Named].ErrorCode).IsEqualTo(ErrorCode.ClusterAuthorizationFailed);
        await Assert.That(results[Named].ErrorMessage).IsEqualTo("original denial");
    }

    [Test]
    [Arguments(ErrorCode.NotController)]
    [Arguments(ErrorCode.ThrottlingQuotaExceeded)]
    public async Task ExplicitRejection_RetriesOnlyRejectedEntity(ErrorCode rejection)
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var calls = 0;
        Setup(connection, request =>
        {
            if (++calls == 1) return new() { Entries = [Entry(request.Entries[0]), Entry(request.Entries[1], rejection)] };
            if (request.Entries.Count != 1 || request.Entries[0].Entity[1].EntityName != "")
                throw new InvalidOperationException("Confirmed success was replayed.");
            return new() { Entries = [Entry(request.Entries[0])] };
        });
        var results = await admin.AlterClientQuotasDetailedAsync(Alterations());
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(results.Values.All(static result => result.IsSuccess)).IsTrue();
    }

    [Test]
    [Arguments("io")]
    [Arguments("malformed")]
    [Arguments("timeout")]
    [Arguments("cancel")]
    public async Task FailureAfterDispatch_PreservesSuccessAndDoesNotReplay(string failure)
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        Setup(connection, request =>
        {
            if (++calls == 1) return new() { Entries = [Entry(request.Entries[0]), Entry(request.Entries[1], ErrorCode.NotController)] };
            if (failure == "timeout") return new() { Entries = [Entry(request.Entries[0], ErrorCode.RequestTimedOut, "uncertain")] };
            if (failure == "malformed") throw new MalformedProtocolDataException("malformed frame");
            if (failure == "cancel") { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); }
            throw new IOException("lost response");
        });
        var results = await admin.AlterClientQuotasDetailedAsync(Alterations(), cancellationToken: cancellation.Token);
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(results[Default].IsSuccess).IsTrue();
        await Assert.That(results[Named].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
        await Assert.That(results[Named].ErrorCode).IsEqualTo(failure == "timeout" ? ErrorCode.RequestTimedOut : (ErrorCode?)null);
    }

    [Test]
    public async Task MissingDuplicateAndUnrequestedResponses_DoNotInferSuccess()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        Setup(connection, request => new() { Entries = [Entry(request.Entries[0]), Entry(request.Entries[0]),
            new() { Entity = [new() { EntityType = "user", EntityName = "unrequested" }] }] });
        var results = await admin.AlterClientQuotasDetailedAsync(Alterations());
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results.Values.All(static result => result.Outcome == AdminMutationOutcome.Unknown)).IsTrue();
    }

    [Test]
    public async Task ValidateOnlyAndInputSnapshot_SurviveCallerMutationAndRetries()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        ClientQuotaEntityComponent[] components = [ClientQuotaEntityComponent.User(null)];
        ClientQuotaOperation[] operations = [ClientQuotaOperation.Set("consumer_byte_rate", 2048)];
        var calls = 0;
        Setup(connection, request =>
        {
            if (!request.ValidateOnly || request.Entries[0].Entity[0].EntityName is not null || request.Entries[0].Ops[0].Value != 2048)
                throw new InvalidOperationException("Snapshot or validate-only flag changed.");
            components[0] = ClientQuotaEntityComponent.User("changed");
            operations[0] = ClientQuotaOperation.RemoveValue("consumer_byte_rate");
            return new() { Entries = [Entry(request.Entries[0], ++calls == 1 ? ErrorCode.NotController : ErrorCode.None)] };
        });
        var result = await admin.AlterClientQuotasDetailedAsync(
            [new() { Entity = new() { Components = components }, Operations = operations }], new() { ValidateOnly = true });
        await Assert.That(result[ClientQuotaEntity.ForUser(null)].IsSuccess).IsTrue();
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(result.Keys.Single().Components is IList<ClientQuotaEntityComponent> { IsReadOnly: true }).IsTrue();
    }

    [Test]
    public async Task ZeroDeadline_IsNotAttemptedWithoutNetworkActivity()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var results = await admin.AlterClientQuotasDetailedAsync(Alterations(), new() { TimeoutMs = 0 });
        await Assert.That(results.Values.All(static result => result.Outcome == AdminMutationOutcome.NotAttempted
            && result.Exception is KafkaTimeoutException)).IsTrue();
        await Assert.That(connection.ReceivedCalls()).IsEmpty();
    }

    [Test]
    public async Task CancellationAfterResponse_PreservesConfirmedErrors()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        using var cancellation = new CancellationTokenSource();
        Setup(connection, request =>
        {
            cancellation.Cancel();
            return new() { Entries = [Entry(request.Entries[0]), Entry(request.Entries[1], ErrorCode.NotController, "moved")] };
        });
        var results = await admin.AlterClientQuotasDetailedAsync(Alterations(), cancellationToken: cancellation.Token);
        await Assert.That(results[Default].IsSuccess).IsTrue();
        await Assert.That(results[Named].ErrorCode).IsEqualTo(ErrorCode.NotController);
        await Assert.That(results[Named].ErrorMessage).IsEqualTo("moved");
    }

    [Test]
    public async Task BrokerBootstrap_DoesNotRequireKnownController()
    {
        var (admin, connection) = CreateAdmin(knownController: false);
        await using var client = admin;
        Setup(connection, request => new() { Entries = request.Entries.Select(entry => Entry(entry)).ToArray() });
        var results = await admin.AlterClientQuotasDetailedAsync(Alterations());
        await Assert.That(results.Values.All(static result => result.IsSuccess)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Deadline_CancelsBlockedDiscoveryOrSend(bool dispatched)
    {
        var (admin, connection) = CreateAdmin(initializeMetadata: dispatched);
        await using var client = admin;
        if (dispatched)
            connection.SendAsync<AlterClientQuotasRequest, AlterClientQuotasResponse>(Arg.Any<AlterClientQuotasRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => new ValueTask<AlterClientQuotasResponse>(WaitForCancellation<AlterClientQuotasResponse>(call.Arg<CancellationToken>())));
        else
            connection.SendAsync<MetadataRequest, MetadataResponse>(Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => new ValueTask<MetadataResponse>(WaitForCancellation<MetadataResponse>(call.Arg<CancellationToken>())));
        var results = await admin.AlterClientQuotasDetailedAsync(Alterations(), new() { TimeoutMs = 1000 }).AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        foreach (var result in results.Values)
        {
            await Assert.That(result.Outcome).IsEqualTo(dispatched ? AdminMutationOutcome.Unknown : AdminMutationOutcome.NotAttempted);
            await Assert.That(result.Exception).IsTypeOf<KafkaTimeoutException>();
        }
    }

    private static async Task<T> WaitForCancellation<T>(CancellationToken token)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, token);
        throw new InvalidOperationException("Cancellation did not stop the blocked request.");
    }

    [Test]
    public async Task EmptyInvalidAndCancelledInput_DoNotInitialize()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        await Assert.That((await admin.AlterClientQuotasDetailedAsync([])).Count).IsEqualTo(0);
        await Assert.ThrowsAsync<ArgumentNullException>(() => admin.AlterClientQuotasDetailedAsync(null!).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => admin.AlterClientQuotasDetailedAsync([], new() { TimeoutMs = -1 }).AsTask());
        await Assert.ThrowsAsync<OperationCanceledException>(() => admin.AlterClientQuotasDetailedAsync(Alterations(), cancellationToken: new(true)).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => admin.AlterClientQuotasDetailedAsync(
            [ClientQuotaAlteration.Set(Default, "a", 1), ClientQuotaAlteration.Set(ClientQuotaEntity.For(Default.Components.Reverse().ToArray()), "b", 2)]).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => admin.AlterClientQuotasDetailedAsync(
            [ClientQuotaAlteration.Set(ClientQuotaEntity.For(ClientQuotaEntityComponent.User(null), ClientQuotaEntityComponent.User("a")), "a", 1)]).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => admin.AlterClientQuotasDetailedAsync(
            [new() { Entity = Default, Operations = [ClientQuotaOperation.Set("same", 1), ClientQuotaOperation.RemoveValue("same")] }]).AsTask());
        await Assert.That(connection.ReceivedCalls()).IsEmpty();
        IAdminClient unsupported = Substitute.For<IAdminClient>();
        await Assert.ThrowsAsync<NotSupportedException>(() => unsupported.AlterClientQuotasDetailedAsync([]).AsTask());
    }

    private static ClientQuotaAlteration[] Alterations() =>
        [ClientQuotaAlteration.Set(Default, "consumer_byte_rate", 1024), ClientQuotaAlteration.Remove(Named, "producer_byte_rate")];

    private static (AdminClient, IKafkaConnection) CreateAdmin(bool initializeMetadata = true, bool knownController = true)
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.BrokerId.Returns(1);
        connection.Host.Returns("localhost");
        connection.Port.Returns(9092);
        connection.IsConnected.Returns(true);
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(connection));
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(connection));
        var metadata = new MetadataResponse
        {
            Brokers = [new() { NodeId = 1, Host = "localhost", Port = 9092 }],
            ControllerId = knownController ? 1 : -1, Topics = []
        };
        var manager = new MetadataManager(pool, ["localhost:9092"]);
        if (initializeMetadata) manager.Metadata.Update(metadata);
        manager.SetApiVersion(ApiKey.Metadata, 9, 13);
        manager.SetApiVersion(ApiKey.AlterClientQuotas, 0, 1);
        connection.SendAsync<MetadataRequest, MetadataResponse>(Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(metadata));
        connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(Arg.Any<ApiVersionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys = [new(ApiKey.Metadata, 9, 13), new(ApiKey.AlterClientQuotas, 0, 1)]
            }));
        return (new AdminClient(new AdminClientOptions
        {
            BootstrapServers = ["localhost:9092"], RetryBackoffMs = 1, RetryBackoffMaxMs = 1
        }, pool, manager), connection);
    }

    private static AlterClientQuotasResponseEntry Entry(AlterClientQuotasRequestEntry entry, ErrorCode code = ErrorCode.None, string? message = null) =>
        new() { Entity = entry.Entity, ErrorCode = code, ErrorMessage = message };

    private static void Setup(IKafkaConnection connection, Func<AlterClientQuotasRequest, AlterClientQuotasResponse> response) =>
        connection.SendAsync<AlterClientQuotasRequest, AlterClientQuotasResponse>(Arg.Any<AlterClientQuotasRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult(response(call.Arg<AlterClientQuotasRequest>())));
}
