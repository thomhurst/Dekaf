
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Perfolizer.Horology;
using Perfolizer.Mathematics.OutlierDetection;

public static class Program
{
    internal static bool Validate;
    internal static bool RenewalStore;
    internal static readonly bool HasMetrics = typeof(Dekaf.Outbox.OutboxRelayOptions).GetProperty("MetricsName") is not null;
    public static int Main(string[] args)
    {
        RenewalStore = args[0].StartsWith("renewal-", StringComparison.Ordinal);
        if (RenewalStore) args[0] = args[0][8..];
        Validate = args[0] == "validate" || args.Contains("--smoke");
        if (args[0] == "validate")
        {
            foreach (var renewal in new[] { false, true })
            foreach (var enabled in new[] { false, true })
            {
                RenewalStore = renewal;
                var sync = new SyncBench { Enabled = enabled };
                sync.Setup(); sync.Workload().GetAwaiter().GetResult(); sync.Cleanup();
                var pending = new PendingBench { Enabled = enabled };
                pending.Setup(); pending.Workload().GetAwaiter().GetResult(); pending.Cleanup();
            }
            Console.WriteLine("All eight fixtures validated");
            return 0;
        }
        using var runtimeLog = new RuntimeLogger(Path.Combine(args[1], "runtime.csv"));
        var config = DefaultConfig.Instance.WithArtifactsPath(args[1])
            .AddLogger(runtimeLog)
            .AddExporter(JsonExporter.Full)
            .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance)
                .WithAffinity(new IntPtr(1L << int.Parse(Environment.GetEnvironmentVariable("ABA_CPU") ?? "0")))
                .WithWarmupCount(Validate ? 1 : 30).WithIterationCount(Validate ? 1 : 25)
                .WithIterationTime(TimeInterval.FromMilliseconds(1000)).WithOutlierMode(OutlierMode.DontRemove))
            .AddFilter(new SimpleFilter(b => (bool)b.Parameters.Items.Single(p => p.Name == "Enabled").Value == args[0].EndsWith("on")));
        var summary = args[0].StartsWith("sync") ? BenchmarkRunner.Run<SyncBench>(config) : BenchmarkRunner.Run<PendingBench>(config);
        return summary.HasCriticalValidationErrors || summary.Reports.Length == 0 || summary.Reports.Any(r => !r.Success) ? 1 : 0;
    }

    internal static void Warm(Func<ValueTask> operation, RelayMetricsBenchmarks fixture, bool enabled)
    {
        var timer = Stopwatch.StartNew();
        long cycles = 0;
        var next = 1d;
        using var process = Process.GetCurrentProcess();
        do
        {
            operation().GetAwaiter().GetResult();
            cycles++;
            if (!Validate && timer.Elapsed.TotalSeconds >= next)
            {
                process.Refresh();
                Console.WriteLine($"WARM seconds={timer.Elapsed.TotalSeconds:F3} cycles={cycles} jit={System.Runtime.JitInfo.GetCompiledMethodCount()} threads={ThreadPool.ThreadCount} cpuMs={process.TotalProcessorTime.TotalMilliseconds:F3} gc0={GC.CollectionCount(0)} gc1={GC.CollectionCount(1)} gc2={GC.CollectionCount(2)} heap={GC.GetTotalMemory(false)} rss={process.WorkingSet64}");
                next++;
            }
        } while (!Validate && timer.Elapsed.TotalSeconds < 20);
        if (fixture.Deleted != cycles * 500 || fixture.Acknowledged != (enabled && HasMetrics ? cycles * 500 : 0))
            throw new InvalidOperationException("Warmup delivery or metric count mismatch");
        Console.WriteLine($"WARM completed seconds={timer.Elapsed.TotalSeconds:F3} cycles={cycles} deleted={fixture.Deleted} acknowledged={fixture.Acknowledged}");
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) operation().GetAwaiter().GetResult();
        Console.WriteLine($"RAW cycles=1000 bytes={GC.GetAllocatedBytesForCurrentThread() - before}");
    }
}

[MemoryDiagnoser]
public class SyncBench
{
    [Params(false,true)] public bool Enabled { get; set; }
    private RelayMetricsBenchmarks _fixture = null!;
    [GlobalSetup] public void Setup()
    {
        _fixture = new RelayMetricsBenchmarks { Enabled = Enabled };
        _fixture.Setup();
        Program.Warm(_fixture.SynchronousBatch, _fixture, Enabled);
    }
    [Benchmark] public ValueTask Workload() => _fixture.SynchronousBatch();
    [GlobalCleanup] public void Cleanup()
    {
        if (_fixture.Deleted <= 0 || _fixture.Deleted % 500 != 0
            || _fixture.Acknowledged != (Enabled && Program.HasMetrics ? _fixture.Deleted : 0))
            throw new InvalidOperationException("Measured synchronous delivery or metric count mismatch");
        _fixture.Cleanup();
    }
}

[MemoryDiagnoser]
public class PendingBench
{
    [Params(false,true)] public bool Enabled { get; set; }
    private RelayMetricsBenchmarks _fixture = null!;
    [GlobalSetup] public void Setup()
    {
        _fixture = new RelayMetricsBenchmarks { Enabled = Enabled };
        _fixture.Setup();
        Program.Warm(_fixture.PendingBatch, _fixture, Enabled);
    }
    [Benchmark] public ValueTask Workload() => _fixture.PendingBatch();
    [GlobalCleanup] public void Cleanup()
    {
        if (_fixture.Deleted <= 0 || _fixture.Deleted % 500 != 0
            || _fixture.Acknowledged != (Enabled && Program.HasMetrics ? _fixture.Deleted : 0))
            throw new InvalidOperationException("Measured pending delivery or metric count mismatch");
        _fixture.Cleanup();
    }
}
