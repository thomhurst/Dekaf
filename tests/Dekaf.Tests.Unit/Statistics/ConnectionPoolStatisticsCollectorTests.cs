using Dekaf.Statistics;

namespace Dekaf.Tests.Unit.Statistics;

public class ConnectionPoolStatisticsCollectorTests
{
    [Test]
    public async Task RecordConnectionCreated_IncrementsGlobalAndBrokerCounters()
    {
        var collector = new ConnectionPoolStatisticsCollector();

        collector.RecordConnectionCreated(1, "broker-1", 9092);

        var stats = collector.Collect();
        await Assert.That(stats.ActiveConnections).IsEqualTo(1);
        await Assert.That(stats.TotalConnectionsCreated).IsEqualTo(1);
        await Assert.That(stats.Brokers).ContainsKey(1);
        await Assert.That(stats.Brokers[1].ConnectionCount).IsEqualTo(1);
        await Assert.That(stats.Brokers[1].TotalConnectionsCreated).IsEqualTo(1);
        await Assert.That(stats.Brokers[1].Host).IsEqualTo("broker-1");
        await Assert.That(stats.Brokers[1].Port).IsEqualTo(9092);
    }

    [Test]
    public async Task RecordConnectionClosed_DecrementsActiveConnections()
    {
        var collector = new ConnectionPoolStatisticsCollector();

        collector.RecordConnectionCreated(1, "broker-1", 9092);
        collector.RecordConnectionClosed(1);

        var stats = collector.Collect();
        await Assert.That(stats.ActiveConnections).IsEqualTo(0);
        await Assert.That(stats.TotalConnectionsCreated).IsEqualTo(1);
        await Assert.That(stats.TotalConnectionsClosed).IsEqualTo(1);
        await Assert.That(stats.Brokers[1].ConnectionCount).IsEqualTo(0);
        await Assert.That(stats.Brokers[1].TotalConnectionsClosed).IsEqualTo(1);
    }

    [Test]
    public async Task RecordRequestSent_IncrementsPendingAndTotalRequests()
    {
        var collector = new ConnectionPoolStatisticsCollector();

        collector.RecordConnectionCreated(1, "broker-1", 9092);
        collector.RecordRequestSent(1);

        var stats = collector.Collect();
        await Assert.That(stats.Brokers[1].PendingRequests).IsEqualTo(1);
        await Assert.That(stats.Brokers[1].TotalRequestsSent).IsEqualTo(1);
    }

    [Test]
    public async Task RecordResponseReceived_DecrementsPendingRequests()
    {
        var collector = new ConnectionPoolStatisticsCollector();

        collector.RecordConnectionCreated(1, "broker-1", 9092);
        collector.RecordRequestSent(1);
        collector.RecordRequestSent(1);
        collector.RecordResponseReceived(1);

        var stats = collector.Collect();
        await Assert.That(stats.Brokers[1].PendingRequests).IsEqualTo(1);
        await Assert.That(stats.Brokers[1].TotalRequestsSent).IsEqualTo(2);
        await Assert.That(stats.Brokers[1].TotalResponsesReceived).IsEqualTo(1);
    }

    [Test]
    public async Task RecordError_IncrementsErrorCounters()
    {
        var collector = new ConnectionPoolStatisticsCollector();

        collector.RecordConnectionCreated(1, "broker-1", 9092);
        collector.RecordError(1);
        collector.RecordError(1);

        var stats = collector.Collect();
        await Assert.That(stats.TotalErrors).IsEqualTo(2);
        await Assert.That(stats.Brokers[1].TotalErrors).IsEqualTo(2);
    }

    [Test]
    public async Task MultipleBrokers_TrackedIndependently()
    {
        var collector = new ConnectionPoolStatisticsCollector();

        collector.RecordConnectionCreated(1, "broker-1", 9092);
        collector.RecordConnectionCreated(2, "broker-2", 9093);
        collector.RecordConnectionCreated(2, "broker-2", 9093);

        collector.RecordRequestSent(1);
        collector.RecordRequestSent(2);
        collector.RecordRequestSent(2);
        collector.RecordRequestSent(2);

        var stats = collector.Collect();
        await Assert.That(stats.ActiveConnections).IsEqualTo(3);
        await Assert.That(stats.TotalConnectionsCreated).IsEqualTo(3);
        await Assert.That(stats.Brokers).Count().IsEqualTo(2);

        await Assert.That(stats.Brokers[1].ConnectionCount).IsEqualTo(1);
        await Assert.That(stats.Brokers[1].TotalRequestsSent).IsEqualTo(1);
        await Assert.That(stats.Brokers[1].PendingRequests).IsEqualTo(1);

        await Assert.That(stats.Brokers[2].ConnectionCount).IsEqualTo(2);
        await Assert.That(stats.Brokers[2].TotalRequestsSent).IsEqualTo(3);
        await Assert.That(stats.Brokers[2].PendingRequests).IsEqualTo(3);
    }

    [Test]
    public async Task IdleConnections_CalculatedCorrectly_WhenNoPendingRequests()
    {
        var collector = new ConnectionPoolStatisticsCollector();

        collector.RecordConnectionCreated(1, "broker-1", 9092);
        collector.RecordConnectionCreated(2, "broker-2", 9093);

        var stats = collector.Collect();
        await Assert.That(stats.ActiveConnections).IsEqualTo(2);
        await Assert.That(stats.IdleConnections).IsEqualTo(2);
    }

    [Test]
    public async Task IdleConnections_CalculatedCorrectly_WithPendingRequests()
    {
        var collector = new ConnectionPoolStatisticsCollector();

        // Broker 1: 2 connections, 1 pending request -> 1 idle
        collector.RecordConnectionCreated(1, "broker-1", 9092);
        collector.RecordConnectionCreated(1, "broker-1", 9092);
        collector.RecordRequestSent(1);

        // Broker 2: 1 connection, 0 pending requests -> 1 idle
        collector.RecordConnectionCreated(2, "broker-2", 9093);

        var stats = collector.Collect();
        await Assert.That(stats.ActiveConnections).IsEqualTo(3);
        await Assert.That(stats.IdleConnections).IsEqualTo(2);
    }

    [Test]
    public async Task IdleConnections_ZeroWhenAllBusy()
    {
        var collector = new ConnectionPoolStatisticsCollector();

        collector.RecordConnectionCreated(1, "broker-1", 9092);
        collector.RecordRequestSent(1);
        collector.RecordRequestSent(1);

        var stats = collector.Collect();
        await Assert.That(stats.ActiveConnections).IsEqualTo(1);
        await Assert.That(stats.IdleConnections).IsEqualTo(0);
    }

    [Test]
    public async Task Collect_ReturnsEmptyBrokers_WhenNoConnectionsCreated()
    {
        var collector = new ConnectionPoolStatisticsCollector();

        var stats = collector.Collect();
        await Assert.That(stats.ActiveConnections).IsEqualTo(0);
        await Assert.That(stats.IdleConnections).IsEqualTo(0);
        await Assert.That(stats.TotalConnectionsCreated).IsEqualTo(0);
        await Assert.That(stats.TotalConnectionsClosed).IsEqualTo(0);
        await Assert.That(stats.TotalErrors).IsEqualTo(0);
        await Assert.That(stats.Brokers).IsEmpty();
    }

    [Test]
    public async Task RecordConnectionClosed_ForUnknownBroker_DoesNotThrow()
    {
        var collector = new ConnectionPoolStatisticsCollector();

        // Should not throw - unknown broker is a no-op for per-broker tracking
        collector.RecordConnectionClosed(999);

        var stats = collector.Collect();
        await Assert.That(stats.TotalConnectionsClosed).IsEqualTo(1);
    }

    [Test]
    public async Task RecordRequestSent_ForUnknownBroker_DoesNotThrow()
    {
        var collector = new ConnectionPoolStatisticsCollector();

        // Should not throw - unknown broker is a no-op for per-broker tracking
        collector.RecordRequestSent(999);

        var stats = collector.Collect();
        await Assert.That(stats.Brokers).IsEmpty();
    }

    [Test]
    public async Task RecordError_ForUnknownBroker_IncrementsGlobalOnly()
    {
        var collector = new ConnectionPoolStatisticsCollector();

        collector.RecordError(999);

        var stats = collector.Collect();
        await Assert.That(stats.TotalErrors).IsEqualTo(1);
    }

    [Test]
    public async Task ConcurrentUpdates_AreThreadSafe()
    {
        var collector = new ConnectionPoolStatisticsCollector();
        const int threadCount = 10;
        const int operationsPerThread = 1000;

        // Pre-create broker entries
        collector.RecordConnectionCreated(1, "broker-1", 9092);
        collector.RecordConnectionCreated(2, "broker-2", 9093);

        var tasks = new List<Task>();
        for (var t = 0; t < threadCount; t++)
        {
            var brokerId = (t % 2) + 1;
            tasks.Add(Task.Run(() =>
            {
                for (var i = 0; i < operationsPerThread; i++)
                {
                    collector.RecordRequestSent(brokerId);
                    collector.RecordResponseReceived(brokerId);
                }
            }));
        }

        await Task.WhenAll(tasks);

        var stats = collector.Collect();
        var totalRequests = stats.Brokers[1].TotalRequestsSent + stats.Brokers[2].TotalRequestsSent;
        var totalResponses = stats.Brokers[1].TotalResponsesReceived + stats.Brokers[2].TotalResponsesReceived;

        await Assert.That(totalRequests).IsEqualTo(threadCount * operationsPerThread);
        await Assert.That(totalResponses).IsEqualTo(threadCount * operationsPerThread);

        // All requests should be completed, so pending should be 0
        await Assert.That(stats.Brokers[1].PendingRequests).IsEqualTo(0);
        await Assert.That(stats.Brokers[2].PendingRequests).IsEqualTo(0);
    }

    [Test]
    public async Task Collect_MultipleSnapshots_ReturnsCumulativeCounters()
    {
        var collector = new ConnectionPoolStatisticsCollector();

        collector.RecordConnectionCreated(1, "broker-1", 9092);
        collector.RecordRequestSent(1);

        var stats1 = collector.Collect();
        await Assert.That(stats1.TotalConnectionsCreated).IsEqualTo(1);
        await Assert.That(stats1.Brokers[1].TotalRequestsSent).IsEqualTo(1);

        collector.RecordRequestSent(1);
        collector.RecordResponseReceived(1);

        var stats2 = collector.Collect();
        await Assert.That(stats2.TotalConnectionsCreated).IsEqualTo(1);
        await Assert.That(stats2.Brokers[1].TotalRequestsSent).IsEqualTo(2);
        await Assert.That(stats2.Brokers[1].TotalResponsesReceived).IsEqualTo(1);
        await Assert.That(stats2.Brokers[1].PendingRequests).IsEqualTo(1);
    }

    [Test]
    public async Task BrokerConnectionStatistics_NodeIdMatchesBrokerId()
    {
        var collector = new ConnectionPoolStatisticsCollector();

        collector.RecordConnectionCreated(42, "my-broker", 9092);

        var stats = collector.Collect();
        await Assert.That(stats.Brokers[42].NodeId).IsEqualTo(42);
    }

    [Test]
    public async Task ConnectionPoolStatistics_HasDefaultEmptyBrokers()
    {
        var stats = new ConnectionPoolStatistics();

        await Assert.That(stats.Brokers).IsNotNull();
        await Assert.That(stats.Brokers).IsEmpty();
    }

    [Test]
    public async Task BrokerConnectionStatistics_RequiredPropertiesAreSet()
    {
        var stats = new BrokerConnectionStatistics
        {
            NodeId = 1,
            Host = "broker-1",
            Port = 9092,
            ConnectionCount = 3,
            TotalConnectionsCreated = 5,
            TotalConnectionsClosed = 2,
            PendingRequests = 10,
            TotalRequestsSent = 1000,
            TotalResponsesReceived = 990,
            TotalErrors = 3
        };

        await Assert.That(stats.NodeId).IsEqualTo(1);
        await Assert.That(stats.Host).IsEqualTo("broker-1");
        await Assert.That(stats.Port).IsEqualTo(9092);
        await Assert.That(stats.ConnectionCount).IsEqualTo(3);
        await Assert.That(stats.TotalConnectionsCreated).IsEqualTo(5);
        await Assert.That(stats.TotalConnectionsClosed).IsEqualTo(2);
        await Assert.That(stats.PendingRequests).IsEqualTo(10);
        await Assert.That(stats.TotalRequestsSent).IsEqualTo(1000);
        await Assert.That(stats.TotalResponsesReceived).IsEqualTo(990);
        await Assert.That(stats.TotalErrors).IsEqualTo(3);
    }
}
