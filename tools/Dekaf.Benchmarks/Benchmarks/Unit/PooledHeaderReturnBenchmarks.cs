using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Allocation and latency gate for returning traced producer-header arrays.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class PooledHeaderReturnBenchmarks
{
    private Header _traceparent;
    private Header _tracestate;
    private Activity _activity = null!;

    [GlobalSetup]
    public void Setup()
    {
        _activity = new Activity("pooled-header-return")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        _activity.TraceStateString = "vendor=value";
        var headers = Diagnostics.TraceContextPropagator.InjectTraceContext(
            new Headers(2),
            _activity)!;
        _traceparent = headers[0];
        _tracestate = headers[1];
    }

    [GlobalCleanup]
    public void Cleanup() => _activity.Dispose();

    [Benchmark]
    public void ReturnTracedHeaders()
    {
        var headers = ProducerContainerPools.Headers.Rent(2);
        headers[0] = _traceparent;
        headers[1] = _tracestate;
        RecordAccumulator.ReturnPooledHeaders(headers, 2);
    }
}
