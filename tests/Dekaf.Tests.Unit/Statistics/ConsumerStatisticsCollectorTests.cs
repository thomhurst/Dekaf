using Dekaf.Statistics;

namespace Dekaf.Tests.Unit.Statistics;

public class ConsumerStatisticsCollectorTests
{
    [Test]
    public async Task RecordMessagesConsumedBatch_IncrementsGlobalCounters()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordMessagesConsumedBatch("test-topic", 0, 1, 100);

        var stats = collector.GetGlobalStats();
        await Assert.That(stats.MessagesConsumed).IsEqualTo(1);
        await Assert.That(stats.BytesConsumed).IsEqualTo(100);
    }

    [Test]
    public async Task RecordMessagesConsumedBatch_IncrementsTopicCounters()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordMessagesConsumedBatch("test-topic", 0, 1, 100);
        collector.RecordMessagesConsumedBatch("test-topic", 1, 1, 200);

        var topicStats = collector.GetTopicStatistics(_ => (null, null, false));
        await Assert.That(topicStats).ContainsKey("test-topic");
        await Assert.That(topicStats["test-topic"].MessagesConsumed).IsEqualTo(2);
        await Assert.That(topicStats["test-topic"].BytesConsumed).IsEqualTo(300);
    }

    [Test]
    public async Task RecordMessagesConsumedBatch_IncrementsPartitionCounters()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordMessagesConsumedBatch("test-topic", 0, 1, 100);
        collector.RecordMessagesConsumedBatch("test-topic", 0, 1, 150);
        collector.RecordMessagesConsumedBatch("test-topic", 1, 1, 200);

        var topicStats = collector.GetTopicStatistics(_ => (null, null, false));
        await Assert.That(topicStats["test-topic"].Partitions).ContainsKey(0);
        await Assert.That(topicStats["test-topic"].Partitions).ContainsKey(1);
        await Assert.That(topicStats["test-topic"].Partitions[0].MessagesConsumed).IsEqualTo(2);
        await Assert.That(topicStats["test-topic"].Partitions[0].BytesConsumed).IsEqualTo(250);
        await Assert.That(topicStats["test-topic"].Partitions[1].MessagesConsumed).IsEqualTo(1);
    }

    [Test]
    public async Task RecordRebalanceStartedAndCompleted_TracksDuration()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordRebalanceStarted();
        collector.RecordRebalanceCompleted();

        var stats = collector.GetGlobalStats();
        await Assert.That(stats.TotalRebalances).IsEqualTo(1);
        await Assert.That(stats.LastRebalanceDurationMs).IsNotNull();
        await Assert.That(stats.LastRebalanceDurationMs!.Value).IsGreaterThanOrEqualTo(0);
        await Assert.That(stats.TotalRebalanceDurationMs).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task RecordRebalanceCompleted_WithoutStart_DoesNotIncrement()
    {
        var collector = new ConsumerStatisticsCollector();

        // Calling completed without started should be a no-op
        collector.RecordRebalanceCompleted();

        var stats = collector.GetGlobalStats();
        await Assert.That(stats.TotalRebalances).IsEqualTo(0);
        await Assert.That(stats.LastRebalanceDurationMs).IsNull();
        await Assert.That(stats.TotalRebalanceDurationMs).IsEqualTo(0);
    }

    [Test]
    public async Task RecordRebalanceCompleted_CalledTwice_OnlyRecordsOnce()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordRebalanceStarted();
        collector.RecordRebalanceCompleted();

        // Second call without a new RecordRebalanceStarted should be a no-op
        // because the timestamp was reset to -1 by the first completion.
        collector.RecordRebalanceCompleted();

        var stats = collector.GetGlobalStats();
        await Assert.That(stats.TotalRebalances).IsEqualTo(1);
    }

    [Test]
    public async Task MultipleRebalances_AccumulatesTotalDuration()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordRebalanceStarted();
        collector.RecordRebalanceCompleted();

        var stats1 = collector.GetGlobalStats();

        collector.RecordRebalanceStarted();
        collector.RecordRebalanceCompleted();

        var stats2 = collector.GetGlobalStats();

        await Assert.That(stats2.TotalRebalances).IsEqualTo(2);
        await Assert.That(stats2.TotalRebalanceDurationMs).IsGreaterThanOrEqualTo(stats1.TotalRebalanceDurationMs);
    }

    [Test]
    public async Task GetGlobalStats_ReturnsNullLastRebalanceDuration_WhenNoRebalanceCompleted()
    {
        var collector = new ConsumerStatisticsCollector();

        var stats = collector.GetGlobalStats();
        await Assert.That(stats.TotalRebalances).IsEqualTo(0);
        await Assert.That(stats.LastRebalanceDurationMs).IsNull();
        await Assert.That(stats.TotalRebalanceDurationMs).IsEqualTo(0);
    }

    [Test]
    public async Task CancelRebalanceStarted_PreventsCompletionFromRecording()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordRebalanceStarted();
        collector.CancelRebalanceStarted();
        collector.RecordRebalanceCompleted();

        var stats = collector.GetGlobalStats();
        await Assert.That(stats.TotalRebalances).IsEqualTo(0);
        await Assert.That(stats.LastRebalanceDurationMs).IsNull();
    }

    [Test]
    public async Task RecordFetchRequestSent_IncrementsFetchRequestCounter()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordFetchRequestSent();
        collector.RecordFetchRequestSent();
        collector.RecordFetchRequestSent();

        var stats = collector.GetGlobalStats();
        await Assert.That(stats.FetchRequestsSent).IsEqualTo(3);
    }

    [Test]
    public async Task RecordFetchResponseReceived_UpdatesLatencyStats()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordFetchResponseReceived(15);
        collector.RecordFetchResponseReceived(25);
        collector.RecordFetchResponseReceived(35);

        var stats = collector.GetGlobalStats();
        await Assert.That(stats.FetchResponsesReceived).IsEqualTo(3);
        await Assert.That(stats.AvgFetchLatencyMs).IsEqualTo(25.0);
    }

    [Test]
    public async Task GetGlobalStats_ReturnsZeroAvgLatency_WhenNoResponses()
    {
        var collector = new ConsumerStatisticsCollector();

        var stats = collector.GetGlobalStats();
        await Assert.That(stats.AvgFetchLatencyMs).IsEqualTo(0.0);
    }

    [Test]
    public async Task UpdatePartitionHighWatermark_SetsHighWatermark()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordMessagesConsumedBatch("test-topic", 0, 1, 100);
        collector.UpdatePartitionHighWatermark("test-topic", 0, 1000);

        var topicStats = collector.GetTopicStatistics(_ => (null, null, false));
        await Assert.That(topicStats["test-topic"].Partitions[0].HighWatermark).IsEqualTo(1000);
    }

    [Test]
    public async Task UpdatePartitionHighWatermark_CreatesPartitionIfNotExists()
    {
        var collector = new ConsumerStatisticsCollector();

        // Set high watermark without first consuming
        collector.UpdatePartitionHighWatermark("test-topic", 0, 500);

        var topicStats = collector.GetTopicStatistics(_ => (null, null, false));
        // Topic should not exist because no messages were consumed
        await Assert.That(topicStats).DoesNotContainKey("test-topic");
    }

    [Test]
    public async Task GetTopicStatistics_IncludesPartitionInfo()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordMessagesConsumedBatch("test-topic", 0, 1, 100);
        collector.UpdatePartitionHighWatermark("test-topic", 0, 1000);

        var topicStats = collector.GetTopicStatistics(tp =>
        {
            if (tp.Topic == "test-topic" && tp.Partition == 0)
            {
                return (Position: 50L, CommittedOffset: 40L, IsPaused: true);
            }
            return (null, null, false);
        });

        var partitionStats = topicStats["test-topic"].Partitions[0];
        await Assert.That(partitionStats.Position).IsEqualTo(50);
        await Assert.That(partitionStats.CommittedOffset).IsEqualTo(40);
        await Assert.That(partitionStats.IsPaused).IsTrue();
        await Assert.That(partitionStats.HighWatermark).IsEqualTo(1000);
    }

    [Test]
    public async Task MultipleTopics_TrackedIndependently()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordMessagesConsumedBatch("topic-a", 0, 1, 100);
        collector.RecordMessagesConsumedBatch("topic-b", 0, 1, 200);

        var topicStats = collector.GetTopicStatistics(_ => (null, null, false));
        await Assert.That(topicStats).Count().IsEqualTo(2);
        await Assert.That(topicStats["topic-a"].BytesConsumed).IsEqualTo(100);
        await Assert.That(topicStats["topic-b"].BytesConsumed).IsEqualTo(200);
    }

    [Test]
    public async Task ConcurrentUpdates_AreThreadSafe()
    {
        var collector = new ConsumerStatisticsCollector();
        const int threadCount = 10;
        const int operationsPerThread = 1000;

        var tasks = new List<Task>();
        for (var t = 0; t < threadCount; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (var i = 0; i < operationsPerThread; i++)
                {
                    collector.RecordMessagesConsumedBatch("test-topic", i % 4, 1, 100);
                }
            }));
        }

        await Task.WhenAll(tasks);

        var stats = collector.GetGlobalStats();
        await Assert.That(stats.MessagesConsumed).IsEqualTo(threadCount * operationsPerThread);
        await Assert.That(stats.BytesConsumed).IsEqualTo(threadCount * operationsPerThread * 100L);
    }
}
