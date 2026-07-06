namespace Dekaf.Consumer;

internal enum PrefetchLoopAction
{
    Continue,
    WaitForAny,
    DelayNoWork
}

internal readonly record struct PrefetchLoopDecision(
    PrefetchLoopAction Action,
    bool ReportBacklog,
    bool RecordFetchWait);

internal static class PrefetchLoopControl
{
    public static bool ShouldWaitForMemory(long currentPrefetchedBytes, long maxBytes)
        => currentPrefetchedBytes >= maxBytes;

    public static bool ShouldBreakOnConsecutiveError(int consecutiveErrors, int threshold)
        => consecutiveErrors >= threshold;

    public static PrefetchLoopDecision DecideAfterDispatch(
        int started,
        int targetCount,
        bool hasInFlight)
    {
        if (started > 0)
            return new PrefetchLoopDecision(PrefetchLoopAction.Continue, ReportBacklog: false, RecordFetchWait: false);

        if (hasInFlight)
        {
            var hasBacklog = targetCount > 0;
            return new PrefetchLoopDecision(PrefetchLoopAction.WaitForAny, hasBacklog, RecordFetchWait: true);
        }

        return new PrefetchLoopDecision(PrefetchLoopAction.DelayNoWork, ReportBacklog: false, RecordFetchWait: false);
    }
}
