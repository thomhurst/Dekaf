using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Metadata;

public sealed class ClusterMetadataTests
{
    #region Initial State

    [Test]
    public async Task NewClusterMetadata_ClusterId_IsNull()
    {
        var metadata = new ClusterMetadata();
        await Assert.That(metadata.ClusterId).IsNull();
    }

    [Test]
    public async Task NewClusterMetadata_ControllerId_IsNegativeOne()
    {
        var metadata = new ClusterMetadata();
        await Assert.That(metadata.ControllerId).IsEqualTo(-1);
    }

    [Test]
    public async Task NewClusterMetadata_GetBrokers_ReturnsEmpty()
    {
        var metadata = new ClusterMetadata();
        await Assert.That(metadata.GetBrokers()).Count().IsEqualTo(0);
    }

    [Test]
    public async Task NewClusterMetadata_GetTopics_ReturnsEmpty()
    {
        var metadata = new ClusterMetadata();
        await Assert.That(metadata.GetTopics()).Count().IsEqualTo(0);
    }

    #endregion

    #region Update

    [Test]
    public async Task Update_SetsClusterId()
    {
        var metadata = new ClusterMetadata();
        metadata.Update(CreateMetadataResponse(clusterId: "test-cluster"));

        await Assert.That(metadata.ClusterId).IsEqualTo("test-cluster");
    }

    [Test]
    public async Task Update_SetsControllerId()
    {
        var metadata = new ClusterMetadata();
        metadata.Update(CreateMetadataResponse(controllerId: 42));

        await Assert.That(metadata.ControllerId).IsEqualTo(42);
    }

    [Test]
    public async Task Update_SetsLastRefreshed()
    {
        var metadata = new ClusterMetadata();
        var before = DateTimeOffset.UtcNow;
        metadata.Update(CreateMetadataResponse());
        var after = DateTimeOffset.UtcNow;

        await Assert.That(metadata.LastRefreshed).IsGreaterThanOrEqualTo(before);
        await Assert.That(metadata.LastRefreshed).IsLessThanOrEqualTo(after);
    }

    #endregion

    #region GetBroker

    [Test]
    public async Task GetBroker_ExistingBroker_ReturnsBroker()
    {
        var metadata = new ClusterMetadata();
        metadata.Update(CreateMetadataResponse());

        var broker = metadata.GetBroker(1);

        await Assert.That(broker).IsNotNull();
        await Assert.That(broker!.NodeId).IsEqualTo(1);
        await Assert.That(broker.Host).IsEqualTo("broker1");
        await Assert.That(broker.Port).IsEqualTo(9092);
    }

    [Test]
    public async Task GetBroker_NonExistentBroker_ReturnsNull()
    {
        var metadata = new ClusterMetadata();
        metadata.Update(CreateMetadataResponse());

        var broker = metadata.GetBroker(999);

        await Assert.That(broker).IsNull();
    }

    #endregion

    #region GetBrokers

    [Test]
    public async Task GetBrokers_ReturnsAllBrokers()
    {
        var metadata = new ClusterMetadata();
        metadata.Update(CreateMetadataResponse());

        var brokers = metadata.GetBrokers();

        await Assert.That(brokers).Count().IsEqualTo(3);
    }

    #endregion

    #region GetTopic by Name

    [Test]
    public async Task GetTopic_ByName_ExistingTopic_ReturnsTopic()
    {
        var metadata = new ClusterMetadata();
        metadata.Update(CreateMetadataResponse());

        var topic = metadata.GetTopic("test-topic");

        await Assert.That(topic).IsNotNull();
        await Assert.That(topic!.Name).IsEqualTo("test-topic");
    }

    [Test]
    public async Task GetTopic_ByName_NonExistentTopic_ReturnsNull()
    {
        var metadata = new ClusterMetadata();
        metadata.Update(CreateMetadataResponse());

        var topic = metadata.GetTopic("nonexistent");

        await Assert.That(topic).IsNull();
    }

    [Test]
    public async Task GetTopic_ByName_ReturnsCorrectPartitions()
    {
        var metadata = new ClusterMetadata();
        metadata.Update(CreateMetadataResponse());

        var topic = metadata.GetTopic("test-topic");

        await Assert.That(topic!.Partitions).Count().IsEqualTo(3);
        await Assert.That(topic.PartitionCount).IsEqualTo(3);
    }

    #endregion

    #region GetTopic by ID

    [Test]
    public async Task GetTopic_ById_ExistingTopic_ReturnsTopic()
    {
        var topicId = Guid.NewGuid();
        var metadata = new ClusterMetadata();
        metadata.Update(CreateMetadataResponse(topicId: topicId));

        var topic = metadata.GetTopic(topicId);

        await Assert.That(topic).IsNotNull();
        await Assert.That(topic!.TopicId).IsEqualTo(topicId);
    }

    [Test]
    public async Task GetTopic_ById_NonExistentId_ReturnsNull()
    {
        var metadata = new ClusterMetadata();
        metadata.Update(CreateMetadataResponse());

        var topic = metadata.GetTopic(Guid.NewGuid());

        await Assert.That(topic).IsNull();
    }

    [Test]
    public async Task GetTopic_ById_EmptyGuid_ReturnsNull()
    {
        var metadata = new ClusterMetadata();
        metadata.Update(CreateMetadataResponse());

        var topic = metadata.GetTopic(Guid.Empty);

        await Assert.That(topic).IsNull();
    }

    #endregion

    #region GetTopics

    [Test]
    public async Task GetTopics_ReturnsAllTopics()
    {
        var metadata = new ClusterMetadata();
        metadata.Update(CreateMetadataResponseMultipleTopics());

        var topics = metadata.GetTopics();

        await Assert.That(topics).Count().IsEqualTo(2);
    }

    #endregion

    #region GetPartitionLeader

    [Test]
    public async Task GetPartitionLeader_ValidPartition_ReturnsBroker()
    {
        var metadata = new ClusterMetadata();
        metadata.Update(CreateMetadataResponse());

        var leader = metadata.GetPartitionLeader("test-topic", 0);

        await Assert.That(leader).IsNotNull();
        await Assert.That(leader!.NodeId).IsEqualTo(1);
    }

    [Test]
    public async Task GetPartitionLeader_NonExistentTopic_ReturnsNull()
    {
        var metadata = new ClusterMetadata();
        metadata.Update(CreateMetadataResponse());

        var leader = metadata.GetPartitionLeader("nonexistent", 0);

        await Assert.That(leader).IsNull();
    }

    [Test]
    public async Task GetPartitionLeader_NonExistentPartition_ReturnsNull()
    {
        var metadata = new ClusterMetadata();
        metadata.Update(CreateMetadataResponse());

        var leader = metadata.GetPartitionLeader("test-topic", 999);

        await Assert.That(leader).IsNull();
    }

    [Test]
    public async Task GetPartitionLeader_LeaderBrokerNotInCluster_ReturnsNull()
    {
        var metadata = new ClusterMetadata();
        // Create response where partition leader ID doesn't match any broker
        var response = new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "broker1", Port = 9092 }],
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = "test-topic",
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 0,
                            LeaderId = 999, // Not in brokers
                            ReplicaNodes = [999],
                            IsrNodes = [999]
                        }
                    ]
                }
            ]
        };
        metadata.Update(response);

        var leader = metadata.GetPartitionLeader("test-topic", 0);

        await Assert.That(leader).IsNull();
    }

    #endregion

    #region Update Replaces Previous State

    [Test]
    public async Task Update_ReplacesAllPreviousData()
    {
        var metadata = new ClusterMetadata();

        // First update
        metadata.Update(CreateMetadataResponse(clusterId: "cluster-1"));
        await Assert.That(metadata.ClusterId).IsEqualTo("cluster-1");
        await Assert.That(metadata.GetBrokers()).Count().IsEqualTo(3);

        // Second update with different data
        metadata.Update(new MetadataResponse
        {
            ClusterId = "cluster-2",
            ControllerId = 10,
            Brokers = [new BrokerMetadata { NodeId = 10, Host = "new-broker", Port = 9093 }],
            Topics = []
        });

        await Assert.That(metadata.ClusterId).IsEqualTo("cluster-2");
        await Assert.That(metadata.ControllerId).IsEqualTo(10);
        await Assert.That(metadata.GetBrokers()).Count().IsEqualTo(1);
        await Assert.That(metadata.GetTopics()).Count().IsEqualTo(0);
        await Assert.That(metadata.GetBroker(1)).IsNull(); // Old broker gone
    }

    #endregion

    #region Helpers

    private static MetadataResponse CreateMetadataResponse(
        string? clusterId = "test-cluster",
        int controllerId = 1,
        Guid topicId = default)
    {
        return new MetadataResponse
        {
            ClusterId = clusterId,
            ControllerId = controllerId,
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "broker1", Port = 9092 },
                new BrokerMetadata { NodeId = 2, Host = "broker2", Port = 9092 },
                new BrokerMetadata { NodeId = 3, Host = "broker3", Port = 9092, Rack = "rack-a" }
            ],
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = "test-topic",
                    TopicId = topicId,
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 0,
                            LeaderId = 1,
                            LeaderEpoch = 5,
                            ReplicaNodes = [1, 2, 3],
                            IsrNodes = [1, 2, 3]
                        },
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 1,
                            LeaderId = 2,
                            LeaderEpoch = 5,
                            ReplicaNodes = [1, 2, 3],
                            IsrNodes = [1, 2]
                        },
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 2,
                            LeaderId = 3,
                            LeaderEpoch = 5,
                            ReplicaNodes = [1, 2, 3],
                            IsrNodes = [1, 2, 3]
                        }
                    ]
                }
            ]
        };
    }

    private static MetadataResponse CreateMetadataResponseMultipleTopics()
    {
        return new MetadataResponse
        {
            ClusterId = "test-cluster",
            ControllerId = 1,
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "broker1", Port = 9092 }
            ],
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = "topic-a",
                    Partitions =
                    [
                        new PartitionMetadata { ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = 1, ReplicaNodes = [1], IsrNodes = [1] }
                    ]
                },
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = "topic-b",
                    Partitions =
                    [
                        new PartitionMetadata { ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = 1, ReplicaNodes = [1], IsrNodes = [1] }
                    ]
                }
            ]
        };
    }

    #endregion
}
