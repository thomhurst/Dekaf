using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Streams;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
[SupportsKafka(420)]
public sealed class AdminGroupListingTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task MixedInventory_PreservesTypesAndFiltersEveryConvenience()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var prefix = $"inventory-{Guid.NewGuid():N}-";
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers);
        await using var admin = client.CreateAdminClient().Build();
        await admin.AlterConsumerGroupOffsetsAsync(prefix + "simple", [new TopicPartitionOffset(topic, 0, 0)]);
        await using var producer = await client.CreateProducer<string, string>().BuildAsync();
        await producer.ProduceAsync(topic, "key", "value");
        await using var consumer = await client.CreateConsumer<string, string>()
            .WithGroupId(prefix + "consumer").WithAutoOffsetReset(AutoOffsetReset.Earliest).BuildAsync();
        consumer.Subscribe(topic);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), timeout.Token);
        await Assert.That(consumed).IsNotNull();

        await using var share = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(prefix + "share").BuildAsync();
        share.Subscribe(topic);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(share);
        await using var streams = client.CreateStreamsGroupMember(new StreamsGroupMemberOptions { GroupId = prefix + "streams" });
        await streams.InitializeAsync();
        await streams.JoinAsync(new StreamsGroupMemberUpdate
        {
            ProcessId = "listing-process", ActiveTasks = [], StandbyTasks = [], WarmupTasks = [], ClientTags = [],
            Topology = new Dekaf.Streams.StreamsGroupTopology
            {
                Epoch = 0,
                Subtopologies = [new Dekaf.Streams.StreamsGroupSubtopology
                {
                    SubtopologyId = "0", SourceTopics = [topic], SourceTopicRegex = [], StateChangelogTopics = [],
                    RepartitionSinkTopics = [], RepartitionSourceTopics = [], CopartitionGroups = []
                }]
            }
        });

        await using var pool = new ConnectionPool("connect-listing-test",
            new ConnectionOptions { RequestTimeout = TimeSpan.FromSeconds(30) }, loggerFactory: null);
        var endpoint = BootstrapServerList.Parse(KafkaContainer.BootstrapServers);
        var bootstrap = await pool.GetConnectionAsync(endpoint.Host, endpoint.Port, timeout.Token);
        var findVersion = ((IKafkaCapabilityProvider)bootstrap).Capabilities.NegotiateVersion(ApiKey.FindCoordinator,
            FindCoordinatorRequest.LowestSupportedVersion, FindCoordinatorRequest.HighestSupportedVersion);
        var found = await bootstrap.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            new() { Key = prefix + "connect", KeyType = CoordinatorType.Group }, findVersion, timeout.Token);
        var coordinator = found.Coordinators.Single();
        await Assert.That(coordinator.ErrorCode).IsEqualTo(ErrorCode.None);
        pool.RegisterBroker(coordinator.NodeId, coordinator.Host, coordinator.Port);
        var connection = await pool.GetConnectionAsync(coordinator.NodeId, timeout.Token);
        var joined = await connection.SendAsync<TestJoinGroupRequest, TestJoinGroupResponse>(
            new(prefix + "connect", ""), 5, timeout.Token);
        if (joined.ErrorCode == ErrorCode.MemberIdRequired)
            joined = await connection.SendAsync<TestJoinGroupRequest, TestJoinGroupResponse>(
                new(prefix + "connect", joined.MemberId), 5, timeout.Token);
        await Assert.That(joined.ErrorCode).IsEqualTo(ErrorCode.None);
        try
        {
            var descriptions = await admin.DescribeClassicGroupsAsync(
                [prefix + "simple", prefix + "connect"],
                new DescribeClassicGroupsOptions { IncludeAuthorizedOperations = true }, timeout.Token);
            await Assert.That(descriptions[prefix + "simple"].ErrorCode).IsEqualTo(ErrorCode.None);
            await Assert.That(descriptions[prefix + "simple"].Description!.ProtocolType).IsEqualTo("");
            await Assert.That(descriptions[prefix + "simple"].Description!.Members).IsEmpty();
            var connect = descriptions[prefix + "connect"];
            await Assert.That(connect.ErrorCode).IsEqualTo(ErrorCode.None);
            await Assert.That(connect.Description!.ProtocolType).IsEqualTo("connect");
            await Assert.That(connect.Description.CoordinatorId).IsEqualTo(coordinator.NodeId);
            await Assert.That(connect.Description.Members.Select(static member => member.MemberId))
                .IsEquivalentTo([joined.MemberId]);
            await Assert.That(connect.Description.Members.All(static member => member.Assignment is null)).IsTrue();
            var all = (await admin.ListGroupsAsync(cancellationToken: timeout.Token))
                .Where(group => group.GroupId.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            await Assert.That(all.Select(group => (group.GroupId[prefix.Length..], group.GroupType, group.ProtocolType)))
                .IsEquivalentTo(new (string, string?, string?)[]
                {
                    ("simple", "classic", ""), ("consumer", "consumer", "consumer"),
                    ("share", "share", "share"), ("streams", "streams", "streams"), ("connect", "classic", "connect")
                });
            var classic = await admin.ListGroupsAsync(new ListGroupsOptions
            {
                Types = ["classic"], ProtocolTypes = ["connect"],
                States = [all.Single(group => group.GroupId == prefix + "connect").State!.ToLowerInvariant()]
            }, timeout.Token);
            await Assert.That(classic.Any(group => group.GroupId == prefix + "connect")).IsTrue();
            await Assert.That(classic.All(static group => group.ProtocolType == "connect" && group.GroupType == "classic")).IsTrue();
            var consumers = await admin.ListConsumerGroupsAsync(cancellationToken: timeout.Token);
            await Assert.That(consumers.Where(group => group.GroupId.StartsWith(prefix, StringComparison.Ordinal))
                .Select(group => group.GroupId[prefix.Length..])).IsEquivalentTo(["consumer", "simple"]);
            var shares = await admin.ListShareGroupsAsync(cancellationToken: timeout.Token);
            await Assert.That(shares.Where(group => group.GroupId.StartsWith(prefix, StringComparison.Ordinal))
                .Select(group => group.GroupId[prefix.Length..])).IsEquivalentTo(["share"]);
            var streamGroups = await admin.ListStreamsGroupsAsync(cancellationToken: timeout.Token);
            await Assert.That(streamGroups.Where(group => group.GroupId.StartsWith(prefix, StringComparison.Ordinal))
                .Select(group => group.GroupId[prefix.Length..])).IsEquivalentTo(["streams"]);
        }
        finally
        {
            var left = await connection.SendAsync<TestLeaveGroupRequest, TestLeaveGroupResponse>(
                new(prefix + "connect", joined.MemberId), 3, CancellationToken.None);
            await Assert.That(left.ErrorCode).IsEqualTo(ErrorCode.None);
        }
    }

    // Test-only Classic coordination creates a non-consumer protocol group. These codecs
    // follow Apache Kafka 4.3.1 JoinGroup v5 and LeaveGroup v3 message schemas.
    private sealed class TestJoinGroupRequest(string groupId, string memberId) : IKafkaRequest<TestJoinGroupResponse>
    {
        public static ApiKey ApiKey => ApiKey.JoinGroup;
        public static short LowestSupportedVersion => 5;
        public static short HighestSupportedVersion => 5;
        public static bool IsFlexibleVersion(short version) => false;
        public static short GetRequestHeaderVersion(short version) => 1;
        public static short GetResponseHeaderVersion(short version) => 0;
        public void Write(ref KafkaProtocolWriter writer, short version)
        {
            writer.WriteString(groupId);
            writer.WriteInt32(60_000);
            writer.WriteInt32(60_000);
            writer.WriteString(memberId);
            writer.WriteString((string?)null);
            writer.WriteString("connect");
            writer.WriteInt32(1);
            writer.WriteString("listing-test");
            writer.WriteBytes([]);
        }
    }

    private sealed class TestJoinGroupResponse : IKafkaResponse
    {
        public static ApiKey ApiKey => ApiKey.JoinGroup;
        public static short LowestSupportedVersion => 5;
        public static short HighestSupportedVersion => 5;
        public required ErrorCode ErrorCode { get; init; }
        public required string MemberId { get; init; }
        public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
        {
            _ = reader.ReadInt32();
            var error = (ErrorCode)reader.ReadInt16();
            _ = reader.ReadInt32();
            _ = reader.ReadString();
            _ = reader.ReadString();
            var memberId = reader.ReadString()!;
            var members = reader.ReadInt32();
            for (var i = 0; i < members; i++)
            {
                _ = reader.ReadString();
                _ = reader.ReadString();
                _ = reader.ReadBytes();
            }
            return new TestJoinGroupResponse { ErrorCode = error, MemberId = memberId };
        }
    }

    private sealed class TestLeaveGroupRequest(string groupId, string memberId) : IKafkaRequest<TestLeaveGroupResponse>
    {
        public static ApiKey ApiKey => ApiKey.LeaveGroup;
        public static short LowestSupportedVersion => 3;
        public static short HighestSupportedVersion => 3;
        public static bool IsFlexibleVersion(short version) => false;
        public static short GetRequestHeaderVersion(short version) => 1;
        public static short GetResponseHeaderVersion(short version) => 0;
        public void Write(ref KafkaProtocolWriter writer, short version)
        {
            writer.WriteString(groupId);
            writer.WriteInt32(1);
            writer.WriteString(memberId);
            writer.WriteString((string?)null);
        }
    }

    private sealed class TestLeaveGroupResponse : IKafkaResponse
    {
        public static ApiKey ApiKey => ApiKey.LeaveGroup;
        public static short LowestSupportedVersion => 3;
        public static short HighestSupportedVersion => 3;
        public required ErrorCode ErrorCode { get; init; }
        public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
        {
            _ = reader.ReadInt32();
            var error = (ErrorCode)reader.ReadInt16();
            var members = reader.ReadInt32();
            for (var i = 0; i < members; i++)
            {
                _ = reader.ReadString();
                _ = reader.ReadString();
                var memberError = (ErrorCode)reader.ReadInt16();
                if (memberError != ErrorCode.None) error = memberError;
            }
            return new TestLeaveGroupResponse { ErrorCode = error };
        }
    }
}
