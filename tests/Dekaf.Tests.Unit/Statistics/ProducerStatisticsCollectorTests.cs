using Dekaf.Statistics;

namespace Dekaf.Tests.Unit.Statistics;

public class ProducerStatisticsCollectorTests
{
    [Test]
    public async Task RecordMessageProduced_IncrementsGlobalCounters()
    {
        var collector = new ProducerStatisticsCollector();

        collector.RecordMessageProduced("test-topic", 0, 100);

        var (messagesProduced, _, _, bytesProduced, _, _, _, _) = collector.GetGlobalStats();
        await Assert.That(messagesProduced).IsEqualTo(1);
        await Assert.That(bytesProduced).IsEqualTo(100);
    }

    [Test]
    public async Task RecordMessageProduced_IncrementsTopicCounters()
    {
        var collector = new ProducerStatisticsCollector();

        collector.RecordMessageProduced("test-topic", 0, 100);
        collector.RecordMessageProduced("test-topic", 1, 200);

        var topicStats = collector.GetTopicStatistics();
        await Assert.That(topicStats).ContainsKey("test-topic");
        await Assert.That(topicStats["test-topic"].MessagesProduced).IsEqualTo(2);
        await Assert.That(topicStats["test-topic"].BytesProduced).IsEqualTo(300);
    }

    [Test]
    public async Task RecordMessageProduced_IncrementsPartitionCounters()
    {
        var collector = new ProducerStatisticsCollector();

        collector.RecordMessageProduced("test-topic", 0, 100);
        collector.RecordMessageProduced("test-topic", 0, 150);
        collector.RecordMessageProduced("test-topic", 1, 200);

        var topicStats = collector.GetTopicStatistics();
        await Assert.That(topicStats["test-topic"].Partitions).ContainsKey(0);
        await Assert.That(topicStats["test-topic"].Partitions).ContainsKey(1);
        await Assert.That(topicStats["test-topic"].Partitions[0].MessagesProduced).IsEqualTo(2);
        await Assert.That(topicStats["test-topic"].Partitions[0].BytesProduced).IsEqualTo(250);
        await Assert.That(topicStats["test-topic"].Partitions[0].QueuedMessages).IsEqualTo(2);
        await Assert.That(topicStats["test-topic"].Partitions[1].MessagesProduced).IsEqualTo(1);
        await Assert.That(topicStats["test-topic"].Partitions[1].QueuedMessages).IsEqualTo(1);
    }

    [Test]
    public async Task RecordBatchDelivered_SingleMessage_UpdatesDeliveryCounters()
    {
        var collector = new ProducerStatisticsCollector();

        collector.RecordMessageProduced("test-topic", 0, 100);
        collector.RecordBatchDelivered("test-topic", 0, 1);

        var (_, messagesDelivered, _, _, _, _, _, _) = collector.GetGlobalStats();
        await Assert.That(messagesDelivered).IsEqualTo(1);

        var topicStats = collector.GetTopicStatistics();
        await Assert.That(topicStats["test-topic"].MessagesDelivered).IsEqualTo(1);
        await Assert.That(topicStats["test-topic"].Partitions[0].MessagesDelivered).IsEqualTo(1);
        await Assert.That(topicStats["test-topic"].Partitions[0].QueuedMessages).IsEqualTo(0);
    }

    [Test]
    public async Task RecordBatchDelivered_UpdatesBatchCounters()
    {
        var collector = new ProducerStatisticsCollector();

        // Produce 5 messages
        for (var i = 0; i < 5; i++)
        {
            collector.RecordMessageProduced("test-topic", 0, 100);
        }

        // Deliver them as a batch
        collector.RecordBatchDelivered("test-topic", 0, 5);

        var (_, messagesDelivered, _, _, _, _, _, _) = collector.GetGlobalStats();
        await Assert.That(messagesDelivered).IsEqualTo(5);

        var topicStats = collector.GetTopicStatistics();
        await Assert.That(topicStats["test-topic"].MessagesDelivered).IsEqualTo(5);
        await Assert.That(topicStats["test-topic"].Partitions[0].QueuedMessages).IsEqualTo(0);
    }

    [Test]
    public async Task RecordBatchFailed_SingleMessage_UpdatesFailureCounters()
    {
        var collector = new ProducerStatisticsCollector();

        collector.RecordMessageProduced("test-topic", 0, 100);
        collector.RecordBatchFailed("test-topic", 0, 1);

        var (_, _, messagesFailed, _, _, _, _, _) = collector.GetGlobalStats();
        await Assert.That(messagesFailed).IsEqualTo(1);

        var topicStats = collector.GetTopicStatistics();
        await Assert.That(topicStats["test-topic"].MessagesFailed).IsEqualTo(1);
        await Assert.That(topicStats["test-topic"].Partitions[0].MessagesFailed).IsEqualTo(1);
        await Assert.That(topicStats["test-topic"].Partitions[0].QueuedMessages).IsEqualTo(0);
    }

    [Test]
    public async Task RecordBatchFailed_UpdatesBatchFailureCounters()
    {
        var collector = new ProducerStatisticsCollector();

        // Produce 3 messages
        for (var i = 0; i < 3; i++)
        {
            collector.RecordMessageProduced("test-topic", 0, 100);
        }

        // Fail them as a batch
        collector.RecordBatchFailed("test-topic", 0, 3);

        var (_, _, messagesFailed, _, _, _, _, _) = collector.GetGlobalStats();
        await Assert.That(messagesFailed).IsEqualTo(3);

        var topicStats = collector.GetTopicStatistics();
        await Assert.That(topicStats["test-topic"].MessagesFailed).IsEqualTo(3);
        await Assert.That(topicStats["test-topic"].Partitions[0].QueuedMessages).IsEqualTo(0);
    }

    [Test]
    public async Task RecordRequestSent_IncrementsRequestCounter()
    {
        var collector = new ProducerStatisticsCollector();

        collector.RecordRequestSent();
        collector.RecordRequestSent();

        var (_, _, _, _, requestsSent, _, _, _) = collector.GetGlobalStats();
        await Assert.That(requestsSent).IsEqualTo(2);
    }

    [Test]
    public async Task RecordResponseReceived_UpdatesLatencyStats()
    {
        var collector = new ProducerStatisticsCollector();

        collector.RecordResponseReceived(10);
        collector.RecordResponseReceived(20);
        collector.RecordResponseReceived(30);

        var (_, _, _, _, _, responsesReceived, _, avgLatencyMs) = collector.GetGlobalStats();
        await Assert.That(responsesReceived).IsEqualTo(3);
        await Assert.That(avgLatencyMs).IsEqualTo(20.0);
    }

    [Test]
    public async Task RecordRetry_IncrementsRetryCounter()
    {
        var collector = new ProducerStatisticsCollector();

        collector.RecordRetry();
        collector.RecordRetry();
        collector.RecordRetry();

        var (_, _, _, _, _, _, retries, _) = collector.GetGlobalStats();
        await Assert.That(retries).IsEqualTo(3);
    }

    [Test]
    public async Task GetGlobalStats_ReturnsZeroAvgLatency_WhenNoResponses()
    {
        var collector = new ProducerStatisticsCollector();

        var (_, _, _, _, _, _, _, avgLatencyMs) = collector.GetGlobalStats();
        await Assert.That(avgLatencyMs).IsEqualTo(0.0);
    }

    [Test]
    public async Task MultipleTopics_TrackedIndependently()
    {
        var collector = new ProducerStatisticsCollector();

        collector.RecordMessageProduced("topic-a", 0, 100);
        collector.RecordMessageProduced("topic-b", 0, 200);

        var topicStats = collector.GetTopicStatistics();
        await Assert.That(topicStats).Count().IsEqualTo(2);
        await Assert.That(topicStats["topic-a"].BytesProduced).IsEqualTo(100);
        await Assert.That(topicStats["topic-b"].BytesProduced).IsEqualTo(200);
    }

    [Test]
    public async Task ConcurrentUpdates_AreThreadSafe()
    {
        var collector = new ProducerStatisticsCollector();
        const int threadCount = 10;
        const int operationsPerThread = 1000;

        var tasks = new List<Task>();
        for (var t = 0; t < threadCount; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (var i = 0; i < operationsPerThread; i++)
                {
                    collector.RecordMessageProduced("test-topic", i % 4, 100);
                }
            }));
        }

        await Task.WhenAll(tasks);

        var (messagesProduced, _, _, bytesProduced, _, _, _, _) = collector.GetGlobalStats();
        await Assert.That(messagesProduced).IsEqualTo(threadCount * operationsPerThread);
        await Assert.That(bytesProduced).IsEqualTo(threadCount * operationsPerThread * 100L);
    }
}
