using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

public sealed partial class ConsumerAssignmentFastPathTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EnsureAssignmentAsync_SeekDuringBatchedResetWins(bool committed)
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(pool, connection);
        await using var metadata = CreateMetadataManager(pool);
        metadata.SetApiVersion(ApiKey.ListOffsets,
            ListOffsetsRequest.LowestSupportedVersion, ListOffsetsRequest.HighestSupportedVersion);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment(0, 1));
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateOffsetFetchResponse((0, committed ? 10 : -1), (1, -1))));
        await using var consumer = CreateGroupConsumer(pool, metadata, autoOffsetReset: AutoOffsetReset.Earliest);
        consumer.Subscribe("test-topic");
        connection.SendAsync<ListOffsetsRequest, ListOffsetsResponse>(
                Arg.Any<ListOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                // Both partitions are assigned, but no position has been published yet.
                consumer.Seek(new TopicPartitionOffset("test-topic", 0, 42, 7));
                return ValueTask.FromResult(CreateOffsetResetResponse(call.Arg<ListOffsetsRequest>(),
                    static partition => (100 + partition.PartitionIndex, ErrorCode.None)));
            });

        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var partition = new TopicPartition("test-topic", 0);
        await Assert.That(consumer.GetPosition(partition)).IsEqualTo(42L);
        await Assert.That(GetFetchPositions(consumer)[partition]).IsEqualTo(42L);
        await Assert.That(GetLastConsumedLeaderEpoch(consumer, partition)).IsEqualTo(7);
        await Assert.That(GetFetchPositions(consumer)[new TopicPartition("test-topic", 1)]).IsEqualTo(101L);
    }

    [Test]
    public async Task ResolveSpecialOffsetsAsync_MissingLeadersRefreshDistinctTopicNamesTogether()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(pool, connection);
        await using var metadata = CreateMetadataManager(pool);
        SetMetadataApiVersion(metadata);
        metadata.SetApiVersion(ApiKey.ListOffsets,
            ListOffsetsRequest.LowestSupportedVersion, ListOffsetsRequest.HighestSupportedVersion);
        var metadataRequests = new List<MetadataRequest>();
        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<MetadataRequest>();
                metadataRequests.Add(request);
                return ValueTask.FromResult(new MetadataResponse
                {
                    Brokers = [new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }],
                    // A cluster-wide request does not create either absent topic.
                    Topics = request.Topics is null ? [] : request.Topics.Select(topic => new TopicMetadata
                    {
                        Name = topic.Name!,
                        ErrorCode = ErrorCode.None,
                        Partitions = Enumerable.Range(0, 2).Select(index => new PartitionMetadata
                        {
                            PartitionIndex = index, LeaderId = 0, ErrorCode = ErrorCode.None,
                            ReplicaNodes = [0], IsrNodes = [0]
                        }).ToArray()
                    }).ToArray()
                });
            });
        connection.SendAsync<ListOffsetsRequest, ListOffsetsResponse>(
                Arg.Any<ListOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult(CreateOffsetResetResponse(call.Arg<ListOffsetsRequest>(),
                static _ => (100, ErrorCode.None))));
        await using var consumer = CreateGroupConsumer(pool, metadata, retryBackoffMs: 0, retryBackoffMaxMs: 0);
        var partitions = new List<TopicPartition>
        {
            new("missing-a", 0), new("missing-a", 1), new("missing-b", 0), new("test-topic", 0)
        };
        consumer.Assign(partitions.ToArray());
        consumer.SeekToBeginning(partitions.ToArray());

        await InvokeResolveSpecialOffsetsAsync(consumer, partitions);

        await Assert.That(metadataRequests.Count).IsEqualTo(1);
        await Assert.That(metadataRequests[0].AllowAutoTopicCreation).IsTrue();
        await Assert.That(metadataRequests[0].Topics!.Select(topic => topic.Name!))
            .IsEquivalentTo(["missing-a", "missing-b"]);
        foreach (var partition in partitions)
            await Assert.That(GetFetchPositions(consumer)[partition]).IsEqualTo(100L);
    }

    [Test]
    public async Task ManualAssignmentInitialization_PositionRemovedDuringBatchedResetSkipsAbsentReset()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(pool, connection);
        await using var metadata = CreateMetadataManager(pool);
        metadata.SetApiVersion(ApiKey.ListOffsets,
            ListOffsetsRequest.LowestSupportedVersion, ListOffsetsRequest.HighestSupportedVersion);
        SetupFindCoordinator(connection);
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateOffsetFetchResponse((0, -1), (1, -1))));
        await using var consumer = CreateGroupConsumer(pool, metadata, autoOffsetReset: AutoOffsetReset.Earliest);
        var positioned = new TopicPartition("test-topic", 0);
        var reset = new TopicPartition("test-topic", 1);
        consumer.Assign(positioned, reset);
        consumer.Seek(new TopicPartitionOffset(positioned.Topic, positioned.Partition, 42));
        connection.SendAsync<ListOffsetsRequest, ListOffsetsResponse>(
                Arg.Any<ListOffsetsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                GetFetchPositions(consumer).TryRemove(positioned, out _);
                return ValueTask.FromResult(CreateOffsetResetResponse(call.Arg<ListOffsetsRequest>(),
                    static _ => (100, ErrorCode.None)));
            });
        var initialize = typeof(KafkaConsumer<string, string>).GetMethod(
            "InitializeManualAssignmentPositionsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (ValueTask)initialize.Invoke(consumer, [new List<TopicPartition> { positioned, reset }, CancellationToken.None])!;

        await Assert.That(GetFetchPositions(consumer).ContainsKey(positioned)).IsFalse();
        await Assert.That(GetFetchPositions(consumer)[reset]).IsEqualTo(100L);
    }
}
