using Dekaf.Producer;

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

    /// <summary>
    /// Produces a message with a per-call timeout and retry. On CI runners, the producer's
    /// internal write path can occasionally hang (EDQCSGX pattern) when a connection becomes
    /// unresponsive after topic creation. This helper retries after a 15s timeout so that
    /// transient hangs don't cause 360s orphan sweep failures.
    /// </summary>
    protected static async ValueTask<RecordMetadata> ProduceWithRetryAsync<TKey, TValue>(
        IKafkaProducer<TKey, TValue> producer,
        ProducerMessage<TKey, TValue> message,
        int maxAttempts = 3,
        int timeoutSeconds = 15)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                return await producer.ProduceAsync(message, cts.Token);
            }
            catch (OperationCanceledException) when (attempt < maxAttempts - 1)
            {
                // Timeout — retry. The cancellation only stops the caller's await;
                // the message may still be delivered in the background.
                await Task.Delay(500);
            }
            catch (ObjectDisposedException) when (attempt < maxAttempts - 1)
            {
                // BrokerSender disposed during an in-flight produce (EDQCSGX pattern).
                // The producer may recover with a new connection on the next attempt.
                await Task.Delay(500);
            }
        }

        // Final attempt without retry catch
        using var finalCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        return await producer.ProduceAsync(message, finalCts.Token);
    }
}