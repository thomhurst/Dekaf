using Dekaf.Admin;
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
        await using var client = Kafka.Connect(kafka.BootstrapServers, builder =>
            builder.WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()));
        await using var member = client.CreateStreamsGroupMember(new StreamsGroupMemberOptions
        {
            GroupId = groupId
        });
        await member.InitializeAsync(cancellationToken);
        await using var admin = client.CreateAdminClient().Build();

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

        // The coordinator asks for the description on a heartbeat once the topology is configured;
        // the member publishes it in the background.
        var described = await TestWait.WaitForConditionAsync(
            async () => (await admin.DescribeStreamsGroupsAsync(
                [groupId],
                new DescribeStreamsGroupsOptions { IncludeTopologyDescription = true },
                cancellationToken))[groupId],
            static group => group.TopologyDescriptionStatus == StreamsGroupTopologyDescriptionStatus.Available,
            maxRetries: 20,
            description: "the published topology description to be readable",
            formatObserved: static group => group.TopologyDescriptionStatus.ToString());

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

        await member.CloseAsync(new StreamsGroupCloseOptions
        {
            GroupMembershipOperation = StreamsGroupMembershipOperation.LeaveGroup
        }, cancellationToken);
    }

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
