namespace Dekaf.Internal;

internal interface IDekafMemoryBudget
{
    ulong PreviewProducerLimit();

    ulong PreviewConsumerLimit();

    void RegisterProducer(IBudgetedInstance instance);

    void UnregisterProducer(IBudgetedInstance instance);

    void RegisterConsumer(IBudgetedInstance instance);

    void UnregisterConsumer(IBudgetedInstance instance);

    void ReserveExplicit(ulong bytes);

    void ReleaseExplicit(ulong bytes);
}
