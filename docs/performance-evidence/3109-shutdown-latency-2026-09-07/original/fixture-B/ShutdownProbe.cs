using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Dekaf.Benchmarks;

internal static class ShutdownProbe
{
    internal static async Task Run(string folder, int samples)
    {
        Directory.CreateDirectory(folder);
        var fixture = new PartitionedShutdownBenchmarks();
        for (var i = 0; i < 100; i++)
        {
            fixture.Setup();
            await fixture.DrainFullQueue();
            fixture.Cleanup();
        }
        var latencies = new long[samples];
        var series = new List<object>();
        using var process = Process.GetCurrentProcess();
        var cpuStart = process.TotalProcessorTime.Ticks;
        var allocatedStart = GC.GetTotalAllocatedBytes(true);
        var started = Stopwatch.GetTimestamp();
        for (var i = 0; i < samples; i++)
        {
            fixture.Setup();
            var stopStart = Stopwatch.GetTimestamp();
            await fixture.DrainFullQueue();
            latencies[i] = Stopwatch.GetTimestamp() - stopStart;
            fixture.Cleanup(); // Asserts all 1024 records completed, checkpoint 1024, queue drained.
            if ((i + 1) % 100 == 0)
            {
                process.Refresh();
                series.Add(new
                {
                    Samples = i + 1, CpuTicks = process.TotalProcessorTime.Ticks,
                    AllocatedBytes = GC.GetTotalAllocatedBytes(false), HeapBytes = GC.GetTotalMemory(false),
                    RssBytes = process.WorkingSet64, Gen0 = GC.CollectionCount(0),
                    Gen1 = GC.CollectionCount(1), Gen2 = GC.CollectionCount(2),
                    ElapsedSeconds = Stopwatch.GetElapsedTime(started).TotalSeconds
                });
            }
        }
        var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
        var cpu = process.TotalProcessorTime.Ticks - cpuStart;
        var allocated = GC.GetTotalAllocatedBytes(true) - allocatedStart;
        using (var output = File.Create(Path.Combine(folder, "latency-ticks.bin")))
            output.Write(MemoryMarshal.AsBytes(latencies.AsSpan()));
        Array.Sort(latencies);
        var ns = 1_000_000_000d / Stopwatch.Frequency;
        File.WriteAllText(Path.Combine(folder, "metrics.json"), JsonSerializer.Serialize(new
        {
            Mode = "shutdown-full-queue", Samples = samples, RecordsPerSample = 1024,
            Completed = samples * 1024L, Failures = 0, BacklogAtEnd = 0,
            LifecycleOperationsPerSecond = samples / elapsed,
            LifecycleCpuNsPerOperation = cpu * 100d / samples,
            LifecycleAllocatedBytesPerOperation = allocated / (double)samples,
            P50Ns = latencies[samples / 2 - 1] * ns,
            P99Ns = latencies[(int)Math.Ceiling(samples * .99) - 1] * ns,
            MaxNs = latencies[^1] * ns, ElapsedSeconds = elapsed, StopwatchFrequency = Stopwatch.Frequency,
            Scope = "Stop latency: full 1024-record queue, request shutdown to completed drain and rejected writer. " +
                "CPU/allocation/throughput: whole lifecycle including fixture setup, validation, and sampler. " +
                "BDN separately measures stop-only allocations. Candidate unit/integration tests cover handler commits, stalled lanes, cancellation and broker offsets."
        }));
        File.WriteAllText(Path.Combine(folder, "series.json"), JsonSerializer.Serialize(series));
    }
}
