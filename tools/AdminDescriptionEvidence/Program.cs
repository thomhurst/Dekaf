using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.CsProj;
using Perfolizer.Horology;
using Perfolizer.Mathematics.OutlierDetection;
using Dekaf.Benchmarks;

if (args.Length > 0 && args[0] == "probe")
{
    if (args.Length != 5) throw new ArgumentException("probe CASE OUTPUT WARMUP_SECONDS MEASURED_SECONDS");
    await using var fixture = new AdminFixture(args[1]);
    await fixture.InitializeAsync();
    // Exercise result finalization and serialization before the full workload warmup.
    var primer = await Probe.MeasureAsync(fixture, 1);
    Probe.Save(Path.Combine(args[2], "primer.json"), primer);
    var warmup = await Probe.MeasureAsync(fixture, double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
    var measured = await Probe.MeasureAsync(fixture, double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture));
    Probe.Save(Path.Combine(args[2], "warmup.json"), warmup);
    Probe.Save(Path.Combine(args[2], "measured.json"), measured);
    Console.WriteLine($"{args[1]} warmup={warmup.Seconds:F3}s/{warmup.Completed} calls measured={measured.Seconds:F3}s/{measured.Completed} calls");
    return 0;
}
var smoke = args.Contains("--smoke-bdn");
args = args.Where(static argument => argument != "--smoke-bdn").ToArray();
var builtIn = CsProjCoreToolchain.NetCoreApp10_0;
var toolchain = new Toolchain("AdminEvidence", new EvidenceGenerator(), builtIn.Builder, builtIn.Executor);
var job = (smoke ? Job.Dry : Job.Default.WithIterationCount(12).WithIterationTime(TimeInterval.FromMilliseconds(500))
    .WithWarmupCount(6).WithLaunchCount(1)).WithOutlierMode(OutlierMode.DontRemove).WithToolchain(toolchain);
var summaries = BenchmarkSwitcher.FromAssembly(typeof(AdminEvidenceBenchmark).Assembly)
    .Run(args, DefaultConfig.Instance.AddJob(job));
return summaries.Any(summary => summary.HasCriticalValidationErrors || summary.Reports.Any(report => !report.Success)) ? 1 : 0;
