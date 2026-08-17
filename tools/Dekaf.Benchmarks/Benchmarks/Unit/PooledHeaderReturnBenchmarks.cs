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
    private readonly Headers _headers = new(4);
    private readonly Header _existingHeader = new("existing", "value"u8.ToArray());
    private Header _traceparent;
    private Header _tracestate;
    private readonly Header _serializerHeader = new("serializer", "appended"u8.ToArray());
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

    [Benchmark]
    public void ReturnTracedHeadersAfterAppend()
    {
        var headers = ProducerContainerPools.Headers.Rent(4);
        headers[0] = _serializerHeader;
        headers[1] = _traceparent;
        headers[2] = _tracestate;
        RecordAccumulator.ReturnPooledHeaders(headers, 3);
    }

    [Benchmark]
    public int RentFillAndReturnAfterAppend()
    {
        _headers.Clear();
        _headers.Add(_existingHeader);
        _headers.AddDeferredTraceContext(_activity, "vendor=value");
        _headers.Add(_serializerHeader);

        KafkaProducer<string, string>.RentAndFillHeaders(
            _headers,
            out var headers,
            out var headerCount);
        RecordAccumulator.ReturnPooledHeaders(headers, headerCount);
        return headerCount;
    }

    [Benchmark]
    public int RentFillAndReturnNormalHeaders()
    {
        _headers.Clear();
        _headers.Add(_existingHeader);
        _headers.Add(_serializerHeader);

        KafkaProducer<string, string>.RentAndFillHeaders(
            _headers,
            out var headers,
            out var headerCount);
        RecordAccumulator.ReturnPooledHeaders(headers, headerCount);
        return headerCount;
    }
}
