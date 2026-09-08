using System.Text.Json;
using Microsoft.Diagnostics.Tracing;

if (args.Length != 2) throw new ArgumentException("TRACE OUTPUT_JSON");
var events = new List<object>();
using var source = new EventPipeEventSource(args[0]);
source.Clr.MethodJittingStarted += data => events.Add(new
{
    kind = "jit", milliseconds = data.TimeStampRelativeMSec, data.ThreadID,
    data.MethodNamespace, data.MethodName, data.MethodSignature, data.MethodID
});
source.Clr.MethodLoadVerbose += data => events.Add(new
{
    kind = "load", milliseconds = data.TimeStampRelativeMSec, data.ThreadID,
    data.MethodNamespace, data.MethodName, data.MethodSignature, data.MethodID,
    payload = data.PayloadNames.ToDictionary(name => name, name => data.PayloadByName(name)?.ToString())
});
source.Clr.GCStart += data => events.Add(new
{
    kind = "gc", milliseconds = data.TimeStampRelativeMSec, data.ThreadID,
    data.Depth, reason = data.Reason.ToString(), data.Count
});
source.Clr.GCSuspendEEStart += data => events.Add(new
{
    kind = "suspend", milliseconds = data.TimeStampRelativeMSec, data.ThreadID,
    reason = data.Reason.ToString()
});
source.Clr.GCRestartEEStop += data => events.Add(new
{
    kind = "restart", milliseconds = data.TimeStampRelativeMSec, data.ThreadID
});
source.Clr.LoaderModuleLoad += data => events.Add(new
{
    kind = "module", milliseconds = data.TimeStampRelativeMSec, data.ThreadID,
    data.ModuleID, data.ModuleILPath
});
source.Dynamic.All += data =>
{
    if (data.ProviderName == "BenchmarkDotNet.EngineEventSource")
        events.Add(new
        {
            kind = "engine", milliseconds = data.TimeStampRelativeMSec, data.ThreadID,
            id = (int)data.ID, data.EventName,
            payload = data.PayloadNames.ToDictionary(name => name, name => data.PayloadByName(name)?.ToString())
        });
};
source.Process();
var result = new { seconds = source.SessionDuration.TotalSeconds, lost = source.EventsLost, events };
File.WriteAllText(args[1], JsonSerializer.Serialize(result));
Console.WriteLine($"Trace seconds={result.seconds:F6}; events={events.Count}; lost={result.lost}");
if (result.lost != 0) throw new InvalidOperationException("Trace lost events; all parsed output retained.");

