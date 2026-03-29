namespace Dekaf.StressTests.Metrics;

/// <summary>
/// Tracks GC collection counts across all generations.
/// Create before the operation, then call Capture() after to calculate deltas.
/// </summary>
internal struct GcStats
{
    private readonly int _gen0Before;
    private readonly int _gen1Before;
    private readonly int _gen2Before;
    private readonly long _allocatedBeforePrecise;
    private readonly long _allocatedBeforeNonPrecise;

    public int Gen0 { get; private set; }
    public int Gen1 { get; private set; }
    public int Gen2 { get; private set; }
    public long? AllocatedBytes { get; private set; }

    public GcStats()
    {
        _gen0Before = GC.CollectionCount(0);
        _gen1Before = GC.CollectionCount(1);
        _gen2Before = GC.CollectionCount(2);
        _allocatedBeforePrecise = GC.GetTotalAllocatedBytes(precise: true);
        _allocatedBeforeNonPrecise = GC.GetTotalAllocatedBytes(precise: false);
        Gen0 = Gen1 = Gen2 = 0;
        AllocatedBytes = null;
    }

    public void Capture()
    {
        Gen0 = GC.CollectionCount(0) - _gen0Before;
        Gen1 = GC.CollectionCount(1) - _gen1Before;
        Gen2 = GC.CollectionCount(2) - _gen2Before;

        var allocatedAfterPrecise = GC.GetTotalAllocatedBytes(precise: true);
        var delta = allocatedAfterPrecise - _allocatedBeforePrecise;

        if (delta >= 0)
        {
            AllocatedBytes = delta;
            return;
        }

        // precise: true can return inconsistent values under heavy concurrent allocation.
        // Fall back to non-precise measurement using a consistent non-precise baseline.
        var allocatedAfterNonPrecise = GC.GetTotalAllocatedBytes(precise: false);
        var fallbackDelta = allocatedAfterNonPrecise - _allocatedBeforeNonPrecise;

        if (fallbackDelta >= 0)
        {
            Console.WriteLine(
                $"[GcStats] Warning: precise allocation delta was negative ({delta:N0} B). " +
                $"BeforePrecise={_allocatedBeforePrecise:N0}, AfterPrecise={allocatedAfterPrecise:N0}. " +
                $"Using non-precise fallback ({fallbackDelta:N0} B).");
            AllocatedBytes = fallbackDelta;
            return;
        }

        Console.WriteLine(
            $"[GcStats] Warning: allocation delta was negative for both precise ({delta:N0} B) " +
            $"and non-precise ({fallbackDelta:N0} B). " +
            $"BeforePrecise={_allocatedBeforePrecise:N0}, AfterPrecise={allocatedAfterPrecise:N0}, " +
            $"BeforeNonPrecise={_allocatedBeforeNonPrecise:N0}, AfterNonPrecise={allocatedAfterNonPrecise:N0}. " +
            $"Reporting as unavailable.");
        AllocatedBytes = null;
    }

    public GcSnapshot ToSnapshot() => new()
    {
        Gen0Collections = Gen0,
        Gen1Collections = Gen1,
        Gen2Collections = Gen2,
        AllocatedBytes = AllocatedBytes
    };
}

internal sealed class GcSnapshot
{
    public required int Gen0Collections { get; init; }
    public required int Gen1Collections { get; init; }
    public required int Gen2Collections { get; init; }
    public required long? AllocatedBytes { get; init; }

    public string FormatAllocatedBytes()
    {
        return AllocatedBytes switch
        {
            null => "N/A (measurement error)",
            < 1024 => $"{AllocatedBytes} B",
            < 1024 * 1024 => $"{AllocatedBytes / 1024.0:F2} KB",
            < 1024 * 1024 * 1024 => $"{AllocatedBytes / (1024.0 * 1024):F2} MB",
            _ => $"{AllocatedBytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }
}
