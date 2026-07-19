using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("Admin")]
[Category("Resilience")]
[NotInParallel("RackAwareKafkaContainer")]
[ClassDataSource<RackAwareKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class PartitionReassignmentIntegrationTests(RackAwareKafkaContainer kafka)
{
    private const int PartitionCount = 4;
    private const int MessagesPerPartition = 100;
    private const int InitialMessagesPerPartition = 20;
    private const int FinalMessagesPerPartition = 10;
    private static readonly int[] InitialReplicas = [1, 2];
    private static readonly int[] TargetReplicas = [3, 2];

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    [Timeout(240_000)]
    public async Task Producer_PartitionReassignment_ContinuesWithoutLossOrDuplicates(
        bool idempotent,
        CancellationToken cancellationToken)
    {
        var topic = await CreateTopicAsync(cancellationToken).ConfigureAwait(false);
        var brokerStopped = false;

        try
        {
            await kafka.StopBrokerAsync(nodeId: 3, cancellationToken).ConfigureAwait(false);
            brokerStopped = true;

            await using var admin = kafka.CreateAdminClient();
            await StartReassignmentAsync(admin, topic, cancellationToken).ConfigureAwait(false);
            await WaitForReassignmentAsync(admin, topic, cancellationToken).ConfigureAwait(false);

            await using var producer = await CreateProducerAsync(idempotent, cancellationToken)
                .ConfigureAwait(false);
            await ProduceRangeAsync(
                    producer,
                    topic,
                    start: 0,
                    count: InitialMessagesPerPartition,
                    cancellationToken)
                .ConfigureAwait(false);

            var productionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var resumeProduction = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var production = ProduceRangeAsync(
                producer,
                topic,
                start: InitialMessagesPerPartition,
                count: MessagesPerPartition - InitialMessagesPerPartition - FinalMessagesPerPartition,
                cancellationToken,
                productionStarted,
                resumeProduction.Task);

            await productionStarted.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            var brokerStartup = kafka.StartBrokerAsync(nodeId: 3, cancellationToken);
            resumeProduction.TrySetResult();

            await Task.WhenAll(production, brokerStartup).ConfigureAwait(false);
            brokerStopped = false;
            await WaitForAssignmentAsync(admin, topic, TargetReplicas, expectedLeader: 3, cancellationToken)
                .ConfigureAwait(false);
            await ProduceRangeAsync(
                    producer,
                    topic,
                    start: MessagesPerPartition - FinalMessagesPerPartition,
                    count: FinalMessagesPerPartition,
                    cancellationToken)
                .ConfigureAwait(false);
            await producer.FlushAsync(cancellationToken).ConfigureAwait(false);

            await AssertBrokerSequenceAsync(topic, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (brokerStopped)
                await kafka.StartBrokerAsync(nodeId: 3, CancellationToken.None).ConfigureAwait(false);
        }
    }

    [Test]
    [Timeout(240_000)]
    public async Task Consumer_PartitionReassignment_ContinuesWithoutSkippedOrDuplicateRecords(
        CancellationToken cancellationToken)
    {
        var topic = await CreateTopicAsync(cancellationToken).ConfigureAwait(false);
        var groupId = $"reassignment-consumer-{Guid.NewGuid():N}";
        var brokerStopped = false;

        try
        {
            await kafka.StopBrokerAsync(nodeId: 3, cancellationToken).ConfigureAwait(false);
            brokerStopped = true;

            await using var admin = kafka.CreateAdminClient();
            await using var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(kafka.BootstrapServers)
                .WithGroupId(groupId)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .WithAutoOffsetStore(false)
                .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                .SubscribeTo(topic)
                .BuildAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var producer = await CreateProducerAsync(idempotent: true, cancellationToken)
                .ConfigureAwait(false);

            var consumption = ConsumeAndCommitSequenceAsync(
                consumer,
                topic,
                MessagesPerPartition,
                cancellationToken);

            await StartReassignmentAsync(admin, topic, cancellationToken).ConfigureAwait(false);
            await WaitForReassignmentAsync(admin, topic, cancellationToken).ConfigureAwait(false);
            await ProduceRangeAsync(
                    producer,
                    topic,
                    start: 0,
                    count: InitialMessagesPerPartition,
                    cancellationToken)
                .ConfigureAwait(false);

            var productionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var resumeProduction = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var production = ProduceRangeAsync(
                producer,
                topic,
                start: InitialMessagesPerPartition,
                count: MessagesPerPartition - InitialMessagesPerPartition - FinalMessagesPerPartition,
                cancellationToken,
                productionStarted,
                resumeProduction.Task);

            await productionStarted.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            var brokerStartup = kafka.StartBrokerAsync(nodeId: 3, cancellationToken);
            resumeProduction.TrySetResult();

            await Task.WhenAll(production, brokerStartup).ConfigureAwait(false);
            brokerStopped = false;
            await WaitForAssignmentAsync(admin, topic, TargetReplicas, expectedLeader: 3, cancellationToken)
                .ConfigureAwait(false);
            await ProduceRangeAsync(
                    producer,
                    topic,
                    start: MessagesPerPartition - FinalMessagesPerPartition,
                    count: FinalMessagesPerPartition,
                    cancellationToken)
                .ConfigureAwait(false);
            await consumption.ConfigureAwait(false);

            var committedOffsets = await admin.ListConsumerGroupOffsetsAsync(groupId, cancellationToken)
                .ConfigureAwait(false);
            for (var partition = 0; partition < PartitionCount; partition++)
            {
                var topicPartition = new TopicPartition(topic, partition);
                var found = committedOffsets.TryGetValue(topicPartition, out var committed);
                if (!found || committed != MessagesPerPartition)
                {
                    throw new InvalidOperationException(
                        $"Committed offset mismatch for {topicPartition}: expected {MessagesPerPartition}, " +
                        $"actual {(found ? committed : -1)}.");
                }
            }
        }
        finally
        {
            if (brokerStopped)
                await kafka.StartBrokerAsync(nodeId: 3, CancellationToken.None).ConfigureAwait(false);
        }
    }

    [Test]
    [Timeout(180_000)]
    public async Task Producer_ReassignmentCancellation_RecoversCleanly(
        CancellationToken cancellationToken)
    {
        var topic = await CreateTopicAsync(cancellationToken).ConfigureAwait(false);
        var brokerStopped = false;

        try
        {
            await kafka.StopBrokerAsync(nodeId: 3, cancellationToken).ConfigureAwait(false);
            brokerStopped = true;

            await using var admin = kafka.CreateAdminClient();
            await StartReassignmentAsync(admin, topic, cancellationToken).ConfigureAwait(false);
            await WaitForReassignmentAsync(admin, topic, cancellationToken).ConfigureAwait(false);

            await using var producer = await CreateProducerAsync(idempotent: true, cancellationToken)
                .ConfigureAwait(false);
            await ProduceRangeAsync(
                    producer,
                    topic,
                    start: 0,
                    count: InitialMessagesPerPartition,
                    cancellationToken)
                .ConfigureAwait(false);

            await CancelReassignmentAsync(admin, topic, cancellationToken).ConfigureAwait(false);
            await WaitForAssignmentAsync(admin, topic, InitialReplicas, expectedLeader: null, cancellationToken)
                .ConfigureAwait(false);

            await ProduceRangeAsync(
                    producer,
                    topic,
                    start: InitialMessagesPerPartition,
                    count: MessagesPerPartition - InitialMessagesPerPartition,
                    cancellationToken)
                .ConfigureAwait(false);
            await producer.FlushAsync(cancellationToken).ConfigureAwait(false);

            await AssertBrokerSequenceAsync(topic, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (brokerStopped)
                await kafka.StartBrokerAsync(nodeId: 3, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<string> CreateTopicAsync(CancellationToken cancellationToken)
    {
        var topic = $"reassignment-{Guid.NewGuid():N}";
        await using var admin = kafka.CreateAdminClient();
        var assignments = new Dictionary<int, IReadOnlyList<int>>(PartitionCount);
        for (var partition = 0; partition < PartitionCount; partition++)
            assignments.Add(partition, InitialReplicas);

        await admin.CreateTopicsAsync(
        [
            new NewTopic
            {
                Name = topic,
                NumPartitions = -1,
                ReplicationFactor = -1,
                ReplicaAssignments = assignments,
                Configs = new Dictionary<string, string>
                {
                    ["min.insync.replicas"] = "1"
                }
            }
        ], cancellationToken: cancellationToken).ConfigureAwait(false);

        await WaitForAssignmentAsync(admin, topic, InitialReplicas, expectedLeader: 1, cancellationToken)
            .ConfigureAwait(false);
        return topic;
    }

    private async Task<IKafkaProducer<string, string>> CreateProducerAsync(
        bool idempotent,
        CancellationToken cancellationToken)
    {
        return await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"reassignment-producer-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithIdempotence(idempotent)
            .WithRequestTimeout(TimeSpan.FromSeconds(5))
            .WithDeliveryTimeout(TimeSpan.FromSeconds(45))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task ProduceRangeAsync(
        IKafkaProducer<string, string> producer,
        string topic,
        int start,
        int count,
        CancellationToken cancellationToken,
        TaskCompletionSource? productionStarted = null,
        Task? resumeProduction = null)
    {
        for (var sequence = start; sequence < start + count; sequence++)
        {
            for (var partition = 0; partition < PartitionCount; partition++)
            {
                var value = $"{partition}:{sequence}";
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Partition = partition,
                    Key = value,
                    Value = value
                }, cancellationToken).ConfigureAwait(false);

                if (sequence == start && partition == 0 && productionStarted is not null)
                {
                    productionStarted.TrySetResult();
                    if (resumeProduction is not null)
                        await resumeProduction.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private static async Task ConsumeAndCommitSequenceAsync(
        IKafkaConsumer<string, string> consumer,
        string topic,
        int messagesPerPartition,
        CancellationToken cancellationToken)
    {
        var nextOffsets = new long[PartitionCount];
        var remaining = PartitionCount * messagesPerPartition;

        while (remaining > 0)
        {
            var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cancellationToken)
                .ConfigureAwait(false);
            if (result is null)
                continue;

            var record = result.Value;
            var expectedOffset = nextOffsets[record.Partition];
            AssertRecord(topic, record, expectedOffset);

            nextOffsets[record.Partition]++;
            remaining--;
            await consumer.CommitAsync(
                [new TopicPartitionOffset(topic, record.Partition, record.Offset + 1)],
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task AssertBrokerSequenceAsync(string topic, CancellationToken cancellationToken)
    {
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"reassignment-oracle-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);
        consumer.Assign(Enumerable.Range(0, PartitionCount)
            .Select(partition => new TopicPartition(topic, partition))
            .ToArray());

        var nextOffsets = new long[PartitionCount];
        var remaining = PartitionCount * MessagesPerPartition;
        while (remaining > 0)
        {
            var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cancellationToken)
                .ConfigureAwait(false);
            if (result is null)
                throw new InvalidOperationException($"Timed out with {remaining} expected record(s) unread.");

            var record = result.Value;
            var expectedOffset = nextOffsets[record.Partition];
            AssertRecord(topic, record, expectedOffset);

            nextOffsets[record.Partition]++;
            remaining--;
        }

        var duplicate = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(2), cancellationToken)
            .ConfigureAwait(false);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Unexpected duplicate at {duplicate.Value.Topic}-{duplicate.Value.Partition}@" +
                $"{duplicate.Value.Offset}.");
        }
    }

    private static void AssertRecord(
        string topic,
        ConsumeResult<string, string> record,
        long expectedOffset)
    {
        var expectedValue = $"{record.Partition}:{expectedOffset}";
        if (record.Offset != expectedOffset || record.Key != expectedValue || record.Value != expectedValue)
        {
            throw new InvalidOperationException(
                $"Record mismatch for {topic}-{record.Partition}: expected offset/value " +
                $"{expectedOffset}/{expectedValue}, actual {record.Offset}/{record.Value}.");
        }
    }

    private static async Task StartReassignmentAsync(
        IAdminClient admin,
        string topic,
        CancellationToken cancellationToken)
    {
        var reassignments = new Dictionary<TopicPartition, Optional<NewPartitionReassignment>>(PartitionCount);
        for (var partition = 0; partition < PartitionCount; partition++)
        {
            reassignments.Add(
                new TopicPartition(topic, partition),
                NewPartitionReassignment.ToReplicas(TargetReplicas));
        }

        await admin.AlterPartitionReassignmentsAsync(reassignments, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task CancelReassignmentAsync(
        IAdminClient admin,
        string topic,
        CancellationToken cancellationToken)
    {
        var cancellations = new Dictionary<TopicPartition, Optional<NewPartitionReassignment>>(PartitionCount);
        for (var partition = 0; partition < PartitionCount; partition++)
        {
            cancellations.Add(
                new TopicPartition(topic, partition),
                Optional.None<NewPartitionReassignment>());
        }

        await admin.AlterPartitionReassignmentsAsync(cancellations, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static Task WaitForReassignmentAsync(
        IAdminClient admin,
        string topic,
        CancellationToken cancellationToken) =>
        WaitUntilAsync(
            async token =>
            {
                var reassignments = await admin.ListPartitionReassignmentsAsync(
                        Enumerable.Range(0, PartitionCount)
                            .Select(partition => new TopicPartition(topic, partition)),
                        cancellationToken: token)
                    .ConfigureAwait(false);
                return Enumerable.Range(0, PartitionCount).All(partition =>
                    reassignments.TryGetValue(new TopicPartition(topic, partition), out var reassignment) &&
                    reassignment.AddingReplicas.Contains(3));
            },
            $"Topic '{topic}' did not enter reassignment to broker 3.",
            cancellationToken);

    private static Task WaitForAssignmentAsync(
        IAdminClient admin,
        string topic,
        IReadOnlyList<int> expectedReplicas,
        int? expectedLeader,
        CancellationToken cancellationToken) =>
        WaitUntilAsync(
            async token =>
            {
                var active = await admin.ListPartitionReassignmentsAsync(
                        Enumerable.Range(0, PartitionCount)
                            .Select(partition => new TopicPartition(topic, partition)),
                        cancellationToken: token)
                    .ConfigureAwait(false);
                if (active.Count != 0)
                    return false;

                var descriptions = await admin.DescribeTopicsAsync([topic], token).ConfigureAwait(false);
                return descriptions[topic].Partitions.All(partition =>
                    (expectedLeader is null
                        ? expectedReplicas.Count == partition.ReplicaNodes.Count &&
                          expectedReplicas.All(partition.ReplicaNodes.Contains) &&
                          expectedReplicas.Contains(partition.LeaderId)
                        : partition.LeaderId == expectedLeader &&
                          partition.ReplicaNodes.SequenceEqual(expectedReplicas)) &&
                    expectedReplicas.All(partition.IsrNodes.Contains));
            },
            $"Topic '{topic}' did not reach assignment [{string.Join(",", expectedReplicas)}] " +
            $"with leader {(expectedLeader is null ? "in the replica set" : expectedLeader)}.",
            cancellationToken);

    private static async Task WaitUntilAsync(
        Func<CancellationToken, Task<bool>> predicate,
        string timeoutMessage,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));

        try
        {
            while (!await predicate(timeout.Token).ConfigureAwait(false))
                await Task.Delay(TimeSpan.FromMilliseconds(200), timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(timeoutMessage);
        }
    }
}
