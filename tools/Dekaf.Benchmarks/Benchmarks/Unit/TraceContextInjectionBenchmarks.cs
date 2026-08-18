using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Allocation gate for injecting an already-created W3C activity into reusable headers.
/// Activity creation and export are measured separately by the producer tracing benchmark.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class TraceContextInjectionBenchmarks
{
    private readonly Headers _headers = new(1);
    private readonly Headers _tracestateHeaders = new(2);
    private readonly Header _leadingHeader = new("leading", "removed"u8.ToArray());
    private readonly Header _serializerHeader = new("serializer", "appended"u8.ToArray());
    private readonly byte[] _destination = new byte[80];
    private readonly byte[] _tracestateDestination = new byte[128];
    private Activity _activity = null!;
    private Activity _tracestateActivity = null!;

    [GlobalSetup]
    public void Setup()
    {
        _activity = new Activity("trace-context-injection")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        _tracestateActivity = new Activity("tracestate-context-injection")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        _tracestateActivity.TraceStateString = "vendor=value";
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _activity.Dispose();
        _tracestateActivity.Dispose();
    }

    [Benchmark]
    public int Inject()
    {
        _headers.Clear();
        return Diagnostics.TraceContextPropagator.InjectTraceContext(_headers, _activity)!.Count;
    }

    [Benchmark]
    public int InjectAndEncode()
    {
        _headers.Clear();
        Diagnostics.TraceContextPropagator.InjectTraceContext(_headers, _activity);
        var offset = 0;
        var traceparent = _headers[0];
        HeaderProtocol.Encode(in traceparent, _destination, ref offset);
        return offset;
    }

    [Benchmark]
    public int InjectAndRemove()
    {
        _headers.Clear();
        Diagnostics.TraceContextPropagator.InjectTraceContext(_headers, _activity);
        _headers.RemoveDeferredTraceContext();
        return _headers.Count;
    }

    [Benchmark]
    public int InjectAppendAndRemove()
    {
        _headers.Clear();
        Diagnostics.TraceContextPropagator.InjectTraceContext(_headers, _activity);
        _headers.Add(_serializerHeader);
        _headers.RemoveDeferredTraceContext();
        return _headers.Count;
    }

    [Benchmark]
    public int InjectRemoveLeadingAndRemove()
    {
        _headers.Clear();
        _headers.Add(_leadingHeader);
        Diagnostics.TraceContextPropagator.InjectTraceContext(_headers, _activity);
        _headers.Remove("leading");
        _headers.RemoveDeferredTraceContext();
        return _headers.Count;
    }

    [Benchmark]
    public int InjectTracestateAndEncode()
    {
        _tracestateHeaders.Clear();
        Diagnostics.TraceContextPropagator.InjectTraceContext(
            _tracestateHeaders,
            _tracestateActivity);
        var offset = 0;
        var traceparent = _tracestateHeaders[0];
        var tracestate = _tracestateHeaders[1];
        HeaderProtocol.Encode(in traceparent, _tracestateDestination, ref offset);
        HeaderProtocol.Encode(in tracestate, _tracestateDestination, ref offset);
        return offset;
    }
}
