using Dekaf.Protocol;
using static Dekaf.Tests.Unit.Protocol.WireFormatSnapshotSupport;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Guards against negotiating protocol versions whose schemas no released Apache Kafka broker ships.
/// A KIP implemented ahead of its release must keep its codec below <c>HighestSupportedVersion</c>
/// until the release ships and the schema is re-verified against it.
/// </summary>
public sealed class ReleasedKafkaApiVersionTests
{
    // Highest validVersions in clients/src/main/resources/common/message/*Request.json at Apache Kafka tag
    // 4.4.0. Update this table deliberately when a new Kafka release ships, after re-verifying the schemas
    // of every version it adds. InitProducerId v6 (KIP-939) is marked latestVersionUnstable in 4.4.0, so
    // brokers advertise it only with unstable.api.versions.enable; its released schema matches Dekaf's codec.
    private static readonly IReadOnlyDictionary<ApiKey, short> ReleasedHighestVersions = new Dictionary<ApiKey, short>
    {
        [ApiKey.Produce] = 13,
        [ApiKey.Fetch] = 18,
        [ApiKey.ListOffsets] = 11,
        [ApiKey.Metadata] = 13,
        [ApiKey.OffsetCommit] = 10,
        [ApiKey.OffsetFetch] = 10,
        [ApiKey.FindCoordinator] = 6,
        [ApiKey.JoinGroup] = 9,
        [ApiKey.Heartbeat] = 4,
        [ApiKey.LeaveGroup] = 5,
        [ApiKey.SyncGroup] = 5,
        [ApiKey.DescribeGroups] = 6,
        [ApiKey.ListGroups] = 5,
        [ApiKey.SaslHandshake] = 1,
        [ApiKey.ApiVersions] = 5,
        [ApiKey.CreateTopics] = 7,
        [ApiKey.DeleteTopics] = 6,
        [ApiKey.DeleteRecords] = 2,
        [ApiKey.InitProducerId] = 6,
        [ApiKey.OffsetForLeaderEpoch] = 4,
        [ApiKey.AddPartitionsToTxn] = 5,
        [ApiKey.AddOffsetsToTxn] = 4,
        [ApiKey.EndTxn] = 5,
        [ApiKey.WriteTxnMarkers] = 2,
        [ApiKey.TxnOffsetCommit] = 6,
        [ApiKey.DescribeAcls] = 3,
        [ApiKey.CreateAcls] = 3,
        [ApiKey.DeleteAcls] = 3,
        [ApiKey.DescribeConfigs] = 4,
        [ApiKey.AlterConfigs] = 2,
        [ApiKey.AlterReplicaLogDirs] = 2,
        [ApiKey.DescribeLogDirs] = 5,
        [ApiKey.SaslAuthenticate] = 2,
        [ApiKey.CreatePartitions] = 3,
        [ApiKey.CreateDelegationToken] = 3,
        [ApiKey.RenewDelegationToken] = 2,
        [ApiKey.ExpireDelegationToken] = 2,
        [ApiKey.DescribeDelegationToken] = 3,
        [ApiKey.DeleteGroups] = 3,
        [ApiKey.ElectLeaders] = 2,
        [ApiKey.IncrementalAlterConfigs] = 1,
        [ApiKey.AlterPartitionReassignments] = 1,
        [ApiKey.ListPartitionReassignments] = 0,
        [ApiKey.OffsetDelete] = 0,
        [ApiKey.DescribeClientQuotas] = 1,
        [ApiKey.AlterClientQuotas] = 1,
        [ApiKey.DescribeUserScramCredentials] = 0,
        [ApiKey.AlterUserScramCredentials] = 0,
        [ApiKey.Vote] = 2,
        [ApiKey.BeginQuorumEpoch] = 1,
        [ApiKey.EndQuorumEpoch] = 1,
        [ApiKey.DescribeQuorum] = 2,
        [ApiKey.AlterPartition] = 3,
        [ApiKey.UpdateFeatures] = 2,
        [ApiKey.Envelope] = 0,
        [ApiKey.FetchSnapshot] = 1,
        [ApiKey.DescribeCluster] = 2,
        [ApiKey.DescribeProducers] = 0,
        [ApiKey.BrokerRegistration] = 4,
        [ApiKey.BrokerHeartbeat] = 2,
        [ApiKey.UnregisterBroker] = 0,
        [ApiKey.DescribeTransactions] = 0,
        [ApiKey.ListTransactions] = 2,
        [ApiKey.AllocateProducerIds] = 0,
        [ApiKey.ConsumerGroupHeartbeat] = 1,
        [ApiKey.ConsumerGroupDescribe] = 1,
        [ApiKey.ControllerRegistration] = 0,
        [ApiKey.GetTelemetrySubscriptions] = 0,
        [ApiKey.PushTelemetry] = 0,
        [ApiKey.AssignReplicasToDirs] = 0,
        [ApiKey.ListClientMetricsResources] = 1,
        [ApiKey.DescribeTopicPartitions] = 0,
        [ApiKey.ShareGroupHeartbeat] = 1,
        [ApiKey.ShareGroupDescribe] = 1,
        [ApiKey.ShareFetch] = 2,
        [ApiKey.ShareAcknowledge] = 2,
        [ApiKey.AddRaftVoter] = 1,
        [ApiKey.RemoveRaftVoter] = 0,
        [ApiKey.StreamsGroupHeartbeat] = 1,
        [ApiKey.StreamsGroupDescribe] = 1,
        [ApiKey.DescribeShareGroupOffsets] = 1,
        [ApiKey.AlterShareGroupOffsets] = 0,
        [ApiKey.DeleteShareGroupOffsets] = 0,
    };

    private static readonly IReadOnlyList<Type> MessageTypes = typeof(IKafkaMessage).Assembly
        .GetTypes()
        .Where(static type => !type.IsAbstract &&
            (GetRequestInterface(type) is not null || typeof(IKafkaResponse).IsAssignableFrom(type)))
        .OrderBy(static type => type.FullName, StringComparer.Ordinal)
        .ToArray();

    [Test]
    public async Task MessageTypes_AreDiscovered()
    {
        await Assert.That(MessageTypes.Count).IsGreaterThan(100);
    }

    [Test]
    [MethodDataSource(nameof(MessageTypeNames))]
    public async Task HighestSupportedVersion_DoesNotExceedReleasedKafkaMaximum(string messageType)
    {
        var type = MessageTypes.Single(type => type.Name == messageType);
        var apiKey = GetStaticProperty<ApiKey>(type, "ApiKey");
        var highestVersion = GetStaticProperty<short>(type, "HighestSupportedVersion");

        await Assert.That(ReleasedHighestVersions.TryGetValue(apiKey, out var releasedHighestVersion))
            .IsTrue()
            .Because($"{apiKey} needs a released Kafka maximum in {nameof(ReleasedHighestVersions)}");
        await Assert.That(highestVersion).IsLessThanOrEqualTo(releasedHighestVersion)
            .Because($"{type.Name} would negotiate {apiKey} v{highestVersion}, which no released Kafka broker ships");
    }

    public static IEnumerable<string> MessageTypeNames() => MessageTypes.Select(static type => type.Name);
}
