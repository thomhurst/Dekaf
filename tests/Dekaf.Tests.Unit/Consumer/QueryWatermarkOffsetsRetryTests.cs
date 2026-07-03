using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class QueryWatermarkOffsetsRetryTests
{
    private const string Topic = "test-topic";
    private static readonly TopicPartition TopicPartition = new(Topic, 0);

    [Test]
    public async Task QueryWatermarkOffsetsAsync_NotLeaderOrFollower_RefreshesMetadataAndRetries()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        var listOffsetCalls = 0;
        var earliestOffsetCalls = 0;

        pool.GetConnectionAsync("localhost", 9092, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(connection));
        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(connection));

        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<MetadataResponse>(CreateMetadataResponse()));

        connection.SendAsync<ListOffsetsRequest, ListOffsetsResponse>(
                Arg.Any<ListOffsetsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                listOffsetCalls++;
                var request = (ListOffsetsRequest)_[0]!;
                var timestamp = request.Topics[0].Partitions[0].Timestamp;

                if (timestamp == -2)
                {
                    earliestOffsetCalls++;
                    return new ValueTask<ListOffsetsResponse>(earliestOffsetCalls == 1
                        ? CreateListOffsetsResponse(ErrorCode.NotLeaderOrFollower, -1)
                        : CreateListOffsetsResponse(ErrorCode.None, 10));
                }

                return new ValueTask<ListOffsetsResponse>(CreateListOffsetsResponse(ErrorCode.None, 42));
            });

        await using var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        metadataManager.SetApiVersion(
            ApiKey.ListOffsets,
            ListOffsetsRequest.LowestSupportedVersion,
            ListOffsetsRequest.HighestSupportedVersion);
        SetMetadataApiVersion(metadataManager);
        metadataManager.Metadata.Update(CreateMetadataResponse());

        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "test-consumer"
            },
            Serializers.String,
            Serializers.String,
            pool,
            metadataManager);
        SetInitialized(consumer);

        var watermarks = await consumer.QueryWatermarkOffsetsAsync(TopicPartition);

        await Assert.That(watermarks.Low).IsEqualTo(10);
        await Assert.That(watermarks.High).IsEqualTo(42);
        await Assert.That(listOffsetCalls).IsEqualTo(4);
        _ = connection.Received(1).SendAsync<MetadataRequest, MetadataResponse>(
            Arg.Any<MetadataRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    private static void SetInitialized(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_initialized field not found");

        field.SetValue(consumer, true);
    }

    private static void SetMetadataApiVersion(MetadataManager metadataManager)
    {
        var field = typeof(MetadataManager)
            .GetField("_metadataApiVersion", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_metadataApiVersion field not found");

        field.SetValue(metadataManager, MetadataRequest.HighestSupportedVersion);
    }

    private static MetadataResponse CreateMetadataResponse()
        => new()
        {
            ClusterId = "test-cluster",
            ControllerId = 1,
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }
            ],
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = Topic,
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 0,
                            LeaderId = 1,
                            LeaderEpoch = 1,
                            ReplicaNodes = [1],
                            IsrNodes = [1]
                        }
                    ]
                }
            ]
        };

    private static ListOffsetsResponse CreateListOffsetsResponse(ErrorCode errorCode, long offset)
        => new()
        {
            Topics =
            [
                new ListOffsetsResponseTopic
                {
                    Name = Topic,
                    Partitions =
                    [
                        new ListOffsetsResponsePartition
                        {
                            PartitionIndex = 0,
                            ErrorCode = errorCode,
                            Offset = offset
                        }
                    ]
                }
            ]
        };
}
