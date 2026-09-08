using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Dekaf.Networking;
using Perfolizer.Horology;
using Perfolizer.Mathematics.OutlierDetection;

namespace Dekaf.Benchmarks;

internal static class Program
{
    internal static bool Smoke;
    public static int Main(string[] args)
    {
        Smoke = args.Contains("--smoke");
        Console.WriteLine(JsonSerializer.Serialize(new[] { typeof(Program).Assembly, typeof(PendingRequestPool).Assembly, typeof(Reservoir.ObjectPool<>).Assembly }
            .Select(a => new { a.FullName, a.Location, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))) })));
        using var runtime = new RuntimeLogger(Path.Combine(args[0], "runtime.csv"));
        var config = DefaultConfig.Instance.WithArtifactsPath(args[0]).AddLogger(runtime).AddExporter(JsonExporter.Full)
            .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance).WithAffinity(new IntPtr(4))
                .WithWarmupCount(Smoke ? 1 : 30).WithIterationCount(Smoke ? 1 : 25)
                .WithIterationTime(TimeInterval.FromMilliseconds(1000)).WithOutlierMode(OutlierMode.DontRemove));
        var summary = BenchmarkRunner.Run<PendingRequestBenchmark>(config);
        return summary.HasCriticalValidationErrors || summary.Reports.Length == 0 || summary.Reports.Any(r => !r.Success) ? 1 : 0;
    }
}

[MemoryDiagnoser]
public class PendingRequestBenchmark
{
    private PendingRequestPool _pool = null!;
    [GlobalSetup]
    public void Setup()
    {
        _pool = new PendingRequestPool();
        var first = _pool.Rent(); _pool.Return(first);
        for (var i = 0; i < 10000; i++)
        {
            var item = _pool.Rent();
            if (!ReferenceEquals(item, first)) throw new InvalidOperationException("Same-thread identity changed.");
            _pool.Return(item);
            if (RentReturn() != 1) throw new InvalidOperationException("Pool count changed.");
        }
        var timer = Stopwatch.StartNew(); long completed = 0; var next = 1d;
        using var process = Process.GetCurrentProcess();
        do
        {
            if (RentReturn() != 1) throw new InvalidOperationException("Warmup count changed.");
            completed++;
            if (!Program.Smoke && timer.Elapsed.TotalSeconds >= next)
            {
                process.Refresh();
                Console.WriteLine($"WARM seconds={timer.Elapsed.TotalSeconds:F6} completed={completed} jit={System.Runtime.JitInfo.GetCompiledMethodCount()} threads={ThreadPool.ThreadCount} cpuMs={process.TotalProcessorTime.TotalMilliseconds:F3} gc0={GC.CollectionCount(0)} gc1={GC.CollectionCount(1)} gc2={GC.CollectionCount(2)} heap={GC.GetTotalMemory(false)} rss={process.WorkingSet64}");
                next++;
            }
        } while (!Program.Smoke && timer.Elapsed.TotalSeconds < 20);
        Console.WriteLine($"WARM completed seconds={timer.Elapsed.TotalSeconds:F6} calls={completed}");
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) RentReturn();
        Console.WriteLine($"RAW calls=1000 bytes={GC.GetAllocatedBytesForCurrentThread() - before}");
    }
    [Benchmark]
    public int RentReturn()
    {
        var request = _pool.Rent();
        _pool.Return(request);
        return _pool.ApproximateCount;
    }
    [GlobalCleanup]
    public void Cleanup()
    {
        if (_pool.ApproximateCount != 1) throw new InvalidOperationException("Final pool count changed.");
    }
}
