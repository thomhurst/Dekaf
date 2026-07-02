using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerLeaderDiscoveryTests
{
    private const string Topic = "test-topic";

    [Test]
    public async Task HandleNotLeaderOrFollower_WithInlineLeader_UpdatesMetadataWithoutRefresh()
    {
        var pool = Substitute.For<IConnectionPool>();
        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);

        SeedMetadata(metadataManager);

        var partitionResponse = new FetchResponsePartition
        {
            PartitionIndex = 0,
            ErrorCode = ErrorCode.NotLeaderOrFollower,
            CurrentLeader = new LeaderIdAndEpoch
            {
                LeaderId = 2,
                LeaderEpoch = 6
            }
        };

        await InvokeHandleNotLeaderOrFollowerAsync(
            consumer,
            partitionResponse,
            [new NodeEndpoint { NodeId = 2, Host = "broker-2", Port = 9094 }]);

        var leader = metadataManager.Metadata.GetPartitionLeader(Topic, 0);

        await Assert.That(leader).IsNotNull();
        await Assert.That(leader!.NodeId).IsEqualTo(2);
        await Assert.That(leader.Host).IsEqualTo("broker-2");
        _ = pool.DidNotReceive().GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleNotLeaderOrFollower_WithoutInlineLeader_DeduplicatesRefreshPerTopic()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connectionRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseConnection = new TaskCompletionSource<IKafkaConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

        pool.GetConnectionAsync("localhost", 9092, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                connectionRequested.TrySetResult();
                return new ValueTask<IKafkaConnection>(releaseConnection.Task);
            });

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);

        var partitionResponse = new FetchResponsePartition
        {
            PartitionIndex = 0,
            ErrorCode = ErrorCode.NotLeaderOrFollower
        };

        await InvokeHandleNotLeaderOrFollowerAsync(consumer, partitionResponse, []);
        await connectionRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await InvokeHandleNotLeaderOrFollowerAsync(consumer, partitionResponse, []);
        await Task.Delay(50);

        _ = pool.Received(1).GetConnectionAsync("localhost", 9092, Arg.Any<CancellationToken>());

        releaseConnection.SetException(new InvalidOperationException("stop refresh"));
        await Task.Delay(50);
    }

    private static MetadataManager CreateMetadataManager(IConnectionPool pool)
        => new(pool, ["localhost:9092"]);

    private static KafkaConsumer<string, string> CreateConsumer(
        IConnectionPool pool,
        MetadataManager metadataManager)
        => new(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "test-consumer"
            },
            Serializers.String,
            Serializers.String,
            pool,
            metadataManager);

    private static void SeedMetadata(MetadataManager metadataManager)
    {
        metadataManager.Metadata.Update(new MetadataResponse
        {
            ClusterId = "test-cluster",
            ControllerId = 1,
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9093 }
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
                            LeaderEpoch = 5,
                            ReplicaNodes = [1],
                            IsrNodes = [1]
                        }
                    ]
                }
            ]
        });
    }

    private static async ValueTask InvokeHandleNotLeaderOrFollowerAsync(
        KafkaConsumer<string, string> consumer,
        FetchResponsePartition partitionResponse,
        IReadOnlyList<NodeEndpoint> nodeEndpoints)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("HandleNotLeaderOrFollowerAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("HandleNotLeaderOrFollowerAsync method not found");

        var result = method.Invoke(consumer, [Topic, partitionResponse, nodeEndpoints, CancellationToken.None]);
        if (result is not ValueTask valueTask)
            throw new InvalidOperationException("HandleNotLeaderOrFollowerAsync did not return ValueTask");

        await valueTask.ConfigureAwait(false);
    }
}
