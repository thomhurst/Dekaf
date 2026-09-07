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
        var seconds = double.Parse(Environment.GetEnvironmentVariable("ADMIN_EVIDENCE_WARMUP_SECONDS") ?? "30",
            System.Globalization.CultureInfo.InvariantCulture);
        var warmup = await Probe.MeasureAsync(_fixture, seconds);
        var output = Environment.GetEnvironmentVariable("ADMIN_EVIDENCE_WARMUP_DIRECTORY") ?? Path.GetTempPath();
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
