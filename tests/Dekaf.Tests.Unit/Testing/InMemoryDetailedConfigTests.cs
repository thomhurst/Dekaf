using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryDetailedConfigTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ResourceFaults_PreserveSiblingsAndSupportGroupScope(bool incremental)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var topic = ConfigResource.Topic("orders");
        var group = new ConfigResource { Type = ConfigResourceType.Group, Name = "workers" };
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin, groupId: group.Name), new KafkaException(ErrorCode.GroupAuthorizationFailed, "original denial"));
        var result = await Invoke(admin, incremental, topic, group);
        await Assert.That(result[topic].IsSuccess).IsTrue();
        await Assert.That(result[group].ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That(result[group].ErrorMessage).IsEqualTo("original denial");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Cancellation_PreservesEarlierSuccessAndLeavesPausedResourceNotAttempted(bool incremental)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var done = ConfigResource.Topic("done");
        var paused = new ConfigResource { Type = ConfigResourceType.Group, Name = "paused" };
        var barrier = cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.Admin, groupId: paused.Name));
        using var cancellation = new CancellationTokenSource();
        var input = new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>> { [done] = [], [paused] = [] };
        var pending = incremental ? admin.IncrementalAlterConfigsDetailedAsync(input, cancellationToken: cancellation.Token).AsTask()
            : admin.AlterConfigsDetailedAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>> { [done] = [], [paused] = [] }, cancellationToken: cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        var results = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        barrier.Release();
        await Assert.That(results[done].IsSuccess).IsTrue();
        await Assert.That(results[paused].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EmptyInput_DoesNotConsumeFaultAndExplicitRejectionCanRetry(bool incremental)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var resource = ConfigResource.Topic("orders");
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin, topic: resource.Name), new KafkaException(ErrorCode.NotController, "moved"));
        await Assert.That((await Invoke(admin, incremental)).Count).IsEqualTo(0);
        await Assert.That((await Invoke(admin, incremental, resource))[resource].IsSuccess).IsTrue();
        var timedOut = await admin.IncrementalAlterConfigsDetailedAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>> { [resource] = [] }, new() { TimeoutMs = 0 });
        await Assert.That(timedOut[resource].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(timedOut[resource].Exception).IsTypeOf<KafkaTimeoutException>();
    }

    private static ValueTask<IReadOnlyDictionary<ConfigResource, AdminMutationResult>> Invoke(IAdminClient admin, bool incremental, params ConfigResource[] resources) =>
        incremental
            ? admin.IncrementalAlterConfigsDetailedAsync(resources.ToDictionary(static resource => resource, static _ => (IReadOnlyList<ConfigAlter>)[]))
            : admin.AlterConfigsDetailedAsync(resources.ToDictionary(static resource => resource, static _ => (IReadOnlyList<ConfigEntry>)[]));
}
