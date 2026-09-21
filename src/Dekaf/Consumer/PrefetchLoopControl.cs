using Dekaf.Retry;

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

    /// <summary>
    /// Counts only failures that repeating the loop cannot fix. A broker or coordinator outage
    /// (reset or refused connections, DNS, a metadata refresh that reached no broker, a group
    /// join that ran out its rebalance timeout) can outlast any number of iterations, and a
    /// counter that reaches its limit ends the consumer for good.
    /// </summary>
    public static int RecordConsecutiveError(int consecutiveErrors, Exception exception)
        => IsTransientFailure(exception)
            ? 0
            : consecutiveErrors + 1;

    private static bool IsTransientFailure(Exception exception)
        => RetryHelper.IsRetriableBrokerFailure(exception)
           || RetryHelper.IsRetriableRequestFailure(exception);

    public static bool ShouldResetConsecutiveErrors(int drained)
        => drained > 0;

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
