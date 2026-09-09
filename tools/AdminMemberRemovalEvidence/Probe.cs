using System.Diagnostics;
using System.Runtime;
using System.Text.Json;

namespace Dekaf.Benchmarks;

public static class Probe
{
    private static readonly IComparer<TickCount> TickComparer =
        Comparer<TickCount>.Create(static (left, right) => left.Ticks.CompareTo(right.Ticks));

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

    public readonly record struct TickCount(long Ticks, long Count);
    public sealed record Snapshot(double Seconds, long Completed, long CpuTicks, long AllocatedBytes,
        long HeapBytes, long RssBytes, int Gen0, int Gen1, int Gen2, long JitMethods,
        double JitMilliseconds, int ThreadPoolThreads, long PendingWorkItems);
    public sealed record Interval(Snapshot Start, Snapshot End, ArraySegment<TickCount> Latencies);
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
        // Share bounded storage across intervals instead of reserving 1 MiB for every second.
        var intervalCount = durations.Sum(static seconds => checked((int)Math.Ceiling(seconds) + 1));
        var histogram = new ExactHistogram((int)Math.Min((long)intervalCount * 65536, 4_194_304));
        var phase = 0;
        var seconds = durations[phase];
        var intervals = intervalSets[phase];
        using var process = Process.GetCurrentProcess();
        if (compilations is not null)
        {
            // Age retained observer storage before the continuous workload starts.
            // Otherwise histogram allocation postpones the first Gen2/ArrayPool
            // finalizer transition into collection, even with longer warmup.
            PhaseEvents.Log.Phase("prepare-observer-heap");
            compilations.Phase("prepare-observer-heap");
            for (var generationPass = 0; generationPass < 2; generationPass++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
            }
        }
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
            histogram.Record(ticks);
            completed++;
            var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds - first.Seconds;
            if (elapsed >= nextSnapshot || elapsed >= seconds)
            {
                var snapshot = TakeSnapshot(process, started, completed);
                intervals.Add(new(previous, snapshot, histogram.Drain()));
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
            Array.Sort(interval.Latencies.Array!, interval.Latencies.Offset, interval.Latencies.Count,
                TickComparer);
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

    // Single-writer recorder. Exact ticks and every maximum survive; exhaustion invalidates the capture.
    internal sealed class ExactHistogram
    {
        private const int IntervalCapacity = 65536;
        private readonly long[] _dense = new long[Math.Min(Stopwatch.Frequency / 1000, 1_000_000) + 1];
        private readonly int[] _touched = new int[IntervalCapacity];
        private readonly Dictionary<long, long> _overflow = new(IntervalCapacity);
        private readonly TickCount[] _archive;
        private int _touchedCount;
        private int _used;

        internal ExactHistogram(int capacity)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
            _archive = new TickCount[capacity];
        }

        internal void Record(long ticks)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(ticks);
            if (ticks < _dense.Length)
            {
                if (_dense[ticks] == 0)
                {
                    RequireIntervalSpace();
                    _touched[_touchedCount++] = (int)ticks;
                }
                _dense[ticks]++;
            }
            else if (_overflow.TryGetValue(ticks, out var count))
            {
                _overflow[ticks] = count + 1;
            }
            else
            {
                RequireIntervalSpace();
                _overflow.Add(ticks, 1);
            }
        }

        private void RequireIntervalSpace()
        {
            if (_touchedCount + _overflow.Count == IntervalCapacity)
                throw new InvalidOperationException("Preallocated interval histogram capacity exceeded.");
        }

        internal ArraySegment<TickCount> Drain()
        {
            var count = _touchedCount + _overflow.Count;
            if (count > _archive.Length - _used)
                throw new InvalidOperationException("Preallocated histogram archive capacity exceeded.");
            var result = new ArraySegment<TickCount>(_archive, _used, count);
            // Visit only observed tick values, not all one million possible values.
            for (var index = 0; index < _touchedCount; index++)
            {
                var tick = _touched[index];
                _archive[_used++] = new(tick, _dense[tick]);
                _dense[tick] = 0;
            }
            foreach (var pair in _overflow)
                _archive[_used++] = new(pair.Key, pair.Value);
            _touchedCount = 0;
            _overflow.Clear();
            return result;
        }
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
