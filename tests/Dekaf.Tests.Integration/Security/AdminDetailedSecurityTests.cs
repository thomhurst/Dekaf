using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;

namespace Dekaf.Tests.Integration.Security;

[Category("Authorization")]
[NotInParallel("AclKafka")]
[ClassDataSource<AclKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class AdminDetailedSecurityTests(AclKafkaContainer kafka)
{
    [Test]
    [Arguments(AclOperation.CreateTokens, false)]
    [Arguments(AclOperation.CreateTokens, true)]
    [Arguments(AclOperation.DescribeTokens, false)]
    [Arguments(AclOperation.DescribeTokens, true)]
    [Arguments(AclOperation.TwoPhaseCommit, false)]
    [Arguments(AclOperation.TwoPhaseCommit, true)]
    public async Task Acls_Kafka4OperationsRespectBrokerSupport(AclOperation operation, bool detailed)
    {
        await using var admin = kafka.CreateAdminClient();
        var name = $"acl-kafka4-{Guid.NewGuid():N}";
        var pattern = operation == AclOperation.TwoPhaseCommit
            ? ResourcePattern.TransactionalId(name) : ResourcePattern.User(name);
        var binding = AclBinding.Allow(pattern, "User:svc-gateway", operation);
        var filter = AclBindingFilter.ForResource(pattern.Type, name);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            if (detailed)
            {
                var results = await admin.CreateAclsDetailedAsync([binding], cancellationToken: timeout.Token);
                if (operation == AclOperation.TwoPhaseCommit)
                {
                    // This fixture pins Kafka 4.0.2, which predates TWO_PHASE_COMMIT.
                    await Assert.That(results.Single().Result.ErrorCode).IsEqualTo(ErrorCode.InvalidRequest);
                    return;
                }
                await Assert.That(results.Single().Result.IsSuccess).IsTrue();
            }
            else
            {
                if (operation == AclOperation.TwoPhaseCommit)
                {
                    var error = await Assert.ThrowsAsync<KafkaException>(() =>
                        admin.CreateAclsAsync([binding], cancellationToken: timeout.Token).AsTask());
                    await Assert.That(error!.ErrorCode).IsEqualTo(ErrorCode.InvalidRequest);
                    return;
                }
                await admin.CreateAclsAsync([binding], cancellationToken: timeout.Token);
            }

            // Creation is committed before every broker's authorizer has applied the ACL.
            IReadOnlyList<AclBinding> described;
            do
            {
                described = await admin.DescribeAclsAsync(filter, cancellationToken: timeout.Token);
                if (described.Count == 0)
                    await Task.Delay(100, timeout.Token);
            } while (described.Count == 0);
            await AssertBindingAsync(described.Single(), binding);

            var deleted = await admin.DeleteAclsAsync([filter], cancellationToken: timeout.Token);
            await AssertBindingAsync(deleted.Single(), binding);
        }
        finally
        {
            await admin.DeleteAclsAsync([filter]);
        }
    }

    private static async Task AssertBindingAsync(AclBinding actual, AclBinding expected)
    {
        await Assert.That(actual.Pattern.Type).IsEqualTo(expected.Pattern.Type);
        await Assert.That(actual.Pattern.Name).IsEqualTo(expected.Pattern.Name);
        await Assert.That(actual.Pattern.PatternType).IsEqualTo(expected.Pattern.PatternType);
        await Assert.That(actual.Entry.Principal).IsEqualTo(expected.Entry.Principal);
        await Assert.That(actual.Entry.Host).IsEqualTo(expected.Entry.Host);
        await Assert.That(actual.Entry.Operation).IsEqualTo(expected.Entry.Operation);
        await Assert.That(actual.Entry.Permission).IsEqualTo(expected.Entry.Permission);
    }

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
