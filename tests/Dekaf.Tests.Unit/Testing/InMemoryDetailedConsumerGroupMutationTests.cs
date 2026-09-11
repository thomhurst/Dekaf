using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryDetailedConsumerGroupMutationTests
{
    [Test]
    public async Task ActiveGroup_RejectsDeletionAndAlterationButOnlySubscribedTopicOffsets()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("subscribed");
        cluster.CreateTopic("other");
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterConsumerGroupOffsetsDetailedAsync("group", [new("subscribed", 0, 1), new("other", 0, 2)]);
        cluster.RegisterConsumerGroupMember("group", "member", [new("subscribed", 0)], out _);
        var groups = await admin.DeleteConsumerGroupsDetailedAsync(["group"]);
        await Assert.That(groups["group"].ErrorCode).IsEqualTo(ErrorCode.NonEmptyGroup);
        var altered = await admin.AlterConsumerGroupOffsetsDetailedAsync("group", [new("other", 0, 3)]);
        await Assert.That(altered[new("other", 0)].ErrorCode).IsEqualTo(ErrorCode.UnknownMemberId);
        var deleted = await admin.DeleteConsumerGroupOffsetsDetailedAsync("group", [new("subscribed", 0), new("other", 0)]);
        await Assert.That(deleted[new("subscribed", 0)].ErrorCode).IsEqualTo(ErrorCode.GroupSubscribedToTopic);
        await Assert.That(deleted[new("other", 0)].IsSuccess).IsTrue();
    }

    [Test]
    public async Task GroupAndOffsetMutations_PreserveMixedOutcomesAndStoredState()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic", 2);
        await using var admin = new InMemoryAdminClient(cluster);
        IAdminClient client = admin;
        var good = new TopicPartition("topic", 0);
        var bad = new TopicPartition("missing", 0);
        var altered = await client.AlterConsumerGroupOffsetsDetailedAsync("group", [new("topic", 0, 12) { Metadata = "kept" }, new("missing", 0, 24)]);
        await Assert.That(altered[good].IsSuccess).IsTrue();
        await Assert.That(altered[bad].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        var stored = await client.ListConsumerGroupOffsetsAsync("group");
        await Assert.That(stored[good]).IsEqualTo(12);
        var deleted = await client.DeleteConsumerGroupOffsetsDetailedAsync("group", [good, bad]);
        await Assert.That(deleted[good].IsSuccess).IsTrue();
        await Assert.That(deleted[bad].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(await client.ListConsumerGroupOffsetsAsync("group")).IsEmpty();
        var groups = await client.DeleteConsumerGroupsDetailedAsync(["group", "missing"]);
        await Assert.That(groups["group"].IsSuccess).IsTrue();
        await Assert.That(groups["missing"].ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        var missing = await client.DeleteConsumerGroupOffsetsDetailedAsync("group", [good]);
        await Assert.That(missing[good].ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Faults_AreScopedByGroupAndRetainOriginalErrors(bool delete)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic", 2);
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterConsumerGroupOffsetsDetailedAsync("group", [new("topic", 0, 12), new("topic", 1, 24)]);
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin, topic: "topic", partition: 1, groupId: "group"),
            new KafkaException(ErrorCode.GroupAuthorizationFailed, "original denial"));
        var results = delete
            ? await admin.DeleteConsumerGroupOffsetsDetailedAsync("group", [new("topic", 0), new("topic", 1)])
            : await admin.AlterConsumerGroupOffsetsDetailedAsync("group", [new("topic", 0, 13), new("topic", 1, 25)]);
        await Assert.That(results[new("topic", 0)].IsSuccess).IsTrue();
        await Assert.That(results[new("topic", 1)].ErrorMessage).IsEqualTo("original denial");
        await Assert.That(results[new("topic", 1)].ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That((await admin.ListConsumerGroupOffsetsAsync("group"))[new("topic", 1)]).IsEqualTo(24);
    }

    [Test]
    public async Task Cancellation_DoesNotEraseSuccessOrLastCoordinatorRejection()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic", 3);
        await using var admin = new InMemoryAdminClient(cluster);
        var scope = new KafkaFaultScope(KafkaFaultOperation.Admin, topic: "topic", partition: 1, groupId: "group");
        cluster.FaultPlan.Fail(scope, new KafkaException(ErrorCode.NotCoordinator, "moved"));
        var barrier = cluster.FaultPlan.PauseNext(scope);
        using var cancellation = new CancellationTokenSource();
        var pending = admin.AlterConsumerGroupOffsetsDetailedAsync("group", [new("topic", 0, 1), new("topic", 1, 2), new("topic", 2, 3)],
            cancellationToken: cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        var results = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        barrier.Release();
        await Assert.That(results[new("topic", 0)].IsSuccess).IsTrue();
        await Assert.That(results[new("topic", 1)].Outcome).IsEqualTo(AdminMutationOutcome.Failed);
        await Assert.That(results[new("topic", 1)].ErrorCode).IsEqualTo(ErrorCode.NotCoordinator);
        await Assert.That(results[new("topic", 2)].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That((await admin.ListConsumerGroupOffsetsAsync("group")).Count).IsEqualTo(1);
    }

    [Test]
    public async Task EmptyTimeoutAndValidation_DoNotConsumeFaults()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin, groupId: "group"), new KafkaException(ErrorCode.GroupAuthorizationFailed, "retained fault"));
        await Assert.That((await admin.DeleteConsumerGroupsDetailedAsync([])).Count).IsEqualTo(0);
        await Assert.That((await admin.AlterConsumerGroupOffsetsDetailedAsync("group", [])).Count).IsEqualTo(0);
        await Assert.That((await admin.DeleteConsumerGroupOffsetsDetailedAsync("group", [])).Count).IsEqualTo(0);
        await Assert.ThrowsAsync<ArgumentException>(() => admin.DeleteConsumerGroupsDetailedAsync(["same", "same"]).AsTask());
        await Assert.ThrowsAsync<OperationCanceledException>(() => admin.DeleteConsumerGroupsDetailedAsync(["group"], cancellationToken: new(true)).AsTask());
        var timedOut = await admin.DeleteConsumerGroupsDetailedAsync(["group"], new() { TimeoutMs = 0 });
        await Assert.That(timedOut["group"].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(timedOut["group"].Exception).IsTypeOf<KafkaTimeoutException>();
        var result = await admin.DeleteConsumerGroupsDetailedAsync(["group"]);
        await Assert.That(result["group"].ErrorMessage).IsEqualTo("retained fault");
    }

    [Test]
    public async Task GroupDeletion_RetriesConfirmedCoordinatorErrorsButNotAmbiguousFailures()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic");
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterConsumerGroupOffsetsDetailedAsync("retry", [new("topic", 0, 1)]);
        await admin.AlterConsumerGroupOffsetsDetailedAsync("unknown", [new("topic", 0, 2)]);
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin, groupId: "retry"), new KafkaException(ErrorCode.CoordinatorLoadInProgress, "loading"));
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin, groupId: "unknown"), new IOException("lost response"));
        var results = await admin.DeleteConsumerGroupsDetailedAsync(["retry", "unknown"]);
        await Assert.That(results["retry"].IsSuccess).IsTrue();
        await Assert.That(results["unknown"].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
        await Assert.That((await admin.ListConsumerGroupOffsetsAsync("unknown")).Count).IsEqualTo(1);
    }
}
