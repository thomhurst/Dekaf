namespace Dekaf.Internal;

/// <summary>
/// Process-global memory budget for Dekaf producers and consumers.
/// Defaults to 40% of available system memory (respects container cgroup limits).
/// </summary>
public static class DekafMemoryBudget
{
    private const double DefaultPercentOfAvailable = 0.40;

    private static readonly object _lock = new();
    private static ulong? _explicitBudget;
    private static double _percentOfAvailable = DefaultPercentOfAvailable;

    /// <summary>
    /// The total memory budget in bytes available to all Dekaf instances in the process.
    /// </summary>
    public static ulong TotalBudget
    {
        get
        {
            lock (_lock)
            {
                if (_explicitBudget.HasValue)
                    return _explicitBudget.Value;

                var available = (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                if (available == 0)
                    return 320UL * 1024 * 1024; // Fallback: 256 MB producer + 64 MB consumer

                return (ulong)(available * _percentOfAvailable);
            }
        }
    }

    /// <summary>
    /// Override the budget with an explicit byte count.
    /// Must be called before building any Dekaf instances.
    /// </summary>
    public static void SetBudget(ulong bytes)
    {
        lock (_lock)
        {
            _explicitBudget = bytes;
        }
    }

    /// <summary>
    /// Override the budget as a percentage of available system memory.
    /// </summary>
    public static void SetBudget(double percentOfAvailable)
    {
        if (percentOfAvailable is <= 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(percentOfAvailable),
                "Must be in the range (0.0, 1.0].");

        lock (_lock)
        {
            _explicitBudget = null;
            _percentOfAvailable = percentOfAvailable;
        }
    }

    /// <summary>
    /// Test hook: reset budget state to defaults.
    /// </summary>
    internal static void ResetForTesting()
    {
        lock (_lock)
        {
            _explicitBudget = null;
            _percentOfAvailable = DefaultPercentOfAvailable;
        }
    }
}
