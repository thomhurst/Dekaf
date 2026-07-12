namespace Dekaf.StressTests.Metrics;

/// <summary>
/// Tracks process-wide managed allocations and GC collection counts for one test pass.
/// Create before the operation, then call Capture() after to calculate deltas.
/// </summary>
internal sealed class GcStats : IDisposable
{
    private readonly int _gen0Before;
    private readonly int _gen1Before;
    private readonly int _gen2Before;
    private readonly Func<long> _readAllocatedBytes;
    private readonly object _gate = new();
    private readonly Timer? _allocationSampler;
    private long _lastAllocatedBytes;
    private long _allocatedBytes;
    private long _largestCounterRebase;
    private bool _captured;

    public int Gen0 { get; private set; }
    public int Gen1 { get; private set; }
    public int Gen2 { get; private set; }
    public long? AllocatedBytes { get; private set; }

    public GcStats() : this(
        // A single lifetime-counter delta can go negative when allocation contexts from
        // threads used by a previous client are retired. Frequent cheap snapshots bound
        // that correction to one interval and let the next segment continue normally.
        () => GC.GetTotalAllocatedBytes(precise: false),
        TimeSpan.FromSeconds(1))
    {
    }

    internal GcStats(Func<long> readAllocatedBytes, TimeSpan? sampleInterval)
    {
        _readAllocatedBytes = readAllocatedBytes;
        _gen0Before = GC.CollectionCount(0);
        _gen1Before = GC.CollectionCount(1);
        _gen2Before = GC.CollectionCount(2);
        _lastAllocatedBytes = readAllocatedBytes();
        Gen0 = Gen1 = Gen2 = 0;
        AllocatedBytes = null;

        if (sampleInterval is { } interval)
        {
            _allocationSampler = new Timer(
                static state => ((GcStats)state!).SampleAllocation(),
                this,
                interval,
                interval);
        }
    }

    public void Capture()
    {
        _allocationSampler?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        lock (_gate)
        {
            if (_captured)
                return;

            SampleAllocationCore();
            Gen0 = GC.CollectionCount(0) - _gen0Before;
            Gen1 = GC.CollectionCount(1) - _gen1Before;
            Gen2 = GC.CollectionCount(2) - _gen2Before;
            AllocatedBytes = _allocatedBytes;
            _captured = true;
        }

        _allocationSampler?.Dispose();

        if (_largestCounterRebase > 0)
        {
            Console.WriteLine(
                $"[GcStats] Warning: allocation counter rebased by up to {_largestCounterRebase:N0} B; " +
                "continued measuring from the new baseline.");
        }
    }

    internal void SampleAllocation()
    {
        lock (_gate)
        {
            if (!_captured)
                SampleAllocationCore();
        }
    }

    private void SampleAllocationCore()
    {
        var current = _readAllocatedBytes();
        var delta = current - _lastAllocatedBytes;
        if (delta >= 0)
            _allocatedBytes += delta;
        else
            _largestCounterRebase = Math.Max(_largestCounterRebase, -delta);

        _lastAllocatedBytes = current;
    }

    public void Dispose() => _allocationSampler?.Dispose();

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
