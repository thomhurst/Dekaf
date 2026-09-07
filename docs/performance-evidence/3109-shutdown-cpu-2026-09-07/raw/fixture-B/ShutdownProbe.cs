using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks;

internal static class ShutdownProbe
{
    private readonly record struct Sample(long Start, long Release, long Resume, long HandlerEnd, long Return,
        long End, int Gen0Before, int Gen0After, int Gen2Before, int Gen2After, int ThreadsBefore, int ThreadsAfter)
    {
        internal long Elapsed => End - Start;
    }

    private static async ValueTask<Sample> RunSample(PartitionedShutdownBenchmarks fixture)
    {
        fixture.Setup();
        var gen0 = GC.CollectionCount(0);
        var gen2 = GC.CollectionCount(2);
        var threads = ThreadPool.ThreadCount;
        var start = Stopwatch.GetTimestamp();
        await fixture.DrainFullQueue();
        var end = Stopwatch.GetTimestamp();
        var sample = new Sample(start, fixture.ReleasedAt, fixture.HandlerResumedAt, fixture.HandlerCompletedAt,
            fixture.StopReturnedAt, end, gen0, GC.CollectionCount(0), gen2, GC.CollectionCount(2), threads, ThreadPool.ThreadCount);
        fixture.Cleanup();
        if (!(sample.Start <= sample.Release && sample.Release <= sample.Resume && sample.Resume <= sample.HandlerEnd
              && sample.HandlerEnd <= sample.Return && sample.Return <= sample.End))
            throw new InvalidOperationException("Invalid shutdown stage ordering.");
        return sample;
    }

    internal static async Task Run(string folder, int samples)
    {
        Directory.CreateDirectory(folder);
        var warmupSeconds = int.Parse(Environment.GetEnvironmentVariable("ABA_SHUTDOWN_WARMUP_SECONDS") ?? "0");
        var fixture = new PartitionedShutdownBenchmarks { CaptureStages = true };
        var observations = new Sample[samples];
        var series = new List<object>();
        using var process = Process.GetCurrentProcess();
        var warming = Stopwatch.GetTimestamp();
        var warmupSamples = 0;
        var warmupSeries = new List<object>();
        do
        {
            await RunSample(fixture);
            warmupSamples++;
            if (warmupSamples % 100 == 0) warmupSeries.Add(Snapshot(process, warming, warmupSamples));
        } while (warmupSamples < 100 || Stopwatch.GetElapsedTime(warming).TotalSeconds < warmupSeconds);
        var actualWarmupSeconds = Stopwatch.GetElapsedTime(warming).TotalSeconds;
        process.Refresh();
        var cpuStart = process.TotalProcessorTime.Ticks;
        var allocatedStart = GC.GetTotalAllocatedBytes(true);
        var started = Stopwatch.GetTimestamp();
        series.Add(Snapshot(process, started, 0));
        for (var i = 0; i < samples; i++)
        {
            observations[i] = await RunSample(fixture);
            if ((i + 1) % 100 == 0) series.Add(Snapshot(process, started, i + 1));
        }
        var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
        process.Refresh();
        var cpu = process.TotalProcessorTime.Ticks - cpuStart;
        var allocated = GC.GetTotalAllocatedBytes(true) - allocatedStart;
        var latencies = new long[samples];
        using (var output = new StreamWriter(Path.Combine(folder, "stages.csv")))
        {
            output.WriteLine("Sample,Start,Release,Resume,HandlerEnd,Return,End,Gen0Before,Gen0After,Gen2Before,Gen2After,ThreadsBefore,ThreadsAfter");
            for (var i = 0; i < samples; i++)
            {
                var s = observations[i];
                latencies[i] = s.Elapsed;
                output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{i+1},{s.Start},{s.Release},{s.Resume},{s.HandlerEnd},{s.Return},{s.End},{s.Gen0Before},{s.Gen0After},{s.Gen2Before},{s.Gen2After},{s.ThreadsBefore},{s.ThreadsAfter}"));
            }
        }
        using (var output = File.Create(Path.Combine(folder, "latency-ticks.bin")))
            output.Write(MemoryMarshal.AsBytes(latencies.AsSpan()));
        Array.Sort(latencies);
        var ns = 1_000_000_000d / Stopwatch.Frequency;
        File.WriteAllText(Path.Combine(folder, "metrics.json"), JsonSerializer.Serialize(new
        {
            Mode = warmupSeconds == 0 ? "shutdown-startup" : "shutdown-warmed", Samples = samples, RecordsPerSample = 1024,
            WarmupSeconds = warmupSeconds, ActualWarmupSeconds = actualWarmupSeconds, WarmupSamples = warmupSamples,
            Completed = samples * 1024L, Failures = 0, BacklogAtEnd = 0,
            LifecycleOperationsPerSecond = samples / elapsed,
            LifecycleCpuNsPerOperation = cpu * 100d / samples,
            LifecycleAllocatedBytesPerOperation = allocated / (double)samples,
            P50Ns = latencies[samples / 2 - 1] * ns,
            P99Ns = latencies[(int)Math.Ceiling(samples * .99) - 1] * ns,
            MaxNs = latencies[^1] * ns, ElapsedSeconds = elapsed, StopwatchFrequency = Stopwatch.Frequency,
            RecordStructBytes = Unsafe.SizeOf<ConsumeResult<string, string>>(),
            FullQueueElementBytes = 1024 * Unsafe.SizeOf<ConsumeResult<string, string>>(),
            ServerGc = GCSettings.IsServerGC, LatencyMode = GCSettings.LatencyMode.ToString(),
            Scope = "Instrumented stop latency: full queue, request to drained handler and rejected writer. " +
                "Stage timestamps split stop setup, handler scheduling, handler work and completion return. " +
                "GC counts bracket the operation but do not measure pause duration. JIT/thread-pool counters are sampled per 100 lifetimes. " +
                "CPU/allocation/throughput include whole fixture lifecycle and diagnostic instrumentation. " +
                "Startup and fixed-20-second-warmup variants retain all measured samples; neither substitutes for application startup or shutdown correctness."
        }));
        File.WriteAllText(Path.Combine(folder, "series.json"), JsonSerializer.Serialize(series));
        File.WriteAllText(Path.Combine(folder, "warmup-series.json"), JsonSerializer.Serialize(warmupSeries));
    }

    private static object Snapshot(Process process, long started, int samples)
    {
        process.Refresh();
        return new
        {
            Samples = samples, CpuTicks = process.TotalProcessorTime.Ticks,
            AllocatedBytes = GC.GetTotalAllocatedBytes(false), HeapBytes = GC.GetTotalMemory(false), RssBytes = process.WorkingSet64,
            Gen0 = GC.CollectionCount(0), Gen1 = GC.CollectionCount(1), Gen2 = GC.CollectionCount(2),
            JitMethods = JitInfo.GetCompiledMethodCount(false), JitMilliseconds = JitInfo.GetCompilationTime(false).TotalMilliseconds,
            ThreadPoolThreads = ThreadPool.ThreadCount, PendingWorkItems = ThreadPool.PendingWorkItemCount,
            ElapsedSeconds = Stopwatch.GetElapsedTime(started).TotalSeconds
        };
    }
}
