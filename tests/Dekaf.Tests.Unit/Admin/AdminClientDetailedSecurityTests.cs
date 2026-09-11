using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientDetailedSecurityTests
{
    [Test]
    public async Task Acls_PreserveDuplicateOccurrencesAndRetryPositions()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var binding = Binding("same");
        var calls = 0;
        connection.SendAsync<CreateAclsRequest, CreateAclsResponse>(Arg.Any<CreateAclsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<CreateAclsRequest>();
                if (++calls == 1)
                    return ValueTask.FromResult(new CreateAclsResponse { Results = [new(), new() { ErrorCode = ErrorCode.NotController }, new() { ErrorCode = ErrorCode.ClusterAuthorizationFailed, ErrorMessage = "denied" }] });
                if (request.Creations.Count != 1 || request.Creations[0].ResourceName != "same")
                    throw new InvalidOperationException("Replayed a completed ACL occurrence.");
                return ValueTask.FromResult(new CreateAclsResponse { Results = [new()] });
            });
        var results = await admin.CreateAclsDetailedAsync([binding, binding, Binding("bad")]);
        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results[0].Binding).IsSameReferenceAs(binding);
        await Assert.That(results[1].Binding).IsSameReferenceAs(binding);
        await Assert.That(results[0].Result.IsSuccess && results[1].Result.IsSuccess).IsTrue();
        await Assert.That(results[2].Result.ErrorCode).IsEqualTo(ErrorCode.ClusterAuthorizationFailed);
        await Assert.That(results[2].Result.ErrorMessage).IsEqualTo("denied");
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(3)]
    public async Task Acls_WrongResponseCountCannotConfirmIdentity(int responseCount)
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        connection.SendAsync<CreateAclsRequest, CreateAclsResponse>(Arg.Any<CreateAclsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new CreateAclsResponse { Results = Enumerable.Range(0, responseCount).Select(_ => new AclCreationResult()).ToArray() }));
        var results = await admin.CreateAclsDetailedAsync([Binding("a"), Binding("b")]);
        await Assert.That(results.All(item => item.Result.Outcome == AdminMutationOutcome.Unknown)).IsTrue();
    }

    [Test]
    public async Task Scram_RetryKeepsEveryMechanismForOneUserAndReusesDerivedCredential()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var salt = new byte[32];
        byte[]? sentSalt = null;
        byte[]? sentPassword = null;
        var calls = 0;
        connection.SendAsync<AlterUserScramCredentialsRequest, AlterUserScramCredentialsResponse>(Arg.Any<AlterUserScramCredentialsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<AlterUserScramCredentialsRequest>();
                var upsert = request.Upsertions!.Single();
                if (++calls == 1)
                {
                    sentSalt = upsert.Salt;
                    sentPassword = upsert.SaltedPassword;
                    salt[0] = 99;
                    return ValueTask.FromResult(new AlterUserScramCredentialsResponse { Results = [new() { User = "done" }, new() { User = "retry", ErrorCode = ErrorCode.NotController }] });
                }
                if (request.Deletions!.Count != 1 || request.Deletions[0].Name != "retry"
                    || upsert.Salt[0] != 0 || !ReferenceEquals(sentSalt, upsert.Salt) || !ReferenceEquals(sentPassword, upsert.SaltedPassword))
                    throw new InvalidOperationException("Retry changed user atomicity or credential snapshot.");
                return ValueTask.FromResult(new AlterUserScramCredentialsResponse { Results = [new() { User = "retry" }] });
            });
        var results = await admin.AlterUserScramCredentialsDetailedAsync([
            Delete("done"), Delete("retry"), new UserScramCredentialUpsertion
            { User = "retry", Mechanism = ScramMechanism.ScramSha512, Iterations = 4096, Password = "fixture-password", Salt = salt }]);
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results.Values.All(item => item.IsSuccess)).IsTrue();
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task Scram_MapsByUserAndMarksMissingOrDuplicateUnknown()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        connection.SendAsync<AlterUserScramCredentialsRequest, AlterUserScramCredentialsResponse>(Arg.Any<AlterUserScramCredentialsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new AlterUserScramCredentialsResponse { Results = [
                new() { User = "bad", ErrorCode = ErrorCode.ClusterAuthorizationFailed, ErrorMessage = "original" },
                new() { User = "duplicate" }, new() { User = "duplicate" }, new() { User = "done" }, new() { User = "extra" }] }));
        var results = await admin.AlterUserScramCredentialsDetailedAsync([Delete("done"), Delete("bad"), Delete("duplicate"), Delete("missing")]);
        await Assert.That(results.Count).IsEqualTo(4);
        await Assert.That(results["done"].IsSuccess).IsTrue();
        await Assert.That(results["bad"].ErrorMessage).IsEqualTo("original");
        await Assert.That(results["bad"].ErrorCode).IsEqualTo(ErrorCode.ClusterAuthorizationFailed);
        await Assert.That(results["duplicate"].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
        await Assert.That(results["missing"].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AmbiguousSend_DoesNotReplay(bool scram)
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var failure = new IOException("response lost");
        var calls = 0;
        Configure(connection, scram, () => { calls++; throw failure; });
        var results = await Invoke(admin, scram);
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(results.Single().Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
        await Assert.That(results.Single().Exception).IsSameReferenceAs(failure);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CancellationDuringSend_IsUnknown(bool scram)
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        using var cancellation = new CancellationTokenSource();
        Configure(connection, scram, () => { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); });
        var results = await Invoke(admin, scram, token: cancellation.Token);
        await Assert.That(results.Single().Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ZeroDeadline_IsNotAttempted(bool scram)
    {
        var (admin, _) = CreateAdmin();
        await using var client = admin;
        var results = await Invoke(admin, scram, timeout: 0);
        await Assert.That(results.Single().Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(results.Single().Exception).IsTypeOf<KafkaTimeoutException>();
    }

    [Test]
    public async Task EmptyInvalidAndPreCancelledInput_NeverDispatches()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        await Assert.That(await admin.CreateAclsDetailedAsync([])).IsEmpty();
        await Assert.That(await admin.AlterUserScramCredentialsDetailedAsync([])).IsEmpty();
        await Assert.ThrowsAsync<ArgumentNullException>(() => admin.CreateAclsDetailedAsync(null!).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => admin.CreateAclsDetailedAsync([AclBinding.Allow(ResourcePattern.Topic("a"), "User:fixture", AclOperation.Any)]).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => admin.AlterUserScramCredentialsDetailedAsync([Delete("same"), Delete("same")]).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => admin.AlterUserScramCredentialsDetailedAsync([new UserScramCredentialDeletion { User = "a", Mechanism = ScramMechanism.Unknown }]).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => admin.AlterUserScramCredentialsDetailedAsync([new UserScramCredentialUpsertion
            { User = "a", Mechanism = ScramMechanism.ScramSha256, Password = "never-in-error", Iterations = 1 }]).AsTask());
        await Assert.ThrowsAsync<OperationCanceledException>(() => admin.CreateAclsDetailedAsync([Binding("a")], cancellationToken: new(true)).AsTask());
        await Assert.ThrowsAsync<OperationCanceledException>(() => admin.AlterUserScramCredentialsDetailedAsync([Delete("a")], cancellationToken: new(true)).AsTask());
        await Assert.That(connection.ReceivedCalls()).IsEmpty();
    }

    [Test]
    public async Task CapabilityExtension_RejectsUnsupportedClient()
    {
        IAdminClient client = Substitute.For<IAdminClient>();
        await Assert.ThrowsAsync<NotSupportedException>(() => client.CreateAclsDetailedAsync([]).AsTask());
        await Assert.ThrowsAsync<NotSupportedException>(() => client.AlterUserScramCredentialsDetailedAsync([]).AsTask());
    }

    internal static AclBinding Binding(string name) => AclBinding.Allow(ResourcePattern.Topic(name), "User:fixture", AclOperation.Read);
    internal static UserScramCredentialDeletion Delete(string user) => new() { User = user, Mechanism = ScramMechanism.ScramSha256 };
    private static (AdminClient, IKafkaConnection) CreateAdmin() =>
        AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(ApiKey.CreateAcls, ApiKey.AlterUserScramCredentials);

    private static async Task<AdminMutationResult[]> Invoke(AdminClient admin, bool scram, int timeout = 30000, CancellationToken token = default) => scram
        ? (await admin.AlterUserScramCredentialsDetailedAsync([Delete("a")], new() { TimeoutMs = timeout }, token)).Values.ToArray()
        : (await admin.CreateAclsDetailedAsync([Binding("a")], new() { TimeoutMs = timeout }, token)).Select(item => item.Result).ToArray();

    private static void Configure(IKafkaConnection connection, bool scram, Action onSend)
    {
        if (scram)
            connection.SendAsync<AlterUserScramCredentialsRequest, AlterUserScramCredentialsResponse>(Arg.Any<AlterUserScramCredentialsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(_ => { onSend(); return ValueTask.FromResult(new AlterUserScramCredentialsResponse { Results = [new() { User = "a" }] }); });
        else
            connection.SendAsync<CreateAclsRequest, CreateAclsResponse>(Arg.Any<CreateAclsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(_ => { onSend(); return ValueTask.FromResult(new CreateAclsResponse { Results = [new()] }); });
    }
}
