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
    [Arguments(ApiKey.OffsetForLeaderEpoch, 1, false)]
    [Arguments(ApiKey.OffsetForLeaderEpoch, 2, true)]
    [Arguments(ApiKey.AlterPartitionReassignments, 0, true)]
    [Arguments(ApiKey.DescribeTopicPartitions, 0, true)]
    [Arguments(ApiKey.ConsumerGroupHeartbeat, 1, true)]
    [Arguments(ApiKey.ShareFetch, 2, true)]
    [Arguments(ApiKey.DescribeCluster, 0, true)]
    [Arguments(ApiKey.AddRaftVoter, 0, true)]
    [Arguments(ApiKey.DescribeQuorum, 2, false)]
    [Arguments(ApiKey.SaslAuthenticate, 2, false)]
    public async Task ShouldClientThrottle_MatchesBrokerDelayVersionBoundary(
        ApiKey apiKey,
        short version,
        bool expected)
    {
        await Assert.That(BrokerThrottlePolicy.ShouldClientThrottle(apiKey, version))
            .IsEqualTo(expected);
    }

    [Test]
    public async Task ShouldClientThrottle_MatchesKafka44SchemaRule()
    {
        // Kafka 4.4.0 RequestResponseTest.testClientThrottlesResponsesWithThrottleTime:
        // a response is client-throttled when its schema has throttle_time_ms and its
        // version is at or above the API's first post-KIP-219 version (default 0).
        (ApiKey ApiKey, short Version)[] firstPostKip219Versions =
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
            (ApiKey.DeleteGroups, 1)
        ];

        // First version whose Kafka 4.4.0 response schema has ThrottleTimeMs
        // (clients/src/main/resources/common/message/*Response.json), or null.
        (ApiKey ApiKey, short? Version)[] firstThrottleTimeVersions =
        [
            (ApiKey.Produce, 1),
            (ApiKey.Fetch, 1),
            (ApiKey.ListOffsets, 2),
            (ApiKey.Metadata, 3),
            (ApiKey.LeaderAndIsr, null),
            (ApiKey.StopReplica, null),
            (ApiKey.UpdateMetadata, null),
            (ApiKey.ControlledShutdown, null),
            (ApiKey.OffsetCommit, 3),
            (ApiKey.OffsetFetch, 3),
            (ApiKey.FindCoordinator, 1),
            (ApiKey.JoinGroup, 2),
            (ApiKey.Heartbeat, 1),
            (ApiKey.LeaveGroup, 1),
            (ApiKey.SyncGroup, 1),
            (ApiKey.DescribeGroups, 1),
            (ApiKey.ListGroups, 1),
            (ApiKey.SaslHandshake, null),
            (ApiKey.ApiVersions, 1),
            (ApiKey.CreateTopics, 2),
            (ApiKey.DeleteTopics, 1),
            (ApiKey.DeleteRecords, 0),
            (ApiKey.InitProducerId, 0),
            (ApiKey.OffsetForLeaderEpoch, 2),
            (ApiKey.AddPartitionsToTxn, 0),
            (ApiKey.AddOffsetsToTxn, 0),
            (ApiKey.EndTxn, 0),
            (ApiKey.WriteTxnMarkers, null),
            (ApiKey.TxnOffsetCommit, 0),
            (ApiKey.DescribeAcls, 0),
            (ApiKey.CreateAcls, 0),
            (ApiKey.DeleteAcls, 0),
            (ApiKey.DescribeConfigs, 0),
            (ApiKey.AlterConfigs, 0),
            (ApiKey.AlterReplicaLogDirs, 0),
            (ApiKey.DescribeLogDirs, 0),
            (ApiKey.SaslAuthenticate, null),
            (ApiKey.CreatePartitions, 0),
            (ApiKey.CreateDelegationToken, 0),
            (ApiKey.RenewDelegationToken, 0),
            (ApiKey.ExpireDelegationToken, 0),
            (ApiKey.DescribeDelegationToken, 0),
            (ApiKey.DeleteGroups, 0),
            (ApiKey.ElectLeaders, 0),
            (ApiKey.IncrementalAlterConfigs, 0),
            (ApiKey.AlterPartitionReassignments, 0),
            (ApiKey.ListPartitionReassignments, 0),
            (ApiKey.OffsetDelete, 0),
            (ApiKey.DescribeClientQuotas, 0),
            (ApiKey.AlterClientQuotas, 0),
            (ApiKey.DescribeUserScramCredentials, 0),
            (ApiKey.AlterUserScramCredentials, 0),
            (ApiKey.Vote, null),
            (ApiKey.BeginQuorumEpoch, null),
            (ApiKey.EndQuorumEpoch, null),
            (ApiKey.DescribeQuorum, null),
            (ApiKey.AlterPartition, 0),
            (ApiKey.UpdateFeatures, 0),
            (ApiKey.Envelope, null),
            (ApiKey.FetchSnapshot, 0),
            (ApiKey.DescribeCluster, 0),
            (ApiKey.DescribeProducers, 0),
            (ApiKey.BrokerRegistration, 0),
            (ApiKey.BrokerHeartbeat, 0),
            (ApiKey.UnregisterBroker, 0),
            (ApiKey.DescribeTransactions, 0),
            (ApiKey.ListTransactions, 0),
            (ApiKey.AllocateProducerIds, 0),
            (ApiKey.ConsumerGroupHeartbeat, 0),
            (ApiKey.ConsumerGroupDescribe, 0),
            (ApiKey.ControllerRegistration, 0),
            (ApiKey.GetTelemetrySubscriptions, 0),
            (ApiKey.PushTelemetry, 0),
            (ApiKey.AssignReplicasToDirs, 0),
            (ApiKey.ListClientMetricsResources, 0),
            (ApiKey.DescribeTopicPartitions, 0),
            (ApiKey.ShareGroupHeartbeat, 0),
            (ApiKey.ShareGroupDescribe, 0),
            (ApiKey.ShareFetch, 0),
            (ApiKey.ShareAcknowledge, 0),
            (ApiKey.AddRaftVoter, 0),
            (ApiKey.RemoveRaftVoter, 0),
            (ApiKey.StreamsGroupHeartbeat, 0),
            (ApiKey.StreamsGroupDescribe, 0),
            (ApiKey.DescribeShareGroupOffsets, 0),
            (ApiKey.AlterShareGroupOffsets, 0),
            (ApiKey.DeleteShareGroupOffsets, 0),
            (ApiKey.UnregisterController, 0)
        ];

        // Every ApiKey needs a schema entry, so a new API cannot fall through untested.
        await Assert.That(firstThrottleTimeVersions.Select(entry => entry.ApiKey))
            .IsEquivalentTo(Enum.GetValues<ApiKey>());

        var postKip219Versions = firstPostKip219Versions.ToDictionary(entry => entry.ApiKey, entry => entry.Version);
        foreach (var (apiKey, throttleTimeVersion) in firstThrottleTimeVersions)
        {
            var postKip219Version = postKip219Versions.GetValueOrDefault(apiKey);
            for (short version = 0; version <= 20; version++)
            {
                var expected = version >= throttleTimeVersion && version >= postKip219Version;
                await Assert.That(BrokerThrottlePolicy.ShouldClientThrottle(apiKey, version))
                    .IsEqualTo(expected)
                    .Because($"{apiKey} v{version}");
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

    [Test]
    public async Task RegisterBroker_ChangedEndpointAliasesCanonicalStateAndMergesDeadline()
    {
        await using var pool = new ConnectionPool();
        pool.RegisterBroker(1, "old-host", 9092);

        var canonicalState = pool.GetBrokerThrottleStateForTest(1, "old-host", 9092);
        canonicalState.Observe(5_000);
        var changedEndpointState = pool.GetBrokerThrottleStateForTest(-1, "new-host", 9093);
        changedEndpointState.Observe(10_000);

        pool.RegisterBroker(1, "new-host", 9093);

        var aliasedEndpointState = pool.GetBrokerThrottleStateForTest(-1, "new-host", 9093);
        await Assert.That(ReferenceEquals(aliasedEndpointState, canonicalState)).IsTrue();
        await Assert.That(canonicalState.GetRemainingMilliseconds()).IsGreaterThan(9_000);
    }

    private static ClientTelemetryMetric Metric(
        ClientTelemetryMetricSnapshot snapshot,
        string name) => snapshot.Metrics.Single(metric => metric.Name == name);

}
