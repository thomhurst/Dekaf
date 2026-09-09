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
    internal static bool Baseline;
    public static int Main(string[] args)
    {
        var cpu = int.Parse(Environment.GetEnvironmentVariable("DEKAF_BENCHMARK_CPU") ?? "2");
        if (cpu < 0 || cpu >= IntPtr.Size * 8)
            throw new InvalidOperationException("DEKAF_BENCHMARK_CPU does not fit the process affinity mask.");
        Smoke = args.Contains("--smoke");
        Baseline = args.Contains("--baseline");
        Console.WriteLine(JsonSerializer.Serialize(new[] { typeof(Program).Assembly, typeof(PendingRequestPool).Assembly, typeof(Reservoir.ObjectPool<>).Assembly }
            .Select(a => new { a.FullName, a.Location, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))) })));
        var config = DefaultConfig.Instance.WithArtifactsPath(args[0]).AddExporter(JsonExporter.Full)
            .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance).WithAffinity(new IntPtr(1L << cpu))
                .WithWarmupCount(Smoke ? 1 : 30).WithIterationCount(Smoke ? 1 : 25)
                .WithIterationTime(TimeInterval.FromMilliseconds(1000)).WithOutlierMode(OutlierMode.DontRemove));
        var summary = args.Contains("--recovery")
            ? BenchmarkRunner.Run<PendingRequestRecoveryBenchmark>(config)
            : BenchmarkRunner.Run<PendingRequestBenchmark>(config);
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
        var timer = Stopwatch.StartNew(); long completed = 0;
        do
        {
            if (RentReturn() != 1) throw new InvalidOperationException("Warmup count changed.");
            completed++;
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
