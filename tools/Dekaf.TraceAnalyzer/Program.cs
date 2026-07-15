using Dekaf.TraceAnalyzer;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

// Aggregates the CLR events in a .nettrace file into a markdown summary
// (allocations by type, contention, GC pauses, thread-pool adjustments,
// exceptions). dotnet-trace report only analyzes CPU samples, so without this
// the gc/contention/full stress profiles ship raw traces nobody has parsed.
//
// Usage: Dekaf.TraceAnalyzer <trace.nettrace> [--output <summary.md>]

var (tracePath, outputPath) = args switch
{
    [var trace] => (trace, (string?)null),
    [var trace, "--output", var path] => (trace, path),
    _ => (null, null),
};

if (tracePath is null)
{
    Console.Error.WriteLine("Usage: Dekaf.TraceAnalyzer <trace.nettrace> [--output <summary.md>]");
    return 2;
}

if (!File.Exists(tracePath))
{
    Console.Error.WriteLine($"Trace file not found: {tracePath}");
    return 2;
}

var aggregator = new RuntimeEventAggregator();
double suspendStartMs = -1;

using (var source = new EventPipeEventSource(tracePath))
{
    source.Clr.GCAllocationTick += data => aggregator.AddAllocationTick(
        data.TypeName, data.AllocationAmount64, data.AllocationKind == GCAllocationKind.Large);

    source.Clr.ContentionStop += data => aggregator.AddContention(data.DurationNs / 1_000_000.0);

    source.Clr.GCStart += data => aggregator.AddGcStart(data.Depth, data.Reason.ToString());

    // GC pause = EE suspension to restart; pairs within one trace window.
    // Filter to GC-caused suspensions so debugger/shutdown suspends don't count.
    source.Clr.GCSuspendEEStart += data =>
    {
        if (data.Reason is GCSuspendEEReason.SuspendForGC or GCSuspendEEReason.SuspendForGCPrep)
        {
            suspendStartMs = data.TimeStampRelativeMSec;
        }
    };
    source.Clr.GCRestartEEStop += data =>
    {
        if (suspendStartMs >= 0)
        {
            aggregator.AddGcPause(data.TimeStampRelativeMSec - suspendStartMs);
            suspendStartMs = -1;
        }
    };

    source.Clr.ThreadPoolWorkerThreadAdjustmentAdjustment += data =>
        aggregator.AddThreadPoolAdjustment(data.Reason.ToString());

    source.Clr.ExceptionStart += data => aggregator.AddException(data.ExceptionType);

    source.Process();
    aggregator.SetTraceDuration(source.SessionDuration.TotalSeconds);
}

var markdown = aggregator.RenderMarkdown();
if (outputPath is null)
{
    Console.Write(markdown);
}
else
{
    File.WriteAllText(outputPath, markdown);
    Console.WriteLine($"Wrote {outputPath}");
}

return 0;
