using Dekaf.Admin;
using Dekaf.Protocol;
using Dekaf.Streams;
using static Dekaf.Admin.AdminClientStreamsGroupDescriptionExtensions;
using StreamsGroupSubtopology = Dekaf.Streams.StreamsGroupSubtopology;
using StreamsGroupTopology = Dekaf.Streams.StreamsGroupTopology;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
[ClassDataSource<TopologyDescriptionKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class StreamsGroupTopologyDescriptionIntegrationTests(TopologyDescriptionKafkaContainer kafka)
{
    [Test]
    [SupportsKafka(440)]
    [Timeout(180_000)]
    public async Task MemberPublishesTopologyDescription_AdminReadsItBack(CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateTestTopicAsync(partitions: 1);
        var groupId = $"streams-group-{Guid.NewGuid():N}";
        await using var client = Connect();
        await using var admin = client.CreateAdminClient().Build();
        await using var member = await JoinWithDescriptionAsync(client, groupId, topic, cancellationToken);

        var described = await WaitForStoredDescriptionAsync(admin, groupId, cancellationToken);

        var subtopology = described.TopologyDescription!.Subtopologies.Single();
        await Assert.That(subtopology.SubtopologyId).IsEqualTo("0");
        await Assert.That(subtopology.Nodes.Select(static node => node.Name))
            .IsEquivalentTo(["KSTREAM-SOURCE-0000000000", "KSTREAM-PROCESSOR-0000000001", "KSTREAM-SINK-0000000002"]);
        var source = subtopology.Nodes.Single(static node => node.NodeType == StreamsGroupTopologyNodeType.Source);
        await Assert.That(source.SourceTopics).IsEquivalentTo([topic]);
        await Assert.That(source.Successors).IsEquivalentTo(["KSTREAM-PROCESSOR-0000000001"]);
        var processor = subtopology.Nodes.Single(static node => node.NodeType == StreamsGroupTopologyNodeType.Processor);
        await Assert.That(processor.Stores).IsEquivalentTo(["counts-store"]);
        var sink = subtopology.Nodes.Single(static node => node.NodeType == StreamsGroupTopologyNodeType.Sink);
        await Assert.That(sink.SinkTopic).IsEqualTo($"{topic}-output");
        await Assert.That(described.TopologyDescription.GlobalStores).IsEmpty();

        await LeaveAsync(member, cancellationToken);
    }

    [Test]
    [SupportsKafka(440)]
    [Timeout(180_000)]
    public async Task DeleteStreamsGroups_PluginDeletionFailure_KeepsOnlyThatGroup(CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateTestTopicAsync(partitions: 1);
        var failingGroupId = $"{TopologyDescriptionKafkaContainer.FailingDeleteGroupPrefix}{Guid.NewGuid():N}";
        var deletableGroupId = $"streams-group-{Guid.NewGuid():N}";
        await using var client = Connect();
        await using var admin = client.CreateAdminClient().Build();

        // The broker calls the plugin's deleteTopology only for groups with a stored description, so both
        // groups publish one and then become empty.
        foreach (var groupId in new[] { failingGroupId, deletableGroupId })
        {
            await using var member = await JoinWithDescriptionAsync(client, groupId, topic, cancellationToken);
            await WaitForStoredDescriptionAsync(admin, groupId, cancellationToken);
            await LeaveAsync(member, cancellationToken);
        }

        var results = await admin.DeleteStreamsGroupsAsync(
            [failingGroupId, deletableGroupId],
            cancellationToken: cancellationToken);

        // Kafka replaces the plugin's exception message with a fixed one.
        await Assert.That(results[failingGroupId].ErrorCode).IsEqualTo(ErrorCode.GroupDeletionFailed);
        await Assert.That(results[failingGroupId].ErrorMessage)
            .IsEqualTo("Topology description plugin failed to delete the topology.");
        await Assert.That(results[deletableGroupId].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results[deletableGroupId].ErrorMessage).IsNull();

        // The broker kept the failing group and deleted the other one. Kafka marks the stored description
        // uncertain before it calls the plugin, so the kept group no longer reports it as available.
        var kept = (await admin.DescribeStreamsGroupsAsync(
            [failingGroupId],
            new DescribeStreamsGroupsOptions { IncludeTopologyDescription = true },
            cancellationToken))[failingGroupId];
        await Assert.That(kept.GroupState).IsEqualTo("Empty");
        await Assert.That(kept.Members).IsEmpty();
        await Assert.That(kept.TopologyDescriptionStatus).IsEqualTo(StreamsGroupTopologyDescriptionStatus.NotStored);
        var deletedAgain = await admin.DeleteStreamsGroupsAsync([deletableGroupId], cancellationToken: cancellationToken);
        await Assert.That(deletedAgain[deletableGroupId].ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
    }

    private KafkaClient Connect() => Kafka.Connect(kafka.BootstrapServers, builder =>
        builder.WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()));

    private static async Task<IStreamsGroupMember> JoinWithDescriptionAsync(
        KafkaClient client, string groupId, string topic, CancellationToken cancellationToken)
    {
        var member = client.CreateStreamsGroupMember(new StreamsGroupMemberOptions
        {
            GroupId = groupId
        });
        try
        {
            await member.InitializeAsync(cancellationToken);
            await member.JoinAsync(new StreamsGroupMemberUpdate
            {
                Topology = new StreamsGroupTopology
                {
                    Epoch = 0,
                    Subtopologies =
                    [
                        new StreamsGroupSubtopology
                        {
                            SubtopologyId = "0",
                            SourceTopics = [topic],
                            SourceTopicRegex = [],
                            StateChangelogTopics = [],
                            RepartitionSinkTopics = [],
                            RepartitionSourceTopics = [],
                            CopartitionGroups = []
                        }
                    ],
                    Description = CreateDescription(topic)
                },
                ActiveTasks = [],
                StandbyTasks = [],
                WarmupTasks = [],
                ProcessId = "process-description",
                ClientTags = []
            }, cancellationToken);
            return member;
        }
        catch
        {
            await member.DisposeAsync();
            throw;
        }
    }

    // The coordinator asks for the description on a heartbeat once the topology is configured;
    // the member publishes it in the background.
    private static Task<StreamsGroupDescription> WaitForStoredDescriptionAsync(
        IAdminClient admin, string groupId, CancellationToken cancellationToken) =>
        TestWait.WaitForConditionAsync(
            async () => (await admin.DescribeStreamsGroupsAsync(
                [groupId],
                new DescribeStreamsGroupsOptions { IncludeTopologyDescription = true },
                cancellationToken))[groupId],
            static group => group.TopologyDescriptionStatus == StreamsGroupTopologyDescriptionStatus.Available,
            maxRetries: 20,
            description: "the published topology description to be readable",
            formatObserved: static group => group.TopologyDescriptionStatus.ToString());

    private static async Task LeaveAsync(IStreamsGroupMember member, CancellationToken cancellationToken) =>
        await member.CloseAsync(new StreamsGroupCloseOptions
        {
            GroupMembershipOperation = StreamsGroupMembershipOperation.LeaveGroup
        }, cancellationToken);

    private static StreamsGroupTopologyDescription CreateDescription(string topic) => new()
    {
        Subtopologies =
        [
            new StreamsGroupTopologyDescriptionSubtopology
            {
                SubtopologyId = "0",
                Nodes =
                [
                    new StreamsGroupTopologyNode
                    {
                        Name = "KSTREAM-SOURCE-0000000000",
                        NodeType = StreamsGroupTopologyNodeType.Source,
                        SourceTopics = [topic],
                        Stores = [],
                        Successors = ["KSTREAM-PROCESSOR-0000000001"]
                    },
                    new StreamsGroupTopologyNode
                    {
                        Name = "KSTREAM-PROCESSOR-0000000001",
                        NodeType = StreamsGroupTopologyNodeType.Processor,
                        SourceTopics = [],
                        Stores = ["counts-store"],
                        Successors = ["KSTREAM-SINK-0000000002"]
                    },
                    new StreamsGroupTopologyNode
                    {
                        Name = "KSTREAM-SINK-0000000002",
                        NodeType = StreamsGroupTopologyNodeType.Sink,
                        SourceTopics = [],
                        SinkTopic = $"{topic}-output",
                        Stores = [],
                        Successors = []
                    }
                ]
            }
        ],
        GlobalStores = []
    };
}
