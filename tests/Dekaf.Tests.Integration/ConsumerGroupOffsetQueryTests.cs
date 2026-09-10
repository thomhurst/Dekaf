using Dekaf.Admin;
using Dekaf.Errors;

namespace Dekaf.Tests.Integration;

[Category("Transaction")]
public sealed class ConsumerGroupOffsetQueryTests(KafkaTestContainer kafka) : TransactionalKafkaIntegrationTest(kafka)
{
    [Test]
    public async Task CompleteOffsets_RoundTripMetadataEpochAndAbsentCommit(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var group = $"offset-query-{Guid.NewGuid():N}";
        var emptyGroup = $"empty-{Guid.NewGuid():N}";
        await using var admin = Kafka.CreateAdminClient().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        await WaitForGroupCoordinatorAsync(admin, group, cancellationToken);
        await WaitForGroupCoordinatorAsync(admin, emptyGroup, cancellationToken);
        var checkpoint = new TopicPartitionOffset(topic, 0, 42, 0) { Metadata = "resume-checkpoint" };
        await admin.AlterConsumerGroupOffsetsAsync(group, [checkpoint], cancellationToken);
        var results = await admin.ListConsumerGroupOffsetsAsync(new Dictionary<string, ListConsumerGroupOffsetsSpec>
        {
            [group] = new() { TopicPartitions = [new(topic, 0), new(topic, 1)] },
            [emptyGroup] = new()
        }, cancellationToken: cancellationToken);
        await Assert.That(results[group].Offsets[new(topic, 0)].Offset).IsEqualTo(checkpoint);
        await Assert.That(results[group].Offsets[new(topic, 1)].Offset).IsNull();
        await Assert.That(results[group].Offsets[new(topic, 1)].ErrorCode).IsEqualTo(Protocol.ErrorCode.None);
        await Assert.That(results[emptyGroup].ErrorCode).IsEqualTo(Protocol.ErrorCode.None);
        await Assert.That(results[emptyGroup].Offsets).IsEmpty();
        var legacy = await admin.ListConsumerGroupOffsetsAsync(group, cancellationToken);
        await Assert.That(legacy.Count).IsEqualTo(1);
        await Assert.That(legacy[new(topic, 0)]).IsEqualTo(42);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task StableOffsets_WaitForPendingTransactionAndObserveCommitOrAbort(bool commit, CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var group = $"stable-offset-query-{Guid.NewGuid():N}";
        await using var admin = Kafka.CreateAdminClient().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        await WaitForGroupCoordinatorAsync(admin, group, cancellationToken);
        var before = new TopicPartitionOffset(topic, 0, 5, 0) { Metadata = "before" };
        var after = new TopicPartitionOffset(topic, 0, 9, 0) { Metadata = "transaction" };
        await admin.AlterConsumerGroupOffsetsAsync(group, [before], cancellationToken);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId($"offset-query-txn-{Guid.NewGuid():N}")
            .BuildAsync(cancellationToken);
        await producer.InitTransactionsAsync(cancellationToken);
        await using var transaction = producer.BeginTransaction();
        await transaction.SendOffsetsToTransactionAsync([after], group, cancellationToken);

        var specs = new Dictionary<string, ListConsumerGroupOffsetsSpec>
        {
            [group] = new() { TopicPartitions = [new(topic, 0)] }
        };
        var unstable = await admin.ListConsumerGroupOffsetsAsync(specs, cancellationToken: cancellationToken);
        await Assert.That(unstable[group].Offsets[new(topic, 0)].Offset).IsEqualTo(before);
        await Assert.That(async () => await admin.ListConsumerGroupOffsetsAsync(specs,
            new ListConsumerGroupOffsetsOptions { RequireStable = true, TimeoutMs = 250 }, cancellationToken))
            .Throws<KafkaTimeoutException>();

        var pending = admin.ListConsumerGroupOffsetsAsync(specs,
            new ListConsumerGroupOffsetsOptions { RequireStable = true }, cancellationToken).AsTask();
        if (commit)
            await transaction.CommitAsync(cancellationToken);
        else
            await transaction.AbortAsync(cancellationToken);
        var stable = await pending;
        await Assert.That(stable[group].Offsets[new(topic, 0)].Offset).IsEqualTo(commit ? after : before);
        await Assert.That(stable[group].Offsets[new(topic, 0)].ErrorCode).IsEqualTo(Protocol.ErrorCode.None);
    }

    private static async Task WaitForGroupCoordinatorAsync(
        IAdminClient admin, string group, CancellationToken cancellationToken)
    {
        // Topic creation only establishes topic readiness. A new group's offsets
        // partition can still be loading when these offset-query tests begin.
        // Wait using a read-only query; keep the tested mutations and stable-offset
        // assertions outside this setup loop so their failures remain visible.
        using var readiness = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readiness.CancelAfter(TimeSpan.FromSeconds(30));
        KafkaException? lastFailure = null;
        try
        {
            while (true)
            {
                try
                {
                    var groups = await admin.DescribeConsumerGroupsAsync([group], readiness.Token);
                    if (!groups.ContainsKey(group))
                        throw new InvalidOperationException($"Coordinator description omitted group '{group}'.");
                    return;
                }
                catch (GroupException exception) when (exception.ErrorCode == Protocol.ErrorCode.GroupIdNotFound)
                {
                    // A missing fresh group is expected once its coordinator is ready.
                    return;
                }
                catch (KafkaException exception) when (exception.IsRetriable && exception.ErrorCode is
                    Protocol.ErrorCode.CoordinatorNotAvailable or Protocol.ErrorCode.CoordinatorLoadInProgress or Protocol.ErrorCode.NotCoordinator)
                {
                    lastFailure = exception;
                }
                await Task.Delay(100, readiness.Token);
            }
        }
        catch (OperationCanceledException) when (readiness.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Group coordinator for '{group}' did not become ready within 30 seconds.", lastFailure);
        }
    }
}
