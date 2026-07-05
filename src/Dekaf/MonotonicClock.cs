namespace Dekaf;

internal static class MonotonicClock
{
    public static long GetMilliseconds()
    {
#if NETSTANDARD2_0
        return (long)(
            System.Diagnostics.Stopwatch.GetTimestamp()
            * 1000.0
            / System.Diagnostics.Stopwatch.Frequency);
#else
        return Environment.TickCount64;
#endif
    }
}
