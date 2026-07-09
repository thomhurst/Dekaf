using System.Runtime.CompilerServices;

namespace Dekaf.Tests.Integration;

internal static class TransactionCoordinatorTestGate
{
    internal const int MaximumConcurrencyPerFixture = 4;

    private static readonly ConditionalWeakTable<KafkaTestContainer, SemaphoreSlim> Gates = new();

    internal static SemaphoreSlim Get(KafkaTestContainer kafka) =>
        Gates.GetValue(kafka, static _ =>
            new SemaphoreSlim(MaximumConcurrencyPerFixture, MaximumConcurrencyPerFixture));
}

/// <summary>
/// Limits transaction-test concurrency per shared Kafka fixture so each broker's transaction
/// coordinator remains responsive while independent Kafka versions still run in parallel.
/// </summary>
public abstract class TransactionalKafkaIntegrationTest(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private readonly SemaphoreSlim _coordinatorGate = TransactionCoordinatorTestGate.Get(kafka);
    private bool _gateEntered;

    [Before(Test)]
    public async Task EnterTransactionCoordinatorGate(CancellationToken cancellationToken)
    {
        await _coordinatorGate.WaitAsync(cancellationToken);
        _gateEntered = true;
    }

    [After(Test)]
    public void ExitTransactionCoordinatorGate()
    {
        if (!_gateEntered)
        {
            return;
        }

        _gateEntered = false;
        _coordinatorGate.Release();
    }
}
