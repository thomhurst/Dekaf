using BenchmarkDotNet.ConsoleArguments;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Dekaf.Benchmarks.Infrastructure;

var config = DefaultConfig.Instance
    .WithOptions(ConfigOptions.DisableOptimizationsValidator);
var benchmarkArguments = BenchmarkJobSelection.ExpandResponseFiles(args);

if (BenchmarkJobSelection.GetExplicitJob(benchmarkArguments) is { } selectedJob)
{
    var baseJob = selectedJob switch
    {
        BenchmarkJob.Dry => Job.Dry,
        BenchmarkJob.Short => Job.ShortRun,
        BenchmarkJob.Medium => Job.MediumRun,
        BenchmarkJob.Long => Job.LongRun,
        BenchmarkJob.VeryLong => Job.VeryLongRun,
        BenchmarkJob.Default => Job.Default,
        _ => throw new System.Diagnostics.UnreachableException()
    };

    var (parsed, commandLineConfig, _) = ConfigParser.Parse(benchmarkArguments, NullLogger.Instance);
    var configuredJobs = parsed ? commandLineConfig.GetJobs().ToArray() : [];

    // BenchmarkDotNet applies customized CLI jobs as mutators to class jobs, so injecting the
    // unmodified preset in that case would duplicate cases and discard the requested settings.
    if (parsed && configuredJobs.All(job => !job.Meta.IsMutator))
    {
        var jobs = configuredJobs.Length == 0 ? [baseJob] : configuredJobs;
        var jobIds = jobs.Select(job => job.ResolvedId).ToHashSet(StringComparer.Ordinal);

        config = config.AddJob(jobs);
        config = config.AddFilter(new SimpleFilter(benchmarkCase =>
            jobIds.Contains(benchmarkCase.Job.ResolvedId)));
    }
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
