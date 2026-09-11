using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryDetailedClientQuotaTests
{
    [Test]
    public async Task ValidateSetRemove_PreserveDefaultAndCompoundEntityIdentity()
    {
        await using var admin = new InMemoryAdminClient(new InMemoryKafkaCluster());
        IAdminClient client = admin;
        var entity = ClientQuotaEntity.For(ClientQuotaEntityComponent.User("alice"), ClientQuotaEntityComponent.ClientId(null));
        var equivalent = ClientQuotaEntity.For(ClientQuotaEntityComponent.ClientId(null), ClientQuotaEntityComponent.User("alice"));
        ClientQuotaAlteration[] alterations = [ClientQuotaAlteration.Set(entity, "consumer_byte_rate", 4096)];
        await Assert.That((await client.AlterClientQuotasDetailedAsync(alterations, new() { ValidateOnly = true }))[equivalent].IsSuccess).IsTrue();
        await Assert.That((await client.DescribeClientQuotasAsync(ClientQuotaFilter.All())).Count).IsEqualTo(0);
        await Assert.That((await client.AlterClientQuotasDetailedAsync(alterations))[equivalent].IsSuccess).IsTrue();
        await Assert.That((await client.DescribeClientQuotasAsync(ClientQuotaFilter.All()))[equivalent]["consumer_byte_rate"]).IsEqualTo(4096);
        await client.AlterClientQuotasDetailedAsync([ClientQuotaAlteration.Remove(equivalent, "consumer_byte_rate")]);
        await Assert.That((await client.DescribeClientQuotasAsync(ClientQuotaFilter.All())).Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(ErrorCode.ClusterAuthorizationFailed, false)]
    [Arguments(ErrorCode.NotController, true)]
    [Arguments(ErrorCode.ThrottlingQuotaExceeded, true)]
    public async Task Faults_PreserveOtherEntitiesAndRetryExplicitRejections(ErrorCode error, bool succeeds)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var first = ClientQuotaEntity.ForUser(null);
        var second = ClientQuotaEntity.ForUser("named");
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin), new KafkaException(error, "original rejection"));
        var result = await admin.AlterClientQuotasDetailedAsync(
            [ClientQuotaAlteration.Set(first, "consumer_byte_rate", 1024), ClientQuotaAlteration.Set(second, "consumer_byte_rate", 2048)]);
        await Assert.That(result[first].IsSuccess).IsEqualTo(succeeds);
        if (!succeeds)
        {
            await Assert.That(result[first].ErrorCode).IsEqualTo(error);
            await Assert.That(result[first].ErrorMessage).IsEqualTo("original rejection");
        }
        await Assert.That(result[second].IsSuccess).IsTrue();
        await Assert.That((await admin.DescribeClientQuotasAsync(ClientQuotaFilter.All())).Count).IsEqualTo(succeeds ? 2 : 1);
    }

    [Test]
    public async Task CancellationBeforeApply_DoesNotMutateOrAcceptPausedEntities()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var barrier = cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.Admin));
        using var cancellation = new CancellationTokenSource();
        var pending = admin.AlterClientQuotasDetailedAsync(
            [ClientQuotaAlteration.Set(ClientQuotaEntity.ForUser(null), "consumer_byte_rate", 1024)], cancellationToken: cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        barrier.Release();
        await Assert.That(result.Values.Single().Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That((await admin.DescribeClientQuotasAsync(ClientQuotaFilter.All())).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Snapshot_PreservesInputAndDictionaryKeysAcrossPausedExecution()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var barrier = cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.Admin));
        ClientQuotaEntityComponent[] components = [ClientQuotaEntityComponent.User(null)];
        ClientQuotaOperation[] operations = [ClientQuotaOperation.Set("consumer_byte_rate", 1024)];
        var pending = admin.AlterClientQuotasDetailedAsync([new() { Entity = new() { Components = components }, Operations = operations }]).AsTask();
        await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        components[0] = ClientQuotaEntityComponent.User("changed");
        operations[0] = ClientQuotaOperation.RemoveValue("consumer_byte_rate");
        barrier.Release();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(result[ClientQuotaEntity.ForUser(null)].IsSuccess).IsTrue();
        await Assert.That((await admin.DescribeClientQuotasAsync(ClientQuotaFilter.All()))[ClientQuotaEntity.ForUser(null)]["consumer_byte_rate"]).IsEqualTo(1024);
    }

    [Test]
    public async Task EmptyInput_DoesNotConsumeFaultAndDeadlineDoesNotApply()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin), new IOException("lost response"));
        await Assert.That((await admin.AlterClientQuotasDetailedAsync([])).Count).IsEqualTo(0);
        var entity = ClientQuotaEntity.ForUser(null);
        var request = new[] { ClientQuotaAlteration.Set(entity, "consumer_byte_rate", 1024) };
        await Assert.That((await admin.AlterClientQuotasDetailedAsync(request))[entity].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
        var timedOut = await admin.AlterClientQuotasDetailedAsync(request, new() { TimeoutMs = 0 });
        await Assert.That(timedOut[entity].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(timedOut[entity].Exception).IsTypeOf<KafkaTimeoutException>();
        await Assert.That((await admin.DescribeClientQuotasAsync(ClientQuotaFilter.All())).Count).IsEqualTo(0);
    }
}
