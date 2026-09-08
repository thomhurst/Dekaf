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
    public sealed record Capture(Snapshot Start, Snapshot End, List<Interval> Intervals)
    {
        public double Seconds => End.Seconds - Start.Seconds;
        public long Completed => End.Completed - Start.Completed;
    }
    public sealed record Result(double Seconds, long Completed, double CallsPerSecond, double CpuNsPerCall,
        double AllocatedBytesPerCall, double P50Ns, double P99Ns, double MaxNs, long StopwatchFrequency,
        Snapshot Start, Snapshot End, List<Interval> Intervals, List<TickCount> Latencies);

    public static async Task PrimeAsync(AdminFixture fixture, string outputPath)
    {
        // Re-enter the complete measurement path so tiered entry stubs and the
        // low-frequency sampler do not first optimize at the measurement boundary.
        var segments = new List<Result>(128);
        for (var index = 0; index < 64; index++)
        {
            var pair = await CapturePhasesAsync(fixture, [0.05, 0.05]);
            segments.Add(Complete(pair[0]));
            segments.Add(Complete(pair[1]));
        }
        Save(Path.Combine(Path.GetDirectoryName(outputPath)!, "segments-" + Path.GetFileName(outputPath)), segments);
        Save(outputPath, await MeasureAsync(fixture, 1));
    }

    public static async Task<Result> MeasureAsync(AdminFixture fixture, double seconds)
        => Complete(await CaptureAsync(fixture, seconds));

    public static async Task<Capture> CaptureAsync(AdminFixture fixture, double seconds)
        => (await CapturePhasesAsync(fixture, [seconds]))[0];

    // Keep the same warmed call loop across phase boundaries. Returning from the
    // warmup state machine and entering another invocation can trigger new Tier 1 code.
    internal static async Task<Capture[]> CapturePhasesAsync(AdminFixture fixture, double[] durations,
        CompilationLog? compilations = null)
    {
        if (durations.Length == 0 || durations.Any(static value => value <= 0 || !double.IsFinite(value)))
            throw new ArgumentOutOfRangeException(nameof(durations));
        var captures = new Capture[durations.Length];
        var intervalSets = durations.Select(static seconds => new List<Interval>((int)Math.Ceiling(seconds) + 1)).ToArray();
        var phase = 0;
        var seconds = durations[phase];
        // Exact tick buckets below one millisecond; sparse overflow preserves every longer tail.
        var dense = new long[Math.Min(Stopwatch.Frequency / 1000, 1_000_000) + 1];
        var overflow = new Dictionary<long, long>();
        var intervals = intervalSets[phase];
        using var process = Process.GetCurrentProcess();
        var started = Stopwatch.GetTimestamp();
        long completed = 0;
        if (compilations is not null)
        {
            PhaseEvents.Log.Phase("warmup");
            compilations.Phase("warmup");
        }
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
                intervals.Add(new(previous, snapshot, histogram));
                previous = snapshot;
                nextSnapshot = Math.Floor(elapsed) + 1;
                if (elapsed >= seconds)
                {
                    captures[phase] = new(first, previous, intervals);
                    if (++phase == durations.Length) break;
                    seconds = durations[phase];
                    intervals = intervalSets[phase];
                    if (compilations is not null)
                    {
                        PhaseEvents.Log.Phase("measured");
                        compilations.Phase("measured");
                    }
                    first = previous = TakeSnapshot(process, started, completed);
                    nextSnapshot = 1;
                }
            }
        }
        return captures;
    }

    // Complete both reports only after measured collection stops. Sorting the
    // large warmup aggregate must not queue new JIT work at measurement start.
    public static Result Complete(Capture capture)
    {
        var (first, previous, intervals) = capture;
        var completed = capture.Completed;
        var aggregate = new Dictionary<long, long>();
        // Sparse overflow buckets were retained unsorted during collection.
        foreach (var interval in intervals)
            interval.Latencies.Sort(static (left, right) => left.Ticks.CompareTo(right.Ticks));
        foreach (var interval in intervals)
        foreach (var bucket in interval.Latencies)
        {
            aggregate.TryGetValue(bucket.Ticks, out var count);
            aggregate[bucket.Ticks] = count + bucket.Count;
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
        foreach (var pair in overflow) result.Add(new(pair.Key, pair.Value));
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
