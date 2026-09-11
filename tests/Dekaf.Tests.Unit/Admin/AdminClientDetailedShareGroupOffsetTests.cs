using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public class AdminClientDetailedShareGroupOffsetTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TopLevelGroupError_PreservesOriginalMessage(bool delete)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        Setup(connection, delete, _ => [], ErrorCode.GroupAuthorizationFailed, "original group denial");
        var results = await Invoke(admin, delete);
        foreach (var result in results.Values)
        {
            await Assert.That(result.Outcome).IsEqualTo(AdminMutationOutcome.Failed);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
            await Assert.That(result.ErrorMessage).IsEqualTo("original group denial");
        }
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task CancellationDuringSend_IsUnknownWithOriginalCause(bool delete, bool timeout)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (delete)
            connection.SendAsync<DeleteShareGroupOffsetsRequest, DeleteShareGroupOffsetsResponse>(Arg.Any<DeleteShareGroupOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => WaitForCancellation<DeleteShareGroupOffsetsResponse>(entered, call.Arg<CancellationToken>()));
        else
            connection.SendAsync<AlterShareGroupOffsetsRequest, AlterShareGroupOffsetsResponse>(Arg.Any<AlterShareGroupOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => WaitForCancellation<AlterShareGroupOffsetsResponse>(entered, call.Arg<CancellationToken>()));
        var pending = Invoke(admin, delete, new() { TimeoutMs = timeout ? 500 : 30000 }, cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (!timeout) cancellation.Cancel();
        var results = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        foreach (var result in results.Values)
        {
            await Assert.That(result.Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
            await Assert.That(result.ErrorCode).IsNull();
            if (timeout) await Assert.That(result.Exception).IsTypeOf<KafkaTimeoutException>();
            else await Assert.That(result.Exception is OperationCanceledException).IsTrue();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ValidationAndEmptyInput_DoNotSend(bool inMemory)
    {
        var (real, connection) = CreateAdmin();
        await using var ownedReal = real;
        await using var simulated = new Dekaf.Testing.InMemoryAdminClient(new Dekaf.Testing.InMemoryKafkaCluster());
        IAdminClient admin = inMemory ? simulated : real;
        await Assert.That(await admin.AlterShareGroupOffsetsDetailedAsync("group", [])).IsEmpty();
        await Assert.That(await admin.DeleteShareGroupOffsetsDetailedAsync("group", [])).IsEmpty();
        await Assert.ThrowsAsync<ArgumentException>(() => admin.DeleteShareGroupOffsetsDetailedAsync("group", ["same", "same"]).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => admin.AlterShareGroupOffsetsDetailedAsync("group",
            [new() { TopicPartition = new("same", 0), StartOffset = 1 }, new() { TopicPartition = new("same", 0), StartOffset = 2 }]).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => admin.AlterShareGroupOffsetsDetailedAsync("group", null!).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => admin.DeleteShareGroupOffsetsDetailedAsync("group", null!).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => admin.DeleteShareGroupOffsetsDetailedAsync(" ", []).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => admin.DeleteShareGroupOffsetsDetailedAsync("group", [], new() { TimeoutMs = -1 }).AsTask());
        await Assert.ThrowsAsync<OperationCanceledException>(() => admin.AlterShareGroupOffsetsDetailedAsync("group", [], cancellationToken: new(true)).AsTask());
        await Assert.ThrowsAsync<NotSupportedException>(() => Substitute.For<IAdminClient>().DeleteShareGroupOffsetsDetailedAsync("group", []).AsTask());
        await Assert.That(connection.ReceivedCalls()).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ZeroDeadline_ReportsNotAttemptedBeforeDiscovery(bool delete)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        var results = await Invoke(admin, delete, new() { TimeoutMs = 0 });
        await Assert.That(results.Values.All(static result => result.Outcome == AdminMutationOutcome.NotAttempted && result.Exception is KafkaTimeoutException)).IsTrue();
        await Assert.That(connection.ReceivedCalls()).IsEmpty();
    }

    [Test]
    public async Task Alter_SameTopicPartitionsKeepTheirIdentityAndExactRequestedOffsets()
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        var requestOffsets = new Dictionary<int, long>();
        connection.SendAsync<AlterShareGroupOffsetsRequest, AlterShareGroupOffsetsResponse>(Arg.Any<AlterShareGroupOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                foreach (var partition in call.Arg<AlterShareGroupOffsetsRequest>().Topics.Single().Partitions)
                    requestOffsets.Add(partition.PartitionIndex, partition.StartOffset);
                return ValueTask.FromResult(new AlterShareGroupOffsetsResponse { Responses =
                    [new() { TopicName = "orders", Partitions = [new() { PartitionIndex = 7, ErrorCode = ErrorCode.InvalidRequest, ErrorMessage = "invalid" }, new() { PartitionIndex = 3 }] }] });
            });
        var results = await admin.AlterShareGroupOffsetsDetailedAsync("group",
            [new() { TopicPartition = new("orders", 3), StartOffset = 42 }, new() { TopicPartition = new("orders", 7), StartOffset = 84 }]);
        await Assert.That(requestOffsets[3]).IsEqualTo(42);
        await Assert.That(requestOffsets[7]).IsEqualTo(84);
        await Assert.That(results[new("orders", 3)].IsSuccess).IsTrue();
        await Assert.That(results[new("orders", 7)].ErrorMessage).IsEqualTo("invalid");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task BrokerTimeout_IsUnknownAndNeverReplayed(bool delete)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        var calls = 0;
        Setup(connection, delete, _ => { calls++; return []; }, ErrorCode.RequestTimedOut, "broker timed out");
        var results = await Invoke(admin, delete);
        await Assert.That(calls).IsEqualTo(1);
        foreach (var result in results.Values)
        {
            await Assert.That(result.Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
            await Assert.That(result.ErrorMessage).IsEqualTo("broker timed out");
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UnsupportedDestination_ReportsNotAttempted(bool delete)
    {
        var (admin, connection) = CreateAdmin(supported: false);
        await using var owned = admin;
        var results = await Invoke(admin, delete);
        foreach (var result in results.Values)
        {
            await Assert.That(result.Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
            await Assert.That(result.Exception).IsTypeOf<BrokerVersionException>();
            await Assert.That(result.ErrorCode).IsNull();
        }
        await connection.DidNotReceive().SendAsync<AlterShareGroupOffsetsRequest, AlterShareGroupOffsetsResponse>(
            Arg.Any<AlterShareGroupOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
        await connection.DidNotReceive().SendAsync<DeleteShareGroupOffsetsRequest, DeleteShareGroupOffsetsResponse>(
            Arg.Any<DeleteShareGroupOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MixedOutcomes_PreserveEveryConfirmedResult(bool delete)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        Setup(connection, delete, _ => [("good", ErrorCode.None, null), ("bad", ErrorCode.TopicAuthorizationFailed, "denied")]);
        var results = await Invoke(admin, delete);
        await Assert.That(results["good"].IsSuccess).IsTrue();
        await Assert.That(results["bad"].Outcome).IsEqualTo(AdminMutationOutcome.Failed);
        await Assert.That(results["bad"].ErrorCode).IsEqualTo(ErrorCode.TopicAuthorizationFailed);
        await Assert.That(results["bad"].ErrorMessage).IsEqualTo("denied");
    }

    [Test]
    [Arguments(false, ErrorCode.NotCoordinator)]
    [Arguments(true, ErrorCode.NotCoordinator)]
    [Arguments(false, ErrorCode.CoordinatorNotAvailable)]
    [Arguments(true, ErrorCode.CoordinatorNotAvailable)]
    [Arguments(false, ErrorCode.CoordinatorLoadInProgress)]
    [Arguments(true, ErrorCode.CoordinatorLoadInProgress)]
    public async Task CoordinatorRejection_RetriesOnlyRejectedEntity(bool delete, ErrorCode rejection)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        var calls = 0;
        Setup(connection, delete, requested =>
        {
            if (++calls == 1) return [("good", ErrorCode.None, null), ("bad", rejection, "moved")];
            if (!requested.SequenceEqual(["bad"])) throw new InvalidOperationException("Replayed confirmed success.");
            return [("bad", ErrorCode.None, null)];
        });
        var results = await Invoke(admin, delete);
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(results.Values.All(static result => result.IsSuccess)).IsTrue();
        await connection.Received(2).SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MissingAndDuplicateResponses_AreUnknown(bool delete)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        Setup(connection, delete, _ => [("good", ErrorCode.None, null), ("good", ErrorCode.None, null), ("other", ErrorCode.None, null)]);
        var results = await Invoke(admin, delete);
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results.Values.All(static result => result.Outcome == AdminMutationOutcome.Unknown)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task LostResponse_IsUnknownAndNeverReplayed(bool delete)
    {
        var (admin, connection) = CreateAdmin();
        await using var owned = admin;
        var calls = 0;
        Setup(connection, delete, _ => { calls++; throw new IOException("lost response"); });
        var results = await Invoke(admin, delete);
        await Assert.That(calls).IsEqualTo(1);
        foreach (var result in results.Values)
        {
            await Assert.That(result.Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
            await Assert.That(result.ErrorCode).IsNull();
            await Assert.That(result.Exception).IsTypeOf<IOException>();
        }
    }

    private static (AdminClient Admin, IKafkaConnection Connection) CreateAdmin(bool supported = true)
    {
        var result = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(supported ? [ApiKey.AlterShareGroupOffsets, ApiKey.DeleteShareGroupOffsets] : []);
        result.Connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse { Coordinators = [new() { Key = "group", NodeId = 1, Host = "localhost", Port = 9092 }] }));
        return result;
    }

    private static async ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> Invoke(AdminClient admin, bool delete,
        ShareGroupOffsetMutationOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (delete) return await admin.DeleteShareGroupOffsetsDetailedAsync("group", ["good", "bad"], options, cancellationToken);
        var results = await admin.AlterShareGroupOffsetsDetailedAsync("group",
            [new() { TopicPartition = new("good", 0), StartOffset = 12 }, new() { TopicPartition = new("bad", 0), StartOffset = 24 }], options, cancellationToken);
        return results.ToDictionary(static pair => pair.Key.Topic, static pair => pair.Value);
    }

    private static async ValueTask<TResponse> WaitForCancellation<TResponse>(TaskCompletionSource entered, CancellationToken token)
    {
        entered.SetResult();
        await Task.Delay(Timeout.Infinite, token);
        throw new InvalidOperationException("Cancellation did not interrupt the simulated send.");
    }

    private static void Setup(IKafkaConnection connection, bool delete, Func<string[], (string Topic, ErrorCode Code, string? Message)[]> respond,
        ErrorCode groupError = ErrorCode.None, string? groupMessage = null)
    {
        if (delete)
            connection.SendAsync<DeleteShareGroupOffsetsRequest, DeleteShareGroupOffsetsResponse>(Arg.Any<DeleteShareGroupOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => ValueTask.FromResult(new DeleteShareGroupOffsetsResponse
                {
                    ErrorCode = groupError, ErrorMessage = groupMessage,
                    Responses = respond(call.Arg<DeleteShareGroupOffsetsRequest>().Topics.Select(static topic => topic.TopicName).ToArray())
                        .Select(static item => new DeleteShareGroupOffsetsResponseTopic { TopicName = item.Topic, ErrorCode = item.Code, ErrorMessage = item.Message }).ToArray()
                }));
        else
            connection.SendAsync<AlterShareGroupOffsetsRequest, AlterShareGroupOffsetsResponse>(Arg.Any<AlterShareGroupOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => ValueTask.FromResult(new AlterShareGroupOffsetsResponse
                {
                    ErrorCode = groupError, ErrorMessage = groupMessage,
                    Responses = respond(call.Arg<AlterShareGroupOffsetsRequest>().Topics.Select(static topic => topic.TopicName).ToArray())
                        .Select(static item => new AlterShareGroupOffsetsResponseTopic { TopicName = item.Topic, Partitions = [new() { PartitionIndex = 0, ErrorCode = item.Code, ErrorMessage = item.Message }] }).ToArray()
                }));
    }
}
