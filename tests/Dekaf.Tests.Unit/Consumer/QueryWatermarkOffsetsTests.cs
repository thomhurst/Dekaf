using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class QueryWatermarkOffsetsTests
{
    private const string Topic = "watermark-topic";
    private const int Partition = 0;
    private const long EarliestOffsetTimestamp = -2;
    private const long LatestOffsetTimestamp = -1;

    [Test]
    public async Task QueryWatermarkOffsetsAsync_StartsEarliestAndLatestRequestsBeforeAwaitingResponses()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        connectionPool.GetConnectionByIndexAsync(0, 0, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));

        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.SetApiVersion(ApiKey.ListOffsets, ListOffsetsRequest.LowestSupportedVersion, ListOffsetsRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(CreateMetadataResponse());

        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "test-group"
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
        SetInitialized(consumer);

        var earliestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var latestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponses = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        connection.SendAsync<ListOffsetsRequest, ListOffsetsResponse>(
                Arg.Any<ListOffsetsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var request = ci.Arg<ListOffsetsRequest>();
                var timestamp = request.Topics[0].Partitions[0].Timestamp;

                if (timestamp == EarliestOffsetTimestamp)
                    earliestStarted.TrySetResult();
                else if (timestamp == LatestOffsetTimestamp)
                    latestStarted.TrySetResult();
                else
                    throw new InvalidOperationException($"Unexpected ListOffsets timestamp {timestamp}");

                return new ValueTask<ListOffsetsResponse>(CreateListOffsetsResponseAsync(timestamp, releaseResponses.Task));
            });

        var queryTask = consumer.QueryWatermarkOffsetsAsync(new TopicPartition(Topic, Partition), CancellationToken.None).AsTask();

        await earliestStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        try
        {
            await latestStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }
        catch
        {
            releaseResponses.TrySetResult();
            await queryTask.WaitAsync(TimeSpan.FromSeconds(1));
            throw;
        }

        releaseResponses.SetResult();
        var watermarks = await queryTask.WaitAsync(TimeSpan.FromSeconds(1));

        await Assert.That(watermarks.Low).IsEqualTo(10);
        await Assert.That(watermarks.High).IsEqualTo(42);
    }

    private static async Task<ListOffsetsResponse> CreateListOffsetsResponseAsync(long timestamp, Task release)
    {
        await release.ConfigureAwait(false);
        var offset = timestamp == EarliestOffsetTimestamp ? 10 : 42;

        return new ListOffsetsResponse
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
                            PartitionIndex = Partition,
                            ErrorCode = ErrorCode.None,
                            Offset = offset
                        }
                    ]
                }
            ]
        };
    }

    private static MetadataResponse CreateMetadataResponse() => new()
    {
        Brokers =
        [
            new BrokerMetadata
            {
                NodeId = 0,
                Host = "localhost",
                Port = 9092
            }
        ],
        Topics =
        [
            new TopicMetadata
            {
                Name = Topic,
                ErrorCode = ErrorCode.None,
                Partitions =
                [
                    new PartitionMetadata
                    {
                        PartitionIndex = Partition,
                        LeaderId = 0,
                        ErrorCode = ErrorCode.None,
                        ReplicaNodes = [0],
                        IsrNodes = [0]
                    }
                ]
            }
        ]
    };

    private static void SetInitialized(KafkaConsumer<string, string> consumer)
    {
        var initializedField = typeof(KafkaConsumer<string, string>)
            .GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_initialized field not found - was it renamed?");

        initializedField.SetValue(consumer, true);
    }
}
