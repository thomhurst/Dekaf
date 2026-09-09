using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks;

[MemoryDiagnoser]
public class AdminEvidenceBenchmark
{
    [ParamsSource(nameof(Cases))]
    public string Case { get; set; } = "legacy:32";
    public static IEnumerable<string> Cases => (Environment.GetEnvironmentVariable("ADMIN_EVIDENCE_CASES") ?? "legacy:32,inventory:32").Split(',');
    private AdminFixture _fixture = null!;
    private RuntimeSampler _sampler = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _sampler = new RuntimeSampler();
        _fixture = new AdminFixture(Case);
        await _fixture.InitializeAsync();
        var output = Environment.GetEnvironmentVariable("ADMIN_EVIDENCE_WARMUP_DIRECTORY") ?? Path.GetTempPath();
        Probe.Save(Path.Combine(output, $"clock-{Case.Replace(':', '-')}-{Environment.ProcessId}.json"),
            new { _sampler.StartedTimestamp, StopwatchFrequency = System.Diagnostics.Stopwatch.Frequency,
                ProcessId = Environment.ProcessId });
        var enginePrimer = BenchmarkEnginePrimer.Warm(this);
        Probe.Save(Path.Combine(output, $"engine-primer-{Case.Replace(':', '-')}-{Environment.ProcessId}.json"), enginePrimer);
        Probe.SaveLoadedBinaries(Path.Combine(output, $"binaries-{Case.Replace(':', '-')}-{Environment.ProcessId}.json"));
        await Probe.PrimeAsync(_fixture, Path.Combine(output, $"primer-{Case.Replace(':', '-')}-{Environment.ProcessId}.json"));
        var seconds = double.Parse(Environment.GetEnvironmentVariable("ADMIN_EVIDENCE_WARMUP_SECONDS") ?? "120",
            System.Globalization.CultureInfo.InvariantCulture);
        var warmup = await Probe.MeasureAsync(_fixture, seconds);
        Probe.Save(Path.Combine(output, $"{Case.Replace(':', '-')}-{Environment.ProcessId}.json"), warmup);
        Console.WriteLine($"BDN workload warmup: {warmup.Seconds:F3}s; {warmup.Completed} completed calls; JIT methods {warmup.Start.JitMethods}/{warmup.End.JitMethods}");
    }
    [Benchmark]
    public ValueTask<int> Invoke() => _fixture.Call();
    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _sampler.DisposeAsync();
        var output = Environment.GetEnvironmentVariable("ADMIN_EVIDENCE_WARMUP_DIRECTORY") ?? Path.GetTempPath();
        Probe.Save(Path.Combine(output, $"runtime-{Case.Replace(':', '-')}-{Environment.ProcessId}.json"), _sampler.Rows);
        await _fixture.DisposeAsync();
    }
}
