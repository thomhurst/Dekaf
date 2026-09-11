using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;
using Dekaf.Tests.Unit.Admin;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryDetailedSecurityTests
{
    [Test]
    public async Task Acls_UseTopicAndGroupFaultScopesAndKeepDuplicates()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin, topic: "denied"), new KafkaException(ErrorCode.TopicAuthorizationFailed, "topic denial"));
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin, groupId: "denied"), new KafkaException(ErrorCode.GroupAuthorizationFailed, "group denial"));
        var binding = AdminClientDetailedSecurityTests.Binding("same");
        var results = await admin.CreateAclsDetailedAsync([binding, AdminClientDetailedSecurityTests.Binding("denied"),
            AclBinding.Allow(ResourcePattern.Group("denied"), "User:fixture", AclOperation.Read), binding]);
        await Assert.That(results.Count).IsEqualTo(4);
        await Assert.That(results[0].Result.IsSuccess && results[3].Result.IsSuccess).IsTrue();
        await Assert.That(results[1].Result.ErrorMessage).IsEqualTo("topic denial");
        await Assert.That(results[2].Result.ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
    }

    [Test]
    public async Task Scram_AppliesFaultOncePerUser()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin), new KafkaException(ErrorCode.ClusterAuthorizationFailed, "user rejected"));
        var results = await admin.AlterUserScramCredentialsDetailedAsync([
            AdminClientDetailedSecurityTests.Delete("bad"), new UserScramCredentialDeletion { User = "bad", Mechanism = ScramMechanism.ScramSha512 },
            AdminClientDetailedSecurityTests.Delete("good")]);
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results["bad"].ErrorMessage).IsEqualTo("user rejected");
        await Assert.That(results["good"].IsSuccess).IsTrue();
    }

    [Test]
    public async Task Cancellation_RetainsSuccessAndDoesNotApplyRemainingAcls()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var barrier = cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.Admin, topic: "paused"));
        using var cancellation = new CancellationTokenSource();
        var pending = admin.CreateAclsDetailedAsync([AdminClientDetailedSecurityTests.Binding("done"),
            AdminClientDetailedSecurityTests.Binding("paused"), AdminClientDetailedSecurityTests.Binding("later")], cancellationToken: cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        var results = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        barrier.Release();
        await Assert.That(results[0].Result.IsSuccess).IsTrue();
        await Assert.That(results[1].Result.Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(results[2].Result.Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
    }

    [Test]
    public async Task AmbiguousFault_IsUnknownAndSafeRejectionCanRetry()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin), new IOException("response lost"));
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin), new KafkaException(ErrorCode.NotController, "moved"));
        var results = await admin.AlterUserScramCredentialsDetailedAsync([AdminClientDetailedSecurityTests.Delete("unknown"), AdminClientDetailedSecurityTests.Delete("retry")]);
        await Assert.That(results["unknown"].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
        await Assert.That(results["retry"].IsSuccess).IsTrue();
    }

    [Test]
    public async Task EmptyValidationAndDeadlineMatchProductionContract()
    {
        await using var admin = new InMemoryAdminClient(new InMemoryKafkaCluster());
        await Assert.That(await admin.CreateAclsDetailedAsync([])).IsEmpty();
        await Assert.That(await admin.AlterUserScramCredentialsDetailedAsync([])).IsEmpty();
        await Assert.ThrowsAsync<ArgumentException>(() => admin.AlterUserScramCredentialsDetailedAsync([
            AdminClientDetailedSecurityTests.Delete("duplicate"), AdminClientDetailedSecurityTests.Delete("duplicate")]).AsTask());
        var result = await admin.AlterUserScramCredentialsDetailedAsync([AdminClientDetailedSecurityTests.Delete("timeout")], new() { TimeoutMs = 0 });
        await Assert.That(result["timeout"].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(result["timeout"].Exception).IsTypeOf<KafkaTimeoutException>();
    }
}
