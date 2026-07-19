using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Telemetry;

namespace Dekaf.Tests.Unit.Networking;

public sealed class BrokerThrottleStateTests
{
    [Test]
    [Arguments(ApiKey.Produce, 5, false)]
    [Arguments(ApiKey.Produce, 6, true)]
    [Arguments(ApiKey.Fetch, 7, false)]
    [Arguments(ApiKey.Fetch, 8, true)]
    [Arguments(ApiKey.Metadata, 5, false)]
    [Arguments(ApiKey.Metadata, 6, true)]
    [Arguments(ApiKey.FindCoordinator, 1, false)]
    [Arguments(ApiKey.FindCoordinator, 2, true)]
    [Arguments(ApiKey.OffsetCommit, 3, false)]
    [Arguments(ApiKey.OffsetCommit, 4, true)]
    [Arguments(ApiKey.CreateTopics, 2, false)]
    [Arguments(ApiKey.CreateTopics, 3, true)]
    [Arguments(ApiKey.DescribeConfigs, 1, false)]
    [Arguments(ApiKey.DescribeConfigs, 2, true)]
    [Arguments(ApiKey.AlterPartitionReassignments, 0, true)]
    [Arguments(ApiKey.DescribeTopicPartitions, 0, true)]
    [Arguments(ApiKey.ConsumerGroupHeartbeat, 1, false)]
    [Arguments(ApiKey.ShareFetch, 2, false)]
    public async Task ShouldClientThrottle_MatchesBrokerDelayVersionBoundary(
        ApiKey apiKey,
        short version,
        bool expected)
    {
        await Assert.That(BrokerThrottlePolicy.ShouldClientThrottle(apiKey, version))
            .IsEqualTo(expected);
    }

    [Test]
    public async Task ShouldClientThrottle_CoversEverySupportedKip219Response()
    {
        (ApiKey ApiKey, short FirstClientThrottledVersion)[] expected =
        [
            (ApiKey.Produce, 6),
            (ApiKey.Fetch, 8),
            (ApiKey.ListOffsets, 3),
            (ApiKey.Metadata, 6),
            (ApiKey.OffsetCommit, 4),
            (ApiKey.OffsetFetch, 4),
            (ApiKey.FindCoordinator, 2),
            (ApiKey.JoinGroup, 3),
            (ApiKey.Heartbeat, 2),
            (ApiKey.LeaveGroup, 2),
            (ApiKey.SyncGroup, 2),
            (ApiKey.DescribeGroups, 2),
            (ApiKey.ListGroups, 2),
            (ApiKey.ApiVersions, 2),
            (ApiKey.CreateTopics, 3),
            (ApiKey.DeleteTopics, 2),
            (ApiKey.DeleteRecords, 1),
            (ApiKey.InitProducerId, 1),
            (ApiKey.AddPartitionsToTxn, 1),
            (ApiKey.AddOffsetsToTxn, 1),
            (ApiKey.EndTxn, 1),
            (ApiKey.TxnOffsetCommit, 1),
            (ApiKey.DescribeAcls, 1),
            (ApiKey.CreateAcls, 1),
            (ApiKey.DeleteAcls, 1),
            (ApiKey.DescribeConfigs, 2),
            (ApiKey.AlterConfigs, 1),
            (ApiKey.AlterReplicaLogDirs, 1),
            (ApiKey.DescribeLogDirs, 1),
            (ApiKey.CreatePartitions, 1),
            (ApiKey.CreateDelegationToken, 1),
            (ApiKey.RenewDelegationToken, 1),
            (ApiKey.ExpireDelegationToken, 1),
            (ApiKey.DescribeDelegationToken, 1),
            (ApiKey.DeleteGroups, 1),
            (ApiKey.ElectLeaders, 0),
            (ApiKey.IncrementalAlterConfigs, 0),
            (ApiKey.AlterPartitionReassignments, 0),
            (ApiKey.ListPartitionReassignments, 0),
            (ApiKey.OffsetDelete, 0),
            (ApiKey.DescribeUserScramCredentials, 0),
            (ApiKey.AlterUserScramCredentials, 0),
            (ApiKey.UnregisterBroker, 0),
            (ApiKey.DescribeTopicPartitions, 0)
        ];

        var actual = Enum.GetValues<ApiKey>()
            .Where(apiKey => BrokerThrottlePolicy.ShouldClientThrottle(apiKey, short.MaxValue))
            .ToArray();
        await Assert.That(actual).IsEquivalentTo(expected.Select(entry => entry.ApiKey));

        foreach (var (apiKey, firstVersion) in expected)
        {
            await Assert.That(BrokerThrottlePolicy.ShouldClientThrottle(apiKey, firstVersion)).IsTrue();
            if (firstVersion > 0)
            {
                await Assert.That(BrokerThrottlePolicy.ShouldClientThrottle(apiKey, (short)(firstVersion - 1)))
                    .IsFalse();
            }
        }
    }

    [Test]
    public async Task Observe_ConcurrentResponsesKeepLatestDeadline()
    {
        var state = new BrokerThrottleState();

        Parallel.Invoke(
            () => state.Observe(250),
            () => state.Observe(2_000),
            () => state.Observe(500));

        await Assert.That(state.GetRemainingMilliseconds()).IsGreaterThan(1_500);
    }

    [Test]
    public async Task WaitAsync_UnthrottledFastPathCompletesSynchronously()
    {
        var state = new BrokerThrottleState();

        var wait = state.WaitAsync(CancellationToken.None, CancellationToken.None);

        await Assert.That(wait.IsCompletedSuccessfully).IsTrue();
        await wait;
    }

    [Test]
    [Timeout(5_000)]
    public async Task WaitAsync_CancellationInterruptsThrottle(CancellationToken cancellationToken)
    {
        var state = new BrokerThrottleState();
        state.Observe(10_000);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var wait = state.WaitAsync(cts.Token, CancellationToken.None);
        cts.Cancel();

        await Assert.That(async () => await wait).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task RecordBrokerThrottle_ExposesProducerAndConsumerTelemetry()
    {
        var producer = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Producer);
        var consumer = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Consumer);
        producer.RecordBrokerThrottle(10);
        producer.RecordBrokerThrottle(30);
        consumer.RecordBrokerThrottle(20);

        var producerSnapshot = producer.Collect(new ClientTelemetrySubscription(
            ClientInstanceId: Guid.Empty,
            SubscriptionId: 1,
            CompressionType: 0,
            PushIntervalMs: 1_000,
            TelemetryMaxBytes: 1024,
            DeltaTemporality: false,
            RequestedMetrics: ["org.apache.kafka.producer.produce.throttle.time"]));
        var consumerSnapshot = consumer.Collect(new ClientTelemetrySubscription(
            ClientInstanceId: Guid.Empty,
            SubscriptionId: 1,
            CompressionType: 0,
            PushIntervalMs: 1_000,
            TelemetryMaxBytes: 1024,
            DeltaTemporality: false,
            RequestedMetrics: ["org.apache.kafka.consumer.fetch.manager.fetch.throttle.time"]));

        await Assert.That(Metric(producerSnapshot, ClientTelemetryMetricNames.ProducerProduceThrottleTimeAvg).Value)
            .IsEqualTo(20);
        await Assert.That(Metric(producerSnapshot, ClientTelemetryMetricNames.ProducerProduceThrottleTimeMax).Value)
            .IsEqualTo(30);
        await Assert.That(producer.MaxObservedBrokerThrottleTimeMs).IsEqualTo(30);
        await Assert.That(Metric(consumerSnapshot, ClientTelemetryMetricNames.ConsumerFetchThrottleTimeAvg).Value)
            .IsEqualTo(20);
        await Assert.That(Metric(consumerSnapshot, ClientTelemetryMetricNames.ConsumerFetchThrottleTimeMax).Value)
            .IsEqualTo(20);
    }

    private static ClientTelemetryMetric Metric(
        ClientTelemetryMetricSnapshot snapshot,
        string name) => snapshot.Metrics.Single(metric => metric.Name == name);
}
