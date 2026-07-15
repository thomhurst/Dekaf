using System.Text;

namespace Dekaf.TraceAnalyzer;

/// <summary>
/// Aggregates CLR runtime events from one trace window into decision-ready
/// tables: allocation by type, monitor contention, GC pauses, thread-pool
/// adjustments, and exceptions. Pure accumulation — the EventPipe wiring in
/// <c>Program</c> stays a thin adapter so this logic is unit-testable.
/// </summary>
public sealed class RuntimeEventAggregator
{
    private const int TopAllocationTypes = 30;

    private sealed class AllocationStats
    {
        public long SampledBytes;
        public long TickCount;
        public long LargeObjectTicks;
    }

    private readonly Dictionary<string, AllocationStats> _allocationsByType = [];
    private readonly Dictionary<string, long> _exceptionsByType = [];
    private readonly Dictionary<string, long> _threadPoolAdjustmentsByReason = [];
    private readonly Dictionary<int, long> _gcCountsByGeneration = [];
    private readonly Dictionary<string, long> _gcCountsByReason = [];

    private long _contentionCount;
    private double _contentionTotalMs;
    private double _contentionMaxMs;

    private long _gcPauseCount;
    private double _gcPauseTotalMs;
    private double _gcPauseMaxMs;

    private double _traceDurationSeconds;

    public void AddAllocationTick(string typeName, long sampledBytes, bool isLargeObject)
    {
        var key = string.IsNullOrEmpty(typeName) ? "(unknown)" : typeName;
        if (!_allocationsByType.TryGetValue(key, out var stats))
        {
            stats = new AllocationStats();
            _allocationsByType[key] = stats;
        }

        stats.SampledBytes += sampledBytes;
        stats.TickCount++;
        if (isLargeObject)
        {
            stats.LargeObjectTicks++;
        }
    }

    public void AddContention(double durationMs)
    {
        _contentionCount++;
        _contentionTotalMs += durationMs;
        _contentionMaxMs = Math.Max(_contentionMaxMs, durationMs);
    }

    public void AddGcStart(int generation, string reason)
    {
        _gcCountsByGeneration[generation] = _gcCountsByGeneration.GetValueOrDefault(generation) + 1;
        _gcCountsByReason[reason] = _gcCountsByReason.GetValueOrDefault(reason) + 1;
    }

    public void AddGcPause(double pauseMs)
    {
        _gcPauseCount++;
        _gcPauseTotalMs += pauseMs;
        _gcPauseMaxMs = Math.Max(_gcPauseMaxMs, pauseMs);
    }

    public void AddThreadPoolAdjustment(string reason)
        => _threadPoolAdjustmentsByReason[reason] =
            _threadPoolAdjustmentsByReason.GetValueOrDefault(reason) + 1;

    public void AddException(string exceptionType)
        => _exceptionsByType[exceptionType] = _exceptionsByType.GetValueOrDefault(exceptionType) + 1;

    public void SetTraceDuration(double seconds) => _traceDurationSeconds = seconds;

    public string RenderMarkdown()
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Event Summary");
        builder.AppendLine();
        if (_traceDurationSeconds > 0)
        {
            builder.AppendLine($"Trace duration: {_traceDurationSeconds:F1}s");
            builder.AppendLine();
        }

        RenderAllocations(builder);
        RenderContention(builder);
        RenderGc(builder);
        RenderThreadPool(builder);
        RenderExceptions(builder);
        return builder.ToString();
    }

    private void RenderAllocations(StringBuilder builder)
    {
        builder.AppendLine("## Allocations by type (GCAllocationTick, ~100KB sampling)");
        builder.AppendLine();
        if (_allocationsByType.Count == 0)
        {
            builder.AppendLine("_No allocation tick events (cpu profile, or level below verbose)._");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Type | Sampled bytes | Ticks | LOH ticks | Sampled rate |");
        builder.AppendLine("|---|---|---|---|---|");
        foreach (var (type, stats) in _allocationsByType
                     .OrderByDescending(pair => pair.Value.SampledBytes)
                     .Take(TopAllocationTypes))
        {
            var rate = _traceDurationSeconds > 0
                ? FormatBytes((long)(stats.SampledBytes / _traceDurationSeconds)) + "/s"
                : "-";
            builder.AppendLine(
                $"| `{type}` | {FormatBytes(stats.SampledBytes)} | {stats.TickCount} | {stats.LargeObjectTicks} | {rate} |");
        }

        if (_allocationsByType.Count > TopAllocationTypes)
        {
            builder.AppendLine();
            builder.AppendLine($"_{_allocationsByType.Count - TopAllocationTypes} further types omitted._");
        }

        builder.AppendLine();
    }

    private void RenderContention(StringBuilder builder)
    {
        builder.AppendLine("## Monitor contention");
        builder.AppendLine();
        if (_contentionCount == 0)
        {
            builder.AppendLine("_No contention events._");
        }
        else
        {
            var perSecond = _traceDurationSeconds > 0
                ? $"{_contentionCount / _traceDurationSeconds:F1}/s"
                : "-";
            builder.AppendLine(
                $"{_contentionCount} events ({perSecond}), total wait {_contentionTotalMs:F1}ms, max {_contentionMaxMs:F2}ms.");
        }

        builder.AppendLine();
    }

    private void RenderGc(StringBuilder builder)
    {
        builder.AppendLine("## Garbage collection");
        builder.AppendLine();
        if (_gcCountsByGeneration.Count == 0 && _gcPauseCount == 0)
        {
            builder.AppendLine("_No GC events._");
            builder.AppendLine();
            return;
        }

        foreach (var (generation, count) in _gcCountsByGeneration.OrderBy(pair => pair.Key))
        {
            builder.AppendLine($"- Gen{generation}: {count}");
        }

        foreach (var (reason, count) in _gcCountsByReason.OrderByDescending(pair => pair.Value))
        {
            builder.AppendLine($"- Reason {reason}: {count}");
        }

        if (_gcPauseCount > 0)
        {
            builder.AppendLine(
                $"- Pauses: {_gcPauseCount}, total {_gcPauseTotalMs:F1}ms, max {_gcPauseMaxMs:F2}ms");
        }

        builder.AppendLine();
    }

    private void RenderThreadPool(StringBuilder builder)
    {
        builder.AppendLine("## Thread-pool adjustments");
        builder.AppendLine();
        if (_threadPoolAdjustmentsByReason.Count == 0)
        {
            builder.AppendLine("_No thread-pool adjustment events (threading keyword not enabled)._");
        }
        else
        {
            foreach (var (reason, count) in _threadPoolAdjustmentsByReason.OrderByDescending(pair => pair.Value))
            {
                builder.AppendLine($"- {reason}: {count}");
            }
        }

        builder.AppendLine();
    }

    private void RenderExceptions(StringBuilder builder)
    {
        builder.AppendLine("## Exceptions thrown");
        builder.AppendLine();
        if (_exceptionsByType.Count == 0)
        {
            builder.AppendLine("_No exception events._");
        }
        else
        {
            foreach (var (type, count) in _exceptionsByType.OrderByDescending(pair => pair.Value))
            {
                builder.AppendLine($"- `{type}`: {count}");
            }
        }

        builder.AppendLine();
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F2} MB",
            >= 1_024 => $"{bytes / 1_024.0:F2} KB",
            _ => $"{bytes} B",
        };
    }
}
