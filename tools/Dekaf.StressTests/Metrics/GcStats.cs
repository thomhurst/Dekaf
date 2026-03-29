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
    private readonly long _allocatedBefore;

    public int Gen0 { get; private set; }
    public int Gen1 { get; private set; }
    public int Gen2 { get; private set; }
    public long? AllocatedBytes { get; private set; }

    public GcStats()
    {
        _gen0Before = GC.CollectionCount(0);
        _gen1Before = GC.CollectionCount(1);
        _gen2Before = GC.CollectionCount(2);
        _allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        Gen0 = Gen1 = Gen2 = 0;
        AllocatedBytes = null;
    }

    public void Capture()
    {
        Gen0 = GC.CollectionCount(0) - _gen0Before;
        Gen1 = GC.CollectionCount(1) - _gen1Before;
        Gen2 = GC.CollectionCount(2) - _gen2Before;

        // precise: true forces cross-heap synchronization, giving an accurate
        // cumulative total regardless of which thread takes the snapshot.
        // On Server GC, compaction can rarely produce a negative delta — report null
        // rather than a misleading negative number.
        var delta = GC.GetTotalAllocatedBytes(precise: true) - _allocatedBefore;

        if (delta < 0)
        {
            Console.WriteLine($"[GcStats] Warning: precise allocation delta was negative ({delta:N0} B). Reporting as N/A.");
            AllocatedBytes = null;
            return;
        }

        AllocatedBytes = delta;
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
            < 1024 => $"{AllocatedBytes.Value} B",
            < 1024 * 1024 => $"{AllocatedBytes.Value / 1024.0:F2} KB",
            < 1024 * 1024 * 1024 => $"{AllocatedBytes.Value / (1024.0 * 1024):F2} MB",
            _ => $"{AllocatedBytes.Value / (1024.0 * 1024 * 1024):F2} GB"
        };
    }
}
