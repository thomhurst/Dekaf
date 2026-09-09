using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Running;

var config = DefaultConfig.Instance.AddExporter(JsonExporter.Full)
    .WithOptions(ConfigOptions.KeepBenchmarkFiles | ConfigOptions.DisableParallelBuild);
if (Environment.GetEnvironmentVariable("ABA_PROFILE") == "1")
    config = config.AddDiagnoser(new EventPipeProfiler(EventPipeProfile.CpuSampling,
        performExtraBenchmarksRun: true));

var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config).ToArray();
return summaries.Length == 0 || summaries.Any(summary => summary.HasCriticalValidationErrors
    || summary.Reports.Length == 0 || summary.Reports.Any(report => !report.Success
        || report.ResultStatistics is null || report.ResultStatistics.N < 1)) ? 1 : 0;
