using System.Diagnostics;
using Dekaf.Statistics;

namespace Dekaf.Tests.Unit.Statistics;

public class ConsumerStatisticsCollectorTests
{
    [Test]
    public async Task RecordMessagesConsumedBatch_IncrementsGlobalCounters()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordMessagesConsumedBatch("test-topic", 0, 1, 100);

        var (messagesConsumed, bytesConsumed, _, _, _, _, _, _) = collector.GetGlobalStats();
        await Assert.That(messagesConsumed).IsEqualTo(1);
        await Assert.That(bytesConsumed).IsEqualTo(100);
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
        // Spin-wait for at least 5ms to ensure a measurable duration
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 5)
        {
            Thread.SpinWait(1000);
        }
        collector.RecordRebalanceCompleted();

        var (_, _, totalRebalances, lastRebalanceDurationMs, totalRebalanceDurationMs, _, _, _) = collector.GetGlobalStats();
        await Assert.That(totalRebalances).IsEqualTo(1);
        await Assert.That(lastRebalanceDurationMs).IsNotNull();
        await Assert.That(lastRebalanceDurationMs!.Value).IsGreaterThanOrEqualTo(5);
        await Assert.That(totalRebalanceDurationMs).IsGreaterThanOrEqualTo(5);
    }

    [Test]
    public async Task RecordRebalanceCompleted_WithoutStart_DoesNotIncrement()
    {
        var collector = new ConsumerStatisticsCollector();

        // Calling completed without started should be a no-op
        collector.RecordRebalanceCompleted();

        var (_, _, totalRebalances, lastRebalanceDurationMs, totalRebalanceDurationMs, _, _, _) = collector.GetGlobalStats();
        await Assert.That(totalRebalances).IsEqualTo(0);
        await Assert.That(lastRebalanceDurationMs).IsNull();
        await Assert.That(totalRebalanceDurationMs).IsEqualTo(0);
    }

    [Test]
    public async Task RecordRebalanceCompleted_CalledTwice_OnlyRecordsOnce()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordRebalanceStarted();
        // Spin-wait for at least 5ms
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 5)
        {
            Thread.SpinWait(1000);
        }
        collector.RecordRebalanceCompleted();

        // Second call without a new RecordRebalanceStarted should be a no-op
        // because the timestamp was reset to 0 by the first completion.
        collector.RecordRebalanceCompleted();

        var (_, _, totalRebalances, _, _, _, _, _) = collector.GetGlobalStats();
        await Assert.That(totalRebalances).IsEqualTo(1);
    }

    [Test]
    public async Task MultipleRebalances_AccumulatesTotalDuration()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordRebalanceStarted();
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 5)
        {
            Thread.SpinWait(1000);
        }
        collector.RecordRebalanceCompleted();

        var (_, _, totalRebalances1, _, totalDuration1, _, _, _) = collector.GetGlobalStats();

        collector.RecordRebalanceStarted();
        sw.Restart();
        while (sw.ElapsedMilliseconds < 5)
        {
            Thread.SpinWait(1000);
        }
        collector.RecordRebalanceCompleted();

        var (_, _, totalRebalances2, _, totalDuration2, _, _, _) = collector.GetGlobalStats();

        await Assert.That(totalRebalances2).IsEqualTo(2);
        await Assert.That(totalDuration2).IsGreaterThan(totalDuration1);
    }

    [Test]
    public async Task GetGlobalStats_ReturnsNullLastRebalanceDuration_WhenNoRebalanceCompleted()
    {
        var collector = new ConsumerStatisticsCollector();

        var (_, _, totalRebalances, lastRebalanceDurationMs, totalRebalanceDurationMs, _, _, _) = collector.GetGlobalStats();
        await Assert.That(totalRebalances).IsEqualTo(0);
        await Assert.That(lastRebalanceDurationMs).IsNull();
        await Assert.That(totalRebalanceDurationMs).IsEqualTo(0);
    }

    [Test]
    public async Task CancelRebalanceStarted_PreventsCompletionFromRecording()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordRebalanceStarted();
        collector.CancelRebalanceStarted();
        collector.RecordRebalanceCompleted();

        var (_, _, totalRebalances, lastRebalanceDurationMs, _, _, _, _) = collector.GetGlobalStats();
        await Assert.That(totalRebalances).IsEqualTo(0);
        await Assert.That(lastRebalanceDurationMs).IsNull();
    }

    [Test]
    public async Task RecordFetchRequestSent_IncrementsFetchRequestCounter()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordFetchRequestSent();
        collector.RecordFetchRequestSent();
        collector.RecordFetchRequestSent();

        var (_, _, _, _, _, fetchRequestsSent, _, _) = collector.GetGlobalStats();
        await Assert.That(fetchRequestsSent).IsEqualTo(3);
    }

    [Test]
    public async Task RecordFetchResponseReceived_UpdatesLatencyStats()
    {
        var collector = new ConsumerStatisticsCollector();

        collector.RecordFetchResponseReceived(15);
        collector.RecordFetchResponseReceived(25);
        collector.RecordFetchResponseReceived(35);

        var (_, _, _, _, _, _, fetchResponsesReceived, avgFetchLatencyMs) = collector.GetGlobalStats();
        await Assert.That(fetchResponsesReceived).IsEqualTo(3);
        await Assert.That(avgFetchLatencyMs).IsEqualTo(25.0);
    }

    [Test]
    public async Task GetGlobalStats_ReturnsZeroAvgLatency_WhenNoResponses()
    {
        var collector = new ConsumerStatisticsCollector();

        var (_, _, _, _, _, _, _, avgFetchLatencyMs) = collector.GetGlobalStats();
        await Assert.That(avgFetchLatencyMs).IsEqualTo(0.0);
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

        var (messagesConsumed, bytesConsumed, _, _, _, _, _, _) = collector.GetGlobalStats();
        await Assert.That(messagesConsumed).IsEqualTo(threadCount * operationsPerThread);
        await Assert.That(bytesConsumed).IsEqualTo(threadCount * operationsPerThread * 100L);
    }
}
