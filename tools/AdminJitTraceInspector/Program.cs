using System.Text.Json;
using Microsoft.Diagnostics.Tracing;

if (args.Length != 2) throw new ArgumentException("TRACE OUTPUT_JSON");
var events = new List<object>();
using (var source = new EventPipeEventSource(args[0]))
{
    source.Clr.MethodJittingStarted += data => events.Add(new
    {
        Kind = "jit", Milliseconds = data.TimeStampRelativeMSec, TimeStampQPC = ReadQpc(data), data.ProcessID, data.ThreadID,
        data.MethodNamespace, data.MethodName, data.MethodSignature, data.MethodID
    });
    source.Clr.MethodLoadVerbose += data => events.Add(new
    {
        Kind = "load", Milliseconds = data.TimeStampRelativeMSec, TimeStampQPC = ReadQpc(data), data.ProcessID, data.ThreadID,
        data.MethodNamespace, data.MethodName, data.MethodSignature, data.MethodID,
        Payload = data.PayloadNames.ToDictionary(name => name, name => data.PayloadByName(name)?.ToString())
    });
    source.Dynamic.All += data =>
    {
        if (data.ProviderName == "Dekaf-AdminEvidence-Phases")
            events.Add(new { Kind = "phase", Milliseconds = data.TimeStampRelativeMSec, TimeStampQPC = ReadQpc(data), data.ProcessID,
                data.ThreadID, Name = data.PayloadByName("name") });
    };
    source.Process();
    Console.WriteLine($"Trace duration: {source.SessionDuration.TotalSeconds:F3}s; events: {events.Count}; lost: {source.EventsLost}");
    if (source.EventsLost != 0) throw new InvalidOperationException("Trace lost events.");
}
File.WriteAllText(args[1], JsonSerializer.Serialize(events));

static long ReadQpc(TraceEvent data)
{
    // Relative trace time cannot be compared with the BDN host's Stopwatch clock.
    // Preserve the raw counter for that cross-process boundary correlation.
#pragma warning disable CS0618
    return data.TimeStampQPC;
#pragma warning restore CS0618
}
