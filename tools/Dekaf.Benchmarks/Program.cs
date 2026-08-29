using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Dekaf.Benchmarks.Infrastructure;

var config = DefaultConfig.Instance
    .WithOptions(ConfigOptions.DisableOptimizationsValidator);
var benchmarkArguments = BenchmarkJobSelection.ExpandResponseFiles(args);

if (BenchmarkJobSelection.GetExplicitJob(benchmarkArguments) is { } selectedJob)
{
    var job = selectedJob switch
    {
        BenchmarkJob.Dry => Job.Dry,
        BenchmarkJob.Short => Job.ShortRun,
        BenchmarkJob.Medium => Job.MediumRun,
        BenchmarkJob.Long => Job.LongRun,
        BenchmarkJob.Default => Job.Default,
        _ => throw new System.Diagnostics.UnreachableException()
    };

    config = config.AddJob(job);
    config = config.AddFilter(new SimpleFilter(benchmarkCase =>
        string.Equals(benchmarkCase.Job.ResolvedId, job.ResolvedId, StringComparison.Ordinal)));
}

// Pass all arguments to BenchmarkSwitcher for flexible filtering
// Examples:
//   dotnet run -c Release -- --filter "*Unit*"     (run unit benchmarks)
//   dotnet run -c Release -- --filter "*Client*"   (run client benchmarks)
//   dotnet run -c Release -- --filter "*Producer*" (run producer benchmarks)
//   dotnet run -c Release -- --filter "*Producer*" --job Dry (override class jobs)
//   dotnet run -c Release                          (run all benchmarks)

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
    .Run(benchmarkArguments, config);
