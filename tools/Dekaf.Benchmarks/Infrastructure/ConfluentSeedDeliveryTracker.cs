using Confluent.Kafka;

namespace Dekaf.Benchmarks.Infrastructure;

/// <summary>
/// Verifies that every fire-and-forget benchmark seed produce reached the broker.
/// </summary>
internal sealed class ConfluentSeedDeliveryTracker
{
    private int _completedCount;
    private int _failedCount;

    public ConfluentSeedDeliveryTracker()
    {
        Handler = OnDelivery;
    }

    /// <summary>
    /// Gets the cached handler shared by every seed produce.
    /// </summary>
    public Action<DeliveryReport<string, string>> Handler { get; }

    public void EnsureComplete(int expectedCount, int undeliveredCount)
    {
        var completedCount = Volatile.Read(ref _completedCount);
        var failedCount = Volatile.Read(ref _failedCount);
        if (undeliveredCount == 0 && completedCount == expectedCount && failedCount == 0)
            return;

        throw new InvalidOperationException(
            $"Benchmark seed delivery failed: expected {expectedCount} reports, received {completedCount}, " +
            $"with {failedCount} failed and {undeliveredCount} still undelivered.");
    }

    private void OnDelivery(DeliveryReport<string, string> report)
    {
        if (report.Error.IsError)
            Interlocked.Increment(ref _failedCount);

        Interlocked.Increment(ref _completedCount);
    }
}
