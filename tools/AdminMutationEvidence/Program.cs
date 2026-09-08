using System.Globalization;
using Dekaf.Benchmarks;

if (args.Length != 5 || args[0] is not ("validate" or "probe"))
    throw new ArgumentException("validate|probe CASE OUTPUT WARMUP_SECONDS MEASURED_SECONDS");
var warmupSeconds = double.Parse(args[3], CultureInfo.InvariantCulture);
var measuredSeconds = double.Parse(args[4], CultureInfo.InvariantCulture);
using var compilations = new CompilationLog(Path.Combine(args[2], "compilations.json"));
PhaseEvents.Log.Phase("initialize");
compilations.Phase("initialize");
await using var fixture = new AdminFixture(args[1]);
await fixture.InitializeAsync();
Probe.SaveLoadedBinaries(Path.Combine(args[2], "binaries.json"));
if (args[0] == "validate")
{
    // Validate every observed outcome and request shape outside the timed loop.
    for (var i = 0; i < 8; i++) await fixture.ValidateAsync();
    Console.WriteLine($"Validated {args[1]}");
    return;
}
PhaseEvents.Log.Phase("primer");
compilations.Phase("primer");
await Probe.PrimeAsync(fixture, Path.Combine(args[2], "primer.json"));
PhaseEvents.Log.Phase("warmup");
compilations.Phase("warmup");
var warmupCapture = await Probe.CaptureAsync(fixture, warmupSeconds);
PhaseEvents.Log.Phase("measured");
compilations.Phase("measured");
var measuredCapture = await Probe.CaptureAsync(fixture, measuredSeconds);
PhaseEvents.Log.Phase("finalize");
compilations.Phase("finalize");
var warmup = Probe.Complete(warmupCapture);
var measured = Probe.Complete(measuredCapture);
await fixture.ValidateAsync();
Probe.Save(Path.Combine(args[2], "warmup.json"), warmup);
Probe.Save(Path.Combine(args[2], "measured.json"), measured);
Console.WriteLine($"{args[1]} warmup={warmup.Seconds:F3}s/{warmup.Completed} completed calls; measured={measured.Seconds:F3}s/{measured.Completed} completed calls");
