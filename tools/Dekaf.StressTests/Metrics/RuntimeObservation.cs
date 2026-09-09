using System.Diagnostics;

namespace Dekaf.StressTests.Metrics;

/// <summary>Process-wide counters, including harness work; no client-only attribution is implied.</summary>
internal sealed record RuntimeObservation
{
    public int ThreadPoolThreads { get; init; }
    public long PendingWorkItems { get; init; }
    public double CpuSeconds { get; init; }
    public long AllocatedBytes { get; init; }
    public long HeapBytes { get; init; }
    public long WorkingSetBytes { get; init; }
    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }
    public double GcPauseMilliseconds { get; init; }

    public static RuntimeObservation Capture()
    {
        using var process = Process.GetCurrentProcess();
        return new RuntimeObservation
        {
            CpuSeconds = Environment.CpuUsage.TotalTime.TotalSeconds,
            AllocatedBytes = GC.GetTotalAllocatedBytes(precise: false),
            HeapBytes = GC.GetTotalMemory(forceFullCollection: false),
            WorkingSetBytes = process.WorkingSet64,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            GcPauseMilliseconds = GC.GetTotalPauseDuration().TotalMilliseconds,
            ThreadPoolThreads = ThreadPool.ThreadCount,
            PendingWorkItems = ThreadPool.PendingWorkItemCount
        };
    }
}
