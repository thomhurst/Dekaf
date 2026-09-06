using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Dekaf.Diagnostics;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Separates trace-context materialization from the zero-allocation no-listener guard.
/// ActivityTraceId/ActivitySpanId own strings when a valid context is materialized.
/// </summary>
[MemoryDiagnoser]
public class TraceContextExtractionBenchmarks
{
    private readonly Header[] _valid = [new("traceparent", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"u8.ToArray())];
    private readonly Header[] _withState =
    [
        new("traceparent", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"u8.ToArray()),
        new("tracestate", "vendor=value"u8.ToArray())
    ];
    private readonly Header[] _invalid = [new("traceparent", "zz-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"u8.ToArray())];

    [Benchmark]
    public ActivityContext? Valid() => TraceContextPropagator.ExtractTraceContext(_valid);

    [Benchmark]
    public ActivityContext? WithTracestate() => TraceContextPropagator.ExtractTraceContext(_withState);

    [Benchmark]
    public ActivityContext? InvalidVersion() => TraceContextPropagator.ExtractTraceContext(_invalid);

    [Benchmark]
    public ActivityContext? NoHeaders() => TraceContextPropagator.ExtractTraceContext(null);

    [Benchmark]
    public ActivityContext? NoListenerGuard() => DekafDiagnostics.Source.HasListeners()
        ? TraceContextPropagator.ExtractTraceContext(_valid)
        : null;
}
