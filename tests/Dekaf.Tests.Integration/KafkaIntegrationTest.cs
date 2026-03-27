using Dekaf.Consumer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Base class for integration tests that require a Kafka container.
/// The <see cref="KafkaTestContainer"/> instance is shared across all tests in the session via TUnit's ClassDataSource.
/// Derived classes receive the container through their own primary constructor parameter.
/// </summary>
[ClassDataSource<KafkaContainer39>(Shared = SharedType.PerTestSession)]
[ClassDataSource<KafkaContainer40>(Shared = SharedType.PerTestSession)]
[ClassDataSource<KafkaContainer41>(Shared = SharedType.PerTestSession)]
public abstract class KafkaIntegrationTest(KafkaTestContainer kafkaTestContainer)
{
    public KafkaTestContainer KafkaContainer { get; } = kafkaTestContainer;

    /// <summary>
    /// Polls until a condition is true, replacing fixed <c>Task.Delay</c> waits.
    /// Returns as soon as the condition is met, avoiding unnecessary delays.
    /// </summary>
    protected static async Task<List<ConsumeResult<string, string>>> ConsumeMessagesAsync(
        IKafkaConsumer<string, string> consumer, int count)
    {
        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= count) break;
        }

        return messages;
    }

    protected static async Task CommitAndVerifyOffsetsAsync(
        IKafkaConsumer<string, string> consumer,
        List<ConsumeResult<string, string>> messages)
    {
        var offsets = messages
            .GroupBy(m => new TopicPartition(m.Topic, m.Partition))
            .Select(g => new TopicPartitionOffset(g.Key.Topic, g.Key.Partition, g.Max(m => m.Offset) + 1))
            .ToArray();

        await consumer.CommitAsync(offsets);

        foreach (var offset in offsets)
        {
            var committed = await consumer.GetCommittedOffsetAsync(new TopicPartition(offset.Topic, offset.Partition));
            await Assert.That(committed).IsEqualTo(offset.Offset);
        }
    }

    /// <summary>
    /// Polls until a condition is true, replacing fixed <c>Task.Delay</c> waits.
    /// Returns as soon as the condition is met, avoiding unnecessary delays.
    /// </summary>
    protected static async Task WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan timeout,
        int pollIntervalMs = 100)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!condition())
        {
            await Task.Delay(pollIntervalMs, cts.Token).ConfigureAwait(false);
        }
    }
}
