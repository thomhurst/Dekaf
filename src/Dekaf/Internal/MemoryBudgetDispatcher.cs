namespace Dekaf.Internal;

/// <summary>
/// Serializes budget callbacks outside the budget lock. Concurrent or reentrant mutations
/// publish a replacement snapshot for the active dispatcher to apply after its current callbacks.
/// Those mutations return without waiting; the active dispatcher drains the latest snapshot before returning.
/// </summary>
internal sealed class MemoryBudgetDispatcher
{
    private Snapshot? _pending;
    private int _dispatching;

    // Call under the owning budget's lock, before releasing it to call Dispatch.
    public void Schedule(IBudgetedInstance[] producers, IBudgetedInstance[] consumers,
        ulong producerLimit, ulong consumerLimit)
    {
        Volatile.Write(ref _pending, new Snapshot(producers, consumers, producerLimit, consumerLimit));
    }

    /// <summary>
    /// Applies pending snapshots inline when idle. If callbacks are already running, returns
    /// without waiting, so registration and unregistration limits are eventually consistent:
    /// the active dispatcher applies the latest snapshot after its current callbacks finish.
    /// </summary>
    public void Dispatch()
    {
        do
        {
            if (Interlocked.CompareExchange(ref _dispatching, 1, 0) != 0)
                return;

            try
            {
                while (Interlocked.Exchange(ref _pending, null) is { } snapshot)
                    snapshot.Dispatch();
            }
            finally
            {
                Volatile.Write(ref _dispatching, 0);
            }
            // A mutation may publish after the last exchange but before we relinquish
            // dispatch ownership. Recheck after releasing it so that update is not stranded.
        } while (Volatile.Read(ref _pending) is not null);
    }

    private sealed class Snapshot(IBudgetedInstance[] producers, IBudgetedInstance[] consumers,
        ulong producerLimit, ulong consumerLimit)
    {
        public void Dispatch()
        {
            foreach (var producer in producers)
            {
                try { producer.OnBudgetChanged(producerLimit); }
                catch (Exception ex) { ReportBookkeepingError(ex); }
            }
            foreach (var consumer in consumers)
            {
                try { consumer.OnBudgetChanged(consumerLimit); }
                catch (Exception ex) { ReportBookkeepingError(ex); }
            }
        }

        private static void ReportBookkeepingError(Exception exception)
        {
            try { DekafMemoryBudget.OnBookkeepingError?.Invoke(exception); } catch { }
        }
    }
}
