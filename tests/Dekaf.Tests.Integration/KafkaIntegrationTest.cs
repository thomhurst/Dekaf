using System.Runtime.CompilerServices;
using Dekaf.Consumer;

namespace Dekaf.Tests.Integration;

internal static class KafkaFixtureConcurrencyGate
{
    // Four coordinator-heavy tests per fixture is the proven-safe bound from #1638.
    // Apply it to all shared-fixture traffic so unrelated clients cannot consume the
    // broker capacity that transaction, group, and share coordinators need for progress.
    internal const int MaximumConcurrencyPerFixture = 4;

    private static readonly ConditionalWeakTable<KafkaTestContainer, SemaphoreSlim> Gates = new();

    internal static SemaphoreSlim Get(KafkaTestContainer kafka) =>
        Gates.GetValue(kafka, static _ =>
            new SemaphoreSlim(MaximumConcurrencyPerFixture, MaximumConcurrencyPerFixture));
}

/// <summary>
/// Base class for integration tests that require a Kafka container.
/// The <see cref="KafkaTestContainer"/> instance is shared across all tests in the session via TUnit's ClassDataSource.
/// Derived classes receive the container through their own primary constructor parameter.
/// The broker version is selected via the <c>KAFKA_TEST_IMAGE_TAG</c> environment variable
/// (see <see cref="KafkaContainerDefault"/>); the NuGet release gate sweeps all
/// supported versions while PR CI runs the current release only.
/// </summary>
[ClassDataSource<KafkaContainerDefault>(Shared = SharedType.PerTestSession)]
public abstract class KafkaIntegrationTest(KafkaTestContainer kafkaTestContainer)
{
    private readonly SemaphoreSlim _fixtureConcurrencyGate =
        KafkaFixtureConcurrencyGate.Get(kafkaTestContainer);
    private bool _fixtureGateEntered;

    public KafkaTestContainer KafkaContainer { get; } = kafkaTestContainer;

    [Before(Test)]
    public async Task EnterKafkaFixtureConcurrencyGate(CancellationToken cancellationToken)
    {
        await _fixtureConcurrencyGate.WaitAsync(cancellationToken);
        _fixtureGateEntered = true;
    }

    [After(Test)]
    public void ExitKafkaFixtureConcurrencyGate()
    {
        if (!_fixtureGateEntered)
            return;

        _fixtureGateEntered = false;
        _fixtureConcurrencyGate.Release();
    }

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
