using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Dekaf.Benchmarks.Infrastructure;

var config = DefaultConfig.Instance
    .WithOptions(ConfigOptions.DisableOptimizationsValidator);

if (BenchmarkJobSelection.GetExplicitJob(args) is { } selectedJob)
{
    var jobId = selectedJob switch
    {
        BenchmarkJob.Dry => Job.Dry.ResolvedId,
        BenchmarkJob.Short => Job.ShortRun.ResolvedId,
        BenchmarkJob.Medium => Job.MediumRun.ResolvedId,
        BenchmarkJob.Long => Job.LongRun.ResolvedId,
        BenchmarkJob.Default => Job.Default.ResolvedId,
        _ => throw new System.Diagnostics.UnreachableException()
    };

    config = config.AddFilter(new SimpleFilter(benchmarkCase =>
        string.Equals(benchmarkCase.Job.ResolvedId, jobId, StringComparison.Ordinal)));
}

// Pass all arguments to BenchmarkSwitcher for flexible filtering
// Examples:
//   dotnet run -c Release -- --filter "*Unit*"     (run unit benchmarks)
//   dotnet run -c Release -- --filter "*Client*"   (run client benchmarks)
//   dotnet run -c Release -- --filter "*Producer*" (run producer benchmarks)
//   dotnet run -c Release -- --filter "*Producer*" --job Dry (override class jobs)
//   dotnet run -c Release                          (run all benchmarks)

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
    .Run(args, config);
