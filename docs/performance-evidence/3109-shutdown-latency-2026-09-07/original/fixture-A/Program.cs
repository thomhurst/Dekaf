using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Perfolizer.Horology;
using Perfolizer.Mathematics.OutlierDetection;

#if ABA_SHUTDOWN
if (args.Length == 3 && args[0] == "--shutdown-probe")
{
    await Dekaf.Benchmarks.ShutdownProbe.Run(args[1], int.Parse(args[2]));
    return 0;
}
#endif

var phase = Environment.GetEnvironmentVariable("ABA_PHASE") ?? throw new InvalidOperationException("Missing phase.");
var dry = args.Contains("--dry");
var shutdown = Environment.GetEnvironmentVariable("ABA_PR") == "3109";
var job = dry ? Job.Dry : shutdown
    ? Job.Default.WithWarmupCount(30).WithIterationCount(300)
    : Job.Default.WithWarmupCount(8).WithIterationCount(25).WithIterationTime(TimeInterval.FromMilliseconds(250));
job = job.WithId(phase).WithOutlierMode(OutlierMode.DontRemove).WithToolchain(InProcessEmitToolchain.Instance);
var config = DefaultConfig.Instance.AddJob(job).AddExporter(JsonExporter.Full)
    .AddFilter(new SimpleFilter(benchmark => benchmark.Job.ResolvedId == phase));
var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
    .Run(args.Where(argument => argument != "--dry").ToArray(), config).ToArray();
return summaries.Length == 0 || summaries.Any(summary => summary.HasCriticalValidationErrors
    || summary.Reports.Length == 0 || summary.Reports.Any(report => !report.Success
        || report.ResultStatistics is null || report.ResultStatistics.N < 1)) ? 1 : 0;
