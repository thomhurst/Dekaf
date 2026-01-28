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
    public long AllocatedBytes { get; private set; }

    public GcStats()
    {
        _gen0Before = GC.CollectionCount(0);
        _gen1Before = GC.CollectionCount(1);
        _gen2Before = GC.CollectionCount(2);
        _allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Gen0 = Gen1 = Gen2 = 0;
        AllocatedBytes = 0;
    }

    public void Capture()
    {
        Gen0 = GC.CollectionCount(0) - _gen0Before;
        Gen1 = GC.CollectionCount(1) - _gen1Before;
        Gen2 = GC.CollectionCount(2) - _gen2Before;
        AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - _allocatedBefore;
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
    public required long AllocatedBytes { get; init; }

    public string FormatAllocatedBytes()
    {
        return AllocatedBytes switch
        {
            < 1024 => $"{AllocatedBytes} B",
            < 1024 * 1024 => $"{AllocatedBytes / 1024.0:F2} KB",
            < 1024 * 1024 * 1024 => $"{AllocatedBytes / (1024.0 * 1024):F2} MB",
            _ => $"{AllocatedBytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }
}
