using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("Admin")]
[Category("Consumer")]
[Category("Producer")]
[Category("Resilience")]
[NotInParallel("BrokerScaleOutKafkaContainer")]
[ClassDataSource<BrokerScaleOutKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class BrokerScaleOutIntegrationTests(BrokerScaleOutKafkaContainer kafka)
{
    private static readonly int[] InitialReplicas = [1, 2];
    private static readonly int[] ScaleOutReplicas = [3, 2];

    [Test]
    [Timeout(240_000)]
    public async Task Clients_DiscoverAndRouteThroughBrokerAddedMidRun(
        CancellationToken cancellationToken)
    {
        const int messagesBeforeScaleOut = 20;
        const int messagesAfterScaleOut = 20;
        var topic = $"broker-scale-out-{Guid.NewGuid():N}";
        var partition = new TopicPartition(topic, 0);

        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithMetadataMaxAge(TimeSpan.FromMilliseconds(250))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();
        await admin.CreateTopicsAsync(
        [
            new NewTopic
            {
                Name = topic,
                NumPartitions = -1,
                ReplicationFactor = -1,
                ReplicaAssignments = new Dictionary<int, IReadOnlyList<int>>
                {
                    [0] = InitialReplicas
                },
                Configs = new Dictionary<string, string>
                {
                    ["min.insync.replicas"] = "1"
                }
            }
        ], cancellationToken: cancellationToken).ConfigureAwait(false);
        await WaitForAssignmentAsync(
            admin,
            topic,
            InitialReplicas,
            expectedLeader: 1,
            cancellationToken).ConfigureAwait(false);

        var initialCluster = await admin.DescribeClusterAsync(cancellationToken).ConfigureAwait(false);
        await Assert.That(initialCluster.Nodes.Select(static node => node.NodeId))
            .IsEquivalentTo(InitialReplicas);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"scale-out-producer-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithIdempotence(true)
            .WithRequestTimeout(TimeSpan.FromSeconds(5))
            .WithDeliveryTimeout(TimeSpan.FromSeconds(45))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken).ConfigureAwait(false);
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"scale-out-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken).ConfigureAwait(false);
        consumer.Assign(partition);

        await ProduceRangeAsync(
            producer,
            topic,
            start: 0,
            count: messagesBeforeScaleOut,
            cancellationToken).ConfigureAwait(false);
        await ConsumeRangeAsync(
            consumer,
            topic,
            start: 0,
            count: messagesBeforeScaleOut,
            cancellationToken).ConfigureAwait(false);

        await kafka.StartBrokerAsync(nodeId: 3, cancellationToken).ConfigureAwait(false);
        await WaitUntilAsync(
            async token =>
            {
                var cluster = await admin.DescribeClusterAsync(token).ConfigureAwait(false);
                return cluster.Nodes.Any(static node => node.NodeId == 3);
            },
            "The running admin client did not discover broker 3.",
            cancellationToken).ConfigureAwait(false);

        await admin.AlterPartitionReassignmentsAsync(
            new Dictionary<TopicPartition, Optional<NewPartitionReassignment>>
            {
                [partition] = NewPartitionReassignment.ToReplicas(ScaleOutReplicas)
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await WaitForAssignmentAsync(
            admin,
            topic,
            ScaleOutReplicas,
            expectedLeader: 3,
            cancellationToken).ConfigureAwait(false);

        await ProduceRangeAsync(
            producer,
            topic,
            start: messagesBeforeScaleOut,
            count: messagesAfterScaleOut,
            cancellationToken).ConfigureAwait(false);
        await producer.FlushAsync(cancellationToken).ConfigureAwait(false);
        await ConsumeRangeAsync(
            consumer,
            topic,
            start: messagesBeforeScaleOut,
            count: messagesAfterScaleOut,
            cancellationToken).ConfigureAwait(false);

        var duplicate = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(2), cancellationToken)
            .ConfigureAwait(false);
        await Assert.That(duplicate).IsNull();
    }

    private static async Task ProduceRangeAsync(
        IKafkaProducer<string, string> producer,
        string topic,
        int start,
        int count,
        CancellationToken cancellationToken)
    {
        for (var sequence = start; sequence < start + count; sequence++)
        {
            var value = sequence.ToString();
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = 0,
                Key = value,
                Value = value
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ConsumeRangeAsync(
        IKafkaConsumer<string, string> consumer,
        string topic,
        int start,
        int count,
        CancellationToken cancellationToken)
    {
        for (var sequence = start; sequence < start + count; sequence++)
        {
            var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), cancellationToken)
                .ConfigureAwait(false);
            if (result is null)
                throw new InvalidOperationException($"Timed out waiting for {topic}-0@{sequence}.");

            var record = result.Value;
            var expected = sequence.ToString();
            if (record.Topic != topic || record.Partition != 0 || record.Offset != sequence ||
                record.Key != expected || record.Value != expected)
            {
                throw new InvalidOperationException(
                    $"Expected {topic}-0@{sequence} value '{expected}', got " +
                    $"{record.Topic}-{record.Partition}@{record.Offset} value '{record.Value}'.");
            }
        }
    }

    private static Task WaitForAssignmentAsync(
        IAdminClient admin,
        string topic,
        IReadOnlyList<int> expectedReplicas,
        int expectedLeader,
        CancellationToken cancellationToken) =>
        WaitUntilAsync(
            async token =>
            {
                var active = await admin.ListPartitionReassignmentsAsync(
                    [new TopicPartition(topic, 0)],
                    cancellationToken: token).ConfigureAwait(false);
                if (active.Count != 0)
                    return false;

                var descriptions = await admin.DescribeTopicsAsync([topic], token).ConfigureAwait(false);
                var partition = descriptions[topic].Partitions.Single();
                return partition.LeaderId == expectedLeader &&
                       partition.ReplicaNodes.SequenceEqual(expectedReplicas) &&
                       expectedReplicas.All(partition.IsrNodes.Contains);
            },
            $"Topic '{topic}' did not reach assignment [{string.Join(",", expectedReplicas)}] " +
            $"with leader {expectedLeader}.",
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
