using Dekaf.Streams;

namespace Dekaf.Tests.Integration;

[SupportsKafka(420)]
public sealed class StreamsGroupMemberIntegrationTests(KafkaTestContainer kafka)
    : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task MemberJoinsAndLeavesStreamsGroup()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers, builder =>
            builder.WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()));
        await using var member = client.CreateStreamsGroupMember(new StreamsGroupMemberOptions
        {
            GroupId = $"streams-group-{Guid.NewGuid():N}"
        });
        await member.InitializeAsync();

        var result = await member.JoinAsync(new StreamsGroupMemberUpdate
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
                ]
            },
            ActiveTasks = [],
            StandbyTasks = [],
            WarmupTasks = [],
            ProcessId = "integration-process",
            ClientTags = []
        });

        await Assert.That(result.MemberEpoch).IsGreaterThan(0);
        await Assert.That(member.Snapshot.IsJoined).IsTrue();
        await member.CloseAsync();
        await Assert.That(member.Snapshot.IsClosed).IsTrue();
    }
}
