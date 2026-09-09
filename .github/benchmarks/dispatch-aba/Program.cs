using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Dekaf.Benchmarks.Benchmarks.Unit;
using Perfolizer.Horology;
using Perfolizer.Mathematics.OutlierDetection;

public static class Program
{
    internal static bool Smoke;
    internal static bool RequireZero;

    public static int Main(string[] args)
    {
        if (args[0] == "shutdown")
            return ShutdownProbe.Run(args).GetAwaiter().GetResult();
        var pattern = Enum.Parse<KeyOrderedDispatchBenchmarks.KeyPattern>(args[0]);
        var batchSize = int.Parse(args[1]);
        Smoke = args.Contains("--smoke");
        RequireZero = args.Contains("--require-zero");
        using var runtime = new RuntimeLogger(Path.Combine(args[2], "runtime.csv"));
        var config = DefaultConfig.Instance.WithArtifactsPath(args[2]).AddLogger(runtime)
            .AddExporter(JsonExporter.Full)
            .AddFilter(new SimpleFilter(test =>
                (KeyOrderedDispatchBenchmarks.KeyPattern)test.Parameters[nameof(KeyOrderedDispatchBenchmarks.Pattern)] == pattern
                && (int)test.Parameters[nameof(KeyOrderedDispatchBenchmarks.BatchSize)] == batchSize))
            .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance)
                .WithWarmupCount(Smoke ? 1 : 30).WithIterationCount(Smoke ? 1 : 25)
                .WithIterationTime(TimeInterval.FromMilliseconds(Smoke ? 1 : 1000))
                .WithOutlierMode(OutlierMode.DontRemove));
        var summary = BenchmarkRunner.Run<KeyOrderedDispatchBenchmarks>(config);
        return summary.HasCriticalValidationErrors || summary.Reports.Length != 1 || !summary.Reports[0].Success ? 1 : 0;
    }
}
