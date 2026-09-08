using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Reports;

namespace Dekaf.Benchmarks;

// Diagnostic-only warmup of the exact helpers observed in run 34194281968.
internal static class MeasurementPrimer
{
    public static void Run(TimeSpan duration, string directory)
    {
        const BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var core = typeof(ThreadPool).Assembly;
        var number = core.GetType("System.Number", throwOnError: true)!;
        var numberMethod = number.GetMethod("UInt64ToDecStr", staticFlags, [typeof(ulong)])
            ?? throw new InvalidOperationException("UInt64ToDecStr(ulong) is missing.");
        var formatNumber = numberMethod.CreateDelegate<Func<ulong, string>>();
        var poolType = core.GetType("System.Threading.PortableThreadPool", throwOnError: true)!;
        var pool = poolType.GetField("ThreadPoolInstance", staticFlags)?.GetValue(null)
            ?? throw new InvalidOperationException("Portable thread pool instance is missing.");
        var threadCountMethod = poolType.GetProperty("ThreadCount", instanceFlags)?.GetMethod
            ?? throw new InvalidOperationException("Portable thread count getter is missing.");
        var readThreadCount = threadCountMethod.CreateDelegate<Func<int>>(pool);
        var countsType = poolType.GetNestedType("ThreadCounts", BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException("ThreadCounts type is missing.");
        var volatileRead = countsType.GetMethod("VolatileRead", instanceFlags, Type.EmptyTypes)
            ?? throw new InvalidOperationException("ThreadCounts.VolatileRead is missing.");
        var existingThreads = countsType.GetProperty("NumExistingThreads", instanceFlags)?.GetMethod
            ?? throw new InvalidOperationException("NumExistingThreads getter is missing.");
        if (volatileRead.ReturnType != countsType || existingThreads.ReturnType != typeof(short))
            throw new InvalidOperationException("ThreadCounts helper signatures changed.");
        // Reflection invokes these value-type helpers on an owned zero value, never live pool state.
        var counts = Activator.CreateInstance(countsType)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "helper-bindings.json"), JsonSerializer.Serialize(
            new[] { numberMethod, threadCountMethod, volatileRead, existingThreads }.Select(method => new
            {
                Type = method.DeclaringType!.FullName,
                Signature = method.ToString(),
                method.MetadataToken,
                ModuleId = method.Module.ModuleVersionId,
                Assembly = method.Module.Assembly.FullName,
                method.Module.Assembly.Location
            })));

        using var process = Process.GetCurrentProcess();
        var samples = new List<Sample>(32);
        var timer = Stopwatch.StartNew();
        long completed = 0;
        var next = 1d;
        do
        {
            InvokeHelpers(completed, formatNumber, readThreadCount, volatileRead, existingThreads, counts);
            completed++;
            var seconds = timer.Elapsed.TotalSeconds;
            if (seconds >= next || seconds >= duration.TotalSeconds)
            {
                samples.Add(Capture(process, seconds, completed));
                next++;
            }
        } while (timer.Elapsed < duration);
        var elapsed = timer.Elapsed.TotalSeconds;
        if (samples.Count == 0 || samples[^1].Completed != completed)
            samples.Add(Capture(process, elapsed, completed));
        File.WriteAllText(Path.Combine(directory, "helper-primer.json"), JsonSerializer.Serialize(new
        {
            Seconds = elapsed, Completed = completed, Samples = samples
        }));
        Console.WriteLine(FormattableString.Invariant($"HELPER completed seconds={elapsed:F6} calls={completed}"));
    }

    private static Sample Capture(Process process, double seconds, long completed)
    {
        process.Refresh();
        return new Sample(seconds, completed, System.Runtime.JitInfo.GetCompiledMethodCount(),
            System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds, ThreadPool.ThreadCount,
            ThreadPool.PendingWorkItemCount, process.TotalProcessorTime.TotalMilliseconds,
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
            GC.GetTotalAllocatedBytes(), GC.GetTotalMemory(false), process.WorkingSet64);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void InvokeHelpers(long index, Func<ulong, string> formatNumber,
        Func<int> readThreadCount, MethodInfo volatileRead, MethodInfo existingThreads, object counts)
    {
        var measurement = new Measurement(0, IterationMode.Workload,
            index % 2 == 0 ? IterationStage.Warmup : IterationStage.Actual,
            (int)(index % 75) + 1, 40_000_000 + (index & 0xffff), 1_000_000_000.125 + (index & 0x3ff));
        GC.KeepAlive(measurement.ToString());
        GC.KeepAlive(formatNumber((ulong)(40_000_000 + (index & 0xffff))));
        if (readThreadCount() < 0)
            throw new InvalidOperationException("Invalid live thread count.");
        var snapshot = volatileRead.Invoke(counts, null)!;
        if ((short)existingThreads.Invoke(snapshot, null)! != 0)
            throw new InvalidOperationException("Owned zero ThreadCounts value changed.");
    }

    private readonly record struct Sample(double Seconds, long Completed, long JitMethods, double JitMs,
        int Threads, long PendingWork, double CpuMs, int Gc0, int Gc1, int Gc2,
        long AllocatedBytes, long HeapBytes, long RssBytes);
}
