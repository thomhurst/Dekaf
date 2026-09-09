using Microsoft.Diagnostics.Tracing.Etlx;
using System.Text.Json;

if (args.Length != 3) throw new ArgumentException("trace.nettrace known.json result.json");
var etlx = TraceLog.CreateFromEventPipeDataFile(args[0]);
using var log = new TraceLog(etlx);
var known = JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
var boundaries = new Dictionary<string, double>();
var ticks = new List<(double Time, long Bytes, int Heap, int Kind)>();
var heapHistory = new List<(double Time, int Heaps)>();
foreach (var entry in log.Events)
{
    if (entry.ProviderName == "Dekaf-AllocationCounterCalibration" && entry.EventName == "Phase")
        boundaries.Add((string)entry.PayloadByName("name"), entry.TimeStampRelativeMSec);
    if (entry.ProviderName == "Microsoft-Windows-DotNETRuntime" && entry.EventName.StartsWith("GC/AllocationTick", StringComparison.Ordinal))
        ticks.Add((entry.TimeStampRelativeMSec, Convert.ToInt64(entry.PayloadByName("AllocationAmount64")),
            Convert.ToInt32(entry.PayloadByName("HeapIndex")), Convert.ToInt32(entry.PayloadByName("AllocationKind"))));
    if (entry.ProviderName == "Microsoft-Windows-DotNETRuntime" && entry.EventName.StartsWith("GC/GlobalHeapHistory", StringComparison.Ordinal))
        heapHistory.Add((entry.TimeStampRelativeMSec, Convert.ToInt32(entry.PayloadByName("NumHeaps"))));
}
if (!boundaries.TryGetValue("start", out var start) || !boundaries.TryGetValue("end", out var end) || end <= start)
    throw new InvalidOperationException("Missing or invalid calibration phase boundaries.");
var measured = ticks.Where(tick => tick.Time >= start && tick.Time < end).ToArray();
var total = measured.Sum(tick => tick.Bytes);
var expected = known.GetProperty("KnownWorkerAllocatedBytes").GetInt64();
var error = (double)total / expected - 1;
var result = new
{
    Verdict = log.EventsLost == 0 && measured.Length > 0 && Math.Abs(error) <= .01 ? "CALIBRATION_PASS" : "CALIBRATION_FAIL",
    Scope = "Local counter calibration with a predeclared 1% agreement check; not product performance acceptance or a general error bound",
    log.EventsLost, Events = measured.Length, EventAllocatedBytes = total,
    KnownWorkerAllocatedBytes = expected, RelativeError = error,
    GlobalAllocatedDelta = known.GetProperty("GlobalAllocatedDelta").GetInt64(),
    PhaseMilliseconds = end - start,
    HeapHistory = heapHistory.Select(item => new { item.Time, item.Heaps }),
    ByHeapAndKind = measured.GroupBy(tick => (tick.Heap, tick.Kind)).Select(group =>
        new { group.Key.Heap, group.Key.Kind, Events = group.Count(), Bytes = group.Sum(tick => tick.Bytes) })
};
File.WriteAllText(args[2], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(result));
if (result.Verdict != "CALIBRATION_PASS") Environment.ExitCode = 1;
