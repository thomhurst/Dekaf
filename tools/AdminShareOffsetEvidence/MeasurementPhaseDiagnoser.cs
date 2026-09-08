using System.Diagnostics;
using System.Text.Json;
using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;

namespace Dekaf.Benchmarks;

/// <summary>Records the host's actual-workload boundaries without per-iteration hooks.</summary>
public sealed class MeasurementPhaseDiagnoser : IDiagnoser
{
    public IEnumerable<string> Ids => [nameof(MeasurementPhaseDiagnoser)];
    public IEnumerable<IExporter> Exporters => [];
    public IEnumerable<IAnalyser> Analysers => [];
    public RunMode GetRunMode(BenchmarkCase benchmarkCase) => RunMode.NoOverhead;
    public IEnumerable<Metric> ProcessResults(DiagnoserResults results) => [];
    public IEnumerable<ValidationError> Validate(ValidationParameters validationParameters) => [];
    public void DisplayResults(ILogger logger) { }

    public void Handle(HostSignal signal, DiagnoserActionParameters parameters)
    {
        if (signal is not (HostSignal.BeforeActualRun or HostSignal.AfterActualRun)) return;
        var timestamp = Stopwatch.GetTimestamp();
        var utcClockBefore = Stopwatch.GetTimestamp();
        var utcTimestamp = DateTime.UtcNow;
        var utcClockAfter = Stopwatch.GetTimestamp();
        var processId = parameters.Process.Id;
        var testCase = (string)parameters.BenchmarkCase.Parameters.Items
            .Single(static parameter => parameter.Name == nameof(AdminEvidenceBenchmark.Case)).Value;
        var output = Environment.GetEnvironmentVariable("ADMIN_EVIDENCE_WARMUP_DIRECTORY") ?? Path.GetTempPath();
        Directory.CreateDirectory(output);
        var row = new { Signal = signal.ToString(), Timestamp = timestamp,
            StopwatchFrequency = Stopwatch.Frequency, ProcessId = processId,
            UtcTimestamp = utcTimestamp, UtcClockBefore = utcClockBefore, UtcClockAfter = utcClockAfter,
            Benchmark = parameters.BenchmarkCase.DisplayInfo };
        File.AppendAllText(Path.Combine(output, $"signals-{testCase.Replace(':', '-')}-{processId}.jsonl"),
            JsonSerializer.Serialize(row) + Environment.NewLine);
    }
}
