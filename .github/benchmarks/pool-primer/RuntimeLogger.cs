using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Loggers;

// In-process BDN emits these lines after each workload interval. Sampling here
// avoids an extra background thread and does not enter the timed operation.
internal sealed class RuntimeLogger(string path) : ILogger, IDisposable
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly Sample[] _samples = new Sample[256];
    private int _count;

    public string Id => nameof(RuntimeLogger);
    public int Priority => 0;
    public void Write(LogKind logKind, string text) { }
    public void WriteLine() { }
    public void Flush() { }

    public void Prime(TimeSpan duration, string outputPath)
    {
        if (_count != 0)
            throw new InvalidOperationException("Sampler priming must precede workload samples.");

        var series = new List<(double Seconds, long Calls, Sample Sample)>(32);
        var timer = Stopwatch.StartNew();
        long completed = 0;
        var next = 1d;
        do
        {
            PrimeCallbacks(this);
            if (_count != 3)
                throw new InvalidOperationException("Primer callbacks did not produce three samples.");
            completed += 3;
            var seconds = timer.Elapsed.TotalSeconds;
            if (seconds >= next || seconds >= duration.TotalSeconds)
            {
                series.Add((seconds, completed, _samples[_count - 1]));
                next++;
            }
            // Only premeasurement samples are reset. Later BDN samples are never cleared.
            _count = 0;
        } while (timer.Elapsed < duration);

        var elapsed = timer.Elapsed.TotalSeconds;
        if (series.Count == 0 || series[^1].Calls != completed)
            series.Add((elapsed, completed, _samples[2]));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var writer = new StreamWriter(outputPath);
        writer.WriteLine("seconds,callbacks,timestamp,cpu_ms,jit_methods,jit_ms,threads,pending_work,gc0,gc1,gc2,allocated_bytes,heap_bytes,rss_bytes");
        foreach (var point in series)
        {
            var sample = point.Sample;
            writer.WriteLine(FormattableString.Invariant(
                $"{point.Seconds},{point.Calls},{sample.Timestamp},{sample.CpuMs},{sample.JitMethods},{sample.JitMs},{sample.Threads},{sample.PendingWork},{sample.Gc0},{sample.Gc1},{sample.Gc2},{sample.AllocatedBytes},{sample.HeapBytes},{sample.RssBytes}"));
        }
        Console.WriteLine(FormattableString.Invariant($"PRIMER completed seconds={elapsed:F6} callbacks={completed}"));
    }

    // Keep the real ILogger calls observable; an optimized primer can inline/elide callbacks.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void PrimeCallbacks(ILogger logger)
    {
        logger.WriteLine(LogKind.Default, "WorkloadWarmup primer");
        logger.WriteLine(LogKind.Default, "WorkloadActual primer");
        logger.WriteLine(LogKind.Default, "WorkloadJitting primer");
    }

    public void WriteLine(LogKind logKind, string text)
    {
        if (!text.StartsWith("WorkloadWarmup", StringComparison.Ordinal)
            && !text.StartsWith("WorkloadActual", StringComparison.Ordinal)
            && !text.StartsWith("WorkloadJitting", StringComparison.Ordinal))
            return;
        if (_count == _samples.Length)
            throw new InvalidOperationException("Runtime sample storage exhausted.");
        _process.Refresh();
        _samples[_count++] = new Sample(text, Stopwatch.GetTimestamp(),
            _process.TotalProcessorTime.TotalMilliseconds,
            System.Runtime.JitInfo.GetCompiledMethodCount(),
            System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds,
            ThreadPool.ThreadCount, ThreadPool.PendingWorkItemCount,
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
            GC.GetTotalAllocatedBytes(), GC.GetTotalMemory(false), _process.WorkingSet64);
    }

    public void Dispose()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var writer = new StreamWriter(path);
        writer.WriteLine("workload,timestamp,cpu_ms,jit_methods,jit_ms,threads,pending_work,gc0,gc1,gc2,allocated_bytes,heap_bytes,rss_bytes");
        for (var index = 0; index < _count; index++)
        {
            var sample = _samples[index];
            writer.WriteLine(FormattableString.Invariant(
                $"\"{sample.Workload}\",{sample.Timestamp},{sample.CpuMs},{sample.JitMethods},{sample.JitMs},{sample.Threads},{sample.PendingWork},{sample.Gc0},{sample.Gc1},{sample.Gc2},{sample.AllocatedBytes},{sample.HeapBytes},{sample.RssBytes}"));
        }
        _process.Dispose();
    }

    private readonly record struct Sample(string Workload, long Timestamp, double CpuMs,
        long JitMethods, double JitMs, int Threads, long PendingWork, int Gc0, int Gc1, int Gc2,
        long AllocatedBytes, long HeapBytes, long RssBytes);
}
