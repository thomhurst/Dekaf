using System.Buffers;
using System.Reflection;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;
using NSubstitute;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareConsumerRecordPoolingTests
{
    [Test]
    [NotInParallel]
    public async Task ParsePartitionRecords_DeserializerThrows_ReturnsBatchToPool()
    {
        var expectedBatch = RecordBatch.RentFromPool();
        expectedBatch.ReturnToPool();

        var buffer = new ArrayBufferWriter<byte>();
        using var source = new RecordBatch
        {
            BaseOffset = 17,
            Records = [new Record { IsKeyNull = true, Value = "value"u8.ToArray() }]
        };
        source.Write(buffer);

        var options = new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "share-pooling-test"
        };
        var pool = Substitute.For<IConnectionPool>();
        await using var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        var valueDeserializer = Substitute.For<IDeserializer<string>>();
        valueDeserializer.Deserialize(
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<SerializationContext>())
            .Returns(_ => throw new InvalidOperationException("Deserializer failure"));
        await using var consumer = new KafkaShareConsumer<string, string>(
            options,
            Serializers.String,
            valueDeserializer,
            pool,
            metadataManager);

        var method = typeof(KafkaShareConsumer<string, string>).GetMethod(
            "ParsePartitionRecords",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        TargetInvocationException? thrown = null;
        try
        {
            method.Invoke(consumer,
            [
                new TopicInfo { Name = "topic", Partitions = [] },
                new ShareFetchResponsePartition
                {
                    PartitionIndex = 0,
                    CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
                    RecordBytes = buffer.WrittenMemory,
                    AcquiredRecords =
                    [
                        new ShareFetchAcquiredRecords
                        {
                            FirstOffset = 17,
                            LastOffset = 17,
                            DeliveryCount = 1
                        }
                    ]
                },
                1
            ]);
        }
        catch (TargetInvocationException exception)
        {
            thrown = exception;
        }

        await Assert.That(thrown?.InnerException).IsTypeOf<InvalidOperationException>();

        var returnedBatch = RecordBatch.RentFromPool();
        await Assert.That(returnedBatch).IsSameReferenceAs(expectedBatch);
        returnedBatch.ReturnToPool();
    }
}
