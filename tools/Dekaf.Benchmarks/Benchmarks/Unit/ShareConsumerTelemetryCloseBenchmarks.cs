using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Per-request close telemetry with cached broker state; every response completes synchronously.</summary>
[MemoryDiagnoser]
public class ShareConsumerTelemetryCloseBenchmarks
{
    private HostedShareRequestBenchmarks _requests = null!;

    [Params(false, true)]
    public bool Hosted { get; set; }

    [ParamsAllValues]
    public ShareRequestTelemetryMode Telemetry { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _requests = new() { Hosted = Hosted, PartitionCount = 16, TelemetryMode = Telemetry };
        await _requests.Setup();
        await _requests.CloseSession();
    }

    [Benchmark]
    public Task CloseSession() => _requests.CloseSession();

    [GlobalCleanup]
    public Task Cleanup() => _requests.Cleanup();
}
