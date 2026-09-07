using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Text.Json;

namespace Dekaf.Benchmarks;

internal static class ShutdownCpuProbe
{
    private readonly record struct Sample(long SetupCpu, long StopCpu, long CleanupCpu,
        long SetupKernel, long StopKernel, long CleanupKernel, long LatencyTicks);

    private static async ValueTask<Sample> SampleAsync(PartitionedShutdownBenchmarks fixture)
    {
        var start = Environment.CpuUsage;
        fixture.Setup();
        var ready = Environment.CpuUsage;
        var stopStart = Stopwatch.GetTimestamp();
        await fixture.DrainFullQueue();
        var stopEnd = Stopwatch.GetTimestamp();
        var stopped = Environment.CpuUsage;
        fixture.Cleanup();
        var end = Environment.CpuUsage;
        return new(ready.TotalTime.Ticks - start.TotalTime.Ticks,
            stopped.TotalTime.Ticks - ready.TotalTime.Ticks,
            end.TotalTime.Ticks - stopped.TotalTime.Ticks,
            ready.PrivilegedTime.Ticks - start.PrivilegedTime.Ticks,
            stopped.PrivilegedTime.Ticks - ready.PrivilegedTime.Ticks,
            end.PrivilegedTime.Ticks - stopped.PrivilegedTime.Ticks,
            stopEnd - stopStart);
    }

    internal static async Task Run(string folder, int samples, int warmupSeconds)
    {
        Directory.CreateDirectory(folder);
        var fixture = new PartitionedShutdownBenchmarks();
        var observations = new Sample[samples];
        var series = new List<object>();
        var warmupSeries = new List<object>();
        var warming = Stopwatch.GetTimestamp();
        var warmed = 0;
        do
        {
            await SampleAsync(fixture);
            if (++warmed % 1000 == 0) warmupSeries.Add(Snapshot(warming, warmed));
        } while (warmed < 100 || Stopwatch.GetElapsedTime(warming).TotalSeconds < warmupSeconds);
        var actualWarmup = Stopwatch.GetElapsedTime(warming).TotalSeconds;
        series.Add(Snapshot(Stopwatch.GetTimestamp(), 0));
        var allocatedStart = GC.GetTotalAllocatedBytes(true);
        var cpuStart = Environment.CpuUsage.TotalTime.Ticks;
        var started = Stopwatch.GetTimestamp();
        for (var i = 0; i < samples; i++)
        {
            observations[i] = await SampleAsync(fixture);
            if ((i + 1) % 1000 == 0) series.Add(Snapshot(started, i + 1));
        }
        var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
        var cpu = Environment.CpuUsage.TotalTime.Ticks - cpuStart;
        var allocated = GC.GetTotalAllocatedBytes(true) - allocatedStart;
        using (var stream = new StreamWriter(Path.Combine(folder, "cpu-stages.csv")))
        {
            stream.WriteLine("Sample,SetupCpuTicks,StopCpuTicks,CleanupCpuTicks,SetupKernelTicks,StopKernelTicks,CleanupKernelTicks,LatencyTicks");
            for (var i = 0; i < samples; i++)
            {
                var s = observations[i];
                stream.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{i+1},{s.SetupCpu},{s.StopCpu},{s.CleanupCpu},{s.SetupKernel},{s.StopKernel},{s.CleanupKernel},{s.LatencyTicks}"));
            }
        }
        var sorted = observations.Select(s => s.LatencyTicks).Order().ToArray();
        var ns = 1e9 / Stopwatch.Frequency;
        File.WriteAllText(Path.Combine(folder, "metrics.json"), JsonSerializer.Serialize(new
        {
            Samples = samples, CompletedRecords = samples * 1024L, Failures = 0, BacklogAtEnd = 0,
            WarmupSeconds = warmupSeconds, ActualWarmupSeconds = actualWarmup, WarmupSamples = warmed,
            LifecycleCpuNs = cpu * 100d / samples,
            SetupCpuNs = observations.Average(s => s.SetupCpu) * 100,
            StopCpuNs = observations.Average(s => s.StopCpu) * 100,
            CleanupCpuNs = observations.Average(s => s.CleanupCpu) * 100,
            SetupKernelNs = observations.Average(s => s.SetupKernel) * 100,
            StopKernelNs = observations.Average(s => s.StopKernel) * 100,
            CleanupKernelNs = observations.Average(s => s.CleanupKernel) * 100,
            LifecycleAllocatedBytes = allocated / (double)samples,
            LifecycleOperationsPerSecond = samples / elapsed,
            P50Ns = sorted[samples / 2 - 1] * ns,
            P99Ns = sorted[(int)Math.Ceiling(samples * .99) - 1] * ns,
            MaxNs = sorted[^1] * ns, ElapsedSeconds = elapsed, StopwatchFrequency = Stopwatch.Frequency,
            Scope = "Diagnostic process CPU brackets: setup includes fixture construction, reflection and queue filling; " +
                "stop includes deadline setup, handler scheduling, draining 1024 records and completion; cleanup includes correctness assertions and disposal. " +
                "CPU includes all process threads and CPU-read overhead; it is not thread-local or a per-message allocation measurement. " +
                "Raw CPU times may be quantized: compare accumulated blocks, never individual samples. No samples removed."
        }));
        File.WriteAllText(Path.Combine(folder, "series.json"), JsonSerializer.Serialize(series));
        File.WriteAllText(Path.Combine(folder, "warmup-series.json"), JsonSerializer.Serialize(warmupSeries));
    }

    private static object Snapshot(long start, int count) => new
    {
        Samples = count, CpuTicks = Environment.CpuUsage.TotalTime.Ticks,
        Gen0 = GC.CollectionCount(0), Gen1 = GC.CollectionCount(1), Gen2 = GC.CollectionCount(2),
        JitMethods = JitInfo.GetCompiledMethodCount(false), JitMilliseconds = JitInfo.GetCompilationTime(false).TotalMilliseconds,
        Threads = ThreadPool.ThreadCount, PendingWork = ThreadPool.PendingWorkItemCount,
        HeapBytes = GC.GetTotalMemory(false), ElapsedSeconds = Stopwatch.GetElapsedTime(start).TotalSeconds
    };
}
