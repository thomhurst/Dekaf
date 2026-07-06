using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class KRaftAdminMessageTests
{
    [Test]
    public async Task MessageMetadata_MatchesKafkaAssignedApiKeys()
    {
        await Assert.That(DescribeQuorumRequest.ApiKey).IsEqualTo(ApiKey.DescribeQuorum);
        await Assert.That(DescribeQuorumRequest.LowestSupportedVersion).IsEqualTo((short)0);
        await Assert.That(DescribeQuorumRequest.HighestSupportedVersion).IsEqualTo((short)2);

        await Assert.That(AddRaftVoterRequest.ApiKey).IsEqualTo(ApiKey.AddRaftVoter);
        await Assert.That(AddRaftVoterRequest.LowestSupportedVersion).IsEqualTo((short)0);
        await Assert.That(AddRaftVoterRequest.HighestSupportedVersion).IsEqualTo((short)1);

        await Assert.That(RemoveRaftVoterRequest.ApiKey).IsEqualTo(ApiKey.RemoveRaftVoter);
        await Assert.That(UnregisterBrokerRequest.ApiKey).IsEqualTo(ApiKey.UnregisterBroker);
    }

    [Test]
    public async Task DescribeQuorumRequest_Write_V2_EncodesMetadataPartition()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = new DescribeQuorumRequest
        {
            Topics =
            [
                new DescribeQuorumRequestTopic
                {
                    TopicName = "__cluster_metadata",
                    Partitions =
                    [
                        new DescribeQuorumRequestPartition
                        {
                            PartitionIndex = 0
                        }
                    ]
                }
            ]
        };

        request.Write(ref writer, version: 2);

        string topicName;
        IReadOnlyList<int> partitions;
        long remaining;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            var topics = reader.ReadCompactArray(static (ref KafkaProtocolReader r) =>
            {
                var name = r.ReadCompactString() ?? string.Empty;
                var partitionIds = r.ReadCompactArray(static (ref KafkaProtocolReader pr) =>
                {
                    var partitionId = pr.ReadInt32();
                    pr.SkipTaggedFields();
                    return partitionId;
                });

                r.SkipTaggedFields();
                return (name, partitionIds);
            });
            reader.SkipTaggedFields();
            topicName = topics[0].name;
            partitions = topics[0].partitionIds;
            remaining = reader.Remaining;
        }

        await Assert.That(topicName).IsEqualTo("__cluster_metadata");
        await Assert.That(partitions).IsEquivalentTo([0]);
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    public async Task AddRaftVoterRequest_Write_V1_EncodesAckAndListeners()
    {
        var directoryId = Guid.NewGuid();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = new AddRaftVoterRequest
        {
            ClusterId = "cluster-a",
            TimeoutMs = 1234,
            VoterId = 2,
            VoterDirectoryId = directoryId,
            Listeners =
            [
                new RaftVoterEndpointData
                {
                    Name = "CONTROLLER",
                    Host = "localhost",
                    Port = 9093
                }
            ],
            AckWhenCommitted = false
        };

        request.Write(ref writer, version: 1);

        string? clusterId;
        int timeoutMs;
        int voterId;
        Guid parsedDirectoryId;
        IReadOnlyList<ListenerData> listeners;
        bool ackWhenCommitted;
        long remaining;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            clusterId = reader.ReadCompactString();
            timeoutMs = reader.ReadInt32();
            voterId = reader.ReadInt32();
            parsedDirectoryId = reader.ReadUuid();
            listeners = reader.ReadCompactArray(static (ref KafkaProtocolReader r) =>
            {
                var name = r.ReadCompactString() ?? string.Empty;
                var host = r.ReadCompactString() ?? string.Empty;
                var port = r.ReadUInt16();
                r.SkipTaggedFields();
                return new ListenerData(name, host, port);
            });
            ackWhenCommitted = reader.ReadBoolean();
            reader.SkipTaggedFields();
            remaining = reader.Remaining;
        }

        await Assert.That(clusterId).IsEqualTo("cluster-a");
        await Assert.That(timeoutMs).IsEqualTo(1234);
        await Assert.That(voterId).IsEqualTo(2);
        await Assert.That(parsedDirectoryId).IsEqualTo(directoryId);
        var listener = listeners.Single();
        await Assert.That(listener.Name).IsEqualTo("CONTROLLER");
        await Assert.That(listener.Host).IsEqualTo("localhost");
        await Assert.That(listener.Port).IsEqualTo((ushort)9093);
        await Assert.That(ackWhenCommitted).IsFalse();
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    public async Task DescribeQuorumResponse_Read_V2_ParsesQuorumNodesAndReplicaState()
    {
        var directoryId = Guid.NewGuid();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactNullableString(null);
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteCompactString("__cluster_metadata");
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactNullableString(null);
        writer.WriteInt32(3);
        writer.WriteInt32(7);
        writer.WriteInt64(42);
        writer.WriteUnsignedVarInt(1 + 1);
        WriteReplicaState(ref writer, 3, directoryId, logEndOffset: 41, lastFetchTimestamp: 100, lastCaughtUpTimestamp: 99);
        writer.WriteUnsignedVarInt(0 + 1);
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteInt32(3);
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteCompactString("CONTROLLER");
        writer.WriteCompactString("localhost");
        writer.WriteUInt16(9093);
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();

        DescribeQuorumResponse response;
        long remaining;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            response = (DescribeQuorumResponse)DescribeQuorumResponse.Read(ref reader, version: 2);
            remaining = reader.Remaining;
        }

        var partition = response.Topics.Single().Partitions.Single();
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(partition.LeaderId).IsEqualTo(3);
        await Assert.That(partition.LeaderEpoch).IsEqualTo(7);
        await Assert.That(partition.HighWatermark).IsEqualTo(42);
        var voter = partition.CurrentVoters.Single();
        await Assert.That(voter.ReplicaDirectoryId).IsEqualTo(directoryId);
        await Assert.That(voter.LastFetchTimestamp).IsEqualTo(100);
        var nodeListener = response.Nodes.Single().Listeners.Single();
        await Assert.That(nodeListener.Port).IsEqualTo((ushort)9093);
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    public async Task UnregisterBrokerResponse_Read_V0_ParsesErrorFields()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(5);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactNullableString("ok");
        writer.WriteEmptyTaggedFields();

        UnregisterBrokerResponse response;
        long remaining;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            response = (UnregisterBrokerResponse)UnregisterBrokerResponse.Read(ref reader, version: 0);
            remaining = reader.Remaining;
        }

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(5);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ErrorMessage).IsEqualTo("ok");
        await Assert.That(remaining).IsEqualTo(0);
    }

    private static void WriteReplicaState(
        ref KafkaProtocolWriter writer,
        int replicaId,
        Guid directoryId,
        long logEndOffset,
        long lastFetchTimestamp,
        long lastCaughtUpTimestamp)
    {
        writer.WriteInt32(replicaId);
        writer.WriteUuid(directoryId);
        writer.WriteInt64(logEndOffset);
        writer.WriteInt64(lastFetchTimestamp);
        writer.WriteInt64(lastCaughtUpTimestamp);
        writer.WriteEmptyTaggedFields();
    }

    private readonly record struct ListenerData(string Name, string Host, ushort Port);
}
