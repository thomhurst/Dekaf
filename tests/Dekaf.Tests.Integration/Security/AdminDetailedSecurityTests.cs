using Dekaf.Admin;
using Dekaf.Protocol;

namespace Dekaf.Tests.Integration.Security;

[Category("Authorization")]
[NotInParallel("AclKafka")]
[ClassDataSource<AclKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class AdminDetailedSecurityTests(AclKafkaContainer kafka)
{
    [Test]
    public async Task Acls_MixedOutcomesKeepBindingIdentity()
    {
        await using var admin = kafka.CreateAdminClient();
        var name = $"detailed-acl-{Guid.NewGuid():N}";
        var good = AclBinding.Allow(ResourcePattern.Topic(name), "User:detailed", AclOperation.Read);
        var invalid = AclBinding.Allow(ResourcePattern.Topic(name), "invalid-principal", AclOperation.Read);
        var results = await admin.CreateAclsDetailedAsync([good, invalid, good]);
        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results[0].Binding).IsSameReferenceAs(good);
        await Assert.That(results[0].Result.IsSuccess).IsTrue();
        await Assert.That(results[1].Result.Outcome).IsEqualTo(AdminMutationOutcome.Failed);
        await Assert.That(results[2].Result.IsSuccess).IsTrue();
        await admin.DeleteAclsAsync([new() { ResourceType = ResourceType.Topic, ResourceName = name }]);
    }

    [Test]
    public async Task Scram_MixedUsersPreservePerUserAtomicity()
    {
        await using var admin = kafka.CreateAdminClient();
        var user = $"scram-atomic-{Guid.NewGuid():N}";
        var good = $"scram-good-{Guid.NewGuid():N}";
        var initial = await admin.AlterUserScramCredentialsDetailedAsync([Upsert(user, 4096)]);
        await Assert.That(initial[user].IsSuccess).IsTrue();
        try
        {
            var results = await admin.AlterUserScramCredentialsDetailedAsync([
                Upsert(user, 8192), new UserScramCredentialDeletion { User = user, Mechanism = ScramMechanism.ScramSha512 }, Upsert(good, 4096)]);
            await Assert.That(results[user].ErrorCode).IsEqualTo(ErrorCode.ResourceNotFound);
            await Assert.That(results[good].IsSuccess).IsTrue();
            var described = await admin.DescribeUserScramCredentialsAsync([user, good]);
            await Assert.That(described[user].Single().Iterations).IsEqualTo(4096);
            await Assert.That(described[good].Single().Mechanism).IsEqualTo(ScramMechanism.ScramSha256);
        }
        finally
        {
            await admin.AlterUserScramCredentialsDetailedAsync([
                new UserScramCredentialDeletion { User = user, Mechanism = ScramMechanism.ScramSha256 },
                new UserScramCredentialDeletion { User = good, Mechanism = ScramMechanism.ScramSha256 }]);
        }
    }

    [Test]
    public async Task UnauthorizedCaller_GetsConfirmedFailures()
    {
        await using var admin = Kafka.CreateAdminClient().WithBootstrapServers(kafka.BootstrapServers)
            .WithSaslPlain(AclKafkaContainer.TestUsername, AclKafkaContainer.TestPassword).Build();
        var acl = await admin.CreateAclsDetailedAsync([AclBinding.Allow(ResourcePattern.Topic("detailed-denied"), "User:denied", AclOperation.Read)]);
        await Assert.That(acl.Single().Result.ErrorCode).IsEqualTo(ErrorCode.ClusterAuthorizationFailed);
        var scram = await admin.AlterUserScramCredentialsDetailedAsync([Upsert("detailed-denied", 4096)]);
        await Assert.That(scram["detailed-denied"].ErrorCode).IsEqualTo(ErrorCode.ClusterAuthorizationFailed);
    }

    private static UserScramCredentialUpsertion Upsert(string user, int iterations) => new()
    {
        User = user, Mechanism = ScramMechanism.ScramSha256, Iterations = iterations, Password = "integration-fixture-password"
    };
}
