using System.Diagnostics;
using System.Runtime;
using System.Text.Json;

namespace Dekaf.Benchmarks;

public static class Probe
{
    public static void SaveLoadedBinaries(string path)
    {
        var root = Path.GetFullPath(AppContext.BaseDirectory);
        var binaries = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(static assembly => assembly.Location)
            .Where(location => Path.GetFullPath(location).StartsWith(root, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Select(location => new { Path = location, Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(location))).ToLowerInvariant() })
            .ToArray();
        Save(path, binaries);
    }

    public sealed record TickCount(long Ticks, long Count);
    public sealed record Snapshot(double Seconds, long Completed, long CpuTicks, long AllocatedBytes,
        long HeapBytes, long RssBytes, int Gen0, int Gen1, int Gen2, long JitMethods,
        double JitMilliseconds, int ThreadPoolThreads, long PendingWorkItems);
    public sealed record Interval(Snapshot Start, Snapshot End, List<TickCount> Latencies);
    public sealed record Result(double Seconds, long Completed, double CallsPerSecond, double CpuNsPerCall,
        double AllocatedBytesPerCall, double P50Ns, double P99Ns, double MaxNs, long StopwatchFrequency,
        Snapshot Start, Snapshot End, List<Interval> Intervals, List<TickCount> Latencies);

    public static async Task<Result> MeasureAsync(AdminFixture fixture, double seconds)
    {
        if (seconds <= 0 || !double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        // Exact tick buckets below one millisecond; sparse overflow preserves every longer tail.
        var dense = new long[Math.Min(Stopwatch.Frequency / 1000, 1_000_000) + 1];
        var overflow = new Dictionary<long, long>();
        var aggregate = new Dictionary<long, long>();
        var intervals = new List<Interval>((int)Math.Ceiling(seconds) + 1);
        using var process = Process.GetCurrentProcess();
        var started = Stopwatch.GetTimestamp();
        long completed = 0;
        var first = TakeSnapshot(process, started, completed);
        var previous = first;
        var nextSnapshot = 1d;
        while (true)
        {
            var callStart = Stopwatch.GetTimestamp();
            _ = await fixture.Call();
            var callEnd = Stopwatch.GetTimestamp();
            var ticks = callEnd - callStart;
            if ((ulong)ticks < (ulong)dense.Length) dense[ticks]++;
            else { overflow.TryGetValue(ticks, out var count); overflow[ticks] = count + 1; }
            completed++;
            var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds - first.Seconds;
            if (elapsed >= nextSnapshot || elapsed >= seconds)
            {
                var snapshot = TakeSnapshot(process, started, completed);
                var histogram = DrainHistogram(dense, overflow);
                foreach (var bucket in histogram)
                {
                    aggregate.TryGetValue(bucket.Ticks, out var count);
                    aggregate[bucket.Ticks] = count + bucket.Count;
                }
                intervals.Add(new(previous, snapshot, histogram));
                previous = snapshot;
                nextSnapshot = Math.Floor(elapsed) + 1;
                if (elapsed >= seconds) break;
            }
        }
        var all = aggregate.OrderBy(static pair => pair.Key).Select(static pair => new TickCount(pair.Key, pair.Value)).ToList();
        if (all.Sum(static bucket => bucket.Count) != completed)
            throw new InvalidOperationException("Latency histogram lost completed operations.");
        var duration = previous.Seconds - first.Seconds;
        return new(duration, completed, completed / duration,
            (previous.CpuTicks - first.CpuTicks) * 100d / completed,
            (previous.AllocatedBytes - first.AllocatedBytes) / (double)completed,
            Percentile(all, completed, 0.50), Percentile(all, completed, 0.99),
            all[^1].Ticks * 1e9 / Stopwatch.Frequency, Stopwatch.Frequency,
            first, previous, intervals, all);
    }

    private static double Percentile(List<TickCount> histogram, long count, double fraction)
    {
        var rank = (long)Math.Ceiling(count * fraction);
        long seen = 0;
        foreach (var bucket in histogram)
        {
            seen += bucket.Count;
            if (seen >= rank) return bucket.Ticks * 1e9 / Stopwatch.Frequency;
        }
        throw new InvalidOperationException("Percentile exceeds histogram population.");
    }

    private static List<TickCount> DrainHistogram(long[] dense, Dictionary<long, long> overflow)
    {
        var result = new List<TickCount>();
        for (var tick = 0; tick < dense.Length; tick++)
        {
            if (dense[tick] == 0) continue;
            result.Add(new(tick, dense[tick]));
            dense[tick] = 0;
        }
        foreach (var pair in overflow.OrderBy(static pair => pair.Key)) result.Add(new(pair.Key, pair.Value));
        overflow.Clear();
        return result;
    }

    internal static Snapshot TakeSnapshot(Process process, long started, long completed)
    {
        process.Refresh();
        return new(Stopwatch.GetElapsedTime(started).TotalSeconds, completed, process.TotalProcessorTime.Ticks,
            GC.GetTotalAllocatedBytes(true), GC.GetTotalMemory(false), process.WorkingSet64,
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
            JitInfo.GetCompiledMethodCount(false), JitInfo.GetCompilationTime(false).TotalMilliseconds,
            ThreadPool.ThreadCount, ThreadPool.PendingWorkItemCount);
    }

    public static void Save(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(value));
    }
}
