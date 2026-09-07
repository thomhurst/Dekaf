using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryConsumerGroupOffsetQueryTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task StaleMetadata_AbortsTransactionAndReleasesStableQuery(bool prepared, bool recoverWithReplacement)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterConsumerGroupOffsetsAsync("group", [new("orders", 0, 7)]);
        await using var consumer = new InMemoryConsumer<string, string>(cluster,
            new InMemoryConsumerOptions { GroupId = "group" });
        consumer.Subscribe("orders");
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await using var transaction = producer.BeginTransaction();
        await transaction.SendOffsetsToTransactionAsync([new("orders", 0, 19)], consumer.ConsumerGroupMetadata!);
        var state = prepared ? await transaction.PrepareAsync() : default;
        await using var recoveryProducer = new InMemoryProducer<string, string>(cluster);
        await recoveryProducer.InitTransactionsAsync(keepPreparedTransaction: true);
        var completionProducer = recoverWithReplacement ? recoveryProducer : producer;
        await using var replacement = new InMemoryConsumer<string, string>(cluster,
            new InMemoryConsumerOptions { GroupId = "group" });
        replacement.Subscribe("orders");
        var pending = admin.ListConsumerGroupOffsetsAsync(Query("group"), new() { RequireStable = true });
        await Assert.That(pending.IsCompleted).IsFalse();
        Exception? publicationFailure = null;
        producer.TransactionCompletionPublishedTestHook = () =>
        {
            try
            {
                completionProducer.BeginTransaction();
            }
            catch (Exception exception)
            {
                publicationFailure = exception;
            }
        };

        var failure = await Assert.ThrowsAsync<FatalTransactionException>(() => prepared
            ? completionProducer.CompletePreparedTransactionAsync(state, true).AsTask()
            : transaction.CommitAsync().AsTask());

        await Assert.That(failure!.ErrorCode).IsEqualTo(ErrorCode.IllegalGeneration);
        await Assert.That(publicationFailure).IsSameReferenceAs(failure);
        await Assert.That(cluster.TryGetStableGroupOffsetDetails("group", null, out _, out _)).IsTrue();
        await Assert.That((await pending)["group"].Offsets[new("orders", 0)].Offset!.Value.Offset).IsEqualTo(7);
        await Assert.That(() => completionProducer.BeginTransaction()).Throws<FatalTransactionException>();
    }

    [Test]
    public async Task StreamsQuery_DeletedTopicHidesStoredCheckpoint()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterConsumerGroupOffsetsAsync("group", [new("orders", 0, 17, 9) { Metadata = "old" }]);
        cluster.DeleteTopic("orders");
        var results = await admin.ListStreamsGroupOffsetsAsync(
            new Dictionary<string, ListStreamsGroupOffsetsSpec> { ["group"] = new() });
        var offset = results["group"].Offsets[new("orders", 0)];
        await Assert.That(offset.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicId);
        await Assert.That(offset.Offset).IsEqualTo(-1);
        await Assert.That(offset.LeaderEpoch).IsEqualTo(-1);
        await Assert.That(offset.Metadata).IsNull();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task RequireStable_WaitsForCommitOrAbort(bool commit)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", partitionCount: 2);
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterConsumerGroupOffsetsAsync("group", [new("orders", 0, 7, 2) { Metadata = "old" }]);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await using var transaction = producer.BeginTransaction();
        await transaction.SendOffsetsToTransactionAsync([new("orders", 0, 19, 5) { Metadata = "new" }], "group");
        var partition = new TopicPartition("orders", 0);
        var specs = Query("group");
        var pending = admin.ListConsumerGroupOffsetsAsync(specs, new() { RequireStable = true });
        await Assert.That(pending.IsCompleted).IsFalse();
        var unstable = await admin.ListConsumerGroupOffsetsAsync(specs);
        await Assert.That(unstable["group"].Offsets[partition].Offset!.Value.Offset).IsEqualTo(7);
        var unrelated = admin.ListConsumerGroupOffsetsAsync(Query("group", [new("orders", 1)]), new() { RequireStable = true });
        await Assert.That(unrelated.IsCompletedSuccessfully).IsTrue();
        await unrelated;
        var empty = admin.ListConsumerGroupOffsetsAsync(Query("group", []), new() { RequireStable = true });
        await Assert.That(empty.IsCompletedSuccessfully).IsTrue();
        await empty;

        if (commit)
            await transaction.CommitAsync();
        else
            await transaction.AbortAsync();
        var result = (await pending)["group"].Offsets[partition].Offset!.Value;
        await Assert.That(result.Offset).IsEqualTo(commit ? 19 : 7);
        await Assert.That(result.LeaderEpoch).IsEqualTo(commit ? 5 : 2);
        await Assert.That(result.Metadata).IsEqualTo(commit ? "new" : "old");
    }

    [Test]
    public async Task OverlappingTransactionsAndDuplicateStages_RemainUnstableUntilAllComplete()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var admin = new InMemoryAdminClient(cluster);
        await using var firstProducer = new InMemoryProducer<string, string>(cluster);
        await using var secondProducer = new InMemoryProducer<string, string>(cluster);
        await using var first = firstProducer.BeginTransaction();
        await using var second = secondProducer.BeginTransaction();
        await first.SendOffsetsToTransactionAsync([new("orders", 0, 10)], "group");
        await first.SendOffsetsToTransactionAsync([new("orders", 0, 11)], "group");
        await second.SendOffsetsToTransactionAsync([new("orders", 0, 20)], "group");
        var partition = new TopicPartition("orders", 0);
        var pending = admin.ListConsumerGroupOffsetsAsync(Query("group", [partition]), new() { RequireStable = true });
        await Assert.That(pending.IsCompleted).IsFalse();
        await first.CommitAsync();
        await Assert.That(cluster.TryGetStableGroupOffsetDetails("group", [partition], out _, out var changed)).IsFalse();
        await Assert.That(changed!.IsCompleted).IsFalse();
        await Assert.That(pending.IsCompleted).IsFalse();
        await second.AbortAsync();
        await Assert.That((await pending)["group"].Offsets[partition].Offset!.Value.Offset).IsEqualTo(11);
        await Assert.That(cluster.TryGetStableGroupOffsetDetails("group", null, out _, out _)).IsTrue();
    }

    [Test]
    public async Task MultipleGroups_RetainReadySnapshotAndRequestOrder()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterConsumerGroupOffsetsAsync("ready", [new("orders", 0, 7)]);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await using var transaction = producer.BeginTransaction();
        await transaction.SendOffsetsToTransactionAsync([new("orders", 0, 19)], "waiting");
        var requests = Query("waiting");
        requests.Add("ready", new());
        var pending = admin.ListConsumerGroupOffsetsAsync(requests, new() { RequireStable = true });
        await Assert.That(pending.IsCompleted).IsFalse();
        await admin.AlterConsumerGroupOffsetsAsync("ready", [new("orders", 0, 99)]);
        await transaction.CommitAsync();
        var results = await pending;
        await Assert.That(results.Keys.ToArray()).IsEquivalentTo(["waiting", "ready"]);
        await Assert.That(results.Keys.First()).IsEqualTo("waiting");
        await Assert.That(results["ready"].Offsets[new("orders", 0)].Offset!.Value.Offset).IsEqualTo(7);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task StabilityWait_UsesOperationTimeoutAndCallerCancellation(bool timeout)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var admin = new InMemoryAdminClient(cluster);
        using var caller = new CancellationTokenSource();
        CancellationTokenSource? timeoutSource = null;
        admin.ConfigureTimeoutSourceTestHook = source => timeoutSource = source;
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await using var transaction = producer.BeginTransaction();
        await transaction.SendOffsetsToTransactionAsync([new("orders", 0, 19)], "group");
        var pending = admin.ListConsumerGroupOffsetsAsync(Query("group"), new() { RequireStable = true, TimeoutMs = 1234 }, caller.Token).AsTask();
        await Assert.That(pending.IsCompleted).IsFalse();
        if (timeout)
        {
            timeoutSource!.Cancel();
            var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(() => pending);
            await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);
            await Assert.That(exception.Configured).IsEqualTo(TimeSpan.FromMilliseconds(1234));
        }
        else
        {
            caller.Cancel();
            var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
            await Assert.That(exception!.CancellationToken.IsCancellationRequested).IsTrue();
        }
        await transaction.AbortAsync();
        var next = admin.ListConsumerGroupOffsetsAsync(Query("group"), new() { RequireStable = true });
        await Assert.That(next.IsCompletedSuccessfully).IsTrue();
        await next;
    }

    [Test]
    public async Task RichQuery_ReportsUnknownPartitionsAndReturnsIndependentSnapshots()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterConsumerGroupOffsetsAsync("group", [new("orders", 0, 7)]);
        var partition = new TopicPartition("orders", 0);
        var unknown = new TopicPartition("orders", 1);
        var snapshot = await admin.ListConsumerGroupOffsetsAsync(Query("group", [partition, unknown]));
        await admin.AlterConsumerGroupOffsetsAsync("group", [new("orders", 0, 99)]);
        await Assert.That(snapshot["group"].Offsets[partition].Offset!.Value.Offset).IsEqualTo(7);
        await Assert.That(snapshot["group"].Offsets[unknown].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(snapshot["group"].Offsets[unknown].Offset).IsNull();
    }

    private static Dictionary<string, ListConsumerGroupOffsetsSpec> Query(string groupId, TopicPartition[]? partitions = null)
        => new() { [groupId] = new() { TopicPartitions = partitions } };

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task PreparedRecovery_KeepsOffsetsUnstableAcrossProducerDisposal(bool commit)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var admin = new InMemoryAdminClient(cluster);
        await using var original = new InMemoryProducer<string, string>(cluster);
        await using var transaction = original.BeginTransaction();
        await transaction.SendOffsetsToTransactionAsync([new("orders", 0, 19)], "group");
        var prepared = await transaction.PrepareAsync();
        await original.DisposeAsync();
        var pending = admin.ListConsumerGroupOffsetsAsync(Query("group", [new("orders", 0)]), new() { RequireStable = true });
        await Assert.That(pending.IsCompleted).IsFalse();
        await using var replacement = new InMemoryProducer<string, string>(cluster);
        await replacement.InitTransactionsAsync(keepPreparedTransaction: true);
        await replacement.CompletePreparedTransactionAsync(prepared, committed: commit);
        var checkpoint = (await pending)["group"].Offsets[new("orders", 0)].Offset;
        await Assert.That(checkpoint?.Offset).IsEqualTo(commit ? (long?)19 : null);
    }

    [Test]
    public async Task Validation_RejectsMalformedQueriesAndHonorsEmptyQueryLifecycle()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        await Assert.ThrowsAsync<ArgumentException>(() => admin.ListConsumerGroupOffsetsAsync(Query(" ")).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => admin.ListConsumerGroupOffsetsAsync(new Dictionary<string, ListConsumerGroupOffsetsSpec> { ["group"] = null! }).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => admin.ListConsumerGroupOffsetsAsync(Query("group", [new("orders", 0), new("orders", 0)])).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => admin.ListConsumerGroupOffsetsAsync(Query("group"), new() { TimeoutMs = -1 }).AsTask());
        await Assert.That(await admin.ListConsumerGroupOffsetsAsync(new Dictionary<string, ListConsumerGroupOffsetsSpec>())).IsEmpty();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => admin.ListConsumerGroupOffsetsAsync(new Dictionary<string, ListConsumerGroupOffsetsSpec>(), cancellationToken: cancelled.Token).AsTask());
        await admin.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => admin.ListConsumerGroupOffsetsAsync(new Dictionary<string, ListConsumerGroupOffsetsSpec>()).AsTask());
    }

    [Test]
    public async Task DependencyInjection_RichQueriesPreserveCheckpointAndAbsentOffsets()
    {
        var services = new ServiceCollection();
        services.AddDekafInMemory();
        await using var provider = services.BuildServiceProvider();
        var cluster = provider.GetRequiredService<InMemoryKafkaCluster>();
        cluster.CreateTopic("orders", partitionCount: 2);
        var admin = provider.GetRequiredService<IAdminClient>();
        var partition = new TopicPartition("orders", 0);
        var absent = new TopicPartition("orders", 1);
        await admin.AlterConsumerGroupOffsetsAsync("group",
            [new TopicPartitionOffset("orders", 0, 17, 9) { Metadata = "checkpoint" }]);

        var results = await admin.ListConsumerGroupOffsetsAsync(new Dictionary<string, ListConsumerGroupOffsetsSpec>
        {
            ["group"] = new() { TopicPartitions = [partition, absent] },
            ["missing-group"] = new() { TopicPartitions = [partition] },
            ["empty-selection"] = new() { TopicPartitions = [] }
        });

        var checkpoint = results["group"].Offsets[partition].Offset;
        await Assert.That(checkpoint).IsNotNull();
        await Assert.That(checkpoint!.Value.Offset).IsEqualTo(17);
        await Assert.That(checkpoint.Value.LeaderEpoch).IsEqualTo(9);
        await Assert.That(checkpoint.Value.Metadata).IsEqualTo("checkpoint");
        await Assert.That(results["group"].Offsets[absent].Offset).IsNull();
        await Assert.That(results["group"].Offsets[absent].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results["missing-group"].Offsets[partition].Offset).IsNull();
        await Assert.That(results["empty-selection"].Offsets).IsEmpty();
    }

    [Test]
    public async Task ExistingStreamsQuery_RequireStableWaitsForTransactionalOffsets()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var admin = new InMemoryAdminClient(cluster);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await using var transaction = producer.BeginTransaction();
        var partition = new TopicPartition("orders", 0);
        await transaction.SendOffsetsToTransactionAsync([new TopicPartitionOffset("orders", 0, 19, 5)], "group");
        var pending = admin.ListStreamsGroupOffsetsAsync(
            new Dictionary<string, ListStreamsGroupOffsetsSpec> { ["group"] = new() { TopicPartitions = [partition] } },
            new ListStreamsGroupOffsetsOptions { RequireStable = true });

        await Assert.That(pending.IsCompleted).IsFalse();
        await transaction.CommitAsync();
        var result = await pending;
        await Assert.That(result["group"].Offsets[partition].Offset).IsEqualTo(19);
        await Assert.That(result["group"].Offsets[partition].LeaderEpoch).IsEqualTo(5);
    }
}
