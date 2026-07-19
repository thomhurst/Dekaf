using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class FetchVersionCompatibilityTests
{
    private static readonly Guid DirectoryId = new("00112233-4455-6677-8899-aabbccddeeff");

    [Test]
    public async Task FetchMessages_SupportVersion18()
    {
        await Assert.That(FetchRequest.HighestSupportedVersion).IsEqualTo((short)18);
        await Assert.That(FetchResponse.HighestSupportedVersion).IsEqualTo((short)18);
    }

    [Test]
    public async Task ConsumerDefaults_KeepV17AndV18RequestBytesEqualToV16()
    {
        var request = CreateConsumerRequest();

        var version16 = Serialize(request, version: 16);
        var version17 = Serialize(request, version: 17);
        var version18 = Serialize(request, version: 18);

        await Assert.That(version16.Length).IsEqualTo(76);
        await Assert.That(version17.AsSpan().SequenceEqual(version16)).IsTrue();
        await Assert.That(version18.AsSpan().SequenceEqual(version16)).IsTrue();
    }

    [Test]
    public async Task V18Partition_WritesReplicaDirectoryAndHighWatermarkTags()
    {
        var partition = new FetchRequestPartition
        {
            Partition = 2,
            CurrentLeaderEpoch = 3,
            FetchOffset = 42,
            LastFetchedEpoch = 3,
            LogStartOffset = 5,
            PartitionMaxBytes = 1024,
            ReplicaDirectoryId = DirectoryId,
            HighWatermark = 100
        };

        var actual = SerializePartition(partition, version: 18);
        var expected = Convert.FromHexString(
            "0000000200000003000000000000002A00000003000000000000000500000400" +
            "02001000112233445566778899AABBCCDDEEFF01080000000000000064");

        await Assert.That(actual.AsSpan().SequenceEqual(expected)).IsTrue();
    }

    [Test]
    public async Task V17Partition_WritesOnlyReplicaDirectoryTag()
    {
        var partition = new FetchRequestPartition
        {
            Partition = 2,
            CurrentLeaderEpoch = 3,
            FetchOffset = 42,
            LastFetchedEpoch = 3,
            LogStartOffset = 5,
            PartitionMaxBytes = 1024,
            ReplicaDirectoryId = DirectoryId,
            HighWatermark = 100
        };

        var actual = SerializePartition(partition, version: 17);
        var expected = Convert.FromHexString(
            "0000000200000003000000000000002A00000003000000000000000500000400" +
            "01001000112233445566778899AABBCCDDEEFF");

        await Assert.That(actual.AsSpan().SequenceEqual(expected)).IsTrue();
    }

    [Test]
    public async Task V15Request_WritesClusterAndReplicaStateAsTaggedFields()
    {
        var request = new FetchRequest
        {
            ClusterId = "cluster",
            ReplicaState = new ReplicaState { ReplicaId = 2, ReplicaEpoch = 3 },
            MaxWaitMs = 500,
            MinBytes = 1,
            MaxBytes = 1024,
            Topics = []
        };

        var actual = Serialize(request, version: 15);
        var expected = Convert.FromHexString(
            "000001F400000001000004000000000000FFFFFFFF010101" +
            "02000808636C7573746572010D00000002000000000000000300");

        await Assert.That(actual.AsSpan().SequenceEqual(expected)).IsTrue();
    }

    private static FetchRequest CreateConsumerRequest() => new()
    {
        MaxWaitMs = 500,
        MinBytes = 1,
        MaxBytes = 1024,
        Topics =
        [
            new FetchRequestTopic
            {
                TopicId = new Guid("10213243-5465-7687-98a9-bacbdcedfe0f"),
                Partitions =
                [
                    new FetchRequestPartition
                    {
                        Partition = 2,
                        FetchOffset = 42,
                        PartitionMaxBytes = 1024
                    }
                ]
            }
        ]
    };

    private static byte[] Serialize(FetchRequest request, short version)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        request.Write(ref writer, version);
        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] SerializePartition(FetchRequestPartition partition, short version)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        partition.Write(ref writer, version);
        return buffer.WrittenSpan.ToArray();
    }
}
