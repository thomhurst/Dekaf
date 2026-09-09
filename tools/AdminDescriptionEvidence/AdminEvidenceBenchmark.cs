using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks;

[MemoryDiagnoser]
public class AdminEvidenceBenchmark
{
    [ParamsSource(nameof(Cases))]
    public string Case { get; set; } = "legacy:16";
    public static IEnumerable<string> Cases => (Environment.GetEnvironmentVariable("ADMIN_EVIDENCE_CASES") ?? "legacy:16,inventory:16").Split(',');
    private AdminFixture _fixture = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _fixture = new AdminFixture(Case);
        await _fixture.InitializeAsync();
    }
    [Benchmark]
    public ValueTask<int> Invoke() => _fixture.Call();
    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _fixture.DisposeAsync();
    }
}
