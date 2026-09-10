using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryShareGroupOffsetQueryTests
{
    [Test]
    [Arguments(0)]
    [Arguments(30000)]
    public async Task Query_EmptyInputCompletesWithoutDeadlineWork(int timeoutMs)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        admin.ConfigureTimeoutSourceTestHook = _ => throw new InvalidOperationException("Empty query created a timer.");
        var empty = new Dictionary<string, ListShareGroupOffsetsSpec>();
        var query = admin.ListShareGroupOffsetsAsync(empty, new() { TimeoutMs = timeoutMs });
        await Assert.That(query.IsCompletedSuccessfully).IsTrue();
        await Assert.That(await query).IsEmpty();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(empty, cancellationToken: cancellation.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(empty, new() { TimeoutMs = -1 }))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Query_ZeroDeadlineDoesNotConsumeFault()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        admin.ConfigureTimeoutSourceTestHook = source => source.CancelAfter(Timeout.Infinite);
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin, groupId: "group"),
            new InvalidOperationException("must remain queued"));
        var specs = new Dictionary<string, ListShareGroupOffsetsSpec> { ["group"] = new() };
        await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(specs, new() { TimeoutMs = 0 }))
            .Throws<KafkaTimeoutException>();
        await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(specs)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Query_PreservesGroupPartitionAndEmptySelectionOutcomes()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("input");
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterShareGroupOffsetsAsync("all", [new() { TopicPartition = new("input", 0), StartOffset = 42 }]);
        await admin.AlterShareGroupOffsetsAsync("selected", [new() { TopicPartition = new("input", 0), StartOffset = 43 }]);
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin, groupId: "denied"),
            new GroupException(ErrorCode.GroupAuthorizationFailed, "denied"));
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin, "input", 0, "selected"),
            new KafkaException(ErrorCode.TopicAuthorizationFailed, "partition denied"));
        IAdminClient client = admin;
        var results = await client.ListShareGroupOffsetsAsync(new Dictionary<string, ListShareGroupOffsetsSpec>
        {
            ["all"] = new(), ["selected"] = new() { TopicPartitions = [new("input", 0), new("unknown", 0)] },
            ["none"] = new() { TopicPartitions = [] }, ["missing"] = new(), ["denied"] = new()
        });
        await Assert.That(results["all"].Offsets[new("input", 0)].StartOffset).IsEqualTo(42);
        await Assert.That(results["selected"].Offsets[new("input", 0)].ErrorCode).IsEqualTo(ErrorCode.TopicAuthorizationFailed);
        await Assert.That(results["selected"].Offsets[new("unknown", 0)].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(results["none"].Offsets).IsEmpty();
        await Assert.That(results["none"].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results["missing"].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results["missing"].Offsets).IsEmpty();
        await Assert.That(results["denied"].ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Query_CancellationAndDeadlineInterruptFaultBarrier(bool timeout)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        using var caller = new CancellationTokenSource();
        CancellationTokenSource? deadline = null;
        admin.ConfigureTimeoutSourceTestHook = source => deadline = source;
        var barrier = cluster.FaultPlan.PauseNext(new KafkaFaultScope(KafkaFaultOperation.Admin, groupId: "group"));
        var pending = admin.ListShareGroupOffsetsAsync(new Dictionary<string, ListShareGroupOffsetsSpec> { ["group"] = new() },
            new() { TimeoutMs = 1234 }, caller.Token).AsTask();
        await barrier.WaitUntilEnteredAsync();
        if (timeout)
        {
            deadline!.Cancel();
            await Assert.That(async () => await pending).Throws<KafkaTimeoutException>();
        }
        else
        {
            caller.Cancel();
            await Assert.That(async () => await pending).Throws<OperationCanceledException>();
        }
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Query_CancellationWinsWhenFaultIsConsumed(bool partitionFault, bool timeout)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("input");
        await using var admin = new InMemoryAdminClient(cluster);
        using var caller = new CancellationTokenSource();
        CancellationTokenSource? deadline = null;
        admin.ConfigureTimeoutSourceTestHook = source => deadline = source;
        var scope = partitionFault
            ? new KafkaFaultScope(KafkaFaultOperation.Admin, "input", 0, "group")
            : new KafkaFaultScope(KafkaFaultOperation.Admin, groupId: "group");
        cluster.FaultPlan.Fail(scope, new KafkaException(ErrorCode.GroupAuthorizationFailed, "scripted failure"));
        cluster.FaultPlan.FaultConsumed += _ =>
        {
            if (timeout)
                deadline!.Cancel();
            else
                caller.Cancel();
        };
        var specs = new Dictionary<string, ListShareGroupOffsetsSpec>
        {
            ["group"] = new() { TopicPartitions = [new("input", 0)] }
        };

        if (timeout)
            await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(specs, cancellationToken: caller.Token))
                .Throws<KafkaTimeoutException>();
        else
            await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(specs, cancellationToken: caller.Token))
                .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task Query_RejectsDuplicatePartitionsBeforeConsumingFault()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin, groupId: "group"),
            new GroupException(ErrorCode.GroupAuthorizationFailed, "queued"));
        await Assert.That(async () => await admin.ListShareGroupOffsetsAsync(new Dictionary<string, ListShareGroupOffsetsSpec>
            { ["group"] = new() { TopicPartitions = [new("input", 0), new("input", 0)] } })).Throws<ArgumentException>();
        var results = await admin.ListShareGroupOffsetsAsync(new Dictionary<string, ListShareGroupOffsetsSpec> { ["group"] = new() });
        await Assert.That(results["group"].ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
    }
}
