using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text.Json;

// Diagnostic event delivery timestamps identify candidate methods for investigation.
// They do not replace runtime counters or justify removing measured samples.
internal sealed class CompilationLog(string outputPath) : EventListener
{
    private readonly Compilation?[] _events = new Compilation?[65536];
    private readonly List<PhaseBoundary> _phases = new(8);
    private int _count;

    internal void Phase(string name) => _phases.Add(new(name, Stopwatch.GetTimestamp()));

    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name == "Microsoft-Windows-DotNETRuntime")
            EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x10);
    }

    protected override void OnEventWritten(EventWrittenEventArgs args)
    {
        if (args.EventId != 145 || _events is null || args.Payload is null)
            return;
        var index = Interlocked.Increment(ref _count) - 1;
        if (index < _events.Length)
            _events[index] = new Compilation(Stopwatch.GetTimestamp(), args.Payload.ToArray());
    }

    public override void Dispose()
    {
        base.Dispose();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(new
        {
            frequency = Stopwatch.Frequency,
            total_events = _count,
            overflow = _count > _events.Length,
            phases = _phases,
            events = _events.Take(Math.Min(_count, _events.Length)).ToArray()
        }));
    }

    private sealed record Compilation(long Timestamp, object?[] Payload);
    private sealed record PhaseBoundary(string Name, long Timestamp);
}
