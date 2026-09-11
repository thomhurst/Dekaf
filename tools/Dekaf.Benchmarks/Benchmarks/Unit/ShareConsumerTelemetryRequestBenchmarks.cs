using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

public enum ShareRequestTelemetryMode { Unsubscribed, Subscribed, Disabled }

/// <summary>
/// Steady-state write observation and telemetry for successful requests carrying 16 acknowledgement batches.
/// Setup warms broker state and cached callbacks; Disabled measures requests after a subscription is disabled.
/// Costs are per request, excluding transport, parsing and the one-time broker context allocation.
/// </summary>
[MemoryDiagnoser]
public class ShareConsumerTelemetryRequestBenchmarks
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
    }

    [Benchmark]
    public Task ShareFetch() => _requests.ShareFetch();

    [Benchmark]
    public Task ShareAcknowledge() => _requests.ShareAcknowledge();

    [GlobalCleanup]
    public Task Cleanup() => _requests.Cleanup();
}
