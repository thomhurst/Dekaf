using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Metadata;

/// <summary>
/// Unit tests for MetadataManager, focusing on endpoint caching behavior.
/// These tests verify that the endpoint cache is properly invalidated when broker
/// topology changes, including both count changes and membership changes.
/// </summary>
public class MetadataManagerTests
{
    /// <summary>
    /// Creates a minimal MetadataManager for testing.
    /// Uses a null connection pool since we're only testing the caching logic.
    /// </summary>
    private static MetadataManager CreateTestManager()
    {
        // MetadataManager requires a connection pool, but we won't call methods that use it
        // We're only testing GetEndpointsToTry which uses ClusterMetadata
        return new MetadataManager(
            connectionPool: null!,
            bootstrapServers: ["localhost:9092"]);
    }

    /// <summary>
    /// Helper to create a test MetadataResponse with the specified brokers.
    /// </summary>
    private static MetadataResponse CreateMetadataResponse(params (int nodeId, string host, int port)[] brokers)
    {
        var brokerList = new List<BrokerMetadata>();
        foreach (var (nodeId, host, port) in brokers)
        {
            brokerList.Add(new BrokerMetadata
            {
                NodeId = nodeId,
                Host = host,
                Port = port
            });
        }

        return new MetadataResponse
        {
            Brokers = brokerList,
            Topics = Array.Empty<TopicMetadata>()
        };
    }

    [Test]
    public async Task GetEndpointsToTry_InitialState_ReturnsBootstrapServers()
    {
        var manager = CreateTestManager();

        var endpoints = manager.GetEndpointsToTry();

        await Assert.That(endpoints.Count).IsEqualTo(1);
        await Assert.That(endpoints[0]).IsEquivalentTo(("localhost", 9092));
    }

    [Test]
    public async Task GetEndpointsToTry_AfterMetadataUpdate_ReturnsBrokersAndBootstrap()
    {
        var manager = CreateTestManager();

        // Update with broker metadata
        var response = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9092),
            (3, "broker3", 9092));
        manager.Metadata.Update(response);

        var endpoints = manager.GetEndpointsToTry();

        // Should return 3 brokers + 1 bootstrap server = 4 total
        await Assert.That(endpoints.Count).IsEqualTo(4);

        // First 3 should be known brokers
        await Assert.That(endpoints[0]).IsEquivalentTo(("broker1", 9092));
        await Assert.That(endpoints[1]).IsEquivalentTo(("broker2", 9092));
        await Assert.That(endpoints[2]).IsEquivalentTo(("broker3", 9092));

        // Last should be bootstrap server
        await Assert.That(endpoints[3]).IsEquivalentTo(("localhost", 9092));
    }

    [Test]
    public async Task GetEndpointsToTry_CacheHit_ReturnsSameEndpointsWithoutReallocation()
    {
        var manager = CreateTestManager();

        // Update with broker metadata
        var response = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9092));
        manager.Metadata.Update(response);

        // First call builds cache
        var endpoints1 = manager.GetEndpointsToTry();

        // Second call should return the same endpoints (cache hit)
        var endpoints2 = manager.GetEndpointsToTry();

        // Verify endpoints are equal
        await Assert.That(endpoints1.Count).IsEqualTo(endpoints2.Count);
        for (int i = 0; i < endpoints1.Count; i++)
        {
            await Assert.That(endpoints1[i]).IsEquivalentTo(endpoints2[i]);
        }

        // Defensive copy means they're different list instances
        await Assert.That(ReferenceEquals(endpoints1, endpoints2)).IsFalse();
    }

    [Test]
    public async Task GetEndpointsToTry_BrokerCountChanges_InvalidatesCache()
    {
        var manager = CreateTestManager();

        // Initial: 2 brokers
        var response1 = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9092));
        manager.Metadata.Update(response1);
        var endpoints1 = manager.GetEndpointsToTry();

        // Update: 3 brokers (count increased)
        var response2 = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9092),
            (3, "broker3", 9092));
        manager.Metadata.Update(response2);
        var endpoints2 = manager.GetEndpointsToTry();

        // Verify cache was invalidated and new broker appears
        await Assert.That(endpoints1.Count).IsEqualTo(3); // 2 brokers + 1 bootstrap
        await Assert.That(endpoints2.Count).IsEqualTo(4); // 3 brokers + 1 bootstrap

        // Verify new broker is in the list
        await Assert.That(endpoints2.Any(e => e.Host == "broker3" && e.Port == 9092)).IsTrue();
    }

    [Test]
    public async Task GetEndpointsToTry_BrokerMembershipChanges_InvalidatesCache()
    {
        var manager = CreateTestManager();

        // Initial: brokers 1, 2, 3
        var response1 = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9092),
            (3, "broker3", 9092));
        manager.Metadata.Update(response1);
        var endpoints1 = manager.GetEndpointsToTry();

        // Update: broker 3 replaced by broker 4 (same count, different membership)
        // This is the critical scenario fixed by PR #86
        var response2 = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9092),
            (4, "broker4", 9092));  // broker3 replaced by broker4
        manager.Metadata.Update(response2);
        var endpoints2 = manager.GetEndpointsToTry();

        // Count should be the same
        await Assert.That(endpoints1.Count).IsEqualTo(endpoints2.Count);

        // But broker3 should be gone and broker4 should be present
        await Assert.That(endpoints1.Any(e => e.Host == "broker3")).IsTrue();
        await Assert.That(endpoints1.Any(e => e.Host == "broker4")).IsFalse();

        await Assert.That(endpoints2.Any(e => e.Host == "broker3")).IsFalse();
        await Assert.That(endpoints2.Any(e => e.Host == "broker4")).IsTrue();
    }

    [Test]
    public async Task GetEndpointsToTry_BrokerHostOrPortChanges_InvalidatesCache()
    {
        var manager = CreateTestManager();

        // Initial: brokers on port 9092
        var response1 = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9092));
        manager.Metadata.Update(response1);
        var endpoints1 = manager.GetEndpointsToTry();

        // Update: broker2's port changed (same node ID, different port)
        var response2 = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9093));  // port changed
        manager.Metadata.Update(response2);
        var endpoints2 = manager.GetEndpointsToTry();

        // Verify endpoints changed
        var broker2Port1 = endpoints1.FirstOrDefault(e => e.Host == "broker2").Port;
        var broker2Port2 = endpoints2.FirstOrDefault(e => e.Host == "broker2").Port;

        await Assert.That(broker2Port1).IsEqualTo(9092);
        await Assert.That(broker2Port2).IsEqualTo(9093);
    }

    [Test]
    public async Task GetEndpointsToTry_MultipleCalls_ReturnsDefensiveCopy()
    {
        var manager = CreateTestManager();

        var response = CreateMetadataResponse((1, "broker1", 9092));
        manager.Metadata.Update(response);

        var endpoints1 = manager.GetEndpointsToTry();
        var endpoints2 = manager.GetEndpointsToTry();

        // Modifying one list should not affect the other (defensive copy)
        endpoints1.Add(("modified", 1234));

        await Assert.That(endpoints1.Count).IsGreaterThan(endpoints2.Count);
    }

    [Test]
    public async Task GetEndpointsToTry_ThreadSafety_ConcurrentCallsDontCrash()
    {
        var manager = CreateTestManager();

        var response = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9092));
        manager.Metadata.Update(response);

        // Call GetEndpointsToTry concurrently from multiple threads
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    var _ = manager.GetEndpointsToTry();
                }
            }));
        }

        await Task.WhenAll(tasks);

        // If we get here without exceptions, thread-safety is working
        // No assertion needed - successful completion proves thread-safety
    }
}
